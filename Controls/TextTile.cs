using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// Words, and optionally a reading after them: "Cooling", or "CPU 62°C".
///
/// The caption is all the user writes. The number and its unit are formatted here, because a format string
/// is a language and this has to work for someone who has never seen one — and because the sensor already
/// knows its own unit, so asking for it again would only be a chance to get it wrong.
///
/// A TextBlock rather than a UserControl with XAML: there is one element and nothing to lay out.
/// </summary>
public sealed class TextTile : TextBlock, ISensorPiece, ICaptionStyle
{
    private SensorNode? _sensor;

    public TextTile()
    {
        VerticalAlignment = VerticalAlignment.Center;
        TextWrapping = TextWrapping.Wrap;
        Compose();
    }

    public double ValueSize
    {
        get => FontSize;
        set => FontSize = value;
    }


    private string? _valueFont;
    private TextWeight _valueWeight = TextWeight.Regular;

    public string? ValueFont
    {
        get => _valueFont;
        set { _valueFont = value; this.Wear(_valueFont, _valueWeight); }
    }

    public TextWeight ValueWeight
    {
        get => _valueWeight;
        set { _valueWeight = value; this.Wear(_valueFont, _valueWeight); }
    }

    public TextAlign Align
    {
        get => TextAlignment.ToAlign();
        set => TextAlignment = value.ToTextAlignment();
    }

    /// <summary>The words. Shown alone when there is no reading, and before it when there is.</summary>
    public string Caption
    {
        get => _caption;
        set { _caption = value; Compose(); }
    }

    private string _caption = string.Empty;

    /// <summary>Whether the reading's unit is written after it.</summary>
    public bool ShowUnit
    {
        get => _showUnit;
        set { _showUnit = value; Compose(); }
    }

    private bool _showUnit = true;

    /// <summary>The reading to show after the caption, if any. Null leaves the caption on its own.</summary>
    public SensorNode? Sensor
    {
        get => _sensor;
        set
        {
            if (_sensor is not null) _sensor.PropertyChanged -= OnSensorChanged;
            _sensor = value;
            if (_sensor is not null) _sensor.PropertyChanged += OnSensorChanged;
            Compose();
        }
    }

    private void OnSensorChanged(object? _, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SensorNode.Value)) return;
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(Compose); return; }
        Compose();
    }

    private void Compose()
    {
        if (_sensor is null)
        {
            Text = _caption;
            return;
        }

        // Volts need decimals to mean anything; everything else reads better rounded. Same rule the other
        // pieces use, so the same sensor is written the same way wherever it appears.
        var value = _sensor.Value is { } v
            ? v.ToString(_sensor.Unit == "V" ? "F2" : "F0")
            : "--";

        var unit = _showUnit ? _sensor.Unit ?? string.Empty : string.Empty;
        var reading = string.IsNullOrEmpty(unit) ? value : $"{value}{unit}";

        Text = string.IsNullOrWhiteSpace(_caption) ? reading : $"{_caption} {reading}";
    }
}
