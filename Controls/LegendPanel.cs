using System.Windows;
using System.Windows.Controls;

namespace Aquila.Controls;

/// <summary>
/// A chart's legend: its readings side by side when every name fits whole, and otherwise folded into lines — two
/// columns, then one — until they do (João, 2026-11-09: "collapse vertically when it does not fit across"). Never so
/// many lines that the legend takes more than <see cref="Room"/>, which leaves the chart its line: past that the names
/// are cut instead. The columns are equal, as a grid's, and a line holds as many as the others.
/// </summary>
public sealed class LegendPanel : Panel
{
    private const double Tolerance = 0.01;

    private double _gap, _lineGap, _room = double.PositiveInfinity;
    private int _across = 1;
    private double _line;

    /// <summary>Between two columns.</summary>
    public double Gap { get => _gap; set => Set(ref _gap, value); }

    /// <summary>Between two lines.</summary>
    public double LineGap { get => _lineGap; set => Set(ref _lineGap, value); }

    /// <summary>The most height the legend may take — set by the chart from its own, as it is measured.</summary>
    public double Room { get => _room; set => Set(ref _room, value); }

    private void Set(ref double field, double value)
    {
        if (field.Equals(value)) return;
        field = value;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size available)
    {
        var count = InternalChildren.Count;
        if (count == 0) return default;

        var natural = new double[count];
        var whole = new Size(double.PositiveInfinity, double.PositiveInfinity);
        _line = 0;
        for (var i = 0; i < count; i++)
        {
            InternalChildren[i].Measure(whole);
            natural[i] = InternalChildren[i].DesiredSize.Width;
            _line = Math.Max(_line, InternalChildren[i].DesiredSize.Height);
        }

        _across = Columns(natural, available.Width, Gap, _line, LineGap, Room);
        var column = double.IsInfinity(available.Width) ? natural.Max() : ColumnWidth(available.Width, Gap, _across);
        foreach (UIElement child in InternalChildren)
            child.Measure(new Size(column, _line));

        return new Size(_across * column + (_across - 1) * Gap, HeightOf(count, _across, _line, LineGap));
    }

    protected override Size ArrangeOverride(Size final)
    {
        var column = ColumnWidth(final.Width, Gap, _across);
        for (var i = 0; i < InternalChildren.Count; i++)
            InternalChildren[i].Arrange(new Rect(
                i % _across * (column + Gap), i / _across * (_line + LineGap), column, _line));
        return final;
    }

    /// <summary>
    /// How many columns a legend of these readings takes: the most in which every name is whole, so one line when they
    /// all fit across — then, if those lines would take more than <paramref name="room"/>, as many lines as it holds,
    /// names cut; and one line if even that is too tall. The lines are kept even: four readings in three columns are
    /// two lines either way, so they are drawn two and two.
    /// </summary>
    public static int Columns(IReadOnlyList<double> widths, double width, double gap, double line, double lineGap, double room)
    {
        var count = widths.Count;
        if (count == 0) return 1;
        if (!double.IsFinite(width)) return count;

        var widest = widths.Max();
        var across = 1;
        for (var c = count; c >= 1; c--)
            if (ColumnWidth(width, gap, c) >= widest - Tolerance) { across = c; break; }

        while (across < count && HeightOf(count, across, line, lineGap) > room + Tolerance) across++;

        var lines = Lines(count, across);
        return (count + lines - 1) / lines;
    }

    public static int Lines(int count, int across) => (count + across - 1) / Math.Max(1, across);

    public static double HeightOf(int count, int across, double line, double lineGap)
    {
        var lines = Lines(count, across);
        return lines * line + (lines - 1) * lineGap;
    }

    private static double ColumnWidth(double width, double gap, int across) =>
        Math.Max(0, (width - (across - 1) * gap) / across);
}
