using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfImageIntakeStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider? timeProvider = null) : IImageIntakeStore
{
    public async Task<ImageIntakeOperationReplay?> ProbeRegisterReplayAsync(
        RegisterImageIntakeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var operationKey = request.OperationKey.Trim();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await context.ImageIntakes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CreationOperationKey == operationKey,
                cancellationToken);
        if (existing is null)
        {
            return null;
        }

        EnsureRegisterReplay(existing, request);
        return new(Map(existing));
    }

    public async Task<ImageIntakeRecord> RegisterAsync(
        RegisterImageIntakeRequest request,
        CancellationToken cancellationToken)
    {
        ImageIntakeLifecycleRules.ValidateRegister(request);
        var operationKey = request.OperationKey.Trim();
        var vrm = request.NormalizedVehicleRegistration.Trim().ToUpperInvariant();
        var sourceChannel = ToChannelCode(request.Origin.SourceIdentity.Channel);
        var sourceToken = request.Origin.SourceIdentity.ExternalReceiptToken.Trim();
        var sourceHash = request.Origin.SourceHash.ToLowerInvariant();
        var requestFingerprint = RegisterFingerprint(request, vrm);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var now = timeProvider?.GetUtcNow() ?? TimeProvider.System.GetUtcNow();
        var replay = await context.ImageIntakes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CreationOperationKey == operationKey,
                cancellationToken);
        if (replay is not null)
        {
            EnsureRegisterReplay(replay, request);
            return Map(replay);
        }

        if (request.SubmissionGroupId is { } replayGroupId)
        {
            // One ImageIntake per submission group: a row already stamped
            // with this group is this registration, however it was keyed.
            var groupExisting = await context.ImageIntakes.AsNoTracking().SingleOrDefaultAsync(
                item => item.SubmissionGroupId == replayGroupId,
                cancellationToken);
            if (groupExisting is not null)
            {
                if (!string.Equals(groupExisting.NormalizedVehicleRegistration, vrm, StringComparison.Ordinal))
                {
                    throw new ImageIntakeOperationConflictException(
                        groupExisting.OriginReceiptId,
                        operationKey);
                }

                return Map(groupExisting);
            }
        }

        var existing = await context.ImageIntakes.SingleOrDefaultAsync(
            item => item.OriginReceiptId == request.Origin.ReceiptId
                || (item.SourceChannel == sourceChannel && item.ExternalReceiptToken == sourceToken),
            cancellationToken);
        if (existing is not null)
        {
            if (existing.OriginReceiptId != request.Origin.ReceiptId
                || existing.SourceChannel != sourceChannel
                || existing.ExternalReceiptToken != sourceToken
                || !string.Equals(existing.SourceHash, sourceHash, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(existing.NormalizedVehicleRegistration, vrm, StringComparison.Ordinal))
            {
                throw new IntakeSourceIdentityConflictException();
            }

            if (request.SubmissionGroupId is { } adoptGroupId && existing.SubmissionGroupId is null)
            {
                // The same identity was registered through the single-receipt
                // path (an ordinal-zero member whose group lookup missed)
                // before the group-scoped registration ran. Adopt the row
                // into the group so the sibling members converge on it
                // instead of staying stranded at Needs sorting.
                existing.SubmissionGroupId = adoptGroupId;
                await RegisterGroupMemberReceiptsAsync(
                    context,
                    adoptGroupId,
                    existing.OriginReceiptId,
                    existing.ImageIntakeReference,
                    request,
                    operationKey,
                    requestFingerprint,
                    now,
                    cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }

            return Map(existing);
        }

        var receipt = await context.IntakeReceipts
            .Include(item => item.InstructionDraft)
            .Include(item => item.Assets)
            .SingleOrDefaultAsync(
                item => item.Id == request.Origin.ReceiptId,
                cancellationToken)
            ?? throw new InvalidOperationException("The originating intake receipt does not exist.");
        if (receipt.SourceChannel != sourceChannel
            || receipt.ExternalReceiptToken != sourceToken
            || !string.Equals(receipt.SourceHash, sourceHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new IntakeSourceIdentityConflictException();
        }

        var evaluationExists = await context.IntakeEvaluations.AsNoTracking().AnyAsync(
            item => item.Id == request.Origin.EvaluationRevisionId
                && item.ProcessedReceiptId == request.Origin.ReceiptId,
            cancellationToken);
        if (!evaluationExists)
        {
            throw new InvalidOperationException(
                "The registering intake evaluation revision does not exist for the receipt.");
        }

        if (receipt.Decision != EfIntakeReceiptStore.ToCode(IntakeDecision.NeedsSorting)
            || !ImageIntakeLifecycleRules.IsImageOnlyMaterial(
                receipt.InstructionDraft is not null,
                EfIntakeReceiptStore.DeserializeFields(receipt.FieldsJson).Length,
                receipt.Assets.Select(asset => asset.MediaType)))
        {
            throw new InvalidOperationException(
                "Only an image-only intake receipt awaiting sorting can register an Image intake.");
        }

        var sequence = await context.ImageIntakeSequences.SingleOrDefaultAsync(
            item => item.NormalizedVehicleRegistration == vrm,
            cancellationToken);
        if (sequence is null)
        {
            sequence = new ImageIntakeSequenceEntity
            {
                NormalizedVehicleRegistration = vrm,
                LastAllocatedSequence = 0
            };
            context.ImageIntakeSequences.Add(sequence);
        }

        // Deliberately no ceiling: the reference format expands past `-99`
        // instead of exhausting, and a sequence value is never reused.
        var allocatedSequence = checked(++sequence.LastAllocatedSequence);
        var reference = ImageIntakeReferenceFormat.Create(vrm, allocatedSequence);
        var entity = new ImageIntakeEntity
        {
            Id = Guid.NewGuid(),
            OriginReceiptId = request.Origin.ReceiptId,
            SourceChannel = sourceChannel,
            ExternalReceiptToken = sourceToken,
            SourceHash = sourceHash,
            EvaluationRevisionId = request.Origin.EvaluationRevisionId,
            SubmissionGroupId = request.SubmissionGroupId,
            NormalizedVehicleRegistration = vrm,
            ImageIntakeReference = reference,
            CreatedAtUtc = now,
            CreatedByActorKind = request.Actor.Kind.ToString(),
            CreatedByActorSubjectId = request.Actor.SubjectId,
            Reason = request.Reason.Trim(),
            CreationOperationKey = operationKey,
            RequestFingerprint = requestFingerprint,
            LifecycleState = ToCode(ImageInitiatedCaseState.AwaitingInstruction),
            LifecycleVersion = 0,
            CustodyState = ImageCustodyStates.Pending
        };
        context.ImageIntakes.Add(entity);

        // Same durable outbox convention as EfCaseAcceptanceStore.AcceptAsync:
        // the Box folder for this Image-initiated Case is created by queued
        // external work, so an unreachable Box can never block registration.
        var custodyWorkId = Guid.NewGuid();
        context.ExternalWorkItems.Add(new ExternalWorkItemEntity
        {
            Id = custodyWorkId,
            ImageIntake = entity,
            ImageIntakeId = entity.Id,
            Kind = ExternalWorkKinds.CreateImageCaseCustody,
            OperationKey = $"image-case-custody:{entity.Id:N}",
            State = "pending",
            AttemptCount = 0,
            DueAtUtc = now,
            CaseRootCreationToken = CustodyCreationOwner.Create()
        });

        var beforeVersion = receipt.Version;
        var beforeJson = Snapshot(receipt);
        receipt.Decision = EfIntakeReceiptStore.ToCode(IntakeDecision.ImageIntakeRegistered);
        receipt.DecisionReason =
            $"Image intake {reference} was registered for this image-only material.";
        receipt.FailureCode = null;
        receipt.FailureReason = null;
        receipt.Version++;
        context.IntakeMutationHistory.Add(new IntakeMutationHistoryEntity
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            EventType = "image_intake_registered",
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(request.Actor.Roles.OrderBy(role => role)),
            Reason = request.Reason.Trim(),
            OperationKey = operationKey,
            RequestFingerprint = requestFingerprint,
            OccurredAtUtc = now,
            ExpectedIntakeVersion = beforeVersion,
            BeforeIntakeVersion = beforeVersion,
            AfterIntakeVersion = receipt.Version,
            BeforeJson = beforeJson,
            AfterJson = Snapshot(receipt)
        });

        if (request.SubmissionGroupId is { } submissionGroupId)
        {
            // The group is the registration unit: every image-only member
            // receipt still awaiting sorting moves to the registered decision
            // against the one reference, in this same transaction, so no
            // member is ever left looking unresolved after its group
            // resolved.
            await RegisterGroupMemberReceiptsAsync(
                context,
                submissionGroupId,
                request.Origin.ReceiptId,
                reference,
                request,
                operationKey,
                requestFingerprint,
                now,
                cancellationToken);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new IntakeVersionConflictException();
        }

        return Map(entity) with { PendingExternalWorkId = custodyWorkId };
    }

    /// <summary>
    /// Moves every image-only member receipt of the registered submission
    /// group that is still awaiting sorting to `ImageIntakeRegistered`
    /// against the group's one reference, each with its own mutation-history
    /// row. Members are resolved through the durable membership itself
    /// (group members → their staged receipts' latest evaluation → the
    /// processed receipt), never a caller-supplied list. A member that is
    /// not image-only (a mixed batch's instruction document) or already
    /// carries another decision stands untouched.
    /// </summary>
    private static async Task RegisterGroupMemberReceiptsAsync(
        PegasusDbContext context,
        Guid submissionGroupId,
        Guid originReceiptId,
        string imageIntakeReference,
        RegisterImageIntakeRequest request,
        string operationKey,
        string requestFingerprint,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var memberReceiptIds =
            (await ResolveGroupMemberReceiptsAsync(context, submissionGroupId, cancellationToken))
            .Select(pair => pair.ProcessedReceiptId)
            .Where(receiptId => receiptId != originReceiptId)
            .Distinct()
            .ToArray();
        if (memberReceiptIds.Length == 0)
        {
            return;
        }

        var receipts = await context.IntakeReceipts
            .Include(item => item.InstructionDraft)
            .Include(item => item.Assets)
            .Where(item => memberReceiptIds.Contains(item.Id))
            .ToArrayAsync(cancellationToken);
        foreach (var receipt in receipts)
        {
            if (receipt.Decision != EfIntakeReceiptStore.ToCode(IntakeDecision.NeedsSorting)
                || !ImageIntakeLifecycleRules.IsImageOnlyMaterial(
                    receipt.InstructionDraft is not null,
                    EfIntakeReceiptStore.DeserializeFields(receipt.FieldsJson).Length,
                    receipt.Assets.Select(asset => asset.MediaType)))
            {
                continue;
            }

            var beforeVersion = receipt.Version;
            var beforeJson = Snapshot(receipt);
            receipt.Decision = EfIntakeReceiptStore.ToCode(IntakeDecision.ImageIntakeRegistered);
            receipt.DecisionReason =
                $"Image intake {imageIntakeReference} was registered for this image-only material.";
            receipt.FailureCode = null;
            receipt.FailureReason = null;
            receipt.Version++;
            context.IntakeMutationHistory.Add(new IntakeMutationHistoryEntity
            {
                Id = Guid.NewGuid(),
                IntakeReceiptId = receipt.Id,
                IntakeReceipt = receipt,
                EventType = "image_intake_registered",
                ActorKind = request.Actor.Kind.ToString(),
                ActorSubjectId = request.Actor.SubjectId,
                ActorRolesJson = JsonSerializer.Serialize(request.Actor.Roles.OrderBy(role => role)),
                Reason = request.Reason.Trim(),
                OperationKey = $"{operationKey}:{receipt.Id:N}",
                RequestFingerprint = requestFingerprint,
                OccurredAtUtc = now,
                ExpectedIntakeVersion = beforeVersion,
                BeforeIntakeVersion = beforeVersion,
                AfterIntakeVersion = receipt.Version,
                BeforeJson = beforeJson,
                AfterJson = Snapshot(receipt)
            });
        }
    }

    /// <summary>
    /// Resolves a submission group's member receipts through the durable
    /// membership itself (member → latest evaluation → processed receipt),
    /// ordered by member ordinal. The one implementation of that rule:
    /// registration above and the queued image-case custody processor both
    /// resolve members through it.
    /// </summary>
    internal static async Task<IReadOnlyList<(int Ordinal, Guid ProcessedReceiptId)>>
        ResolveGroupMemberReceiptsAsync(
            PegasusDbContext context,
            Guid submissionGroupId,
            CancellationToken cancellationToken)
    {
        var members = await context.IntakeSubmissionGroupMembers
            .AsNoTracking()
            .Where(member => member.GroupId == submissionGroupId)
            .Select(member => new { member.Ordinal, member.StagedReceiptId })
            .ToArrayAsync(cancellationToken);
        if (members.Length == 0)
        {
            return [];
        }

        var stagedIds = members.Select(member => member.StagedReceiptId).ToArray();
        var evaluations = await context.IntakeEvaluations
            .AsNoTracking()
            .Where(evaluation => stagedIds.Contains(evaluation.StagedReceiptId))
            .Select(evaluation => new
            {
                evaluation.StagedReceiptId,
                evaluation.ProcessedReceiptId,
                evaluation.Revision
            })
            .ToArrayAsync(cancellationToken);
        var latestByStaged = evaluations
            .GroupBy(evaluation => evaluation.StagedReceiptId)
            .ToDictionary(
                grouping => grouping.Key,
                grouping => grouping
                    .OrderByDescending(evaluation => evaluation.Revision)
                    .First()
                    .ProcessedReceiptId);
        return members
            .Where(member => latestByStaged.ContainsKey(member.StagedReceiptId))
            .Select(member => (member.Ordinal, latestByStaged[member.StagedReceiptId]))
            .OrderBy(pair => pair.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// The one owner of the ordered receipt-id set an Image intake covers:
    /// the group members by submission ordinal (when the group is the
    /// registration unit) with the origin receipt first when it is not
    /// already among them. The custody payload loader and the gallery query
    /// both compose from this.
    /// </summary>
    internal static async Task<IReadOnlyList<Guid>> ResolveOrderedImageReceiptIdsAsync(
        PegasusDbContext context,
        Guid originReceiptId,
        Guid? submissionGroupId,
        CancellationToken cancellationToken)
    {
        var ordered = new List<Guid>();
        if (submissionGroupId is { } groupId)
        {
            ordered.AddRange(
                (await ResolveGroupMemberReceiptsAsync(context, groupId, cancellationToken))
                .Select(pair => pair.ProcessedReceiptId));
        }
        if (!ordered.Contains(originReceiptId))
        {
            ordered.Insert(0, originReceiptId);
        }
        return ordered.Distinct().ToArray();
    }

    public async Task EnsureRegisteredReceiptDecisionAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var registration = await FindForReceiptAsync(context, intakeReceiptId, cancellationToken);
        if (registration is null)
        {
            return;
        }

        var receipt = await context.IntakeReceipts.SingleOrDefaultAsync(
            item => item.Id == intakeReceiptId,
            cancellationToken);
        if (receipt is null
            || receipt.Decision != EfIntakeReceiptStore.ToCode(IntakeDecision.NeedsSorting))
        {
            return;
        }

        var beforeVersion = receipt.Version;
        var beforeJson = Snapshot(receipt);
        receipt.Decision = EfIntakeReceiptStore.ToCode(IntakeDecision.ImageIntakeRegistered);
        receipt.DecisionReason =
            $"Image intake {registration.ImageIntakeReference} remains registered for this image-only material.";
        receipt.FailureCode = null;
        receipt.FailureReason = null;
        receipt.Version++;
        context.IntakeMutationHistory.Add(new IntakeMutationHistoryEntity
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = receipt.Id,
            IntakeReceipt = receipt,
            EventType = "image_intake_registration_reasserted",
            ActorKind = "SystemWorker",
            ActorSubjectId = "image-intake-automation",
            ActorRolesJson = "[]",
            Reason = "The receipt decision was re-asserted after a policy re-evaluation; the registration is permanent.",
            OperationKey = $"image-intake-reassert:{Guid.NewGuid():N}",
            RequestFingerprint = registration.RequestFingerprint,
            OccurredAtUtc = timeProvider?.GetUtcNow() ?? TimeProvider.System.GetUtcNow(),
            ExpectedIntakeVersion = beforeVersion,
            BeforeIntakeVersion = beforeVersion,
            AfterIntakeVersion = receipt.Version,
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

    public Task<ImageIntakeRecord> MergeAsync(
        MergeImageInitiatedCaseRequest request,
        CancellationToken cancellationToken)
    {
        ImageIntakeLifecycleRules.ValidateMerge(request);
        return TransitionAsync(
            request.ImageIntakeId,
            request.ExpectedVersion,
            request.OperationKey,
            request.Actor,
            request.Reason,
            "merged_into_instruction_case",
            ImageInitiatedCaseState.MergedIntoInstructionCase,
            request.CaseId,
            cancellationToken);
    }

    public Task<ImageIntakeRecord> CloseAsync(
        CloseImageInitiatedCaseRequest request,
        CancellationToken cancellationToken)
    {
        ImageIntakeLifecycleRules.ValidateClose(request);
        return TransitionAsync(
            request.ImageIntakeId,
            request.ExpectedVersion,
            request.OperationKey,
            request.Actor,
            request.Reason,
            "staff_closed",
            ImageInitiatedCaseState.StaffClosed,
            null,
            cancellationToken);
    }

    /// <summary>
    /// The known principal is casework metadata, not registration identity and
    /// not a lifecycle transition: it writes no lifecycle event. The equality
    /// check runs before the active-principal check deliberately — a value that
    /// was legitimately recorded while its principal was active must survive a
    /// re-submission after that principal is deactivated, as a no-op. Only an
    /// actual change requires an active principal.
    /// </summary>
    /// <remarks>
    /// The write is guarded by <c>LifecycleVersion</c> rather than a token of
    /// its own: one Image Intake, one optimistic token. A principal save
    /// therefore does invalidate a concurrently-open Merge or Close form,
    /// which is the intended trade — those forms are reloaded, and a second
    /// token would let two staff members write the same record while each
    /// believed they held the current version. A same-value re-submission
    /// leaves the version alone, so the only edits that can invalidate a form
    /// are ones that genuinely changed the record.
    /// </remarks>
    public async Task<ImageIntakeRecord> SetPrincipalAsync(
        SetImageIntakePrincipalRequest request,
        CancellationToken cancellationToken)
    {
        ImageIntakeLifecycleRules.ValidateSetPrincipal(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var entity = await context.ImageIntakes.SingleOrDefaultAsync(
            item => item.Id == request.ImageIntakeId,
            cancellationToken)
            ?? throw new KeyNotFoundException($"Image intake '{request.ImageIntakeId}' was not found.");
        if (entity.LifecycleVersion != request.ExpectedVersion)
        {
            throw new DbUpdateConcurrencyException(
                "This Image Intake changed before the principal assignment.");
        }

        if (entity.PrincipalId == request.PrincipalId)
        {
            return Map(entity);
        }

        if (request.PrincipalId is { } principalId
            && !await context.Principals.AsNoTracking().AnyAsync(
                principal => principal.Id == principalId && principal.IsActive,
                cancellationToken))
        {
            throw new InvalidOperationException("The selected principal is not active.");
        }

        entity.PrincipalId = request.PrincipalId;
        entity.LifecycleVersion++;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<IReadOnlyList<Principal>> ListActivePrincipalsAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var principals = await context.Principals.AsNoTracking()
            .Where(principal => principal.IsActive)
            .OrderBy(principal => principal.Code)
            .ToArrayAsync(cancellationToken);
        return principals.Select(EfOrganizationAdministration.ToPrincipal).ToArray();
    }

    public async Task<IReadOnlyList<ImageIntakeLifecycleEvent>> ListHistoryAsync(
        Guid imageIntakeId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.ImageIntakeLifecycleEvents.AsNoTracking()
            .Where(item => item.ImageIntakeId == imageIntakeId)
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenByDescending(item => item.Id)
            .ToArrayAsync(cancellationToken);
        return rows.Select(Map).ToArray();
    }

    private async Task<ImageIntakeRecord> TransitionAsync(
        Guid imageIntakeId,
        long expectedVersion,
        string operationKey,
        ActionActor actor,
        string reason,
        string eventType,
        ImageInitiatedCaseState targetState,
        Guid? caseId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var operation = operationKey.Trim();
        var fingerprint = TransitionFingerprint(imageIntakeId, eventType, actor, reason, caseId);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await context.ImageIntakeLifecycleEvents.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == operation, cancellationToken);
        if (replay is not null)
        {
            var replayEntity = await context.ImageIntakes.AsNoTracking()
                .SingleAsync(item => item.Id == replay.ImageIntakeId, cancellationToken);
            if (replay.ImageIntakeId != imageIntakeId
                || !FingerprintEquals(replay.RequestFingerprint, fingerprint))
            {
                throw new ImageIntakeOperationConflictException(replayEntity.OriginReceiptId, operation);
            }

            return Map(replayEntity);
        }

        var entity = await context.ImageIntakes.SingleOrDefaultAsync(item => item.Id == imageIntakeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Image intake '{imageIntakeId}' was not found.");
        if (entity.LifecycleVersion != expectedVersion)
        {
            throw new DbUpdateConcurrencyException("The Image-initiated Case changed before this transition.");
        }
        ImageIntakeLifecycleRules.RequireTransitionable(ParseState(entity.LifecycleState));

        string? caseReference = null;
        if (caseId is { } targetCaseId)
        {
            caseReference = await context.Cases
                .AsNoTracking()
                .Where(item => item.Id == targetCaseId)
                .Select(item => item.Reference)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException($"Case '{targetCaseId}' does not exist.");
        }

        var now = timeProvider?.GetUtcNow() ?? TimeProvider.System.GetUtcNow();
        var before = entity.LifecycleVersion;
        entity.LifecycleState = ToCode(targetState);
        entity.LifecycleVersion++;
        entity.MergedIntoCaseId = caseId;
        entity.MergedIntoCaseReference = caseReference;
        entity.ClosureReason = targetState == ImageInitiatedCaseState.StaffClosed ? reason.Trim() : null;
        entity.ClosedAtUtc = targetState == ImageInitiatedCaseState.StaffClosed ? now : null;
        context.ImageIntakeLifecycleEvents.Add(new ImageIntakeLifecycleEventEntity
        {
            Id = Guid.NewGuid(),
            ImageIntakeId = entity.Id,
            EventType = eventType,
            ActorKind = actor.Kind.ToString(),
            ActorSubjectId = actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(actor.Roles.OrderBy(role => role)),
            Reason = reason.Trim(),
            OperationKey = operation,
            RequestFingerprint = fingerprint,
            OccurredAtUtc = now,
            BeforeVersion = before,
            AfterVersion = entity.LifecycleVersion,
            CaseId = caseId,
            CaseReference = caseReference
        });
        Guid? mergeWorkId = null;
        if (caseId is { } linkedCaseId)
        {
            // Fold the image-case Box folder into the paired case through the
            // same durable outbox that created it: the transition commits here
            // regardless of Box availability, and the queued work moves the
            // contents and removes the emptied folder (INTK-014).
            mergeWorkId = Guid.NewGuid();
            context.ExternalWorkItems.Add(new ExternalWorkItemEntity
            {
                Id = mergeWorkId.Value,
                ImageIntake = entity,
                ImageIntakeId = entity.Id,
                CaseId = linkedCaseId,
                Kind = ExternalWorkKinds.MergeImageCaseCustody,
                OperationKey = $"image-case-custody-merge:{entity.Id:N}",
                State = "pending",
                AttemptCount = 0,
                DueAtUtc = now
            });
            context.CaseHistory.Add(new CaseHistoryEntity
            {
                Id = Guid.NewGuid(),
                CaseId = linkedCaseId,
                EventType = "image_initiated_case_merged",
                Actor = actor.SubjectId,
                Reason = $"Image-initiated case {entity.ImageIntakeReference} merged into {caseReference}: {reason.Trim()}",
                OperationKey = $"{operation}:formal-case",
                OccurredAtUtc = now,
                BeforeVersion = null,
                AfterVersion = 0
            });
        }
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity) with { PendingExternalWorkId = mergeWorkId };
    }

    public async Task<IReadOnlyList<ImageIntakeSummary>> ListAsync(
        bool? associated,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await ProjectAsync(
            context.ImageIntakes.AsNoTracking().OrderByDescending(item => item.CreatedAtUtc),
            context,
            cancellationToken);
        return rows
            .Where(row => associated is null
                || (associated.Value ? row.AssociatedCaseId is not null : row.AssociatedCaseId is null))
            .ToArray();
    }

    public async Task<ImageIntakeDetail?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await GetDetailAsync(context, item => item.Id == id, cancellationToken);
    }

    public async Task<ImageIntakeDetail?> GetByReferenceAsync(
        string imageIntakeReference,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageIntakeReference))
        {
            return null;
        }

        var reference = imageIntakeReference.Trim().ToUpperInvariant();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await GetDetailAsync(
            context,
            item => item.ImageIntakeReference == reference,
            cancellationToken);
    }

    public async Task<ImageIntakeDetail?> GetByOriginReceiptAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await FindForReceiptAsync(context, intakeReceiptId, cancellationToken);
        return entity is null
            ? null
            : await ToDetailAsync(context, entity, entity.Principal?.Code, cancellationToken);
    }

    /// <summary>
    /// Resolves the ImageIntake a receipt belongs to: its own origin
    /// registration, or — when the receipt is a member of a submission group
    /// registered as one unit — the group's single registration, reached
    /// through the durable membership itself (the receipt's evaluations name
    /// its staged receipt, the staged receipt names its group member row,
    /// and the group id names the group-stamped intake).
    /// </summary>
    private static async Task<ImageIntakeEntity?> FindForReceiptAsync(
        PegasusDbContext context,
        Guid intakeReceiptId,
        CancellationToken cancellationToken)
    {
        var entity = await context.ImageIntakes
            .AsNoTracking()
            .Include(item => item.Principal)
            .SingleOrDefaultAsync(
                item => item.OriginReceiptId == intakeReceiptId,
                cancellationToken);
        if (entity is not null)
        {
            return entity;
        }

        return await (
            from evaluation in context.IntakeEvaluations.AsNoTracking()
            where evaluation.ProcessedReceiptId == intakeReceiptId
            join member in context.IntakeSubmissionGroupMembers.AsNoTracking()
                on evaluation.StagedReceiptId equals member.StagedReceiptId
            join intake in context.ImageIntakes.AsNoTracking().Include(item => item.Principal)
                on (Guid?)member.GroupId equals intake.SubmissionGroupId
            select intake)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ImageIntakeSummary>> ListByOriginReceiptsAsync(
        IReadOnlyCollection<Guid> intakeReceiptIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intakeReceiptIds);
        if (intakeReceiptIds.Count == 0)
        {
            return [];
        }

        var ids = intakeReceiptIds.ToArray();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await ProjectAsync(
            context.ImageIntakes.AsNoTracking().Where(item => ids.Contains(item.OriginReceiptId)),
            context,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ImageIntakeSummary>> ListForCaseAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await ProjectAsync(
            context.ImageIntakes.AsNoTracking(),
            context,
            cancellationToken);
        return rows.Where(row => row.AssociatedCaseId == caseId).ToArray();
    }

    public async Task<IReadOnlyList<ImageIntakeImage>> ListImagesAsync(
        Guid imageIntakeId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var intake = await context.ImageIntakes
            .AsNoTracking()
            .Where(item => item.Id == imageIntakeId)
            .Select(item => new { item.OriginReceiptId, item.SubmissionGroupId })
            .SingleOrDefaultAsync(cancellationToken);
        if (intake is null)
        {
            return [];
        }

        var receiptIds = await ResolveOrderedImageReceiptIdsAsync(
            context,
            intake.OriginReceiptId,
            intake.SubmissionGroupId,
            cancellationToken);
        var registeredDecision = EfIntakeReceiptStore.ToCode(IntakeDecision.ImageIntakeRegistered);
        // The image rule's owner is ImageIntakeLifecycle.IsImageOnlyMaterial;
        // this projection cites its prefix because SQL cannot run it.
        var rows = await context.IntakeAssets
            .AsNoTracking()
            .Where(asset => receiptIds.Contains(asset.IntakeReceiptId)
                && asset.Kind == "source"
                && asset.Disposition == "source"
                && asset.MediaType.StartsWith(ImageIntakeLifecycleRules.ImageMediaTypePrefix))
            .Join(
                context.IntakeReceipts.AsNoTracking()
                    .Where(receipt => receipt.Decision == registeredDecision),
                asset => asset.IntakeReceiptId,
                receipt => receipt.Id,
                (asset, receipt) => new { asset.IntakeReceiptId, asset.FileName, asset.MediaType })
            .ToArrayAsync(cancellationToken);
        var byReceipt = rows.ToDictionary(row => row.IntakeReceiptId);
        var images = new List<ImageIntakeImage>(rows.Length);
        foreach (var receiptId in receiptIds)
        {
            if (byReceipt.TryGetValue(receiptId, out var row))
            {
                images.Add(new(receiptId, row.FileName, row.MediaType));
            }
        }
        return images;
    }

    public async Task<IReadOnlyList<ImageIntakeSummary>> SearchByRegistrationAsync(
        string normalizedVehicleRegistration,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(normalizedVehicleRegistration))
        {
            return [];
        }

        var vrm = normalizedVehicleRegistration.Trim().ToUpperInvariant();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await ProjectAsync(
            context.ImageIntakes
                .AsNoTracking()
                .Where(item => item.NormalizedVehicleRegistration == vrm)
                .OrderByDescending(item => item.CreatedAtUtc),
            context,
            cancellationToken);
    }

    /// <summary>
    /// The principal code comes out of the same read as a LEFT JOIN through the
    /// navigation rather than a follow-up lookup, so a detail read stays one
    /// round trip whether or not a principal is recorded.
    /// </summary>
    private static async Task<ImageIntakeDetail?> GetDetailAsync(
        PegasusDbContext context,
        System.Linq.Expressions.Expression<Func<ImageIntakeEntity, bool>> predicate,
        CancellationToken cancellationToken)
    {
        var row = await context.ImageIntakes
            .AsNoTracking()
            .Where(predicate)
            .Select(intake => new
            {
                Intake = intake,
                PrincipalCode = intake.Principal != null ? intake.Principal.Code : null
            })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : await ToDetailAsync(context, row.Intake, row.PrincipalCode, cancellationToken);
    }

    private static async Task<ImageIntakeDetail> ToDetailAsync(
        PegasusDbContext context,
        ImageIntakeEntity entity,
        string? principalCode,
        CancellationToken cancellationToken)
    {
        var association = await AssociationAsync(context, entity.OriginReceiptId, cancellationToken);
        return new ImageIntakeDetail(
            Map(entity),
            entity.CreatedAtUtc,
            association?.CaseId,
            association?.CaseReference,
            ParseCustodyState(entity.CustodyState),
            principalCode);
    }

    private static async Task<IReadOnlyList<ImageIntakeSummary>> ProjectAsync(
        IQueryable<ImageIntakeEntity> query,
        PegasusDbContext context,
        CancellationToken cancellationToken)
    {
        var registeredDecision = EfIntakeReceiptStore.ToCode(IntakeDecision.ImageIntakeRegistered);
        var rows = await query
            .Select(intake => new
            {
                intake.Id,
                intake.OriginReceiptId,
                intake.ImageIntakeReference,
                intake.NormalizedVehicleRegistration,
                intake.CreatedAtUtc,
                intake.SourceChannel,
                intake.CustodyState,
                intake.LifecycleState,
                intake.ClosureReason,
                // The one set-based projection carries the principal code as a
                // LEFT JOIN: no per-row read, so the queue's read count does not
                // grow with the number of rows.
                PrincipalCode = intake.Principal != null ? intake.Principal.Code : null,
                Association = context.IntakeManualAssociations
                    .Where(association => association.IntakeReceiptId == intake.OriginReceiptId)
                    .Select(association => new { association.IsActive, association.CaseId })
                    .FirstOrDefault(),
                AcceptedCaseId = context.CaseIntakeLinks
                    .Where(link => link.IntakeReceiptId == intake.OriginReceiptId)
                    .Select(link => (Guid?)link.CaseId)
                    .FirstOrDefault(),
                ImageCount = context.IntakeAssets.Count(asset =>
                    asset.Kind == "source"
                    && asset.Disposition == "source"
                    && asset.MediaType.StartsWith(ImageIntakeLifecycleRules.ImageMediaTypePrefix)
                    && context.IntakeReceipts.Any(receipt =>
                        receipt.Id == asset.IntakeReceiptId
                        && receipt.Decision == registeredDecision)
                    && (asset.IntakeReceiptId == intake.OriginReceiptId
                        || (intake.SubmissionGroupId != null
                            && context.IntakeEvaluations.Any(evaluation =>
                                evaluation.ProcessedReceiptId == asset.IntakeReceiptId
                                && context.IntakeSubmissionGroupMembers.Any(member =>
                                    member.GroupId == intake.SubmissionGroupId
                                    && member.StagedReceiptId == evaluation.StagedReceiptId)))))
            })
            .ToArrayAsync(cancellationToken);
        var associatedCaseIds = rows
            .Select(row => CurrentCaseId(
                row.Association?.IsActive,
                row.Association?.CaseId,
                row.AcceptedCaseId))
            .Where(caseId => caseId is not null)
            .Select(caseId => caseId!.Value)
            .Distinct()
            .ToArray();
        var references = associatedCaseIds.Length == 0
            ? []
            : await context.Cases
                .AsNoTracking()
                .Where(caseEntity => associatedCaseIds.Contains(caseEntity.Id))
                .ToDictionaryAsync(
                    caseEntity => caseEntity.Id,
                    caseEntity => caseEntity.Reference,
                    cancellationToken);
        return rows
            .Select(row =>
            {
                var caseId = CurrentCaseId(
                    row.Association?.IsActive,
                    row.Association?.CaseId,
                    row.AcceptedCaseId);
                return new ImageIntakeSummary(
                    row.Id,
                    row.OriginReceiptId,
                    row.ImageIntakeReference,
                    row.NormalizedVehicleRegistration,
                    caseId,
                    caseId is { } id && references.TryGetValue(id, out var reference)
                        ? reference
                        : null,
                    row.CreatedAtUtc,
                    ParseCustodyState(row.CustodyState),
                    ParseState(row.LifecycleState),
                    row.ClosureReason,
                    row.ImageCount,
                    ParseChannel(row.SourceChannel),
                    row.PrincipalCode);
            })
            .ToArray();
    }

    private static async Task<(Guid CaseId, string CaseReference)?> AssociationAsync(
        PegasusDbContext context,
        Guid originReceiptId,
        CancellationToken cancellationToken)
    {
        var association = await context.IntakeManualAssociations
            .AsNoTracking()
            .Where(item => item.IntakeReceiptId == originReceiptId)
            .Select(item => new { item.IsActive, item.CaseId })
            .SingleOrDefaultAsync(cancellationToken);
        var acceptedCaseId = await context.CaseIntakeLinks
            .AsNoTracking()
            .Where(item => item.IntakeReceiptId == originReceiptId)
            .Select(item => (Guid?)item.CaseId)
            .SingleOrDefaultAsync(cancellationToken);
        var caseId = CurrentCaseId(association?.IsActive, association?.CaseId, acceptedCaseId);
        if (caseId is null)
        {
            return null;
        }

        var reference = await context.Cases
            .AsNoTracking()
            .Where(item => item.Id == caseId.Value)
            .Select(item => item.Reference)
            .SingleAsync(cancellationToken);
        return (caseId.Value, reference);
    }

    /// <summary>
    /// Mirrors <c>IntakeReceipt.CurrentCaseId</c>: once any manual association
    /// exists it owns the current link (active → its case, reversed → none);
    /// otherwise an accepted origin link applies.
    /// </summary>
    private static Guid? CurrentCaseId(bool? manualIsActive, Guid? manualCaseId, Guid? acceptedCaseId) =>
        manualIsActive is null
            ? acceptedCaseId
            : manualIsActive.Value
                ? manualCaseId
                : null;

    private static ImageCustodyState? ParseCustodyState(string? state) => state switch
    {
        null => null,
        ImageCustodyStates.Pending => ImageCustodyState.Pending,
        ImageCustodyStates.Confirmed => ImageCustodyState.Confirmed,
        ImageCustodyStates.Merged => ImageCustodyState.Merged,
        ImageCustodyStates.Failed => ImageCustodyState.Failed,
        _ => throw new InvalidOperationException($"Unknown image custody state '{state}'.")
    };

    private static void EnsureRegisterReplay(ImageIntakeEntity entity, RegisterImageIntakeRequest request)
    {
        var vrm = request.NormalizedVehicleRegistration.Trim().ToUpperInvariant();
        var fingerprint = RegisterFingerprint(request, vrm);
        if (entity.OriginReceiptId != request.Origin.ReceiptId
            || !FingerprintEquals(entity.RequestFingerprint, fingerprint))
        {
            throw new ImageIntakeOperationConflictException(
                request.Origin.ReceiptId,
                request.OperationKey.Trim());
        }
    }

    private static bool FingerprintEquals(string retained, string supplied)
    {
        var left = Encoding.UTF8.GetBytes(retained);
        var right = Encoding.UTF8.GetBytes(supplied);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private static string RegisterFingerprint(RegisterImageIntakeRequest request, string vrm) =>
        Hash(string.Join(
            '|',
            "image_intake_register",
            request.Origin.ReceiptId.ToString("N"),
            ToChannelCode(request.Origin.SourceIdentity.Channel),
            request.Origin.SourceIdentity.ExternalReceiptToken.Trim(),
            request.Origin.SourceHash.ToLowerInvariant(),
            request.Origin.EvaluationRevisionId.ToString("N"),
            vrm,
            request.Actor.Kind.ToString(),
            request.Actor.SubjectId,
            request.Reason.Trim())
            // Appended only when a group is present so every fingerprint
            // recorded before group registration existed stays replayable.
            + (request.SubmissionGroupId is { } groupId ? $"|group:{groupId:N}" : string.Empty));

    /// <summary>
    /// Mirrors <see cref="RegisterFingerprint"/> for a lifecycle transition: a
    /// replayed operation key must carry the exact same command, or it is a
    /// conflicting reuse rather than a retry.
    /// </summary>
    private static string TransitionFingerprint(
        Guid imageIntakeId,
        string eventType,
        ActionActor actor,
        string reason,
        Guid? caseId) =>
        Hash(string.Join(
            '|',
            "image_intake_transition",
            imageIntakeId.ToString("N"),
            eventType,
            actor.Kind.ToString(),
            actor.SubjectId,
            reason.Trim(),
            caseId?.ToString("N") ?? string.Empty));

    private static string Snapshot(IntakeReceiptEntity receipt) => JsonSerializer.Serialize(new
    {
        receipt.Id,
        receipt.Decision,
        receipt.DecisionReason,
        receipt.Version
    });

    private static string Hash(string material) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material))).ToLowerInvariant();

    private static string ToChannelCode(IntakeSourceChannel channel) => channel switch
    {
        IntakeSourceChannel.ManualUpload => "manual_upload",
        IntakeSourceChannel.Mailbox => "mailbox",
        IntakeSourceChannel.ProviderApi => "provider_api",
        _ => throw new InvalidOperationException($"Unknown intake source channel value '{(int)channel}'.")
    };

    private static IntakeSourceChannel ParseChannel(string value) => value switch
    {
        "manual_upload" => IntakeSourceChannel.ManualUpload,
        "mailbox" => IntakeSourceChannel.Mailbox,
        "provider_api" => IntakeSourceChannel.ProviderApi,
        _ => throw new InvalidDataException($"Unknown intake source channel code '{value}'.")
    };

    /// <summary>
    /// Reused by <c>EfDashboardQueries</c> so the Not ready count agrees with
    /// this store's own definition of "image-initiated, awaiting
    /// instruction" instead of duplicating the state-code literal.
    /// </summary>
    internal static string ToCode(ImageInitiatedCaseState state) => state switch
    {
        ImageInitiatedCaseState.AwaitingInstruction => "awaiting_instruction",
        ImageInitiatedCaseState.MergedIntoInstructionCase => "merged_into_instruction_case",
        ImageInitiatedCaseState.StaffClosed => "staff_closed",
        _ => throw new InvalidDataException($"Unknown Image-initiated state '{state}'.")
    };

    private static ImageInitiatedCaseState ParseState(string state) => state switch
    {
        "awaiting_instruction" => ImageInitiatedCaseState.AwaitingInstruction,
        "merged_into_instruction_case" => ImageInitiatedCaseState.MergedIntoInstructionCase,
        "staff_closed" => ImageInitiatedCaseState.StaffClosed,
        _ => throw new InvalidDataException($"Unknown Image-initiated state '{state}'.")
    };

    private static ImageIntakeLifecycleEvent Map(ImageIntakeLifecycleEventEntity entity) => new(
        entity.Id,
        entity.ImageIntakeId,
        entity.EventType,
        ParseActor(entity.ActorKind, entity.ActorSubjectId, entity.ActorRolesJson),
        entity.OccurredAtUtc,
        entity.Reason,
        entity.OperationKey,
        entity.BeforeVersion,
        entity.AfterVersion,
        entity.CaseId,
        entity.CaseReference);

    private static ActionActor ParseActor(string kind, string subjectId, string rolesJson)
    {
        var actorKind = Enum.Parse<ActorKind>(kind, ignoreCase: false);
        return actorKind switch
        {
            ActorKind.Staff => ActionActor.Staff(
                Guid.Parse(subjectId),
                JsonSerializer.Deserialize<StaffRole[]>(rolesJson) ?? []),
            ActorKind.SystemWorker => ActionActor.SystemWorker(subjectId),
            ActorKind.Automation => ActionActor.Automation(subjectId),
            ActorKind.RequestLink => ActionActor.RequestLink(Guid.Parse(subjectId)),
            _ => throw new InvalidDataException($"Unknown actor kind '{kind}'.")
        };
    }

    private static ImageIntakeRecord Map(ImageIntakeEntity entity) => new(
        entity.Id,
        new ImageIntakeOrigin(
            entity.OriginReceiptId,
            new IntakeSourceIdentity(ParseChannel(entity.SourceChannel), entity.ExternalReceiptToken),
            entity.SourceHash,
            entity.EvaluationRevisionId),
        entity.NormalizedVehicleRegistration,
        entity.ImageIntakeReference,
        ParseState(entity.LifecycleState),
        entity.MergedIntoCaseId,
        entity.MergedIntoCaseReference,
        entity.ClosureReason,
        entity.ClosedAtUtc,
        entity.LifecycleVersion,
        entity.SubmissionGroupId,
        PrincipalId: entity.PrincipalId);
}

public sealed class EfImageIntakeOriginResolver(
    IDbContextFactory<PegasusDbContext> contextFactory) : IImageIntakeOriginResolver
{
    public async Task<ImageIntakeOrigin?> ResolveOriginAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken)
    {
        if (intakeReceiptId == Guid.Empty)
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var receipt = await context.IntakeReceipts
            .AsNoTracking()
            .Where(item => item.Id == intakeReceiptId)
            .Select(item => new { item.SourceChannel, item.ExternalReceiptToken, item.SourceHash })
            .SingleOrDefaultAsync(cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        var evaluationRevisionId = await context.IntakeEvaluations
            .AsNoTracking()
            .Where(item => item.ProcessedReceiptId == intakeReceiptId)
            .OrderByDescending(item => item.Revision)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (evaluationRevisionId is null)
        {
            return null;
        }

        var channel = receipt.SourceChannel switch
        {
            "manual_upload" => IntakeSourceChannel.ManualUpload,
            "mailbox" => IntakeSourceChannel.Mailbox,
            "provider_api" => IntakeSourceChannel.ProviderApi,
            _ => throw new InvalidDataException(
                $"Unknown intake source channel code '{receipt.SourceChannel}'.")
        };
        return new ImageIntakeOrigin(
            intakeReceiptId,
            new IntakeSourceIdentity(channel, receipt.ExternalReceiptToken),
            receipt.SourceHash,
            evaluationRevisionId.Value);
    }
}

public sealed class EfImageIntakeCaseCandidates(
    IDbContextFactory<PegasusDbContext> contextFactory) : IImageIntakeCaseCandidates
{
    private static readonly string[] EligibleStates =
        ["NotReady", "Held", "Review", "ReportPreparation"];

    public async Task<IReadOnlyList<ImageIntakeCaseCandidate>> FindEligibleByRegistrationAsync(
        string normalizedVehicleRegistration,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(normalizedVehicleRegistration))
        {
            return [];
        }

        var read = normalizedVehicleRegistration.Trim().ToUpperInvariant();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // The one-missing-character rule cannot translate to SQL; the
        // eligible pre-report set is small, so match in memory over the
        // normalised confirmed registrations.
        var eligible = await (
            from workflow in context.CaseWorkflows.AsNoTracking()
            join caseEntity in context.Cases.AsNoTracking()
                on workflow.CaseId equals caseEntity.Id
            join draft in context.InstructionDrafts.AsNoTracking()
                on caseEntity.OriginIntakeReceiptId equals draft.IntakeReceiptId
            where EligibleStates.Contains(workflow.State)
                && workflow.ReportSentEvidenceId == null
                && workflow.ArchivedAtUtc == null
                && draft.VehicleRegistration != null
            orderby caseEntity.Reference
            select new
            {
                caseEntity.Id,
                caseEntity.Reference,
                workflow.Version,
                Registration = draft.VehicleRegistration!
            })
            .ToArrayAsync(cancellationToken);
        return eligible
            .Select(candidate => new
            {
                candidate,
                Normalized = new string(candidate.Registration
                    .ToUpperInvariant()
                    .Where(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character))
                    .ToArray())
            })
            .Where(item => item.Normalized.Length > 0
                && VrmRegistrationMatching.IsMatch(read, item.Normalized))
            .Select(item => new ImageIntakeCaseCandidate(
                item.candidate.Id,
                item.candidate.Reference,
                item.candidate.Version,
                item.Normalized))
            .ToArray();
    }
}

public sealed class EfImageVrmSuggestionStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider? timeProvider = null) : IVrmSuggestionStore
{
    public async Task<ImageVrmSuggestion> RecordAsync(
        ImageVrmSuggestionDraft draft,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(draft.OperationKey);
        var operationKey = draft.OperationKey.Trim();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var existing = await context.ImageVrmSuggestions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == operationKey, cancellationToken);
        if (existing is not null)
        {
            return Map(existing);
        }

        var entity = new ImageVrmSuggestionEntity
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = draft.IntakeReceiptId,
            IntakeAssetId = draft.IntakeAssetId,
            StorageKey = draft.StorageKey,
            ContentHash = draft.ContentHash.ToLowerInvariant(),
            EngineKey = draft.EngineKey,
            EngineVersion = draft.EngineVersion,
            ModelHashes = draft.ModelHashes,
            Outcome = ToCode(draft.Outcome),
            SuggestedRegistration = draft.SuggestedRegistration,
            Confidence = draft.Confidence,
            FailureCode = draft.FailureCode,
            FailureReason = draft.FailureReason,
            OccurredAtUtc = timeProvider?.GetUtcNow() ?? TimeProvider.System.GetUtcNow(),
            OperationKey = operationKey,
            Disposition = ToCode(ImageVrmSuggestionDisposition.Pending)
        };
        context.ImageVrmSuggestions.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<IReadOnlyList<ImageVrmSuggestion>> ListForReceiptAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.ImageVrmSuggestions
            .AsNoTracking()
            .Where(item => item.IntakeReceiptId == intakeReceiptId)
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        return rows.Select(Map).ToArray();
    }

    public async Task<ImageVrmSuggestion?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.ImageVrmSuggestions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<ImageVrmSuggestion> SetDispositionAsync(
        ImageVrmSuggestionDispositionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OperationKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Reason);
        if (request.Disposition == ImageVrmSuggestionDisposition.Pending)
        {
            throw new ArgumentException(
                "A suggestion disposition cannot return to pending.",
                nameof(request));
        }

        var operationKey = request.OperationKey.Trim();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var entity = await context.ImageVrmSuggestions.SingleOrDefaultAsync(
            item => item.Id == request.SuggestionId,
            cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Image VRM suggestion '{request.SuggestionId}' was not found.");
        if (string.Equals(entity.DispositionOperationKey, operationKey, StringComparison.Ordinal))
        {
            return Map(entity);
        }

        if (entity.Disposition != ToCode(ImageVrmSuggestionDisposition.Pending))
        {
            throw new InvalidOperationException(
                "The suggestion already has a recorded staff disposition.");
        }

        entity.Disposition = ToCode(request.Disposition);
        entity.DispositionActor = $"{request.Actor.Kind}:{request.Actor.SubjectId}";
        entity.DispositionReason = request.Reason.Trim();
        entity.DispositionOperationKey = operationKey;
        entity.DisposedAtUtc = timeProvider?.GetUtcNow() ?? TimeProvider.System.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    private static string ToCode(VrmRecognitionOutcomeKind value) => value switch
    {
        VrmRecognitionOutcomeKind.Suggested => "suggested",
        VrmRecognitionOutcomeKind.NoReadableResult => "no_readable_result",
        VrmRecognitionOutcomeKind.TechnicalFailure => "technical_failure",
        VrmRecognitionOutcomeKind.Unavailable => "unavailable",
        _ => throw new InvalidOperationException($"Unknown recognition outcome value '{(int)value}'.")
    };

    private static VrmRecognitionOutcomeKind ParseOutcome(string value) => value switch
    {
        "suggested" => VrmRecognitionOutcomeKind.Suggested,
        "no_readable_result" => VrmRecognitionOutcomeKind.NoReadableResult,
        "technical_failure" => VrmRecognitionOutcomeKind.TechnicalFailure,
        "unavailable" => VrmRecognitionOutcomeKind.Unavailable,
        _ => throw new InvalidDataException($"Unknown recognition outcome code '{value}'.")
    };

    private static string ToCode(ImageVrmSuggestionDisposition value) => value switch
    {
        ImageVrmSuggestionDisposition.Pending => "pending",
        ImageVrmSuggestionDisposition.Confirmed => "confirmed",
        ImageVrmSuggestionDisposition.Dismissed => "dismissed",
        _ => throw new InvalidOperationException($"Unknown suggestion disposition value '{(int)value}'.")
    };

    private static ImageVrmSuggestionDisposition ParseDisposition(string value) => value switch
    {
        "pending" => ImageVrmSuggestionDisposition.Pending,
        "confirmed" => ImageVrmSuggestionDisposition.Confirmed,
        "dismissed" => ImageVrmSuggestionDisposition.Dismissed,
        _ => throw new InvalidDataException($"Unknown suggestion disposition code '{value}'.")
    };

    private static ImageVrmSuggestion Map(ImageVrmSuggestionEntity entity) => new(
        entity.Id,
        entity.IntakeReceiptId,
        entity.IntakeAssetId,
        entity.StorageKey,
        entity.ContentHash,
        entity.EngineKey,
        entity.EngineVersion,
        entity.ModelHashes,
        ParseOutcome(entity.Outcome),
        entity.SuggestedRegistration,
        entity.Confidence,
        entity.FailureCode,
        entity.FailureReason,
        entity.OccurredAtUtc,
        ParseDisposition(entity.Disposition),
        entity.DispositionActor,
        entity.DispositionReason,
        entity.DisposedAtUtc);
}
