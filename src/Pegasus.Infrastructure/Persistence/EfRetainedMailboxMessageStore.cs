using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Lifecycle;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The retained-mail read model: written by the poll, read by the workspace.
/// </summary>
internal sealed class EfRetainedMailboxMessageStore(
    IDbContextFactory<PegasusDbContext> contextFactory)
    : IRetainedMailboxMessageStore, IRetainedMailQueries, IRetainedMailClassificationStore
{
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

    public async Task<int> CountAsync(
        MailWorkspaceScope scope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        // The same filter pipeline the list reads, so a scope's count is
        // exactly the number of rows clicking that scope would page through.
        return await BuildMatches(context, scope).CountAsync(cancellationToken);
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
            .Select(item => new SummaryRow(
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
                context.RetainedMailFolderMoves
                    .Where(move => move.RetainedMailboxMessageId == item.Id && move.Outcome == "succeeded")
                    .OrderByDescending(move => move.RecordedAtUtc)
                    .ThenByDescending(move => move.Id)
                    .Select(move => move.FolderType)
                    .FirstOrDefault(),
                item.BodyPlainText == null
                    ? null
                    : item.BodyPlainText.Substring(0, 600),
                null,
                item.DismissedAtUtc))
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
            await HasUnretainedHistoryAsync(context, scope, cancellationToken));
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
            .Select(item => new SummaryRow(
                item.Id, item.MailboxId ?? UploadedCorrespondence.MailboxId, item.MailboxAddress, item.SenderAddress,
                item.SenderDisplayName, item.Subject, item.BodyExcerpt, item.ReceivedAtUtc,
                item.IsRead, item.Attachments.Count, item.ExternalReceiptToken,
                searchTerm != null && context.IntakeReceipts.Any(receipt =>
                    receipt.SourceChannel == "mailbox"
                    && receipt.ExternalReceiptToken == item.ExternalReceiptToken
                    && receipt.SearchDocuments.Any(document =>
                        document.AttachmentFileName == null && document.Text != null
                        && document.Text.Contains(searchTerm))),
                context.RetainedMailFolderMoves
                    .Where(move => move.RetainedMailboxMessageId == item.Id && move.Outcome == "succeeded")
                    .OrderByDescending(move => move.RecordedAtUtc).ThenByDescending(move => move.Id)
                    .Select(move => move.FolderType).FirstOrDefault(),
                item.BodyPlainText == null ? null : item.BodyPlainText.Substring(0, 600),
                null,
                item.DismissedAtUtc))
            .ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        if (searchTerm is not null && rows.Count > 0)
        {
            rows = await AddSearchMatchesAsync(context, rows, searchTerm, cancellationToken);
        }
        return new(
            await MapSummariesAsync(context, rows, cancellationToken),
            hasMore,
            await HasUnretainedHistoryAsync(context, scope, cancellationToken));
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
                Decision = item.MailClassificationDecision,
                Route = item.MailRouteDecision!.Disposition,
                EffectiveSenderAddress = item.MailRouteDecision!.EffectiveSenderAddress,
                BodySearchText = item.SearchDocuments
                    .Where(document => document.AttachmentFileName == null)
                    .Select(document => document.Text)
                    .SingleOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);
        var currentFolderType = await ReadCurrentFolderTypeAsync(context, entity.Id, cancellationToken);

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
                currentFolderType,
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
        var summary = (await MapSummariesAsync(context, summaryRows, cancellationToken))[0];

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
                RetainedMailDirection.Of(ParseFolderScope(entity.FolderScope)),
                cancellationToken));
    }

    /// <summary>
    /// The Inbox preview pane's read. A summary the caller already holds from the
    /// list is kept as it is, search matches included; without one the message's
    /// own summary is read. Neither reads the thread, the staff names, the folder
    /// recommendation or the latest move.
    /// </summary>
    public async Task<RetainedMailPreview?> GetPreviewAsync(
        Guid id,
        RetainedMailSummary? summary,
        CancellationToken cancellationToken)
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

        if (summary is null)
        {
            var currentFolderType = await ReadCurrentFolderTypeAsync(context, entity.Id, cancellationToken);
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
                        currentFolderType,
                        entity.BodyPlainText,
                        null,
                        entity.DismissedAtUtc)
                ],
                cancellationToken))[0];
        }

        var decision = await context.IntakeReceipts
            .AsNoTracking()
            .Where(item => item.SourceChannel == "mailbox"
                && item.ExternalReceiptToken == entity.ExternalReceiptToken)
            .Select(item => item.MailClassificationDecision)
            .SingleOrDefaultAsync(cancellationToken);
        return new(
            summary,
            entity.Attachments
                .OrderBy(item => item.Ordinal)
                .Select(item => new RetainedMailAttachment(
                    item.FileName, item.MediaType, item.ContentLength, false, null))
                .ToArray(),
            decision is null ? null : EfIntakeReceiptStore.MapMailClassificationDecision(decision),
            ParseFolderScope(entity.FolderScope));
    }

    private static Task<string?> ReadCurrentFolderTypeAsync(
        PegasusDbContext context,
        Guid retainedMessageId,
        CancellationToken cancellationToken) =>
        context.RetainedMailFolderMoves.AsNoTracking()
            .Where(move => move.RetainedMailboxMessageId == retainedMessageId && move.Outcome == "succeeded")
            .OrderByDescending(move => move.RecordedAtUtc)
            .ThenByDescending(move => move.Id)
            .Select(move => move.FolderType)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<RetainedMailDetail?> GetByOriginReceiptAsync(
        Guid originReceiptId,
        CancellationToken cancellationToken) =>
        await FindIdByOriginReceiptAsync(originReceiptId, cancellationToken) is { } retainedId
            ? await GetAsync(retainedId, cancellationToken)
            : null;

    public async Task<Guid?> FindIdByOriginReceiptAsync(
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
        if (retainedIds.Count > 1)
        {
            throw new InvalidOperationException(
                "The origin receipt matches more than one retained mailbox message.");
        }

        return retainedIds.Count == 0 ? null : retainedIds[0];
    }

    public async Task<IReadOnlyList<RetainedMailMailbox>> ListMailboxesAsync(
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
            .Where(item => item.State == approvedState && item.AllowInboundIntake)
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
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
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

        // The Case this message is currently linked to, by staff or by its
        // accepted origin. A Triage Case has no Completed or Query state.
        var associations = await CurrentIntakeAssociations.ReadAsync(context, [receipt.Id], cancellationToken);
        var linkedCase = associations.Current.TryGetValue(receipt.Id, out var association) && !association.IsTriage
            ? association
            : null;
        CaseWorkflowEntity? workflow = null;
        if (linkedCase is not null)
        {
            await EfIntakeMutationStore.AcquireCaseQueryLockAsync(
                context, transaction, linkedCase.CaseId, receipt.Id, cancellationToken);
            workflow = await context.CaseWorkflows
                .SingleOrDefaultAsync(item => item.CaseId == linkedCase.CaseId, cancellationToken);
        }
        var wasPostReport = PostReportQueryTransitions.IsPostReport(receipt);

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

        // FRD-13 "Completed and Query": a message corrected to Post-report joins
        // the Case as a query would; one corrected away leaves it as an unlink
        // would. The same rule and the same lock as the link paths.
        var queryEntry = PostReportQueryEntry.None;
        var queryWithdrawn = false;
        if (workflow is not null)
        {
            var isPostReport = PostReportQueryTransitions.IsPostReport(receipt);
            var beforeCaseVersion = workflow.Version;
            var operationKey = $"mail-correction:{receipt.Id:N}:v{decision.Version}";
            var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(afterJson)));
            if (isPostReport && !wasPostReport)
            {
                var beforeStateJson = PostReportQueryTransitions.SnapshotState(workflow);
                var entry = await PostReportQueryTransitions.EnterAsync(
                    context, receipt, workflow, correctedAtUtc, cancellationToken);
                queryEntry = entry.Kind;
                if (entry.Kind != PostReportQueryEntry.None)
                {
                    CaseMutationGuard.Complete(workflow);
                    if (entry.Kind == PostReportQueryEntry.EnterQuery)
                    {
                        PostReportQueryTransitions.AddQueryHistory(
                            context, workflow, beforeCaseVersion, PostReportQueryTransitions.QueryReceivedEvent,
                            actor, operationKey, requestHash, reason, correctedAtUtc, beforeStateJson);
                    }
                    else
                    {
                        PostReportQueryTransitions.AddObservedReplyHistory(
                            context, workflow, beforeCaseVersion, entry.ObservedReply!, entry.BeforeReplyJson!);
                    }
                }
            }
            else if (wasPostReport && !isPostReport)
            {
                var beforeStateJson = await PostReportQueryTransitions.WithdrawAsync(
                    context, receipt, workflow, correctedAtUtc, cancellationToken);
                if (beforeStateJson is not null)
                {
                    queryWithdrawn = true;
                    CaseMutationGuard.Complete(workflow);
                    PostReportQueryTransitions.AddQueryHistory(
                        context, workflow, beforeCaseVersion, PostReportQueryTransitions.QueryWithdrawnEvent,
                        actor, operationKey, requestHash, reason, correctedAtUtc, beforeStateJson);
                }
            }
        }

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
        return new(dossier, linkedCase?.CaseId, queryEntry, queryWithdrawn);
    }

    private static async Task<MailClassificationDossier?> LoadClassificationAsync(
        PegasusDbContext context,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var message = await context.RetainedMailboxMessages
            .AsNoTracking()
            .Where(item => item.Id == messageId)
            .Select(item => new { item.ExternalReceiptToken, item.FolderScope })
            .SingleOrDefaultAsync(cancellationToken);
        return message is null
            ? null
            : await LoadClassificationByTokenAsync(
                context,
                message.ExternalReceiptToken,
                RetainedMailDirection.Of(ParseFolderScope(message.FolderScope)),
                cancellationToken);
    }

    /// <summary>The classification dossier of the message whose receipt token is known.</summary>
    private static async Task<MailClassificationDossier?> LoadClassificationByTokenAsync(
        PegasusDbContext context,
        string externalReceiptToken,
        MailDirection direction,
        CancellationToken cancellationToken)
    {
        var decision = await context.IntakeReceipts
            .AsNoTracking()
            .Where(item => item.SourceChannel == "mailbox"
                && item.ExternalReceiptToken == externalReceiptToken)
            .Select(item => item.MailClassificationDecision)
            .SingleOrDefaultAsync(cancellationToken);
        return await ClassificationDossierAsync(context, decision, direction, cancellationToken);
    }

    /// <summary>
    /// The classification dossier of a decision already read: only its
    /// correction history is read here. No decision, no dossier.
    /// </summary>
    private static async Task<MailClassificationDossier?> ClassificationDossierAsync(
        PegasusDbContext context,
        IntakeMailClassificationDecisionEntity? decision,
        MailDirection direction,
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
            history)
        {
            MessageDirection = direction
        };
    }

    private static void Apply(
        MailClassificationResult source,
        IntakeMailClassificationDecisionEntity target)
    {
        target.Outcome = source.Outcome switch
        {
            MailClassificationOutcome.Classified => "classified",
            MailClassificationOutcome.Ambiguous => "ambiguous",
            MailClassificationOutcome.Unclassified => "unclassified",
            _ => throw new InvalidOperationException("Unknown mail classification outcome.")
        };
        target.Direction = source.Category?.Direction.ToString().ToLowerInvariant();
        target.Family = source.Category is { IsOther: false } ? source.Category.Name : null;
        target.Subtype = source.Category?.Subtype;
        target.IsReplyContext = source.Category?.IsReplyContext ?? false;
        target.OtherName = source.Category?.OtherName;
        target.OtherReasoning = source.Category?.OtherReasoning;
        target.CaseType = source.CaseType is { } caseType
            ? CaseTypeCodes.ToCode(caseType)
            : null;
        target.StandaloneAuditReportAssetSourceLabel = source.StandaloneAuditReport?.AssetSourceLabel;
        target.StandaloneAuditReportAssessment = source.StandaloneAuditReport is { } report
            ? EfIntakeReceiptStore.ToCode(report.Assessment)
            : null;
        target.AmbiguousCandidatesJson = EfIntakeReceiptStore.SerializeEnvelope(source.AmbiguousCandidates);
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

    private sealed record ClassificationSnapshot(
        MailClassificationOutcome Outcome,
        MailDirection? Direction,
        string? Family,
        string? Subtype,
        bool IsReplyContext,
        string? OtherName,
        string? OtherReasoning,
        IReadOnlyList<string> AmbiguousCandidates,
        IReadOnlyList<MailClassificationPredicateResult> Predicates,
        string Reason,
        string PolicyKey,
        int PolicyVersion,
        CaseType? CaseType,
        StandaloneAuditReportEvaluation? StandaloneAuditReport)
    {
        public static ClassificationSnapshot From(MailClassificationResult value) => new(
            value.Outcome,
            value.Category?.Direction,
            value.Category is { IsOther: false } ? value.Category.Name : null,
            value.Category?.Subtype,
            value.Category?.IsReplyContext ?? false,
            value.Category?.OtherName,
            value.Category?.OtherReasoning,
            value.AmbiguousCandidates,
            value.Predicates,
            value.Reason,
            value.PolicyKey,
            value.PolicyVersion,
            value.CaseType,
            value.StandaloneAuditReport);

        public MailClassificationResult ToResult()
        {
            MailCategory? category = OtherName is not null
                ? MailCategory.Other(Direction!.Value, OtherName, OtherReasoning!)
                : Family is null
                    ? null
                    : Direction == MailDirection.Received
                        ? MailCategory.Received(MailTaxonomy.ParseReceivedFamily(Family), Subtype, IsReplyContext)
                        : MailCategory.Sent(MailTaxonomy.ParseSentFamily(Family), IsReplyContext);
            return new(
                Outcome,
                category,
                AmbiguousCandidates,
                Predicates,
                Reason,
                PolicyKey,
                PolicyVersion,
                CaseType,
                StandaloneAuditReport);
        }
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
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        // Six lookups for the whole page, never one per row.
        var tokens = rows.Select(item => item.ExternalReceiptToken).Distinct().ToArray();
        var receipts = await context.IntakeReceipts
            .AsNoTracking()
            .Where(item => item.SourceChannel == "mailbox"
                && tokens.Contains(item.ExternalReceiptToken))
            .Select(item => new
            {
                item.Id,
                item.ExternalReceiptToken,
                item.Decision,
                Classification = item.MailClassificationDecision,
                EffectiveSenderAddress = item.MailRouteDecision == null
                    ? null
                    : item.MailRouteDecision.EffectiveSenderAddress,
                // Enough cleaned body to excerpt from once the forwarded
                // header block is skipped; never the whole document text.
                BodyHead = item.SearchDocuments
                    .Where(document => document.AttachmentFileName == null && document.Text != null)
                    .Select(document => document.Text!.Substring(0, BodyHeadLength))
                    .FirstOrDefault()
            })
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
        var resolved = UnidentifiedState.Resolved.ToString();
        var receiptOrigin = UnidentifiedOriginKind.Receipt.ToString();
        var resolvedUnidentifiedReceiptIds = receiptIds.Length == 0
            ? new HashSet<Guid>()
            : (await context.Set<UnidentifiedItemEntity>()
                .AsNoTracking()
                .Where(item => item.OriginKind == receiptOrigin
                    && receiptIds.Contains(item.OriginId)
                    && item.State == resolved)
                .Select(item => item.OriginId)
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var addresses = rows.Select(item => item.MailboxAddress).Distinct().ToArray();
        var approvedState = ApprovedMailboxState.Approved.ToString();
        var polled = await context.ApprovedMailboxes
            .AsNoTracking()
            .Where(item => item.State == approvedState
                && item.AllowInboundIntake
                && addresses.Contains(item.Address))
            .Select(item => item.Address)
            .ToListAsync(cancellationToken);
        var polledAddresses = polled.ToHashSet(StringComparer.OrdinalIgnoreCase);

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
                    polledAddresses.Contains(row.MailboxAddress),
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
                    row.CurrentFolderType is null
                        ? null
                        : Enum.Parse<MailLogicalFolderType>(row.CurrentFolderType),
                    classification,
                    classification is null
                        ? null
                        : MailOperationalDestinationPolicy.Map(classification))
                {
                    DismissedAtUtc = row.DismissedAtUtc,
                    UnidentifiedResolved = receipt is not null
                        && resolvedUnidentifiedReceiptIds.Contains(receipt.Id),
                    IsTriageCase = isTriageCase
                };
            })
            .ToArray();
    }

    /// <summary>
    /// The one filter pipeline for a workspace scope: folder, the not-moved
    /// Inbox exclusion, mailbox, unread, search and classification. The list
    /// pages it and the scope-rail counts it; neither owns a second copy.
    /// </summary>
    private static IQueryable<RetainedMailboxMessageEntity> BuildMatches(
        PegasusDbContext context,
        MailWorkspaceScope scope)
    {
        var searchTerm = scope.SearchTerm?.Trim();
        var matches = context.RetainedMailboxMessages
            .AsNoTracking()
            .Where(item => item.FolderScope == ToCode(scope.Folder));
        if (scope.Folder == MailFolderScope.Inbox && searchTerm is null)
        {
            matches = matches.Where(item => !context.RetainedMailFolderMoves.Any(move =>
                move.RetainedMailboxMessageId == item.Id && move.Outcome == "succeeded"));
        }
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
        return ApplyClassificationFilter(matches, context, scope);
    }

    private static IQueryable<RetainedMailboxMessageEntity> ApplyClassificationFilter(
        IQueryable<RetainedMailboxMessageEntity> messages,
        PegasusDbContext context,
        MailWorkspaceScope scope)
    {
        if (scope.Destination is null && scope.DetailedClassification is null)
        {
            return messages;
        }

        var query = scope.Destination is { } destination
            ? MailOperationalDestinationPolicy.Query(destination)
            : new MailOperationalDestinationQuery(
                ExactClassification: scope.DetailedClassification);
        var familyNames = query.Families
            .Select(MailTaxonomy.CategoryName)
            .ToArray();
        var exact = query.ExactClassification;
        var exactDirection = exact?.Direction.ToString().ToLowerInvariant();
        var exactFamily = exact?.Name;
        var exactSubtype = exact?.Subtype;
        const string classified = "classified";
        var resolved = UnidentifiedState.Resolved.ToString();
        var receiptOrigin = UnidentifiedOriginKind.Receipt.ToString();

        return messages.Where(message => context.IntakeReceipts.Any(receipt =>
            receipt.SourceChannel == "mailbox"
            && receipt.ExternalReceiptToken == message.ExternalReceiptToken
            && receipt.MailClassificationDecision != null
            && (query.IncludesUnidentified
                ? receipt.MailClassificationDecision.Outcome != classified
                    && !context.Set<UnidentifiedItemEntity>().Any(item =>
                        item.OriginKind == receiptOrigin
                        && item.OriginId == receipt.Id
                        && item.State == resolved)
                : receipt.MailClassificationDecision.Outcome == classified
                    && ((query.IncludesOther
                            && receipt.MailClassificationDecision.OtherName != null)
                        || (receipt.MailClassificationDecision.Direction == "received"
                            && receipt.MailClassificationDecision.Family != null
                            && familyNames.Contains(receipt.MailClassificationDecision.Family))
                        || (exact != null
                            && receipt.MailClassificationDecision.OtherName == null
                            && receipt.MailClassificationDecision.Direction == exactDirection
                            && receipt.MailClassificationDecision.Family == exactFamily
                            && receipt.MailClassificationDecision.Subtype == exactSubtype)))));
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
        MailFolderScope.DeletedItems => "deleted_items",
        MailFolderScope.Upload => "upload",
        _ => throw new InvalidOperationException($"Unknown mail folder scope '{(int)value}'.")
    };

    private static MailFolderScope ParseFolderScope(string value) => value switch
    {
        "inbox" => MailFolderScope.Inbox,
        "sent" => MailFolderScope.Sent,
        "deleted_items" => MailFolderScope.DeletedItems,
        "upload" => MailFolderScope.Upload,
        _ => throw new InvalidDataException($"Unknown persisted mail folder scope '{value}'.")
    };

    private static MailClassificationOutcome ParseClassificationOutcome(string value) => value switch
    {
        "classified" => MailClassificationOutcome.Classified,
        "ambiguous" => MailClassificationOutcome.Ambiguous,
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
        string? CurrentFolderType,
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
