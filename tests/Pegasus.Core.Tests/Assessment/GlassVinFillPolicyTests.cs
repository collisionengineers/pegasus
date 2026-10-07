using Pegasus.Core.Assessment;

namespace Pegasus.Core.Tests.Assessment;

/// <summary>
/// Glass's VIN fills the Case's VIN only when the Case holds none, whoever
/// would have recorded one (operator, 7 October 2026).
/// </summary>
public sealed class GlassVinFillPolicyTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(" ", true)]
    [InlineData("TESTVEH0A1B2C3D45", false)]
    public void TheVinFillsOnlyAnEmptyVin(string? existing, bool fills) =>
        Assert.Equal(fills, GlassVinFillPolicy.Fills(existing));

    [Theory]
    [InlineData("TESTVEH0A1B2C3D45", "TESTVEH0A1B2C3D45")]
    [InlineData(" testveh0a1b2c3d45 ", "TESTVEH0A1B2C3D45")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("TESTVIN1234567890", null)]
    [InlineData("TOO-SHORT", null)]
    public void OnlyAVinTheVocabularyHoldsIsCarried(string? raw, string? carried) =>
        Assert.Equal(carried, GlassVinFillPolicy.Carried(raw));
}
