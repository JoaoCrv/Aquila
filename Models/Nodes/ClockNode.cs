using System.ComponentModel;

namespace Aquila.Models.Nodes;

/// <summary>
/// The time, as one reading every clock shares.
///
/// A node like the hardware's, so a clock is drawn the way everything else is — it watches a value and redraws
/// when it changes — but written by <see cref="Services.ClockService"/> on a second of its own, never by the
/// hardware poll: at a poll of two or five seconds a clock on it would show its seconds jumping.
///
/// Raised once per whole second and never twice for the same one, so a clock can neither skip nor repeat a
/// second unless the machine itself stalled for one.
/// </summary>
public sealed class ClockNode : INotifyPropertyChanged
{
    public DateTime Now
    {
        get => _now;
        set
        {
            if (_now == value) return;
            _now = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Now)));
        }
    }

    private DateTime _now = DateTime.Now;

    public event PropertyChangedEventHandler? PropertyChanged;
}
