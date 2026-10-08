using System.Text.Json.Nodes;
using FsCheck;
using FsCheck.Fluent;

namespace Aquila.Tests;

/// <summary>
/// Random preset files — sound ones, broken ones and hostile ones.
///
/// Random TEXT would be thrown out by the JSON reader on its first character and never reach the code worth
/// testing, so the generator speaks the format: the real field names, at the real depths, holding values of every
/// JSON type — right, wrong, empty, huge, negative, null. That is what a file from somebody else's machine can
/// look like, and preset import is the one door through which Aquila reads something it did not write.
/// </summary>
internal static class PresetFiles
{
    private static readonly string[] Fields =
    [
        "format", "id", "name", "author", "description", "ramps", "background", "border", "gauge", "line", "bar",
        "number", "title", "value", "normal", "elevated", "alert", "critical", "color", "opacity", "thickness",
        "cornerRadius", "padding", "fontFamily", "size", "weight", "align", "placement", "corner", "sweep", "track",
        "fill", "fillOpacity", "smoothness", "pointSize", "layout", "unitSize", "panel",
    ];

    // Ramp names, with the casing traps: the reader matches property names case-insensitively, so "primary" and
    // "Primary" can both arrive in one file.
    private static readonly string[] RampNames = ["primary", "Primary", "PRIMARY", "secondary", "accent", "", " "];

    private static readonly string[] Words =
    [
        "#FFF", "#FF6B35", "#80FF6B35", "#ZZZZZZ", "#", "red", "", " ", "Dial", "Arc", "Top", "Bottom", "Bold",
        "Light", "Center", "Beside", "Above", "primary", "Primary", "null", "∞", "🔥", "\u0000",
    ];

    private static Gen<JsonNode?> Scalar() =>
        Gen.OneOf(
            Gen.Constant<JsonNode?>(null),
            ArbMap.Default.GeneratorFor<bool>().Select(b => (JsonNode?)JsonValue.Create(b)),
            ArbMap.Default.GeneratorFor<int>().Select(i => (JsonNode?)JsonValue.Create(i)),
            Gen.Elements(-1e9, -1.0, 0.0, 0.5, 1.0, 255.0, 1e9, double.MaxValue)
                .Select(d => (JsonNode?)JsonValue.Create(d)),
            Gen.Elements(Words).Select(s => (JsonNode?)JsonValue.Create(s)),
            ArbMap.Default.GeneratorFor<string>().Select(s => (JsonNode?)JsonValue.Create(s)));

    private static Gen<JsonNode?> Node(int depth) =>
        depth <= 0
            ? Scalar()
            : Gen.Frequency(
                (5, Scalar()),
                (3, Object(Gen.Elements(Fields), depth - 1)),
                (1, Node(depth - 1).ListOf().Select(items => (JsonNode?)new JsonArray([.. items.Select(Copy)]))));

    private static Gen<JsonNode?> Object(Gen<string> keys, int depth) =>
        (from key in keys
         from value in Node(depth)
         select (key, value))
        .ListOf()
        .Select(pairs => (JsonNode?)Build(pairs));

    private static JsonObject Build(IEnumerable<(string Key, JsonNode? Value)> pairs)
    {
        var o = new JsonObject();
        foreach (var (key, value) in pairs) o[key] = Copy(value);   // a repeated key keeps the last, as a file would
        return o;
    }

    // A JsonNode belongs to one parent, and a generated value can be offered twice.
    private static JsonNode? Copy(JsonNode? node) => node?.DeepClone();

    /// <summary>A whole file. "ramps" is generated on purpose rather than left to chance, since it is where the
    /// repairs happen; everything else is any of the format's fields holding anything.</summary>
    public static Arbitrary<string> Any() =>
        (from fields in Object(Gen.Elements(Fields), 3)
         from ramps in Object(Gen.Elements(RampNames), 2)
         from withRamps in ArbMap.Default.GeneratorFor<bool>()
         select Render((JsonObject)fields!, withRamps ? ramps : null))
        .ToArbitrary();

    private static string Render(JsonObject fields, JsonNode? ramps)
    {
        if (ramps is not null) fields["ramps"] = Copy(ramps);
        return fields.ToJsonString();
    }
}
