using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Aquila.Controls;

/// <summary>
/// A colour, shown as itself and edited in a flyout: a saturation/brightness field, a hue bar, a hex box
/// and a row of swatches.
///
/// Built rather than borrowed because WPF-UI ships no colour picker, and the alternative on offer was a
/// text box wanting six hexadecimal digits — which is fine for the person writing the app and hopeless for
/// everyone the app is for.
///
/// Both halves are here on purpose. The swatches answer "the same colour as that other thing", which is
/// most of what building a preset actually is; the field answers "a bit warmer than this", which no list of
/// swatches ever can. Neither one alone would do.
///
/// The value is a hex string rather than a Color, because that is what a preset stores: converting at the
/// edge keeps one representation in the file and one in the UI, with a single crossing between them.
/// </summary>
public partial class ColorField : UserControl
{
    private bool _updating;

    public ColorField()
    {
        InitializeComponent();
        Show();
    }

    public static readonly DependencyProperty ColorProperty =
        DependencyProperty.Register(nameof(Color), typeof(string), typeof(ColorField),
            new FrameworkPropertyMetadata("#000000",
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, _) => ((ColorField)d).Show()));

    /// <summary>The colour, as the six-digit hex a preset stores.</summary>
    public string Color
    {
        get => (string)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public static readonly DependencyProperty SwatchesProperty =
        DependencyProperty.Register(nameof(Swatches), typeof(IEnumerable), typeof(ColorField),
            new PropertyMetadata(null, (d, e) => ((ColorField)d).SwatchList.ItemsSource = (IEnumerable?)e.NewValue));

    /// <summary>Colours worth offering. Fed the preset's own where it is used, so the swatches are the ones
    /// this design already contains rather than a generic rainbow.</summary>
    public IEnumerable? Swatches
    {
        get => (IEnumerable?)GetValue(SwatchesProperty);
        set => SetValue(SwatchesProperty, value);
    }

    // The hue is kept apart from the colour rather than read back from it. Black and white have no hue to
    // read, so a field driven by the value alone jumps back to red the moment the brightness reaches zero.
    private double _hue;
    private double _saturation;
    private double _value = 1;

    /// <summary>
    /// Opens the flyout on the button coming back UP, not going down.
    ///
    /// A popup with StaysOpen false takes the mouse when it opens, so one opened on the press reads the
    /// release that follows as a click outside itself and shuts again — leaving a control that only stays
    /// open if you hold the button and drag onto it.
    /// </summary>
    private void OnOpen(object sender, MouseButtonEventArgs e)
    {
        (_hue, _saturation, _value) = ToHsv(Parse(Color));
        Place();
        Picker.IsOpen = true;
        e.Handled = true;
    }

    /// <summary>Paints the swatch and the flyout from the current value. Guarded, because the hex box is
    /// one of the things it writes to and it must not read its own writing back.</summary>
    private void Show()
    {
        if (_updating) return;

        var colour = Parse(Color);
        Swatch.Background = new SolidColorBrush(colour);

        _updating = true;
        try
        {
            Hex.Text = $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";
        }
        finally
        {
            _updating = false;
        }
    }

    /// <summary>Moves the two markers and repaints the field's hue. Only meaningful once the popup has been
    /// laid out, which is why it is called on open rather than on every value change.</summary>
    private void Place()
    {
        FieldHue.Background = new LinearGradientBrush(
            Colors.White, FromHsv(_hue, 1, 1), new Point(0, 0), new Point(1, 0));

        var w = Field.ActualWidth > 0 ? Field.ActualWidth : 188;
        var h = Field.ActualHeight > 0 ? Field.ActualHeight : 120;

        // Half the marker's own size, so the middle of it lands on the value it reports.
        Canvas.SetLeft(Crosshair, _saturation * w - 7);
        Canvas.SetTop(Crosshair, (1 - _value) * h - 7);
        Canvas.SetLeft(HueMark, _hue / 360 * (Hue.ActualWidth > 0 ? Hue.ActualWidth : 188) - 3);
    }

    private void Commit()
    {
        var colour = FromHsv(_hue, _saturation, _value);
        Color = $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";
        Place();
    }

    private void OnFieldDown(object sender, MouseButtonEventArgs e)
    {
        Field.CaptureMouse();
        Pick(e.GetPosition(Field));
    }

    private void OnFieldMove(object sender, MouseEventArgs e)
    {
        if (Field.IsMouseCaptured) Pick(e.GetPosition(Field));
    }

    private void Pick(Point at)
    {
        _saturation = Math.Clamp(at.X / Field.ActualWidth, 0, 1);
        _value = Math.Clamp(1 - at.Y / Field.ActualHeight, 0, 1);
        Commit();
    }

    private void OnHueDown(object sender, MouseButtonEventArgs e)
    {
        Hue.CaptureMouse();
        PickHue(e.GetPosition(Hue));
    }

    private void OnHueMove(object sender, MouseEventArgs e)
    {
        if (Hue.IsMouseCaptured) PickHue(e.GetPosition(Hue));
    }

    private void PickHue(Point at)
    {
        _hue = Math.Clamp(at.X / Hue.ActualWidth, 0, 1) * 360;
        Commit();
    }

    private void OnRelease(object sender, MouseButtonEventArgs e)
    {
        Field.ReleaseMouseCapture();
        Hue.ReleaseMouseCapture();
    }

    private void OnSwatchPicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: string hex }) return;

        Color = hex;
        (_hue, _saturation, _value) = ToHsv(Parse(hex));
        Place();
    }

    private void OnHexKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        OnHexCommitted(sender, e);
        e.Handled = true;
    }

    /// <summary>Takes what was typed if it parses, and quietly puts the old value back if it does not —
    /// there is no state a half-typed colour could usefully be left in.</summary>
    private void OnHexCommitted(object sender, RoutedEventArgs e)
    {
        if (_updating) return;

        var text = Hex.Text.Trim();
        if (!text.StartsWith('#')) text = "#" + text;

        try
        {
            var colour = (System.Windows.Media.Color)ColorConverter.ConvertFromString(text);
            Color = $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";
            (_hue, _saturation, _value) = ToHsv(colour);
            Place();
        }
        catch
        {
            Show();
        }
    }

    private static System.Windows.Media.Color Parse(string? hex)
    {
        try
        {
            return string.IsNullOrWhiteSpace(hex)
                ? Colors.Black
                : (System.Windows.Media.Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.Black;
        }
    }

    private static (double H, double S, double V) ToHsv(System.Windows.Media.Color c)
    {
        double r = c.R / 255d, g = c.G / 255d, b = c.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var span = max - min;

        var h = span == 0 ? 0
            : max == r ? 60 * (((g - b) / span + 6) % 6)
            : max == g ? 60 * ((b - r) / span + 2)
            : 60 * ((r - g) / span + 4);

        return (h, max == 0 ? 0 : span / max, max);
    }

    private static System.Windows.Media.Color FromHsv(double h, double s, double v)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;

        var (r, g, b) = h switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x),
        };

        return System.Windows.Media.Color.FromRgb(
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}
