using System.Windows;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// What kind of thing a piece of text is saying about the app.
///
/// Set on the element itself rather than read off a view model by name, so the rule that colours it in
/// Themes/Controls.xaml is a rule about text and not a rule about one page. The first version bound
/// through <c>RelativeSource AncestorType=Page</c> to a property called UpdateStatusKind, which worked
/// exactly once and would have failed silently everywhere else.
///
/// Same shape as <see cref="Surface"/>, and for the same reason: the thing that knows declares it, and
/// the style asks the element rather than its surroundings.
/// </summary>
public static class Status
{
    public static readonly DependencyProperty KindProperty =
        DependencyProperty.RegisterAttached(
            "Kind", typeof(StatusKind), typeof(Status), new PropertyMetadata(StatusKind.Plain));

    public static void SetKind(DependencyObject element, StatusKind value) =>
        element.SetValue(KindProperty, value);

    public static StatusKind GetKind(DependencyObject element) =>
        (StatusKind)element.GetValue(KindProperty);
}
