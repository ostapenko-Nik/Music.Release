using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VKMusicController.ViewModels;
using VKMusicController.Views;
using VKMusicController.Models;
using VKMusicController.Services;

namespace VKMusicController
{
    public partial class MainWindow : Window
    {
        private static readonly BitmapImage? DefaultArtwork = CreateDefaultArtwork();

        private readonly OverlayService _overlayService;
        private readonly MediaSessionService _mediaService;
        private readonly SettingsService _settingsService;
        private readonly StartupService _startupService;
        private bool _initializing = true;

        public MainWindow(
            OverlayService overlayService,
            MediaSessionService mediaService,
            SettingsService settingsService,
            StartupService startupService)
        {
            InitializeComponent();

            _overlayService = overlayService;
            _mediaService = mediaService;
            _settingsService = settingsService;
            _startupService = startupService;

            StartWithWindowsCheck.IsChecked =
                _settingsService.CurrentSettings.StartWithWindows;
            _initializing = false;

            _mediaService.TrackChanged += OnMediaTrackChanged;
            _mediaService.PlaybackStateChanged += OnMediaPlaybackStateChanged;
            Loaded += OnWindowLoaded;
            _settingsService.SettingsSaved += OnSettingsSaved;
            Closed += (_, _) =>
            {
                _mediaService.TrackChanged -= OnMediaTrackChanged;
                _mediaService.PlaybackStateChanged -= OnMediaPlaybackStateChanged;
                _settingsService.SettingsSaved -= OnSettingsSaved;
            };
            UpdateNowPlaying(_mediaService.CurrentTrack);
            RefreshHotkeys();
        }

        // =====================================================
        // ПЕРЕТАСКИВАНИЕ ОКНА
        // =====================================================

        private void Header_MouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null && source != this)
            {
                if (source is System.Windows.Controls.Primitives.ButtonBase)
                    return;
                source = VisualTreeHelper.GetParent(source);
            }
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch
                {
                    // Игнорируем ошибку, если окно уже закрывается
                }
            }
        }

        // =====================================================
        // ОБЛОЖКА ПО УМОЛЧАНИЮ
        // =====================================================

        private static BitmapImage? CreateDefaultArtwork()
        {
            try
            {
                var bitmap = new BitmapImage();

                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;

                bitmap.UriSource = new Uri(
                    "pack://application:,,,/Views/6122149488023872.jfif",
                    UriKind.Absolute);

                bitmap.EndInit();
                bitmap.Freeze();

                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        // =====================================================
        // MEDIA EVENTS
        // =====================================================

        private void OnMediaTrackChanged(
            object? sender,
            MediaTrackInfo? track)
        {
            Dispatcher.BeginInvoke(() =>
            {
                UpdateNowPlaying(track);
            });
        }

        private void OnMediaPlaybackStateChanged(
            object? sender,
            PlaybackState state)
        {
            Dispatcher.BeginInvoke(() =>
            {
                UpdatePlaybackAppearance(state);
            });
        }

        // =====================================================
        // NOW PLAYING
        // =====================================================

        private void UpdateNowPlaying(MediaTrackInfo? track)
        {
            if (track == null ||
                string.IsNullOrEmpty(track.Title))
            {
                NowPlayingTitle.Text = "Пока нет трека";

                NowPlayingArtist.Text =
                    "Ожидаю воспроизведение через VK";

                NowPlayingAlbum.Text = string.Empty;

                UpdatePlaybackAppearance(PlaybackState.Unknown);

                NowPlayingImage.Source = DefaultArtwork;

                return;
            }

            NowPlayingTitle.Text = track.Title;

            NowPlayingArtist.Text =
                string.IsNullOrWhiteSpace(track.Artist)
                    ? "Исполнитель неизвестен"
                    : track.Artist;

            NowPlayingAlbum.Text =
                string.IsNullOrWhiteSpace(track.Album)
                    ? string.Empty
                    : track.Album;

            UpdatePlaybackAppearance(track.IsPlaying ? PlaybackState.Playing : PlaybackState.Paused);

            NowPlayingImage.Source =
                LoadArtworkImage(track.ArtworkData)
                ?? DefaultArtwork;
        }

        private static BitmapImage? LoadArtworkImage(
            byte[]? imageData)
        {
            if (imageData == null ||
                imageData.Length == 0)
            {
                return null;
            }

            try
            {
                using var stream =
                    new MemoryStream(imageData);

                var bitmap = new BitmapImage();

                bitmap.BeginInit();
                bitmap.CacheOption =
                    BitmapCacheOption.OnLoad;

                bitmap.StreamSource = stream;

                bitmap.EndInit();
                bitmap.Freeze();

                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        // =====================================================
        // PLAYBACK
        // =====================================================

        private async void OnPlayPauseClicked(
            object sender,
            RoutedEventArgs e)
        {
            await _mediaService.TogglePlayPauseAsync();
        }

        private async void OnNextClicked(
            object sender,
            RoutedEventArgs e)
        {
            await _mediaService.NextTrackAsync();
        }

        private async void OnPreviousClicked(
            object sender,
            RoutedEventArgs e)
        {
            await _mediaService.PreviousTrackAsync();
        }

        // =====================================================
        // OVERLAY
        // =====================================================

        private void OnShowOverlayClicked(
            object sender,
            RoutedEventArgs e)
        {
            _overlayService.ShowOverlay();
        }

        private void OnHideOverlayClicked(
            object sender,
            RoutedEventArgs e)
        {
            _overlayService.HideOverlay();
        }

        private void OnSettingsClicked(
            object sender,
            RoutedEventArgs e)
        {
            var viewModel = new SettingsViewModel(_settingsService, _startupService, _overlayService)
            {
                ThemeChangedAction = ApplyTheme
            };
            var window = new SettingsWindow
            {
                Owner = this,
                DataContext = viewModel
            };
            window.ShowDialog();
        }

        // =====================================================
        // WINDOW CONTROLS
        // =====================================================

        private void OnMinimizeClicked(
            object sender,
            RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void OnCloseClicked(
            object sender,
            RoutedEventArgs e)
        {
            Close();
        }

        private void OnExitClicked(
            object sender,
            RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        // =====================================================
        // СТАРЫЙ TITLE BAR
        // =====================================================

        private void OnTitleBarMouseDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState =
                    WindowState == WindowState.Maximized
                        ? WindowState.Normal
                        : WindowState.Maximized;

                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch
                {
                    // Игнорируем ошибку
                }
            }
        }

        // =====================================================
        // ЗАГРУЗКА ОКНА
        // =====================================================

        private void OnWindowLoaded(
            object sender,
            RoutedEventArgs e)
        {
            ApplyTheme(_settingsService.CurrentSettings.AppTheme);
        }

        private void ApplyTheme(string theme)
        {
            ThemeService.Apply(theme);
        }

        private void UpdatePlaybackAppearance(PlaybackState state)
        {
            NowPlayingStatus.Text = state switch
            {
                PlaybackState.Playing => "Воспроизводится",
                PlaybackState.Paused => "На паузе",
                PlaybackState.Stopped => "Остановлено",
                _ => "Ожидание музыки"
            };
            MainPlaybackButton.Content = state == PlaybackState.Playing ? "Пауза" : "Воспроизвести";
            NowPlayingStatusDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty,
                state == PlaybackState.Playing ? "UiAccentBrush" : "UiMutedBrush");
        }

        private void OnPlayerClicked(object sender, RoutedEventArgs e) => UpdateNowPlaying(_mediaService.CurrentTrack);

        private void RefreshHotkeys()
        {
            var actions = new[] { HotkeyAction.PlayPause, HotkeyAction.NextTrack, HotkeyAction.ShowHideOverlay };
            MainHotkeys.ItemsSource = actions.Select(action =>
            {
                var hotkey = _settingsService.CurrentSettings.Hotkeys.FirstOrDefault(item => item.Action == action);
                var vm = hotkey == null ? null : new HotkeySettingViewModel(hotkey);
                return new
                {
                    Label = action == HotkeyAction.PlayPause ? "Пауза / play" : action == HotkeyAction.NextTrack ? "Следующий" : "Оверлей",
                    Shortcut = vm?.Enabled == true ? vm.HotkeyDisplay.Replace("+", " + ") : "Выключено"
                };
            }).ToList();
        }

        private void OnSettingsSaved(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(() =>
            {
                _initializing = true;
                StartWithWindowsCheck.IsChecked = _settingsService.CurrentSettings.StartWithWindows;
                _initializing = false;
                RefreshHotkeys();
                ApplyTheme(_settingsService.CurrentSettings.AppTheme);
            });
        }

        // =====================================================
        // START WITH WINDOWS
        // =====================================================

        private async void OnStartupChecked(
            object sender,
            RoutedEventArgs e)
        {
            if (_initializing)
                return;
            _settingsService.CurrentSettings.StartWithWindows =
                true;

            _startupService.UpdateStartupRegistration(true);
            await _settingsService.SaveAsync();
        }

        private async void OnStartupUnchecked(
            object sender,
            RoutedEventArgs e)
        {
            if (_initializing)
                return;
            _settingsService.CurrentSettings.StartWithWindows =
                false;

            _startupService.UpdateStartupRegistration(false);
            await _settingsService.SaveAsync();
        }
    }
}
