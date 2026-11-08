using System.Windows;
using Aquila.Controls.Primitives;

namespace Aquila.Controls;

/// <summary>
/// A bar for a value handed over by binding — <see cref="Value"/> between <see cref="Minimum"/> and
/// <see cref="Maximum"/> — over the dashboard's track: a card's VRAM, a fan's duty, a drive's capacity, the title
/// bar's pills.
///
/// What the ProgressBar on the ThinBar template was, drawn by <see cref="BarFill"/>: it travels at the pace instead of
/// jumping, never draws shorter than the least share, and is drawn rather than laid out. Its colour is the
/// <see cref="BarFill.Fill"/> the page gives it, by binding or by style — there is no default here, because a local
/// value set in a constructor would beat a style's, and the fan rows choose theirs by style.
/// </summary>
public sealed class ValueBar : BarFill, IFollowsSchemeTrack
{
    public static readonly DependencyProperty ValueProperty = RegisterValue(nameof(Value), 0d);
    public static readonly DependencyProperty MinimumProperty = RegisterValue(nameof(Minimum), 0d);
    public static readonly DependencyProperty MaximumProperty = RegisterValue(nameof(Maximum), 100d);

    public static readonly DependencyProperty BarThicknessProperty = RegisterLook(nameof(BarThickness), 6d);
    public static readonly DependencyProperty BarCornerProperty = RegisterLook(nameof(BarCorner), 3d);

    /// <summary>The value drawn.</summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Where the bar is empty. Nought by default.</summary>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>Where the bar is full. A hundred by default.</summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>How thick the bar is: six pixels, the cards' bar, by default.</summary>
    public double BarThickness
    {
        get => (double)GetValue(BarThicknessProperty);
        set => SetValue(BarThicknessProperty, value);
    }

    /// <summary>The radius of its ends: three pixels, the cards' bar, by default.</summary>
    public double BarCorner
    {
        get => (double)GetValue(BarCornerProperty);
        set => SetValue(BarCornerProperty, value);
    }

    private static DependencyProperty RegisterValue(string name, double fallback) =>
        DependencyProperty.Register(name, typeof(double), typeof(ValueBar),
            new PropertyMetadata(fallback, (bar, _) => ((ValueBar)bar).Show(travel: ((ValueBar)bar).IsLoaded)));

    private static DependencyProperty RegisterLook(string name, double fallback) =>
        DependencyProperty.Register(name, typeof(double), typeof(ValueBar),
            new PropertyMetadata(fallback, (bar, _) => ((ValueBar)bar).Dress()));

    public ValueBar()
    {
        SchemeTrack.Follow(this);
        Dress();
        Loaded += (_, _) => Show(travel: false);
    }

    void IFollowsSchemeTrack.SchemeTrackChanged() => Dress();

    private void Dress() => Look = SchemeTrack.Resolve(this, null, new BarLook(BarThickness, BarCorner, null));

    /// <summary>
    /// The value as a share of the scale, travelling there once the bar is on screen — and jumping before, and as it
    /// arrives: bindings hand over the value and the maximum one after the other, and a bar that travelled between
    /// them would sweep up from nothing every time a card appeared. A scale with no height draws an empty bar rather
    /// than a division by nothing.
    /// </summary>
    private void Show(bool travel)
    {
        var (min, max) = (Minimum, Maximum);
        MoveTo(max > min ? (Value - min) / (max - min) : 0, travel);
    }
}
