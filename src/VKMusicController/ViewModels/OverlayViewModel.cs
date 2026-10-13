using System.Windows.Input;
using VKMusicController.Helpers;
using VKMusicController.Models;
using VKMusicController.Services;

namespace VKMusicController.ViewModels
{
    public class OverlayViewModel : ObservableObject
    {
        private string _title = string.Empty;
        private string _artist = string.Empty;
        private string _album = string.Empty;
        private double _progressPercent;
        private PlaybackState _playbackState;
        private object? _artworkImage;
        private readonly AppSettings _settings;
        private double _positionSeconds;
        private double _durationSeconds;
        private bool _canSeek;

        public bool IsSeeking { get; set; }

        public double PositionSeconds => _positionSeconds;
        public double DurationSeconds => _durationSeconds;

        public bool CanSeek
        {
            get => _canSeek;
            set
            {
                if (SetProperty(ref _canSeek, value))
                    RaisePropertyChanged(nameof(SeekHint));
            }
        }

        public string SeekHint => CanSeek
            ? "Нажмите или перетащите. Стрелки ← / → — по 1 секунде."
            : "Источник не поддерживает перемотку или длительность неизвестна.";

        public void UpdateTimeline(double positionSeconds, double durationSeconds)
        {
            durationSeconds = double.IsFinite(durationSeconds) ? Math.Max(0, durationSeconds) : 0;
            positionSeconds = double.IsFinite(positionSeconds) ? Math.Clamp(positionSeconds, 0, durationSeconds) : 0;
            SetProperty(ref _durationSeconds, durationSeconds, nameof(DurationSeconds));
            SetProperty(ref _positionSeconds, positionSeconds, nameof(PositionSeconds));
            PositionText = FormatTime(positionSeconds);
            DurationText = FormatTime(durationSeconds);
            ProgressPercent = durationSeconds > 0 ? positionSeconds / durationSeconds * 100 : 0;
        }

        public void PreviewSeek(double positionSeconds)
        {
            if (IsSeeking && CanSeek)
                UpdateTimeline(Math.Round(positionSeconds), DurationSeconds);
        }

        public void CommitSeek()
        {
            if (!IsSeeking)
                return;

            IsSeeking = false;
            if (CanSeek)
                SeekRequested?.Invoke(this, PositionSeconds);
        }

        public void ReportSeekResult(bool success)
        {
            StatusText = success ? GetPlaybackStatus(PlaybackState) : "Ошибка перемотки";
        }

        private static string FormatTime(double seconds)
        {
            var totalSeconds = (long)Math.Floor(seconds);
            return $"{totalSeconds / 60}:{totalSeconds % 60:D2}";
        }

        private static string GetPlaybackStatus(PlaybackState state) => state switch
        {
            PlaybackState.Playing => "Играет",
            PlaybackState.Paused => "Пауза",
            PlaybackState.Stopped => "Остановлено",
            _ => "Нет музыки"
        };

        public OverlayViewModel(AppSettings settings)
        {
            _settings = settings;
            PreviousCommand = new RelayCommand(_ => PreviousRequested?.Invoke(this, System.EventArgs.Empty));
            PlayPauseCommand = new RelayCommand(_ => PlayPauseRequested?.Invoke(this, System.EventArgs.Empty));
            NextCommand = new RelayCommand(_ => NextRequested?.Invoke(this, System.EventArgs.Empty));
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Artist
        {
            get => _artist;
            set => SetProperty(ref _artist, value);
        }

        public string Album
        {
            get => _album;
            set => SetProperty(ref _album, value);
        }

        public double ProgressPercent
        {
            get => _progressPercent;
            set => SetProperty(ref _progressPercent, value);
        }

        private string _statusText = "Нет музыки";
        private string _positionText = "0:00";
        private string _durationText = "0:00";

        public PlaybackState PlaybackState
        {
            get => _playbackState;
            set
            {
                if (SetProperty(ref _playbackState, value))
                {
                    StatusText = GetPlaybackStatus(value);
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public string PositionText
        {
            get => _positionText;
            set => SetProperty(ref _positionText, value);
        }

        public string DurationText
        {
            get => _durationText;
            set => SetProperty(ref _durationText, value);
        }

        public object? ArtworkImage
        {
            get => _artworkImage;
            set => SetProperty(ref _artworkImage, value);
        }

        public double OverlayLeft
        {
            get => _settings.OverlayLeft;
            set
            {
                if (_settings.OverlayLeft != value)
                {
                    _settings.OverlayLeft = value;
                    RaisePropertyChanged();
                }
            }
        }

        public double OverlayTop
        {
            get => _settings.OverlayTop;
            set
            {
                if (_settings.OverlayTop != value)
                {
                    _settings.OverlayTop = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool ShowArtwork => _settings.ShowArtwork;
        public bool ShowArtist => _settings.ShowArtist;
        public bool ShowAlbum => _settings.ShowAlbum;
        public bool ShowProgress => _settings.ShowProgress;
        public bool ShowControls => _settings.ShowControls;
        public double OverlayCornerRadius => _settings.CornerRadius;

        public void RefreshAppearance()
        {
            RaisePropertyChanged(nameof(ShowArtwork));
            RaisePropertyChanged(nameof(ShowArtist));
            RaisePropertyChanged(nameof(ShowAlbum));
            RaisePropertyChanged(nameof(ShowProgress));
            RaisePropertyChanged(nameof(ShowControls));
            RaisePropertyChanged(nameof(OverlayCornerRadius));
        }

        public ICommand PreviousCommand { get; }
        public ICommand PlayPauseCommand { get; }
        public ICommand NextCommand { get; }

        public event System.EventHandler? PreviousRequested;
        public event System.EventHandler? PlayPauseRequested;
        public event System.EventHandler? NextRequested;
        public event System.EventHandler<double>? SeekRequested;
        public Action? SaveSettingsAction { get; set; }
    }
}
