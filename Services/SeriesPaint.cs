using System.Windows;
using System.Windows.Media;

namespace Aquila.Services;

/// <summary>
/// How many readings a kind draws, and where the colour of the n-th one goes.
///
/// It used to be a plain <c>IReadOnlyList&lt;DependencyProperty&gt;</c> — one property per series, whose
/// length WAS the limit. That is exactly right for every kind that draws a fixed, small number of readings,
/// and it stays the common case: <see cref="Properties"/> produces the identical behaviour and every kind
/// that had a list still reads as a list.
///
/// It could not express a kind whose count is the hardware's to decide. An equaliser draws one bar per CPU
/// core, and this machine has sixteen — nobody is declaring sixteen dependency properties, and the number
/// is not known when the catalogue is written. <see cref="Many"/> takes a ceiling and a way to reach the
/// n-th reading INSIDE the piece, so a control holding a collection can be painted per element.
///
/// The count and the painter are one value on purpose. Held apart they are one fact in two places, and the
/// editor's ceiling would have been free to disagree with what the builder could actually colour.
/// </summary>
/// <param name="Max">The most readings this kind will draw. The editor stops offering more at this.</param>
/// <param name="For">Given the built piece and a series index, the way to apply that series' colour.</param>
public sealed record SeriesPaint(int Max, Func<FrameworkElement, int, Action<Brush>> For)
{
    /// <summary>
    /// For a kind whose one reading is a list: given the built piece, the way to hand it a judge — a value's
    /// brush — that it applies to every row by that row's own value. Null for every other kind.
    ///
    /// A judge and not a brush, because a list's rows are not coloured alike: each is in whatever state its own
    /// figure has earned, under the series' one ramp, and only the piece knows which rows it is showing.
    /// </summary>
    public Func<FrameworkElement, Action<Func<float?, Brush>>>? Rows { get; init; }

    /// <summary>One dependency property per series, in order — the ordinary case.</summary>
    public static SeriesPaint Properties(params DependencyProperty[] properties) =>
        new(properties.Length, (piece, i) => brush => piece.SetValue(properties[i], brush));

    /// <summary>Draws nothing that takes a colour. A backdrop reads no sensor at all; a clock reads one
    /// but has no state to be in.</summary>
    public static SeriesPaint None { get; } = new(0, (_, _) => _ => { });

    /// <summary>A number of readings the hardware decides, coloured through the piece itself.</summary>
    public static SeriesPaint Many(int max, Action<FrameworkElement, int, Brush> paint) =>
        new(max, (piece, i) => brush => paint(piece, i, brush));

    /// <summary>
    /// One reading that is a list, each row judged on its own value — see <see cref="Rows"/>.
    ///
    /// <see cref="For"/> still answers, painting every row with the one brush it is handed. That is all the type
    /// picker's preview asks of any kind, so it draws a list without having to know that lists exist.
    /// </summary>
    public static SeriesPaint PerRow(Action<FrameworkElement, Func<float?, Brush>> paint) =>
        new(1, (piece, _) => brush => paint(piece, _ => brush)) { Rows = piece => judge => paint(piece, judge) };
}
