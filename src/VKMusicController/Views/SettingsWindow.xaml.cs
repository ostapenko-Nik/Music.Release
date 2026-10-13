using System.Windows;
using VKMusicController.ViewModels;

namespace VKMusicController.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel viewModel)
            {
                viewModel.CloseAction = () => Dispatcher.BeginInvoke((Action)Close);
            }
        }

        private void OnHeaderMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null && source != this)
            {
                if (source is System.Windows.Controls.Primitives.ButtonBase)
                    return;
                source = System.Windows.Media.VisualTreeHelper.GetParent(source);
            }
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                DragMove();
        }
    }
}
