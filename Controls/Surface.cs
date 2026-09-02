using System.Windows;

namespace Aquila.Controls;

/// <summary>Which ground a control is standing on.</summary>
public enum SurfaceKind
{
    /// <summary>The window itself.</summary>
    App,

    /// <summary>A raised panel — a card, an expander, a flyout.</summary>
    Card,

    /// <summary>A recess inside a card: the summary block, an inset list.</summary>
    Well,
}

/// <summary>
/// The surface a subtree is drawn on, declared once by whatever draws it.
///
/// The alternative was three styles per control — "button on the app", "button in a card" — chosen by
/// whoever placed each one. That multiplies out into controls × surfaces × states and puts the burden on
/// the person least able to carry it: someone dropping a button into a card has to know which of three
/// styles the card wants, and gets it wrong silently.
///
/// Inherited instead, so a container says it ONCE and everything inside adapts. There is precedent in
/// house: <see cref="BarShape"/> does the same for a corner radius, for the same reason.
///
/// This is a token, not a look. What each surface does to a control belongs in Themes/Controls.xaml, and
/// what its colours are belongs in a theme's tokens — nothing here decides either.
/// </summary>
public static class Surface
{
    public static readonly DependencyProperty KindProperty =
        DependencyProperty.RegisterAttached(
            "Kind", typeof(SurfaceKind), typeof(Surface),
            new FrameworkPropertyMetadata(SurfaceKind.App, FrameworkPropertyMetadataOptions.Inherits));

    public static void SetKind(DependencyObject element, SurfaceKind value) =>
        element.SetValue(KindProperty, value);

    public static SurfaceKind GetKind(DependencyObject element) =>
        (SurfaceKind)element.GetValue(KindProperty);
}
