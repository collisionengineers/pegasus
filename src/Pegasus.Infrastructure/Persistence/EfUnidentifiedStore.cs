using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfUnidentifiedStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IUnidentifiedStore
{
    public async Task<UnidentifiedRegisterResult?> ProbeRegisterReplayAsync(
        RegisterUnidentifiedRequest request,
        CancellationToken cancellationToken = default)
    {
        UnidentifiedValidation.ValidateRegister(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<UnidentifiedItemEntity>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.RegistrationOperationKey == request.OperationKey.Trim(), cancellationToken);
        if (entity is null)
        {
            return null;
        }

        EnsureRegistrationReplay(entity, request);
        return new(Map(entity), true);
    }

    public async Task<UnidentifiedRegisterResult> RegisterAsync(
        RegisterUnidentifiedRequest request,
        CancellationToken cancellationToken = default)
    {
        UnidentifiedValidation.ValidateRegister(request);
        var operationKey = request.OperationKey.Trim();
        var fingerprint = Fingerprint(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var byOperation = await context.Set<UnidentifiedItemEntity>().SingleOrDefaultAsync(
            item => item.RegistrationOperationKey == operationKey, cancellationToken);
        if (byOperation is not null)
        {
            EnsureRegistrationReplay(byOperation, request);
            return new(Map(byOperation), true);
        }

        var originKind = request.Origin.Kind.ToString();
        var existing = await context.Set<UnidentifiedItemEntity>().SingleOrDefaultAsync(
            item => item.OriginKind == originKind && item.OriginId == request.Origin.Id,
            cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.RegistrationFingerprint, fingerprint, StringComparison.Ordinal))
            {
                throw new UnidentifiedOperationConflictException();
            }

            return new(Map(existing), true);
        }

        var sequence = await context.Set<UnidentifiedSequenceEntity>().SingleOrDefaultAsync(
            item => item.Id == 1, cancellationToken);
        if (sequence is null)
        {
            sequence = new UnidentifiedSequenceEntity { Id = 1, LastAllocatedSequence = 0 };
            context.Set<UnidentifiedSequenceEntity>().Add(sequence);
        }

        var allocated = checked(++sequence.LastAllocatedSequence);
        var entity = new UnidentifiedItemEntity
        {
            Id = Guid.NewGuid(),
            Sequence = allocated,
            Reference = UnidentifiedReferenceFormat.Create(allocated),
            OriginKind = originKind,
            OriginId = request.Origin.Id,
            ReasonCode = request.ReasonCode.ToString(),
            SafeDetail = request.SafeDetail.Trim(),
            State = UnidentifiedState.Open.ToString(),
            CreatedAtUtc = request.CreatedAtUtc,
            CreatedByActorKind = request.Actor.Kind.ToString(),
            CreatedByActorSubjectId = request.Actor.SubjectId,
            CreatedByActorRolesJson = JsonSerializer.Serialize(request.Actor.Roles.OrderBy(role => role)),
            RegistrationOperationKey = operationKey,
            RegistrationFingerprint = fingerprint,
            Version = 0
        };
        context.Set<UnidentifiedItemEntity>().Add(entity);
        context.Set<UnidentifiedHistoryEntity>().Add(new UnidentifiedHistoryEntity
        {
            Id = Guid.NewGuid(),
            UnidentifiedItemId = entity.Id,
            PreviousState = UnidentifiedState.Open.ToString(),
            NewState = UnidentifiedState.Open.ToString(),
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = entity.CreatedByActorRolesJson,
            OccurredAtUtc = request.CreatedAtUtc,
            // SafeDetail (up to UnidentifiedValidation.MaximumDetailLength = 1000
            // chars) can exceed the narrower History.Reason column
            // (UnidentifiedValidation.MaximumReasonLength = 500); truncate rather
            // than let an otherwise-valid registration fail at SaveChangesAsync.
            Reason = TruncateForHistory(request.SafeDetail.Trim()),
            OperationKey = operationKey
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(Map(entity), false);
    }

    public async Task<UnidentifiedResolveResult?> ProbeResolveReplayAsync(
        ResolveUnidentifiedRequest request,
        CancellationToken cancellationToken = default)
    {
        UnidentifiedValidation.ValidateResolve(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var history = await context.Set<UnidentifiedHistoryEntity>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.OperationKey == request.OperationKey.Trim(), cancellationToken);
        if (history is null)
        {
            return null;
        }

        if (history.UnidentifiedItemId != request.UnidentifiedItemId)
        {
            throw new UnidentifiedOperationConflictException();
        }

        var entity = await context.Set<UnidentifiedItemEntity>().AsNoTracking()
            .SingleAsync(item => item.Id == request.UnidentifiedItemId, cancellationToken);
        return new(Map(entity), Map(history), true);
    }

    public async Task<UnidentifiedResolveResult> ResolveAsync(
        ResolveUnidentifiedRequest request,
        CancellationToken cancellationToken = default)
    {
        UnidentifiedValidation.ValidateResolve(request);
        var operationKey = request.OperationKey.Trim();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var replay = await context.Set<UnidentifiedHistoryEntity>().SingleOrDefaultAsync(
            item => item.OperationKey == operationKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.UnidentifiedItemId != request.UnidentifiedItemId
                || !string.Equals(replay.Reason, request.Reason.Trim(), StringComparison.Ordinal)
                || !string.Equals(replay.TargetKind, request.TargetKind.ToString(), StringComparison.Ordinal)
                || !string.Equals(replay.TargetId, request.TargetId.Trim(), StringComparison.Ordinal)
                || !string.Equals(replay.TargetReference, request.TargetReference?.Trim(), StringComparison.Ordinal))
            {
                throw new UnidentifiedOperationConflictException();
            }

            var replayItem = await context.Set<UnidentifiedItemEntity>().AsNoTracking()
                .SingleAsync(item => item.Id == request.UnidentifiedItemId, cancellationToken);
            return new(Map(replayItem), Map(replay), true);
        }

        var entity = await context.Set<UnidentifiedItemEntity>().SingleOrDefaultAsync(
            item => item.Id == request.UnidentifiedItemId, cancellationToken)
            ?? throw new KeyNotFoundException("The Unidentified item does not exist.");
        if (entity.Version != request.ExpectedVersion || entity.State != UnidentifiedState.Open.ToString())
        {
            throw new UnidentifiedVersionConflictException();
        }

        entity.State = UnidentifiedState.Resolved.ToString();
        entity.ResolvedAtUtc = request.ResolvedAtUtc;
        entity.ResolvedByActorKind = request.Actor.Kind.ToString();
        entity.ResolvedByActorSubjectId = request.Actor.SubjectId;
        entity.ResolvedByActorRolesJson = JsonSerializer.Serialize(request.Actor.Roles.OrderBy(role => role));
        entity.ResolutionReason = request.Reason.Trim();
        entity.ResolutionTargetKind = request.TargetKind.ToString();
        entity.ResolutionTargetId = request.TargetId.Trim();
        entity.ResolutionTargetReference = request.TargetReference?.Trim();
        entity.Version++;
        var history = new UnidentifiedHistoryEntity
        {
            Id = Guid.NewGuid(),
            UnidentifiedItemId = entity.Id,
            PreviousState = UnidentifiedState.Open.ToString(),
            NewState = UnidentifiedState.Resolved.ToString(),
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = entity.ResolvedByActorRolesJson,
            OccurredAtUtc = request.ResolvedAtUtc,
            Reason = request.Reason.Trim(),
            OperationKey = operationKey,
            TargetKind = request.TargetKind.ToString(),
            TargetId = request.TargetId.Trim(),
            TargetReference = request.TargetReference?.Trim()
        };
        context.Set<UnidentifiedHistoryEntity>().Add(history);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(Map(entity), Map(history), false);
    }

    public async Task<UnidentifiedItem?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<UnidentifiedItemEntity>().AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<UnidentifiedItem?> GetByReferenceAsync(string reference, CancellationToken cancellationToken = default)
    {
        var sequence = UnidentifiedReferenceFormat.Parse(reference);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<UnidentifiedItemEntity>().AsNoTracking().SingleOrDefaultAsync(item => item.Sequence == sequence, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<UnidentifiedItem?> GetByOriginAsync(
        UnidentifiedOrigin origin,
        CancellationToken cancellationToken = default)
    {
        UnidentifiedOrigin.Validate(origin);
        var originKind = origin.Kind.ToString();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Set<UnidentifiedItemEntity>().AsNoTracking().SingleOrDefaultAsync(
            item => item.OriginKind == originKind && item.OriginId == origin.Id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<UnidentifiedItem>> ListAsync(UnidentifiedState? state = UnidentifiedState.Open, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.Set<UnidentifiedItemEntity>().AsNoTracking().AsQueryable();
        if (state is not null)
        {
            query = query.Where(item => item.State == state.Value.ToString());
        }

        var rows = await query.OrderBy(item => item.CreatedAtUtc).ThenBy(item => item.Sequence).ToArrayAsync(cancellationToken);
        return rows.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<UnidentifiedQueueRow>> ListQueueAsync(
        UnidentifiedMediaKind? mediaKind,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var openState = UnidentifiedState.Open.ToString();

        // No foreign key is modelled between an Unidentified item and its
        // origin receipt (the origin can be a receipt or, for INTK-007's
        // grouped-VRM-conflict case, a submission group), so this is a plain
        // left join rather than a navigation property.
        var joined = await (
            from item in context.Set<UnidentifiedItemEntity>().AsNoTracking()
            where item.State == openState
            join receipt in context.Set<IntakeReceiptEntity>().AsNoTracking().Include(entity => entity.MailRouteDecision)
                on item.OriginId equals receipt.Id into receiptGroup
            from receipt in receiptGroup.DefaultIfEmpty()
            orderby item.CreatedAtUtc, item.Sequence
            select new { item, receipt })
            .ToArrayAsync(cancellationToken);

        var rows = joined.Select(row => MapQueueRow(row.item, row.receipt)).ToArray();
        return mediaKind is null
            ? rows
            : rows.Where(row => row.MediaKind == mediaKind.Value).ToArray();
    }

    private static UnidentifiedQueueRow MapQueueRow(UnidentifiedItemEntity item, IntakeReceiptEntity? receipt)
    {
        // The nullable overload owns the no-receipt fallback (a
        // submission-group origin with nothing to classify against); this
        // mapper carries no business judgement of its own.
        var mediaKind = UnidentifiedMediaKindPolicy.Classify(
            receipt is null ? null : EfIntakeReceiptStore.ParseSourceChannel(receipt.SourceChannel),
            receipt?.MediaType);

        string? fileName = null;
        string? emailSubject = null;
        string? emailSender = null;
        if (receipt is not null)
        {
            if (mediaKind == UnidentifiedMediaKind.Email)
            {
                emailSubject = EfIntakeReceiptStore.ReadSubject(receipt.EvidenceJson);
                emailSender = receipt.MailRouteDecision?.EffectiveSenderAddress;
            }
            else
            {
                fileName = receipt.SourceFileName;
            }
        }

        return new(
            item.Id,
            item.Reference,
            mediaKind,
            fileName,
            emailSubject,
            emailSender,
            item.CreatedAtUtc,
            Enum.Parse<UnidentifiedReasonCode>(item.ReasonCode));
    }

    public async Task<IReadOnlyList<UnidentifiedHistoryEntry>> HistoryAsync(Guid unidentifiedItemId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Set<UnidentifiedHistoryEntity>().AsNoTracking()
            .Where(item => item.UnidentifiedItemId == unidentifiedItemId)
            .OrderBy(item => item.OccurredAtUtc).ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);
        return rows.Select(Map).ToArray();
    }

    private static void EnsureRegistrationReplay(UnidentifiedItemEntity entity, RegisterUnidentifiedRequest request)
    {
        if (!string.Equals(entity.RegistrationFingerprint, Fingerprint(request), StringComparison.Ordinal))
        {
            throw new UnidentifiedOperationConflictException();
        }
    }

    private static string TruncateForHistory(string reason) =>
        reason.Length > UnidentifiedValidation.MaximumReasonLength
            ? reason[..UnidentifiedValidation.MaximumReasonLength]
            : reason;

    private static string Fingerprint(RegisterUnidentifiedRequest request)
    {
        var value = string.Join('|', request.Origin.Kind, request.Origin.Id, request.ReasonCode, request.SafeDetail.Trim(), request.Actor.Kind, request.Actor.SubjectId);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static UnidentifiedItem Map(UnidentifiedItemEntity entity) => new(
        entity.Id,
        entity.Sequence,
        entity.Reference,
        new(Enum.Parse<UnidentifiedOriginKind>(entity.OriginKind), entity.OriginId),
        Enum.Parse<UnidentifiedReasonCode>(entity.ReasonCode),
        entity.SafeDetail,
        Enum.Parse<UnidentifiedState>(entity.State),
        entity.CreatedAtUtc,
        entity.ResolvedAtUtc,
        MapActor(entity.CreatedByActorKind, entity.CreatedByActorSubjectId, entity.CreatedByActorRolesJson),
        entity.ResolvedByActorKind is null ? null : MapActor(entity.ResolvedByActorKind, entity.ResolvedByActorSubjectId!, entity.ResolvedByActorRolesJson ?? "[]"),
        entity.ResolutionReason,
        entity.ResolutionTargetKind is null ? null : Enum.Parse<UnidentifiedResolutionTargetKind>(entity.ResolutionTargetKind),
        entity.ResolutionTargetId,
        entity.ResolutionTargetReference,
        entity.Version);

    private static UnidentifiedHistoryEntry Map(UnidentifiedHistoryEntity entity) => new(
        entity.Id,
        entity.UnidentifiedItemId,
        Enum.Parse<UnidentifiedState>(entity.PreviousState),
        Enum.Parse<UnidentifiedState>(entity.NewState),
        MapActor(entity.ActorKind, entity.ActorSubjectId, entity.ActorRolesJson),
        entity.OccurredAtUtc,
        entity.Reason,
        entity.OperationKey,
        entity.TargetKind is null ? null : Enum.Parse<UnidentifiedResolutionTargetKind>(entity.TargetKind),
        entity.TargetId,
        entity.TargetReference);

    private static ActionActor MapActor(string kind, string subjectId, string rolesJson)
    {
        var actorKind = Enum.Parse<ActorKind>(kind);
        return actorKind switch
        {
            ActorKind.Staff when Guid.TryParse(subjectId, out var id) => ActionActor.Staff(
                id,
                JsonSerializer.Deserialize<StaffRole[]>(rolesJson) ?? [StaffRole.User]),
            ActorKind.Automation => ActionActor.Automation(subjectId),
            ActorKind.RequestLink when Guid.TryParse(subjectId, out var requestId) => ActionActor.RequestLink(requestId),
            _ => ActionActor.SystemWorker(subjectId)
        };
    }
}
