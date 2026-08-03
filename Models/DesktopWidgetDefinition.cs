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

    public DesktopWidgetDefinition Clone() => (DesktopWidgetDefinition)MemberwiseClone();

    /// <summary>
    /// Restores this definition's values from another, IN PLACE. Editing mutates the live definition so
    /// the desktop updates as you type; cancelling has to undo that without swapping the instance, because
    /// the rendered element is tracked by reference.
    /// </summary>
    public void CopyFrom(DesktopWidgetDefinition other)
    {
        Kind = other.Kind;
        SensorIdentifier = other.SensorIdentifier;
        Title = other.Title;
        AccentKey = other.AccentKey;
        ScreenKey = other.ScreenKey;
        ScreenIndex = other.ScreenIndex;
        X = other.X; Y = other.Y;
        Width = other.Width; Height = other.Height;
        BackgroundColor = other.BackgroundColor;
        BackgroundOpacity = other.BackgroundOpacity;
        CornerRadius = other.CornerRadius;
        BorderColor = other.BorderColor;
        BorderOpacity = other.BorderOpacity;
        BorderThickness = other.BorderThickness;
    }
}
