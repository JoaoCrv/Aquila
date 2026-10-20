using Aquila.Models;

namespace Aquila.Controls;

/// <summary>What any piece made of words has: how big, and where it sits.</summary>
public interface ITextStyle : IValueStyle
{
    TextAlign Align { get; set; }
}

/// <summary>A piece that shows words the user wrote, and optionally a reading after them.</summary>
public interface ICaptionStyle : ITextStyle, IUnitStyle
{
    string Caption { get; set; }
}

/// <summary>A piece that shows the time or the date.</summary>
public interface IClockStyle : ITextStyle
{
    ClockFormat Format { get; set; }

    /// <summary>The rings round the time. With none, <see cref="Format"/> says what is written; with any, the
    /// time is written in the middle of them, and the two below say what goes with it.</summary>
    ClockRings Rings { get; set; }

    bool ShowSeconds { get; set; }
    bool ShowDate { get; set; }

    /// <summary>
    /// The ramp whose stops the rings are drawn in — the preset's primary.
    ///
    /// A ramp and not three colours, because a ramp is the only thing in a preset that carries more than one
    /// colour for the data, and a fourth kind of colour in the format just for clocks would be the bypass the
    /// format exists to prevent. Nothing is judged: a clock has no state to be in, so each ring keeps one stop
    /// for good, and which ring takes which stop is the clock's own business.
    /// </summary>
    Ramp Ramp { get; set; }
}
