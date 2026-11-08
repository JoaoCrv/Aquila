using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aquila.Controls.Primitives;
using Aquila.Models;
using Aquila.Services;

namespace Aquila.Controls;

/// <summary>
/// Horizontal sensor bar: a single SensorNode drawn by <see cref="BarFill"/> (sibling of <see cref="RadialGauge"/> /
/// <see cref="MiniSparkline"/>). It maps the sensor onto a share of its scale, and applies the accent colour and the
/// look; the bar travels at the <see cref="Pace"/> and draws again without laying anything out.
/// </summary>
public partial class SensorBar : UserControl, IFollowsSchemeTrack
{
    /// <summary>The look of a bar nobody has dressed — a card's meter: the thin bar the cards always drew.</summary>
    private static readonly BarLook Undressed = new(6, 3, null);

    public SensorBar()
    {
        InitializeComponent();
        SchemeTrack.Follow(this);
        Loaded += (_, _) => Hook();
        Unloaded += (_, _) => Unhook();
    }

    // ── Limits ─────────────────────────────────────────────────────

    private bool _showLimits;
    private Brush? _limitBrush;
    private double _markReach = LimitMarks.Reach;
    private VitalMonitor? _monitor;

    /// <summary>Marks across the bar where the reading turns elevated and alert, where its scale reaches them.</summary>
    public bool ShowLimits
    {
        get => _showLimits;
        set { _showLimits = value; Render(); }
    }

    public Brush? LimitBrush
    {
        get => _limitBrush;
        set { _limitBrush = value; Render(); }
    }

    /// <summary>How far the marks reach past the bar either side, at the widget's scale.</summary>
    public double MarkReach
    {
        get => _markReach;
        set { _markReach = value; Render(); }
    }

    /// <summary>Draws, and listens for the limits changing while the bar is on screen, and only then.</summary>
    private void Hook()
    {
        Unhook();
        _monitor = VitalMonitor.Current;
        if (_monitor is not null) _monitor.Changed += Render;
        Render();
    }

    private void Unhook()
    {
        if (_monitor is not null) _monitor.Changed -= Render;
        _monitor = null;
    }

    public static readonly DependencyProperty SensorProperty =
        DependencyProperty.Register(nameof(Sensor), typeof(SensorNode), typeof(SensorBar),
            new PropertyMetadata(null, OnSensorChanged));

    public static readonly DependencyProperty AccentProperty =
        DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(SensorBar),
            new PropertyMetadata(null, (d, _) => ((SensorBar)d).Render()));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(SensorBar),
            new PropertyMetadata(double.NaN, (d, _) => ((SensorBar)d).Render()));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(SensorBar),
            new PropertyMetadata(double.NaN, (d, _) => ((SensorBar)d).Render()));

    public static readonly DependencyProperty LookProperty =
        DependencyProperty.Register(nameof(Look), typeof(BarLook), typeof(SensorBar),
            new PropertyMetadata(null, (d, _) => ((SensorBar)d).Dress()));

    /// <summary>Bar scale minimum. Unset (NaN) → 0.</summary>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>Bar scale maximum. Unset (NaN) → 100 for % sensors, else the sensor's observed max.</summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>Live sensor to display.</summary>
    public SensorNode? Sensor
    {
        get => (SensorNode?)GetValue(SensorProperty);
        set => SetValue(SensorProperty, value);
    }

    /// <summary>Fill colour. Falls back to the dashboard's accent when unset.</summary>
    public Brush? Accent
    {
        get => (Brush?)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>The bar's thickness, corners and track, from the widget's preset. Unset — a card — the thin bar the
    /// cards always drew, over the dashboard's track.</summary>
    public BarLook? Look
    {
        get => (BarLook?)GetValue(LookProperty);
        set => SetValue(LookProperty, value);
    }

    private static void OnSensorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var b = (SensorBar)d;
        if (e.OldValue is SensorNode old) old.PropertyChanged -= b.OnSensorPropertyChanged;
        if (e.NewValue is SensorNode now) now.PropertyChanged += b.OnSensorPropertyChanged;

        // Forgotten with the sensor it belonged to, or the bar would try to travel from the old sensor's
        // last reading to the new one's first — two unrelated numbers with a sweep drawn between them.
        b._shown = double.NaN;
        b.Render();
    }

    /// <summary>The share the bar was last sent to. Kept so an update that changes something else — the accent, the
    /// scale — does not restart a sweep that is already under way.</summary>
    private double _shown = double.NaN;

    private void OnSensorPropertyChanged(object? sender, PropertyChangedEventArgs e) => Render();

    void IFollowsSchemeTrack.SchemeTrackChanged() => Dress();

    private void Dress()
    {
        if (Bar is not null) Bar.Look = SchemeTrack.Resolve(this, Look, Undressed);
    }

    private void Render()
    {
        if (!IsLoaded || Sensor is null)
            return;

        double min = double.IsNaN(Minimum) ? 0 : Minimum;
        double max = SensorScale.Ceiling(Sensor, Maximum);
        if (max <= min) max = min + 1;

        Bar.Fill = Accent ?? TryFindResource("Aquila.Scheme.Accent") as Brush;
        Dress();

        // The marks on this bar's own scale, so they move with it — a throughput's top follows its recent peak.
        var marks = _showLimits ? LimitMarks.On(Sensor, min, max) : null;
        if (!LimitMarks.Same(Bar.Marks, marks)) Bar.Marks = marks;
        Bar.MarkBrush = _limitBrush ?? LimitMarks.Unpainted;
        Bar.MarkReach = _showLimits ? _markReach : 0;

        var share = (System.Math.Clamp(Sensor.Value ?? 0, min, max) - min) / (max - min);
        if (share.Equals(_shown)) return;

        // A sensor just handed over is drawn at its reading, never swept to it from the last one's.
        var animate = !double.IsNaN(_shown);
        _shown = share;
        Bar.MoveTo(share, animate);
    }
}
