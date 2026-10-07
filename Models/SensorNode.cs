using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Aquila.Models;

public class SensorNode : INotifyPropertyChanged
{
    private float? _value;
    private float? _min;
    private float? _max;
    private string? _unit;
    private string? _name;
    private string? _identifier;

    public float? Value
    {
        get => _value;
        set
        {
            // Record on every write (driver writes once per poll tick) so the sparkline's time axis
            // stays regular even for unchanged values. Only raise INPC when the value actually
            // changes, to avoid refreshing the many direct XAML bindings needlessly.
            bool changed = _value != value;
            _value = value;
            Record();
            if (changed) OnPropertyChanged();
        }
    }

    public float? Min
    {
        get => _min;
        set { if (_min != value) { _min = value; OnPropertyChanged(); } }
    }

    public float? Max
    {
        get => _max;
        set { if (_max != value) { _max = value; OnPropertyChanged(); } }
    }

    public string? Unit
    {
        get => _unit;
        set { if (_unit != value) { _unit = value; OnPropertyChanged(); } }
    }

    public string? Name
    {
        get => _name;
        set { if (_name != value) { _name = value; OnPropertyChanged(); } }
    }

    public string? Identifier
    {
        get => _identifier;
        set { if (_identifier != value) { _identifier = value; OnPropertyChanged(); } }
    }

    public ObservableCollection<double> History { get; } = [];

    /// <summary>
    /// How many readings to keep, one per poll tick — so the time they cover depends on the poll interval.
    ///
    /// Sixty by default and raised only on request, because EVERY sensor keeps a history whether anything
    /// draws it or not: a machine reporting three hundred sensors would pay for a ten-minute window on all
    /// three hundred to give it to the two on the desktop. Whoever wants a longer trend asks for it on the
    /// sensor they are actually showing.
    ///
    /// Lowering it trims immediately rather than waiting for the buffer to drain, so the chart matches the
    /// window the moment it is changed.
    /// </summary>
    public int HistoryDepth
    {
        get => _historyDepth;
        set
        {
            _historyDepth = Math.Max(2, value);
            while (History.Count > _historyDepth) History.RemoveAt(0);
        }
    }

    private int _historyDepth = 60;

    private void Record()
    {
        if (History.Count >= _historyDepth) History.RemoveAt(0);
        History.Add(Value ?? 0);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Says the reading again without it having changed, for whatever JUDGES it: a new preset or new
    /// limits change the colour a value has earned, and a binding only converts when it hears the value.</summary>
    public void Announce() => OnPropertyChanged(nameof(Value));

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
