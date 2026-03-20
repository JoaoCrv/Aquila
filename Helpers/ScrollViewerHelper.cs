using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Aquila.Helpers
{
    /// <summary>
    /// Attached behavior that bubbles MouseWheel events up to the nearest
    /// parent ScrollViewer. Fixes scroll not working when the mouse is over
    /// child controls that absorb the wheel event (CardExpander, ItemsControl, etc).
    /// Usage: helpers:ScrollViewerHelper.BubbleScrollEvents="True"
    /// </summary>
    public static class ScrollViewerHelper
    {
        public static readonly DependencyProperty BubbleScrollEventsProperty =
            DependencyProperty.RegisterAttached(
                "BubbleScrollEvents",
                typeof(bool),
                typeof(ScrollViewerHelper),
                new PropertyMetadata(false, OnBubbleScrollEventsChanged));

        public static bool GetBubbleScrollEvents(DependencyObject obj) =>
            (bool)obj.GetValue(BubbleScrollEventsProperty);

        public static void SetBubbleScrollEvents(DependencyObject obj, bool value) =>
            obj.SetValue(BubbleScrollEventsProperty, value);

        private static void OnBubbleScrollEventsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not UIElement element) return;

            if ((bool)e.NewValue)
                element.PreviewMouseWheel += OnPreviewMouseWheel;
            else
                element.PreviewMouseWheel -= OnPreviewMouseWheel;
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;

            // Walk up the visual tree to find the parent ScrollViewer
            var parent = VisualTreeHelper.GetParent((DependencyObject)sender);
            while (parent is not null and not System.Windows.Controls.ScrollViewer)
                parent = VisualTreeHelper.GetParent(parent);

            if (parent is System.Windows.Controls.ScrollViewer scrollViewer)
            {
                // Re-raise the event on the ScrollViewer so it handles the scroll
                var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = sender
                };
                scrollViewer.RaiseEvent(args);
                e.Handled = true;
            }
        }
    }
}
