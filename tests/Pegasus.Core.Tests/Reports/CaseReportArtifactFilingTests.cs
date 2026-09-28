using System.Text.Json;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

/// <summary>
/// Where a generated document's file stands is read from the artifact row
/// alone, so the Case page and every other reader say the same thing about it.
/// </summary>
public sealed class CaseReportArtifactFilingTests
{
    private static readonly Guid GenerationId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    [Theory]
    [InlineData(CaseReportArtifactStatus.Confirmed, true, CaseReportArtifactFiling.Stored)]
    [InlineData(CaseReportArtifactStatus.Pending, true, CaseReportArtifactFiling.BeingStored)]
    [InlineData(CaseReportArtifactStatus.Pending, false, CaseReportArtifactFiling.NotProduced)]
    [InlineData(CaseReportArtifactStatus.Failed, true, CaseReportArtifactFiling.StorageFailed)]
    [InlineData(CaseReportArtifactStatus.Failed, false, CaseReportArtifactFiling.StorageFailed)]
    [InlineData(CaseReportArtifactStatus.Unknown, true, CaseReportArtifactFiling.Unconfirmed)]
    [InlineData(CaseReportArtifactStatus.Unknown, false, CaseReportArtifactFiling.Unconfirmed)]
    public void TheFilingIsReadFromTheStatusAndWhetherAFileExists(
        CaseReportArtifactStatus status, bool hasVersion, CaseReportArtifactFiling expected) =>
        Assert.Equal(expected, Artifact(status, hasVersion).Filing);

    /// <summary>
    /// A row frozen for a render that never finished holds no version. It is
    /// not a file on its way to Box, whatever else the row carries.
    /// </summary>
    [Fact]
    public void APendingRowWithoutAVersionWasNeverProduced()
    {
        var frozen = Artifact(CaseReportArtifactStatus.Pending, hasVersion: false);

        Assert.Equal(CaseReportArtifactFiling.NotProduced, frozen.Filing);
        Assert.Equal(
            CaseReportArtifactFiling.BeingStored,
            (frozen with { VersionId = Guid.NewGuid() }).Filing);
    }

    /// <summary>Every status the row can hold has a filing of its own meaning.</summary>
    [Fact]
    public void EveryStatusHasAFiling()
    {
        foreach (var status in Enum.GetValues<CaseReportArtifactStatus>())
        {
            Assert.True(Enum.IsDefined(Artifact(status, hasVersion: true).Filing));
        }
    }

    /// <summary>The filing is worked out, never stored: it is no part of the row when written as JSON.</summary>
    [Fact]
    public void TheFilingIsNotWrittenWithTheRow()
    {
        var json = JsonSerializer.Serialize(Artifact(CaseReportArtifactStatus.Confirmed, hasVersion: true));

        Assert.DoesNotContain(nameof(CaseReportArtifactRecord.Filing), json, StringComparison.Ordinal);
    }

    private static CaseReportArtifactRecord Artifact(CaseReportArtifactStatus status, bool hasVersion) => new(
        Guid.NewGuid(), GenerationId, CaseReportArtifactKind.AssessmentReport, status, "operation-1",
        hasVersion ? Guid.NewGuid() : null,
        hasVersion ? Guid.NewGuid() : null,
        hasVersion ? new string('c', 64) : null,
        hasVersion ? 3 : null,
        hasVersion ? "CE_100_assessment.pdf" : null,
        hasVersion ? "application/pdf" : null,
        null, null,
        status == CaseReportArtifactStatus.Pending && hasVersion ? "pending/ce-100" : null,
        status == CaseReportArtifactStatus.Failed ? "transient_failure" : null);
}
