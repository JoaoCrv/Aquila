using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aquila.DesktopSurface;

/// <summary>
/// The floating bar shown while edit mode is on. It exists because the app window is minimized
/// on entering the mode (you can't edit a desktop you can't see), which would otherwise leave the user
/// with no way back — the surface itself has no chrome and no taskbar entry.
///
/// Topmost, so it can't be lost behind anything, and pinned to the top edge of the primary screen where
/// it's least likely to sit on top of a widget being edited.
/// </summary>
internal sealed class EditModeToolbar : Window
{
    public event Action? Done;

    public EditModeToolbar()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        SizeToContent = SizeToContent.WidthAndHeight;

        var hint = new TextBlock
        {
            Text = "Editing widgets — drag to move, right-click for more",
            Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 20, 0),
        };

        var done = new Button
        {
            Content = "Done",
            Padding = new Thickness(18, 6, 18, 6),
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        done.Click += (_, _) => Done?.Invoke();

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(hint);
        row.Children.Add(done);

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xEE, 0x18, 0x18, 0x18)),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18, 12, 12, 12),
            Child = row,
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        var source = PresentationSource.FromVisual(this);
        if (screen is null || source?.CompositionTarget is null) return;

        var fromDevice = source.CompositionTarget.TransformFromDevice;
        var topLeft = fromDevice.Transform(new Point(screen.Bounds.Left, screen.Bounds.Top));
        var bottomRight = fromDevice.Transform(new Point(screen.Bounds.Right, screen.Bounds.Bottom));

        UpdateLayout();
        Left = topLeft.X + ((bottomRight.X - topLeft.X) - ActualWidth) / 2;
        Top = topLeft.Y + 24;
    }
}
