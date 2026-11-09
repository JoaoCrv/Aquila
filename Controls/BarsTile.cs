using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using Aquila.Controls.Primitives;
using Aquila.Models;
using Aquila.Services;

namespace Aquila.Controls;

/// <summary>
/// The Bars widget: one to sixteen readings as bars, lying down or standing up, each with its name and its number
/// beside it or above it (docs/widget-barras). It took over from the meter — one bar lying down, its number beside —
/// and from the old Bars, whose layouts open as they were; the dashboard's cards keep their own <see cref="SensorMeter"/>.
///
/// Each bar is a <see cref="BarFill"/> on its own reading's scale, coloured by its own state through its own ramp. With
/// <see cref="ColourByReading"/> it is painted in the colour of each limit it has passed, a VU meter's; with
/// <see cref="ShowPeak"/> it holds its highest reading of the time window. Names and numbers are the widget's
/// (<see cref="IBarParts"/>), never the preset's.
///
/// Standing up, a number or a name that does not fit its column is not written, and its room goes to the bars — for
/// every column at once (<see cref="Fit"/>), so none is left with text and its neighbour without. It takes hold of its
/// readings and the limits on Loaded and lets go on Unloaded.
/// </summary>
public sealed class BarsTile : UserControl, IBarStyle, IValueStyle, IUnitStyle, IHideableValue, IOrientedBars,
    IScaledFigure, IFollowsSchemeTrack, ILimitParts, ILimitStyle, INoteStyle, IPlotTextStyle, IReadingRamp,
    IReadingNames, ITitleAside, IBarParts
{
    /// <summary>The most readings the widget draws — this machine's sixteen cores, and a ceiling, not a shape.</summary>
    public const int MostReadings = 16;

    /// <summary>The look of bars nobody has dressed — the type picker.</summary>
    private static readonly BarLook Undressed = new(6, 3, null);

    /// <summary>A number nobody has painted yet, and the names' colour before anything dresses the piece.</summary>
    private static readonly Brush Unpainted = Frozen(Color.FromRgb(0x60, 0xCD, 0xFF));
    private static readonly Brush QuietUnpainted = Frozen(Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF));

    /// <summary>The most of the width a name takes before its bar; past it, the name is cut.</summary>
    private const double NameShare = 0.35;

    /// <summary>The gaps, at a scale of one: between two bars, from a name to its bar, from a bar to its number, from a
    /// line of text down to its bar, and from a standing bar to what is written under it.</summary>
    private const double BetweenBars = 2, NameGap = 8, NumberGap = 6, TextGap = 2, UnderGap = 2;

    private sealed class Bar(SensorNode sensor)
    {
        public SensorNode Sensor { get; } = sensor;
        public BarFill Fill { get; } = new();
        public TextBlock Name { get; } = new() { TextTrimming = TextTrimming.CharacterEllipsis };
        public TextBlock Number { get; } = new();
        public Grid Cell { get; } = new();

        /// <summary>The share last sent to the bar; NaN until it has been put anywhere, so its first place is a jump.
        /// </summary>
        public double Shown { get; set; } = double.NaN;

        public Brush? Paint { get; set; }
        public Color[]? Ramp { get; set; }
    }

    private readonly Bar[] _bars;
    private readonly UniformGrid _cells = new();
    private readonly TextBlock _aside = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Inks _inks = new();

    private VitalMonitor? _monitor;
    private bool _hooked;

    public BarsTile(IReadOnlyList<SensorNode> sensors)
    {
        _bars = [.. sensors.Take(MostReadings).Select(sensor => new Bar(sensor))];
        Typography.SetNumeralAlignment(_aside, FontNumeralAlignment.Tabular);

        foreach (var bar in _bars)
        {
            bar.Name.Text = bar.Sensor.Name ?? string.Empty;
            Typography.SetNumeralAlignment(bar.Number, FontNumeralAlignment.Tabular);
            bar.Cell.Children.Add(bar.Name);
            bar.Cell.Children.Add(bar.Fill);
            bar.Cell.Children.Add(bar.Number);
            _cells.Children.Add(bar.Cell);
        }

        Content = _cells;
        SchemeTrack.Follow(this);

        Loaded += (_, _) => Hook();
        Unloaded += (_, _) => Unhook();

        Apply();
    }

    public FrameworkElement Aside => _aside;

    // ── Colours ──────────────────────────────────────────────────────

    /// <summary>Paints one bar — its fill, its number and, with no limits to judge it by, its peak — in the colour of
    /// its state. Reached from the catalogue's painter, so each bar follows its own ramp and its own reading.</summary>
    public void PaintBar(int index, Brush brush)
    {
        if (index < 0 || index >= _bars.Length) return;

        var bar = _bars[index];
        bar.Paint = brush;
        bar.Fill.Fill = brush;
        Hold(bar, Ceiling(bar));
        Write(bar);
    }

    /// <summary>A reading's whole ramp: its bands when the bar is coloured by its reading, and its peak's colour.
    /// </summary>
    public void UseRamp(int index, Ramp ramp)
    {
        if (index < 0 || index >= _bars.Length) return;

        var bar = _bars[index];
        var colours = PlotDress.Colours(ramp);
        if (bar.Ramp is { } kept && kept.SequenceEqual(colours)) return;

        bar.Ramp = colours;
        var ceiling = Ceiling(bar);
        Band(bar, ceiling);
        Hold(bar, ceiling);
    }

    /// <summary>Each bar's name (<see cref="ReadingNames.Of"/>).</summary>
    public void NameReadings(IReadOnlyList<(string Identifier, string Label)> names)
    {
        var written = ReadingNames.Of([.. _bars.Select(bar => bar.Sensor)], names);
        for (var i = 0; i < _bars.Length; i++) _bars[i].Name.Text = written[i];

        MeasureWords();
    }

    void IFollowsSchemeTrack.SchemeTrackChanged() => Dress();

    // ── A bar ────────────────────────────────────────────────────────

    private static double Ceiling(Bar bar) => SensorScale.Ceiling(bar.Sensor, double.NaN);

    private void Draw(Bar bar, bool animate)
    {
        var ceiling = Ceiling(bar);
        var share = ceiling > 0 ? Math.Clamp(bar.Sensor.Value ?? 0, 0, ceiling) / ceiling : 0;

        Mark(bar, ceiling);
        Band(bar, ceiling);
        Hold(bar, ceiling);

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

    /// <summary>A bar's bands: its ramp's colours from each of its reading's limits on, as shares of its own scale — or
    /// one colour, for a reading with no limits. Handed on only when they move.</summary>
    private void Band(Bar bar, double ceiling)
    {
        var bands = _colourByReading && ceiling > 0 && PlotDress.Bands(bar.Sensor, bar.Ramp) is { } limits
            ? limits.Select(band => (band.From / ceiling, band.Color)).ToArray()
            : null;

        if (!PlotDress.Same(bar.Fill.Bands, bands)) bar.Fill.Bands = bands;
    }

    /// <summary>
    /// The highest of the bar's last <see cref="PointCount"/> readings (<see cref="PlotGeometry.Peak"/>, the chart's own
    /// rule), held over the bar in the colour of its state — the band it falls in, or the bar's own colour for a reading
    /// with no limits. Worked out again at each reading, so it falls by itself once that reading leaves the window.
    /// </summary>
    private void Hold(Bar bar, double ceiling)
    {
        var history = bar.Sensor.History;
        var at = _showPeak && ceiling > 0 ? PlotGeometry.Peak(history, _pointCount) : -1;
        if (at < 0)
        {
            bar.Fill.Peak = double.NaN;
            return;
        }

        var peak = history[PlotGeometry.Visible(history.Count, _pointCount).First + at];
        bar.Fill.Peak = Math.Clamp(peak, 0, ceiling) / ceiling;
        bar.Fill.PeakBrush = PlotDress.Bands(bar.Sensor, bar.Ramp) is { } bands
                             && BarGeometry.BandAt([.. bands.Select(band => band.From)], peak) is >= 0 and var state
            ? _inks.Solid(bands[state].Color)
            : bar.Paint;
    }

    // ── Words ────────────────────────────────────────────────────────

    /// <summary>One bar lying down with its text above it writes its number at the end of the title's line, at the
    /// reading's size — the line it would have had over the bar is the title's.</summary>
    private bool InTitle => _bars.Length == 1 && _direction == BarDirection.Horizontal && _barLayout == BarLayout.Above;

    private bool Lying => _direction == BarDirection.Horizontal;

    /// <summary>What fits standing up, decided for every column at once; lying down, everything does.</summary>
    private (bool Numbers, bool Unit, bool Names) _fits = (true, true, true);

    private bool NamesShown => _showNames && _bars.Length > 1 && (Lying || _fits.Names);

    private bool NumbersShown => _showValue && !InTitle && (Lying || _fits.Numbers);

    /// <summary>
    /// Whether standing columns <paramref name="column"/> wide write their numbers — with their unit if the widest fits
    /// with it, without if only that fits, or none at all — and their names: all of them or none.
    /// </summary>
    public static (bool Numbers, bool Unit, bool Names) Fit(double column, double withUnit, double withoutUnit,
        double name)
    {
        var unit = !(withUnit > column);
        return (unit || !(withoutUnit > column), unit, !(name > column));
    }

    private void Write()
    {
        foreach (var bar in _bars) Write(bar);
    }

    /// <summary>A bar's number, in its colour — dropping the unit where a standing column cannot hold it — and the
    /// title's, for the first.</summary>
    private void Write(Bar bar)
    {
        bar.Number.Text = bar.Sensor.Reading(_showUnit && (Lying || _fits.Unit), string.Empty);
        bar.Number.Foreground = bar.Paint ?? Unpainted;

        if (!ReferenceEquals(bar, _bars[0])) return;
        _aside.Text = bar.Sensor.Reading(_showUnit, string.Empty);
        _aside.Foreground = bar.Paint ?? Unpainted;
    }

    /// <summary>Which words are on screen now.</summary>
    private void Show()
    {
        foreach (var bar in _bars)
        {
            bar.Name.Visibility = NamesShown ? Visibility.Visible : Visibility.Collapsed;
            bar.Number.Visibility = NumbersShown ? Visibility.Visible : Visibility.Collapsed;
        }

        _aside.Visibility = _showValue && InTitle ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>The words at the sizes and in the faces they have now: one bar's number at the reading's size, several
    /// at the notes', as the chart's are; the names in the title's face, at the notes' size and colour.</summary>
    private void Letter()
    {
        var single = _bars.Length == 1;

        foreach (var bar in _bars)
        {
            bar.Number.FontSize = Math.Max(1, single ? _valueSize : _noteSize);
            bar.Number.Wear(_valueFont, single ? _valueWeight : TextWeight.Regular);

            bar.Name.FontSize = Math.Max(1, _noteSize);
            bar.Name.Wear(_titleFont, _titleWeight);
            bar.Name.Foreground = _noteBrush ?? QuietUnpainted;
        }

        _aside.FontSize = Math.Max(1, _valueSize);
        _aside.Wear(_valueFont, _valueWeight);

        MeasureWords();
    }

    // ── Room ─────────────────────────────────────────────────────────

    /// <summary>The widest number each bar can write — at the top of its scale, with its unit and without — and the
    /// widest name: what the columns are sized by, and what a standing column must hold.</summary>
    private (double Number, double Bare, double Name) _widest;

    /// <summary>
    /// Measures the words the layout is decided by. Each number at the TOP of its bar's scale rather than as it reads
    /// now, so the room kept for it does not change from reading to reading — the track beside it stays put, and every
    /// bar's track is the same length, which is the only way bars can be compared.
    /// </summary>
    private void MeasureWords()
    {
        var (number, bare, name) = (0d, 0d, 0d);

        foreach (var bar in _bars)
        {
            var ceiling = (float)Ceiling(bar);
            number = Math.Max(number, WidthOf(SensorFormat.Reading(ceiling, bar.Sensor.Unit, _showUnit), bar.Number));
            bare = Math.Max(bare, WidthOf(SensorFormat.Reading(ceiling, bar.Sensor.Unit, false), bar.Number));
            name = Math.Max(name, WidthOf(bar.Name.Text, bar.Name));
        }

        _widest = (number, bare, name);
        InvalidateMeasure();
    }

    private double WidthOf(string text, TextBlock like)
    {
        var measured = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(like.FontFamily, like.FontStyle, like.FontWeight, like.FontStretch), like.FontSize,
            Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);

        // A hair of slack: a glyph measured to the pixel can still land on the wrong side of a rounding.
        return Math.Ceiling(measured.WidthIncludingTrailingWhitespace) + 2;
    }

    /// <summary>
    /// The room the words take, decided where the width is known. Lying down with the text beside, the names' column
    /// (no more than <see cref="NameShare"/> of the width) and the numbers' column, the same for every row. Standing up,
    /// whether the numbers and the names fit their columns at all (<see cref="Fit"/>).
    /// </summary>
    protected override Size MeasureOverride(Size constraint)
    {
        if (Lying)
        {
            _fits = (true, true, true);

            if (_barLayout == BarLayout.Beside)
            {
                var names = NamesShown ? _widest.Name + NameGap * _figureScale : 0;
                if (double.IsFinite(constraint.Width)) names = Math.Min(names, constraint.Width * NameShare);
                var numbers = NumbersShown ? _widest.Number + NumberGap * _figureScale : 0;

                foreach (var bar in _bars)
                {
                    Widen(bar.Cell.ColumnDefinitions[0], names);
                    Widen(bar.Cell.ColumnDefinitions[2], numbers);
                }
            }
        }
        else
        {
            var column = constraint.Width / Math.Max(1, _bars.Length) - 2 * BetweenBars * _figureScale;
            var fits = Fit(column, _widest.Number, _widest.Bare, _widest.Name);
            if (fits != _fits)
            {
                _fits = fits;
                Show();
                Write();
            }
        }

        return base.MeasureOverride(constraint);
    }

    private static void Widen(ColumnDefinition column, double width)
    {
        if (column.Width.Value != width || !column.Width.IsAbsolute) column.Width = new GridLength(Math.Max(0, width));
    }

    /// <summary>
    /// Where each bar and its words sit. Lying down, a row each, stacked: beside — name, bar, number on one line — or
    /// above — name and number on a line over the bar. Standing up, a column each, side by side: the number over the bar
    /// or under it, and the name under everything.
    /// </summary>
    private void Lay()
    {
        var lying = Lying;
        var above = _barLayout == BarLayout.Above;
        var scale = _figureScale;

        _cells.Rows = lying ? 0 : 1;
        _cells.Columns = lying ? 1 : 0;

        foreach (var bar in _bars)
        {
            var (cell, fill, name, number) = (bar.Cell, bar.Fill, bar.Name, bar.Number);
            cell.RowDefinitions.Clear();
            cell.ColumnDefinitions.Clear();
            Grid.SetColumnSpan(fill, 1);

            // Turned or restyled, a bar is put at its reading again rather than swept there.
            bar.Shown = double.NaN;
            fill.Orientation = lying ? Orientation.Horizontal : Orientation.Vertical;
            fill.HorizontalAlignment = lying ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
            fill.VerticalAlignment = lying ? VerticalAlignment.Center : VerticalAlignment.Stretch;
            cell.VerticalAlignment = lying ? VerticalAlignment.Center : VerticalAlignment.Stretch;

            // Rows with a line of text over them are spaced by the room they share alone, which is all that four of
            // them have at the size the design draws them.
            cell.Margin = (lying ? above ? default : new Thickness(0, BetweenBars, 0, BetweenBars)
                : new Thickness(BetweenBars, 0, BetweenBars, 0)).Scaled(scale);

            if (lying && !above)
            {
                cell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
                cell.ColumnDefinitions.Add(new ColumnDefinition());
                cell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
                Place(name, 0, 0, HorizontalAlignment.Left, new Thickness(0, 0, NameGap * scale, 0));
                Place(fill, 0, 1);
                Place(number, 0, 2, HorizontalAlignment.Right, new Thickness(NumberGap * scale, 0, 0, 0));
            }
            else if (lying)
            {
                // The bar is measured first and always gets its thickness; short of room, the text gives way.
                cell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                cell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                cell.ColumnDefinitions.Add(new ColumnDefinition());
                cell.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Place(name, 0, 0, HorizontalAlignment.Left, new Thickness(0, 0, NameGap * scale, TextGap * scale));
                Place(number, 0, 1, HorizontalAlignment.Right, new Thickness(0, 0, 0, TextGap * scale));
                Place(fill, 1, 0);
                Grid.SetColumnSpan(fill, 2);
            }
            else
            {
                // Over the bar, or under it — and the name under everything.
                var (numberRow, barRow) = above ? (0, 1) : (1, 0);
                for (var row = 0; row < 3; row++)
                    cell.RowDefinitions.Add(new RowDefinition
                    {
                        Height = row == barRow ? new GridLength(1, GridUnitType.Star) : GridLength.Auto,
                    });

                Place(fill, barRow, 0);
                Place(number, numberRow, 0, HorizontalAlignment.Center,
                    above ? new Thickness(0, 0, 0, UnderGap * scale) : new Thickness(0, UnderGap * scale, 0, 0));
                Place(name, 2, 0, HorizontalAlignment.Center, new Thickness(0, UnderGap * scale, 0, 0));
            }

            name.VerticalAlignment = VerticalAlignment.Center;
            number.VerticalAlignment = VerticalAlignment.Center;
            number.TextAlignment = lying ? TextAlignment.Right : TextAlignment.Center;
        }
    }

    private static void Place(FrameworkElement element, int row, int column,
        HorizontalAlignment along = HorizontalAlignment.Stretch, Thickness margin = default)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        element.HorizontalAlignment = along;
        element.Margin = margin;
    }

    /// <summary>Every bar's look: the preset's, or the type picker's default — over the dashboard's track when the look
    /// names none. Under a line of text it is the list's row's, two thirds of the preset's bar
    /// (<see cref="IBarStyle.UnderTextShare"/>): the preset's was written for one bar that is the widget's reading.
    /// </summary>
    private void Dress()
    {
        var look = SchemeTrack.Resolve(this, _barLook, Undressed);
        if (Lying && _barLayout == BarLayout.Above && _bars.Length > 1) look = look.Scaled(IBarStyle.UnderTextShare);
        foreach (var bar in _bars) bar.Fill.Look = look;
    }

    // ── Listening ────────────────────────────────────────────────────

    private void Hook()
    {
        if (_hooked) return;
        _hooked = true;

        foreach (var bar in _bars) bar.Sensor.PropertyChanged += OnSensorChanged;
        _monitor = VitalMonitor.Current;
        if (_monitor is not null) _monitor.Changed += OnLimitsChanged;

        // Whatever changed while it was off screen.
        OnLimitsChanged();
        Write();
    }

    private void Unhook()
    {
        if (!_hooked) return;
        _hooked = false;

        foreach (var bar in _bars) bar.Sensor.PropertyChanged -= OnSensorChanged;
        if (_monitor is not null) _monitor.Changed -= OnLimitsChanged;
        _monitor = null;
    }

    /// <summary>The limits changed — in Settings, say: every bar's marks and bands, and a temperature's top, which
    /// follows its critical limit, with the room its number takes.</summary>
    private void OnLimitsChanged()
    {
        foreach (var bar in _bars) Draw(bar, animate: true);
        MeasureWords();
    }

    private void OnSensorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SensorNode.Value) || sender is not SensorNode node) return;

        foreach (var bar in _bars)
        {
            if (!ReferenceEquals(bar.Sensor, node)) continue;
            Draw(bar, animate: true);
            Write(bar);
        }
    }

    /// <summary>Everything again, in place: every setting here is Style, and a Style change re-dresses a piece without
    /// rebuilding it.</summary>
    private void Apply()
    {
        Lay();
        Dress();
        Letter();
        Show();
        Write();
        foreach (var bar in _bars) Draw(bar, animate: false);
    }

    // ── Style ────────────────────────────────────────────────────────

    private BarLook? _barLook;
    private double _valueSize = 13;
    private string? _valueFont;
    private TextWeight _valueWeight = TextWeight.Regular;
    private bool _showUnit = true;
    private bool _showValue = true;
    private Brush? _noteBrush;
    private double _noteSize = 11;
    private string? _titleFont;
    private TextWeight _titleWeight = TextWeight.Regular;
    private double _figureScale = 1;
    /// <summary>Lying down until the widget says otherwise — as new Bars start, so the type picker's picture, which
    /// nothing tells, shows them so.</summary>
    private BarDirection _direction = BarDirection.Horizontal;

    private bool _showLimits;
    private Brush? _limitBrush;
    private BarLayout _barLayout;
    private bool _showNames;
    private bool _colourByReading;
    private bool _showPeak;
    private int _pointCount = 60;

    /// <summary>The preset's bar, for every bar alike. Its thickness is each bar's WIDTH standing up: turned upright,
    /// the height is the reading, so thickness can only mean the other dimension.</summary>
    public BarLook? BarLook { get => _barLook; set { _barLook = value; Dress(); } }

    public double ValueSize { get => _valueSize; set { _valueSize = value; Letter(); } }
    public string? ValueFont { get => _valueFont; set { _valueFont = value; Letter(); } }
    public TextWeight ValueWeight { get => _valueWeight; set { _valueWeight = value; Letter(); } }
    public bool ShowUnit { get => _showUnit; set { _showUnit = value; MeasureWords(); Write(); } }
    public bool ShowValue { get => _showValue; set { _showValue = value; Show(); InvalidateMeasure(); } }
    public Brush? NoteBrush { get => _noteBrush; set { _noteBrush = value; Letter(); } }
    public double NoteSize { get => _noteSize; set { _noteSize = value; Letter(); } }

    /// <summary>Asked of a chart for the halo round what it writes over its lines; bars write over nothing.</summary>
    public Brush? HaloBrush { get; set; }

    public string? TitleFont { get => _titleFont; set { _titleFont = value; Letter(); } }
    public TextWeight TitleWeight { get => _titleWeight; set { _titleWeight = value; Letter(); } }

    /// <summary>For the gaps between the bars and their words, and the marks' reach.</summary>
    public double FigureScale { get => _figureScale; set { _figureScale = value; Apply(); } }

    /// <summary>Which way the bars run. A widget setting rather than a preset one: swapping preset must never turn an
    /// equaliser on its side.</summary>
    public BarDirection Direction { get => _direction; set { _direction = value; Apply(); } }

    public bool ShowLimits { get => _showLimits; set { _showLimits = value; Apply(); } }
    public Brush? LimitBrush { get => _limitBrush; set { _limitBrush = value; Apply(); } }
    public BarLayout BarLayout { get => _barLayout; set { _barLayout = value; Apply(); } }
    public bool ShowNames { get => _showNames; set { _showNames = value; Show(); MeasureWords(); } }
    public bool ColourByReading { get => _colourByReading; set { _colourByReading = value; Apply(); } }
    public bool ShowPeak { get => _showPeak; set { _showPeak = value; Apply(); } }
    public int PointCount { get => _pointCount; set { _pointCount = value; Apply(); } }

    private static Brush Frozen(Color colour)
    {
        var brush = new SolidColorBrush(colour);
        brush.Freeze();
        return brush;
    }
}
