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

    /// <summary>
    /// A list written before bars — none asked for, 240×170 — is drawn exactly as it was; a list made today, with
    /// bars, is at one at its own starting size, and turning its bar off there leaves its text the size it was. With
    /// bars it needs more height, so in the old box it is drawn smaller.
    /// </summary>
    [Theory]
    [InlineData(false, 240, 170, 1)]
    [InlineData(false, 480, 340, 2)]
    [InlineData(true, 240, 230, 1)]
    [InlineData(false, 240, 230, 1)]
    [InlineData(true, 240, 170, 170 / 230d)]
    public void A_list_is_measured_by_the_figure_it_draws(bool bars, double width, double height, double scale)
    {
        var list = new DesktopWidgetDefinition { Kind = DesktopWidgetKind.List, ShowBar = bars, Width = width, Height = height };

        Assert.Equal(scale, WidgetScale.Of(list), precision: 6);
    }

    /// <summary>
    /// A Number written before its notes — none asked for, 150×120 — is drawn exactly as it was, and one made today,
    /// with them, is at one at its own starting size, 180×120. Turning its notes off there leaves its number the size it
    /// was; turning them on in the old box draws it all a little smaller, so "min 0  max 980.4 MB/s" still fits.
    /// </summary>
    [Theory]
    [InlineData(false, 150, 120, 1)]
    [InlineData(false, 300, 240, 2)]
    [InlineData(false, 180, 120, 1)]
    [InlineData(true, 180, 120, 1)]
    [InlineData(true, 150, 120, 150 / 180d)]
    public void A_number_is_measured_by_the_figure_it_draws(bool notes, double width, double height, double scale)
    {
        var number = new DesktopWidgetDefinition
        {
            Kind = DesktopWidgetKind.StatBox, ShowMinMax = notes, Width = width, Height = height,
        };

        Assert.Equal(scale, WidgetScale.Of(number), precision: 6);
    }

    /// <summary>
    /// A Chart is measured against 240×100 whatever it writes and however many readings it draws: a number moved from
    /// the title to a legend, a second reading or the scale's labels never resize what is already drawn — they take
    /// their room from the plot (docs/widget-grafico). Its old two-reading sibling keeps the 300×160 it always had.
    /// </summary>
    [Property(MaxTest = 500)]
    public Property A_chart_is_measured_at_its_reference_with_any_numbers() =>
        Prop.ForAll(
            (from place in Gen.Elements(Enum.GetValues<ReadingPlace>())
             from readings in Gen.Choose(1, 4)
             from parts in Gen.Choose(0, 31)
             from width in Sizes()
             from height in Sizes()
             select (place, readings, parts, width, height))
            .ToArbitrary(),
            chart =>
            {
                var (place, readings, parts, width, height) = chart;
                var widget = new DesktopWidgetDefinition
                {
                    Kind = DesktopWidgetKind.Chart, Width = width, Height = height, ReadingsAt = place,
                    Series = [.. Enumerable.Range(0, readings).Select(i => new WidgetSeries { SensorIdentifier = $"/s/{i}" })],
                    ShowScaleLabels = (parts & 1) != 0, ShowPeak = (parts & 2) != 0, ShowEnd = (parts & 4) != 0,
                    ShowLimits = (parts & 8) != 0, ColourByReading = (parts & 16) != 0,
                };

                return WidgetScale.ReferenceOf(widget) == (240d, 100d)
                    && WidgetScale.Of(widget) == Math.Clamp(Math.Min(width / 240, height / 100), WidgetScale.Least, WidgetScale.Most);
            });

    [Fact]
    public void An_old_chart_keeps_its_reference()
    {
        Assert.Equal((300d, 160d),
            WidgetScale.ReferenceOf(new DesktopWidgetDefinition { Kind = DesktopWidgetKind.SparklineChart }));
    }

    /// <summary>
    /// Bars are measured against 240×80 whatever they write, whichever way they run and however many readings they
    /// draw: more bars share the same room, and the text moving above or beside them never resizes what is drawn
    /// (docs/widget-barras). The old Bars keep the 260×140 they always had, and a meter — read as Bars — the 240×80 it
    /// had as a meter.
    /// </summary>
    [Property(MaxTest = 500)]
    public Property Bars_are_measured_at_their_reference_with_any_text() =>
        Prop.ForAll(
            (from layout in Gen.Elements(Enum.GetValues<BarLayout>())
             from direction in Gen.Elements(Enum.GetValues<BarDirection>())
             from readings in Gen.Choose(1, 16)
             from parts in Gen.Choose(0, 15)
             from width in Sizes()
             from height in Sizes()
             select (layout, direction, readings, parts, width, height))
            .ToArbitrary(),
            bars =>
            {
                var (layout, direction, readings, parts, width, height) = bars;
                var widget = new DesktopWidgetDefinition
                {
                    Kind = DesktopWidgetKind.BarChart, Width = width, Height = height,
                    BarLayout = layout, BarDirection = direction,
                    Series = [.. Enumerable.Range(0, readings).Select(i => new WidgetSeries { SensorIdentifier = $"/s/{i}" })],
                    ShowNames = (parts & 1) != 0, ShowPeak = (parts & 2) != 0, ColourByReading = (parts & 4) != 0,
                    ShowLimits = (parts & 8) != 0,
                };

                return WidgetScale.ReferenceOf(widget) == (240d, 80d)
                    && WidgetScale.Of(widget) == Math.Clamp(Math.Min(width / 240, height / 80), WidgetScale.Least, WidgetScale.Most);
            });

    [Fact]
    public void Old_bars_and_a_meter_keep_their_references()
    {
        Assert.Equal((260d, 140d), WidgetScale.ReferenceOf(new DesktopWidgetDefinition { Kind = DesktopWidgetKind.Bars }));
        Assert.Equal((240d, 80d), WidgetScale.ReferenceOf(new DesktopWidgetDefinition { Kind = DesktopWidgetKind.SensorMeter }));
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

    /// <summary>Every kind with every choice of rings — what a resize can meet — split by whether the catalog says it
    /// has a shape of its own. Asked of the catalog rather than listed, so a kind that starts having one is tested
    /// here without anyone remembering to add it.</summary>
    private static readonly DesktopWidgetDefinition[] Everything =
    [
        .. from kind in WidgetCatalog.All
           from rings in Enum.GetValues<ClockRings>()
           select new DesktopWidgetDefinition { Kind = kind.Kind, Rings = rings },
    ];

    private static readonly DesktopWidgetDefinition[] Shaped =
        [.. Everything.Where(w => WidgetScale.ShapeOf(w) is not null)];

    private static readonly DesktopWidgetDefinition[] Free =
        [.. Everything.Where(w => WidgetScale.ShapeOf(w) is null)];

    /// <summary>The dial and a clock with rings have a shape of their own; a clock without rings is a line of text,
    /// and every other kind has none.</summary>
    [Fact]
    public void The_round_figures_have_a_shape_and_nothing_else_does()
    {
        Assert.All(Shaped, w => Assert.True(
            w.Kind == DesktopWidgetKind.RadialGauge || w is { Kind: DesktopWidgetKind.Clock, Rings: not ClockRings.None }));

        Assert.Contains(Shaped, w => w.Kind == DesktopWidgetKind.RadialGauge);
        Assert.Contains(Shaped, w => w is { Kind: DesktopWidgetKind.Clock, Rings: ClockRings.All });
        Assert.Contains(Free, w => w is { Kind: DesktopWidgetKind.Clock, Rings: ClockRings.None });
    }

    /// <summary>
    /// Resized any way, a widget held to its shape comes out in its reference's proportions — led by the side that
    /// was set, which it keeps whenever the range allows it, and inside the range on both sides when there is one.
    /// The editor's range is drawn often, since that is where a dial cannot be both its widest and its height taller.
    /// </summary>
    [Property(MaxTest = 1000)]
    public Property A_shape_keeping_resize_keeps_the_reference_proportions() =>
        Prop.ForAll(
            (from widget in Gen.Elements(Shaped)
             from width in Sizes()
             from height in Sizes()
             from lead in Gen.Elements(SizeLead.Width, SizeLead.Height)
             from range in Gen.Elements((0d, double.PositiveInfinity), (40d, 4000d), (80d, 600d))
             select (widget, width, height, lead, range))
            .ToArbitrary(),
            resize =>
            {
                var (widget, width, height, lead, (least, most)) = resize;
                var (across, down) = WidgetScale.ReferenceOf(widget);
                var (w, h) = WidgetScale.Proportioned(widget, width, height, lead, least, most);

                var shaped = Math.Abs(w * down - h * across) <= 1e-9 * Math.Max(1, Math.Max(w * down, h * across));
                var inside = w >= least - 1e-9 && w <= most + 1e-9 && h >= least - 1e-9 && h <= most + 1e-9;

                // The lead stays as it was set wherever the range lets the shape have it.
                var (asked, got, low, high) = lead == SizeLead.Width
                    ? (width, w, Math.Max(least, least * across / down), Math.Min(most, most * across / down))
                    : (height, h, Math.Max(least, least * down / across), Math.Min(most, most * down / across));
                var led = asked < low || asked > high || got == asked;

                return shaped && inside && led;
            });

    /// <summary>A free kind is given exactly the size it was asked for — any size, either lead, any range — so the
    /// rule cannot reach a widget it is not about.</summary>
    [Property(MaxTest = 1000)]
    public Property A_free_kinds_resize_is_untouched() =>
        Prop.ForAll(
            (from widget in Gen.Elements(Free)
             from width in AnySizes()
             from height in AnySizes()
             from lead in Gen.Elements(SizeLead.Width, SizeLead.Height)
             from range in Gen.Elements((0d, double.PositiveInfinity), (40d, 4000d))
             select (widget, width, height, lead, range))
            .ToArbitrary(),
            resize =>
            {
                var (widget, width, height, lead, (least, most)) = resize;
                var (w, h) = WidgetScale.Proportioned(widget, width, height, lead, least, most);
                return w.Equals(width) && h.Equals(height);
            });

    /// <summary>At its reference width a widget gets its reference height exactly, not a bit either side — the editor's
    /// boxes show whole numbers, and a 189.99999999999997 there is a box that changed by itself.</summary>
    [Fact]
    public void At_its_reference_width_a_shaped_widget_gets_its_reference_height_exactly()
    {
        Assert.All(Shaped, widget =>
        {
            var (across, down) = WidgetScale.ReferenceOf(widget);
            Assert.Equal((across, down), WidgetScale.Proportioned(widget, across, 1, SizeLead.Width));
            Assert.Equal((across * 2, down * 2), WidgetScale.Proportioned(widget, 1, down * 2, SizeLead.Height));
        });
    }

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
