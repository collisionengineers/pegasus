using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
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

        var existing = await context.ImageIntakes.AsNoTracking().SingleOrDefaultAsync(
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
        var now = timeProvider?.GetUtcNow() ?? TimeProvider.System.GetUtcNow();
        var entity = new ImageIntakeEntity
        {
            Id = Guid.NewGuid(),
            OriginReceiptId = request.Origin.ReceiptId,
            SourceChannel = sourceChannel,
            ExternalReceiptToken = sourceToken,
            SourceHash = sourceHash,
            EvaluationRevisionId = request.Origin.EvaluationRevisionId,
            NormalizedVehicleRegistration = vrm,
            ImageIntakeReference = reference,
            CreatedAtUtc = now,
            CreatedByActorKind = request.Actor.Kind.ToString(),
            CreatedByActorSubjectId = request.Actor.SubjectId,
            Reason = request.Reason.Trim(),
            CreationOperationKey = operationKey,
            RequestFingerprint = requestFingerprint
        };
        context.ImageIntakes.Add(entity);

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

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new IntakeVersionConflictException();
        }

        return Map(entity);
    }

    public async Task EnsureRegisteredReceiptDecisionAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var registration = await context.ImageIntakes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.OriginReceiptId == intakeReceiptId,
                cancellationToken);
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
        return await GetDetailAsync(
            context,
            item => item.OriginReceiptId == intakeReceiptId,
            cancellationToken);
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

    private static async Task<ImageIntakeDetail?> GetDetailAsync(
        PegasusDbContext context,
        System.Linq.Expressions.Expression<Func<ImageIntakeEntity, bool>> predicate,
        CancellationToken cancellationToken)
    {
        var entity = await context.ImageIntakes
            .AsNoTracking()
            .SingleOrDefaultAsync(predicate, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var association = await AssociationAsync(context, entity.OriginReceiptId, cancellationToken);
        return new ImageIntakeDetail(
            Map(entity),
            entity.CreatedAtUtc,
            association?.CaseId,
            association?.CaseReference);
    }

    private static async Task<IReadOnlyList<ImageIntakeSummary>> ProjectAsync(
        IQueryable<ImageIntakeEntity> query,
        PegasusDbContext context,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .Select(intake => new
            {
                intake.Id,
                intake.OriginReceiptId,
                intake.ImageIntakeReference,
                intake.NormalizedVehicleRegistration,
                intake.CreatedAtUtc,
                Association = context.IntakeManualAssociations
                    .Where(association => association.IntakeReceiptId == intake.OriginReceiptId)
                    .Select(association => new { association.IsActive, association.CaseId })
                    .FirstOrDefault(),
                AcceptedCaseId = context.CaseIntakeLinks
                    .Where(link => link.IntakeReceiptId == intake.OriginReceiptId)
                    .Select(link => (Guid?)link.CaseId)
                    .FirstOrDefault()
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
                    row.CreatedAtUtc);
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

    private static void EnsureRegisterReplay(ImageIntakeEntity entity, RegisterImageIntakeRequest request)
    {
        var vrm = request.NormalizedVehicleRegistration.Trim().ToUpperInvariant();
        var fingerprint = RegisterFingerprint(request, vrm);
        var retained = Encoding.UTF8.GetBytes(entity.RequestFingerprint);
        var supplied = Encoding.UTF8.GetBytes(fingerprint);
        if (entity.OriginReceiptId != request.Origin.ReceiptId
            || retained.Length != supplied.Length
            || !CryptographicOperations.FixedTimeEquals(retained, supplied))
        {
            throw new ImageIntakeOperationConflictException(
                request.Origin.ReceiptId,
                request.OperationKey.Trim());
        }
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
            request.Reason.Trim()));

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
        _ => throw new InvalidOperationException($"Unknown intake source channel value '{(int)channel}'.")
    };

    private static IntakeSourceChannel ParseChannel(string value) => value switch
    {
        "manual_upload" => IntakeSourceChannel.ManualUpload,
        "mailbox" => IntakeSourceChannel.Mailbox,
        _ => throw new InvalidDataException($"Unknown intake source channel code '{value}'.")
    };

    private static ImageIntakeRecord Map(ImageIntakeEntity entity) => new(
        entity.Id,
        new ImageIntakeOrigin(
            entity.OriginReceiptId,
            new IntakeSourceIdentity(ParseChannel(entity.SourceChannel), entity.ExternalReceiptToken),
            entity.SourceHash,
            entity.EvaluationRevisionId),
        entity.NormalizedVehicleRegistration,
        entity.ImageIntakeReference);
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
