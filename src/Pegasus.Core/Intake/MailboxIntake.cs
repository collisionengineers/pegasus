using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

public sealed record ApprovedInboxPollLease(
    string MailboxId,
    string MailboxAddress,
    string InboxFolderIdentity,
    string? Cursor,
    string LeaseToken);

public sealed record ApprovedInboxSourceRejection(
    string FailureCode,
    long? SourceLength,
    string? SourceHash,
    string? RetentionKey,
    string? OriginalSourceHash = null,
    string? EvidenceMarker = null);

public sealed record RetainedMailboxAttachment(
    string FileName,
    string MediaType,
    long ContentLength);

/// <summary>
/// Everything the mail workspace shows about one message, as the source read it.
/// </summary>
/// <remarks>
/// Carried on the polled message rather than re-derived later because the MIME is
/// in hand exactly once, at poll time. <c>BodyPlainText</c> is retained with the
/// rest: the alternative is re-reading the artifact on every view, which on a
/// workstation with no Worker running means the viewer is blank.
/// </remarks>
public sealed record RetainedMailboxMessageMetadata(
    string FolderIdentity,
    string? ConversationIdentity,
    string? InternetMessageIdentity,
    string? SenderAddress,
    string? SenderDisplayName,
    IReadOnlyList<string> ToAddresses,
    IReadOnlyList<string> CcAddresses,
    string? Subject,
    string? BodyPlainText,
    IReadOnlyList<RetainedMailboxAttachment> Attachments,
    bool IsRead);

public sealed record ApprovedInboxMessage(
    string ImmutableMessageId,
    string FileName,
    ReadOnlyMemory<byte> MimeContent,
    DateTimeOffset ReceivedAtUtc,
    string NextCursor)
{
    public ApprovedInboxSourceRejection? SourceRejection { get; init; }

    /// <summary>
    /// What the workspace will display, where the source could read it. An init
    /// property rather than a constructor parameter, following
    /// <see cref="SourceRejection"/>: a source that supplies none still polls, and
    /// the messages it brought in are then absent from the viewer rather than the
    /// poll refusing them.
    /// </summary>
    public RetainedMailboxMessageMetadata? RetainedMetadata { get; init; }
}

/// <summary>
/// One retained message row. Written once, on the tick that first accepted the
/// message, and never updated: the fact recorded is what arrived, not what the
/// mailbox looks like now.
/// </summary>
public sealed record RetainedMailboxMessage(
    string MailboxId,
    string MailboxAddress,
    string ImmutableMessageId,
    string ExternalReceiptToken,
    DateTimeOffset ReceivedAtUtc,
    long SourceLength,
    string SourceSha256,
    RetainedMailboxMessageMetadata Metadata,
    DateTimeOffset RetainedAtUtc);

public interface IRetainedMailboxMessageStore
{
    /// <summary>
    /// Inserts the row if the mailbox has never retained this message identity.
    /// A redelivery is a no-op, never an update.
    /// </summary>
    Task RetainAsync(
        RetainedMailboxMessage message,
        CancellationToken cancellationToken);
}

public sealed record ApprovedInboxPage(
    IReadOnlyList<ApprovedInboxMessage> Messages,
    string NextCursor);

public sealed record ApprovedInboxPoisonMessage(
    string OccurrenceKey,
    string ImmutableMessageId,
    string FileName,
    long? SourceLength,
    string? SourceHash,
    string? OriginalSourceHash,
    string? EvidenceMarker,
    string? StorageKey,
    DateTimeOffset ReceivedAtUtc,
    string FailureCode);

/// <summary>
/// The mail provider refused this mailbox outright. Approving an address in Pegasus
/// grants nothing at the tenant: the application still needs the tenant's own
/// application access policy to admit it. Distinguished from a generic transport
/// failure so the administration surface can say which of the two happened.
/// </summary>
public sealed class ApprovedMailboxAccessDeniedException(
    string message,
    Exception? innerException = null)
    : Exception(message, innerException);

public interface IApprovedInboxSource
{
    Task<ApprovedInboxPage> ReadAsync(
        ApprovedInboxPollLease lease,
        int maximumMessages,
        CancellationToken cancellationToken);
}

public interface IApprovedInboxPollStore
{
    Task<ApprovedInboxPollLease?> ClaimAsync(
        ApprovedIntakeMailbox mailbox,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task AdvanceAsync(
        string mailboxId,
        string leaseToken,
        string nextCursor,
        DateTimeOffset advancedAtUtc,
        CancellationToken cancellationToken);

    Task QuarantineAsync(
        string mailboxId,
        string leaseToken,
        ApprovedInboxPoisonMessage message,
        string nextCursor,
        DateTimeOffset quarantinedAtUtc,
        CancellationToken cancellationToken);

    Task CompleteAsync(
        string mailboxId,
        string leaseToken,
        string nextCursor,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken);

    Task ReleaseAsync(
        string mailboxId,
        string leaseToken,
        DateTimeOffset dueAtUtc,
        string failureCode,
        CancellationToken cancellationToken);
}

public sealed class PollApprovedInbox(
    IApprovedIntakeMailboxes approvedIntakeMailboxes,
    IApprovedMailboxPolicy approvedMailboxPolicy,
    IApprovedInboxPollStore pollStore,
    IApprovedInboxSource inboxSource,
    IIntakeArtifactStore artifactStore,
    IIntakeQuarantineArtifactStore quarantineArtifactStore,
    ReceiveIntake receiveIntake,
    IRetainedMailboxMessageStore retainedMessageStore,
    TimeProvider timeProvider)
{
    private const int MaximumFileNameLength = 260;
    private const int MaximumExternalReceiptTokenLength = 200;
    private const int MaximumActorLength = 200;
    private const int MaximumMailboxIdentityLength = 100;
    private const int MaximumFolderIdentityLength = 200;
    private const int MaximumRetainedFolderIdentityLength = 500;
    private const int MaximumMessageIdentityLength = 500;
    private const int MaximumAddressLength = 320;
    private const int MaximumSubjectLength = 1000;
    private const int MaximumMediaTypeLength = 200;
    private const int MaximumRecipientCount = 50;
    private const string SystemWorkerActorPrefix = "system-worker:";
    private static readonly TimeSpan PollLeaseDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan FailureRetryDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Polls every mailbox the approved estate currently offers for inbound intake,
    /// each under its own lease and cursor. <paramref name="maximumMessages"/> bounds
    /// one mailbox, not the tick, so a busy mailbox cannot starve the others.
    /// </summary>
    public async Task<int> ExecuteAsync(
        int maximumMessages,
        ActionActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMessages);
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.Kind != ActorKind.SystemWorker)
        {
            throw new UnauthorizedAccessException("Approved inbox polling requires a system-worker actor.");
        }

        var actorCode = $"{SystemWorkerActorPrefix}{actor.SubjectId}";
        if (actorCode.Length > MaximumActorLength)
        {
            throw new ArgumentException(
                $"The system-worker identity must be {MaximumActorLength - SystemWorkerActorPrefix.Length} characters or fewer.",
                nameof(actor));
        }

        var mailboxes = await approvedIntakeMailboxes.ListPollableAsync(cancellationToken);
        ArgumentNullException.ThrowIfNull(mailboxes);

        // Fail closed on the whole estate before claiming anything: a malformed
        // identity is an administration defect, not a per-message poison.
        foreach (var mailbox in mailboxes)
        {
            ValidateMailbox(mailbox);
        }

        var handledMessages = 0;
        var failures = new List<Exception>();
        foreach (var mailbox in mailboxes)
        {
            handledMessages += await PollMailboxAsync(
                mailbox,
                maximumMessages,
                actorCode,
                failures,
                cancellationToken);
        }

        // One failure keeps its exact type and stack so existing callers and tests
        // that assert on a specific intake exception still see it unchanged.
        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures.Count > 1)
        {
            throw new AggregateException(
                "One or more approved mailboxes failed during the bounded inbox poll.",
                failures);
        }

        return handledMessages;
    }

    private async Task<int> PollMailboxAsync(
        ApprovedIntakeMailbox mailbox,
        int maximumMessages,
        string actorCode,
        List<Exception> failures,
        CancellationToken cancellationToken)
    {
        // The whole body sits inside the boundary, claim included. A claim, a lease
        // validation or an approval re-check that threw from outside it escaped this
        // method, escaped the loop over the estate, and bypassed the aggregation
        // below — so one mailbox's failure skipped every mailbox after it and threw
        // away every failure already collected before it.
        ApprovedInboxPollLease? lease = null;
        try
        {
            lease = await pollStore.ClaimAsync(
                mailbox,
                timeProvider.GetUtcNow(),
                PollLeaseDuration,
                cancellationToken);
            if (lease is null)
            {
                return 0;
            }

            ValidateLease(lease);

            // Re-check after claiming, exactly as the Sent side does: the estate may have
            // disabled this mailbox between listing and claiming.
            if (!await approvedMailboxPolicy.IsApprovedAsync(
                    lease.MailboxAddress,
                    ApprovedMailboxRouteScope.InboundIntake,
                    cancellationToken))
            {
                await pollStore.ReleaseAsync(
                    lease.MailboxId,
                    lease.LeaseToken,
                    timeProvider.GetUtcNow().Add(FailureRetryDelay),
                    "mailbox_not_approved",
                    cancellationToken);
                return 0;
            }

            return await PollOneAsync(lease, maximumMessages, actorCode, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            // Only a lease that was actually claimed can be released, and a release
            // that fails must not replace the failure that caused it: the lease
            // lapses on its own, and the original exception is what the estate needs
            // to be told about.
            if (lease is not null)
            {
                try
                {
                    await pollStore.ReleaseAsync(
                        lease.MailboxId,
                        lease.LeaseToken,
                        timeProvider.GetUtcNow().Add(FailureRetryDelay),
                        FailureCode(exception),
                        cancellationToken);
                }
                catch (Exception releaseFailure) when (
                    IntakeExceptionPolicy.IsRecoverable(releaseFailure))
                {
                    // Swallowed deliberately; the lease lapses and the original
                    // failure below is the one that matters.
                }
            }

            failures.Add(exception);
            return 0;
        }
    }

    private async Task<int> PollOneAsync(
        ApprovedInboxPollLease lease,
        int maximumMessages,
        string actorCode,
        CancellationToken cancellationToken)
    {
        var page = await inboxSource.ReadAsync(lease, maximumMessages, cancellationToken);
        ValidatePage(page, maximumMessages);

        var handledMessages = 0;
        foreach (var message in page.Messages)
        {
            PreparedMessage prepared;
            try
            {
                prepared = PrepareMessage(lease, actorCode, message);
            }
            catch (MalformedApprovedInboxMessageException exception)
            {
                await QuarantineMalformedMessageAsync(
                    lease,
                    message,
                    exception.FailureCode,
                    cancellationToken);
                handledMessages++;
                continue;
            }

            try
            {
                await receiveIntake.ExecuteAsync(
                    prepared.Source,
                    CreateOperationKey(prepared.ExternalReceiptToken),
                    cancellationToken);
            }
            catch (IntakeSourceIdentityConflictException exception)
            {
                await QuarantineSourceIdentityConflictAsync(
                    lease,
                    message,
                    exception,
                    cancellationToken);
                handledMessages++;
                continue;
            }

            // Between acceptance and the cursor advance, deliberately. After, so a
            // source-identity conflict is quarantined and never appears in the
            // workspace as something that arrived cleanly; before, so a failed
            // retain leaves the cursor where it was and the outer catch releases
            // the lease — the next tick redelivers the message and the store's
            // insert-if-absent makes that harmless.
            if (prepared.RetainedMessage is { } retained)
            {
                await retainedMessageStore.RetainAsync(
                    retained with { RetainedAtUtc = timeProvider.GetUtcNow() },
                    cancellationToken);
            }

            await pollStore.AdvanceAsync(
                lease.MailboxId,
                lease.LeaseToken,
                message.NextCursor,
                timeProvider.GetUtcNow(),
                cancellationToken);
            handledMessages++;
        }

        await pollStore.CompleteAsync(
            lease.MailboxId,
            lease.LeaseToken,
            page.NextCursor,
            timeProvider.GetUtcNow(),
            cancellationToken);
        return handledMessages;
    }

    private async Task QuarantineMalformedMessageAsync(
        ApprovedInboxPollLease lease,
        ApprovedInboxMessage message,
        string failureCode,
        CancellationToken cancellationToken)
    {
        long? sourceLength;
        string? sourceHash;
        string? originalSourceHash;
        string? evidenceMarker;
        string? storageKey;
        if (message.SourceRejection is { } rejection)
        {
            ValidateSourceRejection(message, rejection);
            sourceLength = rejection.SourceLength;
            sourceHash = rejection.SourceHash;
            originalSourceHash = rejection.OriginalSourceHash;
            evidenceMarker = rejection.EvidenceMarker;
            storageKey = rejection.RetentionKey;
            if (storageKey is not null)
            {
                await VerifyRetainedArtifactAsync(
                    new(
                        storageKey,
                        sourceHash!,
                        sourceLength!.Value),
                    cancellationToken);
            }
        }
        else
        {
            sourceLength = message.MimeContent.Length;
            sourceHash = Convert.ToHexString(SHA256.HashData(message.MimeContent.Span));
            originalSourceHash = null;
            evidenceMarker = null;
            try
            {
                storageKey = await artifactStore.StoreAsync(
                    sourceHash,
                    message.MimeContent,
                    cancellationToken);
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                throw new IntakeArtifactRetentionException(exception);
            }
            await VerifyRetainedArtifactAsync(
                new(storageKey, sourceHash, message.MimeContent.Length),
                cancellationToken);
        }

        await pollStore.QuarantineAsync(
            lease.MailboxId,
            lease.LeaseToken,
            new(
                CreateOccurrenceKey(message.NextCursor),
                message.ImmutableMessageId ?? string.Empty,
                message.FileName ?? string.Empty,
                sourceLength,
                sourceHash,
                originalSourceHash,
                evidenceMarker,
                storageKey,
                message.ReceivedAtUtc,
                failureCode),
            message.NextCursor,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private async Task QuarantineSourceIdentityConflictAsync(
        ApprovedInboxPollLease lease,
        ApprovedInboxMessage message,
        IntakeSourceIdentityConflictException conflict,
        CancellationToken cancellationToken)
    {
        var sourceHash = Convert.ToHexString(SHA256.HashData(message.MimeContent.Span));
        if (!IsHash(conflict.ExistingSourceHash)
            || !IsHash(conflict.PresentedSourceHash)
            || !string.Equals(
                conflict.PresentedSourceHash,
                sourceHash,
                StringComparison.Ordinal))
        {
            throw new IntakeArtifactIntegrityException();
        }

        string storageKey;
        try
        {
            storageKey = await artifactStore.StoreAsync(
                sourceHash,
                message.MimeContent,
                cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            throw new IntakeArtifactRetentionException(exception);
        }

        await VerifyRetainedArtifactAsync(
            new(storageKey, sourceHash, message.MimeContent.Length),
            cancellationToken);
        await pollStore.QuarantineAsync(
            lease.MailboxId,
            lease.LeaseToken,
            new(
                CreateOccurrenceKey(message.NextCursor),
                message.ImmutableMessageId,
                message.FileName,
                message.MimeContent.Length,
                sourceHash,
                conflict.ExistingSourceHash,
                "identity_conflict",
                storageKey,
                message.ReceivedAtUtc,
                "source_identity_conflict"),
            message.NextCursor,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private static void ValidateMailbox(ApprovedIntakeMailbox mailbox)
    {
        ArgumentNullException.ThrowIfNull(mailbox);
        RequireExactIdentity(
            mailbox.MailboxId,
            MaximumMailboxIdentityLength,
            "approved mailbox identity");
        ArgumentException.ThrowIfNullOrWhiteSpace(mailbox.Address);
        RequireExactIdentity(
            mailbox.InboxFolderIdentity,
            MaximumFolderIdentityLength,
            "approved Inbox folder identity");
    }

    private static void RequireExactIdentity(string value, int maximumLength, string description)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Length > maximumLength
            || value.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            throw new ArgumentException(
                $"The {description} is not an exact identity of {maximumLength} characters or fewer.");
        }
    }

    private static void ValidateLease(ApprovedInboxPollLease lease)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lease.MailboxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(lease.MailboxAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(lease.InboxFolderIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(lease.LeaseToken);
    }

    private static void ValidatePage(ApprovedInboxPage page, int maximumMessages)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(page.Messages);
        if (page.Messages.Count > maximumMessages)
        {
            throw new InvalidDataException("The approved inbox source returned more messages than requested.");
        }

        if (string.IsNullOrWhiteSpace(page.NextCursor))
        {
            throw new InvalidDataException("The approved inbox source returned an invalid cursor.");
        }

        var cursors = new HashSet<string>(StringComparer.Ordinal);
        foreach (var message in page.Messages)
        {
            if (message is null)
            {
                throw new InvalidDataException("The approved inbox source returned a missing message.");
            }

            if (string.IsNullOrWhiteSpace(message.NextCursor)
                || !cursors.Add(message.NextCursor))
            {
                throw new InvalidDataException(
                    "The approved inbox source did not return a unique cursor after each message.");
            }
        }

        if (page.Messages.Count > 0
            && !string.Equals(
                page.Messages[^1].NextCursor,
                page.NextCursor,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The approved inbox page cursor does not identify the state after its final message.");
        }
    }

    private static PreparedMessage PrepareMessage(
        ApprovedInboxPollLease lease,
        string actorCode,
        ApprovedInboxMessage message)
    {
        var mailboxId = lease.MailboxId;
        if (string.IsNullOrWhiteSpace(message.ImmutableMessageId))
        {
            throw new MalformedApprovedInboxMessageException(
                "missing_message_identity",
                "The approved inbox message identity is missing.");
        }

        if (string.IsNullOrWhiteSpace(message.FileName))
        {
            throw new MalformedApprovedInboxMessageException(
                "missing_message_file_name",
                "The approved inbox message file name is missing.");
        }

        string safeFileName;
        try
        {
            safeFileName = Path.GetFileName(message.FileName);
        }
        catch (ArgumentException exception)
        {
            throw new MalformedApprovedInboxMessageException(
                "invalid_message_file_name",
                "The approved inbox message file name is invalid.",
                exception);
        }

        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            throw new MalformedApprovedInboxMessageException(
                "invalid_message_file_name",
                "The approved inbox message file name is invalid.");
        }

        if (safeFileName.Length > MaximumFileNameLength)
        {
            throw new MalformedApprovedInboxMessageException(
                "message_file_name_too_long",
                $"The approved inbox message file name exceeds {MaximumFileNameLength} characters.");
        }

        if (message.SourceRejection is { } rejection)
        {
            ValidateSourceRejection(message, rejection);
            throw new MalformedApprovedInboxMessageException(
                rejection.FailureCode,
                "The approved inbox source rejected the message before materializing its content.");
        }

        if (message.MimeContent.IsEmpty)
        {
            throw new MalformedApprovedInboxMessageException(
                "empty_message",
                "The approved inbox message is empty.");
        }

        if (message.MimeContent.Length > IntakeEnvelopeLimits.MaximumContentLength)
        {
            throw new MalformedApprovedInboxMessageException(
                "message_too_large",
                "The approved inbox message exceeds the 10 MB intake limit.");
        }

        var externalReceiptToken = $"{mailboxId.Length}:{mailboxId}{message.ImmutableMessageId}";
        if (externalReceiptToken.Length > MaximumExternalReceiptTokenLength)
        {
            throw new MalformedApprovedInboxMessageException(
                "message_identity_too_long",
                "The approved inbox message identity exceeds the supported length.");
        }

        RetainedMailboxMessage? retainedMessage = null;
        if (message.RetainedMetadata is { } metadata)
        {
            ValidateRetainedMetadata(metadata);
            retainedMessage = new(
                mailboxId,
                lease.MailboxAddress,
                message.ImmutableMessageId,
                externalReceiptToken,
                message.ReceivedAtUtc,
                message.MimeContent.Length,
                Convert.ToHexString(SHA256.HashData(message.MimeContent.Span)),
                metadata,
                // Replaced with the clock reading taken at the write, which is the
                // only place that knows when the row was actually made.
                message.ReceivedAtUtc);
        }

        return new(
            new(
                safeFileName,
                "message/rfc822",
                message.MimeContent,
                message.ReceivedAtUtc,
                actorCode,
                new(IntakeSourceChannel.Mailbox, externalReceiptToken)),
            externalReceiptToken,
            retainedMessage);
    }

    /// <summary>
    /// Bounds on what the workspace will store, applied before anything is written.
    /// A source that hands over a 4 KB subject or a thousand recipients is
    /// malformed, and the poll treats it as poison rather than truncating material
    /// an operator will later read as if it were complete.
    /// </summary>
    private static void ValidateRetainedMetadata(RetainedMailboxMessageMetadata metadata)
    {
        var valid = IsBounded(metadata.FolderIdentity, MaximumRetainedFolderIdentityLength)
            && IsOptionalBounded(metadata.ConversationIdentity, MaximumMessageIdentityLength)
            && IsOptionalBounded(metadata.InternetMessageIdentity, MaximumMessageIdentityLength)
            && IsOptionalBounded(metadata.SenderAddress, MaximumAddressLength)
            && IsOptionalBounded(metadata.SenderDisplayName, MaximumAddressLength)
            && IsOptionalBounded(metadata.Subject, MaximumSubjectLength)
            && IsAddressList(metadata.ToAddresses)
            && IsAddressList(metadata.CcAddresses)
            && metadata.Attachments is not null
            && metadata.Attachments.All(attachment =>
                attachment is not null
                && IsBounded(attachment.FileName, MaximumFileNameLength)
                && IsBounded(attachment.MediaType, MaximumMediaTypeLength)
                && attachment.ContentLength >= 0);
        if (!valid)
        {
            throw new MalformedApprovedInboxMessageException(
                "invalid_message_metadata",
                "The approved inbox message metadata is outside the retained bounds.");
        }
    }

    private static bool IsAddressList(IReadOnlyList<string>? addresses) =>
        addresses is not null
        && addresses.Count <= MaximumRecipientCount
        && addresses.All(address => IsBounded(address, MaximumAddressLength));

    private static bool IsBounded(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;

    private static bool IsOptionalBounded(string? value, int maximumLength) =>
        value is null || IsBounded(value, maximumLength);

    private async Task VerifyRetainedArtifactAsync(
        IntakeQuarantineArtifact artifact,
        CancellationToken cancellationToken)
    {
        try
        {
            await quarantineArtifactStore.VerifyAsync(artifact, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            throw new IntakeArtifactRetentionException(exception);
        }
    }

    private static void ValidateSourceRejection(
        ApprovedInboxMessage message,
        ApprovedInboxSourceRejection rejection)
    {
        var valid = rejection.FailureCode switch
        {
            "message_too_large" =>
                rejection.SourceLength is > IntakeEnvelopeLimits.MaximumContentLength
                && IsHash(rejection.SourceHash)
                && IsStorageKey(rejection.RetentionKey)
                && rejection.OriginalSourceHash is null
                && rejection.EvidenceMarker is null,
            "immutable_source_changed" =>
                rejection.SourceLength is >= 0
                && IsHash(rejection.SourceHash)
                && IsStorageKey(rejection.RetentionKey)
                && IsHash(rejection.OriginalSourceHash)
                && !string.Equals(
                    rejection.SourceHash,
                    rejection.OriginalSourceHash,
                    StringComparison.Ordinal)
                && string.Equals(rejection.EvidenceMarker, "changed", StringComparison.Ordinal),
            "immutable_source_missing" =>
                rejection.SourceLength is null
                && rejection.SourceHash is null
                && rejection.RetentionKey is null
                && IsHash(rejection.OriginalSourceHash)
                && string.Equals(rejection.EvidenceMarker, "missing", StringComparison.Ordinal),
            _ => false
        };
        if (!message.MimeContent.IsEmpty || !valid)
        {
            throw new InvalidDataException(
                "The approved inbox source returned an invalid rejection descriptor.");
        }
    }

    private static bool IsHash(string? value) =>
        value is { Length: 64 }
        && value.All(char.IsAsciiHexDigit);

    private static bool IsStorageKey(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 200;

    private static string CreateOccurrenceKey(string nextCursor) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(nextCursor)));

    private static string CreateOperationKey(string externalReceiptToken)
    {
        Span<byte> identityBytes = stackalloc byte[Encoding.UTF8.GetByteCount(externalReceiptToken)];
        Encoding.UTF8.GetBytes(externalReceiptToken, identityBytes);
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(identityBytes, hash);
        return $"mailbox:{Convert.ToHexString(hash)}";
    }

    private static string FailureCode(Exception exception) => exception switch
    {
        IntakeArtifactRetentionException => "artifact_retention_failure",
        IntakeSourceIdentityConflictException => "source_identity_conflict",
        ApprovedMailboxAccessDeniedException => "mailbox_access_denied",
        InvalidDataException or ArgumentException => "invalid_mailbox_source",
        _ => "mailbox_poll_failure"
    };

    private readonly record struct PreparedMessage(
        IntakeSource Source,
        string ExternalReceiptToken,
        RetainedMailboxMessage? RetainedMessage);

    private sealed class MalformedApprovedInboxMessageException(
        string failureCode,
        string message,
        Exception? innerException = null)
        : Exception(message, innerException)
    {
        public string FailureCode { get; } = failureCode;
    }
}
