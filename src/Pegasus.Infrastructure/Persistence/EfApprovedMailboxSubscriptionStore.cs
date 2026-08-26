using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfApprovedMailboxSubscriptionStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IApprovedMailboxSubscriptionStore
{
    public async Task<ApprovedMailboxSubscription?> GetActiveAsync(
        string subscriptionId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var active = ApprovedMailboxSubscriptionLifecycleState.Active.ToString();
        var approved = ApprovedMailboxState.Approved.ToString();
        var entity = await context.ApprovedMailboxSubscriptions
            .AsNoTracking()
            .Include(item => item.ApprovedMailbox)
            .SingleOrDefaultAsync(item =>
                item.SubscriptionId == subscriptionId
                && item.LifecycleState == active
                && item.ExpiresAtUtc > nowUtc
                && item.ApprovedMailbox.State == approved
                && item.ApprovedMailbox.AllowInboundIntake
                && item.ApprovedMailbox.ActivatedAtUtc != null,
                cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<ApprovedMailboxSubscriptionMaintenanceCandidate>>
        ListMaintenanceCandidatesAsync(
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var active = ApprovedMailboxSubscriptionLifecycleState.Active.ToString();
        var approved = ApprovedMailboxState.Approved.ToString();
        var maintenanceDueBeforeUtc = nowUtc.AddHours(-6);
        var candidates = await context.ApprovedMailboxes
            .AsNoTracking()
            .Where(mailbox =>
                mailbox.State == approved
                && mailbox.AllowInboundIntake
                && mailbox.ActivatedAtUtc != null
                && mailbox.MailboxIdentity != null
                && mailbox.InboxFolderIdentity != null)
            .GroupJoin(
                context.ApprovedMailboxSubscriptions.AsNoTracking(),
                mailbox => mailbox.Id,
                subscription => subscription.ApprovedMailboxId,
                (mailbox, subscriptions) => new { mailbox, subscription = subscriptions.SingleOrDefault() })
            .Where(item => item.subscription == null
                || item.subscription.LifecycleState != active
                || item.subscription.LastMaintainedAtUtc == null
                || item.subscription.LastMaintainedAtUtc <= maintenanceDueBeforeUtc)
            .OrderBy(item => item.mailbox.Id)
            .ToListAsync(cancellationToken);
        return candidates.Select(item =>
            new ApprovedMailboxSubscriptionMaintenanceCandidate(
                item.mailbox.Id,
                item.mailbox.MailboxIdentity!,
                item.mailbox.InboxFolderIdentity!,
                item.subscription is null ? null : Map(item.subscription)))
            .ToArray();
    }

    public async Task SaveAsync(
        ApprovedMailboxSubscription subscription,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        Validate(subscription);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.ApprovedMailboxSubscriptions.SingleOrDefaultAsync(
            item => item.ApprovedMailboxId == subscription.ApprovedMailboxId,
            cancellationToken);
        if (entity is null)
        {
            entity = new ApprovedMailboxSubscriptionEntity
            {
                ApprovedMailboxId = subscription.ApprovedMailboxId,
                SubscriptionId = subscription.SubscriptionId,
                Resource = subscription.Resource,
                LifecycleState = subscription.LifecycleState.ToString()
            };
            context.ApprovedMailboxSubscriptions.Add(entity);
        }
        entity.SubscriptionId = subscription.SubscriptionId;
        entity.Resource = subscription.Resource;
        entity.ExpiresAtUtc = subscription.ExpiresAtUtc;
        entity.LifecycleState = subscription.LifecycleState.ToString();
        entity.LastMaintainedAtUtc = subscription.LastMaintainedAtUtc;
        entity.LastMaintenanceFailureCode = subscription.LastMaintenanceFailureCode;
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordMaintenanceFailureAsync(
        Guid approvedMailboxId,
        string failureCode,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        if (approvedMailboxId == Guid.Empty || failureCode.Length > 100)
        {
            throw new ArgumentException("Valid mailbox and failure identities are required.");
        }
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.ApprovedMailboxSubscriptions.SingleOrDefaultAsync(
            item => item.ApprovedMailboxId == approvedMailboxId,
            cancellationToken);
        if (entity is null)
        {
            return;
        }
        entity.LastMaintainedAtUtc = attemptedAtUtc;
        entity.LastMaintenanceFailureCode = failureCode;
        await context.SaveChangesAsync(cancellationToken);
    }

    private static ApprovedMailboxSubscription Map(ApprovedMailboxSubscriptionEntity entity) => new(
        entity.ApprovedMailboxId,
        entity.SubscriptionId,
        entity.Resource,
        entity.ExpiresAtUtc,
        Enum.Parse<ApprovedMailboxSubscriptionLifecycleState>(entity.LifecycleState),
        entity.LastMaintainedAtUtc,
        entity.LastMaintenanceFailureCode);

    private static void Validate(ApprovedMailboxSubscription subscription)
    {
        if (subscription.ApprovedMailboxId == Guid.Empty
            || string.IsNullOrWhiteSpace(subscription.SubscriptionId)
            || subscription.SubscriptionId.Length > 200
            || string.IsNullOrWhiteSpace(subscription.Resource)
            || subscription.Resource.Length > 500
            || !Enum.IsDefined(subscription.LifecycleState)
            || subscription.LastMaintenanceFailureCode?.Length > 100)
        {
            throw new ArgumentException("The approved mailbox subscription is invalid.", nameof(subscription));
        }
    }
}
