using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Triage;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The route evidence a Triage Case is opened from, in its persisted form
/// (channel code, trimmed token, lower-case source hash).
/// </summary>
internal sealed record TriageCaseOrigin(
    Guid ReceiptId,
    string SourceChannel,
    string ExternalReceiptToken,
    string SourceHash,
    Guid EvaluationRevisionId);

/// <summary>
/// The one owner of the rows a new Triage Case is made of: the Cases row
/// (type <c>triage</c>, its allocated <c>t.</c> Case/PO, no initial workflow
/// state), the Triage subtype row, its <c>triage_created</c> history entry and
/// the standard Case custody work. A Triage Case has no Case workflow, intake
/// link, data snapshot, match index entry, due work or vehicle lookup. The
/// context supplies its primary Case work on save, as it does for every Case.
/// </summary>
internal static class TriageCaseRows
{
    public const string CreatedEventType = "triage_created";

    public static TriageEntity Add(
        PegasusDbContext context,
        PrincipalEntity principal,
        AllocatedCaseIdentity allocated,
        TriageCaseOrigin? origin,
        string normalizedVehicleRegistration,
        ActionActor actor,
        string operationKey,
        string reason,
        string requestHash,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(principal);
        ArgumentNullException.ThrowIfNull(allocated);
        ArgumentNullException.ThrowIfNull(actor);
        var caseId = Guid.NewGuid();
        var caseEntity = new CaseEntity
        {
            Id = caseId,
            PrincipalId = principal.Id,
            Principal = principal,
            SequenceLineageId = principal.SequenceLineageId,
            Year = allocated.Year,
            Sequence = allocated.Sequence,
            Reference = allocated.Reference,
            Type = CaseTypeCodes.Triage,
            InitialState = null,
            CustodyState = "pending",
            // The receipt-origin Triage Case names its source receipt so the
            // standard Case custody retains that source. Receipt state reads
            // the intake links and manual associations, never this column.
            OriginIntakeReceiptId = origin?.ReceiptId,
            InstructionComplete = false,
            ImagesComplete = false,
            CreatedAtUtc = nowUtc,
            Version = 0
        };
        context.Cases.Add(caseEntity);

        var triage = new TriageEntity
        {
            CaseId = caseId,
            Case = caseEntity,
            OriginReceiptId = origin?.ReceiptId,
            SourceChannel = origin?.SourceChannel,
            ExternalReceiptToken = origin?.ExternalReceiptToken,
            SourceHash = origin?.SourceHash,
            EvaluationRevisionId = origin?.EvaluationRevisionId,
            NormalizedVehicleRegistration = normalizedVehicleRegistration,
            State = EfTriageStore.ToCode(TriageState.Open),
            CreatedAtUtc = nowUtc,
            CreationOperationKey = operationKey,
            Version = 0
        };
        context.Triage.Add(triage);
        context.TriageHistory.Add(new TriageHistoryEntity
        {
            Id = Guid.NewGuid(),
            TriageCaseId = caseId,
            Triage = triage,
            EventType = CreatedEventType,
            Actor = actor.SubjectId,
            ActorKind = actor.Kind.ToString(),
            Reason = reason,
            OperationKey = operationKey,
            RequestHash = requestHash,
            OccurredAtUtc = nowUtc,
            BeforeVersion = -1,
            AfterVersion = triage.Version,
            AfterState = triage.State,
            AfterAssigneeId = null,
            AfterLinkedInstructionCaseId = null
        });

        context.ExternalWorkItems.Add(new ExternalWorkItemEntity
        {
            Id = Guid.NewGuid(),
            Case = caseEntity,
            CaseId = caseId,
            Kind = ExternalWorkKinds.CreateCaseCustody,
            OperationKey = origin is null
                ? $"manual-custody:{caseId:N}"
                : $"triage-custody:{caseId:N}",
            State = "pending",
            AttemptCount = 0,
            DueAtUtc = nowUtc,
            CaseRootCreationToken = CustodyCreationOwner.Create()
        });

        return triage;
    }
}
