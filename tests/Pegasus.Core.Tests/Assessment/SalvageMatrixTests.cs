using Pegasus.Core.Assessment;

namespace Pegasus.Core.Tests.Assessment;

/// <summary>
/// The per-Principal salvage matrix (operator, 29 September 2026): bands of
/// Engineer's Value per salvage category, each with the percentage paid.
/// </summary>
public sealed class SalvageMatrixTests
{
    [Fact]
    public void TheCategoriesAreTheCasesOwnLessNotApplicable()
    {
        Assert.Equal("A,B,S,N", string.Join(',', SalvageMatrix.Categories));
    }

    [Fact]
    public void NoBandsIsNoMatrix()
    {
        Assert.Null(SalvageMatrix.Normalize(null));
        Assert.Null(SalvageMatrix.Normalize([]));
        Assert.Null(SalvageMatrix.FromEntries(
        [
            new("S", null, null, null),
            new("N", " ", "", "  ")
        ]));
    }

    [Fact]
    public void BandsSortByCategoryThenFrom()
    {
        var matrix = SalvageMatrix.Normalize(
        [
            Band("N", 1000.01m, 2500m, 4m),
            Band("S", 1000.01m, 2500m, 2m),
            Band("N", 0.01m, 1000m, 0m),
            Band("A", 0.01m, 9999999.99m, 0m)
        ])!;

        Assert.Equal(
            new[] { ("A", 0.01m), ("S", 1000.01m), ("N", 0.01m), ("N", 1000.01m) },
            matrix.Bands.Select(band => (band.Category, band.From)));
    }

    [Theory]
    [InlineData("N/A")]
    [InlineData("C")]
    [InlineData("s")]
    public void AnUnknownCategoryIsRefused(string category)
    {
        var refusal = Assert.Throws<SalvageMatrixException>(() =>
            SalvageMatrix.Normalize([Band(category, 0.01m, 1000m, 2m)]));

        Assert.Equal(SalvageMatrixRule.UnknownCategory, refusal.Rule);
    }

    [Fact]
    public void FromMayEqualToButNotExceedIt()
    {
        Assert.NotNull(SalvageMatrix.Normalize([Band("S", 1000m, 1000m, 2m)]));

        var refusal = Assert.Throws<SalvageMatrixException>(() =>
            SalvageMatrix.Normalize([Band("S", 2500m, 1000m, 2m)]));
        Assert.Equal(SalvageMatrixRule.FromAfterTo, refusal.Rule);
        Assert.Equal("S", refusal.Category);
    }

    [Theory]
    [InlineData(-0.01, 1000)]
    [InlineData(0.01, 1000.001)]
    public void AmountsAreNonNegativePoundsAndPence(double from, double to)
    {
        var refusal = Assert.Throws<SalvageMatrixException>(() =>
            SalvageMatrix.Normalize([Band("B", (decimal)from, (decimal)to, 1m)]));

        Assert.Equal(SalvageMatrixRule.InvalidAmount, refusal.Rule);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    [InlineData(2.345)]
    public void APercentageOutsideNoughtToAHundredOrPastTwoPlacesIsRefused(double percentage)
    {
        var refusal = Assert.Throws<SalvageMatrixException>(() =>
            SalvageMatrix.Normalize([Band("N", 0.01m, 1000m, (decimal)percentage)]));

        Assert.Equal(SalvageMatrixRule.InvalidPercentage, refusal.Rule);
    }

    [Fact]
    public void NoughtAndAHundredPercentAreAllowed()
    {
        Assert.NotNull(SalvageMatrix.Normalize(
        [
            Band("A", 0.01m, 1000m, 0m),
            Band("A", 1000.01m, 2000m, 100m)
        ]));
    }

    [Fact]
    public void BandsMeetingAtThePennyDoNotOverlap()
    {
        Assert.NotNull(SalvageMatrix.Normalize(
        [
            Band("S", 0.01m, 1000m, 0m),
            Band("S", 1000.01m, 2500m, 2m)
        ]));
    }

    [Fact]
    public void BandsSharingABoundOverlapAndNameBothBands()
    {
        var refusal = Assert.Throws<SalvageMatrixException>(() =>
            SalvageMatrix.Normalize(
            [
                Band("S", 1000.01m, 2500m, 2m),
                Band("S", 2000m, 5000m, 4m)
            ]));

        Assert.Equal(SalvageMatrixRule.Overlap, refusal.Rule);
        Assert.Equal("S", refusal.Category);
        Assert.Equal(Band("S", 1000.01m, 2500m, 2m), refusal.Band);
        Assert.Equal(Band("S", 2000m, 5000m, 4m), refusal.OtherBand);

        Assert.Equal(SalvageMatrixRule.Overlap, Assert.Throws<SalvageMatrixException>(() =>
            SalvageMatrix.Normalize(
            [
                Band("N", 0.01m, 1000m, 0m),
                Band("N", 1000m, 2500m, 4m)
            ])).Rule);
    }

    [Fact]
    public void TheSameRangeInTwoCategoriesAndGapsAreAllowed()
    {
        Assert.NotNull(SalvageMatrix.Normalize(
        [
            Band("S", 0.01m, 1000m, 0m),
            Band("N", 0.01m, 1000m, 0m),
            Band("N", 5000.01m, 7500m, 10m)
        ]));
    }

    [Fact]
    public void TypedRowsBecomeBandsAndBlankRowsAreIgnored()
    {
        var matrix = SalvageMatrix.FromEntries(
        [
            new("S", "0.01", "1000.00", "0"),
            new("S", null, null, null),
            new("S", " 1000.01 ", "2500", "2.00")
        ])!;

        Assert.Equal(
            new[] { Band("S", 0.01m, 1000m, 0m), Band("S", 1000.01m, 2500m, 2m) },
            matrix.Bands);
    }

    [Theory]
    [InlineData("0.01", "", "2", SalvageMatrixRule.Incomplete)]
    [InlineData("", "1000", "", SalvageMatrixRule.Incomplete)]
    [InlineData("-1", "1000", "2", SalvageMatrixRule.InvalidAmount)]
    [InlineData("0.01", "1,000", "2", SalvageMatrixRule.InvalidAmount)]
    [InlineData("0.01", "1000", "two", SalvageMatrixRule.InvalidPercentage)]
    public void ARowThatDoesNotReadIsRefusedUnderItsColumnsRule(
        string from, string to, string percentage, SalvageMatrixRule rule)
    {
        var refusal = Assert.Throws<SalvageMatrixException>(() =>
            SalvageMatrix.FromEntries([new("B", from, to, percentage)]));

        Assert.Equal(rule, refusal.Rule);
        Assert.Equal("B", refusal.Category);
    }

    [Theory]
    [InlineData("S", 1000.00, 0.00)]
    [InlineData("S", 1000.01, 20.00)]
    [InlineData("S", 2500.00, 50.00)]
    [InlineData("S", 25000.00, 4000.00)]
    [InlineData("N", 1000.00, 0.00)]
    public void TheBandHoldingTheValueGivesTheSalvageValue(string category, double value, double expected)
    {
        var matrix = Example();

        Assert.Equal((decimal)expected, matrix.SalvageValueFor(TotalLoss, category, (decimal)value));
    }

    [Fact]
    public void NoBandNoCategoryOrNoValueGivesNothing()
    {
        var matrix = Example();

        Assert.Null(matrix.SalvageValueFor(TotalLoss, "N", 5000m)); // the gap
        Assert.Null(matrix.SalvageValueFor(TotalLoss, "B", 5000m)); // no bands
        Assert.Null(matrix.SalvageValueFor(TotalLoss, "N/A", 5000m));
        Assert.Null(matrix.SalvageValueFor(TotalLoss, null, 5000m));
        Assert.Null(matrix.SalvageValueFor(TotalLoss, "S", null));
        Assert.Null(matrix.SalvageValueFor(TotalLoss, "S", 0m));
        Assert.Null(matrix.SalvageValueFor("repairable", "S", 2000m));
        Assert.Null(matrix.SalvageValueFor(null, "S", 2000m));
    }

    [Fact]
    public void AHalfPennyRoundsAwayFromZero()
    {
        var matrix = SalvageMatrix.Normalize(
        [
            Band("S", 0.01m, 5000m, 2m),
            Band("N", 0.01m, 5000m, 12.5m)
        ])!;

        Assert.Equal(24.69m, matrix.SalvageValueFor(TotalLoss, "S", 1234.25m));
        Assert.Equal(0.01m, matrix.SalvageValueFor(TotalLoss, "N", 0.10m));
    }

    [Fact]
    public void AValueFollowsTheMatrixWhenEmptyOrEqualToItsFigure()
    {
        var matrix = Example();

        Assert.True(matrix.Follows(TotalLoss, "S", 2000m, null));
        Assert.True(matrix.Follows(TotalLoss, "S", 2000m, 40m));
        Assert.False(matrix.Follows(TotalLoss, "S", 2000m, 45m));
        Assert.False(matrix.Follows(TotalLoss, "N", 5000m, 45m)); // no figure applies
        Assert.False(matrix.Follows("repairable", "S", 2000m, 40m)); // not a total loss
        Assert.True(matrix.Follows(null, null, null, null));
    }

    [Fact]
    public void MatricesWithTheSameBandsAreEqual()
    {
        Assert.Equal(Example(), Example());
        Assert.Equal(Example().GetHashCode(), Example().GetHashCode());
        Assert.NotEqual(Example(), SalvageMatrix.Normalize([Band("A", 0.01m, 1m, 0m)]));
    }

    private const string TotalLoss = "total_loss";

    private static SalvageMatrix Example() => SalvageMatrix.Normalize(
    [
        Band("S", 0.01m, 1000m, 0m),
        Band("S", 1000.01m, 2500m, 2m),
        Band("S", 2500.01m, 9999999.99m, 16m),
        Band("N", 0.01m, 1000m, 0m),
        Band("N", 7500.01m, 9999999.99m, 20m)
    ])!;

    private static SalvageMatrixBand Band(string category, decimal from, decimal to, decimal percentage) =>
        new(category, from, to, percentage);
}
