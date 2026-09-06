using System.Data;
using System.Security.Cryptography;
using System.Text;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;
using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfApprovedInboxPollStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IApprovedInboxPollStore
{
    public async Task<ApprovedInboxPollLease?> ClaimAsync(
        ApprovedIntakeMailbox mailbox,
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mailbox);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            leaseDuration,
            TimeSpan.Zero);

        var approvedMailboxId = mailbox.ApprovedMailboxId;
        var mailboxAddress = mailbox.Address;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        // Re-assert approval inside the claiming transaction rather than before it, so a
        // disable committed between listing and claiming cannot slip a poll through. A
        // withdrawn mailbox yields no lease; it is not an error.
        var approvedState = ApprovedMailboxState.Approved.ToString();
        var approvedMailbox = await context.Set<ApprovedMailboxEntity>()
            .SingleOrDefaultAsync(
                item => item.Id == approvedMailboxId
                    && item.Address == mailboxAddress
                    && item.State == approvedState
                    && item.AllowInboundIntake,
                cancellationToken);
        if (approvedMailbox?.ActivatedAtUtc is not { } activatedAtUtc
            || activatedAtUtc != mailbox.ActivatedAtUtc)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var state = await context.ApprovedInboxPollStates.SingleOrDefaultAsync(
                item => item.ApprovedMailboxId == approvedMailboxId,
                cancellationToken);
        if (state is null)
        {
            var scopeFingerprint = ScopeFingerprint(mailbox.GraphMailboxId, mailbox.InboxFolderIdentity);
            state = new()
            {
                ApprovedMailboxId = approvedMailboxId,
                MailboxAddress = mailboxAddress,
                ScopeFingerprint = scopeFingerprint,
                Generation = mailbox.Generation,
                ActivatedAtUtc = activatedAtUtc,
                StartBoundaryUtc = activatedAtUtc,
                DueAtUtc = nowUtc
            };
            context.ApprovedInboxPollStates.Add(state);
        }
        else if (!string.Equals(
                     state.MailboxAddress,
                     mailboxAddress,
                     StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The approved mailbox identity is already bound to another address.");
        }

        var currentScopeFingerprint = ScopeFingerprint(
            mailbox.GraphMailboxId,
            mailbox.InboxFolderIdentity);
        if (!string.Equals(state.ScopeFingerprint, currentScopeFingerprint, StringComparison.Ordinal)
            || state.ActivatedAtUtc != activatedAtUtc
            || state.Generation != mailbox.Generation)
        {
            state.ScopeFingerprint = currentScopeFingerprint;
            state.ActivatedAtUtc = activatedAtUtc;
            state.StartBoundaryUtc = activatedAtUtc;
            state.Generation = mailbox.Generation;
            state.Cursor = null;
            state.DueAtUtc = nowUtc;
            state.LeaseToken = null;
            state.LeaseExpiresAtUtc = null;
            state.LastCompletedAtUtc = null;
            state.LastFailureCode = null;
        }

        if ((state.LeaseToken is null) != (state.LeaseExpiresAtUtc is null))
        {
            throw new InvalidDataException("The approved-inbox lease state is inconsistent.");
        }

        if (state.DueAtUtc > nowUtc
            || state.LeaseExpiresAtUtc is { } leaseExpiresAtUtc && leaseExpiresAtUtc > nowUtc)
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        state.LeaseToken = Guid.NewGuid().ToString("N");
        state.LeaseExpiresAtUtc = nowUtc.Add(leaseDuration);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            state.ApprovedMailboxId,
            mailbox.GraphMailboxId,
            state.MailboxAddress,
            mailbox.InboxFolderIdentity,
            activatedAtUtc,
            state.Cursor,
            state.LeaseToken,
            state.StartBoundaryUtc,
            state.Generation);
    }

    public Task AdvanceAsync(
        Guid approvedMailboxId,
        string leaseToken,
        string nextCursor,
        DateTimeOffset advancedAtUtc,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(approvedMailboxId, leaseToken);
        ValidateCursor(nextCursor);
        return UpdateOwnedStateAsync(
            approvedMailboxId,
            leaseToken,
            "advancement",
            state =>
            {
                state.Cursor = nextCursor;
                state.DueAtUtc = advancedAtUtc;
            },
            cancellationToken);
    }

    public async Task QuarantineAsync(
        Guid approvedMailboxId,
        string leaseToken,
        ApprovedInboxPoisonMessage message,
        string nextCursor,
        DateTimeOffset quarantinedAtUtc,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(approvedMailboxId, leaseToken);
        ArgumentNullException.ThrowIfNull(message);
        ValidateCursor(nextCursor);
        ValidatePoisonMessage(message);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var state = await GetOwnedStateAsync(
            context,
            approvedMailboxId,
            leaseToken,
            "quarantine",
            cancellationToken);
        var existing = await context.ApprovedInboxPoisonMessages.SingleOrDefaultAsync(
            item => item.ApprovedMailboxId == approvedMailboxId
                && item.OccurrenceKey == message.OccurrenceKey,
            cancellationToken);
        if (existing is null)
        {
            context.ApprovedInboxPoisonMessages.Add(new()
            {
                Id = Guid.NewGuid(),
                ApprovedMailboxId = approvedMailboxId,
                OccurrenceKey = message.OccurrenceKey,
                ImmutableMessageId = message.ImmutableMessageId,
                FileName = message.FileName,
                SourceLength = message.SourceLength,
                SourceHash = message.SourceHash,
                OriginalSourceHash = message.OriginalSourceHash,
                EvidenceMarker = message.EvidenceMarker,
                StorageKey = message.StorageKey,
                ReceivedAtUtc = message.ReceivedAtUtc,
                FailureCode = message.FailureCode,
                CursorAfterMessage = nextCursor,
                QuarantinedAtUtc = quarantinedAtUtc
            });
        }
        else
        {
            VerifyReplay(existing, message, nextCursor);
        }

        state.Cursor = nextCursor;
        state.DueAtUtc = quarantinedAtUtc;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task CompleteAsync(
        Guid approvedMailboxId,
        string leaseToken,
        string nextCursor,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(approvedMailboxId, leaseToken);
        ValidateCursor(nextCursor);
        return UpdateOwnedStateAsync(
            approvedMailboxId,
            leaseToken,
            "completion",
            state =>
            {
                state.Cursor = nextCursor;
                state.DueAtUtc = completedAtUtc;
                state.LeaseToken = null;
                state.LeaseExpiresAtUtc = null;
                state.LastCompletedAtUtc = completedAtUtc;
                state.LastFailureCode = null;
            },
            cancellationToken);
    }

    public Task ReleaseAsync(
        Guid approvedMailboxId,
        string leaseToken,
        DateTimeOffset dueAtUtc,
        string failureCode,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(approvedMailboxId, leaseToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        if (failureCode.Length > 100)
        {
            throw new ArgumentException(
                "The approved-inbox failure code must be 100 characters or fewer.",
                nameof(failureCode));
        }

        return UpdateOwnedStateAsync(
            approvedMailboxId,
            leaseToken,
            "release",
            state =>
            {
                state.DueAtUtc = dueAtUtc;
                state.LeaseToken = null;
                state.LeaseExpiresAtUtc = null;
                state.LastFailureCode = failureCode;
            },
            cancellationToken);
    }

    private async Task UpdateOwnedStateAsync(
        Guid approvedMailboxId,
        string leaseToken,
        string operation,
        Action<ApprovedInboxPollStateEntity> update,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var state = await GetOwnedStateAsync(
            context,
            approvedMailboxId,
            leaseToken,
            operation,
            cancellationToken);
        update(state);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<ApprovedInboxPollStateEntity> GetOwnedStateAsync(
        PegasusDbContext context,
        Guid approvedMailboxId,
        string leaseToken,
        string operation,
        CancellationToken cancellationToken) =>
        await context.ApprovedInboxPollStates.SingleOrDefaultAsync(
            item => item.ApprovedMailboxId == approvedMailboxId && item.LeaseToken == leaseToken,
            cancellationToken)
        ?? throw new InvalidOperationException(
            $"The approved-inbox lease was lost before {operation}.");

    private static void ValidateIdentity(Guid approvedMailboxId, string leaseToken)
    {
        if (approvedMailboxId == Guid.Empty)
        {
            throw new ArgumentException("The approved mailbox identity is required.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseToken);
    }

    private static string ScopeFingerprint(string graphMailboxId, string inboxFolderIdentity) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{graphMailboxId.Length}:{graphMailboxId}{inboxFolderIdentity}")));

    private static void ValidateCursor(string nextCursor) =>
        ArgumentException.ThrowIfNullOrWhiteSpace(nextCursor);

    private static void ValidatePoisonMessage(ApprovedInboxPoisonMessage message)
    {
        ValidateHash(message.OccurrenceKey, nameof(message.OccurrenceKey));
        ArgumentNullException.ThrowIfNull(message.ImmutableMessageId);
        ArgumentNullException.ThrowIfNull(message.FileName);
        if (message.SourceLength is { } sourceLength)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sourceLength);
        }

        if (message.SourceHash is not null)
        {
            ValidateHash(message.SourceHash, nameof(message.SourceHash));
        }

        if (message.OriginalSourceHash is not null)
        {
            ValidateHash(message.OriginalSourceHash, nameof(message.OriginalSourceHash));
        }

        if (message.StorageKey is { } storageKey && storageKey.Length > 200)
        {
            throw new ArgumentException(
                "The approved-inbox poison storage key must be 200 characters or fewer.",
                nameof(message));
        }

        if (message.EvidenceMarker is { Length: > 50 })
        {
            throw new ArgumentException(
                "The approved-inbox poison evidence marker must be 50 characters or fewer.",
                nameof(message));
        }

        var validEvidence = message.EvidenceMarker switch
        {
            null => message.SourceLength is not null
                && message.SourceHash is not null
                && !string.IsNullOrWhiteSpace(message.StorageKey)
                && message.OriginalSourceHash is null,
            "changed" => message.SourceLength is not null
                && message.SourceHash is not null
                && !string.IsNullOrWhiteSpace(message.StorageKey)
                && message.OriginalSourceHash is not null
                && !string.Equals(
                    message.SourceHash,
                    message.OriginalSourceHash,
                    StringComparison.Ordinal)
                && string.Equals(
                    message.FailureCode,
                    "immutable_source_changed",
                    StringComparison.Ordinal),
            "identity_conflict" => message.SourceLength is not null
                && message.SourceHash is not null
                && !string.IsNullOrWhiteSpace(message.StorageKey)
                && message.OriginalSourceHash is not null
                && !string.Equals(
                    message.SourceHash,
                    message.OriginalSourceHash,
                    StringComparison.Ordinal)
                && string.Equals(
                    message.FailureCode,
                    "source_identity_conflict",
                    StringComparison.Ordinal),
            "missing" => message.SourceLength is null
                && message.SourceHash is null
                && message.StorageKey is null
                && message.OriginalSourceHash is not null
                && string.Equals(
                    message.FailureCode,
                    "immutable_source_missing",
                    StringComparison.Ordinal),
            _ => false
        };
        if (!validEvidence)
        {
            throw new ArgumentException(
                "The approved-inbox poison evidence shape is invalid.",
                nameof(message));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message.FailureCode);
        if (message.FailureCode.Length > 100)
        {
            throw new ArgumentException(
                "The approved-inbox poison failure code must be 100 characters or fewer.",
                nameof(message));
        }
    }

    private static void ValidateHash(string value, string parameterName)
    {
        if (value.Length != 64 || value.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException(
                "The approved-inbox poison hash must contain 64 hexadecimal characters.",
                parameterName);
        }
    }

    private static void VerifyReplay(
        ApprovedInboxPoisonMessageEntity existing,
        ApprovedInboxPoisonMessage message,
        string nextCursor)
    {
        if (!string.Equals(existing.ImmutableMessageId, message.ImmutableMessageId, StringComparison.Ordinal)
            || !string.Equals(existing.FileName, message.FileName, StringComparison.Ordinal)
            || existing.SourceLength != message.SourceLength
            || !string.Equals(existing.SourceHash, message.SourceHash, StringComparison.Ordinal)
            || !string.Equals(
                existing.OriginalSourceHash,
                message.OriginalSourceHash,
                StringComparison.Ordinal)
            || !string.Equals(
                existing.EvidenceMarker,
                message.EvidenceMarker,
                StringComparison.Ordinal)
            || !string.Equals(existing.StorageKey, message.StorageKey, StringComparison.Ordinal)
            || existing.ReceivedAtUtc != message.ReceivedAtUtc
            || !string.Equals(existing.FailureCode, message.FailureCode, StringComparison.Ordinal)
            || !string.Equals(existing.CursorAfterMessage, nextCursor, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "The approved-inbox poison occurrence is already bound to different evidence.");
        }
    }
}
