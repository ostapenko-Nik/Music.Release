using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using VKMusicController.Models;
using VKMusicController.Views;

namespace VKMusicController.Services
{
    public static class NotificationService
    {
        private static TrackNotificationWindow? _activeWindow;

        public static void ShowTrackNotification(MediaTrackInfo track, int durationSeconds)
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() => ShowOnUiThread(track, durationSeconds));
        }

        private static void ShowOnUiThread(MediaTrackInfo track, int durationSeconds)
        {
            _activeWindow?.Close();
            var window = new TrackNotificationWindow(track)
            {
                Opacity = 0
            };
            window.Left = SystemParameters.WorkArea.Right - window.Width - 24;
            window.Top = SystemParameters.WorkArea.Bottom - window.Height - 24;
            _activeWindow = window;

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(Math.Clamp(durationSeconds, 1, 10))
            };
            window.Closed += (_, _) =>
            {
                timer.Stop();
                if (ReferenceEquals(_activeWindow, window))
                    _activeWindow = null;
            };
            window.Loaded += (_, _) =>
            {
                window.BeginAnimation(Window.OpacityProperty,
                    new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180));
                fadeOut.Completed += (_, _) => window.Close();
                window.BeginAnimation(Window.OpacityProperty, fadeOut);
            };

            window.Show();
            timer.Start();
        }
    }
}
