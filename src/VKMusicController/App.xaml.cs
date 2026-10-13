using System;
using System.Threading.Tasks;
using System.Windows;
using Application = System.Windows.Application;
using VKMusicController.Models;
using VKMusicController.Services;
using VKMusicController.ViewModels;

namespace VKMusicController
{
    public partial class App : Application
    {
        private SettingsService? _settingsService;
        private LoggerService? _logger;
        private HotkeyService? _hotkeyService;
        private MediaSessionService? _mediaService;
        private TrayService? _trayService;
        private OverlayService? _overlayService;
        private StartupService? _startupService;
        private VolumeService? _volumeService;
        private DateTime _lastVolumeError;
        private MainWindow? _mainWindow;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                _logger = new LoggerService();
                _logger.Initialize();
                _logger.LogInfo("Application starting.");

                _settingsService = new SettingsService(_logger);
                var settings = await _settingsService.LoadAsync();
                ThemeService.Apply(settings.AppTheme);
                var backgroundLaunch = StartupService.IsBackgroundLaunch(e.Args);
                _logger.LogInfo("Settings loaded.");

                _startupService = new StartupService(_logger);
                _startupService.UpdateStartupRegistration(settings.StartWithWindows);

                _mediaService = new MediaSessionService(_logger);
                _volumeService = new VolumeService(_logger);
                await _volumeService.InitializeAsync();

                _overlayService = new OverlayService(_settingsService, _mediaService, _logger);
                _overlayService.Initialize();

                _mediaService.TrackChanged += OnTrackChanged;
                _mediaService.PlaybackStateChanged += OnPlaybackStateChanged;
                await _mediaService.InitializeAsync();

                _hotkeyService = new HotkeyService(_logger);
                _hotkeyService.HotkeyPressed += OnHotkeyPressed;
                _hotkeyService.RegisterHotkeys(settings);
                _settingsService.SettingsSaved += (_, _) => Dispatcher.BeginInvoke(() =>
                    _hotkeyService.RegisterHotkeys(_settingsService.CurrentSettings));

                _trayService = new TrayService(settings, _overlayService, _mediaService, _hotkeyService, _settingsService, _startupService, _logger);
                _trayService.Initialize();

                if (!backgroundLaunch)
                    ShowMainWindow();

                if (!backgroundLaunch && settings.ShowSettingsOnLaunch)
                {
                    OpenSettingsWindow();
                }

                if (settings.ShowOverlayOnLaunch)
                {
                    _overlayService.ShowOverlay();
                }

                _logger.LogInfo("Application startup complete.");
            }
            catch (Exception ex)
            {
                _logger?.LogError("Startup failed.", ex);
                System.Windows.MessageBox.Show($"PlayerForGame failed to start:\n{ex.Message}", "Ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            _logger?.LogError("Unhandled dispatcher exception.", e.Exception);
            System.Windows.MessageBox.Show($"Произошла непредвиденная ошибка:\n{e.Exception.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                _logger?.LogError("Unhandled domain exception.", ex);
            }
        }

        private void OpenSettingsWindow()
        {
            var window = new Views.SettingsWindow
            {
                DataContext = new SettingsViewModel(_settingsService!, _startupService!, _overlayService!)
            };
            window.Show();
        }

        public void ShowMainWindow()
        {
            if (_overlayService == null || _mediaService == null || _settingsService == null || _startupService == null)
                return;

            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow(_overlayService, _mediaService, _settingsService, _startupService);
                MainWindow = _mainWindow;
                _mainWindow.Closed += (_, _) => _mainWindow = null;
            }
            _mainWindow.Show();
            if (_mainWindow.WindowState == WindowState.Minimized)
                _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        }

        private void OnTrackChanged(object? sender, MediaTrackInfo? track)
        {
            _logger?.LogInfo(track is null ? "Media session became unavailable." : $"Track changed: {track.Title} by {track.Artist}");
            _overlayService?.UpdateTrack(track);
            if (_settingsService?.CurrentSettings.ShowTrackChangeNotification == true && track != null)
            {
                NotificationService.ShowTrackNotification(track, _settingsService.CurrentSettings.NotificationDurationSeconds);
            }

        }

        private void OnPlaybackStateChanged(object? sender, PlaybackState state)
        {
            _logger?.LogInfo($"Playback state changed: {state}");
            _overlayService?.UpdatePlaybackState(state);
        }

        private async void OnHotkeyPressed(object? sender, HotkeyAction action)
        {
            if (_mediaService == null)
                return;

            switch (action)
            {
                case HotkeyAction.PlayPause:
                    await _mediaService.TogglePlayPauseAsync();
                    break;
                case HotkeyAction.NextTrack:
                    await _mediaService.NextTrackAsync();
                    break;
                case HotkeyAction.PreviousTrack:
                    await _mediaService.PreviousTrackAsync();
                    break;
                case HotkeyAction.ShowHideOverlay:
                    _overlayService?.ToggleOverlay();
                    break;
                case HotkeyAction.VolumeUp:
                    await ChangeVkVolumeAsync("up");
                    break;
                case HotkeyAction.VolumeDown:
                    await ChangeVkVolumeAsync("down");
                    break;
                case HotkeyAction.ToggleMute:
                    await ChangeVkVolumeAsync("mute");
                    break;
            }
        }

        private async Task ChangeVkVolumeAsync(string action)
        {
            if (_volumeService == null)
                return;
            var result = await _volumeService.ExecuteAsync(action);
            _logger?.LogInfo(result.Message);
            if (!result.Success && DateTime.UtcNow - _lastVolumeError > TimeSpan.FromSeconds(10))
            {
                _lastVolumeError = DateTime.UtcNow;
                _trayService?.ShowVolumeError(result.Message);
            }
        }

        public async void InvokeOverlayPrevious()
        {
            if (_mediaService != null)
            {
                await _mediaService.PreviousTrackAsync();
            }
        }

        public async void InvokeOverlayPlayPause()
        {
            if (_mediaService != null)
            {
                await _mediaService.TogglePlayPauseAsync();
            }
        }

        public async void InvokeOverlayNext()
        {
            if (_mediaService != null)
            {
                await _mediaService.NextTrackAsync();
            }
        }

        private void OnExit(object sender, ExitEventArgs e)
        {
            _logger?.LogInfo("Application exiting.");
            _trayService?.Dispose();
            _hotkeyService?.Dispose();
            _overlayService?.Dispose();
            _mediaService?.Dispose();
            _volumeService?.Dispose();
        }
    }
}
