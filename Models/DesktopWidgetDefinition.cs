namespace Aquila.Models;

/// <summary>
/// The widget shapes the desktop can show. Names are persisted in widgets.json by name, not by number,
/// so entries may be added freely but not renamed without breaking existing layouts.
/// </summary>
public enum DesktopWidgetKind
{
    RadialGauge,
    MiniSparkline,
    SensorMeter,
    StatBox,
    SparklineChart,
}

/// <summary>
/// One persisted desktop widget (#24): which piece, which sensor, and where it sits. Stored as data
/// rather than code so the layout survives restarts and, later, so the pin-from-Explorer flow can create
/// widgets without touching the widget-building code.
///
/// The sensor is stored as its <see cref="SensorNode.Identifier"/> — resolved back to a live node at
/// startup via <see cref="SensorCatalog.FindByIdentifier"/>. Positions are DIPs within the target
/// screen's canvas.
/// </summary>
public class DesktopWidgetDefinition
{
    public DesktopWidgetKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// What this widget draws, in order. One entry for a dial or a number; a chart may have more.
    ///
    /// A list rather than a first sensor with optional extras beside it. That was tried and the editor it
    /// produced was confusing: a "sensor" and a "second sensor" are the same thing wearing different
    /// names, and nothing on screen explained why one of them could be cleared and the other could not.
    /// A list is also how people describe a chart — these readings, in this order.
    /// </summary>
    public List<WidgetSeries> Series { get; set; } = [];

    /// <summary>Legacy: the single sensor written before <see cref="Series"/> existed. Read once to
    /// migrate an older widgets.json, then left empty.</summary>
    public string SensorIdentifier { get; set; } = string.Empty;

    /// <summary>Legacy, alongside <see cref="SensorIdentifier"/>.</summary>
    public string AccentKey { get; set; } = string.Empty;

    /// <summary>
    /// Folds a pre-list layout into <see cref="Series"/>. Keyed on Series being empty rather than on the
    /// old fields having values, so a widget the user has since emptied is not resurrected on every load.
    /// </summary>
    public void MigrateSeries()
    {
        if (Series.Count > 0 || string.IsNullOrEmpty(SensorIdentifier)) return;

        Series.Add(new WidgetSeries
        {
            SensorIdentifier = SensorIdentifier,
            AccentKey = AccentKey,
        });

        SensorIdentifier = string.Empty;
        AccentKey = string.Empty;
    }

    /// <summary>Which physical monitor the widget lives on — a stable identity derived from the monitor's
    /// EDID manufacturer/product code and connection, not its index or DeviceName (both shift when
    /// monitors are unplugged or rearranged). If the screen is gone, the widget falls back to the primary
    /// one rather than disappearing.</summary>
    public string ScreenKey { get; set; } = string.Empty;

    /// <summary>Legacy: the screen index written before <see cref="ScreenKey"/> existed. Only read to
    /// migrate an older widgets.json, then overwritten with the key on the next save.</summary>
    public int ScreenIndex { get; set; }

    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    /// <summary>Which widget draws on top where two overlap — higher is nearer the front. Without it the
    /// order is whatever the layout file happens to list, which the user has no way to change.</summary>
    public int ZIndex { get; set; }

    // --- Appearance ---
    // Colours are stored as "#RRGGBB" and combined with the separate opacity, rather than as "#AARRGGBB":
    // the user sets colour and transparency independently, and keeping them apart means changing one never
    // silently resets the other. A widget sits on an unknown wallpaper, so a backing panel is the default.

    public string BackgroundColor { get; set; } = "#000000";

    /// <summary>0 = invisible, 1 = solid.</summary>
    public double BackgroundOpacity { get; set; } = 0.6;

    public double CornerRadius { get; set; } = 8;

    public string BorderColor { get; set; } = "#FFFFFF";
    public double BorderOpacity { get; set; } = 0.25;

    /// <summary>0 hides the border entirely.</summary>
    public double BorderThickness { get; set; } = 0;

    public DesktopWidgetDefinition Clone()
    {
        var copy = (DesktopWidgetDefinition)MemberwiseClone();
        copy.Series = Series.Select(s => s.Clone()).ToList();
        return copy;
    }
}

/// <summary>One reading inside a widget: which sensor, and the colour role it is drawn in.</summary>
public class WidgetSeries
{
    public string SensorIdentifier { get; set; } = string.Empty;

    /// <summary>A colour-profile role, e.g. "Aquila.Scheme.Series1" — held as a key rather than a colour
    /// so the series follows theme and profile changes.</summary>
    public string AccentKey { get; set; } = string.Empty;

    public WidgetSeries Clone() => (WidgetSeries)MemberwiseClone();
}
