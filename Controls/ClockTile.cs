using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// The time or the date, on a timer of its own.
///
/// Not driven by the hardware poll, and that is the whole reason it is a piece rather than a Text with a
/// clever caption: every other widget is redrawn when the sensors are read, and #15 proposes throttling that
/// while the app is minimised — which is exactly when desktop widgets are being looked at. A clock hanging
/// off the poll would stop.
///
/// The timer runs only while the piece is on screen, so a clock the user removed stops ticking with it.
/// </summary>
public sealed class ClockTile : TextBlock, IClockStyle
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private ClockFormat _format = ClockFormat.Time;

    public ClockTile()
    {
        VerticalAlignment = VerticalAlignment.Center;

        _timer.Tick += (_, _) => Show();
        Loaded += (_, _) => { Show(); _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();

        Show();
    }

    public double ValueSize
    {
        get => FontSize;
        set => FontSize = value;
    }


    private string? _valueFont;
    private TextWeight _valueWeight = TextWeight.Regular;

    public string? ValueFont
    {
        get => _valueFont;
        set { _valueFont = value; this.Wear(_valueFont, _valueWeight); }
    }

    public TextWeight ValueWeight
    {
        get => _valueWeight;
        set { _valueWeight = value; this.Wear(_valueFont, _valueWeight); }
    }

    public TextAlign Align
    {
        get => TextAlignment.ToAlign();
        set => TextAlignment = value.ToTextAlignment();
    }

    public ClockFormat Format
    {
        get => _format;
        set { _format = value; Show(); }
    }

    /// <summary>
    /// Standard specifiers, never a hand-written pattern: the result then follows the machine's own
    /// locale — 24-hour or 12-hour, day-month or month-day, in the user's language — and there is nothing
    /// for anyone to get wrong.
    /// </summary>
    private void Show() => Text = DateTime.Now.ToString(_format switch
    {
        ClockFormat.TimeWithSeconds => "T",
        ClockFormat.Date => "d",
        ClockFormat.DateLong => "D",
        ClockFormat.DateAndTime => "g",
        _ => "t",
    });
}
