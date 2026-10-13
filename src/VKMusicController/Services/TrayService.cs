using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using VKMusicController.Models;

namespace VKMusicController.Services
{
    public class TrayService : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly OverlayService _overlayService;
        private readonly MediaSessionService _mediaService;
        private readonly HotkeyService _hotkeyService;
        private readonly SettingsService _settingsService;
        private readonly StartupService _startupService;
        private readonly LoggerService _logger;
        private readonly NotifyIcon _notifyIcon;

        public TrayService(AppSettings settings, OverlayService overlayService, MediaSessionService mediaService, HotkeyService hotkeyService, SettingsService settingsService, StartupService startupService, LoggerService logger)
        {
            _settings = settings;
            _overlayService = overlayService;
            _mediaService = mediaService;
            _hotkeyService = hotkeyService;
            _settingsService = settingsService;
            _startupService = startupService;
            _logger = logger;
            _notifyIcon = new NotifyIcon();
        }

        public void Initialize()
        {
            _notifyIcon.Icon = SystemIcons.Application;
            _notifyIcon.Visible = true;
            _notifyIcon.Text = "PlayerForGame";
            _notifyIcon.DoubleClick += OnDoubleClick;

            var menu = new ContextMenuStrip();
            menu.Items.Add("Открыть плеер", null, (_, _) => ((App)System.Windows.Application.Current).ShowMainWindow());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Show Overlay", null, (_, _) => _overlayService.ShowOverlay());
            menu.Items.Add("Hide Overlay", null, (_, _) => _overlayService.HideOverlay());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Play/Pause", null, async (_, _) => await _mediaService.TogglePlayPauseAsync());
            menu.Items.Add("Next Track", null, async (_, _) => await _mediaService.NextTrackAsync());
            menu.Items.Add("Previous Track", null, async (_, _) => await _mediaService.PreviousTrackAsync());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Settings", null, (_, _) => OpenSettings());
            var startupItem = new ToolStripMenuItem("Start with Windows") { Checked = _settings.StartWithWindows };
            startupItem.Click += (_, _) => ToggleStartup(startupItem);
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, (_, _) => System.Windows.Application.Current.Shutdown());

            _notifyIcon.ContextMenuStrip = menu;
            _logger.LogInfo("System tray initialized.");
        }

        private async void ToggleStartup(ToolStripMenuItem item)
        {
            _settings.StartWithWindows = !_settings.StartWithWindows;
            item.Checked = _settings.StartWithWindows;
            _startupService.UpdateStartupRegistration(_settings.StartWithWindows);
            await _settingsService.SaveAsync().ConfigureAwait(false);
        }

        private void OpenSettings()
        {
            var window = new Views.SettingsWindow
            {
                DataContext = new ViewModels.SettingsViewModel(_settingsService, _startupService, _overlayService)
            };
            window.ShowDialog();
        }

        private void OnDoubleClick(object? sender, EventArgs e)
        {
            ((App)System.Windows.Application.Current).ShowMainWindow();
        }

        public void ShowVolumeError(string message)
        {
            _notifyIcon.ShowBalloonTip(5000, "Громкость VK", message, ToolTipIcon.Info);
        }

        public void Dispose()
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }
}
