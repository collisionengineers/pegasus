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
        var received = await ListReceivedAsync(db, caseId, retainedAtOrBeforeUtc, cancellationToken);
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

    public async Task<IReadOnlyList<UnretainedForwardOriginal>> ListUnretainedForwardOriginalsAsync(
        Guid caseId,
        DateTimeOffset retainedAtOrBeforeUtc,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var received = await ListReceivedAsync(db, caseId, retainedAtOrBeforeUtc, cancellationToken);
        var forwards = StaffForwards(received);
        if (forwards.Length == 0)
        {
            return [];
        }

        // An original Pegasus holds an Inbox copy of in the forwarding mailbox,
        // whenever it was retained, is the retained path's: it is moved by its
        // own identity, or left because it holds another Case (R958).
        var inbox = EfRetainedMailboxMessageStore.ToCode(MailFolderScope.Inbox);
        var named = forwards.SelectMany(forward => forward.Named).Distinct(StringComparer.Ordinal).ToArray();
        var retained = await db.RetainedMailboxMessages.AsNoTracking()
            .Where(message => message.FolderScope == inbox
                && message.MailboxId != null
                && message.CanonicalInternetMessageIdentity != null
                && named.Contains(message.CanonicalInternetMessageIdentity))
            .Join(
                db.ApprovedMailboxes.AsNoTracking(),
                message => message.MailboxId!.Value,
                mailbox => mailbox.Id,
                (message, mailbox) => new { mailbox.Address, Identity = message.CanonicalInternetMessageIdentity! })
            .ToArrayAsync(cancellationToken);
        // The approved mailboxes are a handful of rows; the forwarding address is
        // matched as the retained path matches it, ignoring case.
        var mailboxes = await db.ApprovedMailboxes.AsNoTracking()
            .Select(mailbox => new
            {
                mailbox.Address,
                mailbox.MailboxIdentity,
                mailbox.SentFolderIdentity,
                mailbox.State,
                mailbox.ActivatedAtUtc
            })
            .ToArrayAsync(cancellationToken);
        return forwards
            .Where(forward => !retained.Any(row => SameAddress(row.Address, forward.ForwardingAddress)
                && forward.Named.Contains(row.Identity, StringComparer.Ordinal)))
            .Select(forward => new
            {
                forward.Original,
                Mailbox = mailboxes.FirstOrDefault(mailbox => SameAddress(mailbox.Address, forward.ForwardingAddress)
                    && IsWritable(mailbox.MailboxIdentity, mailbox.State, mailbox.ActivatedAtUtc))
            })
            .Where(item => item.Mailbox is not null)
            .Select(item => new UnretainedForwardOriginal(
                item.Mailbox!.MailboxIdentity!,
                item.Mailbox!.SentFolderIdentity,
                item.Original))
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// The Case's received e-mails as they stand linked now, Inbox scope,
    /// retained at or before the cut-off, each with the approved mailbox it
    /// sits in, writable or not.
    /// </summary>
    private static async Task<MessageRow[]> ListReceivedAsync(
        PegasusDbContext db,
        Guid caseId,
        DateTimeOffset retainedAtOrBeforeUtc,
        CancellationToken cancellationToken)
    {
        var mailboxChannel = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox);
        var inbox = EfRetainedMailboxMessageStore.ToCode(MailFolderScope.Inbox);
        var linked = CurrentIntakeAssociations.ReceiptIdsLinkedTo(db, caseId);
        // A Triage Case's request e-mail is linked by the Triage itself, not by an
        // association row, as the Correspondence tab reads it.
        return await db.IntakeReceipts.AsNoTracking()
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
    }

    /// <summary>
    /// The staff forwards among <paramref name="received"/>: held in
    /// instructions@ or pegasustest@ (R586, R651, R956), with the address
    /// that forwarded each and the one message it was made from, the first
    /// identity it retained (R957), as retained and in the forms an identity
    /// column is compared with.
    /// </summary>
    private static StaffForward[] StaffForwards(MessageRow[] received) =>
        received
            .Where(row => PrincipalMailRoutePolicy.HoldsStaffForwards(row.MailboxAddress)
                && !string.IsNullOrWhiteSpace(row.SenderAddress))
            .Select(row => new
            {
                ForwardingAddress = row.SenderAddress!,
                Original = EfIntakeMutationStore.DeserializeReplyIdentities(row.InReplyToIdentitiesJson).FirstOrDefault()
            })
            .Where(forward => !string.IsNullOrWhiteSpace(forward.Original))
            .Select(forward => new StaffForward(
                forward.ForwardingAddress,
                forward.Original!,
                EfIntakeMutationStore.ReplyIdentityLookup([forward.Original!])))
            .ToArray();

    private sealed record StaffForward(string ForwardingAddress, string Original, string[] Named);

    /// <summary>
    /// The original each Collision Engineers staff forward among
    /// <paramref name="received"/> was made from (operator, 10 November 2026;
    /// R917): the forward is held in instructions@ (R586, R651) or, on the
    /// test instance, pegasustest@ (11 November 2026; R956), an approved
    /// mailbox sent it, and the message it answers is that mailbox's own
    /// Inbox message. That is one message, the first identity the forward
    /// retained (<c>LocalEmailDisplayReader.ReplyIdentities</c>), never the
    /// earlier messages of its thread (R957). The original goes with the
    /// forward whether or not it is linked to the Case; one that holds
    /// another Case is left, since the tidy never touches another Case's
    /// message (R958).
    /// </summary>
    private static async Task<List<MessageRow>> ListForwardOriginalsAsync(
        PegasusDbContext db,
        Guid caseId,
        MessageRow[] received,
        DateTimeOffset retainedAtOrBeforeUtc,
        CancellationToken cancellationToken)
    {
        var forwards = StaffForwards(received);
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
        IsWritable(row.MailboxIdentity, row.MailboxState, row.MailboxActivatedAtUtc);

    private static bool IsWritable(string? mailboxIdentity, string mailboxState, DateTimeOffset? activatedAtUtc) =>
        mailboxIdentity is not null
        && activatedAtUtc is not null
        && string.Equals(mailboxState, ApprovedMailboxState.Approved.ToString(), StringComparison.Ordinal);

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
            eventType = SentMailTidyHistory.CorrespondenceMoveFailed;
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
                eventType = SentMailTidyHistory.CorrespondenceMoved;
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
            await SentMailTidyHistory.AddAsync(
                db,
                id,
                eventType,
                Actor,
                reason,
                $"correspondence-tidy:{operation.Id:N}:{operation.CorrespondenceMoveAttempts}",
                nowUtc,
                cancellationToken);
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
