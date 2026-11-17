using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Reports;

/// <summary>Whether a Case's report may be generated or sent for the named work.</summary>
public enum CaseReportActAccess
{
    Open,

    /// <summary>The Case was not found, or its assessment cannot be opened in its state.</summary>
    NotOpen,
}

/// <summary>
/// What the Case page's Generate and the Automation Actor's report acts share
/// (ADR-0064, R1042): the reason each artifact's request records, the fee
/// note a report request goes on to make, and the Automation Actor's gate
/// for generating or sending a report. Each caller opens the generation's
/// custody batch itself (<see cref="IGenerateCaseReport.BeginBatch"/> is an
/// ambient scope that must be opened in the caller's own flow) and presents
/// each outcome its own way; a fee note that fails after its report
/// succeeded is the caller's to present (R771).
/// </summary>
public sealed class CaseReportActs(IGetAssessmentAccess getAssessmentAccess)
{
    private readonly IGetAssessmentAccess getAssessmentAccess =
        getAssessmentAccess ?? throw new ArgumentNullException(nameof(getAssessmentAccess));

    /// <summary>The reason one artifact's generation records.</summary>
    public static string ReasonFor(CaseReportArtifactKind kind) => kind switch
    {
        CaseReportArtifactKind.AssessmentReport => "Generate the immutable case report",
        CaseReportArtifactKind.FeeNote => "Generate the immutable fee note",
        CaseReportArtifactKind.RepairSpecification => "Generate the immutable Repair Spec",
        _ => "Generate the immutable images",
    };

    /// <summary>One artifact's generation request, with the reason its kind records.</summary>
    public static GenerateCaseReportRequest RequestFor(
        ActionActor actor,
        Guid caseId,
        long expectedCaseVersion,
        string leaseToken,
        string operationKey,
        CaseReportArtifactKind kind,
        Guid? targetGenerationId,
        CaseWorkSelector work) =>
        new(
            actor,
            caseId,
            expectedCaseVersion,
            leaseToken,
            operationKey,
            kind,
            ReasonFor(kind),
            targetGenerationId,
            work);

    /// <summary>
    /// The fee note a report request goes on to make, under the same lease and
    /// version, from the generation <see cref="GenerateCaseReport.FeeNoteTargetOf"/>
    /// pairs it with; null when it pairs none. A report not yet confirmed
    /// leaves the fee note to the next Generate report, which replays the
    /// report's operation key.
    /// </summary>
    public static GenerateCaseReportRequest? FeeNoteRequestAfter(
        GenerateCaseReportRequest report,
        CaseReportGenerationResult result,
        string feeNoteOperationKey)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(result);
        return GenerateCaseReport.FeeNoteTargetOf(report.Kind, result.Generation) is { } feeNoteTarget
            ? RequestFor(
                report.Actor,
                report.CaseId,
                report.ExpectedCaseVersion,
                report.LeaseToken,
                feeNoteOperationKey,
                CaseReportArtifactKind.FeeNote,
                feeNoteTarget,
                report.Work)
            : null;
    }

    /// <summary>
    /// The Automation Actor's report gate, as the Case page's: an assessment
    /// that opens. An assessment that opens is never read-only, so no
    /// read-only exception follows (R1095).
    /// </summary>
    public async Task<CaseReportActAccess> GetAccessAsync(
        ActionActor actor,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var access = await getAssessmentAccess.ExecuteAsync(new(caseId, actor), cancellationToken);
        return access is null || !access.CanOpen
            ? CaseReportActAccess.NotOpen
            : CaseReportActAccess.Open;
    }
}
