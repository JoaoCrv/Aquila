using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// A row of bars, one per reading — an equaliser standing up, or a stack of meters lying down.
///
/// Which way it runs is <see cref="IOrientedBars"/>, and the two are the same drawing with the dimensions
/// swapped: the reading grows along one axis and the thickness holds the other. The awkward part is not the
/// layout but the release — a bar turned on its side while still holding the Height it grew to would keep
/// it, so the measurement that stops being the reading is cleared along with its animation.
///
/// The only piece whose count is the hardware's to decide: sixteen CPU cores make sixteen bars. Everything
/// else in the catalogue draws a number of readings fixed when the kind was written, which is why the
/// colour contract had to grow a <see cref="Aquila.Services.SeriesPaint.Many"/> before this could exist.
///
/// Not an <see cref="ISensorPiece"/>, for the same reason <see cref="SparklineChart"/> is not: that
/// interface exists so a piece holding ONE node can be told to let go of it, and this holds a list. It
/// unsubscribes on Unloaded, by itself.
///
/// It is also the first bar in the app that can animate. <c>BarGroup</c>, the per-core bars on the
/// dashboard, rebuilds its items from scratch every tick — new records, new containers — so there is
/// nothing persistent to animate; a sweep would restart from zero every second. Here each bar is built
/// once and only its height moves, which is the same discipline the gauge already needs.
/// </summary>
public sealed class SensorBars : UserControl, IMeterStyle, IUnitStyle, IOrientedBars
{
    private sealed record Bar(
        SensorNode Sensor, Border Track, Border Fill, TextBlock Reading, DockPanel Column)
    {
        public double Shown { get; set; } = double.NaN;
    }

    private readonly List<Bar> _bars = [];
    private readonly UniformGrid _row = new() { Rows = 1 };

    public SensorBars(IReadOnlyList<SensorNode> sensors)
    {
        Content = _row;

        foreach (var sensor in sensors)
        {
            var fill = new Border { VerticalAlignment = VerticalAlignment.Bottom, Height = 0 };

            var track = new Border
            {
                Child = fill,
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = TryFindResource("Aquila.Scheme.Track") as Brush ?? Brushes.Transparent,
            };

            var reading = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0),
                Opacity = 0.7,
            };

            var column = new DockPanel { Margin = new Thickness(2, 0, 2, 0) };
            DockPanel.SetDock(reading, Dock.Bottom);
            column.Children.Add(reading);
            column.Children.Add(track);

            var bar = new Bar(sensor, track, fill, reading, column);
            _bars.Add(bar);
            _row.Children.Add(column);

            // The fill's height is a fraction of a track whose size is only known once laid out, and it
            // changes again whenever the widget is resized. Both are answered in the same place.
            track.SizeChanged += (_, _) => Draw(bar, animate: false);
            sensor.PropertyChanged += OnSensorChanged;
        }

        Loaded += (_, _) => Apply();
        Unloaded += (_, _) =>
        {
            foreach (var bar in _bars) bar.Sensor.PropertyChanged -= OnSensorChanged;
        };
    }

    private void OnSensorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SensorNode.Value)) return;
        if (sender is not SensorNode node) return;

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnSensorChanged(sender, e));
            return;
        }

        foreach (var bar in _bars)
            if (ReferenceEquals(bar.Sensor, node))
                Draw(bar, animate: true);
    }

    /// <summary>Colours one bar. Reached from the catalogue's painter, so each bar follows its own series'
    /// ramp and its own reading's state — sixteen cores can go amber one at a time.</summary>
    public void PaintBar(int index, Brush brush)
    {
        if (index >= 0 && index < _bars.Count) _bars[index].Fill.Background = brush;
    }

    private void Draw(Bar bar, bool animate)
    {
        var upright = Direction == BarDirection.Vertical;

        // The dimension the reading grows along is the one the direction chooses; the other is the bar's
        // thickness and never moves. One property is animated and the other is left alone, which is why
        // the unused one has to be cleared — a bar turned on its side while holding a Height would keep
        // it and refuse to fill.
        var room = upright ? bar.Track.ActualHeight : bar.Track.ActualWidth;
        if (room <= 0) return;

        var ceiling = SensorScale.Ceiling(bar.Sensor, double.NaN);
        var value = Math.Clamp(bar.Sensor.Value ?? 0, 0, ceiling);
        var target = ceiling > 0 ? room * value / ceiling : 0;

        bar.Reading.Text = ShowValue ? bar.Sensor.Reading(ShowUnit, string.Empty) : string.Empty;

        if (Math.Abs(target - bar.Shown) < 0.01) return;
        bar.Shown = target;

        var grows = upright ? HeightProperty : WidthProperty;

        // Same shape as SensorBar: no From, so a reading landing mid-sweep redirects rather than starting
        // again, and the property is released when motion is off or an old animation would hold it frozen.
        if (!animate || !Motion.Enabled)
        {
            bar.Fill.BeginAnimation(grows, null);
            bar.Fill.SetValue(grows, target);
            return;
        }

        bar.Fill.BeginAnimation(grows,
            new DoubleAnimation(target, Motion.Speed) { EasingFunction = Motion.WpfEasing });
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
            var (number, unit) = SensorFormat.Parts((float)ceiling, bar.Sensor.Unit);
            var sample = ShowUnit && !string.IsNullOrEmpty(unit) ? number + unit : number;

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

        foreach (var bar in _bars)
        {
            // Thickness is always the dimension the reading does NOT grow along, and the other is cleared
            // to Auto so the track can take all the room there is for the reading to fill.
            bar.Track.Width = upright ? BarThickness : double.NaN;
            bar.Track.Height = upright ? double.NaN : BarThickness;
            bar.Track.HorizontalAlignment = upright ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            bar.Track.VerticalAlignment = upright ? VerticalAlignment.Stretch : VerticalAlignment.Center;

            // Up from the floor, or out from the left wall.
            bar.Fill.VerticalAlignment = upright ? VerticalAlignment.Bottom : VerticalAlignment.Stretch;
            bar.Fill.HorizontalAlignment = upright ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;

            // The measurement that is no longer the reading has to be released, or a bar turned from one
            // direction to the other keeps the size it grew to in the old one.
            bar.Fill.BeginAnimation(upright ? WidthProperty : HeightProperty, null);
            bar.Fill.ClearValue(upright ? WidthProperty : HeightProperty);
            bar.Shown = double.NaN;

            bar.Fill.CornerRadius = new CornerRadius(BarCorner);
            bar.Track.CornerRadius = new CornerRadius(BarCorner);

            // Under the bar when standing, after it when lying down — always at the end of the reading.
            DockPanel.SetDock(bar.Reading, upright ? Dock.Bottom : Dock.Right);
            bar.Column.Margin = upright ? new Thickness(2, 0, 2, 0) : new Thickness(0, 2, 0, 2);
            bar.Reading.Margin = upright ? new Thickness(0, 2, 0, 0) : new Thickness(6, 0, 0, 0);
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

    private double _barThickness = 6;
    private double _barCorner = 3;
    private bool _showValue = true;
    private bool _showUnit = true;
    private double _valueSize = 9;
    private string? _valueFont;
    private TextWeight _valueWeight = TextWeight.Regular;

    /// <summary>Each bar's WIDTH. On a horizontal meter this is the bar's height; turned upright, the
    /// height is the reading, so thickness can only mean the other dimension.</summary>
    public double BarThickness
    {
        get => _barThickness;
        set { _barThickness = value; Apply(); }
    }

    public double BarCorner
    {
        get => _barCorner;
        set { _barCorner = value; Apply(); }
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
