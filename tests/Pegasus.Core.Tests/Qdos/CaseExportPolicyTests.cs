using Pegasus.Core.Documents;
using Pegasus.Core.CaseExport;

namespace Pegasus.Core.Tests.Qdos;

/// <summary>
/// Image selection is the only Core policy the case export consults, so it
/// is pinned here directly rather than only through the architecture test's
/// source grep.
/// </summary>
public sealed class CaseExportPolicyTests
{
    [Fact]
    public void OnlyCurrentConfirmedOwnVehiclePhotographsAreEligible()
    {
        var eligible = Candidate(ordinal: 2);

        Assert.Equal(
            [eligible.VersionId],
            CaseExportPolicy.SelectEligibleImages([eligible]).Select(item => item.VersionId));

        foreach (var refused in new[]
        {
            eligible with { SemanticRole = DocumentSemanticRole.Instruction },
            eligible with { CustodyConfirmed = false },
            eligible with { IsCurrent = false },
            eligible with { IsLogicallyRemoved = true },
            eligible with { TagIds = [ImageTagVocabulary.ThirdPartyId] },
            eligible with { MediaType = "application/pdf" }
        })
        {
            Assert.Empty(CaseExportPolicy.SelectEligibleImages([refused]));
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
            CaseExportPolicy.SelectEligibleImages([third, first, second])
                .Select(item => item.Ordinal));
    }

    private static CaseExportImageCandidate Candidate(
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
        TagIds: [],
        Ordinal: ordinal);
}
