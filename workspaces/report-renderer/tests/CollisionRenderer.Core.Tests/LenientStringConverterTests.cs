using System.Text.Json;
using CollisionRenderer.Core;
using CollisionRenderer.Core.Models;
using Xunit;

namespace CollisionRenderer.Core.Tests;

/// <summary>
/// Locks the regression fix for numeric money/mileage: the string-typed document models must
/// accept a bare JSON number (guide_value: 26417) as well as a formatted string, so the renderer
/// honours the published number|string valuation contract. Before the LenientStringConverter,
/// System.Text.Json threw "The JSON value could not be converted to System.String".
/// </summary>
public class LenientStringConverterTests
{
    [Fact]
    public void Numeric_money_and_mileage_bind_to_string_fields()
    {
        // camelCase keys = the shape the payload mapper hands to DocumentRenderer.Deserialize.
        const string json = """
        {
          "guideValue": 26417,
          "assessedRetailValue": 26750,
          "subject": { "mileage": 17471 },
          "adverts": [ { "price": 26495, "mileage": 5600, "registrationYear": 2024 } ]
        }
        """;

        var doc = JsonSerializer.Deserialize<MarketValuationEvidenceDocument>(json, CrJson.Options);

        Assert.NotNull(doc);
        Assert.Equal("26417", doc!.GuideValue);
        Assert.Equal("26750", doc.AssessedRetailValue);
        Assert.Equal("17471", doc.Subject.Mileage);
        Assert.Equal("26495", doc.Adverts[0].Price);
        Assert.Equal("5600", doc.Adverts[0].Mileage);
        Assert.Equal("2024", doc.Adverts[0].RegistrationYear);
    }

    [Fact]
    public void Formatted_strings_still_pass_through_unchanged()
    {
        const string json = """
        { "guideValue": "£23,900", "assessedRetailValue": "24750", "subject": { "mileage": "62,000 miles" } }
        """;

        var doc = JsonSerializer.Deserialize<MarketValuationEvidenceDocument>(json, CrJson.Options);

        Assert.Equal("£23,900", doc!.GuideValue);
        Assert.Equal("24750", doc.AssessedRetailValue);
        Assert.Equal("62,000 miles", doc.Subject.Mileage);
    }

    [Fact]
    public void Coerces_number_bool_and_null_and_preserves_strings()
    {
        const string json = """{ "a": "hi", "b": 42, "c": true, "d": false, "e": null }""";

        var box = JsonSerializer.Deserialize<Box>(json, CrJson.Options);

        Assert.Equal("hi", box!.A);
        Assert.Equal("42", box.B);
        Assert.Equal("true", box.C);
        Assert.Equal("false", box.D);
        Assert.Null(box.E);
    }

    [Fact]
    public void Large_integer_mileage_keeps_exact_text_without_float_artifacts()
    {
        var doc = JsonSerializer.Deserialize<MarketValuationEvidenceDocument>(
            """{ "guideValue": 1234567 }""", CrJson.Options);

        Assert.Equal("1234567", doc!.GuideValue);
    }

    private sealed record Box(string? A, string? B, string? C, string? D, string? E);
}
