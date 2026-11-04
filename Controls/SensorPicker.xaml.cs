using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Aquila.ViewModels.Windows;

namespace Aquila.Controls;

/// <summary>One row of the picker: the reading, the words it is listed under, and what the caller writes beside it.
/// </summary>
public sealed record PickerRow(SensorOption Option, string Label, string? Aside);

/// <summary>
/// The readings to choose from, grouped by the part they belong to, searched by name and part. Given its options
/// each time it opens, minus the ones to leave out — a widget's own readings are not offered to it again — and it
/// says which was chosen, or that nothing was. What a choice means is the caller's: the editor adds it to a widget,
/// the edit bar's Add widget makes a widget of it.
/// </summary>
public partial class SensorPicker : UserControl
{
    public static readonly DependencyProperty ExtraProperty = DependencyProperty.Register(
        nameof(Extra), typeof(object), typeof(SensorPicker));

    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
        nameof(Footer), typeof(string), typeof(SensorPicker),
        new PropertyMetadata("↑ ↓ to choose · Enter adds it · Esc closes"));

    /// <summary>Shown between the search and the list — the edit bar's kinds that read nothing.</summary>
    public object? Extra
    {
        get => GetValue(ExtraProperty);
        set => SetValue(ExtraProperty, value);
    }

    /// <summary>The line at the foot: the keys, or what the caller's choice will do.</summary>
    public string Footer
    {
        get => (string)GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    private readonly ObservableCollection<PickerRow> _shown = [];
    private IReadOnlyList<PickerRow> _rows = [];

    /// <summary>A reading was chosen: Enter on the highlighted row, or a click on one.</summary>
    public event Action<SensorOption>? Chosen;

    /// <summary>Esc: nothing was chosen.</summary>
    public event Action? Cancelled;

    public SensorPicker()
    {
        InitializeComponent();

        var view = new CollectionViewSource { Source = _shown };
        view.GroupDescriptions.Add(new PropertyGroupDescription($"{nameof(PickerRow.Option)}.{nameof(SensorOption.Component)}"));
        List.ItemsSource = view.View;
    }

    /// <summary>
    /// Fills it afresh as it opens, from a blank search, with the keys in the search box — so typing finds a reading
    /// at once, and Enter takes the first there is. <paramref name="label"/> names a row where its own name will not
    /// do ("Top applications by CPU" for the list called CPU); <paramref name="aside"/> writes beside it.
    /// </summary>
    public void Open(IEnumerable<SensorOption> options, IEnumerable<string> leaveOut,
        Func<SensorOption, string?>? aside = null, Func<SensorOption, string>? label = null)
    {
        var left = leaveOut.ToHashSet();
        _rows = [.. options.Where(o => !left.Contains(o.Identifier))
            .Select(o => new PickerRow(o, label?.Invoke(o) ?? o.Name, aside?.Invoke(o)))];
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
        foreach (var row in _rows)
            if (text.Length == 0
                || row.Label.Contains(text, StringComparison.CurrentCultureIgnoreCase)
                || row.Option.Component.Contains(text, StringComparison.CurrentCultureIgnoreCase))
                _shown.Add(row);

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
                if (List.SelectedItem is not PickerRow row) return;
                Chosen?.Invoke(row.Option);
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
        if (sender is ListBoxItem { DataContext: PickerRow row }) Chosen?.Invoke(row.Option);
    }
}
