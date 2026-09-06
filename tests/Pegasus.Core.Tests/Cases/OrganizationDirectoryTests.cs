using Pegasus.Core.Cases;

namespace Pegasus.Core.Tests.Cases;

/// <summary>
/// EXT-18/S05: the shared local-suggestion matching rule every C directory
/// search source obeys — no fuzzy or geographic inference, a trimmed,
/// collapsed-whitespace, case-insensitive name prefix, or an uppercase,
/// whitespace-free postcode prefix, and the same internal 20-row cap
/// regardless of what a caller asks for.
/// </summary>
public sealed class OrganizationDirectoryTests
{
    [Theory]
    [InlineData("  Acme   Repairs  ", "ACME REPAIRS")]
    [InlineData("acme", "ACME")]
    [InlineData("Acme\tRepairs", "ACME REPAIRS")]
    public void NormalizeNamePrefixTrimsCollapsesAndUppercases(string input, string expected) =>
        Assert.Equal(expected, InspectionLocationMatchPolicy.NormalizeNamePrefix(input));

    [Theory]
    [InlineData(" ab12 cde ", "AB12CDE")]
    [InlineData("ab12cde", "AB12CDE")]
    public void NormalizePostcodePrefixRemovesWhitespaceAndUppercases(string input, string expected) =>
        Assert.Equal(expected, InspectionLocationMatchPolicy.NormalizePostcodePrefix(input));

    [Theory]
    [InlineData("", false)]
    [InlineData("A", false)]
    [InlineData("AB", true)]
    [InlineData("ABC", true)]
    public void MeetsMinimumLengthRequiresAtLeastTwoNormalizedCharacters(string prefix, bool expected) =>
        Assert.Equal(expected, InspectionLocationMatchPolicy.MeetsMinimumLength(prefix));

    [Theory]
    [InlineData(1, 1)]
    [InlineData(20, 20)]
    [InlineData(21, 20)]
    [InlineData(500, 20)]
    public void ClampLimitNeverExceedsTheInternalCapOfTwenty(int requested, int expected) =>
        Assert.Equal(expected, InspectionLocationMatchPolicy.ClampLimit(requested));

    [Fact]
    public void ClampLimitRespectsASmallerCallerLimit() =>
        Assert.Equal(5, InspectionLocationMatchPolicy.ClampLimit(5));
}
