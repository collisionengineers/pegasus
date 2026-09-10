using Pegasus.Core.Documents;

namespace Pegasus.Core.Tests.Documents;

public sealed class ImageTagVocabularyTests
{
    [Fact]
    public void NormalizeCollapsesInteriorWhitespaceRunsToOneSpace()
    {
        Assert.Equal("Third party", ImageTagVocabulary.Normalize("Third  party"));
        Assert.Equal("Third party", ImageTagVocabulary.Normalize("Third\tparty"));
    }

    [Fact]
    public void NormalizeCollapsesNonBreakingSpaceWithOrdinarySpace()
    {
        // U+00A0 NO-BREAK SPACE looks identical to an ordinary space but is a
        // distinct code point that char.IsWhiteSpace still recognises.
        var value = "Third party";

        Assert.Equal("Third party", ImageTagVocabulary.Normalize(value));
    }

    [Fact]
    public void NormalizeTrimsLeadingAndTrailingWhitespaceAfterCollapsing()
    {
        Assert.Equal("Third party", ImageTagVocabulary.Normalize("  Third  party  "));
    }

    [Fact]
    public void NormalizeKeyOfADoubleSpacedVariantMatchesTheBuiltInKey()
    {
        var builtInKey = ImageTagVocabulary.NormalizeKey(ImageTagVocabulary.ThirdPartyName);
        var doubleSpacedKey = ImageTagVocabulary.NormalizeKey("Third  party");

        Assert.Equal(builtInKey, doubleSpacedKey);
    }

    [Fact]
    public void NormalizeKeyOfANonBreakingSpaceVariantMatchesTheBuiltInKey()
    {
        var builtInKey = ImageTagVocabulary.NormalizeKey(ImageTagVocabulary.ThirdPartyName);
        var nonBreakingSpaceKey = ImageTagVocabulary.NormalizeKey("Third party");

        Assert.Equal(builtInKey, nonBreakingSpaceKey);
    }

    [Fact]
    public void NormalizeKeyIsCaseInsensitiveOfTheCollapsedName()
    {
        Assert.Equal(
            ImageTagVocabulary.NormalizeKey("Third party"),
            ImageTagVocabulary.NormalizeKey("third  PARTY"));
    }
}
