using System.Windows;

namespace Aquila.Controls;

/// <summary>
/// Corner radius for the shared ThinBar template.
///
/// Attached because a ProgressBar has no corner radius of its own and the value has to reach inside a
/// ControlTemplate. Inherited, so setting it once on a SensorMeter reaches the bar nested two controls
/// down without anything in between having to pass it along.
///
/// The alternative was a second template for rounded bars, which would have meant the dashboard's bars and
/// the desktop's drifting apart the first time either one was touched.
/// </summary>
public static class BarShape
{
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.RegisterAttached(
            "CornerRadius", typeof(CornerRadius), typeof(BarShape),
            new FrameworkPropertyMetadata(new CornerRadius(3), FrameworkPropertyMetadataOptions.Inherits));

    public static CornerRadius GetCornerRadius(DependencyObject d) =>
        (CornerRadius)d.GetValue(CornerRadiusProperty);

    public static void SetCornerRadius(DependencyObject d, CornerRadius value) =>
        d.SetValue(CornerRadiusProperty, value);
}
