using System.Windows;
using System.Windows.Controls;

namespace Aquila.Controls;

/// <summary>
/// A number on one line of the editor: a short slider, with what it reads beside it.
///
/// The slider is fed by hand rather than bound. It coerces to its range, and a two-way binding would carry that
/// back: a value arriving before the range, with the slider's Maximum still at its default of 10, would write 10
/// over a preset's 80.
/// </summary>
public partial class SliderField : UserControl
{
    private string _format = "{0:F0}";
    private bool _showing;

    public SliderField()
    {
        InitializeComponent();
        Show();
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(SliderField),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, _) => ((SliderField)d).Show()));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(SliderField),
            new PropertyMetadata(10d, (d, e) => ((SliderField)d).Bar.Maximum = (double)e.NewValue));

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Minimum
    {
        get => Bar.Minimum;
        set => Bar.Minimum = value;
    }

    /// <summary>Set, the slider stops only on multiples of this; left alone, it moves freely.</summary>
    public double Step
    {
        get => Bar.TickFrequency;
        set
        {
            Bar.TickFrequency = value;
            Bar.IsSnapToTickEnabled = true;
        }
    }

    /// <summary>How the reading is written, as a composite format: <c>{0:F0} px</c>.</summary>
    public string Format
    {
        get => _format;
        set
        {
            _format = value;
            Show();
        }
    }

    private void Show()
    {
        _showing = true;
        Bar.Value = Value;
        _showing = false;

        Reading.Text = string.Format(_format, Value);
    }

    private void OnSlid(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_showing) Value = e.NewValue;
    }
}
