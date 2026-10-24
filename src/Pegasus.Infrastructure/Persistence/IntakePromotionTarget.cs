using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <param name="Version">The Case workflow version, or a Triage Case's Triage version, the filing is bound to.</param>
/// <param name="IsStaffDecision">Whether the association was a member of staff's decision rather than the pipeline's.</param>
internal sealed record PromotionTargetEvaluation(
    CaseFilingTargetDisposition Disposition,
    long Version = 0,
    bool IsStaffDecision = false);

/// <summary>
/// Loads the facts <see cref="CaseFilingTargetRule"/> decides on: the plan
/// (<see cref="EfIntakeMutationStore"/>) and every custody step
/// (<see cref="Custody.EfCaseArtifactCustody"/>) read it here, so the two can
/// never disagree. An active association to the Case is required whoever made
/// it. A Triage Case answers through its Triage version.
/// </summary>
internal static class IntakePromotionTarget
{
    /// <summary>
    /// <see cref="CaseFilingTargetRule.AutomaticFilingStates"/> as stored,
    /// for a query that mirrors the rule in SQL.
    /// </summary>
    internal static readonly string[] AutomaticPromotionEligibleStates =
        CaseFilingTargetRule.AutomaticFilingStates
            .Select(state => state.ToString())
            .ToArray();

    public static async Task<PromotionTargetEvaluation> EvaluateAsync(
        PegasusDbContext db,
        Guid caseId,
        Guid receiptId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        var association = await db.IntakeManualAssociations.AsNoTracking()
            .Where(item => item.IntakeReceiptId == receiptId && item.CaseId == caseId && item.IsActive)
            .Select(item => new { item.ActorKind })
            .SingleOrDefaultAsync(cancellationToken);
        if (association is null)
        {
            return new(CaseFilingTargetDisposition.NotApplicable);
        }

        var isStaffDecision = !string.Equals(
            association.ActorKind, nameof(ActorKind.SystemWorker), StringComparison.Ordinal);
        var caseRow = await db.Cases.AsNoTracking()
            .Where(item => item.Id == caseId)
            .Select(item => new
            {
                item.OriginIntakeReceiptId,
                Workflow = db.CaseWorkflows
                    .Where(row => row.CaseId == caseId)
                    .Select(row => new
                    {
                        row.Version,
                        row.ArchivedAtUtc,
                        row.State,
                        row.ReportSentEvidenceId,
                        row.EditLeaseExpiresAtUtc
                    })
                    .FirstOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (caseRow is null)
        {
            return new(CaseFilingTargetDisposition.NotApplicable);
        }

        var isOriginReceipt = caseRow.OriginIntakeReceiptId == receiptId;
        var workflow = caseRow.Workflow;
        // Only a member of staff links material to a Triage Case, so only
        // then is its Triage version read.
        var triageVersion = workflow is null && isStaffDecision && !isOriginReceipt
            ? await db.Triage.AsNoTracking()
                .Where(item => item.CaseId == caseId)
                .Select(item => (long?)item.Version)
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var disposition = CaseFilingTargetRule.Decide(
            new(
                isStaffDecision,
                isOriginReceipt,
                workflow is null
                    ? null
                    : new(
                        workflow.ArchivedAtUtc is not null,
                        Enum.TryParse<CaseLifecycleState>(workflow.State, out var state) ? state : null,
                        workflow.ReportSentEvidenceId is not null,
                        workflow.EditLeaseExpiresAtUtc),
                triageVersion is not null),
            nowUtc);
        return new(disposition, workflow?.Version ?? triageVersion ?? 0, isStaffDecision);
    }

    /// <summary>
    /// Whether the Case may receive the receipt's evidence now: the
    /// association, the archive, the Case's state and a live edit lease are
    /// read again. The Case version is not: it only rises, so a sibling's
    /// filing never makes this one stale.
    /// </summary>
    public static async Task<bool> IsCurrentAsync(
        PegasusDbContext db,
        Guid caseId,
        Guid receiptId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken) =>
        (await EvaluateAsync(db, caseId, receiptId, nowUtc, cancellationToken)).Disposition
            == CaseFilingTargetDisposition.Current;
}
