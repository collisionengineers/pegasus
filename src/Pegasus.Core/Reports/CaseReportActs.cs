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
/// note, the Principal's own invoice (R1147) and the companion documents the
/// Principal's rules require (R1262) a report request goes on to make, and
/// the Automation Actor's gate
/// for generating or sending a report. Each caller opens the generation's
/// custody batch itself (<see cref="IGenerateCaseReport.BeginBatch"/> is an
/// ambient scope that must be opened in the caller's own flow) and presents
/// each outcome its own way; a fee note that fails after its report
/// succeeded is the caller's to present (R771).
/// </summary>
public sealed class CaseReportActs(
    IGetAssessmentAccess getAssessmentAccess,
    IReportRecipientSuggestionQueries dispatchFacts,
    TimeProvider timeProvider)
{
    private readonly IGetAssessmentAccess getAssessmentAccess =
        getAssessmentAccess ?? throw new ArgumentNullException(nameof(getAssessmentAccess));
    private readonly IReportRecipientSuggestionQueries dispatchFacts =
        dispatchFacts ?? throw new ArgumentNullException(nameof(dispatchFacts));
    private readonly TimeProvider timeProvider =
        timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    /// <summary>The reason one artifact's generation records.</summary>
    public static string ReasonFor(CaseReportArtifactKind kind) => kind switch
    {
        CaseReportArtifactKind.AssessmentReport => "Generate the immutable case report",
        CaseReportArtifactKind.FeeNote => "Generate the immutable fee note",
        CaseReportArtifactKind.RepairSpecification => "Generate the immutable Repair Spec",
        CaseReportArtifactKind.PrincipalInvoice => "Generate the immutable ULA Invoice",
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
        return CompanionAfter(
            report,
            CaseReportArtifactKind.FeeNote,
            GenerateCaseReport.FeeNoteTargetOf(report.Kind, result.Generation),
            feeNoteOperationKey);
    }

    /// <summary>
    /// The Principal's own invoice (the ULA Invoice) a report request goes on
    /// to make after its fee note, under the same lease and version, from the
    /// generation <see cref="GenerateCaseReport.PrincipalInvoiceTargetOf"/>
    /// pairs it with; null when it pairs none (R1147). <paramref name="result"/>
    /// is the latest generation result: the fee note's when one was made, so
    /// the invoice reads the generation as the fee note left it.
    /// </summary>
    public static GenerateCaseReportRequest? PrincipalInvoiceRequestAfter(
        GenerateCaseReportRequest report,
        CaseReportGenerationResult result,
        string principalInvoiceOperationKey)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(result);
        return CompanionAfter(
            report,
            CaseReportArtifactKind.PrincipalInvoice,
            GenerateCaseReport.PrincipalInvoiceTargetOf(report.Kind, result.Generation),
            principalInvoiceOperationKey);
    }

    /// <summary>
    /// The companion documents a report request goes on to make after its fee
    /// note and the Principal's own invoice, under the same lease and version:
    /// each one Send report would require and is not yet confirmed, as
    /// <see cref="ReportDispatchPolicy.Plan"/> reads the Principal's rules for
    /// the Case now, the images before the Repair Spec (R1262). None unless
    /// <paramref name="report"/> asked for the report and
    /// <paramref name="result"/>, the latest generation result, holds it
    /// confirmed: a report still being filed leaves them to the next Generate
    /// report, which replays the report's operation key. Images are not asked
    /// of a generation that froze none.
    /// </summary>
    public async Task<IReadOnlyList<GenerateCaseReportRequest>> RequiredCompanionRequestsAfterAsync(
        GenerateCaseReportRequest report,
        CaseReportGenerationResult result,
        Func<CaseReportArtifactKind, string> operationKeyFor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(operationKeyFor);
        var generation = result.Generation;
        if (report.Kind != CaseReportArtifactKind.AssessmentReport
            || !GenerateCaseReport.HasConfirmedReport(generation))
        {
            return [];
        }
        var facts = await dispatchFacts.GetAsync(report.CaseId, report.Work, cancellationToken).ConfigureAwait(false);
        if (facts is null)
        {
            return [];
        }
        var plan = ReportDispatchPolicy.Plan(
            facts with
            {
                ConfirmedArtifacts =
                [
                    .. generation.Artifacts
                        .Where(artifact => artifact.Status == CaseReportArtifactStatus.Confirmed)
                        .Select(artifact => artifact.Kind)
                ],
            },
            timeProvider.GetUtcNow());
        return
        [
            .. RequiredCompanionKinds(plan.MissingCompanions, generation.Snapshot.Images.Count)
                .Select(kind => CompanionAfter(report, kind, generation.Id, operationKeyFor(kind))!)
        ];
    }

    /// <summary>
    /// Of the companions a plan finds missing, the ones a report request makes,
    /// in the order it makes them: the images, when the generation froze any,
    /// then the Repair Spec. The Case page offers Generate report again while
    /// one of these is outstanding (R1262).
    /// </summary>
    public static IReadOnlyList<CaseReportArtifactKind> RequiredCompanionKinds(
        IReadOnlyCollection<CaseReportArtifactKind> missing,
        int frozenImageCount)
    {
        ArgumentNullException.ThrowIfNull(missing);
        var kinds = new List<CaseReportArtifactKind>(2);
        if (missing.Contains(CaseReportArtifactKind.ImagePack) && frozenImageCount > 0)
        {
            kinds.Add(CaseReportArtifactKind.ImagePack);
        }
        if (missing.Contains(CaseReportArtifactKind.RepairSpecification))
        {
            kinds.Add(CaseReportArtifactKind.RepairSpecification);
        }
        return kinds;
    }

    /// <summary>A companion's request under the report's own lease, version and work, when it has a target.</summary>
    private static GenerateCaseReportRequest? CompanionAfter(
        GenerateCaseReportRequest report,
        CaseReportArtifactKind kind,
        Guid? target,
        string operationKey) =>
        target is { } generationId
            ? RequestFor(
                report.Actor,
                report.CaseId,
                report.ExpectedCaseVersion,
                report.LeaseToken,
                operationKey,
                kind,
                generationId,
                report.Work)
            : null;

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
