using System.Globalization;
using System.Windows.Data;

namespace Aquila.Helpers;

/// <summary>
/// True when the bound value equals the parameter, and on the way back the parameter itself — or null.
///
/// What a set of mutually exclusive toggles needs, without giving the view model one boolean per option:
/// checking one writes its own name, unchecking writes null. Radio buttons would have wanted the same
/// converter and would additionally have refused to let the last one go, which is the state that means
/// "stop overriding and show me the real thing".
///
/// Bound to a Visibility, the answer is Visible or Collapsed — so one page of several can be shown by the same
/// comparison that checks its tab, without a second converter or a trigger per page.
/// </summary>
public sealed class MatchConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var match = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);
        return targetType == typeof(System.Windows.Visibility)
            ? match ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed
            : match;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? parameter : null;
}
