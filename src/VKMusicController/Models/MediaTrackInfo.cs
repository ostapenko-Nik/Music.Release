namespace VKMusicController.Models
{
    public class MediaTrackInfo
    {
        public string Title { get; init; } = string.Empty;
        public string Artist { get; init; } = string.Empty;
        public string Album { get; init; } = string.Empty;
        public byte[]? ArtworkData { get; init; }
        public bool IsPlaying { get; init; }
        public double PositionSeconds { get; init; }
        public double DurationSeconds { get; init; }
    }
}
