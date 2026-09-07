using System.Data;
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
                && item.ApprovedMailbox.ActivatedAtUtc != null
                && item.Generation == item.ApprovedMailbox.MailboxGeneration,
                cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<ApprovedMailboxSubscription>> ListAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context.ApprovedMailboxSubscriptions
            .AsNoTracking()
            .OrderBy(item => item.ApprovedMailboxId)
            .ToListAsync(cancellationToken);
        return entities.Select(Map).ToArray();
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
                || item.subscription.Generation != item.mailbox.MailboxGeneration
                || item.subscription.LastMaintainedAtUtc == null
                || item.subscription.LastMaintainedAtUtc <= maintenanceDueBeforeUtc)
            .OrderBy(item => item.mailbox.Id)
            .ToListAsync(cancellationToken);
        return candidates.Select(item =>
            new ApprovedMailboxSubscriptionMaintenanceCandidate(
                item.mailbox.Id,
                item.mailbox.MailboxIdentity!,
                item.mailbox.InboxFolderIdentity!,
                item.subscription is null ? null : Map(item.subscription),
                item.mailbox.MailboxGeneration))
            .ToArray();
    }

    public async Task SaveAsync(
        ApprovedMailboxSubscription subscription,
        string? expectedPriorSubscriptionId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        Validate(subscription);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var approved = ApprovedMailboxState.Approved.ToString();
        var mailbox = await context.ApprovedMailboxes.SingleOrDefaultAsync(
            item => item.Id == subscription.ApprovedMailboxId,
            cancellationToken);
        var entity = await context.ApprovedMailboxSubscriptions.SingleOrDefaultAsync(
            item => item.ApprovedMailboxId == subscription.ApprovedMailboxId,
            cancellationToken);
        if (mailbox is null
            || mailbox.State != approved
            || !mailbox.AllowInboundIntake
            || mailbox.ActivatedAtUtc is null
            || mailbox.MailboxGeneration != subscription.Generation
            || !string.Equals(
                entity?.SubscriptionId,
                expectedPriorSubscriptionId,
                StringComparison.Ordinal))
        {
            throw new ApprovedMailboxSubscriptionMaintenanceLostException();
        }
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
        entity.Generation = subscription.Generation;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordMaintenanceFailureAsync(
        Guid approvedMailboxId,
        long expectedGeneration,
        string? expectedSubscriptionId,
        string failureCode,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);
        if (approvedMailboxId == Guid.Empty
            || expectedGeneration <= 0
            || expectedSubscriptionId?.Length > 200
            || failureCode.Length > 100)
        {
            throw new ArgumentException("Valid mailbox and failure identities are required.");
        }
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var approved = ApprovedMailboxState.Approved.ToString();
        var mailbox = await context.ApprovedMailboxes.SingleOrDefaultAsync(
            item => item.Id == approvedMailboxId,
            cancellationToken);
        var entity = await context.ApprovedMailboxSubscriptions.SingleOrDefaultAsync(
            item => item.ApprovedMailboxId == approvedMailboxId,
            cancellationToken);
        if (mailbox is null
            || mailbox.State != approved
            || !mailbox.AllowInboundIntake
            || mailbox.ActivatedAtUtc is null
            || mailbox.MailboxGeneration != expectedGeneration
            || entity?.Generation != expectedGeneration
            || !string.Equals(
                entity.SubscriptionId,
                expectedSubscriptionId,
                StringComparison.Ordinal))
        {
            throw new ApprovedMailboxSubscriptionMaintenanceLostException();
        }
        entity.LastMaintainedAtUtc = attemptedAtUtc;
        entity.LastMaintenanceFailureCode = failureCode;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static ApprovedMailboxSubscription Map(ApprovedMailboxSubscriptionEntity entity) => new(
        entity.ApprovedMailboxId,
        entity.SubscriptionId,
        entity.Resource,
        entity.ExpiresAtUtc,
        Enum.Parse<ApprovedMailboxSubscriptionLifecycleState>(entity.LifecycleState),
        entity.LastMaintainedAtUtc,
        entity.LastMaintenanceFailureCode,
        entity.Generation);

    private static void Validate(ApprovedMailboxSubscription subscription)
    {
        if (subscription.ApprovedMailboxId == Guid.Empty
            || string.IsNullOrWhiteSpace(subscription.SubscriptionId)
            || subscription.SubscriptionId.Length > 200
            || string.IsNullOrWhiteSpace(subscription.Resource)
            || subscription.Resource.Length > 500
            || !Enum.IsDefined(subscription.LifecycleState)
            || subscription.Generation <= 0
            || subscription.LastMaintenanceFailureCode?.Length > 100)
        {
            throw new ArgumentException("The approved mailbox subscription is invalid.", nameof(subscription));
        }
    }
}
