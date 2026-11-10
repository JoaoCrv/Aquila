using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using Aquila.Models;

namespace Aquila.Controls;

/// <summary>
/// The numbers a widget writes at the end of its title's line, one per reading (<see cref="ITitleAside"/>): one reading
/// at the reading's size and face, several at the notes' size, each in its own reading's colour — they would not fit at
/// the reading's. Written by the Chart and the Bars alike, so the two can never come to write them differently.
/// </summary>
public sealed class TitleNumbers : StackPanel
{
    /// <summary>Between two numbers, at a scale of one.</summary>
    public const double Apart = 10;

    private readonly TextBlock[] _numbers;

    public TitleNumbers(int count)
    {
        Orientation = Orientation.Horizontal;
        _numbers = new TextBlock[count];

        for (var i = 0; i < count; i++)
        {
            var number = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            Typography.SetNumeralAlignment(number, FontNumeralAlignment.Tabular);
            _numbers[i] = number;
            Children.Add(number);
        }
    }

    /// <summary>One reading's number, in its colour.</summary>
    public void Write(int index, string text, Brush ink)
    {
        if (index < 0 || index >= _numbers.Length) return;
        _numbers[index].Text = text;
        _numbers[index].Foreground = ink;
    }

    /// <summary>The numbers at the sizes and in the faces they have now.</summary>
    public void Letter(double valueSize, string? valueFont, TextWeight valueWeight, double noteSize, double scale)
    {
        var single = _numbers.Length == 1;

        for (var i = 0; i < _numbers.Length; i++)
        {
            var number = _numbers[i];
            number.FontSize = single ? Math.Max(1, valueSize) : Math.Max(1, noteSize);
            number.Wear(valueFont, single ? valueWeight : TextWeight.Regular);
            number.Margin = new Thickness(i == 0 ? 0 : Apart * scale, 0, 0, 0);
        }
    }
}

/// <summary>
/// A legend of a widget's readings: for each, a swatch of its colour, its name in the title's face and colour, and its
/// number in its colour — folding into more lines when the names do not fit across (<see cref="LegendPanel"/>).
/// Written by the Chart under its lines and by the Bars under theirs, so the two legends are one.
/// </summary>
public sealed class ReadingLegend : LegendPanel
{
    /// <summary>From what the legend follows down to it, and between its lines, at a scale of one.</summary>
    private const double Above = 4, BetweenLines = 2;

    /// <summary>A swatch of the reading's colour, and the gap after it.</summary>
    private const double SwatchWidth = 10, SwatchHeight = 2, SwatchGap = 5;

    private readonly (TextBlock Name, TextBlock Number, Rectangle Swatch)[] _rows;

    public ReadingLegend(IReadOnlyList<string> names)
    {
        _rows = new (TextBlock, TextBlock, Rectangle)[names.Count];

        for (var i = 0; i < names.Count; i++)
        {
            var swatch = new Rectangle
            {
                Width = SwatchWidth, Height = SwatchHeight, VerticalAlignment = VerticalAlignment.Center,
            };
            var name = new TextBlock
            {
                Text = names[i], TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center,
            };
            var number = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            Typography.SetNumeralAlignment(number, FontNumeralAlignment.Tabular);

            var row = new DockPanel();
            DockPanel.SetDock(swatch, Dock.Left);
            DockPanel.SetDock(number, Dock.Right);
            row.Children.Add(swatch);
            row.Children.Add(number);
            row.Children.Add(name);

            _rows[i] = (name, number, swatch);
            Children.Add(row);
        }
    }

    /// <summary>One reading's name.</summary>
    public void NameAt(int index, string text)
    {
        if (index >= 0 && index < _rows.Length) _rows[index].Name.Text = text;
    }

    /// <summary>One reading's number and swatch, in its colour.</summary>
    public void Write(int index, string number, Brush ink)
    {
        if (index < 0 || index >= _rows.Length) return;

        var (_, text, swatch) = _rows[index];
        text.Text = number;
        text.Foreground = ink;
        swatch.Fill = ink;
    }

    /// <summary>The legend at the sizes and in the faces it has now: names in the title's face at the notes' size and
    /// colour, numbers in the reading's face; its gaps at the widget's scale.</summary>
    public void Letter(double noteSize, string? titleFont, TextWeight titleWeight, string? valueFont, Brush notes,
        double scale)
    {
        foreach (var (name, number, swatch) in _rows)
        {
            name.FontSize = Math.Max(1, noteSize);
            name.Wear(titleFont, titleWeight);
            name.Foreground = notes;
            number.FontSize = Math.Max(1, noteSize);
            number.Wear(valueFont, TextWeight.Regular);
            number.Margin = new Thickness(SwatchGap * scale, 0, 0, 0);
            swatch.Width = SwatchWidth * scale;
            swatch.Height = Math.Max(1, SwatchHeight * scale);
            swatch.Margin = new Thickness(0, 0, SwatchGap * scale, 0);
        }

        Margin = new Thickness(0, Above * scale, 0, 0);
        (Gap, LineGap) = (TitleNumbers.Apart * scale, BetweenLines * scale);
    }
}
