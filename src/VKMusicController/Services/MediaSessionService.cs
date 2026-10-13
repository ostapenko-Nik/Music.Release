using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Threading;
using VKMusicController.Models;
using Windows.Media.Control;
using Windows.Media;

namespace VKMusicController.Services
{
    public class MediaSessionService : IMediaProvider
    {
        private readonly LoggerService _logger;
        private GlobalSystemMediaTransportControlsSessionManager? _manager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        private bool _isSubscribed;
        private int _refreshRequested;
        private int _refreshWorkerRunning;
        private string? _publishedTrackKey;
        private PlaybackState _publishedPlaybackState = PlaybackState.Unknown;

        public event EventHandler<MediaTrackInfo?>? TrackChanged;
        public event EventHandler<PlaybackState>? PlaybackStateChanged;

        public bool HasMediaSession => _currentSession != null;
        public MediaTrackInfo? CurrentTrack { get; private set; }

        public bool CanSeek => _currentSession?.GetPlaybackInfo()?.Controls.IsPlaybackPositionEnabled == true;

        public TimeSpan GetCurrentPosition()
        {
            return _currentSession?.GetTimelineProperties().Position ?? TimeSpan.Zero;
        }

        public TimeSpan GetCurrentDuration()
        {
            return _currentSession?.GetTimelineProperties().EndTime ?? TimeSpan.Zero;
        }

        public bool IsPlaying()
        {
            var playbackInfo = _currentSession?.GetPlaybackInfo();
            return playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }

        public MediaSessionService(LoggerService logger)
        {
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_manager == null)
                {
                    _logger.LogError("Unable to get GlobalSystemMediaTransportControlsSessionManager.");
                    return;
                }

                _manager.CurrentSessionChanged += OnCurrentSessionChanged;
                _manager.SessionsChanged += OnSessionsChanged;
                SelectCurrentSession();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to initialize media session service.", ex);
            }
        }

        private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager? sender, CurrentSessionChangedEventArgs? args)
        {
            _logger.LogInfo("Current media session changed.");
            SelectCurrentSession();
        }

        private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager? sender, SessionsChangedEventArgs? args)
        {
            _logger.LogInfo("Media sessions list changed.");
            SelectCurrentSession();
        }

        private void SelectCurrentSession()
        {
            if (_manager == null)
                return;

            var current = _manager.GetCurrentSession();
            var sessions = _manager.GetSessions().ToArray();
            var browserSessions = sessions.Where(s => IsBrowserSession(s.SourceAppUserModelId)).ToArray();

            GlobalSystemMediaTransportControlsSession? selectedSession = null;

            if (current != null && IsBrowserSession(current.SourceAppUserModelId))
            {
                selectedSession = current;
            }
            else
            {
                selectedSession = browserSessions
                    .FirstOrDefault(s => s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing ||
                                         s.GetPlaybackInfo()?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
                    ?? browserSessions.FirstOrDefault();

                if (selectedSession == null)
                {
                    selectedSession = current;
                }
            }

            if (selectedSession == _currentSession)
                return;

            UnsubscribeSession();
            _currentSession = selectedSession;
            SubscribeSession();
            _logger.LogInfo($"Selected media session: {selectedSession?.SourceAppUserModelId}");
            _publishedTrackKey = null;
            QueueRefresh();
        }

        private bool IsBrowserSession(string? sourceAppUserModelId)
        {
            if (string.IsNullOrEmpty(sourceAppUserModelId))
                return false;

            var lower = sourceAppUserModelId.ToLowerInvariant();
            return lower.Contains("chrome") || lower.Contains("edge") || lower.Contains("browser") || lower.Contains("msedge");
        }

        private void SubscribeSession()
        {
            if (_currentSession == null)
                return;

            if (_isSubscribed)
                return;

            _currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
            _currentSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
            _isSubscribed = true;
            _logger.LogInfo("Subscribed to current media session events.");
        }

        private void UnsubscribeSession()
        {
            if (_currentSession == null || !_isSubscribed)
                return;

            _currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _currentSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            _isSubscribed = false;
        }

        private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession? sender, MediaPropertiesChangedEventArgs? args)
        {
            QueueRefresh();
        }

        private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession? sender, PlaybackInfoChangedEventArgs? args)
        {
            QueueRefresh();
        }

        private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession? sender, TimelinePropertiesChangedEventArgs? args)
        {
            QueueRefresh();
        }

        private void QueueRefresh()
        {
            Interlocked.Exchange(ref _refreshRequested, 1);
            if (Interlocked.CompareExchange(ref _refreshWorkerRunning, 1, 0) == 0)
            {
                _ = ProcessRefreshQueueAsync();
            }
        }

        private async Task ProcessRefreshQueueAsync()
        {
            try
            {
                while (Interlocked.Exchange(ref _refreshRequested, 0) == 1)
                {
                    await RefreshCurrentTrackAsync();
                }
            }
            finally
            {
                Interlocked.Exchange(ref _refreshWorkerRunning, 0);
                if (Volatile.Read(ref _refreshRequested) == 1)
                {
                    QueueRefresh();
                }
            }
        }

        private async Task RefreshCurrentTrackAsync()
        {
            var session = _currentSession;
            if (session == null)
            {
                PublishTrack(null, PlaybackState.Unknown);
                return;
            }

            try
            {
                var properties = await session.TryGetMediaPropertiesAsync();
                if (session != _currentSession)
                    return;

                var playbackInfo = session.GetPlaybackInfo();
                var timeline = session.GetTimelineProperties();
                var artworkData = await LoadArtworkAsync(properties?.Thumbnail);
                if (session != _currentSession)
                    return;

                var track = new MediaTrackInfo
                {
                    Title = properties?.Title ?? string.Empty,
                    Artist = properties?.Artist ?? string.Empty,
                    Album = properties?.AlbumTitle ?? string.Empty,
                    ArtworkData = artworkData,
                    IsPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                    PositionSeconds = timeline?.Position.TotalSeconds ?? 0,
                    DurationSeconds = timeline?.EndTime.TotalSeconds ?? 0
                };

                PublishTrack(track, ConvertPlaybackState(playbackInfo?.PlaybackStatus ?? GlobalSystemMediaTransportControlsSessionPlaybackStatus.Closed));
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to refresh current track.", ex);
            }
        }

        private void PublishTrack(MediaTrackInfo? track, PlaybackState playbackState)
        {
            CurrentTrack = track;
            var trackKey = track == null ? null : $"{track.Title}\u001F{track.Artist}\u001F{track.Album}";
            if (!string.Equals(trackKey, _publishedTrackKey, StringComparison.Ordinal))
            {
                _publishedTrackKey = trackKey;
                TrackChanged?.Invoke(this, track);
            }

            if (playbackState != _publishedPlaybackState)
            {
                _publishedPlaybackState = playbackState;
                PlaybackStateChanged?.Invoke(this, playbackState);
            }
        }

        private async Task<byte[]?> LoadArtworkAsync(Windows.Storage.Streams.IRandomAccessStreamReference? thumbnail)
        {
            if (thumbnail == null)
                return null;

            try
            {
                using var stream = await thumbnail.OpenReadAsync();
                var reader = new Windows.Storage.Streams.DataReader(stream);
                await reader.LoadAsync((uint)stream.Size);
                var data = new byte[stream.Size];
                reader.ReadBytes(data);
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to load artwork bytes.", ex);
                return null;
            }
        }

        private PlaybackState ConvertPlaybackState(GlobalSystemMediaTransportControlsSessionPlaybackStatus status)
        {
            return status switch
            {
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackState.Playing,
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackState.Paused,
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => PlaybackState.Stopped,
                _ => PlaybackState.Unknown,
            };
        }

        public async Task TogglePlayPauseAsync()
        {
            if (_currentSession == null)
                return;

            try
            {
                var playbackInfo = _currentSession.GetPlaybackInfo();
                if (playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                {
                    await _currentSession.TryPauseAsync();
                }
                else
                {
                    await _currentSession.TryPlayAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to toggle play/pause.", ex);
            }
        }

        public async Task NextTrackAsync()
        {
            if (_currentSession == null)
                return;

            try
            {
                await _currentSession.TrySkipNextAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to skip next track.", ex);
            }
        }

        public async Task PreviousTrackAsync()
        {
            if (_currentSession == null)
                return;

            try
            {
                await _currentSession.TrySkipPreviousAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to skip previous track.", ex);
            }
        }

        public async Task<bool> SeekAsync(double positionSeconds)
        {
            var session = _currentSession;
            if (session == null || !double.IsFinite(positionSeconds))
                return false;

            try
            {
                if (!session.GetPlaybackInfo().Controls.IsPlaybackPositionEnabled)
                    return false;

                var timeline = session.GetTimelineProperties();
                if (timeline.EndTime <= timeline.StartTime)
                    return false;

                var seconds = Math.Clamp(Math.Round(positionSeconds),
                    timeline.StartTime.TotalSeconds, timeline.EndTime.TotalSeconds);
                var accepted = await session.TryChangePlaybackPositionAsync(TimeSpan.FromSeconds(seconds).Ticks);
                if (session != _currentSession)
                    return false;

                if (accepted)
                    QueueRefresh();
                return accepted;
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to seek media playback.", ex);
                return false;
            }
        }

        public void Dispose()
        {
            UnsubscribeSession();
            _currentSession = null;
        }
    }
}
