using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using VKMusicController.Models;

namespace VKMusicController.Views;

public partial class TrackNotificationWindow : Window
{
    public TrackNotificationWindow(MediaTrackInfo track)
    {
        InitializeComponent();
        DataContext = track;
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

    private void OnDismissClicked(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        Close();
    }
}
