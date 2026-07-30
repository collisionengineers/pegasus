using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Access;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.Infrastructure.Workflow;

public sealed class EfCaseAcceptanceStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IIntakeReceiptQueries intakeQueries,
    EfCaseStore caseStore,
    TimeProvider clock) : ICaseAcceptance
{
    public async Task<CaseCommandResult> AcceptAsync(AcceptCaseDraft draft, StaffActor actor, CancellationToken cancellationToken)
    {
        var receipt = await intakeQueries.GetAsync(draft.ReceiptId, cancellationToken);
        if (receipt is null || receipt.Decision != IntakeDecision.DraftReady || receipt.InstructionDraft is null)
            return CaseCommandResult.Failed(CaseCommandFailure.AcceptanceIncomplete, "The source is not ready for Case acceptance.");
        var registration = Pegasus.Core.Triage.TriageQuery.NormalizeRegistration(receipt.InstructionDraft.VehicleRegistration);
        if (registration is null) return CaseCommandResult.Failed(CaseCommandFailure.AcceptanceIncomplete, "A normalized vehicle registration is required.");
        var principalCode = receipt.InstructionDraft.SuggestedPrincipalCode?.Trim().ToUpperInvariant();
        if (principalCode != "QDOS") return CaseCommandResult.Failed(CaseCommandFailure.UnknownPrincipal, "Only the accepted QDOS principal can allocate a Case in this alpha.");
        if (draft.Type == CaseType.StandaloneAudit && draft.AuditAssessment is null)
            return CaseCommandResult.Failed(CaseCommandFailure.AcceptanceIncomplete, "Standalone Audit requires an original-report assessment.");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var origin = $"intake:{draft.ReceiptId:D}";
        var existing = await db.Cases.SingleOrDefaultAsync(x => x.Origin == origin, cancellationToken);
        if (existing is not null) return new CaseCommandResult(await caseStore.GetAsync(existing.Id, actor, cancellationToken), null);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var year = receipt.ReceivedAtUtc.Year;
        var sequence = await db.CaseSequences.SingleOrDefaultAsync(x => x.PrincipalCode == principalCode && x.Year == year, cancellationToken);
        if (sequence is null)
        {
            sequence = new CaseSequenceEntity { Id = Guid.NewGuid(), PrincipalCode = principalCode, Year = year, LastSequence = 0 };
            db.CaseSequences.Add(sequence);
        }
        if (sequence.LastSequence >= 999) return CaseCommandResult.Failed(CaseCommandFailure.SequenceExhausted);
        sequence.LastSequence++;
        var baseReference = $"{principalCode}{year % 100:00}{sequence.LastSequence:000}";
        var displayReference = draft.Type == CaseType.StandaloneAudit
            ? $"{(draft.AuditAssessment == Pegasus.Core.Triage.AssessmentFinding.TotalLoss ? "ap" : "a")}.{baseReference}"
            : baseReference;
        var state = draft.InstructionsComplete && draft.ImagesComplete ? CaseWorkflowState.Review : CaseWorkflowState.NotReady;
        var item = new CaseEntity
        {
            Id = Guid.NewGuid(), PrincipalCode = principalCode, BaseReference = baseReference, DisplayReference = displayReference,
            Type = draft.Type.ToString(), Registration = registration, Claimant = receipt.InstructionDraft.ClaimantName,
            ClaimNumber = receipt.InstructionDraft.ClaimNumber, ReceivedAtUtc = receipt.ReceivedAtUtc,
            InstructionDate = receipt.InstructionDraft.InstructionDate, Origin = origin, State = state.ToString(), Version = 1,
            NextDueAtUtc = state == CaseWorkflowState.NotReady ? receipt.ReceivedAtUtc.AddDays(7) : null
        };
        db.Cases.Add(item);
        db.BusinessActions.Add(new BusinessActionEntity { Id = Guid.NewGuid(), CaseId = item.Id, ActorKind = "Staff", ActorId = actor.Id, Caller = "Web", Action = "AcceptCaseDraft", OccurredAtUtc = clock.GetUtcNow(), CorrelationId = draft.CorrelationId, Outcome = "Succeeded" });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { return CaseCommandResult.Failed(CaseCommandFailure.StaleVersion); }
        return new CaseCommandResult(await caseStore.GetAsync(item.Id, actor, cancellationToken), null);
    }
}
