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
