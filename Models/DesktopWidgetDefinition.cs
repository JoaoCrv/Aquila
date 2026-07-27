namespace Aquila.Models;

public enum DesktopWidgetKind
{
    RadialGauge,
    MiniSparkline,
    SensorMeter,
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
    public string SensorIdentifier { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    /// <summary>Resource key of the accent brush (e.g. "Aquila.Cpu"), so widgets follow theme changes.</summary>
    public string AccentKey { get; set; } = string.Empty;

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
}
