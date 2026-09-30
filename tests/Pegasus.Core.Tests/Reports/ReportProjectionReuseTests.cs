using Pegasus.Core.Cases;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Reports;

/// <summary>
/// A page hands the report snapshot source the Case's works its frame read, so
/// the source does not read them again. Only Create audit adds a work and it
/// moves the version, so the works count only at the version the source reads.
/// </summary>
public sealed class ReportProjectionReuseTests
{
    [Fact]
    public void TheFramesWorksAreReusedOnlyAtTheVersionTheSourceRead()
    {
        var caseId = Guid.NewGuid();
        var works = Works(caseId);
        var reuse = new ReportProjectionReuse(CaseWorkSelector.Current, Frame: Frame(caseId, caseId, version: 4, works));

        Assert.Same(works, reuse.WorksFor(caseId, 4));
        Assert.Null(reuse.WorksFor(caseId, 5));
        Assert.Null(new ReportProjectionReuse(CaseWorkSelector.Current).WorksFor(caseId, 4));
        Assert.Null(new ReportProjectionReuse(CaseWorkSelector.Current, Frame: Frame(caseId, caseId, 4, works: null))
            .WorksFor(caseId, 4));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void AFrameOfAnotherCaseIsRefused(bool summaryMatches, bool workflowMatches)
    {
        var caseId = Guid.NewGuid();
        var otherCaseId = Guid.NewGuid();
        var reuse = new ReportProjectionReuse(
            CaseWorkSelector.Current,
            Frame: Frame(
                summaryMatches ? caseId : otherCaseId,
                workflowMatches ? caseId : otherCaseId,
                version: 4,
                Works(caseId)));

        Assert.Throws<ArgumentException>(() => reuse.WorksFor(caseId, 4));
    }

    private static CaseWorkSet Works(Guid caseId) =>
        new(new CaseWork(caseId, caseId, CaseWorkKind.Primary, DateTimeOffset.UnixEpoch), Audit: null);

    private static CaseSectionFrame Frame(Guid summaryCaseId, Guid workflowCaseId, long version, CaseWorkSet? works)
    {
        var identity = new CaseIdentity(workflowCaseId, "QDOS", 2031, 1, "QDOS3100001");
        return new(
            new(
                summaryCaseId, identity.Reference, null, CaseType.Inspection, "QDOS",
                CaseLifecycleState.ReportPreparation, null, null, null, null,
                DateTimeOffset.UnixEpoch, "Email", DateTimeOffset.UnixEpoch),
            new(workflowCaseId, identity, CaseLifecycleState.ReportPreparation, null, null, null, null, null, null, null, version),
            null,
            "case-root",
            CaseCustodyState.Confirmed,
            works);
    }
}
