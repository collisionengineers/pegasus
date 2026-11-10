using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The send journal's record of the Delete case correspondence tidy after a
/// ticked send is confirmed (ADR-0071, R902). A row is due while its tidy state is
/// unset and it has had fewer than
/// <see cref="TidySentCaseCorrespondence.MaximumAttempts"/> runs.
/// </summary>
internal sealed class EfSentCaseCorrespondenceTidyStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : ISentCaseCorrespondenceTidyStore
{
    internal const string DoneState = "Done";
    internal const string FailedState = "Failed";
    private const string Actor = "sent-mail-correspondence-tidy";

    public async Task<IReadOnlyList<SentCaseCorrespondenceCandidate>> ListDueAsync(
        int maximumItems,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumItems, 1);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Set<StaffMailSendOperationEntity>().AsNoTracking()
            .Where(operation => operation.TidiesCaseCorrespondence
                && operation.State == StaffMailState.Sent
                && operation.CorrespondenceMoveState == null
                && operation.CorrespondenceMoveAttempts < TidySentCaseCorrespondence.MaximumAttempts)
            .OrderBy(operation => operation.ObservedSentAtUtc)
            .ThenBy(operation => operation.Id)
            .Take(maximumItems)
            .Select(operation => new
            {
                operation.Id,
                operation.Version,
                operation.Purpose,
                operation.ContextId,
                operation.CreatedAtUtc,
                operation.CorrespondenceMoveAttempts,
                // A report send's context is its generation; every other ticked send's is the Case.
                ReportCaseId = db.Set<CaseReportGenerationEntity>()
                    .Where(generation => generation.Id == operation.ContextId)
                    .Select(generation => (Guid?)generation.CaseId)
                    .FirstOrDefault()
            })
            .ToArrayAsync(cancellationToken);
        return rows
            .Select(row => new
            {
                row,
                CaseId = row.Purpose == StaffMailPurpose.CaseReport ? row.ReportCaseId : row.ContextId
            })
            .Where(item => item.CaseId is not null)
            .Select(item => new SentCaseCorrespondenceCandidate(
                item.row.Id,
                item.row.Version,
                item.CaseId!.Value,
                item.row.CreatedAtUtc,
                item.row.CorrespondenceMoveAttempts))
            .ToArray();
    }

    public async Task<IReadOnlyList<CaseCorrespondenceMessage>> ListMessagesAsync(
        Guid caseId,
        DateTimeOffset retainedAtOrBeforeUtc,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var mailboxChannel = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox);
        var inbox = EfRetainedMailboxMessageStore.ToCode(MailFolderScope.Inbox);
        var linked = CurrentIntakeAssociations.ReceiptIdsLinkedTo(db, caseId);
        // A Triage Case's request e-mail is linked by the Triage itself, not by an
        // association row, as the Correspondence tab reads it.
        var received = await db.IntakeReceipts.AsNoTracking()
            .Where(receipt => receipt.SourceChannel == mailboxChannel
                && (linked.Contains(receipt.Id)
                    || db.Triage.Any(triage => triage.CaseId == caseId && triage.OriginReceiptId == receipt.Id)))
            .Join(
                db.RetainedMailboxMessages.AsNoTracking(),
                receipt => receipt.ExternalReceiptToken,
                message => message.ExternalReceiptToken,
                (receipt, message) => message)
            .Where(message => message.FolderScope == inbox
                && message.MailboxId != null
                && message.RetainedAtUtc <= retainedAtOrBeforeUtc)
            .Join(
                db.ApprovedMailboxes.AsNoTracking(),
                message => message.MailboxId!.Value,
                mailbox => mailbox.Id,
                (message, mailbox) => new MessageRow(
                    message.Id,
                    message.ImmutableMessageId,
                    message.ReceivedAtUtc,
                    message.ExternalReceiptToken,
                    message.SenderAddress,
                    message.InReplyToIdentitiesJson,
                    message.CanonicalInternetMessageIdentity,
                    mailbox.Address,
                    mailbox.MailboxIdentity,
                    mailbox.State,
                    mailbox.ActivatedAtUtc))
            .ToArrayAsync(cancellationToken);
        var originals = await ListForwardOriginalsAsync(
            db, caseId, received, retainedAtOrBeforeUtc, cancellationToken);
        return received
            .Where(IsWritable)
            .Concat(originals)
            .DistinctBy(row => row.Id)
            .OrderBy(row => row.ReceivedAtUtc)
            .ThenBy(row => row.Id)
            .Select(row => new CaseCorrespondenceMessage(row.Id, row.MailboxIdentity!, row.ImmutableMessageId))
            .ToArray();
    }

    /// <summary>
    /// The original each Collision Engineers staff forward among
    /// <paramref name="received"/> was made from (operator, 10 November 2026;
    /// R917): the forward is held in instructions@ (R586, R651), an approved
    /// mailbox sent it, and its In-Reply-To and References name that
    /// mailbox's own Inbox message. The original goes with the forward
    /// whether or not it is linked to the Case; one that holds another Case
    /// is left, since the tidy never touches another Case's message.
    /// </summary>
    private static async Task<List<MessageRow>> ListForwardOriginalsAsync(
        PegasusDbContext db,
        Guid caseId,
        MessageRow[] received,
        DateTimeOffset retainedAtOrBeforeUtc,
        CancellationToken cancellationToken)
    {
        var forwards = received
            .Where(row => SameAddress(row.MailboxAddress, PrincipalMailRoutePolicy.StaffForwardMailboxAddress)
                && !string.IsNullOrWhiteSpace(row.SenderAddress))
            .Select(row => new
            {
                ForwardingAddress = row.SenderAddress!,
                Named = EfIntakeMutationStore.ReplyIdentityLookup(
                    EfIntakeMutationStore.DeserializeReplyIdentities(row.InReplyToIdentitiesJson))
            })
            .Where(forward => forward.Named.Length > 0)
            .ToArray();
        if (forwards.Length == 0)
        {
            return [];
        }

        var mailboxChannel = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox);
        var inbox = EfRetainedMailboxMessageStore.ToCode(MailFolderScope.Inbox);
        var named = forwards.SelectMany(forward => forward.Named).Distinct(StringComparer.Ordinal).ToArray();
        var candidates = await db.RetainedMailboxMessages.AsNoTracking()
            .Where(message => message.FolderScope == inbox
                && message.MailboxId != null
                && message.CanonicalInternetMessageIdentity != null
                && named.Contains(message.CanonicalInternetMessageIdentity)
                && message.RetainedAtUtc <= retainedAtOrBeforeUtc)
            .Join(
                db.ApprovedMailboxes.AsNoTracking(),
                message => message.MailboxId!.Value,
                mailbox => mailbox.Id,
                (message, mailbox) => new MessageRow(
                    message.Id,
                    message.ImmutableMessageId,
                    message.ReceivedAtUtc,
                    message.ExternalReceiptToken,
                    message.SenderAddress,
                    message.InReplyToIdentitiesJson,
                    message.CanonicalInternetMessageIdentity,
                    mailbox.Address,
                    mailbox.MailboxIdentity,
                    mailbox.State,
                    mailbox.ActivatedAtUtc))
            .ToArrayAsync(cancellationToken);
        var originals = new List<MessageRow>();
        // The original sits in the mailbox that forwarded it, never another one
        // that happens to hold a message the forward names.
        foreach (var candidate in candidates.Where(row => IsWritable(row)
            && forwards.Any(forward => SameAddress(row.MailboxAddress, forward.ForwardingAddress)
                && forward.Named.Contains(row.CanonicalInternetMessageIdentity!, StringComparer.Ordinal))))
        {
            var held = await CurrentIntakeAssociations.HeldCaseIdsAsync(
                db,
                db.IntakeReceipts.Where(receipt => receipt.SourceChannel == mailboxChannel
                    && receipt.ExternalReceiptToken == candidate.ExternalReceiptToken),
                cancellationToken);
            if (held.All(heldCaseId => heldCaseId == caseId))
            {
                originals.Add(candidate);
            }
        }

        return originals;
    }

    /// <summary>
    /// A mailbox Pegasus may move a message in: Approved, identified and
    /// activated. One an Administrator has withdrawn is never written to, nor
    /// is one that is not enabled, as a production copy leaves every mailbox
    /// on the test instance (operator, 10 November 2026; R916, R627).
    /// </summary>
    private static bool IsWritable(MessageRow row) =>
        row.MailboxIdentity is not null
        && row.MailboxActivatedAtUtc is not null
        && string.Equals(row.MailboxState, ApprovedMailboxState.Approved.ToString(), StringComparison.Ordinal);

    private static bool SameAddress(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>One Inbox-scope retained message with the approved mailbox it sits in.</summary>
    private sealed record MessageRow(
        Guid Id,
        string ImmutableMessageId,
        DateTimeOffset ReceivedAtUtc,
        string ExternalReceiptToken,
        string? SenderAddress,
        string? InReplyToIdentitiesJson,
        string? CanonicalInternetMessageIdentity,
        string MailboxAddress,
        string? MailboxIdentity,
        string MailboxState,
        DateTimeOffset? MailboxActivatedAtUtc);

    public async Task<bool> RecordAsync(
        Guid operationId,
        long expectedVersion,
        SentCaseCorrespondenceCounts counts,
        string? failureCode,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(counts);
        if (counts.Moved < 0 || counts.AlreadyMoved < 0 || counts.Missing < 0 || counts.Failed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(counts));
        }
        if (counts.Failed > 0 && string.IsNullOrWhiteSpace(failureCode))
        {
            throw new ArgumentException("A failed tidy needs a failure code.", nameof(failureCode));
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var operation = await db.Set<StaffMailSendOperationEntity>().SingleOrDefaultAsync(
            value => value.Id == operationId && value.Version == expectedVersion,
            cancellationToken);
        if (operation is null
            || !operation.TidiesCaseCorrespondence
            || operation.CorrespondenceMoveState is not null
            || operation.CorrespondenceMoveAttempts >= TidySentCaseCorrespondence.MaximumAttempts)
        {
            return false;
        }

        operation.CorrespondenceMoveAttempts++;
        operation.Version++;
        if (counts.Moved > 0)
        {
            operation.CorrespondenceMovedAtUtc = nowUtc;
        }

        string? eventType = null;
        var reason = string.Empty;
        if (counts.Failed > 0)
        {
            operation.CorrespondenceMoveFailureCode = failureCode!.Trim();
            if (operation.CorrespondenceMoveAttempts >= TidySentCaseCorrespondence.MaximumAttempts)
            {
                operation.CorrespondenceMoveState = FailedState;
            }
            eventType = "correspondence_move_failed";
            reason = $"{counts.Failed} of the Case's received e-mails could not be moved to Deleted Items ({operation.CorrespondenceMoveFailureCode}); attempt {operation.CorrespondenceMoveAttempts} of {TidySentCaseCorrespondence.MaximumAttempts}.";
        }
        else
        {
            operation.CorrespondenceMoveState = DoneState;
            operation.CorrespondenceMoveFailureCode = null;
            // Messages that are all gone from the mailbox, or a Case with no received
            // e-mail, changed nothing on the Case, so that is recorded on the send row alone.
            if (counts.Moved + counts.AlreadyMoved > 0)
            {
                eventType = "correspondence_moved_to_deleted_items";
                reason = $"The Case's received e-mails were moved to Deleted Items after the send: {counts.Moved} moved, {counts.AlreadyMoved} already there, {counts.Missing} no longer in the mailbox.";
            }
        }

        // The Case is read from the row, never taken from the caller.
        var caseId = operation.Purpose == StaffMailPurpose.CaseReport
            ? await db.Set<CaseReportGenerationEntity>().AsNoTracking()
                .Where(value => value.Id == operation.ContextId)
                .Select(value => (Guid?)value.CaseId)
                .SingleOrDefaultAsync(cancellationToken)
            : operation.ContextId;
        if (eventType is not null && caseId is { } id)
        {
            db.CaseHistory.Add(new CaseHistoryEntity
            {
                Id = Guid.NewGuid(),
                CaseId = id,
                EventType = eventType,
                Actor = Actor,
                Reason = reason,
                OccurredAtUtc = nowUtc,
                // CaseHistory.OperationKey is unique across the table, so this never
                // shares the instruction tidy's prefix.
                OperationKey = $"correspondence-tidy:{operation.Id:N}:{operation.CorrespondenceMoveAttempts}",
                BeforeVersion = null,
                AfterVersion = 0
            });
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }

        return true;
    }
}
