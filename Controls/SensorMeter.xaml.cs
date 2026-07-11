using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>Where the label/value sit relative to the bar in a <see cref="SensorMeter"/>.</summary>
public enum MeterLabelPlacement { Inline, Top }

/// <summary>
/// One sensor as a labeled bar with a value (sibling of <see cref="RadialGauge"/> /
/// <see cref="MiniSparkline"/> — wraps <see cref="SensorBar"/>). <see cref="LabelPlacement"/> picks
/// between label|bar|value in a row (Inline, e.g. PowerCard) and label/value on top with the bar
/// below (Top, e.g. StorageCard). <see cref="Label"/>/<see cref="ValueText"/> are plain strings the
/// caller formats, the same convention as <see cref="StatBox"/>.
/// </summary>
public partial class SensorMeter : UserControl
{
    public SensorMeter()
    {
        InitializeComponent();
        Apply();
    }

    public static readonly DependencyProperty SensorProperty =
        DependencyProperty.Register(nameof(Sensor), typeof(SensorNode), typeof(SensorMeter),
            new PropertyMetadata(null));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(SensorMeter),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueTextProperty =
        DependencyProperty.Register(nameof(ValueText), typeof(string), typeof(SensorMeter),
            new PropertyMetadata("--"));

    public static readonly DependencyProperty AccentProperty =
        DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(SensorMeter),
            new PropertyMetadata(null, (d, _) => ((SensorMeter)d).Apply()));

    public static readonly DependencyProperty ColorValueProperty =
        DependencyProperty.Register(nameof(ColorValue), typeof(bool), typeof(SensorMeter),
            new PropertyMetadata(false, (d, _) => ((SensorMeter)d).Apply()));

    public static readonly DependencyProperty LabelPlacementProperty =
        DependencyProperty.Register(nameof(LabelPlacement), typeof(MeterLabelPlacement), typeof(SensorMeter),
            new PropertyMetadata(MeterLabelPlacement.Inline, (d, _) => ((SensorMeter)d).Apply()));

    public static readonly DependencyProperty LabelWidthProperty =
        DependencyProperty.Register(nameof(LabelWidth), typeof(GridLength), typeof(SensorMeter),
            new PropertyMetadata(new GridLength(36), (d, _) => ((SensorMeter)d).Apply()));

    public static readonly DependencyProperty ValueWidthProperty =
        DependencyProperty.Register(nameof(ValueWidth), typeof(GridLength), typeof(SensorMeter),
            new PropertyMetadata(new GridLength(44), (d, _) => ((SensorMeter)d).Apply()));

    /// <summary>Live sensor that drives the bar.</summary>
    public SensorNode? Sensor
    {
        get => (SensorNode?)GetValue(SensorProperty);
        set => SetValue(SensorProperty, value);
    }

    /// <summary>Row label (e.g. "CPU", "Read").</summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Already-formatted value text — the caller controls formatting, as with StatBox.</summary>
    public string ValueText
    {
        get => (string)GetValue(ValueTextProperty);
        set => SetValue(ValueTextProperty, value);
    }

    /// <summary>Bar fill colour. Falls back to the CPU accent token when unset (see SensorBar).</summary>
    public Brush? Accent
    {
        get => (Brush?)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>Whether the value text is tinted with <see cref="Accent"/> too (Top layout, e.g.
    /// Storage's Read/Write rows). Default false — Power's rows leave the value uncoloured.</summary>
    public bool ColorValue
    {
        get => (bool)GetValue(ColorValueProperty);
        set => SetValue(ColorValueProperty, value);
    }

    /// <summary>Label/bar/value in a row (Inline, e.g. Power) or label/value above the bar (Top, e.g. Storage).</summary>
    public MeterLabelPlacement LabelPlacement
    {
        get => (MeterLabelPlacement)GetValue(LabelPlacementProperty);
        set => SetValue(LabelPlacementProperty, value);
    }

    /// <summary>Label column width in Inline layout. Default 36.</summary>
    public GridLength LabelWidth
    {
        get => (GridLength)GetValue(LabelWidthProperty);
        set => SetValue(LabelWidthProperty, value);
    }

    /// <summary>Value column width in Inline layout. Default 44.</summary>
    public GridLength ValueWidth
    {
        get => (GridLength)GetValue(ValueWidthProperty);
        set => SetValue(ValueWidthProperty, value);
    }

    private void Apply()
    {
        bool inline = LabelPlacement == MeterLabelPlacement.Inline;
        InlineLayout.Visibility = inline ? Visibility.Visible : Visibility.Collapsed;
        TopLayout.Visibility = inline ? Visibility.Collapsed : Visibility.Visible;

        InlineLabelColumn.Width = LabelWidth;
        InlineValueColumn.Width = ValueWidth;

        if (ColorValue)
            TopValueText.Foreground = Accent ?? TryFindResource("Aquila.Cpu") as Brush;
        else
            TopValueText.ClearValue(TextBlock.ForegroundProperty);
    }
}
