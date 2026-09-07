using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One Approved inbound-intake row exactly as stored, identities possibly absent.
/// </summary>
internal sealed record ApprovedIntakeMailboxCandidate(
    Guid Id,
    string Address,
    string? MailboxIdentity,
    string? InboxFolderIdentity,
    DateTimeOffset? ActivatedAtUtc,
    long Generation);

public sealed class EfApprovedMailboxStore(
    IDbContextFactory<PegasusDbContext> contextFactory,
    TimeProvider timeProvider) : IApprovedMailboxStore, IApprovedIntakeMailboxes
{
    private const string AggregateType = "approved_mailbox";
    private const string EventKind = "approved_mailbox_updated";

    /// <summary>
    /// The raw estate view: only rows that are Approved, scoped to inbound intake, and
    /// fully identified. Rows still awaiting their tenant identities are absent, so the
    /// caller cannot poll a mailbox nobody has identified. Ordered by address so a tick
    /// visits the estate in the same order every time.
    /// </summary>
    public async Task<IReadOnlyList<ApprovedIntakeMailbox>> ListPollableAsync(
        CancellationToken cancellationToken)
    {
        var candidates = await ListInboundIntakeCandidatesAsync(cancellationToken);
        return candidates
            .Where(item => item.MailboxIdentity is not null
                && item.InboxFolderIdentity is not null
                && item.ActivatedAtUtc is not null)
            .Select(item => new ApprovedIntakeMailbox(
                item.Id,
                item.MailboxIdentity!,
                item.Address,
                item.InboxFolderIdentity!,
                item.ActivatedAtUtc!.Value,
                item.Generation))
            .ToArray();
    }

    public async Task<ApprovedIntakeMailbox?> GetPollableAsync(
        Guid approvedMailboxId,
        CancellationToken cancellationToken)
    {
        if (approvedMailboxId == Guid.Empty)
        {
            return null;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var approvedState = ApprovedMailboxState.Approved.ToString();
        var candidate = await context.Set<ApprovedMailboxEntity>()
            .AsNoTracking()
            .Where(item => item.Id == approvedMailboxId
                && item.State == approvedState
                && item.AllowInboundIntake)
            .Select(item => new ApprovedIntakeMailboxCandidate(
                item.Id,
                item.Address,
                item.MailboxIdentity,
                item.InboxFolderIdentity,
                item.ActivatedAtUtc,
                item.MailboxGeneration))
            .SingleOrDefaultAsync(cancellationToken);
        return candidate is { MailboxIdentity: not null, InboxFolderIdentity: not null, ActivatedAtUtc: not null }
            ? new(candidate.Id, candidate.MailboxIdentity, candidate.Address, candidate.InboxFolderIdentity, candidate.ActivatedAtUtc.Value, candidate.Generation)
            : null;
    }

    /// <summary>
    /// Every Approved inbound-intake row. Ordered by address, so a recovery tick visits
    /// the estate in the same order every time.
    /// </summary>
    internal async Task<IReadOnlyList<ApprovedIntakeMailboxCandidate>>
        ListInboundIntakeCandidatesAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var approvedState = ApprovedMailboxState.Approved.ToString();
        return await context.Set<ApprovedMailboxEntity>()
            .AsNoTracking()
            .Where(item => item.State == approvedState && item.AllowInboundIntake)
            .OrderBy(item => item.Address)
            .ThenBy(item => item.Id)
            .Select(item => new ApprovedIntakeMailboxCandidate(
                item.Id,
                item.Address,
                item.MailboxIdentity,
                item.InboxFolderIdentity,
                item.ActivatedAtUtc,
                item.MailboxGeneration))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApprovedMailbox>> ListAsync(
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context.Set<ApprovedMailboxEntity>()
            .AsNoTracking()
            .Include(item => item.FolderBindings)
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
                    && (routeScope == ApprovedMailboxRouteScope.InboundIntake && item.AllowInboundIntake
                        || routeScope == ApprovedMailboxRouteScope.SentEvidence && item.AllowSentEvidence
                        || routeScope == ApprovedMailboxRouteScope.StaffSend && item.AllowStaffSend),
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
            .Include(item => item.FolderBindings)
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
                MailboxIdentity = request.MailboxIdentity,
                InboxFolderIdentity = request.InboxFolderIdentity,
                SentFolderIdentity = request.SentFolderIdentity,
                ActivatedAtUtc = request.State == ApprovedMailboxState.Approved
                    ? timeProvider.GetUtcNow()
                    : null,
                MailboxGeneration = request.State == ApprovedMailboxState.Approved ? 1 : 0,
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
            if (before.State == ApprovedMailboxState.Approved
                && request.State == ApprovedMailboxState.Disabled)
            {
                entity.MailboxGeneration = checked(entity.MailboxGeneration + 1);
            }
            if (request.State == ApprovedMailboxState.Approved
                && (before.State == ApprovedMailboxState.Disabled || entity.ActivatedAtUtc is null))
            {
                entity.ActivatedAtUtc = timeProvider.GetUtcNow();
                entity.MailboxGeneration = checked(entity.MailboxGeneration + 1);
            }
            var mayReplaceCoordinates = before.State == ApprovedMailboxState.Disabled
                && request.State == ApprovedMailboxState.Disabled;
            if (!mayReplaceCoordinates && before.IdentityIsBound
                && (!string.Equals(entity.Address, request.Address, StringComparison.Ordinal)
                    || IsDifferentIdentity(entity.MailboxIdentity, request.MailboxIdentity)
                    || IsDifferentIdentity(entity.InboxFolderIdentity, request.InboxFolderIdentity)
                    || IsDifferentIdentity(entity.SentFolderIdentity, request.SentFolderIdentity)))
            {
                throw new ApprovedMailboxUpdateException(
                    ApprovedMailboxUpdateError.MailboxIdentityImmutable);
            }

            entity.MailboxIdentity = request.MailboxIdentity ?? entity.MailboxIdentity;
            entity.InboxFolderIdentity = request.InboxFolderIdentity ?? entity.InboxFolderIdentity;
            entity.SentFolderIdentity = request.SentFolderIdentity ?? entity.SentFolderIdentity;

            entity.Address = request.Address;
            entity.State = request.State.ToString();
            entity.Version = checked(entity.Version + 1);
        }

        if (request.FolderBindings is not null)
        {
            ReplaceFolderBindings(entity, request.FolderBindings);
        }

        if (await context.Set<ApprovedMailboxEntity>()
            .AsNoTracking()
            .AnyAsync(
                item => item.Address == request.Address && item.Id != request.MailboxId,
                cancellationToken))
        {
            throw new ApprovedMailboxUpdateException(ApprovedMailboxUpdateError.DuplicateAddress);
        }

        var mailboxIdentity = entity.MailboxIdentity;
        if (mailboxIdentity is not null
            && await context.Set<ApprovedMailboxEntity>()
                .AsNoTracking()
                .AnyAsync(
                    item => item.MailboxIdentity == mailboxIdentity
                        && item.Id != request.MailboxId,
                    cancellationToken))
        {
            throw new ApprovedMailboxUpdateException(
                ApprovedMailboxUpdateError.DuplicateMailboxIdentity);
        }

        entity.AllowInboundIntake =
            request.RouteScopes.Contains(ApprovedMailboxRouteScope.InboundIntake);
        entity.AllowSentEvidence =
            request.RouteScopes.Contains(ApprovedMailboxRouteScope.SentEvidence);
        entity.AllowStaffSend =
            request.RouteScopes.Contains(ApprovedMailboxRouteScope.StaffSend);
        if (request.VerifiedEncodedMessageSizeLimit is { } verifiedLimit
            && entity.VerifiedEncodedMessageSizeLimit != verifiedLimit)
        {
            entity.VerifiedEncodedMessageSizeLimit = verifiedLimit;
            entity.SendLimitVerifiedAtUtc = timeProvider.GetUtcNow();
            entity.SendLimitVerifiedBy = request.Actor.SubjectId;
        }
        if (before is { State: ApprovedMailboxState.Approved }
            && request.State == ApprovedMailboxState.Approved
            && !before.RouteScopes.SequenceEqual(Routes(entity)))
        {
            entity.MailboxGeneration = checked(entity.MailboxGeneration + 1);
            entity.ActivatedAtUtc = timeProvider.GetUtcNow();
        }
        if (before is not null && before.Generation != entity.MailboxGeneration)
        {
            var boundary = request.State == ApprovedMailboxState.Approved
                ? entity.ActivatedAtUtc
                    ?? throw new InvalidOperationException("An active mailbox generation requires a start boundary.")
                : timeProvider.GetUtcNow();
            var inboxState = await context.ApprovedInboxPollStates.SingleOrDefaultAsync(
                item => item.ApprovedMailboxId == entity.Id,
                cancellationToken);
            if (inboxState is not null)
            {
                inboxState.Generation = entity.MailboxGeneration;
                inboxState.StartBoundaryUtc = boundary;
                inboxState.ActivatedAtUtc = boundary;
                inboxState.Cursor = null;
                inboxState.DueAtUtc = boundary;
                inboxState.LeaseToken = null;
                inboxState.LeaseExpiresAtUtc = null;
                inboxState.LastCompletedAtUtc = null;
                inboxState.LastFailureCode = null;
            }
            if (entity.MailboxIdentity is { } graphMailboxId)
            {
                var sentState = await context.ApprovedSentPollStates.SingleOrDefaultAsync(
                    item => item.MailboxId == graphMailboxId,
                    cancellationToken);
                if (sentState is not null)
                {
                    sentState.Generation = entity.MailboxGeneration;
                    sentState.StartBoundaryUtc = boundary;
                    sentState.Cursor = null;
                    sentState.DueAtUtc = boundary;
                    sentState.LeaseToken = null;
                    sentState.LeaseExpiresAtUtc = null;
                    sentState.LastCompletedAtUtc = null;
                    sentState.LastFailureCode = null;
                }
            }
        }
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
            // The recorded identities are part of what the operation did, so a replay
            // that presents different ones is a different operation, not a repeat.
            || !IdentityMatchesReplay(snapshot.MailboxIdentity, request.MailboxIdentity)
            || !IdentityMatchesReplay(snapshot.InboxFolderIdentity, request.InboxFolderIdentity)
            || !IdentityMatchesReplay(snapshot.SentFolderIdentity, request.SentFolderIdentity)
            || !FolderBindingsMatchReplay(snapshot.FolderBindings, request.FolderBindings)
            || !snapshot.RouteScopes.OrderBy(scope => scope).SequenceEqual(requestedRoutes))
        {
            throw new ApprovedMailboxUpdateException(
                ApprovedMailboxUpdateError.OperationConflict);
        }

        return Map(snapshot);
    }

    /// <summary>
    /// A replayed request may omit an identity the recorded operation had already bound,
    /// exactly as a live update may; a differing non-null value is a conflict.
    /// </summary>
    private static bool IdentityMatchesReplay(string? recorded, string? presented) =>
        presented is null || string.Equals(recorded, presented, StringComparison.Ordinal);

    private static bool FolderBindingsMatchReplay(
        IReadOnlyList<ApprovedMailboxFolderBinding> recorded,
        IReadOnlyCollection<ApprovedMailboxFolderBinding>? presented) =>
        presented is null
        || recorded.SequenceEqual(presented.OrderBy(item => item.FolderType));

    private static bool IsDifferentIdentity(string? current, string? requested) =>
        requested is not null && !string.Equals(current, requested, StringComparison.Ordinal);

    private static void ReplaceFolderBindings(
        ApprovedMailboxEntity entity,
        IReadOnlyCollection<ApprovedMailboxFolderBinding> bindings)
    {
        var requested = bindings.ToDictionary(item => item.FolderType.ToString(), StringComparer.Ordinal);
        foreach (var existing in entity.FolderBindings.ToArray())
        {
            if (requested.Remove(existing.FolderType, out var binding))
            {
                existing.FolderIdentity = binding.FolderIdentity;
            }
            else
            {
                entity.FolderBindings.Remove(existing);
            }
        }

        foreach (var binding in requested.Values)
        {
            entity.FolderBindings.Add(new ApprovedMailboxFolderBindingEntity
            {
                ApprovedMailboxId = entity.Id,
                FolderType = binding.FolderType.ToString(),
                FolderIdentity = binding.FolderIdentity
            });
        }
    }

    private static MailboxSnapshot Snapshot(ApprovedMailboxEntity entity) => new(
        entity.Id,
        entity.Address,
        Routes(entity),
        ParseState(entity.State),
        entity.MailboxIdentity,
        entity.InboxFolderIdentity,
        entity.SentFolderIdentity,
        entity.ActivatedAtUtc,
        entity.MailboxGeneration,
        entity.Version,
        entity.VerifiedEncodedMessageSizeLimit,
        entity.FolderBindings
            .Select(item => new ApprovedMailboxFolderBinding(
                ParseFolderType(item.FolderType),
                item.FolderIdentity))
            .OrderBy(item => item.FolderType)
            .ToArray());

    private static ApprovedMailbox Map(ApprovedMailboxEntity entity) => Map(Snapshot(entity));

    private static ApprovedMailbox Map(MailboxSnapshot snapshot) => new(
        snapshot.Id,
        snapshot.Address,
        snapshot.RouteScopes,
        snapshot.State,
        snapshot.MailboxIdentity,
        snapshot.InboxFolderIdentity,
        snapshot.SentFolderIdentity,
        snapshot.IdentityIsBound,
        snapshot.ActivatedAtUtc,
        snapshot.Version,
        snapshot.FolderBindings,
        snapshot.Generation,
        snapshot.VerifiedEncodedMessageSizeLimit);

    private static ApprovedMailboxRouteScope[] Routes(ApprovedMailboxEntity entity)
    {
        var routes = new List<ApprovedMailboxRouteScope>(3);
        if (entity.AllowInboundIntake)
        {
            routes.Add(ApprovedMailboxRouteScope.InboundIntake);
        }
        if (entity.AllowSentEvidence)
        {
            routes.Add(ApprovedMailboxRouteScope.SentEvidence);
        }
        if (entity.AllowStaffSend)
        {
            routes.Add(ApprovedMailboxRouteScope.StaffSend);
        }
        return routes.ToArray();
    }

    private static ApprovedMailboxState ParseState(string value) =>
        Enum.TryParse<ApprovedMailboxState>(value, ignoreCase: false, out var state)
        && Enum.IsDefined(state)
            ? state
            : throw new InvalidOperationException("An approved mailbox has an unknown state.");

    private static MailLogicalFolderType ParseFolderType(string value) =>
        Enum.TryParse<MailLogicalFolderType>(value, ignoreCase: false, out var type)
        && Enum.IsDefined(type)
            ? type
            : throw new InvalidOperationException("An approved mailbox has an unknown logical folder type.");

    private sealed record MailboxSnapshot(
        Guid Id,
        string Address,
        IReadOnlyList<ApprovedMailboxRouteScope> RouteScopes,
        ApprovedMailboxState State,
        string? MailboxIdentity,
        string? InboxFolderIdentity,
        string? SentFolderIdentity,
        DateTimeOffset? ActivatedAtUtc,
        long Generation,
        int Version,
        long? VerifiedEncodedMessageSizeLimit,
        IReadOnlyList<ApprovedMailboxFolderBinding> FolderBindings)
    {
        public bool IdentityIsBound => MailboxIdentity is not null;
    }
}
