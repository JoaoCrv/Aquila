using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Aquila.Controls.Primitives;
using Aquila.Models;
using Aquila.Services;

namespace Aquila.Controls;

/// <summary>
/// A row of bars, one per reading — an equaliser standing up, or a stack of meters lying down.
///
/// Which way it runs is <see cref="IOrientedBars"/>, and the two are the same drawing with the dimensions
/// swapped: the reading grows along one axis and the thickness holds the other. Each bar is a
/// <see cref="BarFill"/>, which draws a share of its track rather than being laid out at a size — so turning it on
/// its side is one property, and a resize needs nothing from here at all.
///
/// The only piece whose count is the hardware's to decide: sixteen CPU cores make sixteen bars. Everything
/// else in the catalogue draws a number of readings fixed when the kind was written, which is why the
/// colour contract had to grow a <see cref="Aquila.Services.SeriesPaint.Many"/> before this could exist.
///
/// Not an <see cref="ISensorPiece"/>, for the same reason <see cref="SparklineChart"/> is not: that
/// interface exists so a piece holding ONE node can be told to let go of it, and this holds a list. It
/// unsubscribes on Unloaded, by itself.
///
/// It is also the first bar in the app that could animate. <c>BarGroup</c>, the per-core bars on the
/// dashboard, rebuilds its items from scratch every tick — new records, new containers — so there is
/// nothing persistent to animate; a sweep would restart from zero every second. Here each bar is built
/// once and only its share moves, which is the same discipline the gauge already needs.
/// </summary>
public sealed class SensorBars : UserControl, IMeterStyle, IUnitStyle, IOrientedBars, IScaledFigure, IFollowsSchemeTrack,
    ILimitParts, ILimitStyle
{
    private sealed record Bar(SensorNode Sensor, BarFill Fill, TextBlock Reading, DockPanel Column)
    {
        /// <summary>The share last sent to the bar; NaN until it has been put anywhere, so its first place is a jump.
        /// </summary>
        public double Shown { get; set; } = double.NaN;
    }

    /// <summary>The look of bars nobody has dressed — the type picker.</summary>
    private static readonly BarLook Undressed = new(6, 3, null);

    private readonly List<Bar> _bars = [];
    private readonly UniformGrid _row = new() { Rows = 1 };

    public SensorBars(IReadOnlyList<SensorNode> sensors)
    {
        Content = _row;
        SchemeTrack.Follow(this);

        foreach (var sensor in sensors)
        {
            var fill = new BarFill();

            var reading = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
                Opacity = 0.7,
            };

            var column = new DockPanel { Margin = new Thickness(2, 0, 2, 0) };
            DockPanel.SetDock(reading, Dock.Bottom);
            column.Children.Add(reading);
            column.Children.Add(fill);

            var bar = new Bar(sensor, fill, reading, column);
            _bars.Add(bar);
            _row.Children.Add(column);
        }

        Loaded += (_, _) => Hook();
        Unloaded += (_, _) => Unhook();
    }

    private bool _hooked;
    private VitalMonitor? _monitor;

    /// <summary>Takes hold of the readings and the limits on Loaded and lets go on Unloaded, as every piece does, so a
    /// piece off screen holds nothing — and one that comes back listens again. (It subscribed once, when it was built,
    /// and a piece unloaded and loaded again stopped moving.)</summary>
    private void Hook()
    {
        if (!_hooked)
        {
            _hooked = true;
            foreach (var bar in _bars) bar.Sensor.PropertyChanged += OnSensorChanged;
            _monitor = VitalMonitor.Current;
            if (_monitor is not null) _monitor.Changed += OnLimitsChanged;
        }

        Apply();
    }

    private void Unhook()
    {
        if (!_hooked) return;
        _hooked = false;
        foreach (var bar in _bars) bar.Sensor.PropertyChanged -= OnSensorChanged;
        if (_monitor is not null) _monitor.Changed -= OnLimitsChanged;
        _monitor = null;
    }

    private void OnLimitsChanged()
    {
        foreach (var bar in _bars) Mark(bar, SensorScale.Ceiling(bar.Sensor, double.NaN));
    }

    private void OnSensorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SensorNode.Value)) return;
        if (sender is not SensorNode node) return;

        foreach (var bar in _bars)
            if (ReferenceEquals(bar.Sensor, node))
                Draw(bar, animate: true);
    }

    /// <summary>Colours one bar. Reached from the catalogue's painter, so each bar follows its own series'
    /// ramp and its own reading's state — sixteen cores can go amber one at a time.</summary>
    public void PaintBar(int index, Brush brush)
    {
        if (index >= 0 && index < _bars.Count) _bars[index].Fill.Fill = brush;
    }

    private void Draw(Bar bar, bool animate)
    {
        var ceiling = SensorScale.Ceiling(bar.Sensor, double.NaN);
        var share = ceiling > 0 ? Math.Clamp(bar.Sensor.Value ?? 0, 0, ceiling) / ceiling : 0;

        bar.Reading.Text = ShowValue ? bar.Sensor.Reading(ShowUnit, string.Empty) : string.Empty;
        Mark(bar, ceiling);

        if (Math.Abs(share - bar.Shown) < 1e-4) return;

        animate &= !double.IsNaN(bar.Shown);
        bar.Shown = share;

        // As every bar does: from wherever it is drawn, or there at once — see BarFill.MoveTo.
        bar.Fill.MoveTo(share, animate);
    }

    /// <summary>A bar's limits, on its own scale — each bar its own sensor's, so a core and a drive side by side are
    /// each marked where they turn.</summary>
    private void Mark(Bar bar, double ceiling)
    {
        var marks = _showLimits ? LimitMarks.On(bar.Sensor, 0, ceiling) : null;
        if (!LimitMarks.Same(bar.Fill.Marks, marks)) bar.Fill.Marks = marks;
        bar.Fill.MarkBrush = _limitBrush ?? LimitMarks.Unpainted;
        bar.Fill.MarkReach = _showLimits ? LimitMarks.Reach * _figureScale : 0;
    }

    void IFollowsSchemeTrack.SchemeTrackChanged() => Dress();

    /// <summary>Every bar's look: the preset's, or the type picker's default — over the dashboard's track when the
    /// look names none.</summary>
    private void Dress()
    {
        var look = SchemeTrack.Resolve(this, _barLook, Undressed);
        foreach (var bar in _bars) bar.Fill.Look = look;
    }

    /// <summary>
    /// How much room to keep for the number beside a bar, so every track is the same length.
    ///
    /// Lying down, each row's reading took whatever width its own text needed, which left the track
    /// shorter on the rows showing "23%" than on those showing "1%" — so the same value drew a different
    /// length depending on which row it landed in, and the bars stopped being comparable with each other,
    /// which is the only job they have.
    ///
    /// The width is measured from the reading at the TOP of each bar's scale rather than from a guess at
    /// how many characters a number takes. "Three digits and a unit" is right for a percentage and wrong
    /// for a throughput that says "1.2MB/s", and the ceiling already knows which of those this is.
    ///
    /// A minimum and not a fixed width: if a reading ever does come out wider than its own ceiling, that
    /// row gives up a few pixels of track rather than clipping the number. Better to lose a little
    /// accuracy in one row than to show a figure with its last digit cut off.
    /// </summary>
    private double Reserve()
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var widest = 0d;

        foreach (var bar in _bars)
        {
            var ceiling = SensorScale.Ceiling(bar.Sensor, double.NaN);
            var sample = SensorFormat.Reading((float)ceiling, bar.Sensor.Unit, ShowUnit);

            var measured = new FormattedText(
                sample, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(bar.Reading.FontFamily, bar.Reading.FontStyle, bar.Reading.FontWeight,
                    bar.Reading.FontStretch),
                bar.Reading.FontSize, Brushes.Black, dpi);

            if (measured.Width > widest) widest = measured.Width;
        }

        // A hair of slack: a glyph measured to the pixel can still land on the wrong side of a rounding.
        return Math.Ceiling(widest) + 2;
    }

    private void Apply()
    {
        var upright = Direction == BarDirection.Vertical;

        // Side by side when standing, stacked when lying down. One row of N, or one column of N.
        _row.Rows = upright ? 1 : 0;
        _row.Columns = upright ? 0 : 1;

        Dress();

        foreach (var bar in _bars)
        {
            // Up from the floor, or out from the left wall: the bar is as thick as its look across, and takes all
            // the room there is along the way the reading grows.
            bar.Fill.Orientation = upright ? Orientation.Vertical : Orientation.Horizontal;
            bar.Fill.HorizontalAlignment = upright ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            bar.Fill.VerticalAlignment = upright ? VerticalAlignment.Stretch : VerticalAlignment.Center;

            // Turned or restyled, a bar is put at its reading again rather than swept there.
            bar.Shown = double.NaN;

            // Under the bar when standing, after it when lying down — always at the end of the reading. The gaps
            // grow with the widget, as the bars and the numbers do, or sixteen bars made twice as big would
            // close up into one block.
            DockPanel.SetDock(bar.Reading, upright ? Dock.Bottom : Dock.Right);
            bar.Column.Margin = (upright ? new Thickness(2, 0, 2, 0) : new Thickness(0, 2, 0, 2)).Scaled(_figureScale);
            bar.Reading.Margin = (upright ? new Thickness(0, 2, 0, 0) : new Thickness(6, 0, 0, 0)).Scaled(_figureScale);
            bar.Reading.VerticalAlignment = upright ? VerticalAlignment.Top : VerticalAlignment.Center;

            bar.Reading.Visibility = ShowValue ? Visibility.Visible : Visibility.Collapsed;
            bar.Reading.FontSize = ValueSize;
            bar.Reading.Wear(ValueFont, ValueWeight);
        }

        // After the faces are set, because the measurement depends on them — and once for every bar, so
        // they all reserve the same room whatever each one happens to be reading right now.
        //
        // Standing up there is nothing to reserve: the reading sits UNDER its bar, and the columns are a
        // UniformGrid, which already gives every one of them the same width.
        var reserve = upright || _bars.Count == 0 ? 0 : Reserve();

        foreach (var bar in _bars)
        {
            bar.Reading.MinWidth = reserve;
            bar.Reading.TextAlignment = upright ? TextAlignment.Center : TextAlignment.Right;

            Draw(bar, animate: false);
        }
    }

    // ── Style ────────────────────────────────────────────────────────

    private BarLook? _barLook;
    private bool _showLimits;
    private Brush? _limitBrush;
    private bool _showValue = true;

    /// <summary>Marks across each bar where its reading turns elevated and alert.</summary>
    public bool ShowLimits
    {
        get => _showLimits;
        set { _showLimits = value; Apply(); }
    }

    public Brush? LimitBrush
    {
        get => _limitBrush;
        set { _limitBrush = value; Apply(); }
    }
    private bool _showUnit = true;
    private double _valueSize = 9;
    private string? _valueFont;
    private TextWeight _valueWeight = TextWeight.Regular;

    /// <summary>The preset's bar, for every bar alike. Its thickness is each bar's WIDTH standing up: turned upright,
    /// the height is the reading, so thickness can only mean the other dimension.</summary>
    public BarLook? BarLook
    {
        get => _barLook;
        set { _barLook = value; Dress(); }
    }

    public bool ShowValue
    {
        get => _showValue;
        set { _showValue = value; Apply(); }
    }

    public bool ShowUnit
    {
        get => _showUnit;
        set { _showUnit = value; Apply(); }
    }

    public double ValueSize
    {
        get => _valueSize;
        set { _valueSize = value; Apply(); }
    }

    public string? ValueFont
    {
        get => _valueFont;
        set { _valueFont = value; Apply(); }
    }

    public TextWeight ValueWeight
    {
        get => _valueWeight;
        set { _valueWeight = value; Apply(); }
    }

    private double _figureScale = 1;

    /// <summary>For the gaps between the bars and between each bar and its number.</summary>
    public double FigureScale
    {
        get => _figureScale;
        set { _figureScale = value; Apply(); }
    }

    private BarDirection _direction = BarDirection.Vertical;

    /// <summary>Which way the bars run. A widget setting rather than a preset one: swapping preset must
    /// never turn an equaliser on its side.</summary>
    public BarDirection Direction
    {
        get => _direction;
        set { _direction = value; Apply(); }
    }

    /// <summary>Ignored, and declared only because <see cref="IMeterStyle"/> asks for it: Inline and Top
    /// describe where a label sits beside a horizontal bar, and these bars have no label beside them.
    /// Left on the interface rather than split out for one piece — a setting that visibly does nothing is
    /// cheaper than an interface nobody can remember the shape of.</summary>
    public MeterLayout Layout { get; set; } = MeterLayout.Beside;
}
