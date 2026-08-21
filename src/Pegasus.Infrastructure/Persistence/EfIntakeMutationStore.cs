using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfIntakeMutationStore(
    IDbContextFactory<PegasusDbContext> contextFactory)
    : IIntakeMutationStore, IAutomaticCaseAssociationStore,
      IAutomaticMailCaseAssociationEvidenceQueries
{
    public async Task<AutomaticMailCaseAssociationEvidence?> GetAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await LoadMailAssociationEvidenceAsync(context, intakeReceiptId, cancellationToken);
    }

    public async Task<AutomaticCaseAssociationOutcome> AssociateFromMatchAsync(
        AutomaticCaseAssociationRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.MatchPolicyKey);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MatchPolicyVersion);
        if (request.ExpectedEvidenceFingerprint is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(request.ExpectedEvidenceFingerprint);
        }
        var operationKey = request.OperationKey.Trim();
        var requestHash = RequestHash("intake_case_linked_automatic", request);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replay = await context.IntakeMutationHistory
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == operationKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.IntakeReceiptId != request.IntakeReceiptId
                || !FixedTimeHashEquals(replay.RequestFingerprint, requestHash))
            {
                throw new IntakeOperationConflictException();
            }

            return AutomaticCaseAssociationOutcome.AlreadyAssociated;
        }

        var receipt = await LoadReceiptAsync(context, request.IntakeReceiptId, cancellationToken)
            ?? throw new KeyNotFoundException("The intake receipt does not exist.");
        if (receipt.ManualAssociation is not null)
        {
            // Any prior association row — active, or deliberately reversed by staff —
            // stops the automatic write: a staff unlink must never be silently re-linked
            // by a later evaluation. Relinking stays the staff LinkIntake path.
            return AutomaticCaseAssociationOutcome.AlreadyAssociated;
        }

        var acceptedCaseId = await AcceptedCaseIdAsync(
            context,
            request.IntakeReceiptId,
            cancellationToken);
        if (acceptedCaseId is not null)
        {
            return AutomaticCaseAssociationOutcome.AlreadyAssociated;
        }

        if (request.ExpectedEvidenceFingerprint is { } expectedFingerprint)
        {
            var currentEvidence = await LoadMailAssociationEvidenceAsync(
                context,
                request.IntakeReceiptId,
                cancellationToken);
            if (currentEvidence is null
                || !FixedTimeHashEquals(currentEvidence.Fingerprint, expectedFingerprint))
            {
                throw new IntakeAssociationConflictException(
                    "The retained-mail matching evidence changed before association; the automatic association yields.");
            }
        }

        var caseWorkflow = await context.CaseWorkflows
            .Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException("The matched case does not exist.");
        // The accepted eliminator predicates make every lifecycle state
        // eligible, but that operator decision does not cover an archived
        // case or one under a live staff edit lease — those yield.
        ArchivedCaseGuard.RequireNotArchived(caseWorkflow);
        if (caseWorkflow.EditLeaseExpiresAtUtc is { } leaseExpiresAtUtc
            && leaseExpiresAtUtc > occurredAtUtc)
        {
            throw new IntakeAssociationConflictException(
                "The case is being edited by a staff member; the automatic association yields.");
        }

        var @case = caseWorkflow.Case;
        var beforeVersion = receipt.Version;
        var beforeJson = Snapshot(receipt);
        var reason = request.Reason.Trim();
        // The any-prior-row guard above means no association row exists here.
        receipt.ManualAssociation = new IntakeManualAssociationEntity
        {
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            CaseId = @case.Id,
            Case = @case,
            IsActive = true,
            Version = 0,
            LinkedAtUtc = occurredAtUtc,
            ActorKind = nameof(ActorKind.SystemWorker),
            ActorSubjectId = request.Actor.Trim(),
            ActorRolesJson = "[]",
            Reason = reason,
            LastOperationKey = operationKey,
            MatchPolicyKey = request.MatchPolicyKey.Trim(),
            MatchPolicyVersion = request.MatchPolicyVersion
        };

        receipt.Version++;
        context.IntakeMutationHistory.Add(new IntakeMutationHistoryEntity
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            CaseId = @case.Id,
            Case = @case,
            EventType = "intake_case_linked_automatic",
            ActorKind = nameof(ActorKind.SystemWorker),
            ActorSubjectId = request.Actor.Trim(),
            ActorRolesJson = "[]",
            Reason = reason,
            OperationKey = operationKey,
            RequestFingerprint = requestHash,
            OccurredAtUtc = occurredAtUtc,
            ExpectedIntakeVersion = beforeVersion,
            BeforeIntakeVersion = beforeVersion,
            AfterIntakeVersion = receipt.Version,
            ExpectedCaseVersion = null,
            BeforeCaseVersion = null,
            AfterCaseVersion = null,
            BeforeJson = beforeJson,
            AfterJson = Snapshot(receipt)
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return AutomaticCaseAssociationOutcome.Associated;
    }

    public Task<IntakeReceipt> ResolveAsync(
        ResolveIntakeRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            request.ReceiptId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            "intake_resolved",
            RequestHash("intake_resolved", request),
            expectedCaseId: null,
            expectedCaseVersion: null,
            editLeaseToken: null,
            (context, receipt, _, _) =>
            {
                if (request.Kind == IntakeResolutionKind.Block)
                {
                    receipt.Decision = EfIntakeReceiptStore.ToCode(IntakeDecision.BlockedIntake);
                    receipt.DecisionReason = request.Reason.Trim();
                    receipt.FailureCode = "blocked_intake";
                    receipt.FailureReason = request.Reason.Trim();
                    return Task.CompletedTask;
                }

                var correctedDraft = request.CorrectedDraft
                    ?? throw new ArgumentException(
                        "A corrected draft is required for a draft correction.",
                        nameof(request));
                ApplyResolvedDraft(receipt, correctedDraft);
                ApplyDraftToReviewFields(receipt, correctedDraft);
                // Only identity-critical facts fail a correction closed. Thin
                // ordinary detail is not a blocked intake: the requirement is to
                // allocate once Principal and Case type are established and
                // carry the gap on the case as `Not ready`. Blocking on it made
                // `Blocked intake` mean "some field is empty", which is not its
                // settled meaning, and left real instructions with no case.
                var missing = InstructionDraftCompleteness
                    .MissingIdentityCriticalFieldNames(correctedDraft);
                var canBecomeCase = missing.Count == 0;
                receipt.Decision = EfIntakeReceiptStore.ToCode(
                    canBecomeCase ? IntakeDecision.CaseCreated : IntakeDecision.BlockedIntake);
                receipt.DecisionReason = canBecomeCase
                    ? "The intake correction produced a reviewable instruction draft."
                    : "The intake correction was retained but the instruction does not say which claim it is about.";
                receipt.FailureCode = canBecomeCase ? null : "blocked_intake";
                receipt.FailureReason = canBecomeCase
                    ? null
                    : $"{string.Join(", ", missing)} remains unresolved.";
                return Task.CompletedTask;
            },
            occurredAtUtc,
            cancellationToken);

    public Task<IntakeReceipt> ScheduleReevaluationAsync(
        ReevaluateIntakeRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            request.ReceiptId,
            request.ExpectedVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            "intake_reevaluation_queued",
            RequestHash("intake_reevaluation_queued", request),
            expectedCaseId: null,
            expectedCaseVersion: null,
            editLeaseToken: null,
            async (context, receipt, _, token) =>
            {
                var stagedReceiptId = await context.IntakeEvaluations
                    .Where(item => item.ProcessedReceiptId == receipt.Id)
                    .OrderByDescending(item => item.Revision)
                    .Select(item => (Guid?)item.StagedReceiptId)
                    .FirstOrDefaultAsync(token)
                    ?? throw new InvalidDataException(
                        "The intake receipt does not have a retained evaluation source.");
                var workItem = await context.IntakeWorkItems.SingleOrDefaultAsync(
                    item => item.StagedReceiptId == stagedReceiptId,
                    token)
                    ?? throw new InvalidDataException(
                        "The intake receipt does not have durable evaluation work.");
                if (workItem.State == "processing"
                    && workItem.LeaseExpiresAtUtc is { } leaseExpiresAtUtc
                    && leaseExpiresAtUtc > occurredAtUtc)
                {
                    throw new InvalidOperationException(
                        "The intake receipt is already being evaluated.");
                }

                workItem.State = "pending";
                workItem.DueAtUtc = occurredAtUtc;
                workItem.LeaseToken = null;
                workItem.LeaseExpiresAtUtc = null;
                workItem.FailureCode = null;
                receipt.Decision = EfIntakeReceiptStore.ToCode(IntakeDecision.BlockedIntake);
                receipt.DecisionReason = "A policy re-evaluation of the retained source is queued.";
                receipt.FailureCode = "reevaluation_pending";
                receipt.FailureReason = null;
            },
            occurredAtUtc,
            cancellationToken);

    public async Task LinkAsync(
        LinkIntakeRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        _ = await ExecuteAsync(
            request.ReceiptId,
            request.ExpectedIntakeVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            "intake_case_linked",
            RequestHash("intake_case_linked", request),
            request.CaseId,
            request.ExpectedCaseVersion,
            request.EditLeaseToken,
            async (context, receipt, @case, token) =>
            {
                if (@case is null)
                {
                    throw new InvalidOperationException("A case is required for an intake association.");
                }

                if (receipt.ManualAssociation is { IsActive: true })
                {
                    throw new IntakeAssociationConflictException(
                        "The intake receipt already has an active manual case association.");
                }

                await EnforceImageIntakeEligibilityAsync(context, receipt.Id, @case.Id, token);

                if (receipt.ManualAssociation is null)
                {
                    receipt.ManualAssociation = new IntakeManualAssociationEntity
                    {
                        IntakeReceiptId = receipt.Id,
                        IntakeReceipt = receipt,
                        CaseId = @case.Id,
                        Case = @case,
                        IsActive = true,
                        Version = 0,
                        LinkedAtUtc = occurredAtUtc,
                        ActorKind = request.Actor.Kind.ToString(),
                        ActorSubjectId = request.Actor.SubjectId,
                        ActorRolesJson = RolesJson(request.Actor),
                        Reason = request.Reason.Trim(),
                        LastOperationKey = request.OperationKey.Trim()
                    };
                }
                else
                {
                    var association = receipt.ManualAssociation;
                    association.CaseId = @case.Id;
                    association.Case = @case;
                    association.IsActive = true;
                    association.Version++;
                    association.LinkedAtUtc = occurredAtUtc;
                    association.UnlinkedAtUtc = null;
                    association.ActorKind = request.Actor.Kind.ToString();
                    association.ActorSubjectId = request.Actor.SubjectId;
                    association.ActorRolesJson = RolesJson(request.Actor);
                    association.Reason = request.Reason.Trim();
                    association.LastOperationKey = request.OperationKey.Trim();
                    // A staff relink is a staff decision: the automatic
                    // match-policy stamp from an earlier reversed automatic
                    // association must not carry onto it.
                    association.MatchPolicyKey = null;
                    association.MatchPolicyVersion = null;
                }
            },
            occurredAtUtc,
            cancellationToken);
    }

    public async Task ReverseLinkAsync(
        ReverseIntakeLinkRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        _ = await ExecuteAsync(
            request.ReceiptId,
            request.ExpectedIntakeVersion,
            request.Actor,
            request.OperationKey,
            request.Reason,
            "intake_case_link_reversed",
            RequestHash("intake_case_link_reversed", request),
            request.CaseId,
            request.ExpectedCaseVersion,
            request.EditLeaseToken,
            (_, receipt, _, _) =>
            {
                var association = receipt.ManualAssociation;
                if (association is null
                    || !association.IsActive
                    || association.CaseId != request.CaseId)
                {
                    throw new IntakeAssociationConflictException(
                        "The requested active intake-to-case association does not exist.");
                }

                association.IsActive = false;
                association.Version++;
                association.UnlinkedAtUtc = occurredAtUtc;
                association.ActorKind = request.Actor.Kind.ToString();
                association.ActorSubjectId = request.Actor.SubjectId;
                association.ActorRolesJson = RolesJson(request.Actor);
                association.Reason = request.Reason.Trim();
                association.LastOperationKey = request.OperationKey.Trim();
                return Task.CompletedTask;
            },
            occurredAtUtc,
            cancellationToken);
    }

    /// <summary>
    /// The pipeline's one-shot automatic association: a system-worker actor,
    /// no staff edit lease, the same association write, replay protection,
    /// history rows, and Image-intake case eligibility as the manual link. It
    /// refuses to run while any staff edit lease is active on the case.
    /// </summary>
    public async Task AutoLinkAsync(
        AutomaticIntakeLinkRequest request,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.ExecuteSystemWork);
        var operationKey = request.OperationKey.Trim();
        var reason = request.Reason.Trim();
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        const string eventType = "intake_case_auto_linked";
        var requestHash = RequestHash(eventType, request);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replay = await context.IntakeMutationHistory
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == operationKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.IntakeReceiptId != request.ReceiptId
                || !string.Equals(replay.EventType, eventType, StringComparison.Ordinal)
                || !FixedTimeHashEquals(replay.RequestFingerprint, requestHash))
            {
                throw new IntakeOperationConflictException();
            }

            return;
        }

        var receipt = await LoadReceiptAsync(context, request.ReceiptId, cancellationToken)
            ?? throw new KeyNotFoundException("The intake receipt does not exist.");
        var acceptedCaseId = await AcceptedCaseIdAsync(context, request.ReceiptId, cancellationToken);
        if (acceptedCaseId is not null)
        {
            throw new IntakeAssociationConflictException(
                "An accepted intake receipt cannot be associated automatically.");
        }

        if (receipt.ManualAssociation is not null)
        {
            throw new IntakeAssociationConflictException(
                "The intake receipt already has a case-association history; later changes are staff decisions.");
        }

        var caseWorkflow = await context.CaseWorkflows
            .Include(item => item.Case)
            .SingleOrDefaultAsync(item => item.CaseId == request.CaseId, cancellationToken)
            ?? throw new KeyNotFoundException("The case does not exist.");
        ArchivedCaseGuard.RequireNotArchived(caseWorkflow);
        if (!Enum.TryParse<CaseLifecycleState>(
                caseWorkflow.State,
                ignoreCase: false,
                out var lifecycleState))
        {
            throw new InvalidDataException(
                $"Case '{caseWorkflow.CaseId}' has an unrecognized lifecycle state.");
        }

        if (CaseLifecycleRules.IsTerminal(lifecycleState))
        {
            throw new CaseTerminalMutationException(caseWorkflow.CaseId);
        }

        if (caseWorkflow.Version != request.ExpectedCaseVersion)
        {
            throw new CaseVersionConflictException(
                caseWorkflow.CaseId,
                request.ExpectedCaseVersion,
                caseWorkflow.Version);
        }

        if (caseWorkflow.EditLeaseExpiresAtUtc is { } leaseExpiresAtUtc
            && leaseExpiresAtUtc > occurredAtUtc)
        {
            throw new IntakeAssociationConflictException(
                "The case is being edited by a staff member; the automatic association yields.");
        }

        if (!ImageIntakeLifecycleRules.IsCaseEligibleForAssociation(
                lifecycleState,
                caseWorkflow.ReportSentEvidenceId is not null))
        {
            throw new ImageIntakeCaseNotEligibleException(caseWorkflow.CaseId);
        }

        var @case = caseWorkflow.Case;
        var beforeCaseVersion = caseWorkflow.Version;
        var beforeVersion = receipt.Version;
        var beforeJson = Snapshot(receipt);
        receipt.ManualAssociation = new IntakeManualAssociationEntity
        {
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            CaseId = @case.Id,
            Case = @case,
            IsActive = true,
            Version = 0,
            LinkedAtUtc = occurredAtUtc,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = RolesJson(request.Actor),
            Reason = reason,
            LastOperationKey = operationKey
        };
        receipt.Version++;
        CaseMutationGuard.Complete(caseWorkflow);
        context.CaseWorkflowEvents.Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = caseWorkflow.CaseId,
            Workflow = caseWorkflow,
            EventType = eventType,
            OperationKey = operationKey,
            RequestHash = requestHash,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = RolesJson(request.Actor),
            Reason = reason,
            OccurredAtUtc = occurredAtUtc,
            BeforeVersion = beforeCaseVersion,
            AfterVersion = caseWorkflow.Version,
            ResultJson = Snapshot(receipt)
        });
        context.IntakeMutationHistory.Add(new IntakeMutationHistoryEntity
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            CaseId = @case.Id,
            Case = @case,
            EventType = eventType,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = RolesJson(request.Actor),
            Reason = reason,
            OperationKey = operationKey,
            RequestFingerprint = requestHash,
            OccurredAtUtc = occurredAtUtc,
            ExpectedIntakeVersion = beforeVersion,
            BeforeIntakeVersion = beforeVersion,
            AfterIntakeVersion = receipt.Version,
            ExpectedCaseVersion = request.ExpectedCaseVersion,
            BeforeCaseVersion = beforeCaseVersion,
            AfterCaseVersion = caseWorkflow.Version,
            BeforeJson = beforeJson,
            AfterJson = Snapshot(receipt)
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new IntakeVersionConflictException();
        }
    }

    /// <summary>
    /// Once a receipt carries a registered Image intake, every new case
    /// association must satisfy the Image-intake eligibility rule (editable
    /// pre-report state, no report-sent evidence). Reversal stays available.
    /// </summary>
    private static async Task EnforceImageIntakeEligibilityAsync(
        PegasusDbContext context,
        Guid receiptId,
        Guid caseId,
        CancellationToken cancellationToken)
    {
        var hasImageIntake = await context.ImageIntakes
            .AsNoTracking()
            .AnyAsync(item => item.OriginReceiptId == receiptId, cancellationToken);
        if (!hasImageIntake)
        {
            return;
        }

        var workflow = await context.CaseWorkflows
            .AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .Select(item => new { item.State, item.ReportSentEvidenceId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("The case does not exist.");
        if (!Enum.TryParse<CaseLifecycleState>(workflow.State, ignoreCase: false, out var state))
        {
            throw new InvalidDataException($"Case '{caseId}' has an unrecognized lifecycle state.");
        }

        if (!ImageIntakeLifecycleRules.IsCaseEligibleForAssociation(
                state,
                workflow.ReportSentEvidenceId is not null))
        {
            throw new ImageIntakeCaseNotEligibleException(caseId);
        }
    }

    private async Task<IntakeReceipt> ExecuteAsync(
        Guid receiptId,
        long expectedVersion,
        ActionActor actor,
        string operationKey,
        string reason,
        string eventType,
        string requestHash,
        Guid? expectedCaseId,
        long? expectedCaseVersion,
        string? editLeaseToken,
        Func<PegasusDbContext, IntakeReceiptEntity, CaseEntity?, CancellationToken, Task> mutate,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        operationKey = operationKey.Trim();
        reason = reason.Trim();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replay = await context.IntakeMutationHistory
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == operationKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.IntakeReceiptId != receiptId
                || !string.Equals(replay.EventType, eventType, StringComparison.Ordinal)
                || !FixedTimeHashEquals(replay.RequestFingerprint, requestHash))
            {
                throw new IntakeOperationConflictException();
            }

            var replayed = await LoadReceiptAsync(context, receiptId, cancellationToken)
                ?? throw new InvalidDataException("The replayed intake receipt no longer exists.");
            var replayedAcceptedCaseId = await AcceptedCaseIdAsync(context, receiptId, cancellationToken);
            return EfIntakeReceiptStore.Map(replayed, false, replayedAcceptedCaseId);
        }

        var receipt = await LoadReceiptAsync(context, receiptId, cancellationToken)
            ?? throw new KeyNotFoundException("The intake receipt does not exist.");
        if (receipt.Version != expectedVersion)
        {
            throw new IntakeVersionConflictException();
        }

        var acceptedCaseId = await AcceptedCaseIdAsync(context, receiptId, cancellationToken);
        var isAssociationMutation = eventType is
            "intake_case_linked" or "intake_case_link_reversed";
        if (acceptedCaseId is not null && !isAssociationMutation)
        {
            throw new InvalidOperationException(
                "An accepted intake receipt cannot be changed through the pre-case intake workflow.");
        }
        if (eventType == "intake_case_linked"
            && acceptedCaseId is not null
            && receipt.ManualAssociation is null)
        {
            throw new IntakeAssociationConflictException(
                "The accepted intake origin association must be reversed before relinking.");
        }

        CaseEntity? @case = null;
        CaseWorkflowEntity? caseWorkflow = null;
        long? beforeCaseVersion = null;
        if (expectedCaseId is { } caseId)
        {
            caseWorkflow = await context.CaseWorkflows
                .Include(item => item.Case)
                .SingleOrDefaultAsync(item => item.CaseId == caseId, cancellationToken)
                ?? throw new KeyNotFoundException("The case does not exist.");
            CaseMutationGuard.Require(
                caseWorkflow,
                actor,
                expectedCaseVersion
                    ?? throw new InvalidOperationException("An expected case version is required."),
                editLeaseToken
                    ?? throw new InvalidOperationException("A case edit lease token is required."),
                occurredAtUtc);
            @case = caseWorkflow.Case;
            beforeCaseVersion = caseWorkflow.Version;
        }


        var beforeVersion = receipt.Version;
        var beforeJson = Snapshot(receipt);
        await mutate(context, receipt, @case, cancellationToken);
        receipt.Version++;
        if (caseWorkflow is not null)
        {
            CaseMutationGuard.Complete(caseWorkflow);
        }
        if (caseWorkflow is not null && beforeCaseVersion is not null)
        {
            context.CaseWorkflowEvents.Add(new()
            {
                Id = Guid.NewGuid(),
                CaseId = caseWorkflow.CaseId,
                Workflow = caseWorkflow,
                EventType = eventType,
                OperationKey = operationKey,
                RequestHash = requestHash,
                ActorKind = actor.Kind.ToString(),
                ActorSubjectId = actor.SubjectId,
                ActorRolesJson = RolesJson(actor),
                Reason = reason,
                OccurredAtUtc = occurredAtUtc,
                BeforeVersion = beforeCaseVersion.Value,
                AfterVersion = caseWorkflow.Version,
                ResultJson = Snapshot(receipt)
            });
        }

        context.IntakeMutationHistory.Add(new IntakeMutationHistoryEntity
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            CaseId = @case?.Id,
            Case = @case,
            EventType = eventType,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = RolesJson(actor),
            Reason = reason,
            OperationKey = operationKey,
            RequestFingerprint = requestHash,
            OccurredAtUtc = occurredAtUtc,
            ExpectedIntakeVersion = expectedVersion,
            BeforeIntakeVersion = beforeVersion,
            AfterIntakeVersion = receipt.Version,
            ExpectedCaseVersion = expectedCaseVersion,
            BeforeCaseVersion = beforeCaseVersion,
            AfterCaseVersion = caseWorkflow?.Version,
            BeforeJson = beforeJson,
            AfterJson = Snapshot(receipt)
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new IntakeVersionConflictException();
        }

        return EfIntakeReceiptStore.Map(receipt, false, acceptedCaseId);
    }

    private static Task<IntakeReceiptEntity?> LoadReceiptAsync(
        PegasusDbContext context,
        Guid receiptId,
        CancellationToken cancellationToken) =>
        context.IntakeReceipts
            .Include(item => item.Assets)
            .Include(item => item.InstructionDraft)
            .Include(item => item.MailRouteDecision)
            .Include(item => item.MailClassificationDecision)
            .Include(item => item.CaseMatchDecision)
            .Include(item => item.ManualAssociation)
            .SingleOrDefaultAsync(item => item.Id == receiptId, cancellationToken);

    private static Task<Guid?> AcceptedCaseIdAsync(
        PegasusDbContext context,
        Guid receiptId,
        CancellationToken cancellationToken) =>
        context.CaseIntakeLinks
            .AsNoTracking()
            .Where(item => item.IntakeReceiptId == receiptId)
            .Select(item => (Guid?)item.CaseId)
            .SingleOrDefaultAsync(cancellationToken);

    private static void ApplyResolvedDraft(
        IntakeReceiptEntity receipt,
        InstructionDraft draft)
    {
        var entity = receipt.InstructionDraft ?? new InstructionDraftEntity
        {
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt
        };
        entity.SuggestedPrincipalCode = draft.SuggestedPrincipalCode;
        entity.ClaimantName = draft.ClaimantName;
        entity.ClaimNumber = draft.ClaimNumber;
        entity.VehicleRegistration = draft.VehicleRegistration;
        entity.VehicleMake = draft.VehicleMake;
        entity.VehicleModel = draft.VehicleModel;
        entity.VehicleMileage = draft.VehicleMileage;
        entity.AccidentCircumstances = draft.AccidentCircumstances;
        entity.DateOfIncident = draft.DateOfIncident;
        entity.InstructionDate = draft.InstructionDate;
        entity.InspectionDate = draft.InspectionDate;
        entity.InspectionAddress = draft.InspectionAddress;
        receipt.InstructionDraft = entity;
    }
    /// <summary>
    /// Puts the corrected values back onto the review fields, and records the
    /// staff member as the source of anything they keyed themselves.
    /// </summary>
    /// <remarks>
    /// This used only to overwrite <c>SuggestedValue</c> on fields extraction
    /// had already produced, which left two holes. A value a person typed where
    /// extraction found nothing had no review field at all, so acceptance threw
    /// "has no unambiguous source provenance" and a wholly hand-keyed item could
    /// not become a case. And a value a person typed <em>over</em> an extracted
    /// one kept the extracted candidate as its only provenance, so the case
    /// recorded a staff correction as though the document had said it.
    ///
    /// Both are answered the same way: the keyed value becomes a candidate in
    /// its own right, sourced to the staff correction. Nothing is invented —
    /// the candidate says exactly where the value came from.
    ///
    /// A staff candidate is deliberately not content evidence, so
    /// <see cref="Pegasus.Core.Address.Ext18InspectionAddressPolicy"/> ignores
    /// it: a typed address can never come back as an extracted suggestion, and
    /// an address fingerprint rendered on a screen does not move underneath the
    /// person looking at it.
    /// </remarks>
    private static void ApplyDraftToReviewFields(
        IntakeReceiptEntity receipt,
        InstructionDraft draft)
    {
        (string Name, string? Value)[] drafted =
        [
            ("Claimant name", draft.ClaimantName),
            ("Claim number", draft.ClaimNumber),
            ("Vehicle registration", draft.VehicleRegistration),
            ("Vehicle make", draft.VehicleMake),
            ("Vehicle model", draft.VehicleModel),
            ("Vehicle mileage", draft.VehicleMileage?.ToString(CultureInfo.InvariantCulture)),
            ("Accident circumstances", draft.AccidentCircumstances),
            ("Date of incident", draft.DateOfIncident?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ("Instruction date", draft.InstructionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ("Inspection address", draft.InspectionAddress),
            ("Inspection date", draft.InspectionDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
        ];
        var fields = EfIntakeReceiptStore.DeserializeFields(receipt.FieldsJson).ToList();
        foreach (var (name, value) in drafted)
        {
            var index = fields.FindIndex(
                field => string.Equals(field.Name, name, StringComparison.Ordinal));
            if (index < 0)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    fields.Add(new(name, value, [StaffCandidate(value)], false, false));
                }

                continue;
            }

            var field = fields[index];
            if (string.IsNullOrWhiteSpace(value))
            {
                fields[index] = field with { SuggestedValue = value };
                continue;
            }

            var candidates = field.Candidates.Any(candidate =>
                    string.Equals(candidate.Value, value, StringComparison.OrdinalIgnoreCase))
                ? field.Candidates
                : [.. field.Candidates, StaffCandidate(value)];
            // The conflict is gone because a person settled it by stating the
            // value; the losing candidates stay on the record.
            fields[index] = field with
            {
                SuggestedValue = value,
                Candidates = candidates,
                HasConflict = false
            };
        }

        receipt.FieldsJson = EfIntakeReceiptStore.SerializeFields([.. fields]);
    }

    private static InstructionFieldCandidate StaffCandidate(string value) =>
        new(value, IntakeEvidenceSource.StaffCorrection, "keyed by staff");

    private static string Snapshot(IntakeReceiptEntity receipt) => JsonSerializer.Serialize(new
    {
        receipt.Id,
        receipt.Decision,
        receipt.DecisionReason,
        receipt.Version,
        Fields = EfIntakeReceiptStore.DeserializeFields(receipt.FieldsJson),
        InstructionDraft = receipt.InstructionDraft is null
            ? null
            : new
            {
                receipt.InstructionDraft.SuggestedPrincipalCode,
                receipt.InstructionDraft.ClaimantName,
                receipt.InstructionDraft.ClaimNumber,
                receipt.InstructionDraft.VehicleRegistration,
                receipt.InstructionDraft.VehicleMake,
                receipt.InstructionDraft.VehicleModel,
                receipt.InstructionDraft.VehicleMileage,
                receipt.InstructionDraft.AccidentCircumstances,
                receipt.InstructionDraft.DateOfIncident,
                receipt.InstructionDraft.InstructionDate,
                receipt.InstructionDraft.InspectionAddress,
                receipt.InstructionDraft.InspectionDate
            },
        Association = receipt.ManualAssociation is null
            ? null
            : new
            {
                receipt.ManualAssociation.CaseId,
                receipt.ManualAssociation.IsActive,
                receipt.ManualAssociation.Version,
                receipt.ManualAssociation.LinkedAtUtc,
                receipt.ManualAssociation.UnlinkedAtUtc,
                receipt.ManualAssociation.MatchPolicyKey,
                receipt.ManualAssociation.MatchPolicyVersion
            },
        MailRouteDecision = receipt.MailRouteDecision is null
            ? null
            : new
            {
                receipt.MailRouteDecision.Disposition,
                receipt.MailRouteDecision.WorkProviderCode,
                receipt.MailRouteDecision.PolicyKey,
                receipt.MailRouteDecision.PolicyVersion,
                receipt.MailRouteDecision.Reason
            },
        MailClassificationDecision = receipt.MailClassificationDecision is null
            ? null
            : new
            {
                receipt.MailClassificationDecision.Outcome,
                receipt.MailClassificationDecision.Family,
                receipt.MailClassificationDecision.Subtype,
                receipt.MailClassificationDecision.IsReplyContext,
                receipt.MailClassificationDecision.PolicyKey,
                receipt.MailClassificationDecision.PolicyVersion,
                receipt.MailClassificationDecision.Reason
            },
        CaseMatchDecision = receipt.CaseMatchDecision is null
            ? null
            : new
            {
                receipt.CaseMatchDecision.Outcome,
                receipt.CaseMatchDecision.MatchedCaseId,
                receipt.CaseMatchDecision.RedirectedFromCaseId,
                receipt.CaseMatchDecision.PolicyKey,
                receipt.CaseMatchDecision.PolicyVersion,
                receipt.CaseMatchDecision.Reason
            }
    });

    private static string RolesJson(ActionActor actor) =>
        JsonSerializer.Serialize(actor.Roles.OrderBy(role => role));

    private static string RequestHash(string eventType, AutomaticCaseAssociationRequest request)
        => Hash(JsonSerializer.Serialize(new
        {
            EventType = eventType,
            request.IntakeReceiptId,
            request.CaseId,
            request.MatchPolicyKey,
            request.MatchPolicyVersion,
            request.Actor,
            request.OperationKey,
            request.Reason
        }));

    private static async Task<AutomaticMailCaseAssociationEvidence?> LoadMailAssociationEvidenceAsync(
        PegasusDbContext context,
        Guid intakeReceiptId,
        CancellationToken cancellationToken)
    {
        var receipt = await context.IntakeReceipts
            .AsNoTracking()
            .Where(item => item.Id == intakeReceiptId && item.SourceChannel == "mailbox")
            .Select(item => new
            {
                item.Id,
                item.Version,
                item.ExternalReceiptToken,
                VehicleRegistration = item.InstructionDraft == null
                    ? null
                    : item.InstructionDraft.VehicleRegistration
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        var message = await context.RetainedMailboxMessages
            .AsNoTracking()
            .Where(item => item.ExternalReceiptToken == receipt.ExternalReceiptToken)
            .Select(item => new { item.MailboxId, item.ConversationIdentity })
            .SingleOrDefaultAsync(cancellationToken);
        if (message is null)
        {
            return null;
        }

        var normalizedRegistration = Pegasus.Core.Cases.CaseRegistration.Normalize(
            receipt.VehicleRegistration);
        Guid[] registrationCaseIds = [];
        if (normalizedRegistration is not null)
        {
            var registrations = await context.CaseDataFields
                .AsNoTracking()
                .Where(item => item.FieldName == CaseDataFieldNames.VehicleRegistration
                    && context.CaseWorkflows.Any(workflow =>
                        workflow.CaseId == item.CaseId && workflow.ArchivedAtUtc == null))
                .Select(item => new { item.CaseId, item.Value })
                .ToListAsync(cancellationToken);
            registrationCaseIds = registrations
                .Where(item => string.Equals(
                    Pegasus.Core.Cases.CaseRegistration.Normalize(item.Value),
                    normalizedRegistration,
                    StringComparison.Ordinal))
                .Select(item => item.CaseId)
                .Distinct()
                .Order()
                .ToArray();
        }

        Guid[] threadCaseIds = [];
        if (!string.IsNullOrWhiteSpace(message.ConversationIdentity))
        {
            var threadTokens = await context.RetainedMailboxMessages
                .AsNoTracking()
                .Where(item => item.MailboxId == message.MailboxId
                    && item.ConversationIdentity == message.ConversationIdentity)
                .Select(item => item.ExternalReceiptToken)
                .ToListAsync(cancellationToken);
            var threadReceiptIds = await context.IntakeReceipts
                .AsNoTracking()
                .Where(item => item.SourceChannel == "mailbox"
                    && threadTokens.Contains(item.ExternalReceiptToken))
                .Select(item => item.Id)
                .ToListAsync(cancellationToken);
            var current = await CurrentIntakeAssociations.ReadAsync(
                context,
                threadReceiptIds,
                cancellationToken);
            threadCaseIds = current.Current.Values
                .Select(item => item.CaseId)
                .Distinct()
                .Order()
                .ToArray();
        }

        return new(
            receipt.Id,
            receipt.Version,
            normalizedRegistration,
            registrationCaseIds,
            message.MailboxId,
            message.ConversationIdentity,
            threadCaseIds);
    }

    private static string RequestHash(string eventType, ResolveIntakeRequest request) =>
        Hash(JsonSerializer.Serialize(new
        {
            EventType = eventType,
            request.ReceiptId,
            request.ExpectedVersion,
            request.Kind,
            request.CorrectedDraft,
            Actor = ActorMaterial(request.Actor),
            request.OperationKey,
            request.Reason
        }));

    private static string RequestHash(string eventType, ReevaluateIntakeRequest request) =>
        Hash(JsonSerializer.Serialize(new
        {
            EventType = eventType,
            request.ReceiptId,
            request.ExpectedVersion,
            Actor = ActorMaterial(request.Actor),
            request.OperationKey,
            request.Reason
        }));

    private static string RequestHash(string eventType, AutomaticIntakeLinkRequest request) =>
        Hash(JsonSerializer.Serialize(new
        {
            EventType = eventType,
            request.ReceiptId,
            request.CaseId,
            request.ExpectedCaseVersion,
            Actor = ActorMaterial(request.Actor),
            request.OperationKey,
            request.Reason
        }));

    private static string RequestHash(string eventType, LinkIntakeRequest request) =>
        Hash(JsonSerializer.Serialize(new
        {
            EventType = eventType,
            request.ReceiptId,
            request.CaseId,
            request.ExpectedIntakeVersion,
            request.ExpectedCaseVersion,
            request.EditLeaseToken,
            Actor = ActorMaterial(request.Actor),
            request.OperationKey,
            request.Reason
        }));

    private static string RequestHash(string eventType, ReverseIntakeLinkRequest request) =>
        Hash(JsonSerializer.Serialize(new
        {
            EventType = eventType,
            request.ReceiptId,
            request.CaseId,
            request.ExpectedIntakeVersion,
            request.ExpectedCaseVersion,
            request.EditLeaseToken,
            Actor = ActorMaterial(request.Actor),
            request.OperationKey,
            request.Reason
        }));

    private static object ActorMaterial(ActionActor actor) => new
    {
        Kind = actor.Kind.ToString(),
        actor.SubjectId,
        Roles = actor.Roles.OrderBy(role => role).Select(role => role.ToString()).ToArray()
    };

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static bool FixedTimeHashEquals(string left, string right) =>
        left.Length == 64
        && right.Length == 64
        && CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(left),
            Convert.FromHexString(right));
}
