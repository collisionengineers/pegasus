using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Operations;

/// <summary>
/// Dismiss on the Work Centre (FRD-15, 2 October 2026): any row in Needs
/// attention, New cases or AI jobs can be dismissed. A dismissal belongs to
/// the record behind the row (the Case, Unidentified item or AI job) and hides
/// it for everyone, in every tab. It hides only what the record was showing:
/// a row whose occurrence began after the dismissal (the next chase falls due,
/// the Case is held again or re-enters Review, the Triage or job changes
/// state, the item is reopened) shows again. Nothing on the record changes and
/// there is no undo.
/// </summary>
public sealed record DismissWorkCentreItemRequest(Guid RecordId, ActionActor Actor);

public interface IWorkCentreDismissalStore
{
    /// <summary>
    /// Records the record as dismissed at <paramref name="dismissedAtUtc"/>. A
    /// later dismissal moves the instant on; an earlier one never moves it back.
    /// </summary>
    Task DismissAsync(
        Guid recordId,
        DateTimeOffset dismissedAtUtc,
        ActionActor actor,
        CancellationToken cancellationToken);

    /// <summary>The dismissal instant of each listed record that has one.</summary>
    Task<IReadOnlyDictionary<Guid, DateTimeOffset>> ListAsync(
        IReadOnlyCollection<Guid> recordIds,
        CancellationToken cancellationToken);
}

public interface IDismissWorkCentreItem
{
    Task ExecuteAsync(DismissWorkCentreItemRequest request, CancellationToken cancellationToken);
}

public static class WorkCentreDismissalPolicy
{
    /// <summary>
    /// A row is dismissed when its record was dismissed at or after the row's
    /// occurrence began.
    /// </summary>
    public static bool IsDismissed(DateTimeOffset qualifiedAtUtc, DateTimeOffset? dismissedAtUtc) =>
        dismissedAtUtc is { } dismissed && qualifiedAtUtc <= dismissed;

    /// <summary>
    /// When the job entered the state it shows in: taken, draft written,
    /// closed, or back in the queue when its lease lapsed. A job released back
    /// to the queue records no instant, so its Queued row dates from creation.
    /// </summary>
    public static DateTimeOffset AiJobQualifiedAt(AiJobRecord job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return job.State switch
        {
            AiJobState.Queued => job.LeaseExpiresAtUtc ?? job.CreatedAtUtc,
            AiJobState.Taken => job.TakenAtUtc ?? job.CreatedAtUtc,
            AiJobState.DraftReady => job.DraftReadyAtUtc ?? job.CreatedAtUtc,
            _ => job.ClosedAtUtc ?? job.CreatedAtUtc
        };
    }

    /// <summary>The rows not dismissed, read with one store call (none when there are no rows).</summary>
    public static async Task<IReadOnlyList<T>> WithoutDismissedAsync<T>(
        IWorkCentreDismissalStore store,
        IReadOnlyList<T> rows,
        Func<T, Guid> recordId,
        Func<T, DateTimeOffset> qualifiedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return rows;
        }

        var dismissed = await store.ListAsync(rows.Select(recordId).Distinct().ToArray(), cancellationToken);
        if (dismissed.Count == 0)
        {
            return rows;
        }

        return rows
            .Where(row => !IsDismissed(
                qualifiedAtUtc(row),
                dismissed.TryGetValue(recordId(row), out var at) ? at : null))
            .ToArray();
    }
}

public sealed class DismissWorkCentreItem(
    IWorkCentreDismissalStore store,
    TimeProvider timeProvider) : IDismissWorkCentreItem
{
    private readonly IWorkCentreDismissalStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public Task ExecuteAsync(DismissWorkCentreItemRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        if (request.Actor.Kind != ActorKind.Staff)
        {
            throw new StaffAuthorizationException(StaffAccessRight.PerformCasework);
        }

        if (request.RecordId == Guid.Empty)
        {
            throw new ArgumentException("A record identifier is required.", nameof(request));
        }

        return _store.DismissAsync(request.RecordId, _timeProvider.GetUtcNow(), request.Actor, cancellationToken);
    }
}
