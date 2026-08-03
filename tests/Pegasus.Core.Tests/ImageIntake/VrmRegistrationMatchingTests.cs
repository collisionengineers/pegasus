using Pegasus.Core.ImageIntake;

namespace Pegasus.Core.Tests.ImageIntake;

public sealed class VrmRegistrationMatchingTests
{
    [Theory]
    [InlineData("BX69YLM", "BX69YLM", true)]
    [InlineData("BX69YL", "BX69YLM", true)]
    [InlineData("X69YLM", "BX69YLM", true)]
    [InlineData("BX69LM", "BX69YLM", true)]
    [InlineData("PK20YHR", "PK201YHR", true)]
    [InlineData("KM26UWG", "KM26OWG", false)]
    [InlineData("BX69Y", "BX69YLM", false)]
    [InlineData("BX69YLMA", "BX69YLM", false)]
    [InlineData("AB12CDE", "XY34ZZZ", false)]
    [InlineData("", "A", false)]
    public void MatchesExactlyOrWithOneMissingCharacter(
        string read,
        string confirmed,
        bool expected) =>
        Assert.Equal(expected, VrmRegistrationMatching.IsMatch(read, confirmed));

    [Fact]
    public void OneMissingCharacterIsNeverASubstitution()
    {
        Assert.False(VrmRegistrationMatching.IsOneCharacterMissing("KM26UWG", "KM26OWG"));
        Assert.True(VrmRegistrationMatching.IsOneCharacterMissing("KM26WG", "KM26OWG"));
    }
}
