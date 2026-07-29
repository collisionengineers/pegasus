using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Intake;

public sealed record ApprovedInboxPollLease(
    string MailboxId,
    string MailboxAddress,
    string? Cursor,
    string LeaseToken);

public sealed record ApprovedInboxMessage(
    string ImmutableMessageId,
    string FileName,
    ReadOnlyMemory<byte> MimeContent,
    DateTimeOffset ReceivedAtUtc);

public sealed record ApprovedInboxPage(
    IReadOnlyList<ApprovedInboxMessage> Messages,
    string NextCursor);

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
    IApprovedInboxPollStore pollStore,
    IApprovedInboxSource inboxSource,
    ReceiveIntake receiveIntake,
    TimeProvider timeProvider)
{
    private const int MaximumSourceLength = 10 * 1024 * 1024;
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

            foreach (var message in page.Messages)
            {
                var externalReceiptToken = CreateExternalReceiptToken(lease.MailboxId, message);
                await receiveIntake.ExecuteAsync(
                    new(
                        Path.GetFileName(message.FileName),
                        "message/rfc822",
                        message.MimeContent,
                        message.ReceivedAtUtc,
                        actorCode,
                        new(IntakeSourceChannel.Mailbox, externalReceiptToken)),
                    CreateOperationKey(externalReceiptToken),
                    cancellationToken);
            }

            await pollStore.CompleteAsync(
                lease.MailboxId,
                lease.LeaseToken,
                page.NextCursor,
                timeProvider.GetUtcNow(),
                cancellationToken);
            return page.Messages.Count;
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

    private static void ValidateLease(ApprovedInboxPollLease lease)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lease.MailboxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(lease.MailboxAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(lease.LeaseToken);
    }

    private static string CreateExternalReceiptToken(
        string mailboxId,
        ApprovedInboxMessage message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message.ImmutableMessageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.FileName);
        if (message.MimeContent.IsEmpty)
        {
            throw new InvalidDataException("The approved inbox message is empty.");
        }

        if (message.MimeContent.Length > MaximumSourceLength)
        {
            throw new InvalidDataException("The approved inbox message exceeds the 10 MB intake limit.");
        }

        var token = $"{mailboxId.Length}:{mailboxId}{message.ImmutableMessageId}";
        if (token.Length > MaximumExternalReceiptTokenLength)
        {
            throw new InvalidDataException("The approved inbox message identity exceeds the supported length.");
        }

        return token;
    }

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
}
