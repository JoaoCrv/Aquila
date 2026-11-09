namespace Aquila.Controls;

/// <summary>
/// A piece drawn with bars, and the preset's <c>bar</c> section it takes them from: how thick, how round at the
/// ends, the track under them, and whether they are drawn in blocks. Beside <see cref="IChartStyle"/> and
/// <see cref="IGaugeStyle"/>, the third of the structural style interfaces — the Bars, a list's rows and a Number's
/// level take it.
///
/// It stood under a meter's interface once, which also carried where the meter wrote its number; that went to the
/// widget with the Bars (docs/widget-barras), and the meter's interface with it. Its own since the list's rows, which
/// carry a bar each and nothing else: the split <see cref="IArcStyle"/> made for the clock's rings, for the same
/// reason — a piece takes exactly what it draws, and two pieces drawing the same bar cannot come to disagree about what
/// the preset's thickness means.
/// </summary>
public interface IBarStyle
{
    /// <summary>
    /// How thick a bar drawn under a line of text is against the preset's bar section — a list's rows, a Number's
    /// level. That section was written for a meter, ONE bar that is the widget's whole reading; a bar under a line of
    /// text is that line's company. Two thirds of Ember's 6 and 3 is the design's 4 and 2. A share and not a number, so
    /// a preset with a heavier bar still gets heavier ones, in proportion — the clock's RingShare, for the same reason.
    /// One home, so the list's bars and the Number's can never come apart.
    /// </summary>
    const double UnderTextShare = 2.0 / 3;

    /// <summary>
    /// The preset's bar section at the widget's size — thickness and corners — with its track, as one value, so a
    /// change of preset is one assignment.
    ///
    /// The track is the widget's own preset's. Unset (null, or a look naming no track), a bar takes the DASHBOARD'S
    /// (<see cref="SchemeTrack"/>), which is right on a card and was wrong on the desktop: a widget wearing another
    /// preset drew its fill in its own colours over the dashboard's track, while a dial beside it, which has always
    /// taken its track from its own preset, did not. Null until the piece is dressed: it then draws its own default.
    /// </summary>
    Aquila.Controls.Primitives.BarLook? BarLook { get; set; }
}
