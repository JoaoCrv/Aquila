using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using Aquila.Controls.Primitives;
using Aquila.Models;
using Aquila.Services;

namespace Aquila.Controls;

/// <summary>
/// The Chart widget: one to four readings and their history, drawn by a <see cref="LinePlot"/>, with their numbers
/// where the widget says (<see cref="ReadingsAt"/>) — at the end of the title's line, at the lines' ends, in a legend
/// under the chart, or nowhere (docs/widget-grafico).
///
/// It took over from the sparkline, whose one number sat in the chart's corner over its newest readings, and from the
/// two-reading chart, which wrote none. The dashboard's cards keep their own <see cref="SparklineChart"/>; both turn
/// the preset into what the plot draws through <see cref="PlotDress"/>, so they cannot come to disagree.
///
/// Each reading is coloured by its own state, through its own ramp, and with <see cref="ColourByReading"/> each line is
/// painted in its states along the way. The scale and the limit lines are the first reading's, as they always were.
/// It takes hold of its readings and the limits on Loaded and lets go on Unloaded.
/// </summary>
public sealed class ChartTile : UserControl, IChartStyle, IValueStyle, IUnitStyle, INoteStyle, IPlotTextStyle,
    ILimitParts, ILimitStyle, IChartParts, IReadingRamp, ITitleAside, IScaledFigure
{
    /// <summary>The most readings a chart draws: four, each in a ramp of the preset's.</summary>
    public const int MostReadings = 4;

    /// <summary>A line nobody has painted yet — the colour the chart always fell back on.</summary>
    private static readonly Brush Unpainted = Frozen(Color.FromRgb(0x60, 0xCD, 0xFF));

    /// <summary>The notes' colour when nothing has dressed the piece — the type picker.</summary>
    private static readonly Brush QuietUnpainted = Frozen(Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF));

    /// <summary>Between two numbers in the title, and between two readings of the legend.</summary>
    private const double Apart = 10;

    /// <summary>From the chart down to its legend.</summary>
    private const double LegendGap = 4;

    /// <summary>A legend's swatch of its line's colour, and the gap after it.</summary>
    private const double SwatchWidth = 10, SwatchHeight = 2, SwatchGap = 5;

    private readonly SensorNode[] _sensors;
    private readonly LinePlot _plot = new();
    private readonly StackPanel _aside = new() { Orientation = Orientation.Horizontal };
    private readonly UniformGrid _legend = new() { Rows = 1 };
    private readonly TextBlock[] _titleNumbers;
    private readonly (TextBlock Name, TextBlock Number, Rectangle Swatch)[] _legendRows;

    private readonly Brush?[] _strokes;
    private readonly Color[]?[] _ramps;
    private readonly IReadOnlyList<(double From, Color Color)>?[] _bands;
    private Func<double, string>?[] _formats;

    private VitalMonitor? _monitor;
    private bool _hooked;

    public ChartTile(IReadOnlyList<SensorNode> sensors)
    {
        _sensors = [.. sensors.Take(MostReadings)];
        var count = _sensors.Length;
        _strokes = new Brush?[count];
        _ramps = new Color[]?[count];
        _bands = new IReadOnlyList<(double, Color)>?[count];
        _formats = new Func<double, string>?[count];
        _titleNumbers = new TextBlock[count];
        _legendRows = new (TextBlock, TextBlock, Rectangle)[count];

        for (var i = 0; i < count; i++)
        {
            var number = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            Typography.SetNumeralAlignment(number, FontNumeralAlignment.Tabular);
            _titleNumbers[i] = number;
            _aside.Children.Add(number);

            var swatch = new Rectangle
            {
                Width = SwatchWidth, Height = SwatchHeight, VerticalAlignment = VerticalAlignment.Center,
            };
            var name = new TextBlock
            {
                Text = _sensors[i].Name ?? string.Empty, TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var reading = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            Typography.SetNumeralAlignment(reading, FontNumeralAlignment.Tabular);

            var row = new DockPanel();
            DockPanel.SetDock(swatch, Dock.Left);
            DockPanel.SetDock(reading, Dock.Right);
            row.Children.Add(swatch);
            row.Children.Add(reading);
            row.Children.Add(name);

            _legendRows[i] = (name, reading, swatch);
            _legend.Children.Add(row);
        }

        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_legend, 1);
        root.Children.Add(_plot);
        root.Children.Add(_legend);
        Content = root;

        Loaded += (_, _) => Hook();
        Unloaded += (_, _) => Unhook();

        Format();
        Apply();
    }

    public FrameworkElement Aside => _aside;

    // ── Colours ──────────────────────────────────────────────────────

    /// <summary>Paints one reading — its line, its end, its numbers — in the colour of its state. Reached from the
    /// catalogue's painter, so each reading follows its own ramp and its own reading.</summary>
    public void PaintLine(int index, Brush brush)
    {
        if (index < 0 || index >= _sensors.Length) return;

        _strokes[index] = brush;
        Draw();
        Write();
    }

    /// <summary>A reading's whole ramp, which its bands are painted in when the chart is coloured by its readings.
    /// </summary>
    public void UseRamp(int index, Ramp ramp)
    {
        if (index < 0 || index >= _sensors.Length) return;

        var colours = PlotDress.Colours(ramp);
        if (_ramps[index] is { } kept && kept.SequenceEqual(colours)) return;

        _ramps[index] = colours;
        Band();
    }

    /// <summary>Each line's bands: its ramp's colours between its own reading's limits, or one colour throughout.
    /// Handed on only when they change, so a line is not given new bands on every reading.</summary>
    private void Band()
    {
        var changed = false;
        for (var i = 0; i < _sensors.Length; i++)
        {
            var bands = _colourByReading ? PlotDress.Bands(_sensors[i], _ramps[i]) : null;
            if (PlotDress.Same(_bands[i], bands)) continue;

            _bands[i] = bands;
            changed = true;
        }

        if (changed) Draw();
    }

    /// <summary>The lines, as the plot reads them. The same readings each time, so a repaint is never a new scale.
    /// </summary>
    private void Draw()
    {
        var lines = new PlotLine[_sensors.Length];
        for (var i = 0; i < lines.Length; i++)
            lines[i] = new PlotLine(_sensors[i].History, _strokes[i] ?? Unpainted, _bands[i], _formats[i]);

        if (_plot.Lines is not { } drawn || !drawn.SequenceEqual(lines)) _plot.Lines = lines;
    }

    /// <summary>How each reading is written, at the line ends — made again only when the unit is turned on or off, so
    /// a line keeps the same way of writing from reading to reading.</summary>
    private void Format()
    {
        _formats = new Func<double, string>?[_sensors.Length];
        for (var i = 0; i < _sensors.Length; i++)
        {
            var sensor = _sensors[i];
            var showUnit = _showUnit;
            _formats[i] = value => SensorFormat.Reading((float)value, sensor.Unit, showUnit);
        }
    }

    // ── The scale and the limits ─────────────────────────────────────

    /// <summary>The window, and the scale's ends — the first reading's fixed top, a percentage's hundred or a
    /// temperature's limit, unless the widget fits or fixes its own.</summary>
    private void Rescale()
    {
        _plot.Window = _pointCount;

        var top = _sensors.Length > 0 && SensorScale.Fixed(_sensors[0]) is { } fixedTop ? fixedTop : double.NaN;
        (_plot.RangeMin, _plot.RangeMax) = PlotDress.Range(_scale, _scaleMin, _scaleMax, top);
    }

    /// <summary>The first reading's limits, across the chart, when they are asked for.</summary>
    private void Guide()
    {
        var guides = _showLimits && _sensors.Length > 0 ? LimitMarks.Of(_sensors[0]) : null;
        if (!LimitMarks.Same(_plot.Guides, guides)) _plot.Guides = guides;
        _plot.GuideBrush = _limitBrush ?? LimitMarks.Unpainted;
    }

    /// <summary>The limits changed — in Settings, say: the lines across the chart, the bands, and a temperature's top,
    /// which follows its critical limit.</summary>
    private void OnLimitsChanged()
    {
        Rescale();
        Guide();
        Band();
    }

    // ── Numbers ──────────────────────────────────────────────────────

    /// <summary>
    /// The numbers where they are written now. In the title one reading is written at the reading's size and face, in
    /// its colour — the number a sparkline wrote in its corner, moved off the newest readings it covered — and several
    /// at the notes' size, each in its line's colour, as they would not fit at the reading's. In the legend each
    /// reading's name in the title's face and colour, and its number in its line's.
    /// </summary>
    private void Write()
    {
        for (var i = 0; i < _sensors.Length; i++)
        {
            var reading = _sensors[i].Reading(_showUnit, string.Empty);
            var ink = _strokes[i] ?? Unpainted;

            var title = _titleNumbers[i];
            title.Text = reading;
            title.Foreground = ink;

            var (_, number, swatch) = _legendRows[i];
            number.Text = reading;
            number.Foreground = ink;
            swatch.Fill = ink;
        }

        _aside.Visibility = _readingsAt == ReadingPlace.Title && _sensors.Length > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        _legend.Visibility = _readingsAt == ReadingPlace.Legend && _sensors.Length > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>Lays the numbers out at the sizes and in the faces they have now.</summary>
    private void Letter()
    {
        var single = _sensors.Length == 1;
        var notes = _noteBrush ?? QuietUnpainted;

        for (var i = 0; i < _sensors.Length; i++)
        {
            var title = _titleNumbers[i];
            title.FontSize = single ? Math.Max(1, _valueSize) : Math.Max(1, _noteSize);
            title.Wear(_valueFont, single ? _valueWeight : TextWeight.Regular);
            title.Margin = new Thickness(i == 0 ? 0 : Apart * _figureScale, 0, 0, 0);

            var (name, number, swatch) = _legendRows[i];
            name.FontSize = Math.Max(1, _noteSize);
            name.Wear(_titleFont, _titleWeight);
            name.Foreground = notes;
            number.FontSize = Math.Max(1, _noteSize);
            number.Wear(_valueFont, TextWeight.Regular);
            number.Margin = new Thickness(SwatchGap * _figureScale, 0, 0, 0);
            swatch.Width = SwatchWidth * _figureScale;
            swatch.Height = Math.Max(1, SwatchHeight * _figureScale);
            swatch.Margin = new Thickness(0, 0, SwatchGap * _figureScale, 0);
            ((FrameworkElement)swatch.Parent).Margin = new Thickness(0, 0, i < _sensors.Length - 1 ? Apart * _figureScale : 0, 0);
        }

        _legend.Margin = new Thickness(0, LegendGap * _figureScale, 0, 0);

        // What the plot writes — its line ends, its peak and its scale — in the notes' face, size and colour.
        var family = string.IsNullOrWhiteSpace(_valueFont) ? FontFamily : new FontFamily(_valueFont);
        _plot.Text = new TextLook(new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            Math.Max(1, _noteSize), notes, _haloBrush);
    }

    // ── Listening ────────────────────────────────────────────────────

    private void Hook()
    {
        if (_hooked) return;
        _hooked = true;

        foreach (var sensor in _sensors) sensor.PropertyChanged += OnSensorChanged;
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

        foreach (var sensor in _sensors) sensor.PropertyChanged -= OnSensorChanged;
        if (_monitor is not null) _monitor.Changed -= OnLimitsChanged;
        _monitor = null;
    }

    private void OnSensorChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SensorNode.Value)) Write();
    }

    /// <summary>Everything again, in place: every setting here is Style, and a Style change re-dresses a piece without
    /// rebuilding it.</summary>
    private void Apply()
    {
        _plot.Look = PlotDress.Look(_lineThickness, _fill, _fillOpacity, _smoothness, _pointSize);
        _plot.ShowEnd = _showEnd;
        _plot.ShowPeak = _showPeak;
        _plot.ShowScaleLabels = _showScaleLabels;
        _plot.ShowEndLabels = _readingsAt == ReadingPlace.LineEnds;

        Rescale();
        Guide();
        Band();
        Draw();
        Letter();
        Write();
    }

    // ── Style ────────────────────────────────────────────────────────

    private double _lineThickness = 1.5;
    private ChartFill _fill = ChartFill.Gradient;
    private double _fillOpacity = 0.30;
    private double _smoothness = 0.5;
    private double _pointSize;
    private int _pointCount = 60;
    private ChartScale _scale = ChartScale.FromZero;
    private double _scaleMin;
    private double _scaleMax = 100;

    private double _valueSize = 20;
    private string? _valueFont;
    private TextWeight _valueWeight = TextWeight.Regular;
    private bool _showUnit = true;
    private Brush? _noteBrush;
    private double _noteSize = 11;
    private Brush? _haloBrush;
    private string? _titleFont;
    private TextWeight _titleWeight = TextWeight.Regular;
    private double _figureScale = 1;

    private bool _showLimits;
    private Brush? _limitBrush;
    private bool _showEnd;
    private bool _colourByReading;
    private bool _showPeak;
    private bool _showScaleLabels;
    private ReadingPlace _readingsAt;

    public double LineThickness { get => _lineThickness; set { _lineThickness = value; Apply(); } }
    public ChartFill Fill { get => _fill; set { _fill = value; Apply(); } }
    public double FillOpacity { get => _fillOpacity; set { _fillOpacity = value; Apply(); } }
    public double Smoothness { get => _smoothness; set { _smoothness = value; Apply(); } }
    public double PointSize { get => _pointSize; set { _pointSize = value; Apply(); } }
    public int PointCount { get => _pointCount; set { _pointCount = value; Rescale(); } }
    public ChartScale Scale { get => _scale; set { _scale = value; Rescale(); } }
    public double ScaleMin { get => _scaleMin; set { _scaleMin = value; Rescale(); } }
    public double ScaleMax { get => _scaleMax; set { _scaleMax = value; Rescale(); } }

    public double ValueSize { get => _valueSize; set { _valueSize = value; Letter(); } }
    public string? ValueFont { get => _valueFont; set { _valueFont = value; Letter(); } }
    public TextWeight ValueWeight { get => _valueWeight; set { _valueWeight = value; Letter(); } }

    public bool ShowUnit
    {
        get => _showUnit;
        set { _showUnit = value; Format(); Draw(); Write(); }
    }

    public Brush? NoteBrush { get => _noteBrush; set { _noteBrush = value; Letter(); } }
    public double NoteSize { get => _noteSize; set { _noteSize = value; Letter(); } }
    public Brush? HaloBrush { get => _haloBrush; set { _haloBrush = value; Letter(); } }
    public string? TitleFont { get => _titleFont; set { _titleFont = value; Letter(); } }
    public TextWeight TitleWeight { get => _titleWeight; set { _titleWeight = value; Letter(); } }

    /// <summary>For the gaps between the numbers and the legend's swatches.</summary>
    public double FigureScale { get => _figureScale; set { _figureScale = value; Letter(); } }

    public bool ShowLimits { get => _showLimits; set { _showLimits = value; Guide(); } }
    public Brush? LimitBrush { get => _limitBrush; set { _limitBrush = value; Guide(); } }
    public bool ShowEnd { get => _showEnd; set { _showEnd = value; Apply(); } }
    public bool ColourByReading { get => _colourByReading; set { _colourByReading = value; Band(); } }
    public bool ShowPeak { get => _showPeak; set { _showPeak = value; Apply(); } }
    public bool ShowScaleLabels { get => _showScaleLabels; set { _showScaleLabels = value; Apply(); } }
    public ReadingPlace ReadingsAt { get => _readingsAt; set { _readingsAt = value; Apply(); } }

    private static Brush Frozen(Color colour)
    {
        var brush = new SolidColorBrush(colour);
        brush.Freeze();
        return brush;
    }
}
