using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VKMusicController.Models;
using VKMusicController.ViewModels;
using VKMusicController.Views;

namespace VKMusicController.Services
{
    public class OverlayService : IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly IMediaProvider _mediaService;
        private readonly LoggerService _logger;
        private OverlayWindow? _window;
        private OverlayViewModel? _viewModel;
        private DispatcherTimer? _positionTimer;
        private double _lastPositionSeconds;
        private double _lastDurationSeconds;
        private DateTime _positionSnapshotUtc = DateTime.MinValue;
        private bool _lastTrackIsPlaying;
        private double _lastReportedPositionSeconds = double.NaN;
        private bool _seekInProgress;
        private int _trackVersion;

        public OverlayService(SettingsService settingsService, IMediaProvider mediaService, LoggerService logger)
        {
            _settingsService = settingsService;
            _mediaService = mediaService;
            _logger = logger;
        }

        public void Initialize()
        {
            _viewModel = new OverlayViewModel(_settingsService.CurrentSettings)
            {
                SaveSettingsAction = SaveSettingsAsync
            };
            _viewModel.SeekRequested += OnSeekRequested;

            var left = _settingsService.CurrentSettings.OverlayLeft;
            var top = _settingsService.CurrentSettings.OverlayTop;
            var width = Math.Max(520, _settingsService.CurrentSettings.OverlayWidth);
            var height = Math.Max(190, _settingsService.CurrentSettings.OverlayHeight);
            var opacity = Math.Clamp(_settingsService.CurrentSettings.OverlayOpacity, 0.3, 1.0);

            _window = new OverlayWindow
            {
                DataContext = _viewModel,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = left,
                Top = top,
                Width = width,
                Height = height,
                Opacity = opacity
            };

            EnsureWindowOnScreen(_window);
            _window.ShowInTaskbar = false;
            _window.Hide();

            _positionTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, OnPositionTimerTick, System.Windows.Application.Current.Dispatcher);
            _positionTimer.Start();

            _logger.LogInfo($"Overlay initialized at {_window.Left}, {_window.Top}.");
        }

        public void ShowOverlay()
        {
            if (_window == null)
                return;

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_window.IsVisible)
                {
                    _window.Show();
                }

                _window.Topmost = true;
                _window.Activate();
                _window.Focus();
                if (_window is OverlayWindow overlayWindow)
                {
                    overlayWindow.AnimateShow();
                }
            }));

            _logger.LogInfo("Overlay shown.");
        }

        public void HideOverlay()
        {
            if (_window == null)
                return;

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_window is OverlayWindow overlayWindow)
                {
                    overlayWindow.AnimateHide();
                }
                else
                {
                    _window.Hide();
                }
            }));
        }

        public void ToggleOverlay()
        {
            if (_window == null)
                return;

            if (_window.IsVisible)
                HideOverlay();
            else
                ShowOverlay();
        }

        public void ApplySettings()
        {
            if (_window == null || _viewModel == null)
                return;

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                _window.Width = Math.Max(520, _settingsService.CurrentSettings.OverlayWidth);
                _window.Height = Math.Max(190, _settingsService.CurrentSettings.OverlayHeight);
                _window.Opacity = Math.Clamp(_settingsService.CurrentSettings.OverlayOpacity, 0.3, 1.0);
                _viewModel.RefreshAppearance();
            }));
        }

        public void UpdateTrack(MediaTrackInfo? track)
        {
            if (_viewModel == null)
                return;

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                _trackVersion++;
                _viewModel.IsSeeking = false;
                if (track == null || string.IsNullOrEmpty(track.Title))
                {
                    _viewModel.Title = "Музыка не воспроизводится";
                    _viewModel.Artist = string.Empty;
                    _viewModel.Album = string.Empty;
                    _viewModel.UpdateTimeline(0, 0);
                    _viewModel.CanSeek = false;
                    _viewModel.PlaybackState = PlaybackState.Stopped;
                    _viewModel.ArtworkImage = null;
                    _viewModel.PositionText = "0:00";
                    _viewModel.DurationText = "0:00";
                    _lastPositionSeconds = 0;
                    _lastReportedPositionSeconds = double.NaN;
                    _lastDurationSeconds = 0;
                    _lastTrackIsPlaying = false;
                    _positionSnapshotUtc = DateTime.MinValue;
                    return;
                }

                _viewModel.Title = track.Title;
                _viewModel.Artist = track.Artist;
                _viewModel.Album = track.Album;
                _viewModel.PlaybackState = track.IsPlaying ? PlaybackState.Playing : PlaybackState.Paused;
                _viewModel.UpdateTimeline(track.PositionSeconds, track.DurationSeconds);
                _viewModel.CanSeek = !_seekInProgress && _mediaService.CanSeek && track.DurationSeconds > 0;
                _viewModel.ArtworkImage = GetBitmapImage(track.ArtworkData);
                _lastPositionSeconds = track.PositionSeconds;
                _lastReportedPositionSeconds = track.PositionSeconds;
                _lastDurationSeconds = track.DurationSeconds;
                _lastTrackIsPlaying = track.IsPlaying;
                _positionSnapshotUtc = DateTime.UtcNow;
            }));
        }

        public void UpdatePlaybackState(PlaybackState state)
        {
            if (_viewModel == null)
                return;

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                _viewModel.PlaybackState = state;
                _lastTrackIsPlaying = state == PlaybackState.Playing;
                if (_lastTrackIsPlaying)
                {
                    _positionSnapshotUtc = DateTime.UtcNow;
                }
            }));
        }

        private async void OnSeekRequested(object? sender, double positionSeconds)
        {
            if (_viewModel == null || _seekInProgress || !_mediaService.HasMediaSession || !_mediaService.CanSeek)
                return;

            var trackVersion = _trackVersion;
            var target = Math.Clamp(Math.Round(positionSeconds), 0, _viewModel.DurationSeconds);
            _seekInProgress = true;
            _viewModel.CanSeek = false;
            try
            {
                var accepted = await _mediaService.SeekAsync(target);
                if (trackVersion != _trackVersion)
                    return;

                if (accepted)
                {
                    _lastPositionSeconds = target;
                    // The browser can publish its new timeline after accepting the request.
                    _lastReportedPositionSeconds = _mediaService.GetCurrentPosition().TotalSeconds;
                    _lastTrackIsPlaying = _mediaService.IsPlaying();
                    _positionSnapshotUtc = DateTime.UtcNow;
                    _viewModel.UpdateTimeline(target, _lastDurationSeconds);
                }
                else
                {
                    _viewModel.UpdateTimeline(_lastPositionSeconds, _lastDurationSeconds);
                }
                _viewModel.ReportSeekResult(accepted);
            }
            catch (Exception ex)
            {
                _logger.LogError("Overlay seek failed.", ex);
                if (trackVersion == _trackVersion)
                {
                    _viewModel.UpdateTimeline(_lastPositionSeconds, _lastDurationSeconds);
                    _viewModel.ReportSeekResult(false);
                }
            }
            finally
            {
                _seekInProgress = false;
                _viewModel.CanSeek = _mediaService.HasMediaSession && _mediaService.CanSeek && _viewModel.DurationSeconds > 0;
            }
        }

        private BitmapImage? GetBitmapImage(byte[]? data)
        {
            if (data == null || data.Length == 0)
                return null;

            try
            {
                using var stream = new MemoryStream(data);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load artwork image.", ex);
                return null;
            }
        }

        public void Dispose()
        {
            if (_viewModel != null)
                _viewModel.SeekRequested -= OnSeekRequested;
            if (_positionTimer != null)
            {
                _positionTimer.Stop();
                _positionTimer.Tick -= OnPositionTimerTick;
                _positionTimer = null;
            }

            if (_window != null)
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => _window.Close()));
            }
        }

        private void OnPositionTimerTick(object? sender, EventArgs e)
        {
            if (_viewModel == null || _window == null || !_window.IsVisible)
                return;

            try
            {
                _viewModel.CanSeek = !_seekInProgress && _mediaService.HasMediaSession && _mediaService.CanSeek && _viewModel.DurationSeconds > 0;
                if (_viewModel.IsSeeking || _seekInProgress || !_mediaService.HasMediaSession)
                    return;

                var position = _mediaService.GetCurrentPosition();
                var duration = _mediaService.GetCurrentDuration();
                var isPlaying = _mediaService.IsPlaying();

                if (Math.Abs(position.TotalSeconds - _lastReportedPositionSeconds) > 0.01 || double.IsNaN(_lastReportedPositionSeconds))
                {
                    // Honor timeline changes, including seeks made in the browser itself.
                    _lastReportedPositionSeconds = position.TotalSeconds;
                }
                else if (_positionSnapshotUtc != DateTime.MinValue)
                {
                    var elapsedSeconds = _lastTrackIsPlaying ? (DateTime.UtcNow - _positionSnapshotUtc).TotalSeconds : 0;
                    var computedPosition = _lastPositionSeconds + Math.Max(0, elapsedSeconds);
                    position = TimeSpan.FromSeconds(computedPosition);
                }

                if (duration.TotalSeconds <= 0 && _lastDurationSeconds > 0)
                {
                    duration = TimeSpan.FromSeconds(_lastDurationSeconds);
                }

                _viewModel.PlaybackState = isPlaying ? PlaybackState.Playing : PlaybackState.Paused;
                _viewModel.UpdateTimeline(position.TotalSeconds, duration.TotalSeconds);
                _viewModel.CanSeek = _mediaService.CanSeek && duration.TotalSeconds > 0;

                _lastPositionSeconds = _viewModel.PositionSeconds;
                _lastDurationSeconds = duration.TotalSeconds;
                _lastTrackIsPlaying = isPlaying;
                _positionSnapshotUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError("Overlay timer failed to update position.", ex);
            }
        }

        private async void SaveSettingsAsync()
        {
            try
            {
                await _settingsService.SaveAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to save overlay settings.", ex);
            }
        }

        private void EnsureWindowOnScreen(Window window)
        {
            var workArea = SystemParameters.WorkArea;
            if (window.Left < workArea.Left || window.Left > workArea.Right - window.Width)
            {
                window.Left = workArea.Left + 32;
            }

            if (window.Top < workArea.Top || window.Top > workArea.Bottom - window.Height)
            {
                window.Top = workArea.Top + 32;
            }
        }
    }
}
