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
    /// <summary>Leave edit mode and keep the changes.</summary>
    public event Action? Save;

    /// <summary>Leave edit mode and throw the changes away.</summary>
    public event Action? Discard;

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
            Text = "Editing widgets — nothing is saved until you press Save",
            Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 20, 0),
        };

        var discard = new Button
        {
            Content = "Discard",
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        discard.Click += (_, _) => Discard?.Invoke();

        var save = new Button
        {
            Content = "Save",
            Padding = new Thickness(18, 6, 18, 6),
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        save.Click += (_, _) => Save?.Invoke();

        // Discard first, Save last: the destructive one is the one further from where the hand rests.
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(hint);
        row.Children.Add(discard);
        row.Children.Add(save);

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
