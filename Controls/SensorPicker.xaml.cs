using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Aquila.ViewModels.Windows;

namespace Aquila.Controls;

/// <summary>
/// The sensors to choose a reading from, grouped by the part they belong to, searched by name and part. Given its
/// options each time it opens, minus the ones to leave out — a widget's own readings are not offered to it again —
/// and it says which was chosen, or that nothing was. What a choice means is the caller's: the editor adds it, the
/// edit bar's Add widget will make a widget of it.
/// </summary>
public partial class SensorPicker : UserControl
{
    private readonly ObservableCollection<SensorOption> _shown = [];
    private IReadOnlyList<SensorOption> _options = [];
    private HashSet<string> _left = [];

    /// <summary>A sensor was chosen: Enter on the highlighted row, or a click on one.</summary>
    public event Action<SensorOption>? Chosen;

    /// <summary>Esc: nothing was chosen.</summary>
    public event Action? Cancelled;

    public SensorPicker()
    {
        InitializeComponent();

        var view = new CollectionViewSource { Source = _shown };
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SensorOption.Component)));
        List.ItemsSource = view.View;
    }

    /// <summary>Fills it afresh as it opens, from a blank search, with the keys in the search box — so typing
    /// finds a sensor at once, and Enter takes the first there is.</summary>
    public void Open(IReadOnlyList<SensorOption> options, IEnumerable<string> leaveOut)
    {
        _options = options;
        _left = [.. leaveOut];
        Search.Text = string.Empty;
        Filter();
        Dispatcher.BeginInvoke(() => Search.Focus(), DispatcherPriority.Input);
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => Filter();

    /// <summary>By name and by part, either case — "gpu" finds a graphics card's every reading — and the first
    /// row left highlighted, so Enter has something to take.</summary>
    private void Filter()
    {
        var text = Search.Text.Trim();

        _shown.Clear();
        foreach (var option in _options)
            if (!_left.Contains(option.Identifier)
                && (text.Length == 0 || option.Display.Contains(text, StringComparison.CurrentCultureIgnoreCase)))
                _shown.Add(option);

        Nothing.Visibility = _shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        List.SelectedIndex = _shown.Count > 0 ? 0 : -1;
        if (List.SelectedItem is { } first) List.ScrollIntoView(first);
    }

    /// <summary>The same keys wherever the focus is, the search box or the list: a highlight to move, a row to take,
    /// a way out. The arrows are taken here so the search box keeps the keys while they move through the list.</summary>
    private void OnKey(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
            case Key.Up:
                if (List.Items.Count == 0) return;
                var step = e.Key == Key.Down ? 1 : -1;
                List.SelectedIndex = Math.Clamp(List.SelectedIndex + step, 0, List.Items.Count - 1);
                List.ScrollIntoView(List.SelectedItem);
                break;
            case Key.Enter:
                if (List.SelectedItem is not SensorOption option) return;
                Chosen?.Invoke(option);
                break;
            case Key.Escape:
                Cancelled?.Invoke();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void OnItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem { DataContext: SensorOption option }) Chosen?.Invoke(option);
    }
}
