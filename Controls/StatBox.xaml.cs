using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aquila.Controls;

/// <summary>
/// Small "stat box": a large value with a small unit suffix and a caption below, on a rounded fill.
/// Reusable across dashboard cards (CPU, GPU, ...). Set <see cref="Accent"/> to colour the value;
/// leave it unset to inherit the theme foreground.
/// </summary>
public partial class StatBox : UserControl, IStatStyle
{
    public StatBox()
    {
        InitializeComponent();
        Apply();
    }

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(StatBox),
            new PropertyMetadata(string.Empty, (d, _) => ((StatBox)d).Apply()));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatBox),
            new PropertyMetadata("--", null, CoerceValue));

    /// <summary>
    /// Keeps the box from rendering a bare unit. A sensor that exists but reports null is not a broken
    /// binding, so FallbackValue never fires; StringFormat simply yields an empty string and the caption
    /// is left with "W" and nothing in front of it. Coercing here fixes every caller at once, rather
    /// than relying on each of them to remember TargetNullValue.
    /// </summary>
    private static object CoerceValue(DependencyObject d, object baseValue) =>
        string.IsNullOrWhiteSpace(baseValue as string) ? "--" : baseValue;

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(StatBox),
            new PropertyMetadata(string.Empty, (d, _) => ((StatBox)d).Apply()));

    public static readonly DependencyProperty ShowUnitProperty =
        DependencyProperty.Register(nameof(ShowUnit), typeof(bool), typeof(StatBox),
            new PropertyMetadata(true, (d, _) => ((StatBox)d).Apply()));

    public static readonly DependencyProperty ShowPanelProperty =
        DependencyProperty.Register(nameof(ShowPanel), typeof(bool), typeof(StatBox),
            new PropertyMetadata(true, (d, _) => ((StatBox)d).Apply()));

    public static readonly DependencyProperty ValueSizeProperty =
        DependencyProperty.Register(nameof(ValueSize), typeof(double), typeof(StatBox),
            new PropertyMetadata(20.0));

    /// <summary>
    /// What the unit Run actually draws — the unit, or nothing when it is switched off.
    ///
    /// A property of its own rather than binding the Run straight to Unit and clearing it: the dashboard
    /// cards all set Unit and must go on working untouched, and code that writes over a binding to hide
    /// something is code that has to remember to put the binding back.
    /// </summary>
    public static readonly DependencyProperty UnitTextProperty =
        DependencyProperty.Register(nameof(UnitText), typeof(string), typeof(StatBox),
            new PropertyMetadata(string.Empty));

    public string UnitText
    {
        get => (string)GetValue(UnitTextProperty);
        private set => SetValue(UnitTextProperty, value);
    }

    /// <summary>Whether the unit suffix is drawn at all.</summary>
    public bool ShowUnit { get => (bool)GetValue(ShowUnitProperty); set => SetValue(ShowUnitProperty, value); }

    /// <summary>Whether the rounded fill behind the number is drawn.</summary>
    public bool ShowPanel { get => (bool)GetValue(ShowPanelProperty); set => SetValue(ShowPanelProperty, value); }

    /// <summary>Font size of the value (default 20, matching the StatBoxValue style).</summary>
    public double ValueSize { get => (double)GetValue(ValueSizeProperty); set => SetValue(ValueSizeProperty, value); }

    private void Apply()
    {
        UnitText = ShowUnit ? Unit : string.Empty;

        // An empty caption still occupies a line, which pushed the number off centre. Widgets never set one
        // — they carry their title above the piece — so for them this is always the case.
        LabelText.Visibility = string.IsNullOrEmpty(Label) ? Visibility.Collapsed : Visibility.Visible;

        // Cleared rather than set back to a colour: the style supplies the fill through a DynamicResource,
        // so a local value would pin it to whichever theme was current when the panel was last turned on.
        // Border.BackgroundProperty, qualified: unqualified here would bind to Control.BackgroundProperty,
        // which this UserControl inherits and which is a different property from the Border's own.
        if (ShowPanel) StatPanel.ClearValue(Border.BackgroundProperty);
        else StatPanel.Background = Brushes.Transparent;
    }

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
