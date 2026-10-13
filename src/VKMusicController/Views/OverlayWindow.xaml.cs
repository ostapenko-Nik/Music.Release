using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using VKMusicController.ViewModels;

namespace VKMusicController.Views
{
    public partial class OverlayWindow : Window
    {
        public OverlayWindow()
        {
            InitializeComponent();
            // Slider handles point clicks in its class handler. Observe release even when handled.
            SeekSlider.AddHandler(PreviewMouseLeftButtonUpEvent,
                new MouseButtonEventHandler((sender, e) => OnSeekInteractionEnded(sender, e)), true);
            Loaded += OnWindowLoaded;
        }

        public void AnimateShow()
        {
            if (RootBorder == null)
                return;

            RootBorder.Opacity = 0;
            if (RootBorder.RenderTransform is ScaleTransform scale)
            {
                scale.ScaleX = 0.92;
                scale.ScaleY = 0.92;
            }

            var duration = TimeSpan.FromMilliseconds(220);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            var opacityAnimation = new DoubleAnimation(1, duration) { EasingFunction = ease };
            var scaleAnimation = new DoubleAnimation(1, duration) { EasingFunction = ease };

            RootBorder.BeginAnimation(OpacityProperty, opacityAnimation);
            if (RootBorder.RenderTransform is ScaleTransform scaleTransform)
            {
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
            }
        }

        public void AnimateHide()
        {
            if (RootBorder == null)
            {
                Hide();
                return;
            }

            var duration = TimeSpan.FromMilliseconds(180);
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

            var opacityAnimation = new DoubleAnimation(0, duration) { EasingFunction = ease };
            var scaleAnimation = new DoubleAnimation(0.96, duration) { EasingFunction = ease };
            opacityAnimation.Completed += (_, _) => Hide();

            RootBorder.BeginAnimation(OpacityProperty, opacityAnimation);
            if (RootBorder.RenderTransform is ScaleTransform scaleTransform)
            {
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
            }
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is OverlayViewModel vm)
            {
                vm.PreviousRequested += (_, _) => ((App)System.Windows.Application.Current).InvokeOverlayPrevious();
                vm.PlayPauseRequested += (_, _) => ((App)System.Windows.Application.Current).InvokeOverlayPlayPause();
                vm.NextRequested += (_, _) => ((App)System.Windows.Application.Current).InvokeOverlayNext();
            }
        }

        private void OnWindowPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Begin before the Slider class handler changes Value on a point click.
            if (IsSeekSource(e.OriginalSource as DependencyObject))
            {
                if (DataContext is OverlayViewModel vm && vm.CanSeek)
                    vm.IsSeeking = true;
                return;
            }

            if (e.OriginalSource is DependencyObject source)
            {
                var element = source;
                while (element != null && element != this)
                {
                    if (element is System.Windows.Controls.Primitives.ButtonBase || element is System.Windows.Controls.Slider || element is System.Windows.Documents.Hyperlink)
                    {
                        return;
                    }
                    element = System.Windows.Media.VisualTreeHelper.GetParent(element);
                }
            }

            try
            {
                DragMove();
            }
            catch
            {
                // DragMove may fail in edge cases when the window is not ready.
            }
        }

        private void OnWindowPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (IsSeekSource(e.OriginalSource as DependencyObject))
                return;

            if (DataContext is OverlayViewModel vm)
            {
                vm.OverlayLeft = Left;
                vm.OverlayTop = Top;
                vm.SaveSettingsAction?.Invoke();
            }
        }

        private bool IsSeekSource(DependencyObject? source)
        {
            while (source != null && source != this)
            {
                if (source is System.Windows.Controls.Slider)
                    return true;
                source = VisualTreeHelper.GetParent(source);
            }
            return false;
        }

        private void OnSeekMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is OverlayViewModel vm && vm.CanSeek)
                vm.IsSeeking = true;
        }

        private void OnSeekInteractionEnded(object sender, RoutedEventArgs e)
        {
            if (DataContext is OverlayViewModel vm)
                vm.CommitSeek();
        }

        private void OnSeekValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DataContext is OverlayViewModel vm)
                vm.PreviewSeek(e.NewValue);
        }

        private static bool IsSeekKey(Key key) => key is Key.Left or Key.Right or Key.Up or Key.Down
            or Key.PageUp or Key.PageDown or Key.Home or Key.End;

        private void OnSeekKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (IsSeekKey(e.Key) && DataContext is OverlayViewModel vm && vm.CanSeek)
                vm.IsSeeking = true;
        }

        private void OnSeekKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (IsSeekKey(e.Key) && DataContext is OverlayViewModel vm)
                vm.CommitSeek();
        }
    }
}
