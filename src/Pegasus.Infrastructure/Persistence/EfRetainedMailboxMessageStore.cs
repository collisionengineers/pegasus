using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The retained-mail read model: written by the poll, read by the workspace.
/// </summary>
internal sealed class EfRetainedMailboxMessageStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    IMemoryCache cache)
    : IRetainedMailboxMessageStore, IRetainedMailQueries, IRetainedMailClassificationStore
{
    // The mailbox list is kept in IMemoryCache like a person's release note
    // (R141's 30 s sliding window under the 300 s ceiling). A Mail settings save
    // evicts it; a mailbox first seen by a poll shows once the entry expires
    // (R334).
    private static readonly TimeSpan MailboxListSlidingWindow = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MailboxListCeiling = Pegasus.Core.Actors.StaffSessionPolicy.AccountReadLifetime;

    private static readonly object MailboxListKey = new();

    /// <summary>Drops the kept mailbox list, so a read begun before this call is never kept.</summary>
    internal static void EvictMailboxList(IMemoryCache memoryCache) =>
        EvictableCacheEntry.Evict(memoryCache, MailboxListKey);

    /// <summary>The preview's excerpt: eight lines or this many characters, whichever comes first.</summary>
    private const int ExcerptLength = 600;
    private const int ExcerptLines = 8;
    /// <summary>
    /// How much of the receipt's cleaned body the read paths excerpt from: the
    /// excerpt's length again as headroom for the forwarded header block it
    /// skips, so a long paragraph is still long enough to cut.
    /// </summary>
    private const int BodyHeadLength = ExcerptLength * 2;
    /// <summary>The retained column's length (<see cref="MailboxModelConfiguration"/>); the read paths excerpt the receipt's body head instead.</summary>
    internal const int StoredExcerptLength = 400;
    private static readonly char[] LineOrSpace = [' ', '\n'];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task RetainAsync(
        RetainedMailboxMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await FindExistingAsync(context, message, cancellationToken);
        if (existing is not null)
        {
            VerifySameMessage(existing, message);
            return;
        }

        var entity = new RetainedMailboxMessageEntity
        {
            Id = Guid.NewGuid(),
            MailboxId = message.MailboxId,
            MailboxAddress = message.MailboxAddress,
            // Inbound polling writes Inbox rows and an uploaded email is retained
            // under the Upload scope, which the mailbox workspace leaves out. Sent
            // and Deleted Items are declared scopes with no writer yet, and the
            // workspace says so rather than hiding the tab.
            FolderScope = ToCode(message.Folder),
            FolderIdentity = message.Metadata.FolderIdentity,
            ImmutableMessageId = message.ImmutableMessageId,
            ConversationIdentity = message.Metadata.ConversationIdentity,
            InternetMessageIdentity = message.Metadata.InternetMessageIdentity,
            CanonicalInternetMessageIdentity = CanonicalInternetMessageIdentity(message),
            InReplyToIdentitiesJson = message.Metadata.InReplyToIdentities.Count == 0
                ? null
                : JsonSerializer.Serialize(message.Metadata.InReplyToIdentities, JsonOptions),
            ExternalReceiptToken = message.ExternalReceiptToken,
            SenderAddress = message.Metadata.SenderAddress,
            SenderDisplayName = message.Metadata.SenderDisplayName,
            ToAddressesJson = JsonSerializer.Serialize(message.Metadata.ToAddresses, JsonOptions),
            CcAddressesJson = JsonSerializer.Serialize(message.Metadata.CcAddresses, JsonOptions),
            ReplyToAddressesJson = JsonSerializer.Serialize(message.Metadata.ReplyToAddresses, JsonOptions),
            Subject = message.Metadata.Subject,
            BodyExcerpt = Excerpt(message.Metadata.BodyPlainText, StoredExcerptLength),
            BodyPlainText = message.Metadata.BodyPlainText,
            IsRead = message.Metadata.IsRead,
            SourceLength = message.SourceLength,
            SourceSha256 = message.SourceSha256,
            ReceivedAtUtc = message.ReceivedAtUtc,
            RetainedAtUtc = message.RetainedAtUtc
        };
        var ordinal = 0;
        foreach (var attachment in message.Metadata.Attachments)
        {
            entity.Attachments.Add(new()
            {
                Id = Guid.NewGuid(),
                RetainedMailboxMessageId = entity.Id,
                RetainedMailboxMessage = entity,
                Ordinal = ordinal++,
                FileName = attachment.FileName,
                MediaType = attachment.MediaType,
                ContentLength = attachment.ContentLength
            });
        }

        context.RetainedMailboxMessages.Add(entity);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two ticks raced on the same redelivered message. The unique index
            // settled it, and the loser has nothing left to do because the row it
            // wanted to write is the row that is there. Anything else the database
            // refused is still a failure and still reaches the poll, which leaves
            // the cursor unadvanced.
            if (!await IsAlreadyRetainedAsync(message, cancellationToken))
            {
                throw;
            }
        }
    }

    public async Task<bool> HasRetainedMessageAsync(
        Guid mailboxId,
        string immutableMessageId,
        string canonicalInternetMessageIdentity,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(immutableMessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalInternetMessageIdentity);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // The canonical id is unique in the mailbox's Inbox, so this reads at most
        // one row. The item id is compared here, exactly, as the wake compares it:
        // the column takes the database's collation, which can ignore case, and
        // Graph ids do not.
        var inbox = ToCode(MailFolderScope.Inbox);
        var retainedImmutableMessageId = await context.RetainedMailboxMessages
            .AsNoTracking()
            .Where(item => item.MailboxId == mailboxId
                && item.FolderScope == inbox
                && item.CanonicalInternetMessageIdentity == canonicalInternetMessageIdentity)
            .Select(item => item.ImmutableMessageId)
            .SingleOrDefaultAsync(cancellationToken);
        return string.Equals(retainedImmutableMessageId, immutableMessageId, StringComparison.Ordinal);
    }

    public async Task<IReadOnlyList<int>> CountManyAsync(
        IReadOnlyList<MailWorkspaceScope> scopes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        if (scopes.Count == 0)
        {
            return [];
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<int>? union = null;
        for (var index = 0; index < scopes.Count; index++)
        {
            var scopeIndex = index;
            var rows = BuildMatches(context, scopes[index]).Select(_ => scopeIndex);
            union = union is null ? rows : union.Concat(rows);
        }

        var grouped = await union!
            .GroupBy(index => index)
            .Select(group => new { Index = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Index, item => item.Count, cancellationToken);
        return Enumerable.Range(0, scopes.Count)
            .Select(index => grouped.GetValueOrDefault(index))
            .ToArray();
    }

    public async Task<RetainedMailPage> ListAsync(
        MailWorkspaceScope scope,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var searchTerm = scope.SearchTerm?.Trim();
        var matches = BuildMatches(context, scope);

        // Counted and paged in SQL. Reading every row to take twenty-five of them
        // makes the list slower the more mail is retained, which is the one thing a
        // mailbox is guaranteed to accumulate.
        var totalCount = await matches.CountAsync(cancellationToken);
        var ordered = scope.OldestFirst
            ? matches.OrderBy(item => item.ReceivedAtUtc).ThenBy(item => item.Id)
            : matches.OrderByDescending(item => item.ReceivedAtUtc).ThenByDescending(item => item.Id);
        var rows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(SummaryProjection(context, searchTerm))
            .ToListAsync(cancellationToken);

        if (searchTerm is not null && rows.Count > 0)
        {
            rows = await AddSearchMatchesAsync(
                context,
                rows,
                searchTerm,
                cancellationToken);
        }

        var summaries = await MapSummariesAsync(context, rows, cancellationToken);
        return new(
            summaries,
            page,
            pageSize,
            totalCount,
            // A page with rows proves the scope holds retained mail.
            summaries.Count == 0 && await HasUnretainedHistoryAsync(context, scope, cancellationToken));
    }

    public async Task<RetainedMailCursorPage> ListByCursorAsync(
        MailWorkspaceScope scope,
        DateTimeOffset? beforeReceivedAtUtc,
        Guid? beforeId,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var searchTerm = scope.SearchTerm?.Trim();
        var matches = BuildMatches(context, scope);
        if (beforeReceivedAtUtc is not null)
        {
            var received = beforeReceivedAtUtc.Value;
            var id = beforeId!.Value;
            matches = matches.Where(item => item.ReceivedAtUtc < received
                || (item.ReceivedAtUtc == received && item.Id.CompareTo(id) < 0));
        }
        var rows = await matches
            .OrderByDescending(item => item.ReceivedAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(limit + 1)
            .Select(SummaryProjection(context, searchTerm))
            .ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;
        var pageHadRows = rows.Count > 0;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        if (searchTerm is not null && rows.Count > 0)
        {
            rows = await AddSearchMatchesAsync(context, rows, searchTerm, cancellationToken);
        }
        return new(
            await MapSummariesAsync(context, rows, cancellationToken),
            hasMore,
            !pageHadRows && await HasUnretainedHistoryAsync(context, scope, cancellationToken));
    }

    public async Task<RetainedMailDetail?> GetAsync(
        Guid id,
        CancellationToken cancellationToken,
        string? searchTerm = null)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.RetainedMailboxMessages
            .AsNoTracking()
            .Include(item => item.Attachments)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        // Retained scope only: a matching conversation identity never reaches for a
        // message this application has not already retained.
        var thread = entity.ConversationIdentity is null
            ? []
            : await context.RetainedMailboxMessages
                .AsNoTracking()
                .Where(item => item.MailboxId == entity.MailboxId
                    && item.FolderScope == entity.FolderScope
                    && item.ConversationIdentity == entity.ConversationIdentity)
                .OrderBy(item => item.ReceivedAtUtc)
                .ThenBy(item => item.Id)
                .Select(item => new RetainedMailThreadEntry(
                    item.Id,
                    item.SenderDisplayName,
                    item.SenderAddress,
                    item.Subject,
                    item.ReceivedAtUtc))
                .ToListAsync(cancellationToken);

        // The receipt's classification decision is read here once, for the
        // message's classification and for its classification dossier.
        var receipt = await context.IntakeReceipts
            .AsNoTracking()
            .Where(item => item.SourceChannel == "mailbox"
                && item.ExternalReceiptToken == entity.ExternalReceiptToken)
            .Select(item => new
            {
                item.Id,
                IntakeDecision = item.Decision,
                Decision = item.MailClassificationDecision,
                Route = item.MailRouteDecision!.Disposition,
                EffectiveSenderAddress = item.MailRouteDecision!.EffectiveSenderAddress,
                RoutePrincipalCode = item.MailRouteDecision!.PrincipalCode,
                DraftRegistration = item.InstructionDraft == null
                    ? null
                    : item.InstructionDraft.VehicleRegistration,
                BodySearchText = item.SearchDocuments
                    .Where(document => document.AttachmentFileName == null)
                    .Select(document => document.Text)
                    .SingleOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);
        var summaryRows = new List<SummaryRow>
        {
            new(
                entity.Id,
                entity.MailboxId ?? UploadedCorrespondence.MailboxId,
                entity.MailboxAddress,
                entity.SenderAddress,
                entity.SenderDisplayName,
                entity.Subject,
                entity.BodyExcerpt,
                entity.ReceivedAtUtc,
                entity.IsRead,
                entity.Attachments.Count,
                entity.ExternalReceiptToken,
                searchTerm is not null
                    && receipt?.BodySearchText?.Contains(
                        searchTerm,
                        StringComparison.OrdinalIgnoreCase) == true,
                entity.BodyPlainText,
                null,
                entity.DismissedAtUtc)
        };
        if (searchTerm is not null)
        {
            summaryRows = await AddSearchMatchesAsync(
                context,
                summaryRows,
                searchTerm,
                cancellationToken);
        }
        // The summary is built from the receipt read above, not a second read of it.
        var summary = (await MapSummariesAsync(
            context,
            summaryRows,
            cancellationToken,
            receipt is null
                ? []
                : [new ReceiptFacts(
                    receipt.Id,
                    entity.ExternalReceiptToken,
                    receipt.IntakeDecision,
                    receipt.Decision,
                    receipt.EffectiveSenderAddress,
                    receipt.BodySearchText is { } bodyText
                        ? bodyText[..Math.Min(bodyText.Length, BodyHeadLength)]
                        : null,
                    receipt.RoutePrincipalCode,
                    receipt.DraftRegistration)]))[0];

        // A staff forward is de-cluttered on read so existing (write-once) rows
        // are corrected too: the effective sender differs from the transport
        // sender exactly when the route unwrapped a Collision Engineers forward.
        var isStaffForward = summary.EffectiveSenderAddress is { } effectiveSender
            && !string.Equals(effectiveSender, entity.SenderAddress, StringComparison.OrdinalIgnoreCase);
        var body = receipt?.BodySearchText
            ?? StaffForwardBodyCleaner.Clean(entity.BodyPlainText ?? string.Empty, isStaffForward);

        var attachmentDocuments = receipt is null
            ? []
            : await context.Set<IntakeSearchDocumentEntity>().AsNoTracking()
                .Where(item => item.IntakeReceiptId == receipt.Id
                    && item.AttachmentOrdinal != null)
                .Select(item => new
                {
                    Ordinal = item.AttachmentOrdinal!.Value,
                    item.SourceLabel,
                    IsSearchable = item.AttachmentFileName != null && item.Text != null
                })
                .ToListAsync(cancellationToken);
        var searchableOrdinals = attachmentDocuments.Where(item => item.IsSearchable)
            .Select(item => item.Ordinal)
            .ToHashSet();
        var attachmentAssets = receipt is null
            ? []
            : await context.Set<IntakeAssetEntity>().AsNoTracking()
                .Where(item => item.IntakeReceiptId == receipt.Id
                    && item.Kind == "attachment")
                .Select(item => new { item.Id, item.SourceLabel })
                .ToListAsync(cancellationToken);
        var sourceLabelsByOrdinal = attachmentDocuments
            .GroupBy(item => item.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.SourceLabel).Distinct(StringComparer.Ordinal).ToArray());
        var assetIdsBySourceLabel = attachmentAssets
            .GroupBy(item => item.SourceLabel, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Id).Distinct().ToArray(),
                StringComparer.Ordinal);

        return new(
            summary,
            Deserialize(entity.ToAddressesJson),
            Deserialize(entity.CcAddressesJson),
            entity.ReplyToAddressesJson is null
                ? null
                : Deserialize(entity.ReplyToAddressesJson),
            body,
            entity.Attachments
                .OrderBy(item => item.Ordinal)
                .Select(item => new RetainedMailAttachment(
                    item.FileName,
                    item.MediaType,
                    item.ContentLength,
                    searchableOrdinals.Contains(item.Ordinal),
                    IntakeAssetId(item.Ordinal, sourceLabelsByOrdinal, assetIdsBySourceLabel)))
                .ToArray(),
            thread,
            ParseFolderScope(entity.FolderScope),
            receipt?.Decision?.Outcome is { } classification
                ? ParseClassificationOutcome(classification)
                : null,
            receipt?.Route is { } route ? ParseRouteDisposition(route) : null,
            entity.ImmutableMessageId,
            entity.InternetMessageIdentity,
            entity.ConversationIdentity,
            await ClassificationDossierAsync(
                context,
                receipt?.Decision,
                cancellationToken));
    }

    /// <summary>
    /// The Inbox preview pane's read. A summary the caller already holds from the
    /// list is kept as it is, search matches included; without one the message's
    /// own summary is read. Neither reads the thread or the staff names.
    /// </summary>
    public async Task<RetainedMailPreview?> GetPreviewAsync(
        Guid id,
        RetainedMailSummary? summary,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        string folderScope;
        IReadOnlyList<RetainedMailAttachment> attachments;
        if (summary is null)
        {
            var entity = await context.RetainedMailboxMessages
                .AsNoTracking()
                .Include(item => item.Attachments)
                .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (entity is null)
            {
                return null;
            }

            summary = (await MapSummariesAsync(
                context,
                [
                    new SummaryRow(
                        entity.Id,
                        entity.MailboxId ?? UploadedCorrespondence.MailboxId,
                        entity.MailboxAddress,
                        entity.SenderAddress,
                        entity.SenderDisplayName,
                        entity.Subject,
                        entity.BodyExcerpt,
                        entity.ReceivedAtUtc,
                        entity.IsRead,
                        entity.Attachments.Count,
                        entity.ExternalReceiptToken,
                        false,
                        entity.BodyPlainText,
                        null,
                        entity.DismissedAtUtc)
                ],
                cancellationToken))[0];
            folderScope = entity.FolderScope;
            attachments = entity.Attachments
                .OrderBy(item => item.Ordinal)
                .Select(item => PreviewAttachment(item.FileName, item.MediaType, item.ContentLength))
                .ToArray();
        }
        else
        {
            // The summary the list read already holds the body-derived fields
            // and the classification, so only the folder scope and the
            // attachment rows are read.
            var held = await context.RetainedMailboxMessages
                .AsNoTracking()
                .Where(item => item.Id == id)
                .Select(item => new
                {
                    item.FolderScope,
                    Attachments = item.Attachments
                        .OrderBy(attachment => attachment.Ordinal)
                        .Select(attachment => new
                        {
                            attachment.FileName,
                            attachment.MediaType,
                            attachment.ContentLength
                        })
                        .ToList()
                })
                .SingleOrDefaultAsync(cancellationToken);
            if (held is null)
            {
                return null;
            }

            folderScope = held.FolderScope;
            attachments = held.Attachments
                .Select(item => PreviewAttachment(item.FileName, item.MediaType, item.ContentLength))
                .ToArray();
        }

        return new(summary, attachments, summary.Classification, ParseFolderScope(folderScope));
    }

    private static RetainedMailAttachment PreviewAttachment(string fileName, string mediaType, long contentLength) =>
        new(fileName, mediaType, contentLength, false, null);

    public async Task<RetainedMailDetail?> GetByOriginReceiptAsync(
        Guid originReceiptId,
        CancellationToken cancellationToken) =>
        await FindIdByOriginReceiptAsync(originReceiptId, cancellationToken) is { } retainedId
            ? await GetAsync(retainedId, cancellationToken)
            : null;

    private async Task<Guid?> FindIdByOriginReceiptAsync(
        Guid originReceiptId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var retainedIds = await (
                from receipt in context.IntakeReceipts.AsNoTracking()
                join retained in context.RetainedMailboxMessages.AsNoTracking()
                    on receipt.ExternalReceiptToken equals retained.ExternalReceiptToken
                where receipt.Id == originReceiptId
                    && receipt.SourceChannel == "mailbox"
                    && receipt.ExternalReceiptToken != ""
                select retained.Id)
            .Take(2)
            .ToListAsync(cancellationToken);
        return SingleRetainedId(retainedIds, "The origin receipt");
    }

    public async Task<Guid?> FindIdByExternalReceiptTokenAsync(
        string externalReceiptToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(externalReceiptToken))
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var retainedIds = await context.RetainedMailboxMessages
            .AsNoTracking()
            .Where(retained => retained.ExternalReceiptToken == externalReceiptToken)
            .Select(retained => retained.Id)
            .Take(2)
            .ToListAsync(cancellationToken);
        return SingleRetainedId(retainedIds, "The external receipt token");
    }

    private static Guid? SingleRetainedId(List<Guid> retainedIds, string subject) =>
        retainedIds.Count switch
        {
            0 => null,
            1 => retainedIds[0],
            _ => throw new InvalidOperationException(
                $"{subject} matches more than one retained mailbox message.")
        };

    /// <summary>
    /// The message's receipt as the detail and the summaries find it (the
    /// mailbox receipt with the message's token), without loading the message,
    /// its attachments or its summary.
    /// </summary>
    public async Task<Guid?> FindReceiptIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await (
                from retained in context.RetainedMailboxMessages.AsNoTracking()
                join receipt in context.IntakeReceipts.AsNoTracking()
                    on retained.ExternalReceiptToken equals receipt.ExternalReceiptToken
                where retained.Id == id && receipt.SourceChannel == "mailbox"
                select (Guid?)receipt.Id)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> ListPrincipalsAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Set<PrincipalEntity>().AsNoTracking()
            .Where(principal => principal.IsActive)
            .OrderBy(principal => principal.Code)
            .Select(principal => principal.Code)
            .ToArrayAsync(cancellationToken);
    }

    public Task<IReadOnlyList<RetainedMailMailbox>> ListMailboxesAsync(
        CancellationToken cancellationToken) =>
        EvictableCacheEntry.GetOrReadAsync(
            cache,
            MailboxListKey,
            MailboxListCeiling,
            MailboxListSlidingWindow,
            ReadMailboxesAsync,
            cancellationToken);

    private async Task<IReadOnlyList<RetainedMailMailbox>> ReadMailboxesAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var mailboxes = await context.RetainedMailboxMessages
            .AsNoTracking()
            // Uploaded emails have no mailbox; they are read from their Case.
            .Where(item => item.FolderScope != ToCode(MailFolderScope.Upload))
            .GroupBy(item => new { item.MailboxId, item.MailboxAddress })
            .Select(group => new
            {
                MailboxId = group.Key.MailboxId ?? UploadedCorrespondence.MailboxId,
                group.Key.MailboxAddress
            })
            .ToListAsync(cancellationToken);
        var approvedState = ApprovedMailboxState.Approved.ToString();
        var polled = await context.ApprovedMailboxes
            .AsNoTracking()
            .Where(item => item.State == approvedState && item.AllowIncoming)
            .Select(item => item.Address)
            .ToListAsync(cancellationToken);
        var polledAddresses = polled.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return mailboxes
            .Select(item => new RetainedMailMailbox(
                item.MailboxId,
                item.MailboxAddress,
                polledAddresses.Contains(item.MailboxAddress)))
            .OrderBy(item => item.MailboxAddress, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<MailPollHealth>> ListPollHealthAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ApprovedInboxPollStates
            .AsNoTracking()
            .Select(item => new MailPollHealth(
                item.ApprovedMailboxId,
                item.LastCompletedAtUtc,
                item.LastFailureCode,
                item.DueAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<MailClassificationDossier?> GetClassificationAsync(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await LoadClassificationAsync(context, messageId, cancellationToken);
    }

    public async Task<MailClassificationCorrectionResult> AppendCorrectionAsync(
        Guid messageId,
        int expectedVersion,
        MailClassificationResult before,
        MailClassificationResult after,
        ActionActor actor,
        string reason,
        DateTimeOffset correctedAtUtc,
        CancellationToken cancellationToken)
    {
        var packedActor = MailClassificationActor.Format(actor);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginReadCommittedAsync(cancellationToken);
        var retained = await context.RetainedMailboxMessages
            .SingleOrDefaultAsync(item => item.Id == messageId, cancellationToken)
            ?? throw new InvalidOperationException("The retained message no longer exists.");
        var receipt = await context.IntakeReceipts
            .Include(item => item.MailClassificationDecision)
            .Where(item => item.SourceChannel == "mailbox"
                && item.ExternalReceiptToken == retained.ExternalReceiptToken)
            .SingleOrDefaultAsync(cancellationToken);
        var decision = receipt?.MailClassificationDecision
            ?? throw new InvalidOperationException("The retained message has no classification decision.");
        if (decision.Version != expectedVersion
            || !string.Equals(
                SerializeSnapshot(EfIntakeReceiptStore.MapMailClassificationDecision(decision)),
                SerializeSnapshot(before),
                StringComparison.Ordinal))
        {
            throw new MailClassificationConcurrencyException();
        }

        // The Case this message is currently linked to, so a cancellation
        // correction can notify its Engineer. A category change does not open
        // or withdraw a query.
        var associations = await CurrentIntakeAssociations.ReadAsync(context, [receipt.Id], cancellationToken);
        var linkedCase = associations.Current.TryGetValue(receipt.Id, out var association) && !association.IsTriage
            ? association
            : null;

        Apply(after, decision);
        decision.Version++;
        decision.DecidedByActor = packedActor;
        decision.DecidedAtUtc = correctedAtUtc;
        // FRD-03: the accepted Triage match is the classification's evidence,
        // so a correction to or from Triage request writes or removes it and
        // the receipt moves on a version, as any change to it does.
        if (MailTriageMatch.Reconcile(EfIntakeReceiptStore.DeserializeEvidence(receipt.EvidenceJson), after) is { } evidence)
        {
            receipt.EvidenceJson = EfIntakeReceiptStore.SerializeEvidence(evidence);
            receipt.Version++;
        }
        var afterJson = SerializeSnapshot(after);
        context.IntakeMailClassificationHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = decision.IntakeReceiptId,
            ClassificationDecision = decision,
            Version = decision.Version,
            BeforeJson = SerializeSnapshot(before),
            AfterJson = afterJson,
            Actor = packedActor,
            Reason = reason,
            CorrectedAtUtc = correctedAtUtc
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new MailClassificationConcurrencyException();
        }

        var dossier = (await LoadClassificationAsync(context, messageId, cancellationToken))!;
        return new(dossier, linkedCase?.CaseId);
    }

    private static async Task<MailClassificationDossier?> LoadClassificationAsync(
        PegasusDbContext context,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var message = await context.RetainedMailboxMessages
            .AsNoTracking()
            .Where(item => item.Id == messageId)
            .Select(item => item.ExternalReceiptToken)
            .SingleOrDefaultAsync(cancellationToken);
        return message is null
            ? null
            : await LoadClassificationByTokenAsync(
                context,
                message,
                cancellationToken);
    }

    /// <summary>The classification dossier of the message whose receipt token is known.</summary>
    private static async Task<MailClassificationDossier?> LoadClassificationByTokenAsync(
        PegasusDbContext context,
        string externalReceiptToken,
        CancellationToken cancellationToken)
    {
        var decision = await context.IntakeReceipts
            .AsNoTracking()
            .Where(item => item.SourceChannel == "mailbox"
                && item.ExternalReceiptToken == externalReceiptToken)
            .Select(item => item.MailClassificationDecision)
            .SingleOrDefaultAsync(cancellationToken);
        return await ClassificationDossierAsync(context, decision, cancellationToken);
    }

    /// <summary>
    /// The classification dossier of a decision already read: only its
    /// correction history is read here. No decision, no dossier.
    /// </summary>
    private static async Task<MailClassificationDossier?> ClassificationDossierAsync(
        PegasusDbContext context,
        IntakeMailClassificationDecisionEntity? decision,
        CancellationToken cancellationToken)
    {
        if (decision is null)
        {
            return null;
        }
        var historyRows = await context.IntakeMailClassificationHistory
            .AsNoTracking()
            .Where(item => item.IntakeReceiptId == decision.IntakeReceiptId)
            .OrderBy(item => item.Version)
            .ToListAsync(cancellationToken);
        var history = historyRows
            .Select(item => new MailClassificationHistoryEntry(
                item.Version,
                DeserializeSnapshot(item.BeforeJson),
                DeserializeSnapshot(item.AfterJson),
                item.Actor,
                item.Reason,
                item.CorrectedAtUtc))
            .ToArray();
        return new(
            decision.Version,
            EfIntakeReceiptStore.MapMailClassificationDecision(decision),
            decision.DecidedByActor,
            decision.DecidedAtUtc,
            history);
    }

    private static void Apply(
        MailClassificationResult source,
        IntakeMailClassificationDecisionEntity target)
    {
        target.Outcome = source.Outcome switch
        {
            MailClassificationOutcome.Classified => "classified",
            MailClassificationOutcome.Unclassified => "unclassified",
            _ => throw new InvalidOperationException("Unknown mail classification outcome.")
        };
        target.Category = source.Category is { } category ? MailCategories.Code(category) : null;
        target.IsReplyContext = source.IsReplyContext;
        target.OpensQuery = source.OpensQuery;
        target.CaseType = source.CaseType is { } caseType
            ? CaseTypeCodes.ToCode(caseType)
            : null;
        target.StandaloneAuditReportAssetSourceLabel = source.StandaloneAuditReport?.AssetSourceLabel;
        target.StandaloneAuditReportAssessment = source.StandaloneAuditReport is { } report
            ? EfIntakeReceiptStore.ToCode(report.Assessment)
            : null;
        target.PredicatesJson = EfIntakeReceiptStore.SerializeEnvelope(source.Predicates);
        target.Reason = source.Reason;
        target.PolicyKey = source.PolicyKey;
        target.PolicyVersion = source.PolicyVersion;
    }

    private static Guid? IntakeAssetId(
        int attachmentOrdinal,
        Dictionary<int, string[]> sourceLabelsByOrdinal,
        Dictionary<string, Guid[]> assetIdsBySourceLabel)
    {
        if (!sourceLabelsByOrdinal.TryGetValue(attachmentOrdinal, out var sourceLabels)
            || sourceLabels.Length != 1
            || !assetIdsBySourceLabel.TryGetValue(sourceLabels[0], out var assetIds)
            || assetIds.Length != 1)
        {
            return null;
        }

        return assetIds[0];
    }

    private static string SerializeSnapshot(MailClassificationResult value) =>
        JsonSerializer.Serialize(ClassificationSnapshot.From(value), JsonOptions);

    private static MailClassificationResult DeserializeSnapshot(string value) =>
        JsonSerializer.Deserialize<ClassificationSnapshot>(value, JsonOptions)?.ToResult()
        ?? throw new InvalidDataException("Classification history contains an invalid snapshot.");

    /// <summary>
    /// The persisted before/after shape of one correction. The category is its
    /// stored code, so a later reordering of the enum cannot reread history.
    /// </summary>
    private sealed record ClassificationSnapshot(
        MailClassificationOutcome Outcome,
        string? Category,
        bool IsReplyContext,
        IReadOnlyList<MailClassificationPredicateResult> Predicates,
        string Reason,
        string PolicyKey,
        int PolicyVersion,
        CaseType? CaseType,
        StandaloneAuditReportEvaluation? StandaloneAuditReport,
        bool OpensQuery = false)
    {
        public static ClassificationSnapshot From(MailClassificationResult value) => new(
            value.Outcome,
            value.Category is { } category ? MailCategories.Code(category) : null,
            value.IsReplyContext,
            value.Predicates,
            value.Reason,
            value.PolicyKey,
            value.PolicyVersion,
            value.CaseType,
            value.StandaloneAuditReport,
            value.OpensQuery);

        public MailClassificationResult ToResult() => new(
            Outcome,
            Category is null ? null : MailCategories.Parse(Category),
            Predicates,
            Reason,
            PolicyKey,
            PolicyVersion,
            CaseType,
            StandaloneAuditReport,
            IsReplyContext,
            OpensQuery);
    }

    private async Task<bool> IsAlreadyRetainedAsync(
        RetainedMailboxMessage message,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await FindExistingAsync(context, message, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        VerifySameMessage(existing, message);
        return true;
    }

    private static async Task<RetainedMailboxMessageEntity?> FindExistingAsync(
        PegasusDbContext context,
        RetainedMailboxMessage message,
        CancellationToken cancellationToken)
    {
        // The Message-ID is unique per folder: the Sent copy of a message the
        // mailbox also received is its own item, not the Inbox row.
        var canonicalIdentity = CanonicalInternetMessageIdentity(message);
        var folder = ToCode(message.Folder);
        return await context.RetainedMailboxMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.MailboxId == message.MailboxId
                    && (item.ImmutableMessageId == message.ImmutableMessageId
                        || (canonicalIdentity != null
                            && item.FolderScope == folder
                            && item.CanonicalInternetMessageIdentity == canonicalIdentity)),
                cancellationToken);
    }

    private static void VerifySameMessage(
        RetainedMailboxMessageEntity existing,
        RetainedMailboxMessage message)
    {
        if (!string.Equals(
                existing.CanonicalInternetMessageIdentity,
                CanonicalInternetMessageIdentity(message),
                StringComparison.Ordinal)
            || !string.Equals(existing.SourceSha256, message.SourceSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The mailbox item identities contradict an already retained message.");
        }
    }

    /// <summary>
    /// The canonical Message-ID, or null for a message retained without one: an
    /// uploaded email is identified by its bytes alone, so a re-saved copy of the
    /// same message is a second retained row rather than a contradiction.
    /// </summary>
    private static string? CanonicalInternetMessageIdentity(RetainedMailboxMessage message) =>
        message.Metadata.InternetMessageIdentity is { } identity
            ? MailboxMessageIdentity.CanonicalizeInternetMessageIdentity(identity)
            : null;

    /// <summary>
    /// True where a mailbox in scope has polled successfully but this scope holds no
    /// retained rows: the messages that poll brought in predate message-level
    /// retention and nothing reconstructs them.
    /// </summary>
    private static async Task<bool> HasUnretainedHistoryAsync(
        PegasusDbContext context,
        MailWorkspaceScope scope,
        CancellationToken cancellationToken)
    {
        if (scope.Folder is not (MailFolderScope.Inbox or MailFolderScope.Sent))
        {
            return false;
        }

        var retained = context.RetainedMailboxMessages.AsNoTracking()
            .Where(item => item.FolderScope == ToCode(scope.Folder));
        if (scope.MailboxId is { } mailboxId)
        {
            retained = retained.Where(item => item.MailboxId == mailboxId);
        }
        if (scope.Folder == MailFolderScope.Sent)
        {
            // The Sent poll keys its state by the provider's mailbox identity;
            // the approved mailbox row joins the two.
            var completedSentPolls = context.ApprovedSentPollStates
                .AsNoTracking()
                .Where(item => item.LastCompletedAtUtc != null);
            if (scope.MailboxId is { } sentMailboxId)
            {
                completedSentPolls = completedSentPolls.Where(item =>
                    context.ApprovedMailboxes.Any(mailbox =>
                        mailbox.Id == sentMailboxId && mailbox.Address == item.MailboxAddress));
            }
            return await completedSentPolls.AnyAsync(cancellationToken)
                && !await retained.AnyAsync(cancellationToken);
        }

        var completedPolls = context.ApprovedInboxPollStates
            .AsNoTracking()
            .Where(item => item.LastCompletedAtUtc != null);
        if (scope.MailboxId is { } inboxMailboxId)
        {
            completedPolls = completedPolls.Where(item => item.ApprovedMailboxId == inboxMailboxId);
        }

        return await completedPolls.AnyAsync(cancellationToken)
            && !await retained.AnyAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<RetainedMailSummary>> MapSummariesAsync(
        PegasusDbContext context,
        IReadOnlyList<SummaryRow> rows,
        CancellationToken cancellationToken,
        IReadOnlyList<ReceiptFacts>? knownReceipts = null)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        // Six lookups for the whole page, never one per row; none for the
        // receipts a caller has already read.
        var tokens = rows.Select(item => item.ExternalReceiptToken).Distinct().ToArray();
        var receipts = knownReceipts ?? await context.IntakeReceipts
            .AsNoTracking()
            .Where(item => item.SourceChannel == "mailbox"
                && tokens.Contains(item.ExternalReceiptToken))
            .Select(item => new ReceiptFacts(
                item.Id,
                item.ExternalReceiptToken,
                item.Decision,
                item.MailClassificationDecision,
                item.MailRouteDecision == null
                    ? null
                    : item.MailRouteDecision.EffectiveSenderAddress,
                // Enough cleaned body to excerpt from once the forwarded
                // header block is skipped; never the whole document text.
                item.SearchDocuments
                    .Where(document => document.AttachmentFileName == null && document.Text != null)
                    .Select(document => document.Text!.Substring(0, BodyHeadLength))
                    .FirstOrDefault(),
                item.MailRouteDecision == null ? null : item.MailRouteDecision.PrincipalCode,
                item.InstructionDraft == null ? null : item.InstructionDraft.VehicleRegistration))
            .ToListAsync(cancellationToken);
        var receiptsByToken = receipts.ToDictionary(
            item => item.ExternalReceiptToken,
            StringComparer.Ordinal);
        var receiptIds = receipts.Select(item => item.Id).ToArray();
        var associations = await CurrentIntakeAssociations.ReadAsync(
            context,
            receiptIds,
            cancellationToken);
        var allocationStates = receiptIds.Length == 0
            ? new Dictionary<Guid, IntakeAllocationState>()
            : (await context.IntakeAllocationAttempts
                .AsNoTracking()
                .Where(item => receiptIds.Contains(item.IntakeReceiptId))
                .OrderByDescending(item => item.AttemptNumber)
                .ToListAsync(cancellationToken))
                .GroupBy(item => item.IntakeReceiptId)
                .ToDictionary(
                    group => group.Key,
                    group => IntakeAllocationState.FromAttempt(
                        EfIntakeAllocationStore.Map(group.First())));
        // A Triage opened from the receipt is the message's Case as much as an
        // instruction Case is; the intake log reads it the same way. The
        // unique index on Triage.OriginReceiptId allows one Triage per receipt,
        // whatever its state, so a cancelled Triage is never followed by a
        // second from the same receipt.
        var triageCases = receiptIds.Length == 0
            ? new Dictionary<Guid, CurrentIntakeAssociation>()
            : await context.Triage
                .AsNoTracking()
                .Where(item => item.OriginReceiptId != null && receiptIds.Contains(item.OriginReceiptId.Value))
                .Select(item => new
                {
                    ReceiptId = item.OriginReceiptId!.Value,
                    item.CaseId,
                    item.Case.Reference,
                    IsTriage = item.Case.Type == CaseTypeCodes.Triage
                })
                .ToDictionaryAsync(
                    item => item.ReceiptId,
                    item => new CurrentIntakeAssociation(item.CaseId, item.Reference, item.IsTriage),
                    cancellationToken);
        // One Cases read and one CaseQueries read for the page, never one per row.
        var pageCaseIds = associations.Current.Values.Select(item => item.CaseId)
            .Concat(allocationStates.Values.Where(item => item.CaseId is not null).Select(item => item.CaseId!.Value))
            .Concat(triageCases.Values.Select(item => item.CaseId))
            .Distinct()
            .ToArray();
        var caseFacts = pageCaseIds.Length == 0
            ? []
            : await (
                from caseEntity in context.Set<CaseEntity>().AsNoTracking()
                where pageCaseIds.Contains(caseEntity.Id)
                join principal in context.Set<PrincipalEntity>().AsNoTracking()
                    on caseEntity.PrincipalId equals principal.Id
                join registrationCandidate in context.CaseDataFields.AsNoTracking()
                        .Where(item => item.FieldName == CaseDataFieldNames.VehicleRegistration
                            && item.ValueKind == CaseDataCodes.Confirmed)
                    on caseEntity.Id equals registrationCandidate.WorkId into registrations
                from registration in registrations.DefaultIfEmpty()
                join draftCandidate in context.Set<InstructionDraftEntity>().AsNoTracking()
                    on caseEntity.OriginIntakeReceiptId equals draftCandidate.IntakeReceiptId into drafts
                from draft in drafts.DefaultIfEmpty()
                select new
                {
                    caseEntity.Id,
                    caseEntity.Type,
                    PrincipalCode = principal.Code,
                    Registration = registration == null ? draft.VehicleRegistration : registration.Value
                })
                .ToListAsync(cancellationToken);
        var casesById = caseFacts.ToDictionary(item => item.Id);
        var withdrawn = nameof(CaseQueryClosure.Withdrawn);
        var queryRows = receiptIds.Length == 0
            ? []
            : await (
                from query in context.CaseQueries.AsNoTracking()
                where query.IntakeReceiptId != null
                    && receiptIds.Contains(query.IntakeReceiptId.Value)
                    && query.ClosedAs != withdrawn
                join userCandidate in context.Users.AsNoTracking()
                    on query.AssigneeStaffId equals userCandidate.Id into users
                from user in users.DefaultIfEmpty()
                select new
                {
                    query.Id,
                    query.CaseId,
                    ReceiptId = query.IntakeReceiptId!.Value,
                    IsOpen = query.ClosedAtUtc == null,
                    AssignedTo = user == null ? null : user.UserName
                })
                .ToListAsync(cancellationToken);
        var queryByReceipt = queryRows
            .GroupBy(item => item.ReceiptId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.IsOpen).ThenBy(item => item.Id).First());
        return rows
            .Select(row =>
            {
                receiptsByToken.TryGetValue(row.ExternalReceiptToken, out var receipt);
                var linkedCase = receipt is null
                    ? null
                    : associations.Current.GetValueOrDefault(receipt.Id);
                var allocationState = receipt is null
                    ? null
                    : allocationStates.GetValueOrDefault(receipt.Id);
                // Once the association has been reversed the
                // allocation's record of the case it created no longer
                // stands in for it, or unlinking would visibly do nothing.
                var allocationCase = receipt is not null
                    && associations.AllocationMayStandIn(receipt.Id)
                        ? allocationState
                        : null;
                var triageCase = receipt is null
                    ? null
                    : triageCases.GetValueOrDefault(receipt.Id);
                // The manual acceptance route writes a CaseIntakeLinks row; the
                // automatic allocation route records its created case on the
                // succeeded attempt; a Triage request records the Triage Case it
                // opened. Any one of them is the case.
                var caseId = linkedCase?.CaseId ?? allocationCase?.CaseId ?? triageCase?.CaseId;
                var caseReference = linkedCase?.Reference ?? allocationCase?.CaseReference ?? triageCase?.Reference;
                // Whether that Case is a Triage is its own type, in the same
                // order: a message staff linked to a Triage Case is on a
                // Triage as surely as one that opened it. An allocation
                // records the type of the Case it created.
                var isTriageCase = linkedCase?.IsTriage
                    ?? (allocationCase?.CaseId is null
                        ? triageCase?.IsTriage
                        : allocationCase.AttemptedCaseType == CaseType.Triage)
                    ?? false;
                var classification = receipt?.Classification is null
                    ? null
                    : EfIntakeReceiptStore.MapMailClassificationDecision(receipt.Classification);
                var caseFact = caseId is { } resolvedCaseId
                    ? casesById.GetValueOrDefault(resolvedCaseId)
                    : null;
                var query = receipt is null ? null : queryByReceipt.GetValueOrDefault(receipt.Id);
                var messageQuery = query is null || query.CaseId != caseId
                    ? null
                    : new CaseQuerySummary(query.Id, query.IsOpen, query.AssignedTo);
                // MAIL-009: the route decision is authoritative but is written
                // by intake processing, a later hop. Until it exists the same
                // unwrap is applied to what retention already holds, so a
                // staff forward is never rendered as the forwarding desk.
                var effectiveSenderAddress = receipt?.EffectiveSenderAddress
                    ?? PrincipalMailRoutePolicy.ProvisionalEffectiveSender(
                        row.SenderAddress,
                        row.BodyHead);
                var isStaffForward = effectiveSenderAddress is { } effectiveSender
                    && !string.Equals(effectiveSender, row.SenderAddress, StringComparison.OrdinalIgnoreCase);
                // The preview line is the message as its sender wrote it: the
                // receipt's cleaned body with the forwarded header skipped.
                // Only when no receipt resolved does the stored raw excerpt
                // stand in, cleaned of the forwarder wrapper.
                var cleanedExcerpt = receipt?.BodyHead is { } bodyHead
                    ? Excerpt(StaffForwardBodyCleaner.TrimPrincipalFooter(
                        StaffForwardBodyCleaner.SplitForwardedHeader(bodyHead).Body))
                    : row.BodyExcerpt is { } excerpt
                        ? Excerpt(StaffForwardBodyCleaner.TrimPrincipalFooter(
                            StaffForwardBodyCleaner.SplitForwardedHeader(
                                StaffForwardBodyCleaner.Clean(excerpt, isStaffForward)).Body))
                        : null;
                if (string.IsNullOrWhiteSpace(cleanedExcerpt))
                {
                    cleanedExcerpt = null;
                }

                return new RetainedMailSummary(
                    row.Id,
                    row.MailboxId,
                    row.MailboxAddress,
                    row.SenderAddress,
                    row.SenderDisplayName,
                    effectiveSenderAddress,
                    row.Subject,
                    cleanedExcerpt,
                    row.ReceivedAtUtc,
                    row.IsRead,
                    row.AttachmentCount,
                    receipt is null
                        ? null
                        : EfIntakeReceiptStore.ParseDecision(receipt.Decision),
                    receipt?.Id,
                    caseId,
                    caseReference,
                    allocationState,
                    row.SearchMatches,
                    classification)
                {
                    DismissedAtUtc = row.DismissedAtUtc,
                    IsTriageCase = isTriageCase,
                    ClassificationVersion = receipt?.Classification?.Version,
                    PrincipalCode = receipt?.RoutePrincipalCode ?? caseFact?.PrincipalCode,
                    VehicleSummary = string.IsNullOrWhiteSpace(caseFact?.Registration)
                        ? receipt?.DraftRegistration
                        : caseFact.Registration,
                    CaseType = caseFact is null ? null : CaseTypeCodes.Parse(caseFact.Type),
                    Query = messageQuery
                };
            })
            .ToArray();
    }

    /// <summary>
    /// The one filter pipeline for a workspace scope: folder, dismissal,
    /// mailbox, unread, search, Principal and the Inbox membership. The list
    /// pages it and the scope counts it; neither owns a second copy.
    /// </summary>
    private static IQueryable<RetainedMailboxMessageEntity> BuildMatches(
        PegasusDbContext context,
        MailWorkspaceScope scope)
    {
        var searchTerm = scope.SearchTerm?.Trim();
        var matches = context.RetainedMailboxMessages
            .AsNoTracking()
            .Where(item => item.FolderScope == ToCode(scope.Folder));
        // A dismissed message sits in the Dismissed scope and nowhere else.
        matches = scope.DismissedOnly
            ? matches.Where(item => item.DismissedAtUtc != null)
            : matches.Where(item => item.DismissedAtUtc == null);
        if (scope.MailboxId is { } mailboxId)
        {
            matches = matches.Where(item => item.MailboxId == mailboxId);
        }
        if (scope.UnreadOnly)
        {
            matches = matches.Where(item => !item.IsRead);
        }
        if (searchTerm is not null && scope.Folder == MailFolderScope.Sent)
        {
            // A Sent item has no intake receipt to search, so its own retained
            // subject, sender and text are what the search reads.
            matches = matches.Where(item =>
                item.Attachments.Any(attachment => attachment.FileName.Contains(searchTerm))
                || (item.Subject != null && item.Subject.Contains(searchTerm))
                || (item.SenderAddress != null && item.SenderAddress.Contains(searchTerm))
                || (item.BodyPlainText != null && item.BodyPlainText.Contains(searchTerm)));
        }
        else if (searchTerm is not null)
        {
            matches = matches.Where(item =>
                item.Attachments.Any(attachment => attachment.FileName.Contains(searchTerm))
                || context.IntakeReceipts.Any(receipt =>
                    receipt.SourceChannel == "mailbox"
                    && receipt.ExternalReceiptToken == item.ExternalReceiptToken
                    && receipt.SearchDocuments.Any(document =>
                        document.Text != null
                        && document.Text.Contains(searchTerm))));
        }
        return ApplyWorkspaceFilter(matches, context, scope);
    }

    private static IQueryable<RetainedMailboxMessageEntity> ApplyWorkspaceFilter(
        IQueryable<RetainedMailboxMessageEntity> messages,
        PegasusDbContext context,
        MailWorkspaceScope scope)
    {
        if (scope.PrincipalCode is { } principalCode)
        {
            messages = messages.Where(message => context.IntakeReceipts.Any(receipt =>
                receipt.SourceChannel == "mailbox"
                && receipt.ExternalReceiptToken == message.ExternalReceiptToken
                && ((receipt.MailRouteDecision != null && receipt.MailRouteDecision.PrincipalCode == principalCode)
                    || context.Set<CaseEntity>().Any(caseRow =>
                        caseRow.Principal.Code == principalCode
                        && (context.IntakeManualAssociations.Any(association =>
                                association.IntakeReceiptId == receipt.Id
                                && association.IsActive
                                && association.CaseId == caseRow.Id)
                            || (!context.IntakeManualAssociations.Any(association =>
                                    association.IntakeReceiptId == receipt.Id)
                                && context.CaseIntakeLinks.Any(link =>
                                    link.IntakeReceiptId == receipt.Id && link.CaseId == caseRow.Id))
                            || (!context.IntakeManualAssociations.Any(association =>
                                    association.IntakeReceiptId == receipt.Id && !association.IsActive)
                                && context.IntakeAllocationAttempts
                                    .Where(attempt => attempt.IntakeReceiptId == receipt.Id)
                                    .OrderByDescending(attempt => attempt.AttemptNumber)
                                    .Select(attempt => attempt.CaseId)
                                    .FirstOrDefault() == caseRow.Id)
                            || context.Triage.Any(triage =>
                                triage.OriginReceiptId == receipt.Id && triage.CaseId == caseRow.Id))))));
        }

        if (scope.NoCaseFound)
        {
            // No Case Found: received mail whose receipt stands linked to no Case.
            // The same order the summaries read the Case in (MapSummariesAsync):
            // an active association; else an accepted link when no association
            // row exists; else the Case the latest allocation created, unless an
            // association was reversed; else the Triage the receipt opened.
            return messages.Where(message => context.IntakeReceipts.Any(receipt =>
                receipt.SourceChannel == "mailbox"
                && receipt.ExternalReceiptToken == message.ExternalReceiptToken
                && !context.IntakeManualAssociations.Any(association =>
                    association.IntakeReceiptId == receipt.Id && association.IsActive)
                && (context.IntakeManualAssociations.Any(association => association.IntakeReceiptId == receipt.Id)
                    || !context.CaseIntakeLinks.Any(link => link.IntakeReceiptId == receipt.Id))
                && (context.IntakeManualAssociations.Any(association =>
                        association.IntakeReceiptId == receipt.Id && !association.IsActive)
                    || context.IntakeAllocationAttempts
                        .Where(attempt => attempt.IntakeReceiptId == receipt.Id)
                        .OrderByDescending(attempt => attempt.AttemptNumber)
                        .Select(attempt => attempt.CaseId)
                        .FirstOrDefault() == null)
                && !context.Triage.Any(triage => triage.OriginReceiptId == receipt.Id)));
        }

        // All, on Inbox only: linked mail, a New instruction, a Triage request,
        // or a receipt nothing has classified yet. Sent and Upload are not the
        // Inbox ledger. Dismissed is its own scope.
        if (scope.Folder != MailFolderScope.Inbox || scope.DismissedOnly)
        {
            return messages;
        }

        var newInstruction = MailCategories.Code(MailCategory.NewInstruction);
        var triageRequest = MailCategories.Code(MailCategory.TriageRequest);
        const string classified = "classified";
        return messages.Where(message =>
            !context.IntakeReceipts.Any(receipt =>
                receipt.SourceChannel == "mailbox"
                && receipt.ExternalReceiptToken == message.ExternalReceiptToken
                && receipt.MailClassificationDecision != null)
            || context.IntakeReceipts.Any(receipt =>
                receipt.SourceChannel == "mailbox"
                && receipt.ExternalReceiptToken == message.ExternalReceiptToken
                && ((receipt.MailClassificationDecision != null
                        && receipt.MailClassificationDecision.Outcome == classified
                        && (receipt.MailClassificationDecision.Category == newInstruction
                            || receipt.MailClassificationDecision.Category == triageRequest))
                    || context.IntakeManualAssociations.Any(association =>
                        association.IntakeReceiptId == receipt.Id && association.IsActive)
                    || (!context.IntakeManualAssociations.Any(association =>
                            association.IntakeReceiptId == receipt.Id)
                        && context.CaseIntakeLinks.Any(link => link.IntakeReceiptId == receipt.Id))
                    || (!context.IntakeManualAssociations.Any(association =>
                            association.IntakeReceiptId == receipt.Id && !association.IsActive)
                        && context.IntakeAllocationAttempts
                            .Where(attempt => attempt.IntakeReceiptId == receipt.Id)
                            .OrderByDescending(attempt => attempt.AttemptNumber)
                            .Select(attempt => attempt.CaseId)
                            .FirstOrDefault() != null)
                    || context.Triage.Any(triage => triage.OriginReceiptId == receipt.Id))));
    }

    /// <summary>
    /// The excerpt as the sender wrote it, line breaks kept: runs of spaces
    /// collapse, blank lines fall away, and it stops after
    /// <see cref="ExcerptLines"/> lines or at a word boundary before
    /// <paramref name="maxLength"/>, whichever comes first, with an ellipsis
    /// where it stopped. Retention stores it at the column's length; the read
    /// paths excerpt the receipt's cleaned body head at the preview's.
    /// </summary>
    internal static string? Excerpt(string? bodyPlainText, int maxLength = ExcerptLength)
    {
        if (string.IsNullOrWhiteSpace(bodyPlainText))
        {
            return null;
        }

        var lines = new List<string>(ExcerptLines);
        var length = 0;
        var moreLines = false;
        var remaining = bodyPlainText.AsSpan();
        while (!remaining.IsEmpty)
        {
            var newline = remaining.IndexOf('\n');
            var line = CollapseSpaces(newline < 0 ? remaining : remaining[..newline], maxLength);
            remaining = newline < 0 ? ReadOnlySpan<char>.Empty : remaining[(newline + 1)..];
            if (line.Length == 0)
            {
                continue;
            }
            if (lines.Count == ExcerptLines || length > maxLength)
            {
                moreLines = true;
                break;
            }

            lines.Add(line);
            length += line.Length + 1;
        }

        var text = string.Join('\n', lines);
        if (text.Length == 0)
        {
            return null;
        }
        if (!moreLines && text.Length <= maxLength)
        {
            return text;
        }
        // The ellipsis counts against the limit: the stored excerpt fills a
        // column of exactly that length, and one character over fails the
        // insert.
        if (text.Length < maxLength)
        {
            return text + "…";
        }

        var cut = text.LastIndexOfAny(LineOrSpace, maxLength - 1);
        return (cut > 0 ? text[..cut] : text[..(maxLength - 1)]) + "…";
    }

    /// <summary>
    /// One line with its runs of whitespace collapsed and its ends trimmed.
    /// It stops once it passes <paramref name="limit"/>: the excerpt cuts
    /// there anyway, and a body can be one very long line.
    /// </summary>
    private static string CollapseSpaces(ReadOnlySpan<char> line, int limit)
    {
        var collapsed = new StringBuilder(Math.Min(line.Length, limit + 1));
        var pendingSpace = false;
        foreach (var character in line)
        {
            if (collapsed.Length > limit)
            {
                break;
            }
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = collapsed.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                collapsed.Append(' ');
                pendingSpace = false;
            }

            collapsed.Append(character);
        }

        return collapsed.ToString();
    }

    private static IReadOnlyList<string> Deserialize(string json) =>
        JsonSerializer.Deserialize<IReadOnlyList<string>>(json, JsonOptions) ?? [];

    internal static string ToCode(MailFolderScope value) => value switch
    {
        MailFolderScope.Inbox => "inbox",
        MailFolderScope.Sent => "sent",
        MailFolderScope.Upload => "upload",
        _ => throw new InvalidOperationException($"Unknown mail folder scope '{(int)value}'.")
    };

    private static MailFolderScope ParseFolderScope(string value) => value switch
    {
        "inbox" => MailFolderScope.Inbox,
        "sent" => MailFolderScope.Sent,
        "upload" => MailFolderScope.Upload,
        _ => throw new InvalidDataException($"Unknown persisted mail folder scope '{value}'.")
    };

    private static MailClassificationOutcome ParseClassificationOutcome(string value) => value switch
    {
        "classified" => MailClassificationOutcome.Classified,
        "unclassified" => MailClassificationOutcome.Unclassified,
        _ => throw new InvalidDataException($"Unknown persisted mail-classification outcome '{value}'.")
    };

    private static MailRouteDisposition ParseRouteDisposition(string value) => value switch
    {
        "accepted" => MailRouteDisposition.Accepted,
        "no_match" => MailRouteDisposition.NoMatch,
        "needs_sorting" => MailRouteDisposition.NeedsSorting,
        _ => throw new InvalidDataException($"Unknown persisted mail-route disposition '{value}'.")
    };

    private sealed record ReceiptFacts(
        Guid Id,
        string ExternalReceiptToken,
        string Decision,
        IntakeMailClassificationDecisionEntity? Classification,
        string? EffectiveSenderAddress,
        string? BodyHead,
        string? RoutePrincipalCode = null,
        string? DraftRegistration = null);

    /// <summary>
    /// The one list-row projection, shared by the page and cursor lists so the
    /// two cannot read different columns.
    /// </summary>
    private static Expression<Func<RetainedMailboxMessageEntity, SummaryRow>> SummaryProjection(
        PegasusDbContext context,
        string? searchTerm) =>
        item => new SummaryRow(
            item.Id,
            item.MailboxId ?? UploadedCorrespondence.MailboxId,
            item.MailboxAddress,
            item.SenderAddress,
            item.SenderDisplayName,
            item.Subject,
            item.BodyExcerpt,
            item.ReceivedAtUtc,
            item.IsRead,
            item.Attachments.Count,
            item.ExternalReceiptToken,
            searchTerm != null
                && context.IntakeReceipts.Any(receipt =>
                    receipt.SourceChannel == "mailbox"
                    && receipt.ExternalReceiptToken == item.ExternalReceiptToken
                    && receipt.SearchDocuments.Any(document =>
                        document.AttachmentFileName == null
                        && document.Text != null
                        && document.Text.Contains(searchTerm))),
            item.BodyPlainText == null
                ? null
                : item.BodyPlainText.Substring(0, 600),
            null,
            item.DismissedAtUtc);

    private sealed record SummaryRow(
        Guid Id,
        Guid MailboxId,
        string MailboxAddress,
        string? SenderAddress,
        string? SenderDisplayName,
        string? Subject,
        string? BodyExcerpt,
        DateTimeOffset ReceivedAtUtc,
        bool IsRead,
        int AttachmentCount,
        string ExternalReceiptToken,
        bool BodyMatched,
        // Enough retained body, blank lines intact, to read the forwarded
        // header block from. BodyExcerpt drops blank lines, so it cannot
        // answer this question (MAIL-009).
        string? BodyHead = null,
        IReadOnlyList<RetainedMailSearchMatch>? SearchMatches = null,
        DateTimeOffset? DismissedAtUtc = null);

    private static async Task<List<SummaryRow>> AddSearchMatchesAsync(
        PegasusDbContext context,
        IReadOnlyList<SummaryRow> rows,
        string searchTerm,
        CancellationToken cancellationToken)
    {
        var messageIds = rows.Select(item => item.Id).ToArray();
        var tokens = rows.Select(item => item.ExternalReceiptToken).ToArray();
        var fileNameMatches = await context.RetainedMailboxAttachments
            .AsNoTracking()
            .Where(item => messageIds.Contains(item.RetainedMailboxMessageId)
                && item.FileName.Contains(searchTerm))
            .Select(item => new { item.RetainedMailboxMessageId, item.FileName, item.Ordinal })
            .ToListAsync(cancellationToken);
        var contentMatches = await context.IntakeReceipts
            .AsNoTracking()
            .Where(item => item.SourceChannel == "mailbox"
                && tokens.Contains(item.ExternalReceiptToken))
            .SelectMany(
                receipt => receipt.SearchDocuments
                    .Where(document => document.AttachmentFileName != null
                        && document.Text != null
                        && document.Text.Contains(searchTerm)),
                (receipt, document) => new
                {
                    receipt.ExternalReceiptToken,
                    document.AttachmentFileName,
                    document.AttachmentOrdinal
                })
            .ToListAsync(cancellationToken);

        return rows.Select(row =>
        {
            var found = new List<RetainedMailSearchMatch>();
            if (row.BodyMatched)
            {
                found.Add(new(MailSearchMatchKind.MessageBody));
            }
            found.AddRange(fileNameMatches
                .Where(item => item.RetainedMailboxMessageId == row.Id)
                .Select(item => new RetainedMailSearchMatch(
                    MailSearchMatchKind.AttachmentFileName,
                    item.FileName,
                    item.Ordinal)));
            found.AddRange(contentMatches
                .Where(item => item.ExternalReceiptToken == row.ExternalReceiptToken)
                .Select(item => new RetainedMailSearchMatch(
                    MailSearchMatchKind.AttachmentContent,
                    item.AttachmentFileName,
                    item.AttachmentOrdinal)));
            return row with { SearchMatches = found.Distinct().ToArray() };
        }).ToList();
    }
}
