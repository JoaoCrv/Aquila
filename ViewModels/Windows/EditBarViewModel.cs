using System.Collections.ObjectModel;
using Aquila.Models;
using Aquila.Services;
using Aquila.Services.EditMode;
using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Controls;

namespace Aquila.ViewModels.Windows;

/// <summary>One widget in the edit bar's list. <paramref name="Aside"/> is its kind, or how many widgets cover it
/// when any do — the one thing about a widget the desktop cannot show.</summary>
public sealed record WidgetRow(DesktopWidgetDefinition Definition, string Title, string Aside, SymbolRegular Icon,
    string? Problem, string Highlight)
{
    public bool HasProblem => Problem is not null;
}

/// <summary>A heading of the edit bar's list and its widgets: the ones that need attention, then one per screen.
/// </summary>
public sealed record WidgetGroup(string Header, int Count, bool First, IReadOnlyList<WidgetRow> Rows);

/// <summary>
/// The edit bar: what is true of the whole session rather than of one widget — the widgets there are, the state
/// they are previewed in, whether anything would be lost — and the session's ways in and out.
///
/// It works the service directly rather than through events: the bar exists only inside a session the service
/// owns, and every action here is one of its own.
/// </summary>
public sealed partial class EditBarViewModel(DesktopWidgetService widgets, SettingsService settings) : ObservableObject
{
    /// <summary>The first-time tip under the bar: the three gestures nothing on screen announces. Until it is closed
    /// or a widget is first selected, and never again after that.</summary>
    [ObservableProperty] private bool _showsTip = !settings.Current.EditTipSeen;

    public void TipSeen()
    {
        if (!ShowsTip) return;

        ShowsTip = false;
        settings.Current.EditTipSeen = true;
        settings.Save();
    }

    /// <summary>The list as it stands, filtered by <see cref="Search"/>; rebuilt each time the menu opens, since
    /// widgets move, change kind and get covered while it is shut.</summary>
    public ObservableCollection<WidgetGroup> Groups { get; } = [];

    [ObservableProperty] private string _search = string.Empty;
    partial void OnSearchChanged(string value) => FillGroups();

    [ObservableProperty] private int _widgetCount;
    [ObservableProperty] private bool _needsAttention;
    [ObservableProperty] private string _widgetsTip = string.Empty;

    /// <summary>Whether discarding would lose anything. Before undo there is no count to give, only the fact.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _hasChanges;

    public string StatusText => HasChanges ? "Unsaved changes" : "No changes yet";

    /// <summary>The state every widget is drawn in, or null for what the sensors read — Live. The bar's, because
    /// it paints the whole desktop, not the widget the panel is about.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewName), nameof(IsPreviewing))]
    private string? _previewRole;

    partial void OnPreviewRoleChanged(string? value) => DesktopWidgetService.PreviewRole(value);

    public string PreviewName => PreviewRole ?? "Live";
    public bool IsPreviewing => PreviewRole is not null;

    /// <summary>Asked again after every change the session makes; cheap enough, and coalesced by the caller.
    /// </summary>
    public void Refresh()
    {
        var places = widgets.Places();
        var attention = places.Count(p => p.Summary.HasProblem);

        WidgetCount = places.Count;
        NeedsAttention = attention > 0;
        WidgetsTip = attention > 0
            ? $"{Plural(places.Count, "widget")}, {attention} {(attention == 1 ? "needs" : "need")} attention"
            : Plural(places.Count, "widget");
        HasChanges = widgets.HasUnsavedChanges;
    }

    /// <summary>
    /// Fills the list: first what needs attention, then each screen's widgets from the front to the back — the
    /// order the canvas draws them, which makes the list the way to reach a widget hidden behind another.
    /// A widget that needs attention is listed there alone, so the screens count what can be seen.
    /// </summary>
    public void FillGroups()
    {
        Groups.Clear();
        var places = widgets.Places();
        var search = Search.Trim();

        var attention = places.Where(p => p.Summary.HasProblem).ToList();
        if (attention.Count > 0)
            Add("Needs attention", [.. attention.Select(p => Row(p, p.Summary.Kind, 0))]);

        foreach (var screen in places.Where(p => !p.Summary.HasProblem)
                     .GroupBy(p => p.Screen ?? "Not on a screen")
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var onIt = screen.ToList();
            var boxes = onIt.Select(p => p.Box).ToList();
            var layers = onIt.Select(p => p.Summary.Definition.ZIndex).ToList();

            Add(screen.Key, [.. Layers.FrontToBack(layers).Select(i =>
            {
                var behind = Layers.Behind(boxes, layers, i);
                return Row(onIt[i], behind > 0 ? $"Behind {Plural(behind, "widget")}" : onIt[i].Summary.Kind, behind);
            })]);
        }

        void Add(string header, List<WidgetRow> rows)
        {
            var shown = rows.Where(r => search.Length == 0
                || r.Title.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                || WidgetCatalog.For(r.Definition.Kind).Name.Contains(search, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
            if (shown.Count > 0) Groups.Add(new WidgetGroup(header, shown.Count, Groups.Count == 0, shown));
        }
    }

    private static WidgetRow Row(WidgetPlace place, string aside, int behind)
    {
        var summary = place.Summary;
        var highlight = behind > 0 ? $"{summary.Title} · behind {Plural(behind, "widget")}" : summary.Title;
        var icon = summary.HasProblem ? SymbolRegular.Warning24 : WidgetCatalog.For(summary.Definition.Kind).Icon;
        return new WidgetRow(summary.Definition, summary.Title, aside, icon, summary.Problem, highlight);
    }

    private static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";

    public void Select(WidgetRow row) => widgets.Select(row.Definition);

    public void Highlight(WidgetRow? row) => widgets.Highlight(row?.Definition, row?.Highlight ?? string.Empty);

    public void Add() => widgets.AddWidget(presetSensorIdentifier: null);

    public void Finish(bool save) => widgets.Finish(save);
}
