using System.Data;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;
using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfApprovedInboxPollStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    LocalApprovedInboxOptions options) : IApprovedInboxPollStore
{
    public async Task<ApprovedInboxPollLease?> ClaimAsync(
        DateTimeOffset nowUtc,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            leaseDuration,
            TimeSpan.Zero);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var state = await context.ApprovedInboxPollStates.SingleOrDefaultAsync(
            item => item.MailboxId == options.MailboxId,
            cancellationToken);
        if (state is null)
        {
            state = new()
            {
                MailboxId = options.MailboxId,
                MailboxAddress = options.MailboxAddress,
                DueAtUtc = nowUtc
            };
            context.ApprovedInboxPollStates.Add(state);
        }
        else if (!string.Equals(
                     state.MailboxAddress,
                     options.MailboxAddress,
                     StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The configured approved mailbox identity is already bound to another address.");
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
            state.MailboxId,
            state.MailboxAddress,
            state.Cursor,
            state.LeaseToken);
    }

    public async Task CompleteAsync(
        string mailboxId,
        string leaseToken,
        string nextCursor,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(mailboxId, leaseToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(nextCursor);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var state = await context.ApprovedInboxPollStates.SingleOrDefaultAsync(
            item => item.MailboxId == mailboxId && item.LeaseToken == leaseToken,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The approved-inbox lease was lost before completion.");
        state.Cursor = nextCursor;
        state.DueAtUtc = completedAtUtc;
        state.LeaseToken = null;
        state.LeaseExpiresAtUtc = null;
        state.LastCompletedAtUtc = completedAtUtc;
        state.LastFailureCode = null;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReleaseAsync(
        string mailboxId,
        string leaseToken,
        DateTimeOffset dueAtUtc,
        string failureCode,
        CancellationToken cancellationToken)
    {
        ValidateIdentity(mailboxId, leaseToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        if (failureCode.Length > 100)
        {
            throw new ArgumentException(
                "The approved-inbox failure code must be 100 characters or fewer.",
                nameof(failureCode));
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var state = await context.ApprovedInboxPollStates.SingleOrDefaultAsync(
            item => item.MailboxId == mailboxId && item.LeaseToken == leaseToken,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The approved-inbox lease was lost before release.");
        state.DueAtUtc = dueAtUtc;
        state.LeaseToken = null;
        state.LeaseExpiresAtUtc = null;
        state.LastFailureCode = failureCode;
        await context.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateIdentity(string mailboxId, string leaseToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mailboxId);
        ArgumentException.ThrowIfNullOrWhiteSpace(leaseToken);
    }
}
