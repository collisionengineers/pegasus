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
    [Fact]
    public void NotesFromClientKeepTheirParagraphsWithinFourThousandCharacters()
    {
        var normalized = CaseDataPolicy.Normalize(new CaseEditableData(
            ClientNotes: "  The claimant says the   car was parked.\r\n\r\n\r\nPhotos to follow.  "));

        Assert.Equal("The claimant says the car was parked.\n\nPhotos to follow.", normalized.ClientNotes);
        Assert.Equal(4000, CaseDataPolicy.Normalize(new CaseEditableData(ClientNotes: new string('x', 4000))).ClientNotes!.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CaseDataPolicy.Normalize(new CaseEditableData(ClientNotes: new string('x', 4001))));
        Assert.Null(CaseDataPolicy.Normalize(new CaseEditableData(ClientNotes: "   ")).ClientNotes);
    }

    [Fact]
    public void TheOverviewSectionCarriesNotesFromClientOntoTheCase()
    {
        var request = new SaveCaseWorkspaceRequest(
            Guid.NewGuid(), 3, ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), "op-client-notes", null, new string('a', 32))
        {
            Overview = new CaseWorkspaceOverview(
                "A Claimant", null, null, null, null, null, null, null, null, null, null, null, null,
                ClientNotes: "Collected from the depot on Monday.")
        };

        var merged = CaseWorkspacePolicy.Overlay(new CaseEditableData(ClientNotes: "Old note"), request);

        Assert.Equal("Collected from the depot on Monday.", merged.ClientNotes);
    }

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
            ClientNotes = "Photos to follow."
        };
        var none = new Dictionary<string, object?>();

        Assert.Equal(
            "Claim source: Acme Claims, Client notes",
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
        Assert.Equal(expected, AssessmentPolicy.NormalizeWritableField(AssessmentVocabulary.VehicleVin, raw));
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
            AssessmentPolicy.NormalizeWritableField(AssessmentVocabulary.VehicleVin, raw));
    }

    [Fact]
    public void ABlankVinClearsItAndTheBodyTypeTakesAHundredCharacters()
    {
        Assert.Null(AssessmentPolicy.NormalizeWritableField(AssessmentVocabulary.VehicleVin, "   "));
        Assert.Equal(
            new string('b', 100),
            AssessmentPolicy.NormalizeWritableField(AssessmentVocabulary.VehicleBody, new string('b', 100)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AssessmentPolicy.NormalizeWritableField(AssessmentVocabulary.VehicleBody, new string('b', 101)));
        Assert.Throws<ArgumentException>(() =>
            AssessmentPolicy.NormalizeWritableField(AssessmentVocabulary.VehicleType, "hatchback"));
    }
}
