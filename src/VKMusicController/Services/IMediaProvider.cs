using System;
using System.Threading.Tasks;
using VKMusicController.Models;

namespace VKMusicController.Services
{
    public interface IMediaProvider : IDisposable
    {
        event EventHandler<MediaTrackInfo?>? TrackChanged;
        event EventHandler<PlaybackState>? PlaybackStateChanged;

        bool HasMediaSession { get; }
        bool CanSeek { get; }
        TimeSpan GetCurrentPosition();
        TimeSpan GetCurrentDuration();
        bool IsPlaying();
        Task InitializeAsync();
        Task TogglePlayPauseAsync();
        Task NextTrackAsync();
        Task PreviousTrackAsync();
        Task<bool> SeekAsync(double positionSeconds);
    }
}
