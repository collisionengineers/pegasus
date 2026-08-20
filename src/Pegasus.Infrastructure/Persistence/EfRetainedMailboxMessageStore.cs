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

    public async Task<RetainedMailPage> ListAsync(
        MailWorkspaceScope scope,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var folderScope = ToCode(scope.Folder);
        var matches = context.RetainedMailboxMessages
            .AsNoTracking()
            .Where(item => item.FolderScope == folderScope);
        if (scope.MailboxId is { } mailboxId)
        {
            matches = matches.Where(item => item.MailboxId == mailboxId);
        }
        var searchTerm = scope.SearchTerm?.Trim();
        if (searchTerm is not null)
        {
            matches = matches.Where(item =>
                (item.BodyPlainText != null && item.BodyPlainText.Contains(searchTerm))
                || item.Attachments.Any(attachment => attachment.FileName.Contains(searchTerm))
                || context.IntakeReceipts.Any(receipt =>
                    receipt.SourceChannel == "mailbox"
                    && receipt.ExternalReceiptToken == item.ExternalReceiptToken
                    && receipt.SearchDocuments.Any(document =>
                        document.Text != null && document.Text.Contains(searchTerm))));
        }

        // Counted and paged in SQL. Reading every row to take twenty-five of them
        // makes the list slower the more mail is retained, which is the one thing a
        // mailbox is guaranteed to accumulate.
        var totalCount = await matches.CountAsync(cancellationToken);
        var rows = await matches
            .OrderByDescending(item => item.ReceivedAtUtc)
            .ThenByDescending(item => item.Id)
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
                    && item.BodyPlainText != null
                    && item.BodyPlainText.Contains(searchTerm)))
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

        var summary = (await MapSummariesAsync(
            context,
            [
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
                    false)
            ],
            cancellationToken))[0];

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
                EffectiveSenderAddress = item.MailRouteDecision!.EffectiveSenderAddress
            })
            .SingleOrDefaultAsync(cancellationToken);

        // A staff forward is de-cluttered on read so existing (write-once) rows
        // are corrected too: the effective sender differs from the transport
        // sender exactly when the route unwrapped a Collision Engineers forward.
        var isStaffForward = receipt?.EffectiveSenderAddress is { } effectiveSender
            && !string.Equals(effectiveSender, entity.SenderAddress, StringComparison.OrdinalIgnoreCase);
        var body = StaffForwardBodyCleaner.Clean(entity.BodyPlainText ?? string.Empty, isStaffForward);

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
                item.MailboxId,
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
            completedPolls = completedPolls.Where(item => item.MailboxId == mailboxId);
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
                EffectiveSenderAddress = item.MailRouteDecision == null
                    ? null
                    : item.MailRouteDecision.EffectiveSenderAddress
            })
            .ToListAsync(cancellationToken);
        var receiptsByToken = receipts.ToDictionary(
            item => item.ExternalReceiptToken,
            StringComparer.Ordinal);
        var receiptIds = receipts.Select(item => item.Id).ToArray();
        var cases = receiptIds.Length == 0
            ? []
            : await context.CaseIntakeLinks
                .AsNoTracking()
                .Where(link => receiptIds.Contains(link.IntakeReceiptId))
                .Select(link => new
                {
                    link.IntakeReceiptId,
                    link.CaseId,
                    link.Case.Reference
                })
                .ToListAsync(cancellationToken);
        var casesByReceipt = cases.ToDictionary(item => item.IntakeReceiptId);
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
                    : casesByReceipt.GetValueOrDefault(receipt.Id);
                var allocationState = receipt is null
                    ? null
                    : allocationStates.GetValueOrDefault(receipt.Id);
                var isStaffForward = receipt?.EffectiveSenderAddress is { } effectiveSender
                    && !string.Equals(effectiveSender, row.SenderAddress, StringComparison.OrdinalIgnoreCase);
                var cleanedExcerpt = row.BodyExcerpt is { } excerpt
                    ? StaffForwardBodyCleaner.Clean(excerpt, isStaffForward)
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
                    receipt?.EffectiveSenderAddress,
                    row.Subject,
                    cleanedExcerpt,
                    row.ReceivedAtUtc,
                    row.IsRead,
                    row.AttachmentCount,
                    receipt is null
                        ? null
                        : EfIntakeReceiptStore.ParseDecision(receipt.Decision),
                    receipt?.Id,
                    linkedCase?.CaseId,
                    linkedCase?.Reference,
                    allocationState,
                    row.SearchMatches);
            })
            .ToArray();
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
        string MailboxId,
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
