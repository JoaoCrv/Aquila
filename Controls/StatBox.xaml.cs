using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aquila.Controls;

/// <summary>
/// Small "stat box": a large value with a small unit suffix and a caption below, on a rounded fill.
/// Reusable across dashboard cards (CPU, GPU, ...). Set <see cref="Accent"/> to colour the value;
/// leave it unset to inherit the theme foreground.
/// </summary>
public partial class StatBox : UserControl
{
    public StatBox() => InitializeComponent();

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(StatBox),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatBox),
            new PropertyMetadata("--"));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(StatBox),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty UnitSizeProperty =
        DependencyProperty.Register(nameof(UnitSize), typeof(double), typeof(StatBox),
            new PropertyMetadata(13.0));

    public static readonly DependencyProperty AccentProperty =
        DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(StatBox),
            new PropertyMetadata(null, OnAccentChanged));

    /// <summary>Caption shown under the value (e.g. "Usage").</summary>
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>The (already-formatted) value text.</summary>
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>Small unit suffix (e.g. "%", "°C", "W").</summary>
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    /// <summary>Font size of the unit suffix (default 13).</summary>
    public double UnitSize { get => (double)GetValue(UnitSizeProperty); set => SetValue(UnitSizeProperty, value); }

    /// <summary>Optional value colour. Unset → inherits the theme foreground.</summary>
    public Brush? Accent { get => (Brush?)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }

    private static void OnAccentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var box = (StatBox)d;
        if (e.NewValue is Brush b) box.ValueText.Foreground = b;
        else box.ValueText.ClearValue(TextBlock.ForegroundProperty);
    }
}
