using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The retained-mail read model: written by the poll, read by the workspace.
/// </summary>
internal sealed class EfRetainedMailboxMessageStore(
    IDbContextFactory<PegasusDbContext> contextFactory)
    : IRetainedMailboxMessageStore, IRetainedMailQueries, IRetainedMailClassificationStore
{
    private const int ExcerptLength = 300;
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
            // Inbound polling is the only writer, so every row it makes is an Inbox
            // row. Sent and Deleted Items are declared scopes with no writer yet,
            // and the workspace says so rather than hiding the tab.
            FolderScope = ToCode(MailFolderScope.Inbox),
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
            BodyExcerpt = Excerpt(message.Metadata.BodyPlainText),
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
                item.MailboxId,
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
                    : item.BodyPlainText.Substring(0, 600)))
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

        var receipt = await context.IntakeReceipts
            .AsNoTracking()
            .Where(item => item.SourceChannel == "mailbox"
                && item.ExternalReceiptToken == entity.ExternalReceiptToken)
            .Select(item => new
            {
                Classification = item.MailClassificationDecision!.Outcome,
                Route = item.MailRouteDecision!.Disposition,
                EffectiveSenderAddress = item.MailRouteDecision!.EffectiveSenderAddress,
                BodySearchText = item.SearchDocuments
                    .Where(document => document.AttachmentFileName == null)
                    .Select(document => document.Text)
                    .SingleOrDefault()
            })
            .SingleOrDefaultAsync(cancellationToken);
        var currentFolderType = await context.RetainedMailFolderMoves.AsNoTracking()
            .Where(move => move.RetainedMailboxMessageId == entity.Id && move.Outcome == "succeeded")
            .OrderByDescending(move => move.RecordedAtUtc)
            .ThenByDescending(move => move.Id)
            .Select(move => move.FolderType)
            .FirstOrDefaultAsync(cancellationToken);

        var summaryRows = new List<SummaryRow>
        {
            new(
                entity.Id,
                entity.MailboxId,
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
                entity.BodyPlainText)
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

        var searchableAttachments = await context.IntakeReceipts
            .AsNoTracking()
            .Where(item => item.SourceChannel == "mailbox"
                && item.ExternalReceiptToken == entity.ExternalReceiptToken)
            .SelectMany(item => item.SearchDocuments)
            .Where(item => item.AttachmentFileName != null && item.Text != null)
            .Select(item => item.AttachmentOrdinal)
            .Distinct()
            .ToListAsync(cancellationToken);
        var searchableOrdinals = searchableAttachments.Where(item => item is not null)
            .Select(item => item!.Value)
            .ToHashSet();

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
                    searchableOrdinals.Contains(item.Ordinal)))
                .ToArray(),
            thread,
            ParseFolderScope(entity.FolderScope),
            receipt?.Classification is { } classification
                ? ParseClassificationOutcome(classification)
                : null,
            receipt?.Route is { } route ? ParseRouteDisposition(route) : null,
            entity.ImmutableMessageId,
            entity.InternetMessageIdentity,
            entity.ConversationIdentity,
            await LoadClassificationAsync(context, id, cancellationToken));
    }

    public async Task<IReadOnlyList<RetainedMailMailbox>> ListMailboxesAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var mailboxes = await context.RetainedMailboxMessages
            .AsNoTracking()
            .GroupBy(item => new { item.MailboxId, item.MailboxAddress })
            .Select(group => new
            {
                group.Key.MailboxId,
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

    public async Task<MailClassificationDossier> AppendCorrectionAsync(
        Guid messageId,
        int expectedVersion,
        MailClassificationResult before,
        MailClassificationResult after,
        string actor,
        string reason,
        DateTimeOffset correctedAtUtc,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var retained = await context.RetainedMailboxMessages
            .SingleOrDefaultAsync(item => item.Id == messageId, cancellationToken)
            ?? throw new InvalidOperationException("The retained message no longer exists.");
        var decision = await context.IntakeReceipts
            .Where(item => item.SourceChannel == "mailbox"
                && item.ExternalReceiptToken == retained.ExternalReceiptToken)
            .Select(item => item.MailClassificationDecision)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The retained message has no classification decision.");
        if (decision.Version != expectedVersion
            || !string.Equals(
                SerializeSnapshot(EfIntakeReceiptStore.MapMailClassificationDecision(decision)),
                SerializeSnapshot(before),
                StringComparison.Ordinal))
        {
            throw new MailClassificationConcurrencyException();
        }

        Apply(after, decision);
        decision.Version++;
        decision.DecidedByActor = actor;
        decision.DecidedAtUtc = correctedAtUtc;
        context.IntakeMailClassificationHistory.Add(new()
        {
            Id = Guid.NewGuid(),
            IntakeReceiptId = decision.IntakeReceiptId,
            ClassificationDecision = decision,
            Version = decision.Version,
            BeforeJson = SerializeSnapshot(before),
            AfterJson = SerializeSnapshot(after),
            Actor = actor,
            Reason = reason,
            CorrectedAtUtc = correctedAtUtc
        });
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new MailClassificationConcurrencyException();
        }

        return (await LoadClassificationAsync(context, messageId, cancellationToken))!;
    }

    private static async Task<MailClassificationDossier?> LoadClassificationAsync(
        PegasusDbContext context,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var retained = await context.RetainedMailboxMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == messageId, cancellationToken);
        if (retained is null)
        {
            return null;
        }
        var decision = await context.IntakeReceipts
            .AsNoTracking()
            .Where(item => item.SourceChannel == "mailbox"
                && item.ExternalReceiptToken == retained.ExternalReceiptToken)
            .Select(item => item.MailClassificationDecision)
            .SingleOrDefaultAsync(cancellationToken);
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
            ? EfIntakeReceiptStore.ToCode(caseType)
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
        var canonicalIdentity = CanonicalInternetMessageIdentity(message);
        return await context.RetainedMailboxMessages
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.MailboxId == message.MailboxId
                    && (item.CanonicalInternetMessageIdentity == canonicalIdentity
                        || item.ImmutableMessageId == message.ImmutableMessageId),
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

    private static string CanonicalInternetMessageIdentity(RetainedMailboxMessage message) =>
        MailboxMessageIdentity.CanonicalizeInternetMessageIdentity(
            message.Metadata.InternetMessageIdentity!);

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
        if (scope.Folder != MailFolderScope.Inbox)
        {
            return false;
        }

        var completedPolls = context.ApprovedInboxPollStates
            .AsNoTracking()
            .Where(item => item.LastCompletedAtUtc != null);
        var retained = context.RetainedMailboxMessages.AsNoTracking();
        if (scope.MailboxId is { } mailboxId)
        {
            completedPolls = completedPolls.Where(item => item.ApprovedMailboxId == mailboxId);
            retained = retained.Where(item => item.MailboxId == mailboxId);
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

        // Three lookups for the whole page, never one per row.
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
                    .Select(document => document.Text!.Substring(0, 600))
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
                // INTK-029: once the association has been reversed the
                // allocation's record of the case it created no longer
                // stands in for it, or unlinking would visibly do nothing.
                var allocationCase = receipt is not null
                    && associations.AllocationMayStandIn(receipt.Id)
                        ? allocationState
                        : null;
                var classification = receipt?.Classification is null
                    ? null
                    : EfIntakeReceiptStore.MapMailClassificationDecision(receipt.Classification);
                // MAIL-009: the route decision is authoritative but is written
                // by intake processing, a later hop. Until it exists the same
                // unwrap is applied to what retention already holds, so a
                // staff forward is never rendered as the forwarding desk.
                var effectiveSenderAddress = receipt?.EffectiveSenderAddress
                    ?? QdosMailRoutePolicy.ProvisionalEffectiveSender(
                        row.SenderAddress,
                        row.BodyHead);
                var isStaffForward = effectiveSenderAddress is { } effectiveSender
                    && !string.Equals(effectiveSender, row.SenderAddress, StringComparison.OrdinalIgnoreCase);
                // The preview line is the message as its sender wrote it: the
                // receipt's cleaned body with the forwarded header skipped.
                // Only when no receipt resolved does the stored raw excerpt
                // stand in, cleaned of the forwarder wrapper.
                var cleanedExcerpt = receipt?.BodyHead is { } bodyHead
                    ? Excerpt(StaffForwardBodyCleaner.TrimProviderFooter(
                        StaffForwardBodyCleaner.SplitForwardedHeader(bodyHead).Body))
                    : row.BodyExcerpt is { } excerpt
                        ? Excerpt(StaffForwardBodyCleaner.TrimProviderFooter(
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
                    // The manual acceptance route writes a CaseIntakeLinks row;
                    // the automatic allocation route records its created case on
                    // the succeeded attempt instead. Either one is the case.
                    linkedCase?.CaseId ?? allocationCase?.CaseId,
                    linkedCase?.Reference ?? allocationCase?.CaseReference,
                    allocationState,
                    row.SearchMatches,
                    row.CurrentFolderType is null
                        ? null
                        : Enum.Parse<MailLogicalFolderType>(row.CurrentFolderType),
                    classification,
                    classification is null
                        ? null
                        : MailOperationalDestinationPolicy.Map(classification));
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
        if (scope.MailboxId is { } mailboxId)
        {
            matches = matches.Where(item => item.MailboxId == mailboxId);
        }
        if (scope.UnreadOnly)
        {
            matches = matches.Where(item => !item.IsRead);
        }
        if (searchTerm is not null)
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

        return messages.Where(message => context.IntakeReceipts.Any(receipt =>
            receipt.SourceChannel == "mailbox"
            && receipt.ExternalReceiptToken == message.ExternalReceiptToken
            && receipt.MailClassificationDecision != null
            && (query.IncludesUnidentified
                ? receipt.MailClassificationDecision.Outcome != classified
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
    /// The list excerpt, computed once at retention rather than on every read.
    /// Whitespace is collapsed so a quoted reply does not spend the excerpt on
    /// blank lines, and the cut lands on a word boundary.
    /// </summary>
    internal static string? Excerpt(string? bodyPlainText)
    {
        if (string.IsNullOrWhiteSpace(bodyPlainText))
        {
            return null;
        }

        var collapsed = new StringBuilder(Math.Min(bodyPlainText.Length, ExcerptLength + 64));
        var pendingSpace = false;
        foreach (var character in bodyPlainText)
        {
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
            if (collapsed.Length > ExcerptLength)
            {
                break;
            }
        }

        var text = collapsed.ToString();
        if (text.Length <= ExcerptLength)
        {
            return text.Length == 0 ? null : text;
        }

        var cut = text.LastIndexOf(' ', ExcerptLength - 1);
        return (cut > 0 ? text[..cut] : text[..ExcerptLength]) + "…";
    }

    private static IReadOnlyList<string> Deserialize(string json) =>
        JsonSerializer.Deserialize<IReadOnlyList<string>>(json, JsonOptions) ?? [];

    internal static string ToCode(MailFolderScope value) => value switch
    {
        MailFolderScope.Inbox => "inbox",
        MailFolderScope.Sent => "sent",
        MailFolderScope.DeletedItems => "deleted_items",
        _ => throw new InvalidOperationException($"Unknown mail folder scope '{(int)value}'.")
    };

    private static MailFolderScope ParseFolderScope(string value) => value switch
    {
        "inbox" => MailFolderScope.Inbox,
        "sent" => MailFolderScope.Sent,
        "deleted_items" => MailFolderScope.DeletedItems,
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
        // Enough retained body, newlines intact, to read the forwarded
        // header block from. BodyExcerpt collapses whitespace, so it cannot
        // answer this question (MAIL-009).
        string? BodyHead = null,
        IReadOnlyList<RetainedMailSearchMatch>? SearchMatches = null);

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
