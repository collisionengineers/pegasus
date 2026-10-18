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

    [Fact]
    public void AConfidentReadContainedInALongerConfidentReadIsNotADistinctRegistration()
    {
        // AK, AK24 and AK24UYD are one plate read three times (operator,
        // 12 October 2026).
        Assert.Equal(
            ["AK24UYD"],
            VrmRecognitionProvisionalBar.DistinctAcceptedRegistrations(
                new[] { Read("AK", 0.9), Read("AK24", 0.85), Read("AK24UYD", 0.89), Read("AK24UYD", 0.95) }));
        // Only a confident longer read absorbs a shorter one.
        Assert.Equal(
            ["AK24"],
            VrmRecognitionProvisionalBar.DistinctAcceptedRegistrations(
                new[] { Read("AK24", 0.85), Read("AK24UYD", 0.5) }));
        // Two full plates that do not contain each other still conflict.
        Assert.Equal(
            ["AK24UYD", "BX69YLM"],
            VrmRecognitionProvisionalBar.DistinctAcceptedRegistrations(
                new[] { Read("AK24UYD", 0.89), Read("BX69YLM", 0.9) }));
    }

    [Fact]
    public void TheLargestAcceptedPlateIsPreferredWhenTwoRegistrationsAreRead()
    {
        // A third-party plate out-scores the subject vehicle's larger plate
        // (operator, 15 October 2026); a larger plate below the bar is never
        // chosen by its area.
        var thirdParty = Plate("ZZ99ZZZ", 0.97, width: 60, height: 14);
        var subject = Plate("AB12CDE", 0.86, width: 240, height: 52);
        var belowBar = Plate("XY34ZZZ", 0.6, width: 400, height: 90);
        Assert.Same(
            subject,
            VrmRecognitionProvisionalBar.OfferedCandidate([thirdParty, belowBar, subject]));
        // A plate without bounds has no area.
        var unbounded = new VrmPlateCandidate("ZZ99ZZZ", "ZZ99ZZZ", 0.97, null);
        var small = Plate("AB12CDE", 0.81, width: 20, height: 4);
        Assert.Same(small, VrmRecognitionProvisionalBar.OfferedCandidate([unbounded, small]));
    }

    [Fact]
    public void EqualAreasMissingBoundsAndReadsBelowTheBarFallToTheHighestConfidence()
    {
        var lessConfident = Plate("ZZ99ZZZ", 0.85, width: 120, height: 26);
        var moreConfident = Plate("AB12CDE", 0.9, width: 120, height: 26);
        Assert.Same(
            moreConfident,
            VrmRecognitionProvisionalBar.OfferedCandidate([lessConfident, moreConfident]));
        var unbounded = new VrmPlateCandidate("ZZ99ZZZ", "ZZ99ZZZ", 0.85, null);
        var unboundedMoreConfident = new VrmPlateCandidate("AB12CDE", "AB12CDE", 0.9, null);
        Assert.Same(
            unboundedMoreConfident,
            VrmRecognitionProvisionalBar.OfferedCandidate([unbounded, unboundedMoreConfident]));
        var largeBelowBar = Plate("ZZ99ZZZ", 0.6, width: 240, height: 52);
        var smallBelowBar = Plate("AB12CDE", 0.7, width: 60, height: 14);
        Assert.Same(
            smallBelowBar,
            VrmRecognitionProvisionalBar.OfferedCandidate([largeBelowBar, smallBelowBar]));
    }

    [Fact]
    public void PlatesOfOneRegistrationKeepTheHighestConfidenceRead()
    {
        var lone = Plate("AB12CDE", 0.9, width: 60, height: 14);
        Assert.Same(lone, VrmRecognitionProvisionalBar.OfferedCandidate([lone]));
        var largerRead = Plate("AB12CDE", 0.85, width: 240, height: 52);
        var moreConfidentRead = Plate("AB12CDE", 0.95, width: 60, height: 14);
        Assert.Same(
            moreConfidentRead,
            VrmRecognitionProvisionalBar.OfferedCandidate([largerRead, moreConfidentRead]));
        Assert.Null(VrmRecognitionProvisionalBar.OfferedCandidate([]));
    }

    [Fact]
    public void ATruncatedReadBesideItsFullReadIsNotAConflictSoConfidenceDecides()
    {
        var truncated = Plate("AK24", 0.9, width: 60, height: 14);
        var full = Plate("AK24UYD", 0.85, width: 240, height: 52);
        Assert.Same(truncated, VrmRecognitionProvisionalBar.OfferedCandidate([truncated, full]));
    }

    private static ImageIntakeGroupMemberRecognition Read(string registration, double confidence) =>
        new(Guid.NewGuid(), IsTerminal: true, VrmRecognitionOutcomeKind.Suggested, registration, confidence);

    private static VrmPlateCandidate Plate(string registration, double confidence, double width, double height) =>
        new(registration, registration, confidence, new VrmPlateBounds(10, 20, 10 + width, 20 + height));

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
