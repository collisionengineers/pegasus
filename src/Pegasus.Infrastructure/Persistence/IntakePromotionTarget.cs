using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

internal enum PromotionTargetDisposition
{
    /// <summary>No filing is due: no active association to this Case, the Case's own origin receipt, an archived Case, or an automatic association the Case's state no longer accepts.</summary>
    NotApplicable,

    /// <summary>The Case is being edited; filing waits for the editor.</summary>
    Deferred,

    /// <summary>The Case may receive the receipt's evidence now, at <see cref="PromotionTargetEvaluation.Version"/>.</summary>
    Current
}

/// <param name="Version">The Case workflow version, or a Triage Case's Triage version, the filing is bound to.</param>
/// <param name="IsStaffDecision">Whether the association was a member of staff's decision rather than the pipeline's.</param>
internal sealed record PromotionTargetEvaluation(
    PromotionTargetDisposition Disposition,
    long Version = 0,
    bool IsStaffDecision = false);

/// <summary>
/// The one rule for whether a receipt's retained evidence may be filed on a
/// Case: the plan (<see cref="EfIntakeMutationStore"/>) and every custody
/// step (<see cref="Custody.EfCaseArtifactCustody"/>) read it here, so the
/// two can never disagree. An active association to the Case is required
/// whoever made it. The pipeline's own (system-worker) association files
/// only while the Case is pre-report and in a state automatic association
/// accepts; a member of staff's association — a staff link, or a destination
/// declared on upload — files on any Case in any state (FRD-22), including a
/// Triage Case, which has no workflow and answers through its Triage
/// version. An archived Case never files. A live Case edit lease defers
/// filing rather than refusing it; a Triage edit scope never yields.
/// </summary>
internal static class IntakePromotionTarget
{
    internal static readonly string[] AutomaticPromotionEligibleStates =
        Enum.GetValues<CaseLifecycleState>()
            .Where(state => ImageIntakeLifecycleRules.IsCaseEligibleForAssociation(state, false))
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
            return new(PromotionTargetDisposition.NotApplicable);
        }

        var isStaffDecision = !string.Equals(
            association.ActorKind, nameof(ActorKind.SystemWorker), StringComparison.Ordinal);
        var caseRow = await db.Cases.AsNoTracking()
            .Where(item => item.Id == caseId)
            .Select(item => new { item.OriginIntakeReceiptId })
            .SingleOrDefaultAsync(cancellationToken);
        if (caseRow is null || caseRow.OriginIntakeReceiptId == receiptId)
        {
            // The receipt that created the Case had its source filed by
            // acceptance custody; there is nothing to promote.
            return new(PromotionTargetDisposition.NotApplicable);
        }

        var workflow = await db.CaseWorkflows.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => new
            {
                item.Version,
                item.ArchivedAtUtc,
                item.State,
                item.ReportSentEvidenceId,
                item.EditLeaseExpiresAtUtc
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (workflow is not null)
        {
            if (workflow.ArchivedAtUtc is not null
                || (!isStaffDecision
                    && (!AutomaticPromotionEligibleStates.Contains(workflow.State)
                        || workflow.ReportSentEvidenceId is not null)))
            {
                return new(PromotionTargetDisposition.NotApplicable, workflow.Version, isStaffDecision);
            }

            return CaseEditAuthority.IsHeld(workflow.EditLeaseExpiresAtUtc, nowUtc)
                ? new(PromotionTargetDisposition.Deferred, workflow.Version, isStaffDecision)
                : new(PromotionTargetDisposition.Current, workflow.Version, isStaffDecision);
        }

        // A Triage Case: no workflow, no archive. Only a member of staff links
        // material to one; the pipeline's automatic association never does.
        if (!isStaffDecision)
        {
            return new(PromotionTargetDisposition.NotApplicable);
        }

        var triageVersion = await db.Triage.AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => (long?)item.Version)
            .SingleOrDefaultAsync(cancellationToken);
        return triageVersion is { } version
            ? new(PromotionTargetDisposition.Current, version, true)
            : new(PromotionTargetDisposition.NotApplicable);
    }

    /// <summary>Whether the Case may receive the receipt's evidence now, at exactly the version the filing was planned against.</summary>
    public static async Task<bool> IsCurrentAsync(
        PegasusDbContext db,
        Guid caseId,
        Guid receiptId,
        long expectedCaseVersion,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var target = await EvaluateAsync(db, caseId, receiptId, nowUtc, cancellationToken);
        return target.Disposition == PromotionTargetDisposition.Current
            && target.Version == expectedCaseVersion;
    }
}
