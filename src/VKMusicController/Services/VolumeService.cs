using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace VKMusicController.Services;

// Commands go to VK's page player. This service never calls Windows audio APIs.
public sealed class VolumeService : IDisposable
{
    public const int Port = 43821;
    private readonly LoggerService _logger;
    private readonly ConcurrentDictionary<Guid, BrowserConnection> _connections = new();
    private readonly SemaphoreSlim _commands = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private WebApplication? _server;
    private string? _startupError;

    public VolumeService(LoggerService logger) => _logger = logger;

    public async Task InitializeAsync()
    {
        try
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, Port));
            _server = builder.Build();
            _server.UseWebSockets();
            _server.Map("/vk-volume", HandleConnectionAsync);
            await _server.StartAsync(_shutdown.Token);
            _logger.LogInfo($"VK volume bridge listening on 127.0.0.1:{Port}.");
        }
        catch (Exception ex)
        {
            _startupError = $"Не удалось открыть локальное подключение VK (порт {Port}). Закройте вторую копию приложения и перезапустите его.";
            _logger.LogError("Unable to start VK volume bridge.", ex);
        }
    }

    private async Task HandleConnectionAsync(HttpContext context)
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (!context.WebSockets.IsWebSocketRequest ||
            !Regex.IsMatch(origin, "^chrome-extension://[a-p]{32}$", RegexOptions.CultureInvariant))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        using var connection = new BrowserConnection(socket);
        var id = Guid.NewGuid();
        _connections[id] = connection;
        _logger.LogInfo("VK volume extension connected.");
        try { await connection.ReceiveAsync(_shutdown.Token); }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or JsonException)
        {
            _logger.LogInfo("VK volume extension disconnected.");
        }
        finally { _connections.TryRemove(id, out _); }
    }

    public async Task<VolumeResult> ExecuteAsync(string action)
    {
        if (action is not ("up" or "down" or "mute"))
            return new(false, "Неизвестная команда громкости.");
        try { await _commands.WaitAsync(_shutdown.Token); }
        catch (OperationCanceledException) { return new(false, "Приложение закрывается."); }
        try
        {
            if (_startupError != null)
                return new(false, _startupError);
            var connections = _connections.Values.ToArray();
            if (connections.Length == 0)
                return new(false, "Для громкости VK установите расширение из папки BrowserExtension рядом с приложением. Инструкция находится в настройках.");
            var replies = await Task.WhenAll(connections.Select(async connection =>
                (Connection: connection, Reply: await connection.RequestAsync("probe", null, null, _shutdown.Token))));
            var candidates = replies.Where(item => item.Reply is { Found: true, TabId: >= 0 }).ToArray();
            if (candidates.Count(item => item.Reply!.Playing) > 1 || replies.Any(item => item.Reply?.Ambiguous == true))
                return new(false, "Музыка играет в нескольких вкладках VK. Оставьте воспроизведение в одной вкладке.");
            var selected = candidates.OrderByDescending(item => item.Reply!.Playing)
                .ThenByDescending(item => item.Reply!.Active).FirstOrDefault();
            if (selected.Connection == null)
                return new(false, "Откройте VK Музыку и запустите трек. Доступный музыкальный плеер VK не найден.");
            var result = await selected.Connection.RequestAsync("apply", selected.Reply!.TabId, action, _shutdown.Token);
            if (result is not { Ok: true, Volume: >= 0 and <= 1 })
                return new(false, "Плеер VK не ответил на изменение громкости. Обновите вкладку VK и попробуйте снова.");
            var percent = (int)Math.Round(result.Volume.Value * 100);
            return new(true, $"Громкость VK: {percent}%", percent);
        }
        catch (Exception ex)
        {
            _logger.LogError("VK volume command failed.", ex);
            return new(false, "Не удалось изменить громкость VK. Проверьте подключение расширения.");
        }
        finally { _commands.Release(); }
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        foreach (var connection in _connections.Values)
            connection.Dispose();
        if (_server != null)
            _ = StopServerAsync(_server);
    }

    private static async Task StopServerAsync(WebApplication server)
    {
        try { await server.StopAsync(); }
        finally { await server.DisposeAsync(); }
    }

    private sealed class BrowserConnection(WebSocket socket) : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly ConcurrentDictionary<string, TaskCompletionSource<BrowserReply>> _pending = new();
        private readonly SemaphoreSlim _send = new(1, 1);

        public async Task<BrowserReply?> RequestAsync(string command, int? tabId, string? action, CancellationToken cancellation)
        {
            var id = Guid.NewGuid().ToString("N");
            var completion = new TaskCompletionSource<BrowserReply>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[id] = completion;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            try
            {
                await SendAsync(new { id, command, tabId, action }, timeout.Token);
                return await completion.Task.WaitAsync(timeout.Token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or ObjectDisposedException)
            {
                return null;
            }
            finally { _pending.TryRemove(id, out _); }
        }

        private async Task SendAsync(object message, CancellationToken cancellation)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
            await _send.WaitAsync(cancellation);
            try { await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellation); }
            finally { _send.Release(); }
        }

        public async Task ReceiveAsync(CancellationToken cancellation)
        {
            var buffer = new byte[16 * 1024];
            while (socket.State == WebSocketState.Open && !cancellation.IsCancellationRequested)
            {
                var count = 0;
                WebSocketReceiveResult received;
                do
                {
                    received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, count, buffer.Length - count), cancellation);
                    if (received.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellation);
                        return;
                    }
                    if (received.MessageType != WebSocketMessageType.Text)
                        return;
                    count += received.Count;
                    if (count == buffer.Length && !received.EndOfMessage)
                        return;
                } while (!received.EndOfMessage);
                var reply = JsonSerializer.Deserialize<BrowserReply>(buffer.AsSpan(0, count), JsonOptions);
                if (reply?.Type == "ping")
                    await SendAsync(new { type = "pong" }, cancellation);
                else if (reply?.Id != null && _pending.TryGetValue(reply.Id, out var completion))
                    completion.TrySetResult(reply);
            }
        }

        public void Dispose()
        {
            foreach (var completion in _pending.Values)
                completion.TrySetCanceled();
            socket.Dispose();
        }
    }

    private sealed class BrowserReply
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
        public bool Found { get; set; }
        public bool Playing { get; set; }
        public bool Active { get; set; }
        public bool Ambiguous { get; set; }
        public int? TabId { get; set; }
        public bool Ok { get; set; }
        public double? Volume { get; set; }
    }
}

public sealed record VolumeResult(bool Success, string Message, int? Percent = null);
