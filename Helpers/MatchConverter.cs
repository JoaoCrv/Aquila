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
/// </summary>
public sealed class MatchConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? parameter : null;
}
