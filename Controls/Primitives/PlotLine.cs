using System.Windows.Media;

namespace Aquila.Controls.Primitives;

/// <summary>
/// One line of a <see cref="LinePlot"/>: its readings, oldest first, read where they are; its colour; the colour of
/// each stretch of the scale when it is coloured by its reading; and how its readings are written where the plot
/// writes them.
///
/// A value, so a wrapper can make the list again whenever anything about a line changes — a colour, every reading —
/// and the plot can tell a line that is the same readings in new clothes from new readings, which is a new scale.
/// </summary>
/// <param name="Values">The readings, oldest first.</param>
/// <param name="Stroke">The line's colour: its line, its area, its dots and its end. Null draws nothing of it.</param>
/// <param name="Bands">A colour for each stretch of the scale, each from its value up to the next one's, the first from
/// below anything — the line painted in the colour of the height it is drawn at. Null paints it in
/// <paramref name="Stroke"/> throughout.</param>
/// <param name="Format">How a reading of this line is written, where the plot writes one. Null writes none.</param>
public sealed record PlotLine(IReadOnlyList<double> Values, Brush? Stroke,
    IReadOnlyList<(double From, Color Color)>? Bands = null, Func<double, string>? Format = null);
