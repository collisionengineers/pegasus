using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CollisionRenderer.Core;

/// <summary>
/// Reads a JSON <b>string</b> field tolerantly: a JSON number or boolean is accepted and
/// returned as its text, so a payload may send e.g. <c>guideValue: 26417</c> or
/// <c>price: 26495</c> as bare numbers rather than pre-formatted strings.
///
/// <para>The document models (<see cref="Models.MarketValuationEvidenceDocument"/>,
/// <see cref="Models.Advert"/>, <see cref="Models.SubjectVehicle"/>, …) type money/mileage as
/// <c>string</c> on purpose — to tolerate real-world values like <c>"£24,750"</c> or
/// <c>"62,000 miles"</c> and format on output. But <c>System.Text.Json</c> will not bind a JSON
/// <i>number</i> into a <c>string</c> property, so a numeric <c>guide_value</c> threw
/// <c>"The JSON value could not be converted to System.String"</c> before rendering. That made the
/// renderer stricter than the published valuation contract, which declares money/mileage as
/// <c>number | string</c>. This converter closes that gap: numbers and booleans coerce to their
/// text, genuine strings pass straight through, and presentation formatting still happens
/// downstream in <see cref="Format"/> (so <c>26417</c> renders as <c>£26,417</c>).</para>
///
/// <para>Writing is identical to the default (<c>WriteStringValue</c>), so serialization — the
/// authoring envelopes, CLI/GUI form output — is unchanged. Registered globally on
/// <see cref="CrJson.Options"/>/<see cref="CrJson.Relaxed"/>, it only diverges from default
/// behaviour for the exact number/bool tokens that currently throw; every other string is untouched.</para>
/// </summary>
public sealed class LenientStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();

            case JsonTokenType.Number:
                // Keep integer text exact — no float round-tripping (26417 -> "26417", not "26417.0");
                // fall back to decimal for the rare fractional value. Format.Money/Mileage normalise
                // on output either way.
                return reader.TryGetInt64(out var whole)
                    ? whole.ToString(CultureInfo.InvariantCulture)
                    : reader.GetDecimal().ToString(CultureInfo.InvariantCulture);

            case JsonTokenType.True:
                return "true";

            case JsonTokenType.False:
                return "false";

            case JsonTokenType.Null:
                return null;

            default:
                throw new JsonException(
                    $"Expected a string, number or boolean for a string field but found {reader.TokenType}.");
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        // Identical to the default string writer: numbers only ever arrive on the read path.
        writer.WriteStringValue(value);
    }

    // A custom JsonConverter<string> otherwise hijacks dictionary string KEYS (the base virtuals
    // throw NotSupportedException). Delegate both to default string-key behaviour so any
    // Dictionary<string, T> keeps (de)serializing exactly as before.
    public override string ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString()!;

    public override void WriteAsPropertyName(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value);
}
