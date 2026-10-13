using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VKMusicController;
using VKMusicController.Models;
using VKMusicController.Services;
using VKMusicController.ViewModels;
using VKMusicController.Views;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Use a plain WPF host: constructing App schedules the real startup workflow.
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/VKMusicController;component/Views/DesignResources.xaml")
        });
        var windowStyle = new Style(typeof(Window));
        windowStyle.Setters.Add(new Setter(Control.FontFamilyProperty, new FontFamily("Segoe UI")));
        windowStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12d));
        app.Resources[typeof(Window)] = windowStyle;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        var logger = new LoggerService();
        var settings = new SettingsService(logger);
        var startup = new StartupService(logger);
        var provider = new FakeProvider();
        using var overlay = new OverlayService(settings, provider, logger);
        overlay.Initialize();
        var overlayWindow = Field<OverlayWindow>(overlay, "_window");
        var vm = (OverlayViewModel)overlayWindow.DataContext;
        overlayWindow.ShowActivated = false;
        overlayWindow.Topmost = false;
        overlayWindow.Opacity = 0;
        overlayWindow.Show();
        SetTrack(5, 320);

        for (var second = 0; second <= 320; second++)
        {
            Seek(second);
            Check(provider.LastSeek == second && vm.PositionSeconds == second, $"Seek to {second} seconds", quiet: true);
        }
        Check(provider.Requests == 321, "every second from start to end sends one seek request");
        Seek(-10); Check(provider.LastSeek == 0, "negative positions clamp to track start");
        Seek(1000); Check(provider.LastSeek == 320, "positions beyond track clamp to its end");
        Tick(); Check(vm.PositionSeconds == 320, "stale browser timeline does not undo an accepted seek");
        provider.Position = 210; Tick();
        Check(vm.PositionSeconds == 210, "external browser seek updates the overlay");

        provider.AcceptSeek = false;
        Seek(12);
        Check(vm.PositionSeconds == 210 && vm.StatusText == "Ошибка перемотки", "rejected seek restores actual position and reports failure");
        provider.AcceptSeek = true;
        provider.PendingSeek = new TaskCompletionSource<bool>();
        Seek(90);
        Check(!vm.CanSeek, "concurrent seek disabled while awaiting browser");
        Tick(); Check(vm.PositionSeconds == 90, "timer does not overwrite pending seek preview");
        provider.PendingSeek.SetResult(true); Drain(); provider.PendingSeek = null;
        Check(vm.PositionSeconds == 90 && vm.CanSeek, "accepted asynchronous seek completes and re-enables slider");

        provider.PendingSeek = new TaskCompletionSource<bool>();
        Seek(150);
        SetTrack(8, 240);
        provider.PendingSeek.SetResult(true); Drain(); provider.PendingSeek = null;
        Check(vm.PositionSeconds == 8 && vm.DurationSeconds == 240, "old seek completion cannot overwrite a new track");
        provider.Position = 8;
        vm.IsSeeking = true; vm.PreviewSeek(79); Tick();
        Check(vm.PositionSeconds == 79, "timer does not overwrite position while dragging");
        vm.CommitSeek();
        Check(provider.LastSeek == 79, "releasing drag commits preview position");

        var slider = (Slider)overlayWindow.FindName("SeekSlider");
        vm.IsSeeking = true;
        slider.SetCurrentValue(RangeBase.ValueProperty, 123d);
        vm.CommitSeek();
        Check(provider.LastSeek == 123, "real slider ValueChanged is wired to seek request");
        vm.IsSeeking = true;
        Slider.IncreaseSmall.Execute(null, slider);
        vm.CommitSeek();
        Check(provider.LastSeek == 124, "keyboard slider command advances exactly one second");
        vm.IsSeeking = true;
        Slider.DecreaseSmall.Execute(null, slider);
        vm.CommitSeek();
        Check(provider.LastSeek == 123, "keyboard slider command goes back exactly one second");

        var mouse = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = Mouse.PreviewMouseDownEvent, Source = slider };
        slider.RaiseEvent(mouse);
        slider.SetCurrentValue(RangeBase.ValueProperty, 57d);
        slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = Mouse.PreviewMouseUpEvent, Source = slider });
        Check(provider.LastSeek == 57 && !vm.IsSeeking, $"mouse down/change/up commits through actual routed handlers (requested={provider.LastSeek}, preview={vm.PositionSeconds}, seeking={vm.IsSeeking})");
        provider.CanSeek = false; Tick();
        Check(!vm.CanSeek && !slider.IsEnabled, "unsupported source disables seeking");
        var requests = provider.Requests;
        Seek(10); Check(provider.Requests == requests, "disabled seeking sends no request");
        provider.CanSeek = true;
        SetTrack(0, 0); Check(!vm.CanSeek && slider.Maximum == 0, "unknown duration disables slider");
        overlay.UpdateTrack(null); Drain();
        Check(!vm.CanSeek && vm.PositionSeconds == 0, "missing track clears timeline");

        Check(!StartupService.IsBackgroundLaunch(Array.Empty<string>()), "manual launch shows main menu");
        Check(StartupService.IsBackgroundLaunch(new[] { "--startup" }), "Windows startup selects background mode");
        Check(StartupService.IsBackgroundLaunch(new[] { "--BACKGROUND" }), "explicit background mode is case insensitive");
        var buildCommand = typeof(StartupService).GetMethod("BuildStartupCommand", BindingFlags.Static | BindingFlags.NonPublic)!;
        Check((string)buildCommand.Invoke(null, new object[] { @"C:\Apps With Spaces\Player.exe", "unused" })! ==
            "\"C:\\Apps With Spaces\\Player.exe\" --startup", "published executable autostart is quoted and silent");
        Check((string)buildCommand.Invoke(null, new object[] { @"C:\Program Files\dotnet\dotnet.exe", @"C:\Music Player\VKMusicController.dll" })! ==
            "\"C:\\Program Files\\dotnet\\dotnet.exe\" \"C:\\Music Player\\VKMusicController.dll\" --startup", "framework-dependent autostart includes assembly path");

        foreach (var size in new[] { new Size(520, 190), new Size(760, 280) })
        for (var flags = 0; flags < 32; flags++)
        {
            var appearance = new AppSettings { ShowArtwork = (flags & 1) != 0, ShowArtist = (flags & 2) != 0,
                ShowAlbum = (flags & 4) != 0, ShowProgress = (flags & 8) != 0, ShowControls = (flags & 16) != 0 };
            var layoutVm = new OverlayViewModel(appearance) { Title = "Очень длинное название композиции для проверки обрезания текста",
                Artist = "Очень длинное имя исполнителя", Album = "Альбом с длинным названием", CanSeek = true };
            layoutVm.UpdateTimeline(100, 320);
            var window = new OverlayWindow { DataContext = layoutVm };
            var content = Layout(window, size);
            CheckBounds(content, content);
            window.Close();
        }
        Check(true, "64 overlay layouts fit at minimum and maximum sizes");

        var notificationTrack = new MediaTrackInfo { Title = "Смерть луны", Artist = "Asper X", IsPlaying = true };
        var notification = new TrackNotificationWindow(notificationTrack);
        var notificationContent = Layout(notification, new Size(400,140));
        CheckBounds(notificationContent, notificationContent, 16);
        Check(!notification.ShowActivated, "track notification does not steal focus from the game");
        foreach (var sample in new[] {
            new MediaTrackInfo { Title = "Очень длинное название трека с участием нескольких исполнителей", Artist = "Очень длинное имя исполнителя и приглашённые музыканты" },
            new MediaTrackInfo { Title = "Без обложки и исполнителя", Artist = "" },
            new MediaTrackInfo { Title = "Повреждённая обложка", Artist = "Исполнитель", ArtworkData = new byte[] { 1,2,3 } }
        })
        {
            var toast = new TrackNotificationWindow(sample);
            var toastContent = Layout(toast, new Size(400,140));
            CheckBounds(toastContent, toastContent, 16);
            toast.Close();
        }
        Check(true, "notifications fit long titles and missing or invalid artwork");
        notification.Opacity = 0;
        notification.Show();
        var windowCount = app.Windows.Count;
        foreach (var name in new[] { "NotificationCard", "TrackTitle", "TrackArtist" })
            ((UIElement)notification.FindName(name)).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
        Check(notification.IsVisible && app.Windows.Count == windowCount,
            "clicks on notification card, title and artist neither navigate nor dismiss it");
        notification.Close();
        var dismiss = new TrackNotificationWindow(notificationTrack);
        dismiss.Opacity = 0; dismiss.Show();
        ((Button)dismiss.FindName("DismissButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        Check(!dismiss.IsVisible, "dismiss button still closes the non-clickable notification");
        using (var iconStream = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/VKMusicController;component/Assets/app.ico")).Stream)
        {
            var icon = new IconBitmapDecoder(iconStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            Check(icon.Frames.Count == 7 && icon.Frames.Any(frame => frame.PixelWidth == 16) && icon.Frames.Any(frame => frame.PixelWidth == 256),
                "embedded music note icon contains all seven resolutions");
        }
        NotificationService.ShowTrackNotification(notificationTrack, 10); Drain();
        var firstToast = app.Windows.OfType<TrackNotificationWindow>().Single();
        NotificationService.ShowTrackNotification(notificationTrack, 1); Drain();
        var secondToast = app.Windows.OfType<TrackNotificationWindow>().Single();
        Check(!firstToast.IsVisible && secondToast.IsVisible && !ReferenceEquals(firstToast, secondToast), "new track replaces old notification");
        var timeoutFrame = new DispatcherFrame();
        var timeoutTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        timeoutTimer.Tick += (_, _) => { timeoutTimer.Stop(); timeoutFrame.Continue = false; };
        timeoutTimer.Start(); Dispatcher.PushFrame(timeoutFrame);
        Check(!secondToast.IsVisible, "notification closes automatically after configured duration");
        Console.WriteLine($"Verified player assembly: {typeof(TrackNotificationWindow).Assembly.Location}; version {typeof(TrackNotificationWindow).Assembly.GetName().Version}");

        MediaTrackInfo? liveTrack = null;
        if (args.Contains("--probe-media"))
        {
            using var liveProvider = new MediaSessionService(logger);
            var initialization = liveProvider.InitializeAsync();
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!initialization.IsCompleted && DateTime.UtcNow < deadline) { Drain(); Thread.Sleep(10); }
            if (initialization.IsCompleted)
            {
                initialization.GetAwaiter().GetResult();
                while (liveProvider.CurrentTrack == null && DateTime.UtcNow < deadline) { Drain(); Thread.Sleep(10); }
                liveTrack = liveProvider.CurrentTrack;
                Console.WriteLine($"Read-only live media probe: session={liveProvider.HasMediaSession}, seekSupported={liveProvider.CanSeek}, duration={liveProvider.GetCurrentDuration().TotalSeconds:F0}s");
            }
            else Console.WriteLine("Live media probe did not complete within 5 seconds.");
        }

        if (args.Length > 0)
        {
            var directory = args[0]; Directory.CreateDirectory(directory);
            var media = new MediaSessionService(logger);
            var main = new MainWindow(overlay, media, settings, startup);
            typeof(MainWindow).GetMethod("UpdateNowPlaying", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main,
                new object[] { new MediaTrackInfo { Title = "ЧСВ", Artist = "Lida, Серега Пират", IsPlaying = true } });
            var image = (Image)main.FindName("NowPlayingImage"); image.Source = MakeArtwork();
            Save(main, new Size(1000, 680), Path.Combine(directory, "main-menu.png"));
            Save(main, new Size(900, 640), Path.Combine(directory, "main-menu-compact.png"));
            var preferences = new SettingsWindow { DataContext = new SettingsViewModel(settings, startup, overlay) };
            var tabs = (TabControl)preferences.FindName("SettingsTabs");
            var names = new[] { "general", "overlay", "hotkeys", "extension" };
            for (var tab = 0; tab < 4; tab++)
            {
                tabs.SelectedIndex = tab;
                Save(preferences, new Size(1000, 760), Path.Combine(directory, $"settings-{names[tab]}.png"));
            }
            Save(preferences, new Size(900, 680), Path.Combine(directory, "settings-compact.png"));
            tabs.SelectedIndex = 0;
            ThemeService.Apply("Ocean"); Save(preferences, new Size(1000,760),Path.Combine(directory,"settings-ocean.png"));
            ThemeService.Apply("Nebula");
            provider.CanSeek = true; SetTrack(128, 320);
            vm.Title = "Закричу на весь мир"; vm.Artist = "Танисия"; vm.ArtworkImage = MakeArtwork();
            vm.PlaybackState = PlaybackState.Playing;
            Save(overlayWindow, new Size(520,190), Path.Combine(directory,"overlay-seek.png"));
            var notificationPreview = new TrackNotificationWindow(notificationTrack);
            ((Image)notificationPreview.FindName("ArtworkImage")).Source = MakeArtwork();
            Save(notificationPreview, new Size(400,140), Path.Combine(directory,"notification-v1.1.2.png"));
            ThemeService.Apply("Ocean");
            Save(notificationPreview, new Size(400,140), Path.Combine(directory,"notification-ocean-v1.1.2.png"));
            ThemeService.Apply("Nebula"); notificationPreview.Close();
            if (liveTrack != null)
            {
                var savedSettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VKMusicController", "settings.json");
                var savedTheme = File.Exists(savedSettingsPath)
                    ? System.Text.Json.JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(savedSettingsPath))?.AppTheme
                    : "Nebula";
                ThemeService.Apply(savedTheme ?? "Nebula");
                var livePreview = new TrackNotificationWindow(liveTrack);
                Save(livePreview, new Size(400,140), Path.Combine(directory,"notification-current-v1.1.2.png"));
                livePreview.Close(); ThemeService.Apply("Nebula");
            }
            preferences.Close(); main.Close();
            Console.WriteLine($"Screenshots rendered from actual WPF windows: {directory}");
        }
        Console.WriteLine("All player UI and seeking checks passed.");

        void Seek(double position) { vm.IsSeeking = true; vm.PreviewSeek(position); vm.CommitSeek(); Drain(); }
        void Tick() => typeof(OverlayService).GetMethod("OnPositionTimerTick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(overlay, new object?[] { null, EventArgs.Empty });
        void SetTrack(double position, double duration)
        {
            provider.Position = position; provider.Duration = duration;
            overlay.UpdateTrack(new MediaTrackInfo { Title = "Track", PositionSeconds = position, DurationSeconds = duration }); Drain();
        }
    }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Drain() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void Check(bool condition, string name, bool quiet = false)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        if (!quiet) Console.WriteLine("PASS: " + name);
    }
    private static FrameworkElement Layout(Window window, Size size)
    {
        var element = (FrameworkElement)window.Content;
        if (window.FindName("RootBorder") is Border border) { border.Opacity = 1; border.RenderTransform = Transform.Identity; }
        element.Measure(size); element.Arrange(new Rect(size)); element.UpdateLayout(); Drain();
        return element;
    }
    private static void Save(Window window, Size size, string path)
    {
        var element = Layout(window, size);
        var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private static void CheckBounds(FrameworkElement root, DependencyObject node, double padding = 18)
    {
        if (node is UIElement ui && ui.Visibility != Visibility.Visible) return;
        if (node is FrameworkElement element && (node is TextBlock || node is Button || node is Slider))
        {
            var bounds = element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));
            Check(bounds.Left >= padding && bounds.Top >= padding && bounds.Right <= root.ActualWidth - padding && bounds.Bottom <= root.ActualHeight - padding,
                $"{node.GetType().Name} is within overlay bounds: {bounds}", quiet: true);
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) CheckBounds(root, VisualTreeHelper.GetChild(node, i), padding);
    }
    private static DrawingImage MakeArtwork()
    {
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(new LinearGradientBrush(Color.FromRgb(90, 68, 125), Color.FromRgb(37, 39, 67), 45), null, new RectangleGeometry(new Rect(0, 0, 144, 144))));
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(31,28,44)),new Pen(new SolidColorBrush(Color.FromRgb(158,135,196)),1),new EllipseGeometry(new Point(72,72),49,49)));
        foreach(var radius in new[]{42,35,28}) drawing.Children.Add(new GeometryDrawing(null,new Pen(new SolidColorBrush(Color.FromRgb(60,52,78)),1),new EllipseGeometry(new Point(72,72),radius,radius)));
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(198,182,250)),null,new EllipseGeometry(new Point(72,72),15,15)));
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(31,28,44)),null,new EllipseGeometry(new Point(72,72),3,3)));
        return new DrawingImage(drawing);
    }
}

internal sealed class FakeProvider : IMediaProvider
{
    public event EventHandler<MediaTrackInfo?>? TrackChanged { add { } remove { } }
    public event EventHandler<PlaybackState>? PlaybackStateChanged { add { } remove { } }
    public bool HasMediaSession => true;
    public bool CanSeek { get; set; } = true;
    public double Position { get; set; }
    public double Duration { get; set; } = 320;
    public double LastSeek { get; private set; }
    public int Requests { get; private set; }
    public bool AcceptSeek { get; set; } = true;
    public TaskCompletionSource<bool>? PendingSeek { get; set; }
    public TimeSpan GetCurrentPosition() => TimeSpan.FromSeconds(Position);
    public TimeSpan GetCurrentDuration() => TimeSpan.FromSeconds(Duration);
    public bool IsPlaying() => false;
    public Task<bool> SeekAsync(double positionSeconds) { LastSeek = positionSeconds; Requests++; return PendingSeek?.Task ?? Task.FromResult(AcceptSeek); }
    public Task InitializeAsync() => Task.CompletedTask;
    public Task TogglePlayPauseAsync() => Task.CompletedTask;
    public Task NextTrackAsync() => Task.CompletedTask;
    public Task PreviousTrackAsync() => Task.CompletedTask;
    public void Dispose() { }
}
