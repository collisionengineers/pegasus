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
    /// <summary>The <c>ActionHistory</c> aggregate the staff acts on a capture are recorded under.</summary>
    internal const string HistoryAggregateType = "photo_capture";

    internal static readonly JsonSerializerOptions DetailsJsonOptions = new(JsonSerializerDefaults.Web)
    {
        // MarkedAreas is derived from DamageAreas; only the stored fields are written.
        IgnoreReadOnlyProperties = true
    };

    public async Task<PhotoCapture> CreateAsync(
        NewPhotoCapture capture,
        string tokenDigest,
        PhotoLinkTokenPurpose tokenPurpose,
        string? sessionTokenDigest,
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
            QuickLinkFor = capture.QuickLinkFor is { } quickLinkFor ? PhotoCaptureCodes.ToCode(quickLinkFor) : null,
            LiveCameraOnly = capture.LiveCameraOnly,
            OriginalsInBackground = capture.OriginalsInBackground,
            IssuedByStaffId = capture.IssuedByStaffId,
            CreatedAtUtc = capture.CreatedAtUtc,
            ExpiresAtUtc = capture.ExpiresAtUtc,
            OpenedAtUtc = capture.OpenedAtUtc,
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
        if (sessionTokenDigest is not null)
        {
            context.PhotoCaptureTokens.Add(new()
            {
                TokenDigest = sessionTokenDigest,
                CaptureId = entity.Id,
                Purpose = PhotoCaptureCodes.ToCode(PhotoLinkTokenPurpose.Session),
                IssuedAtUtc = capture.CreatedAtUtc,
                IssuedByStaffId = capture.IssuedByStaffId
            });
        }
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

    public async Task<(PhotoCapture Capture, bool TokenRevoked, bool CaseArchived, PhotoLinkTokenPurpose Purpose)?> FindByTokenDigestAsync(
        string tokenDigest,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenDigest);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // The Case's archive is read in the same statement: a capture whose
        // Case is archived answers like an unknown token (R793).
        var row = await (
            from token in context.PhotoCaptureTokens.AsNoTracking()
            join capture in context.PhotoCaptures.AsNoTracking() on token.CaptureId equals capture.Id
            join workflow in context.CaseWorkflows.AsNoTracking() on capture.CaseId equals (Guid?)workflow.CaseId into workflows
            from workflow in workflows.DefaultIfEmpty()
            where token.TokenDigest == tokenDigest
            select new
            {
                Capture = capture,
                token.RevokedAtUtc,
                token.Purpose,
                CaseArchived = workflow != null && workflow.ArchivedAtUtc != null
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return null;
        }

        return (Map(row.Capture), row.RevokedAtUtc is not null, row.CaseArchived, PhotoCaptureCodes.ParseTokenPurpose(row.Purpose));
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
        return StoreConcurrencyRetry.ExecuteAsync(
            token => RecordRoutingOnceAsync(captureId, expectedVersion, destination, caseId, reason, token),
            cancellationToken,
            StoreConcurrencyRetry.IsDeadlockOrKeyCollision);
    }

    /// <summary>
    /// Under the capture row's lock, the one an item add takes, so no item
    /// row arrives between the check and the write: once the capture holds
    /// an item row its routing stands (FRD-28 "Registration").
    /// </summary>
    private async Task<PhotoCapture> RecordRoutingOnceAsync(
        Guid captureId,
        long expectedVersion,
        PhotoCaptureDestination destination,
        Guid? caseId,
        string reason,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        await context.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM [PhotoCaptures] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {captureId}",
            cancellationToken);
        var entity = await context.PhotoCaptures
            .SingleOrDefaultAsync(item => item.Id == captureId, cancellationToken)
            ?? throw new KeyNotFoundException("The photo link was not found.");
        if (entity.Version != expectedVersion)
        {
            throw new DbUpdateConcurrencyException("The photo link changed since it was read.");
        }

        var code = PhotoCaptureCodes.ToCode(destination);
        if (entity.PhotoCount + entity.DocumentCount + entity.VideoCount > 0)
        {
            if (string.Equals(entity.Destination, code, StringComparison.Ordinal) && entity.CaseId == caseId)
            {
                await transaction.CommitAsync(cancellationToken);
                return Map(entity);
            }

            throw new PhotoCaptureRefusalException(
                PhotoCaptureRefusals.RegistrationLocked,
                "The registration cannot change once photos have been sent.");
        }

        entity.Destination = code;
        entity.RoutingReason = reason;
        entity.CaseId = caseId;
        entity.CaseReference = caseId is { } id
            ? await context.Cases.AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => item.Reference)
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("The routed Case was not found.")
            : null;
        entity.Version++;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
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
        // The batch an item joins, and the Case it declares, are the capture's
        // as it is under this lock, never as the request read it (F-R6a,
        // F-R10): an item cannot land in a batch already settled.
        var current = Map(capture);
        var openBatch = current.CurrentBatch;

        var existing = await context.PhotoCaptureItems
            .SingleOrDefaultAsync(
                value => value.CaptureId == item.CaptureId && value.ClientItemId == item.ClientItemId,
                cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.ContentHash, item.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                await transaction.CommitAsync(cancellationToken);
                return new(PhotoCaptureItemWrite.Conflict, null);
            }

            if (existing.StagedReceiptId is null
                && existing.Batch < openBatch
                && !await StagedUnderOwnTokenAsync(context, existing, cancellationToken))
            {
                // A retry of an upload that never reached intake, whose batch
                // has settled since: it joins the open batch, declaring the
                // capture's Case as it is now. One that did reach intake keeps
                // its batch, so its retry finds the receipt it staged.
                existing.Batch = openBatch;
                existing.Ordinal = await NextOrdinalAsync(context, item.CaptureId, openBatch, cancellationToken);
                existing.DeclaredCaseId = current.DeclaredCaseId;
                capture.Version++;
                await context.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new(PhotoCaptureItemWrite.Replayed, Map(existing));
        }

        if (!WithinCaps(capture, item))
        {
            await transaction.CommitAsync(cancellationToken);
            return new(PhotoCaptureItemWrite.CapReached, null);
        }

        var ordinal = await NextOrdinalAsync(context, item.CaptureId, openBatch, cancellationToken);
        var entity = new PhotoCaptureItemEntity
        {
            Id = item.Id,
            CaptureId = item.CaptureId,
            ClientItemId = item.ClientItemId,
            Kind = PhotoCaptureCodes.ToCode(item.Kind),
            Area = item.Area,
            Stage = PhotoCaptureCodes.ToCode(item.Stage),
            Position = item.Position,
            Batch = openBatch,
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
            ReceivedAtUtc = item.ReceivedAtUtc,
            DeclaredCaseId = current.DeclaredCaseId
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

    /// <summary>The next ordinal in a batch: the batch's rows are only ever added to while it is open.</summary>
    private static Task<int> NextOrdinalAsync(
        PegasusDbContext context,
        Guid captureId,
        int batch,
        CancellationToken cancellationToken) =>
        context.PhotoCaptureItems.CountAsync(
            value => value.CaptureId == captureId && value.Batch == batch,
            cancellationToken);

    /// <summary>Whether intake staged a receipt under the item's own member token, whether or not the item recorded it.</summary>
    private static Task<bool> StagedUnderOwnTokenAsync(
        PegasusDbContext context,
        PhotoCaptureItemEntity item,
        CancellationToken cancellationToken)
    {
        var channel = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.PhotoCapture);
        var token = PhotoCaptureIntakeRules.MemberToken(item.CaptureId, item.Batch, item.Ordinal);
        return context.IntakeStagedReceipts.AsNoTracking().AnyAsync(
            receipt => receipt.SourceChannel == channel && receipt.ExternalReceiptToken == token,
            cancellationToken);
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

    public Task<bool> ReserveOriginalAsync(Guid itemId, long contentLength, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contentLength);
        return StoreConcurrencyRetry.ExecuteAsync(
            token => ChangeOriginalsAsync(itemId, contentLength, reserve: true, token),
            cancellationToken,
            StoreConcurrencyRetry.IsDeadlockOrKeyCollision);
    }

    public Task ReleaseOriginalAsync(Guid itemId, long contentLength, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(contentLength);
        return StoreConcurrencyRetry.ExecuteAsync(
            token => ChangeOriginalsAsync(itemId, contentLength, reserve: false, token),
            cancellationToken,
            StoreConcurrencyRetry.IsDeadlockOrKeyCollision);
    }

    /// <summary>
    /// Counts an original against its capture's caps, or gives it back, under
    /// the capture row's lock, the one an item add takes, so the originals
    /// and the bytes a link holds are decided by one writer at a time
    /// (<see cref="CaptureLimits.MaximumOriginalsPerCapture"/>,
    /// <see cref="CaptureLimits.MaximumBytesPerCapture"/>).
    /// </summary>
    private async Task<bool> ChangeOriginalsAsync(
        Guid itemId,
        long contentLength,
        bool reserve,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        var captureId = await context.PhotoCaptureItems.AsNoTracking()
            .Where(value => value.Id == itemId)
            .Select(value => (Guid?)value.CaptureId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("The photo link item was not found.");
        await context.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM [PhotoCaptures] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {captureId}",
            cancellationToken);
        var capture = await context.PhotoCaptures
            .SingleAsync(value => value.Id == captureId, cancellationToken);
        if (reserve)
        {
            if (capture.OriginalCount >= CaptureLimits.MaximumOriginalsPerCapture
                || capture.AcceptedBytes + contentLength > CaptureLimits.MaximumBytesPerCapture)
            {
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            capture.OriginalCount++;
            capture.AcceptedBytes += contentLength;
        }
        else
        {
            capture.OriginalCount = Math.Max(0, capture.OriginalCount - 1);
            capture.AcceptedBytes = Math.Max(0, capture.AcceptedBytes - contentLength);
        }

        capture.Version++;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task RecordItemOriginalAsync(Guid itemId, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var found = await context.PhotoCaptureItems
            .Where(value => value.Id == itemId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.OriginalReceived, true), cancellationToken);
        if (found == 0)
        {
            throw new KeyNotFoundException("The photo link item was not found.");
        }
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

    public Task<PhotoCaptureBatchClosed> CloseBatchAsync(
        Guid captureId,
        int batch,
        DateTimeOffset nowUtc,
        PhotoCaptureDeclaredGroup? group,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batch);
        return StoreConcurrencyRetry.ExecuteAsync(
            token => CloseBatchOnceAsync(captureId, batch, nowUtc, group, token),
            cancellationToken,
            StoreConcurrencyRetry.IsDeadlockOrKeyCollision);
    }

    /// <summary>
    /// Under the capture row's lock, the one an item add takes, so the batch
    /// an item joins and the batch that closes are decided by one writer at a
    /// time (F-R6b): no item lands in a batch after it closed, and a Send
    /// recorded since the listing changes nothing here.
    /// </summary>
    private async Task<PhotoCaptureBatchClosed> CloseBatchOnceAsync(
        Guid captureId,
        int batch,
        DateTimeOffset nowUtc,
        PhotoCaptureDeclaredGroup? group,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        await context.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM [PhotoCaptures] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {captureId}",
            cancellationToken);
        var capture = await context.PhotoCaptures
            .SingleOrDefaultAsync(item => item.Id == captureId, cancellationToken)
            ?? throw new KeyNotFoundException("The photo link was not found.");
        if (capture.SettledBatches >= batch)
        {
            await transaction.CommitAsync(cancellationToken);
            return new(PhotoCaptureBatchClose.AlreadyClosed, Map(capture), []);
        }

        if (batch != capture.SettledBatches + 1)
        {
            throw new InvalidOperationException("Only the capture's open batch can close.");
        }

        var items = await StoryOrdered(context.PhotoCaptureItems.AsNoTracking()
                .Where(item => item.CaptureId == captureId && item.Batch == batch))
            .ToListAsync(cancellationToken);
        var inFlightSince = nowUtc - SettlePhotoCaptures.UploadInFlightGrace;
        if (items.Exists(item => item.StagedReceiptId == null && item.ReceivedAtUtc > inFlightSince))
        {
            await transaction.CommitAsync(cancellationToken);
            return new(PhotoCaptureBatchClose.Waiting, Map(capture), []);
        }

        // An item with no receipt older than the grace never reached intake;
        // its retry joins the next batch (AddItemOnceAsync).
        var received = items.Where(item => item.StagedReceiptId != null).ToArray();
        if (group is not null)
        {
            var photographs = received
                .Where(item => PhotoCaptureStory.IsPhotographKind(PhotoCaptureCodes.ParseItemKind(item.Kind)))
                .OrderBy(item => item.Ordinal)
                .ToArray();
            if (photographs.Length > 0)
            {
                // Its members keep their items' ordinals, which their receipts'
                // member tokens already name; paperwork and video are not
                // members and wait for the Case (R803).
                var submissionGroup = await EfIntakeSubmissionGroupStore.GetOrAddGroupAsync(
                    context,
                    PhotoCaptureIntakeRules.BatchGroupId(captureId, batch),
                    IntakeSourceChannel.PhotoCapture,
                    PhotoCaptureIntakeRules.BatchToken(captureId, batch),
                    photographs.Length,
                    IntakeActorIdentity.PhotoCapture(captureId),
                    photographs[0].ReceivedAtUtc,
                    parentReceiptId: null,
                    group.Registration,
                    group.IncidentDate,
                    cancellationToken);
                foreach (var photograph in photographs)
                {
                    await EfIntakeSubmissionGroupStore.AddMemberEntityAsync(
                        context, submissionGroup, photograph.Ordinal, photograph.StagedReceiptId!.Value, cancellationToken);
                }

                capture.SubmissionGroupId = submissionGroup.Id;
            }
        }

        capture.SettledBatches = batch;
        // A Send recorded after this settle's listing but before its close is
        // for photos this batch holds, so the close covers it: the batch it
        // opens is listed again only on a Send after this one.
        capture.SettledAtUtc = capture.SentAtUtc is { } sent && sent > nowUtc ? sent : nowUtc;
        capture.Version++;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(PhotoCaptureBatchClose.Closed, Map(capture), received.Select(Map).ToArray());
    }

    public Task<PhotoCapture> RevokeAsync(
        Guid captureId,
        Pegasus.Core.Identity.ActionActor actor,
        string operationKey,
        DateTimeOffset revokedAtUtc,
        CancellationToken cancellationToken) =>
        ActAsync(captureId, actor, operationKey, PhotoLinkActs.Cancelled, revokedAtUtc, afterJson: null, entity =>
        {
            // A link already cancelled stays as it was, and no second act is recorded.
            if (entity.RevokedAtUtc is not null)
            {
                return false;
            }

            entity.RevokedAtUtc = revokedAtUtc;
            return true;
        }, cancellationToken);

    public Task<PhotoCapture> ExtendAsync(
        Guid captureId,
        Pegasus.Core.Identity.ActionActor actor,
        string operationKey,
        DateTimeOffset expiresAtUtc,
        CancellationToken cancellationToken) =>
        ActAsync(
            captureId,
            actor,
            operationKey,
            PhotoLinkActs.Extended,
            expiresAtUtc,
            DocumentActionHistory.Serialize(new { ExpiresAtUtc = expiresAtUtc }),
            entity =>
            {
                entity.ExpiresAtUtc = expiresAtUtc;
                return true;
            },
            cancellationToken);

    public Task<PhotoCapture> RecordReminderAsync(
        Guid captureId,
        Pegasus.Core.Identity.ActionActor actor,
        string operationKey,
        DateTimeOffset remindedAtUtc,
        CancellationToken cancellationToken) =>
        ActAsync(captureId, actor, operationKey, PhotoLinkActs.Reminded, remindedAtUtc, afterJson: null, entity =>
        {
            entity.ReminderCount++;
            entity.LastRemindedAtUtc = remindedAtUtc;
            return true;
        }, cancellationToken);

    public Task<PhotoCapture> RecordCallAsync(
        Guid captureId,
        Pegasus.Core.Identity.ActionActor actor,
        string operationKey,
        DateTimeOffset calledAtUtc,
        CancellationToken cancellationToken) =>
        ActAsync(captureId, actor, operationKey, PhotoLinkActs.CallLogged, calledAtUtc, afterJson: null, entity =>
        {
            entity.CallLoggedAtUtc = calledAtUtc;
            return true;
        }, cancellationToken);

    /// <summary>
    /// One staff act on the capture row and its <c>ActionHistory</c> event,
    /// saved together, so the link's history names who acted (R805). The
    /// operation key is the event's correlation: a key already recorded for
    /// this act on this capture changes nothing and answers the capture as it
    /// is; one recorded for anything else is refused. A lost race re-reads and
    /// decides again, so two clicks with one key act once.
    /// </summary>
    private Task<PhotoCapture> ActAsync(
        Guid captureId,
        Pegasus.Core.Identity.ActionActor actor,
        string operationKey,
        string act,
        DateTimeOffset occurredAtUtc,
        string? afterJson,
        Func<PhotoCaptureEntity, bool> change,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        var key = operationKey.Trim();
        var aggregateId = captureId.ToString("D");
        return StoreConcurrencyRetry.ExecuteAsync(
            async token =>
            {
                await using var context = await contextFactory.CreateDbContextAsync(token);
                var entity = await context.PhotoCaptures
                    .SingleOrDefaultAsync(item => item.Id == captureId, token)
                    ?? throw new KeyNotFoundException("The photo link was not found.");
                var replay = await context.ActionHistory.AsNoTracking()
                    .Where(item => item.AggregateType == HistoryAggregateType && item.CorrelationId == key)
                    .FirstOrDefaultAsync(token);
                if (replay is not null)
                {
                    if (!string.Equals(replay.AggregateId, aggregateId, StringComparison.Ordinal)
                        || !string.Equals(replay.EventKind, act, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("The operation key was already used for a different photo link action.");
                    }

                    return Map(entity);
                }

                if (change(entity))
                {
                    entity.Version++;
                    context.ActionHistory.Add(DocumentActionHistory.Succeeded(
                        HistoryAggregateType, aggregateId, act, actor, occurredAtUtc, key, afterJson: afterJson));
                    await context.SaveChangesAsync(token);
                }

                return Map(entity);
            },
            cancellationToken,
            StoreConcurrencyRetry.IsRetryable);
    }

    public async Task<IReadOnlyList<PhotoCaptureSettleCandidate>> ListAwaitingSettleAsync(
        DateTimeOffset nowUtc,
        PhotoCaptureSettleCursor? after,
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        var paperwork = PhotoCaptureCodes.ToCode(PhotoCaptureItemKind.Paperwork);
        var video = PhotoCaptureCodes.ToCode(PhotoCaptureItemKind.Video);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // A received photograph in the open batch, and PhotoCapture.IsDueToSettle
        // as SQL: a Send since the last settle, an expiry or a cancellation
        // (R792, R794), as a cancelled link's photos still settle.
        var listed = context.PhotoCaptures.AsNoTracking()
            .Where(capture => context.PhotoCaptureItems.Any(item => item.CaptureId == capture.Id
                    && item.Batch == capture.SettledBatches + 1
                    && item.StagedReceiptId != null
                    && item.Kind != paperwork
                    && item.Kind != video)
                && ((capture.SentAtUtc != null
                        && (capture.SettledAtUtc == null || capture.SettledAtUtc < capture.SentAtUtc))
                    || capture.ExpiresAtUtc <= nowUtc
                    || capture.RevokedAtUtc != null));
        if (after is { } cursor)
        {
            listed = listed.Where(capture => capture.CreatedAtUtc > cursor.CreatedAtUtc
                || (capture.CreatedAtUtc == cursor.CreatedAtUtc && capture.Id.CompareTo(cursor.Id) > 0));
        }

        var rows = await listed
            .OrderBy(capture => capture.CreatedAtUtc)
            .ThenBy(capture => capture.Id)
            .Take(maximumItems)
            .Select(capture => new
            {
                Capture = capture,
                CaseArchived = capture.CaseId != null && context.CaseWorkflows.Any(workflow =>
                    workflow.CaseId == capture.CaseId && workflow.ArchivedAtUtc != null),
                CaseIsTriage = capture.CaseId != null && !context.CaseWorkflows.Any(workflow =>
                    workflow.CaseId == capture.CaseId)
            })
            .ToListAsync(cancellationToken);
        var items = await ItemsByCaptureAsync(context, rows.Select(row => row.Capture.Id).ToArray(), cancellationToken);
        return rows
            .Select(row => new PhotoCaptureSettleCandidate(
                Map(row.Capture),
                items.TryGetValue(row.Capture.Id, out var list) ? list : [],
                row.Capture.CaseId,
                row.CaseArchived,
                row.CaseIsTriage))
            .ToArray();
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

    public async Task<IReadOnlyList<PhotoCaptureSettleCandidate>> ListSettledReachingCaseAsync(
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
        // Whether each reached Case is archived (R793) or a Triage Case,
        // which has no workflow and keeps no Notes.
        var cases = await context.Cases.AsNoTracking()
            .Where(item => EF.Parameter(caseIds).Contains(item.Id))
            .Select(item => new
            {
                item.Id,
                Archived = context.CaseWorkflows.Any(workflow => workflow.CaseId == item.Id && workflow.ArchivedAtUtc != null),
                Triage = !context.CaseWorkflows.Any(workflow => workflow.CaseId == item.Id)
            })
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var withItems = await WithItemsAsync(context, captures, cancellationToken);
        return withItems
            .Where(pending => cases.ContainsKey(reached[pending.Capture.Id]))
            .Select(pending =>
            {
                var reachedCase = cases[reached[pending.Capture.Id]];
                return new PhotoCaptureSettleCandidate(
                    pending.Capture, pending.Items, reachedCase.Id, reachedCase.Archived, reachedCase.Triage);
            })
            .ToArray();
    }

    public Task<PhotoCaptureCaseRecorded> RecordCaseAsync(
        Guid captureId,
        Guid caseId,
        string? routingReason,
        DateTimeOffset? imagesCompletedAtUtc,
        CancellationToken cancellationToken) =>
        StoreConcurrencyRetry.ExecuteAsync(
            async token =>
            {
                await using var context = await contextFactory.CreateDbContextAsync(token);
                var entity = await context.PhotoCaptures
                    .SingleOrDefaultAsync(item => item.Id == captureId, token)
                    ?? throw new KeyNotFoundException("The photo link was not found.");
                if (entity.CaseId is not null)
                {
                    // The Case a capture has stands: a second record or a
                    // repeated settle changes nothing.
                    return new PhotoCaptureCaseRecorded(Map(entity), Changed: false);
                }

                entity.CaseReference = await context.Cases.AsNoTracking()
                    .Where(item => item.Id == caseId)
                    .Select(item => item.Reference)
                    .SingleOrDefaultAsync(token)
                    ?? throw new KeyNotFoundException("The Case the photo link reached was not found.");
                entity.CaseId = caseId;
                entity.Destination = PhotoCaptureCodes.ToCode(PhotoCaptureDestination.Case);
                if (routingReason is not null)
                {
                    entity.RoutingReason = routingReason;
                }

                entity.ImagesCompletedAtUtc ??= imagesCompletedAtUtc;
                entity.Version++;
                await context.SaveChangesAsync(token);
                return new PhotoCaptureCaseRecorded(Map(entity), Changed: true);
            },
            cancellationToken,
            StoreConcurrencyRetry.IsRetryable);

    public async Task<bool> AllItemsSettledAsync(Guid captureId, int batch, Guid? caseId, CancellationToken cancellationToken)
    {
        var paperwork = PhotoCaptureCodes.ToCode(PhotoCaptureItemKind.Paperwork);
        var video = PhotoCaptureCodes.ToCode(PhotoCaptureItemKind.Video);
        const string completed = EfCaseFilingReplayStore.PromotionCompletedEventType;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // A received photograph settles once its receipt is evaluated, or its
        // work has failed for good (F-R12); on a Case, once it has also filed
        // there (the filing's completion marker) or its association has since
        // moved elsewhere. An item whose upload never completed has no staged
        // receipt and is not waited for.
        var photographs = await context.PhotoCaptureItems.AsNoTracking()
            .Where(item => item.CaptureId == captureId
                && item.Batch == batch
                && item.StagedReceiptId != null
                && item.Kind != paperwork
                && item.Kind != video)
            .Select(item => new
            {
                Evaluated = item.IntakeReceiptId != null,
                Failed = context.IntakeWorkItems.Any(work => work.StagedReceiptId == item.StagedReceiptId && work.State == "failed"),
                Filed = caseId != null && context.IntakeMutationHistory.Any(history =>
                    history.IntakeReceiptId == item.IntakeReceiptId
                    && history.CaseId == caseId
                    && history.EventType == completed),
                MovedElsewhere = caseId != null && context.IntakeManualAssociations.Any(association =>
                    association.IntakeReceiptId == item.IntakeReceiptId
                    && (!association.IsActive || association.CaseId != caseId))
            })
            .ToListAsync(cancellationToken);
        return photographs.TrueForAll(item =>
            item.Failed || (item.Evaluated && (caseId == null || item.Filed || item.MovedElsewhere)));
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
        entity.QuickLinkFor is { } quickLinkFor ? PhotoCaptureCodes.ParseQuickLinkFor(quickLinkFor) : null,
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
        entity.Version,
        entity.ImagesCompletedAtUtc);

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
        entity.ReceivedAtUtc,
        entity.DeclaredCaseId);
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
        var memberToken = await context.IntakeStagedReceipts.AsNoTracking()
            .Where(item => item.Id == stagedReceiptId && item.SourceChannel == channel)
            .Select(item => item.ExternalReceiptToken)
            .FirstOrDefaultAsync(cancellationToken);
        if (memberToken is null)
        {
            return;
        }

        var sourceAssetId = await SourceAssetIdAsync(context, processedReceiptId, cancellationToken);
        await RecordFromReceiptAsync(context, stagedReceiptId, memberToken, processedReceiptId, sourceAssetId, cancellationToken);
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

    private static async Task RecordFromReceiptAsync(
        PegasusDbContext context,
        Guid stagedReceiptId,
        string memberToken,
        Guid processedReceiptId,
        Guid? sourceAssetId,
        CancellationToken cancellationToken)
    {
        var item = await context.PhotoCaptureItems
            .SingleOrDefaultAsync(value => value.StagedReceiptId == stagedReceiptId, cancellationToken);
        if (item is null
            && PhotoCaptureIntakeRules.TryReadMemberToken(memberToken, out var captureId, out var batch, out var ordinal))
        {
            // The item records its staged receipt just after intake accepts
            // it. When this evaluation wins that race the item is found by the
            // member token its receipt carries, so it knows its asset before
            // its Case filing reads the basket it arrives in (FRD-28);
            // RecordItemReceiptAsync still records and counts the receipt.
            item = await context.PhotoCaptureItems
                .SingleOrDefaultAsync(
                    value => value.CaptureId == captureId
                        && value.Batch == batch
                        && value.Ordinal == ordinal
                        && value.StagedReceiptId == null,
                    cancellationToken);
        }

        if (item is null)
        {
            return;
        }

        item.IntakeReceiptId = processedReceiptId;
        item.IntakeAssetId = sourceAssetId;
    }
}
