using System;
using System.Text.Json;
using CollisionRenderer.Core.Design;
using CollisionRenderer.Core.Models;
using CollisionRenderer.Core.Templating;
using Xunit;

namespace CollisionRenderer.Core.Tests;

/// <summary>
/// A <see cref="TimeProvider"/> frozen at one instant, so a document date that falls
/// back to the ambient clock becomes assertable.
/// </summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;
}

/// <summary>
/// Locks the document-date seam. <c>Format.Today</c> previously read
/// <c>DateTime.Now</c>: machine-local and untestable, so the same payload rendered in
/// a UTC container and on a UK desktop during BST could produce different dates near
/// midnight. The date is now the Europe/London conversion of an injected clock.
/// </summary>
public class DocumentDateSeamTests
{
    // 23:30 UTC on 15 June is 00:30 BST on 16 June. A UTC-clock renderer would print
    // the 15th; the UK business date is the 16th. This is the case the old code got
    // wrong, and it is wrong in the direction that matters — a report dated a day early.
    [Fact]
    public void Bst_midnight_resolves_to_the_next_uk_business_date()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 6, 15, 23, 30, 0, TimeSpan.Zero));

        Assert.Equal("16/06/2026", Format.Today(clock));
    }

    [Fact]
    public void Bst_just_before_midnight_stays_on_the_same_uk_business_date()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 6, 15, 22, 30, 0, TimeSpan.Zero));

        Assert.Equal("15/06/2026", Format.Today(clock));
    }

    // In winter the UK is on GMT, so there is no shift. Proves the conversion is a real
    // zone conversion and not a blanket +1 hour.
    [Fact]
    public void Gmt_midnight_does_not_shift_the_uk_business_date()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 1, 15, 23, 30, 0, TimeSpan.Zero));

        Assert.Equal("15/01/2026", Format.Today(clock));
    }

    // The clock's own offset must not leak into the answer: the same instant expressed
    // in a different offset is still the same instant, and so the same UK date.
    [Fact]
    public void Clock_offset_does_not_change_the_resolved_date()
    {
        var utc = new FixedTimeProvider(new DateTimeOffset(2026, 6, 15, 23, 30, 0, TimeSpan.Zero));
        var elsewhere = new FixedTimeProvider(new DateTimeOffset(2026, 6, 16, 8, 30, 0, TimeSpan.FromHours(9)));

        Assert.Equal(Format.Today(utc), Format.Today(elsewhere));
    }

    [Fact]
    public void Blank_payload_date_falls_back_to_the_injected_clock()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 6, 15, 23, 30, 0, TimeSpan.Zero));

        var html = Compose(WithDate(null), clock);

        Assert.Contains("16/06/2026", html);
    }

    [Fact]
    public void Whitespace_payload_date_falls_back_to_the_injected_clock()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 6, 15, 23, 30, 0, TimeSpan.Zero));

        var html = Compose(WithDate("   "), clock);

        Assert.Contains("16/06/2026", html);
    }

    // The ambient clock is a fallback, never an override.
    [Fact]
    public void Caller_supplied_date_wins_over_the_ambient_clock()
    {
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 6, 15, 23, 30, 0, TimeSpan.Zero));

        var html = Compose(WithDate("01/01/2020"), clock);

        Assert.Contains("01/01/2020", html);
        Assert.DoesNotContain("16/06/2026", html);
    }

    private static FeeNoteDocument WithDate(string? date)
    {
        var model = JsonSerializer.Deserialize<FeeNoteDocument>(
            AuthoringTemplateCatalog.Default.GetStarterJson("fee-note"), CrJson.Options)!;
        return model with { Meta = model.Meta with { Date = date } };
    }

    private static string Compose(FeeNoteDocument model, TimeProvider clock)
    {
        var composer = new HtmlComposer(BrandAssets.Default, TemplateCatalog.Default, clock);
        return composer.Compose(TemplateCatalog.Default.Get("fee-note"), model, Density.Normal).Html;
    }
}

/// <summary>
/// Golden-string assertions for the <c>en-GB</c> pin. Hard-coding the culture is what
/// makes money and mileage output independent of the host's locale, but the pin still
/// resolves against the runtime's ICU — which changes between .NET releases and between
/// container base images. These assertions make such a shift fail a test instead of
/// silently changing a document.
///
/// They also fail loudly if globalization-invariant mode is ever switched on, because
/// <c>CultureInfo.GetCultureInfo("en-GB")</c> throws under
/// <c>InvariantGlobalization=true</c> with the default <c>PredefinedCulturesOnly</c>.
/// </summary>
public class FormatGoldenStringTests
{
    [Theory]
    [InlineData("1234.5", "£1,234.50")]
    [InlineData("12345.67", "£12,345.67")]
    [InlineData("1000", "£1,000.00")]
    [InlineData("0", "£0.00")]
    [InlineData("£12,345.67", "£12,345.67")]
    [InlineData("GBP 1000", "£1,000.00")]
    [InlineData("gbp1234.5", "£1,234.50")]
    public void Money_renders_the_en_gb_golden_form(string input, string expected)
    {
        Assert.Equal(expected, Format.Money(input));
    }

    [Fact]
    public void Money_without_decimals_renders_the_en_gb_golden_form()
    {
        Assert.Equal("£1,235", Format.Money("1234.5", decimals: false));
        Assert.Equal("£1,000", Format.Money("1000", decimals: false));
    }

    [Fact]
    public void Money_from_a_decimal_renders_the_en_gb_golden_form()
    {
        Assert.Equal("£1,234.50", Format.Money(1234.5m));
        Assert.Equal("-£1,234.50", Format.Money(-1234.5m));
        Assert.Equal("£1,235", Format.Money(1234.5m, decimals: false));
    }

    [Fact]
    public void Unparseable_money_is_returned_encoded_rather_than_reformatted()
    {
        Assert.Equal("on application", Format.Money("on application"));
        Assert.Equal(string.Empty, Format.Money(null));
        Assert.Equal(string.Empty, Format.Money("   "));
    }

    [Fact]
    public void Optional_money_omits_blanks_and_formats_the_rest()
    {
        Assert.Null(Format.OptionalMoney(null));
        Assert.Null(Format.OptionalMoney("  "));
        Assert.Equal("£1,000.00", Format.OptionalMoney("1000"));
    }

    [Fact]
    public void Mileage_uses_the_en_gb_group_separator()
    {
        Assert.Equal("45,000", Format.Mileage("45000"));
        Assert.Equal("1,234,567", Format.Mileage("1234567"));
        Assert.Equal(string.Empty, Format.Mileage(null));
    }

    [Fact]
    public void Subject_mileage_appends_the_unit_to_a_grouped_number()
    {
        Assert.Equal("45,000 miles", Format.SubjectMileage("45000"));
        Assert.Equal("Not stated", Format.SubjectMileage(null));
    }
}
