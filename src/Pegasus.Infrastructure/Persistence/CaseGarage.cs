using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The stored garage facts of a Case (<see cref="CaseGarageFacts"/>), read
/// once for every owner that asks: the Review gate, the authorisation acts,
/// the report blockers and the Worker's reconciliation. A Case that is not a
/// Garage authorisation and invoice Audit costs no read.
/// </summary>
internal static class CaseGarage
{
    /// <summary>
    /// The facts of a Case being created: nothing is filed or authorised yet,
    /// so a Garage authorisation and invoice Audit starts Not ready until its
    /// External estimate arrives (R1141).
    /// </summary>
    internal static CaseGarageFacts ForNewCase(CaseEntity caseEntity) =>
        new(Applies(caseEntity), false, false, false, false);

    /// <summary>The same, before the Case row exists: its type and its Principal's Audit workflow.</summary>
    internal static CaseGarageFacts ForNewCase(CaseType caseType, string principalAuditWorkflowCode) =>
        new(
            GarageAuthorisationPolicy.Applies(
                caseType,
                PrincipalAuditWorkflowPolicy.ForNewCase(
                    caseType, PrincipalAuditWorkflowPolicy.Parse(principalAuditWorkflowCode))),
            false,
            false,
            false,
            false);

    internal static bool Applies(CaseEntity caseEntity) => Applies(caseEntity.Type, caseEntity.AuditWorkflow);

    private static bool Applies(string caseTypeCode, string auditWorkflowCode) =>
        GarageAuthorisationPolicy.Applies(
            CaseTypeCodes.Parse(caseTypeCode),
            PrincipalAuditWorkflowPolicy.Parse(auditWorkflowCode));

    /// <summary>The Case's stored facts. Unsaved changes in <paramref name="db"/> are not read.</summary>
    internal static Task<CaseGarageFacts> ReadAsync(
        PegasusDbContext db,
        CaseEntity caseEntity,
        DateTimeOffset? garageAuthorisedAtUtc,
        CancellationToken cancellationToken) =>
        ReadAsync(
            db, caseEntity.Id, caseEntity.Type, caseEntity.AuditWorkflow, garageAuthorisedAtUtc, cancellationToken);

    /// <summary>The same, for a reader that projected the Case's type and Audit workflow codes.</summary>
    internal static async Task<CaseGarageFacts> ReadAsync(
        PegasusDbContext db,
        Guid caseId,
        string caseTypeCode,
        string auditWorkflowCode,
        DateTimeOffset? garageAuthorisedAtUtc,
        CancellationToken cancellationToken)
    {
        if (!Applies(caseTypeCode, auditWorkflowCode))
        {
            return CaseGarageFacts.None;
        }

        var outcome = await db.CaseAssessmentFields.AsNoTracking()
            .Where(field => field.WorkId == caseId && field.FieldPath == AssessmentVocabulary.Outcome)
            .Select(field => field.Value)
            .SingleOrDefaultAsync(cancellationToken);
        return new(
            true,
            garageAuthorisedAtUtc is not null,
            GarageAuthorisationPolicy.IsWaived(outcome),
            await HasConfirmedAsync(db, caseId, DocumentSemanticRole.ExternalEstimate, cancellationToken),
            await HasConfirmedAsync(db, caseId, DocumentSemanticRole.RepairInvoice, cancellationToken));
    }

    /// <summary>The same, for a caller that holds only the Case's id.</summary>
    internal static async Task<CaseGarageFacts> ReadAsync(
        PegasusDbContext db,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var workflow = await db.CaseWorkflows.AsNoTracking()
            .Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken);
        return workflow is null
            ? CaseGarageFacts.None
            : await ReadAsync(db, workflow.Case, workflow.GarageAuthorisedAtUtc, cancellationToken);
    }

    /// <summary>
    /// Whether a save recorded an Outcome that waives the garage's
    /// authorisation and invoice where the Outcome before did not: the
    /// assessment saves that end a waiting Case's wait in the same save
    /// (R1143, R1195).
    /// </summary>
    internal static bool RecordsWaivingOutcome(
        IReadOnlyDictionary<string, string?> before,
        IReadOnlyDictionary<string, string?> after) =>
        GarageAuthorisationPolicy.IsWaived(after.GetValueOrDefault(AssessmentVocabulary.Outcome))
        && !GarageAuthorisationPolicy.IsWaived(before.GetValueOrDefault(AssessmentVocabulary.Outcome));

    /// <summary>
    /// Whether the Case holds a filed document of <paramref name="role"/> that
    /// is current, not removed and confirmed in custody: the files the Case's
    /// own Files list shows as kept.
    /// </summary>
    internal static Task<bool> HasConfirmedAsync(
        PegasusDbContext db,
        Guid caseId,
        DocumentSemanticRole role,
        CancellationToken cancellationToken) =>
        Confirmed(db, role).AnyAsync(occurrence => occurrence.CaseId == caseId, cancellationToken);

    /// <summary>The confirmed, current, kept occurrences of one type, for a query to join.</summary>
    internal static IQueryable<DocumentOccurrenceEntity> Confirmed(PegasusDbContext db, DocumentSemanticRole role) =>
        Confirmed(db).Where(occurrence => occurrence.SemanticRole == role);

    /// <summary>
    /// The confirmed, current, kept occurrences of every type that Pegasus did
    /// not generate: the one definition the typed reads above and the report
    /// send's offered files (R1151) share.
    /// </summary>
    internal static IQueryable<DocumentOccurrenceEntity> Confirmed(PegasusDbContext db) =>
        db.Set<DocumentOccurrenceEntity>().AsNoTracking()
            .Where(occurrence => occurrence.Source != DocumentSource.Generated
                && db.Set<DocumentVersionEntity>().Any(version =>
                    version.Id == occurrence.VersionId
                    && version.IsCurrent
                    && !version.IsLogicallyRemoved
                    && version.CustodyStatus == DocumentCustodyStatus.Confirmed));

    /// <summary>
    /// The Not ready Garage authorisation and invoice Audits a stored fact may
    /// now move on: one waiting for its repair invoice that has a confirmed
    /// Repair invoice or a waiving Outcome, and one on its way into Review,
    /// with no Engineer, whose instruction and images are complete and whose
    /// External estimate is confirmed. A Case somebody is editing is left for
    /// the next run, so the session's version does not move under it. The
    /// page after <paramref name="after"/> in Case id order, so a run passes
    /// over a candidate it could not move.
    /// </summary>
    internal static async Task<IReadOnlyList<Guid>> CandidatesAsync(
        PegasusDbContext db,
        DateTimeOffset nowUtc,
        Guid? after,
        int limit,
        CancellationToken cancellationToken)
    {
        var afterId = after ?? Guid.Empty;
        var notReady = nameof(CaseLifecycleState.NotReady);
        var audit = CaseTypeCodes.ToCode(CaseType.Audit);
        var estimates = Confirmed(db, DocumentSemanticRole.ExternalEstimate);
        var invoices = Confirmed(db, DocumentSemanticRole.RepairInvoice);
        return await db.CaseWorkflows.AsNoTracking()
            .Where(workflow => workflow.State == notReady
                && workflow.ArchivedAtUtc == null
                && (after == null || workflow.CaseId.CompareTo(afterId) > 0)
                && (workflow.EditLeaseExpiresAtUtc == null || workflow.EditLeaseExpiresAtUtc <= nowUtc)
                && workflow.Case.Type == audit
                && workflow.Case.AuditWorkflow == PrincipalAuditWorkflowPolicy.GarageAuthorisationAndInvoiceCode
                && ((workflow.GarageAuthorisedAtUtc != null
                        && (invoices.Any(invoice => invoice.CaseId == workflow.CaseId)
                            || db.CaseAssessmentFields.Any(field =>
                                field.WorkId == workflow.CaseId
                                && field.FieldPath == AssessmentVocabulary.Outcome
                                && (field.Value == "total_loss" || field.Value == "cash_in_lieu"))))
                    || (workflow.GarageAuthorisedAtUtc == null
                        && workflow.AssignedEngineerId == null
                        && workflow.Case.InstructionComplete
                        && workflow.Case.ImagesComplete
                        && estimates.Any(estimate => estimate.CaseId == workflow.CaseId))))
            .OrderBy(workflow => workflow.CaseId)
            .Select(workflow => workflow.CaseId)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
