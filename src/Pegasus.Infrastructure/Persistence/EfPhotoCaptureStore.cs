using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Intake;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The capture store (FRD-28). An item is added under the capture row's
/// update lock, so the caps, the per-batch ordinal and the counters are
/// decided by one writer at a time; every other write is an optimistic save
/// on the row's version, retried on a lost race unless the caller named the
/// version it read.
/// </summary>
public sealed class EfPhotoCaptureStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IPhotoCaptureStore
{
    internal static readonly JsonSerializerOptions DetailsJsonOptions = new(JsonSerializerDefaults.Web)
    {
        // MarkedAreas is derived from DamageAreas; only the stored fields are written.
        IgnoreReadOnlyProperties = true
    };

    public async Task<PhotoCapture> CreateAsync(
        NewPhotoCapture capture,
        string tokenDigest,
        PhotoLinkTokenPurpose tokenPurpose,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capture);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenDigest);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        string? caseReference = null;
        if (capture.CaseId is { } caseId)
        {
            caseReference = await context.Cases.AsNoTracking()
                .Where(item => item.Id == caseId)
                .Select(item => item.Reference)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("The Case of the photo link was not found.");
        }

        var entity = new PhotoCaptureEntity
        {
            Id = capture.Id,
            Origin = PhotoCaptureCodes.ToCode(capture.Origin),
            StandingLinkId = capture.StandingLinkId,
            OrganizationId = capture.OrganizationId,
            CaseId = capture.CaseId,
            CaseReference = caseReference,
            Destination = PhotoCaptureCodes.ToCode(capture.Destination),
            NormalizedRegistration = capture.NormalizedRegistration,
            IncidentDate = capture.IncidentDate,
            CustomerName = capture.CustomerName,
            Mobile = capture.Mobile,
            Email = capture.Email,
            DetailsJson = SerializeDetails(PhotoCaptureDetails.Empty),
            Mode = PhotoCaptureCodes.ToCode(capture.Mode),
            LiveCameraOnly = capture.LiveCameraOnly,
            OriginalsInBackground = capture.OriginalsInBackground,
            IssuedByStaffId = capture.IssuedByStaffId,
            CreatedAtUtc = capture.CreatedAtUtc,
            ExpiresAtUtc = capture.ExpiresAtUtc,
            Version = 1
        };
        context.PhotoCaptures.Add(entity);
        context.PhotoCaptureTokens.Add(new()
        {
            TokenDigest = tokenDigest,
            CaptureId = entity.Id,
            Purpose = PhotoCaptureCodes.ToCode(tokenPurpose),
            IssuedAtUtc = capture.CreatedAtUtc,
            IssuedByStaffId = capture.IssuedByStaffId
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<PhotoCapture?> GetAsync(Guid captureId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.PhotoCaptures.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == captureId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<(PhotoCapture Capture, bool TokenRevoked)?> FindByTokenDigestAsync(
        string tokenDigest,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenDigest);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var row = await (
            from token in context.PhotoCaptureTokens.AsNoTracking()
            join capture in context.PhotoCaptures.AsNoTracking() on token.CaptureId equals capture.Id
            where token.TokenDigest == tokenDigest
            select new { Capture = capture, token.RevokedAtUtc })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        return (Map(row.Capture), row.RevokedAtUtc is not null);
    }

    public async Task AddTokenAsync(
        Guid captureId,
        string tokenDigest,
        PhotoLinkTokenPurpose purpose,
        Guid? issuedByStaffId,
        DateTimeOffset issuedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenDigest);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.PhotoCaptureTokens.Add(new()
        {
            TokenDigest = tokenDigest,
            CaptureId = captureId,
            Purpose = PhotoCaptureCodes.ToCode(purpose),
            IssuedAtUtc = issuedAtUtc,
            IssuedByStaffId = issuedByStaffId
        });
        await context.SaveChangesAsync(cancellationToken);
    }

    public Task<PhotoCapture> RecordOpenedAsync(Guid captureId, DateTimeOffset openedAtUtc, CancellationToken cancellationToken) =>
        UpdateAsync(captureId, expectedVersion: null, (_, entity, _) =>
        {
            entity.OpenedAtUtc ??= openedAtUtc;
            return Task.CompletedTask;
        }, cancellationToken);

    public Task<PhotoCapture> RecordDetailsAsync(
        Guid captureId,
        long expectedVersion,
        string? normalizedRegistration,
        DateOnly? incidentDate,
        string? customerName,
        string? mobile,
        string? email,
        PhotoCaptureDetails details,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(details);
        return UpdateAsync(captureId, expectedVersion, (_, entity, _) =>
        {
            entity.NormalizedRegistration = normalizedRegistration;
            entity.IncidentDate = incidentDate;
            entity.CustomerName = customerName;
            entity.Mobile = mobile;
            entity.Email = email;
            entity.DetailsJson = SerializeDetails(details);
            return Task.CompletedTask;
        }, cancellationToken);
    }

    public Task<PhotoCapture> RecordRoutingAsync(
        Guid captureId,
        long expectedVersion,
        PhotoCaptureDestination destination,
        Guid? caseId,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return UpdateAsync(captureId, expectedVersion, async (context, entity, token) =>
        {
            entity.Destination = PhotoCaptureCodes.ToCode(destination);
            entity.RoutingReason = reason;
            entity.CaseId = caseId;
            entity.CaseReference = caseId is { } id
                ? await context.Cases.AsNoTracking()
                    .Where(item => item.Id == id)
                    .Select(item => item.Reference)
                    .SingleOrDefaultAsync(token)
                    ?? throw new KeyNotFoundException("The routed Case was not found.")
                : null;
        }, cancellationToken);
    }

    public Task<PhotoCaptureItemWriteResult> AddItemAsync(
        NewPhotoCaptureItem item,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        // The capture row's lock serializes adds; a unique-key collision can
        // only come from a racing first insert of the same client id, and the
        // retry reads the winner's row as a replay or a conflict.
        return StoreConcurrencyRetry.ExecuteAsync(
            token => AddItemOnceAsync(item, token),
            cancellationToken,
            StoreConcurrencyRetry.IsDeadlockOrKeyCollision);
    }

    private async Task<PhotoCaptureItemWriteResult> AddItemOnceAsync(
        NewPhotoCaptureItem item,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        await context.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM [PhotoCaptures] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {item.CaptureId}",
            cancellationToken);
        var capture = await context.PhotoCaptures
            .SingleOrDefaultAsync(value => value.Id == item.CaptureId, cancellationToken)
            ?? throw new KeyNotFoundException("The photo link was not found.");

        var existing = await context.PhotoCaptureItems.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.CaptureId == item.CaptureId && value.ClientItemId == item.ClientItemId,
                cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return string.Equals(existing.ContentHash, item.ContentHash, StringComparison.OrdinalIgnoreCase)
                ? new(PhotoCaptureItemWrite.Replayed, Map(existing))
                : new(PhotoCaptureItemWrite.Conflict, null);
        }

        if (!WithinCaps(capture, item))
        {
            await transaction.CommitAsync(cancellationToken);
            return new(PhotoCaptureItemWrite.CapReached, null);
        }

        var ordinal = await context.PhotoCaptureItems
            .CountAsync(value => value.CaptureId == item.CaptureId && value.Batch == item.Batch, cancellationToken);
        var entity = new PhotoCaptureItemEntity
        {
            Id = item.Id,
            CaptureId = item.CaptureId,
            ClientItemId = item.ClientItemId,
            Kind = PhotoCaptureCodes.ToCode(item.Kind),
            Area = item.Area,
            Stage = PhotoCaptureCodes.ToCode(item.Stage),
            Position = item.Position,
            Batch = item.Batch,
            Ordinal = ordinal,
            StoryGroup = item.StoryGroup,
            StorySequence = item.StorySequence,
            FileName = item.FileName,
            MediaType = item.MediaType,
            TakenAtUtc = item.TakenAtUtc,
            LiveCamera = item.LiveCamera,
            InReportOnArrival = item.InReportOnArrival,
            ContentHash = item.ContentHash.ToLowerInvariant(),
            ContentLength = item.ContentLength,
            ReceivedAtUtc = item.ReceivedAtUtc
        };
        context.PhotoCaptureItems.Add(entity);
        // The row counts bound what a link may add; what it has received
        // (AcceptedItemCount, AcceptedBytes, first and last item) is counted
        // when the item's receipt lands (RecordItemReceiptAsync), so an upload
        // that failed after its row was written is not a photo the Case has.
        switch (CapKind(item.Kind))
        {
            case ItemCap.Video:
                capture.VideoCount++;
                break;
            case ItemCap.Document:
                capture.DocumentCount++;
                break;
            default:
                capture.PhotoCount++;
                break;
        }

        capture.Version++;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(PhotoCaptureItemWrite.Created, Map(entity));
    }

    private enum ItemCap
    {
        Photo,
        Document,
        Video
    }

    private static ItemCap CapKind(PhotoCaptureItemKind kind) => kind switch
    {
        PhotoCaptureItemKind.Video => ItemCap.Video,
        PhotoCaptureItemKind.Paperwork => ItemCap.Document,
        _ => ItemCap.Photo
    };

    private static bool WithinCaps(PhotoCaptureEntity capture, NewPhotoCaptureItem item)
    {
        if (capture.AcceptedBytes + item.ContentLength > CaptureLimits.MaximumBytesPerCapture)
        {
            return false;
        }

        return CapKind(item.Kind) switch
        {
            ItemCap.Video => capture.VideoCount < CaptureLimits.MaximumVideosPerCapture,
            ItemCap.Document => capture.DocumentCount < CaptureLimits.MaximumDocumentsPerCapture,
            _ => capture.PhotoCount < CaptureLimits.MaximumPhotosPerCapture
        };
    }

    public async Task RecordItemReceiptAsync(
        Guid itemId,
        Guid stagedReceiptId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        // The receipt's evaluation records the item under the work item's
        // lock (EfPhotoCaptureItemProcessing). Taking that lock first here
        // orders the two, in the same lock order: an evaluation that committed
        // first is read below, and one that commits later finds this item's
        // staged receipt.
        await context.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM [IntakeWorkItems] WITH (UPDLOCK, HOLDLOCK) WHERE [StagedReceiptId] = {stagedReceiptId}",
            cancellationToken);
        var item = await context.PhotoCaptureItems
            .SingleOrDefaultAsync(value => value.Id == itemId, cancellationToken)
            ?? throw new KeyNotFoundException("The photo link item was not found.");
        if (item.StagedReceiptId is null)
        {
            // The item is received now: the capture's accepted count, bytes
            // and item times move with the receipt, not the row.
            await context.Database.ExecuteSqlAsync(
                $"SELECT 1 FROM [PhotoCaptures] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {item.CaptureId}",
                cancellationToken);
            var capture = await context.PhotoCaptures
                .SingleOrDefaultAsync(value => value.Id == item.CaptureId, cancellationToken)
                ?? throw new KeyNotFoundException("The photo link was not found.");
            capture.AcceptedItemCount++;
            capture.AcceptedBytes += item.ContentLength;
            capture.FirstItemAtUtc ??= item.ReceivedAtUtc;
            if (capture.LastItemAtUtc is null || capture.LastItemAtUtc < item.ReceivedAtUtc)
            {
                capture.LastItemAtUtc = item.ReceivedAtUtc;
            }

            capture.Version++;
        }

        item.StagedReceiptId = stagedReceiptId;
        await context.SaveChangesAsync(cancellationToken);
        var processedReceiptId = await context.IntakeWorkItems.AsNoTracking()
            .Where(work => work.StagedReceiptId == stagedReceiptId)
            .Select(work => work.ProcessedReceiptId)
            .FirstOrDefaultAsync(cancellationToken);
        if (processedReceiptId is { } processed && item.IntakeReceiptId is null)
        {
            item.IntakeReceiptId = processed;
            item.IntakeAssetId = await EfPhotoCaptureItemProcessing.SourceAssetIdAsync(context, processed, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordItemProcessedAsync(
        Guid itemId,
        Guid intakeReceiptId,
        Guid? intakeAssetId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var changed = await context.PhotoCaptureItems
            .Where(item => item.Id == itemId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.IntakeReceiptId, (Guid?)intakeReceiptId)
                .SetProperty(item => item.IntakeAssetId, intakeAssetId),
                cancellationToken);
        if (changed == 0)
        {
            throw new KeyNotFoundException("The photo link item was not found.");
        }
    }

    public async Task RecordItemOriginalAsync(Guid itemId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        var item = await context.PhotoCaptureItems.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == itemId, cancellationToken)
            ?? throw new KeyNotFoundException("The photo link item was not found.");
        await context.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM [PhotoCaptures] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {item.CaptureId}",
            cancellationToken);
        var changed = await context.PhotoCaptureItems
            .Where(value => value.Id == itemId && !value.OriginalReceived)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.OriginalReceived, true), cancellationToken);
        if (changed > 0)
        {
            var capture = await context.PhotoCaptures
                .SingleAsync(value => value.Id == item.CaptureId, cancellationToken);
            capture.OriginalCount++;
            capture.Version++;
            await context.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public Task<PhotoCapture> RecordSentAsync(
        Guid captureId,
        string? note,
        DateTimeOffset sentAtUtc,
        CancellationToken cancellationToken) =>
        UpdateAsync(captureId, expectedVersion: null, (_, entity, _) =>
        {
            entity.Note = note;
            entity.SentAtUtc = sentAtUtc;
            return Task.CompletedTask;
        }, cancellationToken);

    public Task<PhotoCapture> RecordSettledAsync(
        Guid captureId,
        long expectedVersion,
        Guid? submissionGroupId,
        DateTimeOffset settledAtUtc,
        CancellationToken cancellationToken) =>
        UpdateAsync(captureId, expectedVersion, (_, entity, _) =>
        {
            entity.SettledAtUtc = settledAtUtc;
            entity.SettledBatches++;
            if (submissionGroupId is not null)
            {
                entity.SubmissionGroupId = submissionGroupId;
            }

            return Task.CompletedTask;
        }, cancellationToken);

    public Task<PhotoCapture> RevokeAsync(Guid captureId, DateTimeOffset revokedAtUtc, CancellationToken cancellationToken) =>
        UpdateAsync(captureId, expectedVersion: null, (_, entity, _) =>
        {
            entity.RevokedAtUtc ??= revokedAtUtc;
            return Task.CompletedTask;
        }, cancellationToken);

    public Task<PhotoCapture> ExtendAsync(Guid captureId, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken) =>
        UpdateAsync(captureId, expectedVersion: null, (_, entity, _) =>
        {
            entity.ExpiresAtUtc = expiresAtUtc;
            return Task.CompletedTask;
        }, cancellationToken);

    public Task<PhotoCapture> RecordReminderAsync(Guid captureId, DateTimeOffset remindedAtUtc, CancellationToken cancellationToken) =>
        UpdateAsync(captureId, expectedVersion: null, (_, entity, _) =>
        {
            entity.ReminderCount++;
            entity.LastRemindedAtUtc = remindedAtUtc;
            return Task.CompletedTask;
        }, cancellationToken);

    public Task<PhotoCapture> RecordCallAsync(Guid captureId, DateTimeOffset calledAtUtc, CancellationToken cancellationToken) =>
        UpdateAsync(captureId, expectedVersion: null, (_, entity, _) =>
        {
            entity.CallLoggedAtUtc = calledAtUtc;
            return Task.CompletedTask;
        }, cancellationToken);

    public async Task<IReadOnlyList<PhotoCaptureWithItems>> ListAwaitingSettleAsync(
        DateTimeOffset nowUtc,
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // PhotoCapture.HasUnsettledSend, as SQL.
        var captures = await context.PhotoCaptures.AsNoTracking()
            .Where(capture => capture.RevokedAtUtc == null
                && ((capture.SentAtUtc != null
                        && (capture.SettledAtUtc == null || capture.SettledAtUtc < capture.SentAtUtc))
                    || (capture.ExpiresAtUtc <= nowUtc
                        && ((capture.SettledAtUtc == null && capture.AcceptedItemCount > 0)
                            || (capture.SettledAtUtc != null && capture.LastItemAtUtc > capture.SettledAtUtc)))))
            .OrderBy(capture => capture.CreatedAtUtc)
            .ThenBy(capture => capture.Id)
            .Take(maximumItems)
            .ToListAsync(cancellationToken);
        return await WithItemsAsync(context, captures, cancellationToken);
    }

    public async Task<IReadOnlyList<PhotoCaptureItem>> ListItemsAsync(Guid captureId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return (await StoryOrdered(context.PhotoCaptureItems.AsNoTracking().Where(item => item.CaptureId == captureId))
                .ToListAsync(cancellationToken))
            .Select(Map)
            .ToArray();
    }

    public async Task<PhotoCaptureItem?> FindItemAsync(Guid captureId, Guid clientItemId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.PhotoCaptureItems.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CaptureId == captureId && item.ClientItemId == clientItemId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<(PhotoCaptureWithItems Capture, Guid CaseId, string CaseReference)>> ListSettledReachingCaseAsync(
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var settled = context.PhotoCaptures.AsNoTracking()
            .Where(capture => capture.SettledAtUtc != null && capture.CaseId == null);

        // The Vehicle images record the settle formed was merged into a Case.
        var viaRecord = await (
            from capture in settled
            join record in context.ImageIntakes.AsNoTracking()
                on capture.SubmissionGroupId equals record.SubmissionGroupId
            where record.MergedIntoCaseId != null
            orderby capture.CreatedAtUtc, capture.Id
            select new { CaptureId = capture.Id, CaseId = record.MergedIntoCaseId!.Value })
            .Take(maximumItems)
            .ToListAsync(cancellationToken);

        // Or the receipts its items became were filed on a Case: an active
        // association, or a Case's own intake link.
        var viaAssociation = await (
            from capture in settled
            join item in context.PhotoCaptureItems.AsNoTracking() on capture.Id equals item.CaptureId
            join association in context.IntakeManualAssociations.AsNoTracking()
                on item.IntakeReceiptId equals (Guid?)association.IntakeReceiptId
            where association.IsActive
            select new { CaptureId = capture.Id, association.CaseId })
            .Distinct()
            .Take(maximumItems * 2)
            .ToListAsync(cancellationToken);
        var viaLink = await (
            from capture in settled
            join item in context.PhotoCaptureItems.AsNoTracking() on capture.Id equals item.CaptureId
            join link in context.CaseIntakeLinks.AsNoTracking()
                on item.IntakeReceiptId equals (Guid?)link.IntakeReceiptId
            select new { CaptureId = capture.Id, link.CaseId })
            .Distinct()
            .Take(maximumItems * 2)
            .ToListAsync(cancellationToken);

        var reached = new Dictionary<Guid, Guid>();
        foreach (var row in viaRecord)
        {
            reached.TryAdd(row.CaptureId, row.CaseId);
        }

        // A capture whose receipts reached more than one Case is ambiguous and waits for staff.
        foreach (var group in viaAssociation.Concat(viaLink).GroupBy(row => row.CaptureId))
        {
            var cases = group.Select(row => row.CaseId).Distinct().ToArray();
            if (cases.Length == 1)
            {
                reached.TryAdd(group.Key, cases[0]);
            }
        }

        if (reached.Count == 0)
        {
            return [];
        }

        var captureIds = reached.Keys.ToArray();
        var caseIds = reached.Values.Distinct().ToArray();
        var captures = await context.PhotoCaptures.AsNoTracking()
            .Where(capture => EF.Parameter(captureIds).Contains(capture.Id))
            .OrderBy(capture => capture.CreatedAtUtc)
            .ThenBy(capture => capture.Id)
            .Take(maximumItems)
            .ToListAsync(cancellationToken);
        var references = await context.Cases.AsNoTracking()
            .Where(item => EF.Parameter(caseIds).Contains(item.Id))
            .Select(item => new { item.Id, item.Reference })
            .ToDictionaryAsync(item => item.Id, item => item.Reference, cancellationToken);
        var withItems = await WithItemsAsync(context, captures, cancellationToken);
        return withItems
            .Where(pending => references.ContainsKey(reached[pending.Capture.Id]))
            .Select(pending => (pending, reached[pending.Capture.Id], references[reached[pending.Capture.Id]]))
            .ToArray();
    }

    public Task<PhotoCapture> RecordCaseAsync(Guid captureId, Guid caseId, string caseReference, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseReference);
        return UpdateAsync(captureId, expectedVersion: null, (_, entity, _) =>
        {
            entity.CaseId = caseId;
            entity.CaseReference = caseReference;
            entity.Destination = PhotoCaptureCodes.ToCode(PhotoCaptureDestination.Case);
            return Task.CompletedTask;
        }, cancellationToken);
    }

    public async Task<bool> AllItemsProcessedAsync(Guid captureId, int batch, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // An item whose upload never completed has no staged receipt and is
        // not waited for; a received item waits for its evaluation.
        return !await context.PhotoCaptureItems.AnyAsync(
            item => item.CaptureId == captureId && item.Batch == batch
                && item.StagedReceiptId != null && item.IntakeReceiptId == null,
            cancellationToken);
    }

    /// <summary>
    /// One optimistic write of the capture row. With an expected version the
    /// caller's read decides, and a changed row is a concurrency failure it
    /// sees; without one a lost race re-reads and writes again.
    /// </summary>
    private Task<PhotoCapture> UpdateAsync(
        Guid captureId,
        long? expectedVersion,
        Func<PegasusDbContext, PhotoCaptureEntity, CancellationToken, Task> change,
        CancellationToken cancellationToken)
    {
        Func<Exception, bool> retryable = expectedVersion is null
            ? StoreConcurrencyRetry.IsRetryable
            : StoreConcurrencyRetry.IsDeadlockOrKeyCollision;
        return StoreConcurrencyRetry.ExecuteAsync(
            async token =>
            {
                await using var context = await contextFactory.CreateDbContextAsync(token);
                var entity = await context.PhotoCaptures
                    .SingleOrDefaultAsync(item => item.Id == captureId, token)
                    ?? throw new KeyNotFoundException("The photo link was not found.");
                if (expectedVersion is { } expected && entity.Version != expected)
                {
                    throw new DbUpdateConcurrencyException("The photo link changed since it was read.");
                }

                await change(context, entity, token);
                entity.Version++;
                await context.SaveChangesAsync(token);
                return Map(entity);
            },
            cancellationToken,
            retryable);
    }

    internal static IQueryable<PhotoCaptureItemEntity> StoryOrdered(IQueryable<PhotoCaptureItemEntity> items) =>
        items.OrderBy(item => item.Batch)
            .ThenBy(item => item.StoryGroup)
            .ThenBy(item => item.StorySequence)
            .ThenBy(item => item.Ordinal);

    /// <summary>The captures with their items, read in one statement for all of them.</summary>
    internal static async Task<IReadOnlyList<PhotoCaptureWithItems>> WithItemsAsync(
        PegasusDbContext context,
        IReadOnlyList<PhotoCaptureEntity> captures,
        CancellationToken cancellationToken)
    {
        if (captures.Count == 0)
        {
            return [];
        }

        var items = await ItemsByCaptureAsync(context, captures.Select(capture => capture.Id).ToArray(), cancellationToken);
        return captures
            .Select(capture => new PhotoCaptureWithItems(
                Map(capture),
                items.TryGetValue(capture.Id, out var list) ? list : []))
            .ToArray();
    }

    internal static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<PhotoCaptureItem>>> ItemsByCaptureAsync(
        PegasusDbContext context,
        IReadOnlyCollection<Guid> captureIds,
        CancellationToken cancellationToken)
    {
        if (captureIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<PhotoCaptureItem>>();
        }

        // One JSON parameter, so no page of captures can reach the parameter limit.
        var ids = captureIds.ToArray();
        var rows = await StoryOrdered(context.PhotoCaptureItems.AsNoTracking()
                .Where(item => EF.Parameter(ids).Contains(item.CaptureId)))
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(item => item.CaptureId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PhotoCaptureItem>)group.Select(Map).ToArray());
    }

    internal static string SerializeDetails(PhotoCaptureDetails details) =>
        JsonSerializer.Serialize(details, DetailsJsonOptions);

    internal static PhotoCaptureDetails DeserializeDetails(string json) =>
        string.IsNullOrWhiteSpace(json)
            ? PhotoCaptureDetails.Empty
            : JsonSerializer.Deserialize<PhotoCaptureDetails>(json, DetailsJsonOptions) ?? PhotoCaptureDetails.Empty;

    internal static PhotoCapture Map(PhotoCaptureEntity entity) => new(
        entity.Id,
        PhotoCaptureCodes.ParseOrigin(entity.Origin),
        entity.StandingLinkId,
        entity.OrganizationId,
        entity.CaseId,
        entity.CaseReference,
        entity.SubmissionGroupId,
        PhotoCaptureCodes.ParseDestination(entity.Destination),
        entity.RoutingReason,
        entity.NormalizedRegistration,
        entity.IncidentDate,
        entity.CustomerName,
        entity.Mobile,
        entity.Email,
        DeserializeDetails(entity.DetailsJson),
        entity.Note,
        PhotoCaptureCodes.ParseMode(entity.Mode),
        entity.LiveCameraOnly,
        entity.OriginalsInBackground,
        entity.IssuedByStaffId,
        entity.CreatedAtUtc,
        entity.ExpiresAtUtc,
        entity.RevokedAtUtc,
        entity.OpenedAtUtc,
        entity.FirstItemAtUtc,
        entity.LastItemAtUtc,
        entity.SentAtUtc,
        entity.SettledAtUtc,
        entity.SettledBatches,
        entity.AcceptedItemCount,
        entity.AcceptedBytes,
        entity.ReminderCount,
        entity.LastRemindedAtUtc,
        entity.CallLoggedAtUtc,
        entity.Version);

    internal static PhotoCaptureItem Map(PhotoCaptureItemEntity entity) => new(
        entity.Id,
        entity.CaptureId,
        entity.ClientItemId,
        PhotoCaptureCodes.ParseItemKind(entity.Kind),
        entity.Area,
        PhotoCaptureCodes.ParseOptionalStage(entity.Stage),
        entity.Position,
        entity.Batch,
        entity.Ordinal,
        entity.StoryGroup,
        entity.StorySequence,
        entity.FileName,
        entity.MediaType,
        entity.TakenAtUtc,
        entity.LiveCamera,
        entity.InReportOnArrival,
        entity.ContentHash,
        entity.ContentLength,
        entity.StagedReceiptId,
        entity.IntakeReceiptId,
        entity.IntakeAssetId,
        entity.OriginalReceived,
        entity.ReceivedAtUtc);
}

/// <summary>
/// How a capture's item learns the receipt it became, in the transaction
/// that records the receipt's evaluation (<see cref="EfIntakeWorkStore"/>),
/// so no second sweep has to find it.
/// </summary>
internal static class EfPhotoCaptureItemProcessing
{
    /// <summary>
    /// When the staged receipt came from a photo link, records its processed
    /// receipt and that receipt's source asset on the item. The caller saves.
    /// </summary>
    public static async Task RecordIfPhotoCaptureAsync(
        PegasusDbContext context,
        Guid stagedReceiptId,
        Guid processedReceiptId,
        CancellationToken cancellationToken)
    {
        var channel = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.PhotoCapture);
        if (!await context.IntakeStagedReceipts.AsNoTracking()
                .AnyAsync(item => item.Id == stagedReceiptId && item.SourceChannel == channel, cancellationToken))
        {
            return;
        }

        var sourceAssetId = await SourceAssetIdAsync(context, processedReceiptId, cancellationToken);
        await RecordFromReceiptAsync(context, stagedReceiptId, processedReceiptId, sourceAssetId, cancellationToken);
    }

    /// <summary>The receipt's own file: an image, document or video receipt has one source asset.</summary>
    public static Task<Guid?> SourceAssetIdAsync(
        PegasusDbContext context,
        Guid processedReceiptId,
        CancellationToken cancellationToken) =>
        context.IntakeAssets.AsNoTracking()
            .Where(asset => asset.IntakeReceiptId == processedReceiptId && asset.Kind == SourceAssetKind)
            .OrderBy(asset => asset.Id)
            .Select(asset => (Guid?)asset.Id)
            .FirstOrDefaultAsync(cancellationToken);

    // EfIntakeReceiptStore's stored code of IntakeAssetKind.Source.
    private const string SourceAssetKind = "source";

    public static async Task RecordFromReceiptAsync(
        PegasusDbContext context,
        Guid stagedReceiptId,
        Guid processedReceiptId,
        Guid? sourceAssetId,
        CancellationToken cancellationToken)
    {
        var item = await context.PhotoCaptureItems
            .SingleOrDefaultAsync(value => value.StagedReceiptId == stagedReceiptId, cancellationToken);
        if (item is null)
        {
            // The item records its staged receipt just after intake accepts
            // it; when this evaluation wins that race, RecordItemReceiptAsync
            // reads the processed receipt instead.
            return;
        }

        item.IntakeReceiptId = processedReceiptId;
        item.IntakeAssetId = sourceAssetId;
    }
}
