using Pegasus.Core.PhotoCapture;

namespace Pegasus.Core.Tests.PhotoCapture;

public sealed class PhotoLinkTokenTests
{
    [Fact]
    public void ATokenIs43Base64UrlCharactersAndItsDigestIs64LowercaseHexCharacters()
    {
        var token = PhotoLinkToken.Create();

        Assert.Equal(PhotoLinkToken.TokenLength, token.Value.Length);
        Assert.True(PhotoLinkToken.HasTokenShape(token.Value));
        var digest = PhotoLinkToken.Digest(token);
        Assert.Equal(64, digest.Length);
        Assert.All(digest, character => Assert.True(char.IsAsciiDigit(character) || character is >= 'a' and <= 'f'));
        Assert.Equal(digest, PhotoLinkToken.Digest(token.Value));
    }

    [Fact]
    public void TwoTokensDiffer() =>
        Assert.NotEqual(PhotoLinkToken.Create().Value, PhotoLinkToken.Create().Value);

    [Fact]
    public void TheSecretNeverPrintsItself() =>
        Assert.Equal("[REDACTED]", PhotoLinkToken.Create().ToString());

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA+=")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA A")]
    public void AnythingButTheTokenShapeIsRefusedBeforeAnyLookup(string? presented) =>
        Assert.False(PhotoLinkToken.HasTokenShape(presented));

    [Fact]
    public void AContactSlugIsTwelveCrockfordCharacters()
    {
        var slug = PhotoLinkToken.CreateSlug();

        Assert.Equal(PhotoLinkToken.SlugLength, slug.Length);
        Assert.True(PhotoLinkToken.HasSlugShape(slug));
        Assert.DoesNotContain(slug, character => character is 'i' or 'l' or 'o' or 'u');
        Assert.Equal(slug, PhotoLinkToken.NormalizeSlug(" " + slug.ToUpperInvariant() + " "));
    }

    [Theory]
    [InlineData("website", true)]
    [InlineData("demo-accident-repair", true)]
    [InlineData("abc", false)]
    [InlineData("-leading", false)]
    [InlineData("trailing-", false)]
    [InlineData("has space", false)]
    [InlineData("Caps", false)]
    public void ASlugIsLowercaseLettersDigitsAndHyphens(string presented, bool expected) =>
        Assert.Equal(expected, PhotoLinkToken.HasSlugShape(presented));
}
