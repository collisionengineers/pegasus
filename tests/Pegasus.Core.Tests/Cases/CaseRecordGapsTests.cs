using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Cases;

/// <summary>
/// Phase 5b's Case record gaps: Notes from client, the claim source named in
/// the history line, and the VIN the Vehicle section now edits.
/// </summary>
public sealed class CaseRecordGapsTests
{
    private static readonly ActionActor Staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

    [Fact]
    public void TheHistoryLineNamesTheClaimSourceOnceByTheSourceItNowIs()
    {
        var before = new CaseEditableData(
            ClaimSourceId: Guid.NewGuid(), ClaimSourceVersion: 1, ClaimSourceName: "Old Claims Ltd",
            ClaimSourceContactName: "A Handler");
        var after = before with
        {
            ClaimSourceId = Guid.NewGuid(),
            ClaimSourceVersion = 4,
            ClaimSourceName = "Acme Claims",
            ClaimSourceContactName = "B Handler",
            ClaimSourceNotes = "Photos to follow."
        };
        var none = new Dictionary<string, object?>();

        Assert.Equal(
            "Claim source: Acme Claims, Claim source notes",
            CaseWorkspaceChangeSummary.Describe(before, after, none, none, false, 0, null));
        Assert.Equal(
            "Claim source: none",
            CaseWorkspaceChangeSummary.Describe(
                before,
                before with
                {
                    ClaimSourceId = null, ClaimSourceVersion = null, ClaimSourceName = null, ClaimSourceContactName = null
                },
                none, none, false, 0, null));
    }

    [Theory]
    [InlineData("WVWZZZ1JZXW000001", "WVWZZZ1JZXW000001")]
    [InlineData("wvw zzz1j zxw 000001", "WVWZZZ1JZXW000001")]
    [InlineData("  1HGCM82633A004352 ", "1HGCM82633A004352")]
    public void AVinIsSeventeenIso3779CharactersCanonicalised(string raw, string expected)
    {
        Assert.Equal(expected, AssessmentPolicy.NormalizeWritableField(AssessmentVocabulary.VehicleVin, raw, Staff));
    }

    [Theory]
    [InlineData("WVWZZZ1JZXW00000")]
    [InlineData("WVWZZZ1JZXW0000011")]
    [InlineData("WVWZZZ1JZXW00000I")]
    [InlineData("OVWZZZ1JZXW000001")]
    [InlineData("QVWZZZ1JZXW000001")]
    [InlineData("WVWZZZ1JZXW-00001")]
    public void AVinOfTheWrongLengthOrWithIOQOrPunctuationIsRefused(string raw)
    {
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.NormalizeWritableField(AssessmentVocabulary.VehicleVin, raw, Staff));
    }

    [Fact]
    public void ABlankVinClearsItAndTheBodyTypeTakesAHundredCharacters()
    {
        Assert.Null(AssessmentPolicy.NormalizeWritableField(AssessmentVocabulary.VehicleVin, "   ", Staff));
        Assert.Equal(
            new string('b', 100),
            AssessmentPolicy.NormalizeWritableField(AssessmentVocabulary.VehicleBody, new string('b', 100), Staff));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AssessmentPolicy.NormalizeWritableField(AssessmentVocabulary.VehicleBody, new string('b', 101), Staff));
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.NormalizeWritableField(AssessmentVocabulary.VehicleType, "hatchback", Staff));
    }
}
