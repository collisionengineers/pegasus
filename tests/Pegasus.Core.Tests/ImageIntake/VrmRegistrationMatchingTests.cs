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

    /// <summary>
    /// A store bounds its Case read with the candidate forms, so every
    /// registration the rule accepts must be a form or a form with one
    /// character added. Checked against every registration one edit away from
    /// each read and from each of its forms.
    /// </summary>
    [Theory]
    [InlineData("BX69YLM")]
    [InlineData("BX69YL")]
    [InlineData("PK201YHR")]
    [InlineData("AB121CDE")]
    [InlineData("11111111")]
    [InlineData("PK2Y01HR")]
    [InlineData("A")]
    [InlineData("")]
    public void EveryAcceptedRegistrationIsACandidateFormOrOneCharacterLonger(string read)
    {
        var forms = VrmRegistrationMatching.CandidateForms(read);
        var accepted = forms.Prepend(read)
            .SelectMany(OneEditAway)
            .Distinct(StringComparer.Ordinal)
            .Where(confirmed => VrmRegistrationMatching.IsMatch(read, confirmed))
            .ToArray();

        Assert.NotEmpty(accepted);
        Assert.All(accepted, confirmed => Assert.Contains(forms, form =>
            form == confirmed
            || (confirmed.Length == form.Length + 1
                && Enumerable.Range(0, confirmed.Length).Any(index => confirmed.Remove(index, 1) == form))));
    }

    [Fact]
    public void AReadWithAFifthPositionOneHasTheReadWithoutItAsASecondForm()
    {
        Assert.Equal(["PK201YHR", "PK20YHR"], VrmRegistrationMatching.CandidateForms("PK201YHR"));
        Assert.Equal(["BX69YLM"], VrmRegistrationMatching.CandidateForms("BX69YLM"));
        Assert.Equal(["PK2Y01HR"], VrmRegistrationMatching.CandidateForms("PK2Y01HR"));
    }

    private static IEnumerable<string> OneEditAway(string value)
    {
        const string alphabet = "1AXZ";
        yield return value;
        for (var index = 0; index <= value.Length; index++)
        {
            foreach (var character in alphabet)
            {
                yield return value.Insert(index, new string(character, 1));
            }
            if (index < value.Length)
            {
                yield return value.Remove(index, 1);
                foreach (var character in alphabet)
                {
                    yield return value.Remove(index, 1).Insert(index, new string(character, 1));
                }
            }
        }
    }

    [Theory]
    [InlineData("PK201YHR", "PK20YHR", true)]
    [InlineData("AB121CDE", "AB12CDE", true)]
    [InlineData("PK201YH", "PK20YHR", false)]
    [InlineData("PK201HR", "PK20YHR", false)]
    [InlineData("PK2Y01HR", "PK2Y0HR", false)]
    [InlineData("PK201YHRX", "PK20YHRX", false)]
    public void AnEightCharacterReadWithAFifthPositionOneRetriesWithoutIt(
        string read,
        string confirmed,
        bool expected) =>
        Assert.Equal(expected, VrmRegistrationMatching.IsMatch(read, confirmed));
}
