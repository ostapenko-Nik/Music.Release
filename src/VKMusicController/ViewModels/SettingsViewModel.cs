using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using VKMusicController.Helpers;
using VKMusicController.Models;
using VKMusicController.Services;

namespace VKMusicController.ViewModels
{
    public class SettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly StartupService _startupService;
        private readonly OverlayService _overlayService;
        private readonly AppSettings _settings;

        public SettingsViewModel(SettingsService settingsService, StartupService startupService, OverlayService overlayService)
        {
            _settingsService = settingsService;
            _startupService = startupService;
            _overlayService = overlayService;
            _settings = settingsService.CurrentSettings;
            HotkeySettings = _settings.Hotkeys
                .Select(h => new HotkeySettingViewModel(h)).ToList();
            SaveCommand = new RelayCommand(async _ => await SaveAsync());
            CancelCommand = new RelayCommand(_ => Close());
            OpenExtensionFolderCommand = new RelayCommand(_ => OpenExtensionFolder());

            OverlayWidth = Math.Clamp(_settings.OverlayWidth, 520, 760);
            OverlayHeight = Math.Clamp(_settings.OverlayHeight, 190, 280);
            OverlayOpacity = Math.Clamp(_settings.OverlayOpacity, 0.4, 1);
            CornerRadius = Math.Clamp(_settings.CornerRadius, 4, 32);
            ShowArtwork = _settings.ShowArtwork;
            ShowArtist = _settings.ShowArtist;
            ShowAlbum = _settings.ShowAlbum;
            ShowProgress = _settings.ShowProgress;
            ShowControls = _settings.ShowControls;
            ShowTrackChangeNotification = _settings.ShowTrackChangeNotification;
            NotificationDurationSeconds = _settings.NotificationDurationSeconds;
            StartWithWindows = _settings.StartWithWindows;
            ShowOverlayOnLaunch = _settings.ShowOverlayOnLaunch;
            ShowSettingsOnLaunch = _settings.ShowSettingsOnLaunch;
            ShowOverlayOnTrackChange = _settings.ShowOverlayOnTrackChange;
            AppTheme = _settings.AppTheme;
        }

        public List<HotkeySettingViewModel> HotkeySettings { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand OpenExtensionFolderCommand { get; }
        public Action? CloseAction { get; set; }

        public double OverlayWidth { get; set; }
        public double OverlayHeight { get; set; }
        public double OverlayOpacity { get; set; }
        public double CornerRadius { get; set; }
        public bool ShowArtwork { get; set; }
        public bool ShowArtist { get; set; }
        public bool ShowAlbum { get; set; }
        public bool ShowProgress { get; set; }
        public bool ShowControls { get; set; }
        public bool ShowTrackChangeNotification { get; set; }
        public int NotificationDurationSeconds { get; set; }
        public bool StartWithWindows { get; set; }
        public bool ShowOverlayOnLaunch { get; set; }
        public bool ShowSettingsOnLaunch { get; set; }
        public bool ShowOverlayOnTrackChange { get; set; }
        public List<string> AvailableThemes { get; } = new() { "Nebula", "Ocean", "Ember" };
        public string AppTheme { get; set; } = "Nebula";
        public Action<string>? ThemeChangedAction { get; set; }

        private async Task SaveAsync()
        {
            if (!ValidateHotkeys())
                return;

            if (!double.IsFinite(OverlayWidth) || OverlayWidth < 520 || OverlayWidth > 760 ||
                !double.IsFinite(OverlayHeight) || OverlayHeight < 190 || OverlayHeight > 280 ||
                !double.IsFinite(OverlayOpacity) || OverlayOpacity < 0.4 || OverlayOpacity > 1 ||
                !double.IsFinite(CornerRadius) || CornerRadius < 4 || CornerRadius > 32 ||
                NotificationDurationSeconds < 1 || NotificationDurationSeconds > 10)
            {
                System.Windows.MessageBox.Show("Проверьте значения размеров, непрозрачности, скругления и длительности уведомления. Допустимые диапазоны указаны рядом с полями.",
                    "Проверьте настройки", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _settings.Hotkeys = HotkeySettings.Select(vm => vm.ToModel()).ToList();
            _settings.OverlayWidth = OverlayWidth;
            _settings.OverlayHeight = OverlayHeight;
            _settings.OverlayOpacity = OverlayOpacity;
            _settings.CornerRadius = CornerRadius;
            _settings.ShowArtwork = ShowArtwork;
            _settings.ShowArtist = ShowArtist;
            _settings.ShowAlbum = ShowAlbum;
            _settings.ShowProgress = ShowProgress;
            _settings.ShowControls = ShowControls;
            _settings.ShowTrackChangeNotification = ShowTrackChangeNotification;
            _settings.NotificationDurationSeconds = NotificationDurationSeconds;
            _settings.StartWithWindows = StartWithWindows;
            _settings.ShowOverlayOnLaunch = ShowOverlayOnLaunch;
            _settings.ShowSettingsOnLaunch = ShowSettingsOnLaunch;
            _settings.ShowOverlayOnTrackChange = ShowOverlayOnTrackChange;
            _settings.AppTheme = AppTheme;

            _startupService.UpdateStartupRegistration(StartWithWindows);
            _overlayService.ApplySettings();
            ThemeChangedAction?.Invoke(AppTheme);
            await _settingsService.SaveAsync();
            Close();
        }

        private void OpenExtensionFolder()
        {
            try
            {
                Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "BrowserExtension"))
                {
                    UseShellExecute = true
                });
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show("Папка BrowserExtension не найдена. Используйте полную новую сборку приложения.",
                    "Расширение VK", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Close()
        {
            CloseAction?.Invoke();
        }

        private bool ValidateHotkeys()
        {
            var used = new Dictionary<string, HotkeyAction>();
            foreach (var model in HotkeySettings)
            {
                if (!model.Enabled || model.Key == Key.None)
                    continue;

                var signature = $"{model.Ctrl}-{model.Alt}-{model.Shift}-{model.Win}-{model.Key}";
                if (used.TryGetValue(signature, out var existing))
                {
                    System.Windows.MessageBox.Show($"Горячая клавиша для {model.ActionName} конфликтует с {existing}. Пожалуйста, измените комбинацию.", "Конфликт горячих клавиш", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                used[signature] = model.Action;
            }

            return true;
        }
    }
}
