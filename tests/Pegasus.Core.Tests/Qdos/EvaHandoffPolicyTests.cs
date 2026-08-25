using Pegasus.Core.Documents;
using Pegasus.Core.Eva;

namespace Pegasus.Core.Tests.Qdos;

/// <summary>
/// ENG-016 collapsed the hand-off into the export, and the stage/custody/
/// evidence gate that used to live in <c>EvaHandoffPolicy.Evaluate</c> went
/// with the act it gated. Image selection is what remains, and it is now the
/// only Core policy the export consults — so it is pinned here directly rather
/// than only through the architecture test's source grep.
/// </summary>
public sealed class EvaHandoffPolicyTests
{
    [Fact]
    public void OnlyCurrentConfirmedOwnVehiclePhotographsAreEligible()
    {
        var eligible = Candidate(ordinal: 2);

        Assert.Equal(
            [eligible.VersionId],
            EvaHandoffPolicy.SelectEligibleImages([eligible]).Select(item => item.VersionId));

        foreach (var refused in new[]
        {
            eligible with { SemanticRole = DocumentSemanticRole.Instruction },
            eligible with { CustodyConfirmed = false },
            eligible with { IsCurrent = false },
            eligible with { IsLogicallyRemoved = true },
            eligible with { IsThirdPartyVehicle = true },
            eligible with { MediaType = "application/pdf" }
        })
        {
            Assert.Empty(EvaHandoffPolicy.SelectEligibleImages([refused]));
        }
    }

    [Fact]
    public void EligibleImagesKeepTheirRecordedOrdinalOrder()
    {
        var third = Candidate(ordinal: 3);
        var first = Candidate(ordinal: 1);
        var second = Candidate(ordinal: 2, mediaType: "image/png");

        Assert.Equal(
            [1, 2, 3],
            EvaHandoffPolicy.SelectEligibleImages([third, first, second])
                .Select(item => item.Ordinal));
    }

    private static EvaHandoffImageCandidate Candidate(
        int ordinal,
        string mediaType = "image/jpeg") => new(
        OccurrenceId: Guid.NewGuid(),
        DocumentId: Guid.NewGuid(),
        VersionId: Guid.NewGuid(),
        Version: 1,
        FileName: $"{ordinal}_offside.jpg",
        MediaType: mediaType,
        ContentLength: 1024,
        Sha256: new string('a', 64),
        SemanticRole: DocumentSemanticRole.Image,
        Source: DocumentSource.Intake,
        SourceOccurrenceIdentity: $"intake:{ordinal}",
        CustodyConfirmed: true,
        IsCurrent: true,
        IsLogicallyRemoved: false,
        IsThirdPartyVehicle: false,
        Ordinal: ordinal);
}
