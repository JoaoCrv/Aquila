using Aquila.Models;
using Aquila.Services;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace Aquila.Tests;

/// <summary>
/// The widget scale (<see cref="WidgetScale"/>): one multiplier every size the preset gives is put through. A scale
/// that jumped when a widget grew, or came out at 1.0000001 for a widget nobody resized, shows on the desktop as text
/// that twitches or a widget that is not the one the type picker showed — and is a sum here, held for any size.
/// </summary>
public sealed class ScaleProperties
{
    private static Gen<WidgetKindInfo> Kinds() => Gen.Elements(WidgetCatalog.All.ToArray());

    /// <summary>A size as widgets.json may hold one: anything from nothing to far past any screen, to the hundredth,
    /// with the sizes the editor and the wheel stop at drawn often, since an even spread would rarely land on them.
    /// </summary>
    private static Gen<double> Sizes() =>
        Gen.OneOf(
            Gen.Choose(0, 4_000_000).Select(v => v / 100d),
            Gen.Elements(0d, 1d, 40d, 60d, 80d, 500d, 600d, 4000d));

    /// <summary>What a hand can also write: below nothing, absurdly large, and not a number at all.</summary>
    private static Gen<double> AnySizes() =>
        Gen.OneOf(Sizes(), Gen.Elements(-1d, -500d, 1e12, double.MaxValue, double.NaN));

    private static DesktopWidgetDefinition Sized(WidgetKindInfo kind, double width, double height) =>
        new() { Kind = kind.Kind, Width = width, Height = height };

    /// <summary>Every kind at its default size is at exactly 1 — the size the type picker shows it at, and the size
    /// every layout file is started with. Exactly, not nearly: at 1 a widget draws what it drew before there was a
    /// scale, and was compared with that to the pixel.</summary>
    [Fact]
    public void At_its_default_size_every_kind_is_at_one()
    {
        Assert.All(WidgetCatalog.All,
            kind => Assert.Equal(1d, WidgetScale.Of(Sized(kind, kind.DefaultWidth, kind.DefaultHeight))));
    }

    /// <summary>
    /// A clock written by 2.3.0 — no rings, 200×70, the text clock's size then — is drawn exactly as it was. Measured
    /// against the ringed clock's 200×200 it was a third of itself, a 6px time; a ringless clock is a line of text,
    /// and is measured as one. Rings, and the clock is a ring again.
    /// </summary>
    [Theory]
    [InlineData(ClockRings.None, 200, 70, 1)]
    [InlineData(ClockRings.None, 400, 140, 2)]
    [InlineData(ClockRings.None, 200, 200, 1)]
    [InlineData(ClockRings.All, 200, 200, 1)]
    [InlineData(ClockRings.All, 200, 70, 0.35)]
    [InlineData(ClockRings.Seconds, 600, 600, 3)]
    public void A_clock_is_measured_by_the_figure_it_draws(ClockRings rings, double width, double height, double scale)
    {
        var clock = new DesktopWidgetDefinition { Kind = DesktopWidgetKind.Clock, Rings = rings, Width = width, Height = height };

        Assert.Equal(scale, WidgetScale.Of(clock), precision: 6);
    }

    /// <summary>Whatever a file says the size is, the scale stays within its bounds.</summary>
    [Property(MaxTest = 1000)]
    public Property The_scale_is_always_within_its_bounds() =>
        Prop.ForAll(
            (from kind in Kinds() from width in AnySizes() from height in AnySizes() select (kind, width, height))
            .ToArbitrary(),
            widget =>
            {
                var scale = WidgetScale.Of(Sized(widget.kind, widget.width, widget.height));
                return scale is >= WidgetScale.Least and <= WidgetScale.Most;
            });

    /// <summary>
    /// The tighter axis decides: the scale is that axis' share of the kind's reference size, held to the bounds — and
    /// stretching a widget along the other axis leaves it where it was, so a meter made only wider is a longer meter
    /// with the same text.
    /// </summary>
    [Property(MaxTest = 1000)]
    public Property The_tighter_axis_decides() =>
        Prop.ForAll(
            (from kind in Kinds()
             from width in Sizes()
             from height in Sizes()
             from stretch in Sizes()
             select (kind, width, height, stretch))
            .ToArbitrary(),
            widget =>
            {
                var (kind, width, height, stretch) = widget;
                var (referenceWidth, referenceHeight) =
                    kind.Reference?.Invoke(Sized(kind, width, height)) ?? (kind.DefaultWidth, kind.DefaultHeight);
                var across = width / referenceWidth;
                var down = height / referenceHeight;
                var scale = WidgetScale.Of(Sized(kind, width, height));

                var decided = scale == Math.Clamp(Math.Min(across, down), WidgetScale.Least, WidgetScale.Most);

                var stretched = across <= down
                    ? WidgetScale.Of(Sized(kind, width, height + stretch))
                    : WidgetScale.Of(Sized(kind, width + stretch, height));

                return decided && stretched == scale;
            });

    /// <summary>Made bigger the same way on both axes, a widget is that much bigger — within the bounds. A tenth of
    /// a millionth is allowed for, since a size times a factor over the size is not always the factor to the last
    /// bit.</summary>
    [Property(MaxTest = 1000)]
    public Property Grown_evenly_a_widget_scales_by_as_much() =>
        Prop.ForAll(
            (from kind in Kinds()
             from factor in Gen.Choose(1, 1200).Select(f => f / 100d)
             select (kind, factor))
            .ToArbitrary(),
            widget =>
            {
                var (kind, factor) = widget;
                var scale = WidgetScale.Of(Sized(kind, kind.DefaultWidth * factor, kind.DefaultHeight * factor));
                return Math.Abs(scale - Math.Clamp(factor, WidgetScale.Least, WidgetScale.Most)) < 1e-7;
            });

    /// <summary>Making a widget bigger in either direction never makes what is drawn in it smaller.</summary>
    [Property(MaxTest = 1000)]
    public Property Growing_either_dimension_never_shrinks_the_scale() =>
        Prop.ForAll(
            (from kind in Kinds()
             from width in Sizes()
             from height in Sizes()
             from wider in Sizes()
             from taller in Sizes()
             select (kind, width, height, wider, taller))
            .ToArbitrary(),
            widget =>
            {
                var (kind, width, height, wider, taller) = widget;
                var scale = WidgetScale.Of(Sized(kind, width, height));

                return WidgetScale.Of(Sized(kind, width + wider, height)) >= scale
                    && WidgetScale.Of(Sized(kind, width, height + taller)) >= scale
                    && WidgetScale.Of(Sized(kind, width + wider, height + taller)) >= scale;
            });
}
