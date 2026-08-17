using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aquila.Models;

/// <summary>
/// A colour profile: the palette applied to widgets, cards and charts.
///
/// Deliberately NOT the app theme. A theme dresses the window — background, cards, chrome — and is ours
/// to ship. A profile dresses the DATA, is a single JSON file, and is meant to be copied, edited and
/// swapped by anyone.
///
/// The format has two layers, and one rule holds it together: <b>only <see cref="Palette"/> may contain
/// colour literals</b>. Every role names a STEP on the ramp, never a colour, so replacing the five
/// palette entries restyles the whole application at once. It is the same reason a Tailwind component
/// says "primary" and not "orange".
///
/// A profile does NOT say when each role applies — that 80 °C counts as "alert" is a property of the
/// metric, not of the palette (see <c>IntensityBrushConverter</c>). Keeping them apart means a shared
/// profile restyles someone's machine without silently changing what their numbers mean.
///
/// Every role has a sensible default, so the smallest valid profile really is five colours.
/// </summary>
public sealed class ColorProfile
{
    /// <summary>File name stem by convention, and the key stored in settings. A user profile with the
    /// same id as a built-in replaces it — that is how one of ours gets customised.</summary>
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Author { get; set; }
    public string? Description { get; set; }

    public ProfilePalette Palette { get; set; } = new();

    /// <summary>Role name to ramp step. A dictionary rather than fixed properties so adding a role later
    /// needs no format change, and an old profile keeps loading — it just takes the new role's default.</summary>
    public Dictionary<string, RoleValue> Roles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>True for profiles shipped inside the app. They can be duplicated but never overwritten,
    /// so a broken edit can always be walked back.</summary>
    [JsonIgnore]
    public bool IsBuiltIn { get; set; }

    [JsonIgnore]
    public string DisplayName => IsBuiltIn ? Name : $"{Name} (custom)";
}

/// <summary>
/// The ramp, cool to hot, in five steps. <see cref="Light"/> is optional: a profile that provides only
/// one ramp uses it for both themes. Requiring both would double the work for someone who just wants to
/// try a combination they generated online, which is precisely the case worth keeping easy.
/// </summary>
public sealed class ProfilePalette
{
    public List<string> Dark { get; set; } = [];
    public List<string> Light { get; set; } = [];
}

/// <summary>
/// What a role points at: a step, and optionally an opacity. Written as a bare number in the common case
/// (<c>"alert": 4</c>) or as an object when it needs to be see-through
/// (<c>"track": { "step": 3, "opacity": 0.13 }</c>).
///
/// The long form is accepted for every role, not just the ones that need it today — a format where one
/// key is special is a format that has to break the first time a second key needs the same thing.
/// </summary>
[JsonConverter(typeof(RoleValueJsonConverter))]
public sealed class RoleValue
{
    public int Step { get; set; } = 1;
    public double Opacity { get; set; } = 1;
}

public sealed class RoleValueJsonConverter : JsonConverter<RoleValue>
{
    public override RoleValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return new RoleValue { Step = reader.GetInt32() };

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("A role must be a step number, or an object with 'step' and 'opacity'.");

        var value = new RoleValue();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return value;

            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            var property = reader.GetString();
            reader.Read();

            if (string.Equals(property, "step", StringComparison.OrdinalIgnoreCase))
                value.Step = reader.GetInt32();
            else if (string.Equals(property, "opacity", StringComparison.OrdinalIgnoreCase))
                value.Opacity = reader.GetDouble();
            else
                reader.Skip();
        }

        throw new JsonException("Unterminated role object.");
    }

    /// <summary>Writes the short form whenever it says the same thing, so a profile duplicated for editing
    /// comes back looking like the ones we ship rather than a verbose expansion of them.</summary>
    public override void Write(Utf8JsonWriter writer, RoleValue value, JsonSerializerOptions options)
    {
        if (value.Opacity >= 1)
        {
            writer.WriteNumberValue(value.Step);
            return;
        }

        writer.WriteStartObject();
        writer.WriteNumber("step", value.Step);
        writer.WriteNumber("opacity", value.Opacity);
        writer.WriteEndObject();
    }
}
