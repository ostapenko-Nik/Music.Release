using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using VKMusicController.Services;

using var bridge = new VolumeService(new LoggerService());
await bridge.InitializeAsync();
Check(!(await bridge.ExecuteAsync("up")).Success, "no extension returns useful failure");
Check(!(await bridge.ExecuteAsync("bad")).Success, "unknown actions rejected");
using var http = new HttpClient();
Check((await http.GetAsync($"http://127.0.0.1:{VolumeService.Port}/vk-volume")).StatusCode == HttpStatusCode.Forbidden,
    "ordinary HTTP requests rejected");
using (var rejected = new ClientWebSocket())
{
    rejected.Options.SetRequestHeader("Origin", "https://vk.com");
    try
    {
        await rejected.ConnectAsync(new Uri($"ws://127.0.0.1:{VolumeService.Port}/vk-volume"), CancellationToken.None);
        throw new Exception("Website origin accepted!");
    }
    catch (WebSocketException) { Console.WriteLine("PASS: website WebSocket origin rejected"); }
}

using var browserA = await Browser.ConnectAsync(playing: false, volume: 0.4, tabId: 10);
using var browserB = await Browser.ConnectAsync(playing: true, volume: 0.8, tabId: 20);
var result = await bridge.ExecuteAsync("down");
Check(result.Success && result.Percent == 75, "real WebSocket command/response reports VK volume");
Check(browserA.Applied == 0 && browserB.Applied == 1, "only playing browser receives mutation");
await Task.WhenAll(bridge.ExecuteAsync("up"), bridge.ExecuteAsync("up"));
Check(browserB.Applied == 3 && Math.Abs(browserB.Volume - 0.85) < 0.001, "rapid hotkeys serialized without lost updates");
browserA.Playing = true;
Check(!(await bridge.ExecuteAsync("up")).Success && browserB.Applied == 3 && browserA.Applied == 0,
    "multiple playing browsers cause no mutations");
browserA.Playing = false;
browserB.Ambiguous = true;
Check(!(await bridge.ExecuteAsync("mute")).Success && browserB.Applied == 3, "ambiguous tabs cause no mutations");
browserB.Ambiguous = false;
browserA.Found = false;
browserB.Found = false;
Check(!(await bridge.ExecuteAsync("up")).Success, "no VK player causes no mutation");
browserB.Found = true;
browserB.IgnoreCommands = true;
Check(!(await bridge.ExecuteAsync("up")).Success && browserB.Applied == 3, "unresponsive extension times out safely");
browserB.IgnoreCommands = false;
Check((await bridge.ExecuteAsync("down")).Success, "bridge recovers after command timeout");
Console.WriteLine("All bridge checks passed.");

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}

sealed class Browser : IDisposable
{
    private readonly ClientWebSocket _socket = new();
    private readonly CancellationTokenSource _stop = new();
    public bool Playing { get; set; }
    public bool Found { get; set; } = true;
    public bool Ambiguous { get; set; }
    public bool IgnoreCommands { get; set; }
    public double Volume { get; private set; }
    public int Applied { get; private set; }
    private int _tabId;

    public static async Task<Browser> ConnectAsync(bool playing, double volume, int tabId)
    {
        var browser = new Browser { Playing = playing, Volume = volume, _tabId = tabId };
        browser._socket.Options.SetRequestHeader("Origin", "chrome-extension://" + new string('a', 32));
        await browser._socket.ConnectAsync(new Uri($"ws://127.0.0.1:{VolumeService.Port}/vk-volume"), CancellationToken.None);
        _ = browser.RespondAsync();
        return browser;
    }

    private async Task RespondAsync()
    {
        var buffer = new byte[8192];
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var received = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _stop.Token);
                if (received.MessageType == WebSocketMessageType.Close) return;
                var request = JsonDocument.Parse(buffer.AsMemory(0, received.Count));
                using (request)
                {
                    if (IgnoreCommands) continue;
                    var root = request.RootElement;
                    var id = root.GetProperty("id").GetString();
                    if (root.GetProperty("command").GetString() == "apply")
                    {
                        if (root.GetProperty("tabId").GetInt32() != _tabId) throw new Exception("Wrong tab mutated!");
                        Applied++;
                        Volume = Math.Clamp(Volume + (root.GetProperty("action").GetString() == "up" ? 0.05 : -0.05), 0, 1);
                    }
                    var reply = JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        id, found = Found, playing = Playing, ambiguous = Ambiguous,
                        tabId = _tabId, ok = true, volume = Volume
                    });
                    // Exercise fragmented WebSocket replies.
                    var split = reply.Length / 2;
                    await _socket.SendAsync(new ArraySegment<byte>(reply, 0, split), WebSocketMessageType.Text, false, _stop.Token);
                    await _socket.SendAsync(new ArraySegment<byte>(reply, split, reply.Length - split), WebSocketMessageType.Text, true, _stop.Token);
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or WebSocketException or ObjectDisposedException) { }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _socket.Dispose();
    }
}
