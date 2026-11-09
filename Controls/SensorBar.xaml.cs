using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aquila.Controls.Primitives;
using Aquila.Models;
using Aquila.Services;

namespace Aquila.Controls;

/// <summary>
/// Horizontal sensor bar: a single SensorNode drawn by <see cref="BarFill"/> — the bar inside the cards'
/// <see cref="SensorMeter"/>. It maps the sensor onto a share of its scale and applies the accent colour, in the
/// cards' thin look; the bar travels at the <see cref="Pace"/> and draws again without laying anything out. The
/// widgets' bars are <see cref="BarsTile"/>'s.
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

    private VitalMonitor? _monitor;

    /// <summary>Draws, and listens for the limits changing while the bar is on screen, and only then: a temperature's
    /// scale follows its critical limit (<see cref="SensorScale.Fixed"/>).</summary>
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

    /// <summary>The cards' thin bar, over the dashboard's track.</summary>
    private void Dress()
    {
        if (Bar is not null) Bar.Look = SchemeTrack.Resolve(this, null, Undressed);
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

        var share = (System.Math.Clamp(Sensor.Value ?? 0, min, max) - min) / (max - min);
        if (share.Equals(_shown)) return;

        // A sensor just handed over is drawn at its reading, never swept to it from the last one's.
        var animate = !double.IsNaN(_shown);
        _shown = share;
        Bar.MoveTo(share, animate);
    }
}
