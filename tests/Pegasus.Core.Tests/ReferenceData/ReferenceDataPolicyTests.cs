using Pegasus.Core.ReferenceData;

namespace Pegasus.Core.Tests.ReferenceData;

public sealed class PrincipalDomainPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("qdosassist.co.uk")]
    [InlineData("@@qdosassist.co.uk")]
    [InlineData("@QDOSASSIST.CO.UK")]
    [InlineData("@localhost")]
    [InlineData("@-invalid.example")]
    [InlineData("@invalid-.example")]
    [InlineData("@invalid..example")]
    [InlineData("@invalid_example.test")]
    [InlineData("@invalid.example ")]
    public void PrincipalDomainSuffixGrammarRejectsNoncanonicalValues(string? suffix)
    {
        Assert.False(ReferenceDataPolicy.IsCanonicalDomainSuffix(suffix));
    }

    [Fact]
    public void PrincipalDomainSuffixExtractionReturnsOnlyCanonicalLowercaseSuffix()
    {
        var extracted = ReferenceDataPolicy.TryExtractDomainSuffix(
            "  synthetic-local-001@EXAMPLE.INVALID  ",
            out var suffix);

        Assert.True(extracted);
        Assert.Equal("@example.invalid", suffix);
        Assert.DoesNotContain("synthetic-local-001", suffix, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("missing-separator")]
    [InlineData("@example.invalid")]
    [InlineData("synthetic-local-001@localhost")]
    public void PrincipalDomainSuffixExtractionRejectsInvalidInput(string? input)
    {
        Assert.False(ReferenceDataPolicy.TryExtractDomainSuffix(input, out var suffix));
        Assert.Null(suffix);
    }
}
