using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Aquila.Controls.Primitives;
using Aquila.Models;
using Aquila.Models.Nodes;

namespace Aquila.Controls;

/// <summary>
/// A list, a row per item: the name on the left, the reading on the right, in the order the source gave them — and,
/// where the widget asks for them, a bar under each row, the row's recent trend beside its number, and the whole
/// list's total at the end of the title's line.
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
/// A row is FOLLOWED, never re-lettered — the design it came from (docs/widget-processos), applied. Every row the
/// source keeps has a row here, found by its node and never by its place, standing where the source puts it: the
/// first <see cref="ItemCount"/> on screen, the rest below them out of sight. So an application climbing the list
/// slides up into view as it fades in, one falling out of the top slides down out of view as it fades, and the rest
/// move up or down to make room — name, number, trend and bar together, so no text ever changes in place. A node new
/// to the source fades in already in its place; one the source has dropped fades out where it stands, and its row
/// goes back to a pool for the next. The order is still the source's alone: moving a row is drawing it where
/// <see cref="ListNode.Rows"/> puts it. The trend is the node's own <see cref="SensorNode.History"/>, which the node
/// keeps for as long as its application stays in the list — it belongs to the application, not to the place.
///
/// Every movement goes through <see cref="Motion.To"/> — the slide, the fades and the bar — at the speed in force,
/// which is held to nine tenths of the poll, so a row has always arrived before the next read. The number never
/// moves: it is the reading, and it changes at once. With motion off, everything is simply where it belongs.
///
/// Every row's name and number wear the VALUE role, not the title's. A row is one line at one size, and under the
/// title role the slider sizing the widget's heading would resize every row of the list along with it. Nothing
/// here aligns the reading either — the number column is the alignment — so "Reading align" does nothing on a
/// list, as on a dial.
///
/// Two figures, as the clock has two. Without bars it is the column of text it always was, its rows at the
/// reading's size. With them it is the design's card, whose one large number is the total: the total is drawn at
/// the reading's size, and the rows at <see cref="RowShare"/> of it, as the clock writes its date and seconds at a
/// share of its time.
/// </summary>
public sealed class SensorList : UserControl, IValueStyle, IUnitStyle, IListStyle, IBarStyle, IScaledFigure,
    ITitleAside, IFollowsSchemeTrack
{
    /// <summary>Where a row stands: on screen; below the rows shown, kept out of sight so it can come back moving;
    /// or dropped by the source, on its way back to the pool.</summary>
    private enum Presence { Shown, Hidden, Gone }

    /// <summary>One row. Made once and handed from node to node through the pool, so a list that has run for a day
    /// has the rows it had in its first minute.</summary>
    private sealed class Row(Grid root, TranslateTransform place, TextBlock name, TextBlock reading, LinePlot trend,
        BarFill bar)
    {
        public Grid Root { get; } = root;
        public TranslateTransform Place { get; } = place;
        public TextBlock Name { get; } = name;
        public TextBlock Reading { get; } = reading;
        public LinePlot Trend { get; } = trend;
        public BarFill Bar { get; } = bar;

        /// <summary>The node this row draws, or null while it waits in the pool.</summary>
        public SensorNode? Node { get; set; }

        /// <summary>Where the row is going. Out of sight — hidden, or in the pool — it is collapsed, and takes no
        /// part in layout.</summary>
        public Presence Presence { get; set; } = Presence.Hidden;

        /// <summary>Counts every change of presence, so a fade that ends late knows it has been overtaken.</summary>
        public int Turn { get; set; }

        /// <summary>The draw in which the source last had this row's node: whatever it did not have, it dropped.</summary>
        public int Pass { get; set; }

        /// <summary>Where the row was last sent, and how long its bar. NaN until it has been put anywhere, so its first
        /// placing is a jump — a row cannot slide from nowhere, nor a bar sweep up from a length it never had.</summary>
        public double Y { get; set; } = double.NaN;

        public double Shown { get; set; } = double.NaN;
    }

    private readonly ListNode? _list;
    private readonly IReadOnlyList<SensorNode> _fixed;

    private readonly Dictionary<SensorNode, Row> _byNode = [];
    private readonly Stack<Row> _pool = [];
    private readonly List<Row> _all = [];
    private readonly List<Row> _gone = [];

    // Every row in one cell, at the top, each drawn at its own height by its transform — so a row can be anywhere
    // between two places while it moves, which a row of a Grid cannot. Clipped, so a row sliding down out of view
    // goes under the edge of the list rather than over whatever is below it.
    private readonly Grid _stage = new() { VerticalAlignment = VerticalAlignment.Top, ClipToBounds = true };

    // The list's total. Not in this piece's own tree: it is handed to the title's line (ITitleAside), and stays ours
    // to fill, colour and hide.
    private readonly TextBlock _total = new() { VerticalAlignment = VerticalAlignment.Bottom };

    private bool _hooked;
    private IDisposable? _watch;
    private Func<float?, Brush>? _judge;
    private int _pass;

    // The layout, worked out in Apply: the size the rows are written at, a line of them, the distance from one row to
    // the next, and the face the numbers are measured in.
    private double _rowSize;
    private double _line;
    private double _step;
    private Typeface _typeface = new("Segoe UI");

    // The room every number is given (see Widen), and what each shape of number has measured, until the next dressing.
    private double _readingWidth;
    private readonly Dictionary<string, double> _widths = [];

    /// <summary>A live list — what a List widget on the desktop draws.</summary>
    public SensorList(ListNode list) : this(list, []) { }

    /// <summary>Fixed rows, one per sensor: the type picker's preview, which must show what a list looks like
    /// without watching one — that would start the process source from a picker. It has no total to show.</summary>
    public SensorList(IReadOnlyList<SensorNode> rows) : this(null, rows) { }

    private SensorList(ListNode? list, IReadOnlyList<SensorNode> rows)
    {
        _list = list;
        _fixed = rows;

        Typography.SetNumeralAlignment(_total, FontNumeralAlignment.Tabular);
        Content = _stage;

        Loaded += (_, _) => Hook();
        Unloaded += (_, _) => Unhook();

        SchemeTrack.Follow(this);
        Apply();
    }

    public FrameworkElement Aside => _total;

    private IReadOnlyList<SensorNode> Source => _list?.Rows ?? _fixed;

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

        // Laid out again now the face it inherits is the window's, and drawn as things are — whatever changed while it
        // was off screen, or what is there already, shown by another widget.
        Apply();
    }

    private void Unhook()
    {
        if (!_hooked) return;
        _hooked = false;

        if (_list is not null) _list.PropertyChanged -= OnListChanged;
        foreach (var row in _fixed) row.PropertyChanged -= OnRowChanged;

        _watch?.Dispose();
        _watch = null;

        // Everything put where it was going and every dropped row let go: a widget sent to another screen arrives with
        // nothing in flight, and one taken away leaves no animation holding on to it.
        Redraw(animate: false);
    }

    /// <summary>Once per read, after every row's value and the total have been written — so the list moves to its
    /// new order in one go, never through the half-sorted states in between.</summary>
    private void OnListChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ListNode.Rows)) Redraw(animate: true);
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SensorNode.Value)) Redraw(animate: true);
    }

    /// <summary>
    /// Colours every row by its own value. The judge comes from whoever paints, with the series' ramp and the limits
    /// already put together inside it — so the application at the top of a list can go amber while the rest do not,
    /// and this never has to learn what a ramp or a limit is. The total is judged by the same judge, as a row is.
    /// </summary>
    public void PaintRows(Func<float?, Brush> judge)
    {
        _judge = judge;

        foreach (var row in _byNode.Values) Paint(row);
        PaintTotal();
    }

    /// <summary>
    /// Puts every row where the source says it belongs now: the first ones on screen, the rest below, out of sight,
    /// each going there from where it is drawn. A node new to the source gets a row already in its place, and fades in
    /// if that place is on screen; a row whose node the source has dropped fades out where it stands. Without
    /// <paramref name="animate"/> all of that is simply done — a re-dress, a widget arriving or leaving the screen.
    /// </summary>
    private void Redraw(bool animate)
    {
        var rows = Source;
        var count = Math.Min(_itemCount, rows.Count);
        var pass = ++_pass;

        for (var slot = 0; slot < rows.Count; slot++)
        {
            var node = rows[slot];
            var arriving = !_byNode.TryGetValue(node, out var row);
            row ??= Take(node);

            // Shown before it is moved, so a row coming up from out of sight is on screen for the whole of its way.
            Present(row, slot < count ? Presence.Shown : Presence.Hidden, animate);
            MoveTo(row, slot, animate && !arriving);
            row.Pass = pass;

            // Only what is on screen is lettered; a row out of sight is lettered when it comes back.
            if (slot < count) Draw(row, rows, animate);
        }

        // What the source no longer has. A row already going is left to finish — unless nothing may move, and then it
        // goes at once.
        _gone.Clear();
        foreach (var row in _byNode.Values)
            if (row.Pass != pass && (row.Presence != Presence.Gone || !animate)) _gone.Add(row);

        foreach (var row in _gone) Present(row, Presence.Gone, animate);

        Widen(rows, count);

        if (_list is not null) _total.Text = _list.Total.Reading(ShowUnit, string.Empty);
        PaintTotal();
    }

    /// <summary>
    /// Fades a row to where it now stands. On screen it fades in; out of sight or dropped it fades out, and then
    /// collapses or goes back to the pool. A row already out of sight has nothing to watch fade, so it simply goes.
    /// </summary>
    private void Present(Row row, Presence presence, bool animate)
    {
        if (animate && row.Presence == presence) return;

        row.Presence = presence;
        var turn = ++row.Turn;

        if (presence == Presence.Shown)
        {
            row.Root.Visibility = Visibility.Visible;
            Motion.To(row.Root, OpacityProperty, 1, animate);
            return;
        }

        Motion.To(row.Root, OpacityProperty, 0, animate && row.Root.Visibility == Visibility.Visible, () =>
        {
            if (row.Turn != turn) return;

            if (presence == Presence.Gone) Release(row);
            else row.Root.Visibility = Visibility.Collapsed;
        });
    }

    private void MoveTo(Row row, int slot, bool animate)
    {
        var y = slot * _step;
        if (animate && Math.Abs(y - row.Y) < 0.01) return;

        // A row out of sight has nowhere to be seen sliding, and one never placed has nowhere to slide from.
        animate &= !double.IsNaN(row.Y) && row.Root.Visibility == Visibility.Visible;
        row.Y = y;
        Motion.To(row.Place, TranslateTransform.YProperty, y, animate);
    }

    private Row Take(SensorNode node)
    {
        var row = _pool.Count > 0 ? _pool.Pop() : Create();

        row.Node = node;
        row.Trend.Values = node.History;
        row.Y = double.NaN;
        row.Shown = double.NaN;

        _byNode[node] = row;
        return row;
    }

    /// <summary>
    /// Back to the pool, out of sight and holding nothing of the node it drew. The pool is held to the most a list
    /// keeps: with every row of a full list placed, on screen or below it, that many again is the most the source
    /// can drop at once — so however long the list runs, it never has more than twice that many rows.
    /// </summary>
    private void Release(Row row)
    {
        if (row.Node is { } node) _byNode.Remove(node);

        row.Node = null;
        row.Presence = Presence.Hidden;
        row.Trend.Values = null;
        row.Root.Visibility = Visibility.Collapsed;

        if (_pool.Count < ListNode.MaxRows)
        {
            _pool.Push(row);
            return;
        }

        _stage.Children.Remove(row.Root);
        _all.Remove(row);
    }

    private void Draw(Row row, IReadOnlyList<SensorNode> rows, bool animate)
    {
        if (row.Node is not { } node) return;

        row.Name.Text = node.Name ?? string.Empty;
        row.Reading.Text = node.Reading(ShowUnit, string.Empty);

        Paint(row);
        Bar(row, rows, animate);
    }

    /// <summary>
    /// One row in the state its own value has earned. The number, its trend and its bar are one reading, so they wear
    /// one colour. Until something paints, the bar and the trend take the text's colour and the number keeps it.
    /// </summary>
    private void Paint(Row row)
    {
        if (row.Node is not { } node) return;

        var brush = _judge?.Invoke(node.Value);
        if (brush is not null) row.Reading.Foreground = brush;

        row.Trend.Stroke = brush ?? Foreground;
        row.Bar.Fill = brush ?? Foreground;
    }

    private void PaintTotal()
    {
        if (_list is not null && _judge is not null) _total.Foreground = _judge(_list.Total.Value);
    }

    /// <summary>
    /// A row's bar, against the list's scale (<see cref="SensorScale.Ceiling(SensorNode, IReadOnlyList{SensorNode})"/>):
    /// the whole machine for a share of the CPU, the largest row for anything with no top of its own — and never
    /// shorter than <see cref="BarGeometry.Least"/> of its track, which the bar holds it to, so a row at nothing still
    /// shows the end of a bar rather than a bare track. It travels as every bar does, and a row that has only just
    /// appeared is drawn at its reading, never swept up from nothing.
    /// </summary>
    private void Bar(Row row, IReadOnlyList<SensorNode> rows, bool animate)
    {
        if (!_showBar || row.Node is not { } node) return;

        var share = Math.Clamp((node.Value ?? 0) / SensorScale.Ceiling(node, rows), 0, 1);
        if (animate && Math.Abs(share - row.Shown) < 1e-4) return;

        animate &= !double.IsNaN(row.Shown);
        row.Shown = share;
        row.Bar.MoveTo(share, animate);
    }

    /// <summary>
    /// Gives every number the same room, so every name is cut off at the same place — as one Grid's column did before
    /// the rows came apart to move on their own.
    ///
    /// The widest the format CAN write would be the answer, and there is none for megabytes or bytes per second, which
    /// have no top. So: the top of the scale where there is one — 100% — which makes a CPU list its final width from
    /// its first frame; and otherwise the widest number shown since the list was last dressed, only ever wider until
    /// then, so the names do not twitch as a figure gains and loses a digit.
    ///
    /// A minimum and not a width, as the bars reserve theirs: a number wider than the room takes a little more in its
    /// own row rather than losing its last digit.
    /// </summary>
    private void Widen(IReadOnlyList<SensorNode> rows, int count)
    {
        var widest = _readingWidth;

        if (count > 0 && SensorScale.Fixed(rows[0]) is { } top)
            widest = Math.Max(widest, Measured(SensorFormat.Reading((float)top, rows[0].Unit, ShowUnit)));

        for (var i = 0; i < count; i++)
            widest = Math.Max(widest, Measured(rows[i].Reading(ShowUnit, string.Empty)));

        if (widest <= _readingWidth) return;
        _readingWidth = widest;

        foreach (var row in _all) row.Reading.MinWidth = widest;
    }

    /// <summary>
    /// How wide a number is drawn, measured with every digit written as a nought. The number is drawn in tabular
    /// digits where its face has them, which a FormattedText cannot be asked for — and in a face that has them a nought
    /// is exactly as wide as any digit, while in one that has not it is among the widest. Kept by that shape, which a
    /// list writes in a handful of ways, so a read measures nothing new.
    /// </summary>
    private double Measured(string text)
    {
        if (text.Length == 0) return 0;

        var shape = string.Create(text.Length, text, (letters, from) =>
        {
            for (var i = 0; i < letters.Length; i++) letters[i] = char.IsAsciiDigit(from[i]) ? '0' : from[i];
        });

        if (_widths.TryGetValue(shape, out var known)) return known;

        var measured = new FormattedText(shape, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, _typeface,
            _rowSize, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);

        // A hair of slack, as the bars keep: a glyph measured to the pixel can still land on the wrong side of a
        // rounding.
        return _widths[shape] = Math.Ceiling(measured.WidthIncludingTrailingWhitespace) + 2;
    }

    private Row Create()
    {
        var place = new TranslateTransform();
        var root = new Grid
        {
            VerticalAlignment = VerticalAlignment.Top,
            RenderTransform = place,
            Opacity = 0,
            Visibility = Visibility.Collapsed,
        };

        // The line, whose height Dress sets, and the bar under it.
        root.RowDefinitions.Add(new RowDefinition());
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Dimmed and never painted: the name inherits the widget's value colour, which is what text nothing is
        // judging is drawn in. The number is the reading, so the number and what draws it wear the ramp.
        var name = new TextBlock
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // Its margin and its room are set with the face, in Dress and Widen.
        var reading = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Right,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Typography.SetNumeralAlignment(reading, FontNumeralAlignment.Tabular);
        Grid.SetColumn(reading, 2);

        // From nought to the highest of its points: a trend, not a magnitude. No area — it sits beside a number. It
        // follows its node's history by itself while it is on screen, so a reading needs no word from here.
        var trend = new LinePlot
        {
            Window = TrendPoints,
            RangeMin = 0,
            Opacity = TrendOpacity,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(trend, 1);

        // A share of its track, drawn — so a resize needs nothing from here.
        var bar = new BarFill { VerticalAlignment = VerticalAlignment.Top };
        Grid.SetRow(bar, 1);
        Grid.SetColumnSpan(bar, 3);

        root.Children.Add(name);
        root.Children.Add(reading);

        var row = new Row(root, place, name, reading, trend, bar);

        _stage.Children.Add(root);
        _all.Add(row);
        Dress(row);
        return row;
    }

    /// <summary>One row in the reading's face, at the widget's scale, drawing what the widget asks it to — and nothing
    /// it does not: a part turned off leaves the row, rather than staying in it unseen.</summary>
    private void Dress(Row row)
    {
        row.Name.FontSize = _rowSize;
        row.Reading.FontSize = _rowSize;
        row.Name.Wear(_valueFont, _valueWeight);
        row.Reading.Wear(_valueFont, _valueWeight);

        row.Root.RowDefinitions[0].Height = new GridLength(_line);

        // At the widget's scale, like the text either side of it: a gap that stayed eight pixels between names and
        // numbers twice the size would run them together.
        row.Reading.Margin = new Thickness((_showSparkline ? TrendGap : NameGap) * _figureScale, 0, 0, 0);
        row.Reading.MinWidth = _readingWidth;

        Include(row.Root, row.Trend, _showSparkline);
        row.Trend.Width = TrendWidth * _figureScale;
        row.Trend.Height = Math.Min(TrendHeight * _figureScale, _line);
        row.Trend.Look = new LineLook(TrendThickness * _figureScale, AreaFill.None, 0, 0, 0);
        row.Trend.Margin = new Thickness(NameGap * _figureScale, 0, 0, 0);

        Include(row.Root, row.Bar, _showBar);
        row.Bar.Look = RowLook();
        row.Bar.Margin = new Thickness(0, BarGap * _figureScale, 0, 0);
    }

    /// <summary>A row's bar: <see cref="BarShare"/> of the preset's — or the type picker's default — and never under a
    /// pixel thick, since the thickness comes from a preset, which a hand can edit to nothing. Over the dashboard's
    /// track when the look names none.</summary>
    private BarLook RowLook()
    {
        var look = SchemeTrack.Resolve(this, _barLook, Undressed).Scaled(BarShare);
        return look with { Thickness = Math.Max(1, look.Thickness) };
    }

    void IFollowsSchemeTrack.SchemeTrackChanged()
    {
        foreach (var row in _all) row.Bar.Look = RowLook();
    }

    private static void Include(Panel panel, UIElement part, bool wanted)
    {
        var there = panel.Children.Contains(part);

        if (wanted && !there) panel.Children.Add(part);
        else if (!wanted && there) panel.Children.Remove(part);
    }

    /// <summary>
    /// Lays the rows out again — as many as the widget asks for, each in the reading's face, with the parts it asks
    /// for — and draws them. In place: rows are dressed again and the watch is kept, because all of this is Style, and
    /// a Style change re-dresses a piece without rebuilding it.
    /// </summary>
    private void Apply()
    {
        // The rows at the reading's size in the column of text, and at their share of it in the design's card, where
        // the total is the reading. A line of them exactly as tall as a TextBlock draws it, so that without a bar the
        // rows stack line on line, as the rows of the Grid this replaced did.
        _rowSize = _showBar ? _valueSize * RowShare : _valueSize;

        var family = string.IsNullOrWhiteSpace(_valueFont) ? FontFamily : new FontFamily(_valueFont);
        _line = family.LineSpacing * _rowSize;
        _step = _line + (_showBar ? (BarGap + RowGap) * _figureScale + RowLook().Thickness : 0);
        _typeface = new Typeface(family, FontStyles.Normal, _valueWeight.ToFontWeight(), FontStretches.Normal);

        // Measured again, in the new face.
        _widths.Clear();
        _readingWidth = 0;

        foreach (var row in _all) Dress(row);

        // As tall as the rows asked for, filled or not, so the widget does not jump when the first rows arrive, a poll
        // or two after it appears. The last row has no gap after it.
        _stage.Height = Math.Max(0, _itemCount * _step - (_showBar ? RowGap * _figureScale : 0));

        _total.FontSize = _valueSize;
        _total.Wear(_valueFont, _valueWeight);
        _total.Visibility = _showTotal && _list is not null ? Visibility.Visible : Visibility.Collapsed;

        // Every row put where it belongs at the new sizes, not travelled there: the sizes moved, not the rows.
        foreach (var row in _all)
        {
            row.Y = double.NaN;
            row.Shown = double.NaN;
        }

        Redraw(animate: false);
    }

    // ── The figure, at the kind's default size ────────────────────────────
    //
    // The design's own measurements (docs/widget-processos, drawn at 13-pixel rows), each at the widget's scale.

    /// <summary>The rows against the total, in the card: the design's 13 against 20. Ember's reading of 18 gives rows
    /// of 11.7 beside a title of 11 — the design's 13 beside 12.</summary>
    private const double RowShare = 13.0 / 20;

    /// <summary>Between the end of a name and what follows it.</summary>
    private const double NameGap = 8;

    /// <summary>Between the trend and its number.</summary>
    private const double TrendGap = 10;

    /// <summary>The trend's box, and its stroke. Its height is held to the line, so a small face never has a trend
    /// taller than its text.</summary>
    private const double TrendWidth = 44;
    private const double TrendHeight = 14;
    private const double TrendThickness = 1.5;

    /// <summary>How many readings the trend spans — twenty, of the sixty every node keeps.</summary>
    private const int TrendPoints = 20;

    /// <summary>A trend is the number's company, not its rival: drawn in its colour, at a little over half of it.
    /// </summary>
    private const double TrendOpacity = 0.6;

    /// <summary>From the text down to the bar; and from one row's bar to the next row's text — the design's 38 between
    /// rows, less its line of 13-pixel text (17.3), those 6 and the bar's 4.</summary>
    private const double BarGap = 6;
    private const double RowGap = 10.7;

    /// <summary>How thick a row's bar is against the preset's bar section — see <see cref="IBarStyle.UnderTextShare"/>.
    /// </summary>
    private const double BarShare = IBarStyle.UnderTextShare;

    /// <summary>The look of a list nobody has dressed — the type picker: the preset bar the design was drawn at.
    /// </summary>
    private static readonly BarLook Undressed = new(6, 3, null);

    // ── Style ────────────────────────────────────────────────────────

    private int _itemCount = 5;
    private double _figureScale = 1;
    private bool _showUnit = true;

    // The design's reading, which the type picker — the only place a piece is drawn undressed — shows its rows at a
    // share of: 13, the design's own rows.
    private double _valueSize = 20;
    private string? _valueFont;
    private TextWeight _valueWeight = TextWeight.Regular;
    private BarLook? _barLook;

    // On here, though off in the widget's file: on the desktop every piece is told these from its file, so the piece's
    // own say only ever shows in the type picker — which shows what a list made today looks like.
    private bool _showBar = true;
    private bool _showSparkline = true;
    private bool _showTotal = true;

    /// <summary>How many rows are shown — between one and <see cref="ListNode.MaxRows"/>, the most a list keeps,
    /// because the number comes from widgets.json and a file can be edited by hand. Rows the list has not got are
    /// left blank.</summary>
    public int ItemCount
    {
        get => _itemCount;
        set { _itemCount = Math.Clamp(value, 1, ListNode.MaxRows); Apply(); }
    }

    public bool ShowBar
    {
        get => _showBar;
        set { _showBar = value; Apply(); }
    }

    public bool ShowSparkline
    {
        get => _showSparkline;
        set { _showSparkline = value; Apply(); }
    }

    public bool ShowTotal
    {
        get => _showTotal;
        set { _showTotal = value; Apply(); }
    }

    public bool ShowUnit
    {
        get => _showUnit;
        set { _showUnit = value; Apply(); }
    }

    /// <summary>For the figure's own sizes: the gaps, the trend's box and the space between rows.</summary>
    public double FigureScale
    {
        get => _figureScale;
        set { _figureScale = value; Apply(); }
    }

    /// <summary>The reading's size: the rows' in the column of text, the total's in the card.</summary>
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

    /// <summary>The preset's bar, at the widget's scale. A row draws <see cref="BarShare"/> of it.</summary>
    public BarLook? BarLook
    {
        get => _barLook;
        set { _barLook = value; Apply(); }
    }
}
