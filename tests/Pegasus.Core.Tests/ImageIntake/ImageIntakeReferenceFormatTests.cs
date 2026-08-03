using Pegasus.Core.ImageIntake;

namespace Pegasus.Core.Tests.ImageIntake;

public sealed class ImageIntakeReferenceFormatTests
{
    [Theory]
    [InlineData("AB12CDE", 1, "AB12CDE-01")]
    [InlineData("AB12CDE", 9, "AB12CDE-09")]
    [InlineData("AB12CDE", 99, "AB12CDE-99")]
    [InlineData("AB12CDE", 100, "AB12CDE-100")]
    [InlineData("AB12CDE", 1234, "AB12CDE-1234")]
    [InlineData("X1", 2, "X1-02")]
    public void FormatsTwoDigitMinimumAndExpandsPastNinetyNine(
        string registration,
        int sequence,
        string expected) =>
        Assert.Equal(expected, ImageIntakeReferenceFormat.Create(registration, sequence));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveSequences(int sequence) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ImageIntakeReferenceFormat.Create("AB12CDE", sequence));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsBlankRegistrations(string registration) =>
        Assert.Throws<ArgumentException>(
            () => ImageIntakeReferenceFormat.Create(registration, 1));
}
