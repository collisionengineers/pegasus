using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

public sealed record ApprovedInboxPollLease(
    string MailboxId,
    string MailboxAddress,
    string? Cursor,
    string LeaseToken);

public sealed record ApprovedInboxSourceRejection(
    string FailureCode,
    long? SourceLength,
    string? SourceHash,
    string? RetentionKey,
    string? OriginalSourceHash = null,
    string? EvidenceMarker = null);

public sealed record ApprovedInboxMessage(
    string ImmutableMessageId,
    string FileName,
    ReadOnlyMemory<byte> MimeContent,
    DateTimeOffset ReceivedAtUtc,
    string NextCursor)
{
    public ApprovedInboxSourceRejection? SourceRejection { get; init; }
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

/// <param name="maximumContentLength">
/// The size of one received message this mailbox accepts, envelope and
/// attachments together. It is a parameter only so that a test can exercise
/// both sides of the boundary; nothing configures it.
/// </param>
public sealed class PollApprovedInbox(
    IApprovedInboxPollStore pollStore,
    IApprovedInboxSource inboxSource,
    IIntakeArtifactStore artifactStore,
    IIntakeQuarantineArtifactStore quarantineArtifactStore,
    ReceiveIntake receiveIntake,
    TimeProvider timeProvider,
    long maximumContentLength = IntakeEnvelopeLimits.MaximumMailboxContentLength)
{
    private const int MaximumFileNameLength = 260;
    private const int MaximumExternalReceiptTokenLength = 200;
    private const int MaximumActorLength = 200;
    private const string SystemWorkerActorPrefix = "system-worker:";
    private static readonly TimeSpan PollLeaseDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan FailureRetryDelay = TimeSpan.FromSeconds(30);

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

        var lease = await pollStore.ClaimAsync(
            timeProvider.GetUtcNow(),
            PollLeaseDuration,
            cancellationToken);
        if (lease is null)
        {
            return 0;
        }

        ValidateLease(lease);
        try
        {
            var page = await inboxSource.ReadAsync(lease, maximumMessages, cancellationToken);
            ValidatePage(page, maximumMessages);

            var handledMessages = 0;
            foreach (var message in page.Messages)
            {
                PreparedMessage prepared;
                try
                {
                    prepared = PrepareMessage(lease.MailboxId, actorCode, message, maximumContentLength);
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
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            await pollStore.ReleaseAsync(
                lease.MailboxId,
                lease.LeaseToken,
                timeProvider.GetUtcNow().Add(FailureRetryDelay),
                FailureCode(exception),
                cancellationToken);
            throw;
        }
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
            ValidateSourceRejection(message, rejection, maximumContentLength);
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

    private static void ValidateLease(ApprovedInboxPollLease lease)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lease.MailboxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(lease.MailboxAddress);
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
        string mailboxId,
        string actorCode,
        ApprovedInboxMessage message,
        long maximumContentLength)
    {
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
            ValidateSourceRejection(message, rejection, maximumContentLength);
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

        if (message.MimeContent.Length > maximumContentLength)
        {
            throw new MalformedApprovedInboxMessageException(
                "message_too_large",
                "The approved inbox message exceeds the mailbox intake limit.");
        }

        var externalReceiptToken = $"{mailboxId.Length}:{mailboxId}{message.ImmutableMessageId}";
        if (externalReceiptToken.Length > MaximumExternalReceiptTokenLength)
        {
            throw new MalformedApprovedInboxMessageException(
                "message_identity_too_long",
                "The approved inbox message identity exceeds the supported length.");
        }

        return new(
            new(
                safeFileName,
                "message/rfc822",
                message.MimeContent,
                message.ReceivedAtUtc,
                actorCode,
                new(IntakeSourceChannel.Mailbox, externalReceiptToken)),
            externalReceiptToken);
    }

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
        ApprovedInboxSourceRejection rejection,
        long maximumContentLength)
    {
        var valid = rejection.FailureCode switch
        {
            "message_too_large" =>
                rejection.SourceLength > maximumContentLength
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
        InvalidDataException or ArgumentException => "invalid_mailbox_source",
        _ => "mailbox_poll_failure"
    };

    private readonly record struct PreparedMessage(
        IntakeSource Source,
        string ExternalReceiptToken);

    private sealed class MalformedApprovedInboxMessageException(
        string failureCode,
        string message,
        Exception? innerException = null)
        : Exception(message, innerException)
    {
        public string FailureCode { get; } = failureCode;
    }
}
