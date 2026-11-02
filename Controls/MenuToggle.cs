using System.Windows;
using System.Windows.Controls.Primitives;

namespace Aquila.Controls;

/// <summary>
/// A button that opens a menu and wears the theme's chosen look while its menu has something chosen that is not
/// the usual — the edit bar's Preview outside Live. A ToggleButton for that look, which every theme already draws
/// (the kit's checked family, and ours over it); but a click must not toggle it, since the click opens the menu
/// and only what is picked there decides the state. So <see cref="ToggleButton.IsChecked"/> is the caller's, bound
/// one way, and survives the click.
///
/// Given no style of its own, it wears the ToggleButton's: an implicit style is found by the element's own type, so
/// a subclass would otherwise be drawn with none. Only then — asked once its own properties are in, never in the
/// constructor: a style set there is a local value, and inside a template a local value beats the style the template
/// gives, so the gallery's tiles came out as plain toggle buttons the width of their content.
/// </summary>
public sealed class MenuToggle : ToggleButton
{
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        if (Style is null) SetResourceReference(StyleProperty, typeof(ToggleButton));
    }

    protected override void OnToggle() { }
}
