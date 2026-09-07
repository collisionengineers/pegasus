using Pegasus.Core.Triage;

namespace Pegasus.Core.Tests.Triage;

public sealed class TriageReferenceFormatTests
{
    [Theory]
    [InlineData(1, "T-00001")]
    [InlineData(2, "T-00002")]
    [InlineData(42, "T-00042")]
    [InlineData(99_999, "T-99999")]
    [InlineData(100_000, "T-100000")]
    [InlineData(1_234_567, "T-1234567")]
    public void FormatsFiveDigitMinimumAndExpandsPastNinetyNineThousand(
        long sequence,
        string expected) =>
        Assert.Equal(expected, TriageReferenceFormat.Format(sequence));

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void RejectsNonPositiveSequences(long sequence) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TriageReferenceFormat.Format(sequence));

    [Theory]
    [InlineData("T-00001", 1L)]
    [InlineData("T-00042", 42L)]
    [InlineData("T-100000", 100_000L)]
    [InlineData("  T-00007  ", 7L)]
    public void ParsesItsOwnOutput(string value, long expected)
    {
        Assert.True(TriageReferenceFormat.TryParse(value, out var sequence));
        Assert.Equal(expected, sequence);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("T-0001")]
    [InlineData("T00001")]
    [InlineData("t-00001")]
    [InlineData("T-00000")]
    [InlineData("U1")]
    [InlineData("AB12CDE-01")]
    [InlineData("T-0000a")]
    public void RejectsAnythingThatIsNotATriageReference(string? value)
    {
        Assert.False(TriageReferenceFormat.TryParse(value, out var sequence));
        Assert.Equal(0, sequence);
    }
}
