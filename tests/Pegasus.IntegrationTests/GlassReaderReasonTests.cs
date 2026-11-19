using Pegasus.Infrastructure.Glass;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Issue 916: the export reader's reason reaches the host log as position
/// numbers, field names and short code values, and nothing the provider could
/// put in a value that names a vehicle, a token or an address.
/// </summary>
public sealed class GlassReaderReasonTests
{
    [Fact]
    public void APositionAndACodeValueAreKept()
    {
        var reason = GlassReaderReason.Of(
            "Position 7 carries the unknown repair kind 'Overhaul', so nothing was imported.", "AB12CDE");

        Assert.Equal("Position 7 carries the unknown repair kind 'Overhaul', so nothing was imported.", reason);
    }

    /// <summary>
    /// R1209: a position type or repair kind is Glass's own name and may hold
    /// spaces, so after those words a short name of letters, digits, spaces
    /// and underscores reaches the log; any other value there is still cut.
    /// </summary>
    [Theory]
    [InlineData("position type", "Text Line", "Text Line")]
    [InlineData("position type", "Free_Labour", "Free_Labour")]
    [InlineData("repair kind", "Old for New", "Old for New")]
    [InlineData("repair kind", "Old for New, see https://portal.test", "?")]
    [InlineData("repair kind", "0123456789 0123456789 0123456789 0123456789", "?")]
    [InlineData("position type", "", "?")]
    public void APositionTypeOrRepairKindNameIsKept(string field, string value, string expected)
    {
        var reason = GlassReaderReason.Of($"Position 3 carries the unknown {field} '{value}', so nothing was imported.", "AB12CDE");

        Assert.Equal($"Position 3 carries the unknown {field} '{expected}', so nothing was imported.", reason);
    }

    [Fact]
    public void ARegistrationWrittenAsAPositionTypeIsNeverKept()
    {
        var reason = GlassReaderReason.Of("Position 2 carries the unknown position type 'AB 12 CDE'.", "AB12 CDE");

        Assert.Equal("Position 2 carries the unknown position type '?'.", reason);
    }

    [Theory]
    [InlineData("https://portal.test/ere/ere_session/abc123")]
    [InlineData("a value with spaces")]
    [InlineData("0123456789012345678901234567890123456789X")]
    [InlineData("")]
    public void AQuotedValueThatIsNotAShortCodeIsReplaced(string value)
    {
        var reason = GlassReaderReason.Of($"The attachment is of type '{value}' and not PDF.", "AB12CDE");

        Assert.Equal("The attachment is of type '?' and not PDF.", reason);
    }

    [Theory]
    [InlineData("AB12CDE")]
    [InlineData("ab12cde")]
    [InlineData("AB12 CDE")]
    public void TheCaseRegistrationIsNeverKept(string stated)
    {
        var reason = GlassReaderReason.Of($"Position 2 carries the unknown position type '{stated}'.", "AB12 CDE");

        Assert.DoesNotContain("AB12", reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CDE", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OnlyPrintableAsciiIsKeptAndTheWholeIsBounded()
    {
        var reason = GlassReaderReason.Of("Position 1\r\nhas an unreadable price \u00e9 " + new string('x', 1000), "AB12CDE");

        Assert.All(reason, character => Assert.InRange(character, ' ', '~'));
        Assert.Equal(GlassReaderReason.MaximumLength, reason.Length);
    }
}
