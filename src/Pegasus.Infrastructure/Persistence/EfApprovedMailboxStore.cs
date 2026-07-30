using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

public sealed class EfApprovedMailboxStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IApprovedMailboxStore
{
    private const string AggregateType = "approved_mailbox";
    private const string EventKind = "approved_mailbox_updated";

    public async Task<IReadOnlyList<ApprovedMailbox>> ListAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context.Set<ApprovedMailboxEntity>()
            .AsNoTracking()
            .OrderBy(item => item.Address)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        return entities.Select(Map).ToArray();
    }

    public async Task<bool> IsApprovedAsync(
        string mailboxAddress,
        ApprovedMailboxRouteScope routeScope,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(routeScope))
        {
            return false;
        }

        string normalizedAddress;
        try
        {
            normalizedAddress = ApprovedMailboxAddress.Normalize(mailboxAddress);
        }
        catch (ArgumentException)
        {
            return false;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var approvedState = ApprovedMailboxState.Approved.ToString();
        return await context.Set<ApprovedMailboxEntity>()
            .AsNoTracking()
            .AnyAsync(
                item => item.Address == normalizedAddress
                    && item.State == approvedState
                    && (routeScope == ApprovedMailboxRouteScope.InboundIntake
                        ? item.AllowInboundIntake
                        : item.AllowSentEvidence),
                cancellationToken);
    }

    public async Task<ApprovedMailbox> UpdateAsync(
        UpdateApprovedMailboxRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var replay = await context.ActionHistory
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.AggregateType == AggregateType
                    && item.CorrelationId == request.OperationKey,
                cancellationToken);
        if (replay is not null)
        {
            var replayed = Replay(request, replay);
            await transaction.CommitAsync(cancellationToken);
            return replayed;
        }

        var entity = await context.Set<ApprovedMailboxEntity>()
            .SingleOrDefaultAsync(item => item.Id == request.MailboxId, cancellationToken);
        MailboxSnapshot? before = null;
        if (request.ExpectedVersion == 0)
        {
            if (entity is not null)
            {
                throw new ApprovedMailboxUpdateException(
                    ApprovedMailboxUpdateError.VersionConflict,
                    entity.Version);
            }

            entity = new ApprovedMailboxEntity
            {
                Id = request.MailboxId,
                Address = request.Address,
                State = request.State.ToString(),
                Version = 1
            };
            context.Set<ApprovedMailboxEntity>().Add(entity);
        }
        else
        {
            if (entity is null)
            {
                throw new ApprovedMailboxUpdateException(ApprovedMailboxUpdateError.NotFound);
            }
            if (entity.Version != request.ExpectedVersion)
            {
                throw new ApprovedMailboxUpdateException(
                    ApprovedMailboxUpdateError.VersionConflict,
                    entity.Version);
            }

            before = Snapshot(entity);
            entity.Address = request.Address;
            entity.State = request.State.ToString();
            entity.Version = checked(entity.Version + 1);
        }

        if (await context.Set<ApprovedMailboxEntity>()
            .AsNoTracking()
            .AnyAsync(
                item => item.Address == request.Address && item.Id != request.MailboxId,
                cancellationToken))
        {
            throw new ApprovedMailboxUpdateException(ApprovedMailboxUpdateError.DuplicateAddress);
        }

        entity.AllowInboundIntake =
            request.RouteScopes.Contains(ApprovedMailboxRouteScope.InboundIntake);
        entity.AllowSentEvidence =
            request.RouteScopes.Contains(ApprovedMailboxRouteScope.SentEvidence);
        var after = Snapshot(entity);
        context.ActionHistory.Add(new ActionHistoryEntity
        {
            Id = Guid.NewGuid(),
            AggregateType = AggregateType,
            AggregateId = entity.Id.ToString("D"),
            EventKind = EventKind,
            ActorKind = request.Actor.Kind.ToString(),
            ActorSubjectId = request.Actor.SubjectId,
            ActorRolesJson = JsonSerializer.Serialize(
                request.Actor.Roles.OrderBy(role => role).Select(role => role.ToString())),
            OccurredAtUtc = timeProvider.GetUtcNow(),
            Outcome = "succeeded",
            CorrelationId = request.OperationKey,
            Reason = request.Reason,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(after),
            PolicyVersion = $"approved-mailbox/{entity.Id:D}/v{entity.Version}"
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(entity);
    }

    private static ApprovedMailbox Replay(
        UpdateApprovedMailboxRequest request,
        ActionHistoryEntity history)
    {
        if (history.AggregateId != request.MailboxId.ToString("D")
            || history.EventKind != EventKind
            || history.ActorKind != request.Actor.Kind.ToString()
            || history.ActorSubjectId != request.Actor.SubjectId
            || history.Reason != request.Reason
            || history.AfterJson is null)
        {
            throw new ApprovedMailboxUpdateException(
                ApprovedMailboxUpdateError.OperationConflict);
        }

        var snapshot = JsonSerializer.Deserialize<MailboxSnapshot>(history.AfterJson);
        var expectedResultVersion = checked(request.ExpectedVersion + 1);
        var requestedRoutes = request.RouteScopes.OrderBy(scope => scope).ToArray();
        if (snapshot is null
            || snapshot.Id != request.MailboxId
            || snapshot.Address != request.Address
            || snapshot.State != request.State
            || snapshot.Version != expectedResultVersion
            || !snapshot.RouteScopes.OrderBy(scope => scope).SequenceEqual(requestedRoutes))
        {
            throw new ApprovedMailboxUpdateException(
                ApprovedMailboxUpdateError.OperationConflict);
        }

        return Map(snapshot);
    }

    private static MailboxSnapshot Snapshot(ApprovedMailboxEntity entity) => new(
        entity.Id,
        entity.Address,
        Routes(entity),
        ParseState(entity.State),
        entity.Version);

    private static ApprovedMailbox Map(ApprovedMailboxEntity entity) => Map(Snapshot(entity));

    private static ApprovedMailbox Map(MailboxSnapshot snapshot) => new(
        snapshot.Id,
        snapshot.Address,
        snapshot.RouteScopes,
        snapshot.State,
        snapshot.Version);

    private static ApprovedMailboxRouteScope[] Routes(ApprovedMailboxEntity entity)
    {
        var routes = new List<ApprovedMailboxRouteScope>(2);
        if (entity.AllowInboundIntake)
        {
            routes.Add(ApprovedMailboxRouteScope.InboundIntake);
        }
        if (entity.AllowSentEvidence)
        {
            routes.Add(ApprovedMailboxRouteScope.SentEvidence);
        }
        return routes.ToArray();
    }

    private static ApprovedMailboxState ParseState(string value) =>
        Enum.TryParse<ApprovedMailboxState>(value, ignoreCase: false, out var state)
        && Enum.IsDefined(state)
            ? state
            : throw new InvalidOperationException("An approved mailbox has an unknown state.");

    private sealed record MailboxSnapshot(
        Guid Id,
        string Address,
        IReadOnlyList<ApprovedMailboxRouteScope> RouteScopes,
        ApprovedMailboxState State,
        int Version);
}
