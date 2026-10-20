using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Aquila.Models;
using Aquila.Models.Nodes;

namespace Aquila.Controls;

/// <summary>
/// A list, a row per item: the name on the left, the reading on the right, in the order the source gave them.
///
/// Built the way <see cref="SensorBars"/> is — in code, one visual per reading, each coloured on its own through
/// the catalogue — and not an <see cref="ISensorPiece"/> for the same reason: it holds a list rather than one
/// node, and lets go of it by itself.
///
/// It takes hold on Loaded rather than in the constructor, and lets go on Unloaded. Watching the list is what
/// makes the process source read at all, so the watch must end when the widget leaves the screen and start again
/// if it comes back — and "Send to screen" moves this very element from one window to another, which a
/// subscription taken once at construction and dropped at the first Unloaded would not survive.
///
/// Every row's name and number wear the VALUE role, not the title's. A row is one line at one size, and under the
/// title role the slider sizing the widget's heading would resize every row of the list along with it. Nothing
/// here aligns the reading either — the number column is the alignment — so "Reading align" does nothing on a
/// list, as on a dial.
/// </summary>
public sealed class SensorList : UserControl, IValueStyle, IUnitStyle, IListStyle
{
    private sealed record Row(TextBlock Name, TextBlock Reading);

    private readonly ListNode? _list;
    private readonly IReadOnlyList<SensorNode> _fixed;
    private readonly List<Row> _rows = [];

    // One Grid for every row rather than a panel per row: the numbers then share the width of the widest, so
    // they line up down the right edge and every name is cut off at the same place.
    private readonly Grid _grid = new() { VerticalAlignment = VerticalAlignment.Top };

    private bool _hooked;
    private IDisposable? _watch;
    private Func<float?, Brush>? _judge;

    /// <summary>A live list — what a List widget on the desktop draws.</summary>
    public SensorList(ListNode list) : this(list, []) { }

    /// <summary>Fixed rows, one per sensor: the type picker's preview, which must show what a list looks like
    /// without watching one — that would start the process source from a picker.</summary>
    public SensorList(IReadOnlyList<SensorNode> rows) : this(null, rows) { }

    private SensorList(ListNode? list, IReadOnlyList<SensorNode> rows)
    {
        _list = list;
        _fixed = rows;

        _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Content = _grid;

        Loaded += (_, _) => Hook();
        Unloaded += (_, _) => Unhook();

        Apply();
    }

    private void Hook()
    {
        if (_hooked) return;
        _hooked = true;

        if (_list is not null)
        {
            _list.PropertyChanged += OnListChanged;
            _watch = _list.Watch();
        }

        foreach (var row in _fixed) row.PropertyChanged += OnRowChanged;

        // Whatever changed while it was off screen — or what is there already, shown by another widget.
        Redraw();
    }

    private void Unhook()
    {
        if (!_hooked) return;
        _hooked = false;

        if (_list is not null) _list.PropertyChanged -= OnListChanged;
        foreach (var row in _fixed) row.PropertyChanged -= OnRowChanged;

        _watch?.Dispose();
        _watch = null;
    }

    /// <summary>Once per read, after every row's value has been written — so the list is drawn in its new order
    /// in one go, never through the half-sorted states in between.</summary>
    private void OnListChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ListNode.Rows)) Redraw();
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SensorNode.Value)) Redraw();
    }

    /// <summary>
    /// Colours every row's number by its own value. The judge comes from whoever paints, with the series' ramp
    /// and the limits already put together inside it — so the application at the top of a list can go amber
    /// while the rest do not, and this never has to learn what a ramp or a limit is.
    /// </summary>
    public void PaintRows(Func<float?, Brush> judge)
    {
        _judge = judge;
        Redraw();
    }

    private void Redraw()
    {
        var shown = _list?.Rows ?? _fixed;

        for (var i = 0; i < _rows.Count; i++)
        {
            var (name, reading) = _rows[i];

            // A row the list has not filled yet keeps its line, blank — an empty TextBlock is still a line high —
            // so the widget does not jump when the first rows arrive, a poll or two after it appears.
            var node = i < shown.Count ? shown[i] : null;

            name.Text = node?.Name ?? string.Empty;
            reading.Text = node.Reading(ShowUnit, string.Empty);

            if (node is not null && _judge is not null) reading.Foreground = _judge(node.Value);
        }
    }

    /// <summary>
    /// Lays the rows out again — as many as the widget asks for, each in the reading's face — and draws them.
    /// In place: rows are added or taken away and the watch is kept, because the count and the face are Style,
    /// and a Style change re-dresses a piece without rebuilding it.
    /// </summary>
    private void Apply()
    {
        while (_rows.Count < _itemCount) AddRow();
        while (_rows.Count > _itemCount) RemoveRow();

        foreach (var (name, reading) in _rows)
        {
            name.FontSize = ValueSize;
            reading.FontSize = ValueSize;
            name.Wear(ValueFont, ValueWeight);
            reading.Wear(ValueFont, ValueWeight);
        }

        Redraw();
    }

    private void AddRow()
    {
        var index = _rows.Count;
        _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Dimmed and never painted: the name inherits the widget's value colour, which is what text nothing is
        // judging is drawn in. The number is the reading, so the number alone wears the ramp.
        var name = new TextBlock { TextTrimming = TextTrimming.CharacterEllipsis, Opacity = 0.7 };

        var reading = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(8, 0, 0, 0),
        };

        Grid.SetRow(name, index);
        Grid.SetRow(reading, index);
        Grid.SetColumn(reading, 1);

        _grid.Children.Add(name);
        _grid.Children.Add(reading);
        _rows.Add(new Row(name, reading));
    }

    private void RemoveRow()
    {
        var (name, reading) = _rows[^1];

        _grid.Children.Remove(name);
        _grid.Children.Remove(reading);
        _grid.RowDefinitions.RemoveAt(_grid.RowDefinitions.Count - 1);
        _rows.RemoveAt(_rows.Count - 1);
    }

    // ── Style ────────────────────────────────────────────────────────

    private int _itemCount = 5;
    private bool _showUnit = true;
    private double _valueSize = 13;
    private string? _valueFont;
    private TextWeight _valueWeight = TextWeight.Regular;

    /// <summary>How many rows are shown — between one and <see cref="ListNode.MaxRows"/>, the most a list keeps,
    /// because the number comes from widgets.json and a file can be edited by hand. Rows the list has not got are
    /// drawn blank.</summary>
    public int ItemCount
    {
        get => _itemCount;
        set { _itemCount = Math.Clamp(value, 1, ListNode.MaxRows); Apply(); }
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
}
