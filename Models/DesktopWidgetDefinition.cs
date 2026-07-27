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

    /// <summary>Which screen the widget lives on. An index for now — #24's EDID-based identity replaces
    /// this, since screen order is not stable across unplug/replug.</summary>
    public int ScreenIndex { get; set; }

    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}
