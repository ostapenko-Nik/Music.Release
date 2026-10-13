using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using VKMusicController.Models;

namespace VKMusicController.Views;

public partial class TrackNotificationWindow : Window
{
    private readonly Action? _openPlayer;

    public TrackNotificationWindow(MediaTrackInfo track, Action? openPlayer = null)
    {
        InitializeComponent();
        DataContext = track;
        _openPlayer = openPlayer;
        if (string.IsNullOrWhiteSpace(track.Artist))
            TrackArtist.Visibility = Visibility.Collapsed;
        if (track.ArtworkData is not { Length: > 0 })
            return;

        try
        {
            using var stream = new MemoryStream(track.ArtworkData);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            ArtworkImage.Source = image;
        }
        catch
        {
            // Keep the music placeholder when a browser supplies an invalid thumbnail.
        }
    }

    private void OnOpenPlayerClicked(object sender, MouseButtonEventArgs e)
    {
        _openPlayer?.Invoke();
        Close();
    }

    private void OnDismissClicked(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Close();
    }
}
