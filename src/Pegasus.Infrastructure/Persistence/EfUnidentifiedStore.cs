using System.Data;
using System.Linq.Expressions;
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

    public async Task<UnidentifiedReopenResult> ReopenAsync(
        ReopenUnidentifiedRequest request,
        CancellationToken cancellationToken = default)
    {
        UnidentifiedValidation.ValidateReopen(request);
        var operationKey = request.OperationKey.Trim();
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var replay = await context.Set<UnidentifiedHistoryEntity>().SingleOrDefaultAsync(
            item => item.OperationKey == operationKey, cancellationToken);
        if (replay is not null)
        {
            if (replay.UnidentifiedItemId != request.UnidentifiedItemId
                || replay.PreviousState != UnidentifiedState.Resolved.ToString()
                || replay.NewState != UnidentifiedState.Open.ToString()
                || !string.Equals(replay.Reason, request.Reason.Trim(), StringComparison.Ordinal))
            {
                throw new UnidentifiedOperationConflictException();
            }

            // The state this reopen produced, not the row as it stands now. A
            // later re-resolve or reopen may have moved the item on, and a
            // stale caller replaying the reopen must not be handed that newer
            // version to build its next key from: it would consume the key the
            // fresher pass needs. The expected version is the one the reopen
            // applied at, so the reopen left the item one past it, open, with
            // every resolution field cleared.
            var replayItem = await context.Set<UnidentifiedItemEntity>().AsNoTracking()
                .SingleAsync(item => item.Id == request.UnidentifiedItemId, cancellationToken);
            var reopenedState = Map(replayItem) with
            {
                State = UnidentifiedState.Open,
                ResolvedAtUtc = null,
                ResolvedBy = null,
                ResolutionReason = null,
                ResolutionTargetKind = null,
                ResolutionTargetId = null,
                ResolutionTargetReference = null,
                Version = request.ExpectedVersion + 1
            };
            return new(reopenedState, Map(replay), true);
        }

        var entity = await context.Set<UnidentifiedItemEntity>().SingleOrDefaultAsync(
            item => item.Id == request.UnidentifiedItemId, cancellationToken)
            ?? throw new KeyNotFoundException("The Unidentified item does not exist.");
        if (entity.Version != request.ExpectedVersion || entity.State != UnidentifiedState.Resolved.ToString())
        {
            throw new UnidentifiedVersionConflictException();
        }

        entity.State = UnidentifiedState.Open.ToString();
        entity.ResolvedAtUtc = null;
        entity.ResolvedByActorKind = null;
        entity.ResolvedByActorSubjectId = null;
        entity.ResolvedByActorRolesJson = null;
        entity.ResolutionReason = null;
        entity.ResolutionTargetKind = null;
        entity.ResolutionTargetId = null;
        entity.ResolutionTargetReference = null;
        // The recheck watermark belongs to the resolution being withdrawn, not
        // to the item: the next resolution is reconciled from scratch.
        entity.ReconciledAssociationVersion = null;
        entity.Version++;
        var history = new UnidentifiedHistoryEntity
        {
            Id = Guid.NewGuid(),
            UnidentifiedItemId = entity.Id,
            PreviousState = UnidentifiedState.Resolved.ToString(),
            NewState = UnidentifiedState.Open.ToString(),
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(request.Actor.Roles.OrderBy(role => role)),
            OccurredAtUtc = request.ReopenedAtUtc,
            Reason = request.Reason.Trim(),
            OperationKey = operationKey
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

    public async Task<IReadOnlyList<UnidentifiedItem>> ListResolutionsToRecheckAsync(
        int maximum,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximum);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var resolved = UnidentifiedState.Resolved.ToString();
        var receipt = UnidentifiedOriginKind.Receipt.ToString();
        var automation = ActorKind.Automation.ToString();
        var automationSubject = ReconcileUnidentifiedDestinations.AutomationActorId;

        // As with ListQueueAsync, the origin is polymorphic and carries no
        // modelled foreign key, so the receipt's manual association is joined
        // directly on the origin id. Only the association can move an
        // automation resolution's destination without the receipt's own
        // processing pass seeing it, so only rows with an association are ever
        // candidates.
        //
        // The association's own version, not its timestamps, decides freshness.
        // A recheck that finds the destination unchanged writes no resolution,
        // so a timestamp compared against ResolvedAtUtc would never advance and
        // would re-select the row on every pass for ever; and being the oldest
        // resolutions, such rows would hold the head of this bounded
        // oldest-first page and starve every later stale resolution of its
        // recheck in silence. The version is monotonic per receipt, moves on
        // every link, unlink and relink, and needs no clock.
        var rows = await (
            from item in context.Set<UnidentifiedItemEntity>().AsNoTracking()
            join association in context.Set<IntakeManualAssociationEntity>().AsNoTracking()
                on item.OriginId equals association.IntakeReceiptId
            where item.State == resolved
                && item.OriginKind == receipt
                && item.ResolvedByActorKind == automation
                && item.ResolvedByActorSubjectId == automationSubject
                && (item.ReconciledAssociationVersion == null
                    || item.ReconciledAssociationVersion != association.Version)
            orderby item.ResolvedAtUtc, item.Sequence
            select item)
            .Take(maximum)
            .ToArrayAsync(cancellationToken);
        return rows.Select(Map).ToArray();
    }

    public async Task MarkResolutionRecheckedAsync(
        Guid unidentifiedItemId,
        long associationVersion,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var resolved = UnidentifiedState.Resolved.ToString();
        // Scoped to the still-resolved item and written without the concurrency
        // token: this is freshness bookkeeping about a resolution, not a
        // transition of the item, so it neither takes a version nor resurrects a
        // watermark onto a resolution that has since been reopened.
        await context.Set<UnidentifiedItemEntity>()
            .Where(item => item.Id == unidentifiedItemId && item.State == resolved)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    item => item.ReconciledAssociationVersion, associationVersion),
                cancellationToken);
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

    public async Task<KeysetPage<UnidentifiedQueueRow>> ListQueueByCursorAsync(
        UnidentifiedMediaKind? mediaKind,
        KeysetPosition? after,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var openState = UnidentifiedState.Open.ToString();

        var items = context.Set<UnidentifiedItemEntity>().AsNoTracking()
            .Where(item => item.State == openState);
        if (after is { } position)
        {
            // The composite keyset predicate, written out rather than as a
            // tuple comparison: SQL Server translates this form, and it is the
            // whole of the stability guarantee - a row equal on the timestamp
            // is included only when its id is strictly greater, so no row is
            // skipped or repeated when the queue changes under the reader.
            items = items.Where(item =>
                item.CreatedAtUtc > position.SortKey
                || (item.CreatedAtUtc == position.SortKey && item.Id.CompareTo(position.Id) > 0));
        }

        // The media kind is derived from two of the origin receipt's own
        // columns, so the filter runs in the DATABASE beside the keyset bound,
        // the order and the Take. That matters for correctness, not speed: a
        // filter applied after a bounded fetch window would let a page come
        // back short while rows remained, and the continuation would then be
        // minted from the last MATCHING row and silently drop the rest of the
        // queue - the precise failure the keyset predicate above exists to
        // prevent. With the filter in SQL, every row returned matched, so the
        // last row returned is always the correct next position.
        //
        // No foreign key is modelled between an Unidentified item and its
        // origin receipt (the origin can be a receipt or, for INTK-007's
        // grouped-VRM-conflict case, a submission group), so this is a plain
        // left join rather than a navigation property.
        var joined =
            from item in items
            join receipt in context.Set<IntakeReceiptEntity>().AsNoTracking()
                .Include(entity => entity.MailRouteDecision)
                on item.OriginId equals receipt.Id into receiptGroup
            from receipt in receiptGroup.DefaultIfEmpty()
            select new UnidentifiedQueueJoin { Item = item, Receipt = receipt };

        if (mediaKind is { } requested)
        {
            joined = joined.Where(MediaKindPredicate(requested));
        }

        // One extra row beyond the page: its presence is what says another page
        // exists, without a second count query that could disagree with it.
        var rows = await joined
            .OrderBy(row => row.Item.CreatedAtUtc)
            .ThenBy(row => row.Item.Id)
            .Take(limit + 1)
            .ToArrayAsync(cancellationToken);

        var hasMore = rows.Length > limit;
        var page = rows.Take(limit).ToArray();
        var next = hasMore && page.Length > 0
            ? new KeysetPosition(page[^1].Item.CreatedAtUtc, page[^1].Item.Id)
            : null;
        return new(page.Select(row => MapQueueRow(row.Item, row.Receipt)).ToArray(), next);
    }

    /// <summary>
    /// <see cref="UnidentifiedMediaKindPolicy"/> as a translatable predicate
    /// over the origin receipt's own columns, so the queue can be filtered
    /// where it is ordered and bounded.
    ///
    /// This mirrors the policy rather than calling it — the policy is C# and
    /// runs on materialised rows — so the two could in principle drift. They are
    /// held together by test: the three filtered pages must partition the
    /// unfiltered queue exactly, and each row's mapped kind (which DOES come
    /// from the policy) must equal the kind it was filtered by. A change to the
    /// policy that is not made here fails that test rather than quietly
    /// returning the wrong rows.
    ///
    /// The image test spells each letter as a character class rather than using
    /// <c>StartsWith("image/")</c>: the policy compares with
    /// <see cref="StringComparison.OrdinalIgnoreCase"/>, and a plain SQL
    /// <c>LIKE</c> follows the database collation, so a case-sensitive
    /// collation would disagree with the policy on <c>IMAGE/JPEG</c>. The
    /// character classes are case-insensitive whatever the collation is.
    /// </summary>
    private static Expression<Func<UnidentifiedQueueJoin, bool>> MediaKindPredicate(
        UnidentifiedMediaKind mediaKind)
    {
        var mailbox = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox);
        return mediaKind switch
        {
            // A mailbox-channel receipt is a received e-mail, whatever its
            // content type happens to be.
            UnidentifiedMediaKind.Email => row =>
                row.Receipt != null && row.Receipt.SourceChannel == mailbox,

            // A row with no origin receipt has no channel or content type to
            // classify, and the policy's no-receipt fallback is Image.
            UnidentifiedMediaKind.Image => row =>
                row.Receipt == null
                || (row.Receipt.SourceChannel != mailbox
                    && EF.Functions.Like(row.Receipt.MediaType, ImageMediaTypePattern)),

            UnidentifiedMediaKind.Document => row =>
                row.Receipt != null
                && row.Receipt.SourceChannel != mailbox
                && !EF.Functions.Like(row.Receipt.MediaType, ImageMediaTypePattern),

            _ => throw new ArgumentOutOfRangeException(
                nameof(mediaKind),
                $"Unknown Unidentified media kind '{(int)mediaKind}'.")
        };
    }

    /// <summary>
    /// The join shape the media-kind predicate is written against. Named rather
    /// than anonymous only so the predicate can be a typed expression the
    /// provider translates.
    /// </summary>
    private sealed class UnidentifiedQueueJoin
    {
        public required UnidentifiedItemEntity Item { get; init; }

        public IntakeReceiptEntity? Receipt { get; init; }
    }

    /// <summary>
    /// <c>image/</c> with every letter as a two-case character class, so the
    /// SQL <c>LIKE</c> is case-insensitive independently of the database
    /// collation and therefore agrees with the policy's OrdinalIgnoreCase.
    /// </summary>
    private const string ImageMediaTypePattern = "[Ii][Mm][Aa][Gg][Ee]/%";

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
