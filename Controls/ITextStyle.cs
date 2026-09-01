using Aquila.Models;

namespace Aquila.Controls;

/// <summary>What any piece made of words has: how big, and where it sits.</summary>
public interface ITextStyle : IValueStyle
{
    TextAlign Align { get; set; }
}

/// <summary>A piece that shows words the user wrote, and optionally a reading after them.</summary>
public interface ICaptionStyle : ITextStyle
{
    string Caption { get; set; }
    bool ShowUnit { get; set; }
}

/// <summary>A piece that shows the time or the date.</summary>
public interface IClockStyle : ITextStyle
{
    ClockFormat Format { get; set; }
}
