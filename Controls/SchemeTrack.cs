using System.Windows;
using System.Windows.Media;
using Aquila.Controls.Primitives;

namespace Aquila.Controls;

/// <summary>A piece that draws bars over the dashboard's track when its own preset gives none — told when that track
/// changes, so it can make its look again.</summary>
public interface IFollowsSchemeTrack
{
    void SchemeTrackChanged();
}

/// <summary>
/// The dashboard's bar track — <c>Aquila.Scheme.Track</c>, which the dashboard's preset publishes — followed live by a
/// piece with no track of its own: a card's meter, which is never dressed, and any widget before it is.
///
/// One home for what each bar piece used to do for itself: the meter, the bars and the list each set that key as a
/// resource of their own so the bars inside would find it, and the Number followed it through a property of its own.
/// A look holds a brush, not a resource, so the brush is read from here whenever a look is made, and the piece is told
/// when it changes, to make it again.
/// </summary>
public static class SchemeTrack
{
    private const string Key = "Aquila.Scheme.Track";

    public static readonly DependencyProperty BrushProperty = DependencyProperty.RegisterAttached(
        "Brush", typeof(Brush), typeof(SchemeTrack),
        new PropertyMetadata(null, (piece, _) => (piece as IFollowsSchemeTrack)?.SchemeTrackChanged()));

    /// <summary>Starts following the dashboard's track on <paramref name="piece"/>, by reference: read once, it would
    /// keep the track it found through every preset change after.</summary>
    public static void Follow(FrameworkElement piece) => piece.SetResourceReference(BrushProperty, Key);

    /// <summary>The look to draw: the one given — a dressed piece's, from its own preset — or the piece's own default;
    /// and, if it names no track, the dashboard's.</summary>
    public static BarLook Resolve(DependencyObject piece, BarLook? given, BarLook fallback)
    {
        var look = given ?? fallback;
        return look.Track is null ? look with { Track = (Brush?)piece.GetValue(BrushProperty) } : look;
    }
}
