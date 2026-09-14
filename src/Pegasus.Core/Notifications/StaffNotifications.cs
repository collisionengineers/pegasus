using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Notifications;

/// <summary>
/// What raises a personal notification (Work Centre D10). The bell is personal
/// notifications and nothing else: office-wide work never appears in it, and
/// nothing else raises one until decided. Market research raises none.
/// </summary>
public enum StaffNotificationCause
{
    /// <summary>An AI draft is ready on a Case: the Case's engineer, otherwise the person who started the job.</summary>
    AiDraftReady,

    /// <summary>A Case was assigned to an engineer: that engineer.</summary>
    CaseAssigned,

    /// <summary>A Case the engineer is assigned to was edited by someone else.</summary>
    EditedByOther,

    /// <summary>A Case the engineer is assigned to received an e-mail.</summary>
    EmailReceived,

    /// <summary>A post-report query arrived on a Case the engineer is assigned to.</summary>
    QueryReceived
}

/// <summary>
/// One notification for one person. <see cref="Route"/> is the relative application
/// path the shell opens — the Case at its Estimate section, the message, or the
/// Unidentified item, per cause. <see cref="CaseId"/> is null only for an
/// Unidentified resolution draft, whose subject is a U-reference rather than a Case.
/// </summary>
public sealed record StaffNotification(
    Guid Id,
    Guid StaffId,
    Guid? CaseId,
    string Reference,
    string? Registration,
    StaffNotificationCause Cause,
    string Route,
    DateTimeOffset RaisedAtUtc,
    DateTimeOffset? ReadAtUtc)
{
    /// <summary>The person whose act raised it, for "Edited by E Mawdsley"; null when no person did.</summary>
    public string? ActorSubjectId { get; init; }

    public bool IsUnread => ReadAtUtc is null;
}

public sealed record NewStaffNotification(
    Guid StaffId,
    Guid? CaseId,
    string Reference,
    string? Registration,
    StaffNotificationCause Cause,
    string Route,
    string? ActorSubjectId);

public interface IStaffNotificationStore
{
    Task<StaffNotification> AddAsync(NewStaffNotification notification, CancellationToken cancellationToken);

    /// <summary>The person's notifications raised at or after <paramref name="sinceUtc"/>, newest first.</summary>
    Task<IReadOnlyList<StaffNotification>> ListAsync(
        Guid staffId,
        DateTimeOffset sinceUtc,
        CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid staffId, DateTimeOffset sinceUtc, CancellationToken cancellationToken);

    /// <summary>Marks one of the person's notifications read; a notification that is not theirs is left alone.</summary>
    Task<StaffNotification?> MarkReadAsync(Guid staffId, Guid notificationId, DateTimeOffset readAtUtc, CancellationToken cancellationToken);

    Task<int> MarkAllReadAsync(Guid staffId, DateTimeOffset readAtUtc, CancellationToken cancellationToken);

    /// <summary>Removes notifications raised before <paramref name="cutoffUtc"/>; returns how many went.</summary>
    Task<int> PurgeOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken);
}

/// <summary>
/// The one owner of who is told what. Nothing here sends anything: a notification
/// is a row the bell reads.
/// </summary>
public static class StaffNotificationPolicy
{
    /// <summary>Notifications older than this drop off the list and are purged.</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    public const int MaximumRouteLength = 400;

    /// <summary>
    /// Who a notification goes to, or null for nobody. The actor is never told
    /// about their own act. An AI draft goes to the Case's engineer, otherwise to
    /// the staff member who started the job. Assignment goes to the engineer. The
    /// "changed hands" causes go to the Case's engineer only, so a User who created
    /// a Case is never told about it (decided 13 September).
    /// </summary>
    public static Guid? Recipient(
        StaffNotificationCause cause,
        Guid? caseEngineerId,
        Guid? jobStarterStaffId,
        Guid? actorStaffId)
    {
        var recipient = cause switch
        {
            StaffNotificationCause.AiDraftReady => caseEngineerId ?? jobStarterStaffId,
            StaffNotificationCause.CaseAssigned => caseEngineerId,
            StaffNotificationCause.EditedByOther
                or StaffNotificationCause.EmailReceived
                or StaffNotificationCause.QueryReceived => caseEngineerId,
            _ => throw new ArgumentOutOfRangeException(nameof(cause), cause, null)
        };
        return recipient is { } id && id != Guid.Empty && id != actorStaffId ? id : null;
    }

    public static Guid? StaffId(ActionActor? actor) =>
        actor is { Kind: ActorKind.Staff } && Guid.TryParse(actor.SubjectId, out var id) && id != Guid.Empty
            ? id
            : null;

    public static string CaseRoute(Guid caseId, string? section = null) =>
        section is null ? $"/Cases/{caseId:D}" : $"/Cases/{caseId:D}?section={section}";

    public static string MessageRoute(Guid retainedMessageId) => $"/Inbox/{retainedMessageId:D}";

    public static string UnidentifiedRoute(Guid unidentifiedId) => $"/Unidentified/{unidentifiedId:D}";

    /// <summary>
    /// Where an AI draft opens, per kind: Estimate at the Case's Estimate section,
    /// Query response at the message it answers when the job names one, otherwise
    /// the Case's correspondence, Unidentified resolution at the item. Market
    /// research and the queue pass raise nothing (null).
    /// </summary>
    public static string? AiDraftRoute(AiJobRecord job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return job.Kind switch
        {
            AiJobKind.Estimate when job.SubjectId is { } caseId => CaseRoute(caseId, "estimate"),
            AiJobKind.QueryResponse when job.SubjectId is { } caseId =>
                Guid.TryParse(job.ResultReference, out var messageId)
                    ? MessageRoute(messageId)
                    : CaseRoute(caseId, "correspondence"),
            AiJobKind.UnidentifiedResolution when job.SubjectId is { } itemId => UnidentifiedRoute(itemId),
            _ => null
        };
    }

    public static string ValidateRoute(string route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
        var value = route.Trim();
        if (!value.StartsWith('/') || value.StartsWith("//", StringComparison.Ordinal)
            || value.Length > MaximumRouteLength || value.Any(char.IsControl))
        {
            throw new ArgumentException("A notification route is a relative application path.", nameof(route));
        }

        return value;
    }
}

/// <summary>
/// What a caller knows when something happens on a Case; the policy decides who
/// is told, and <see cref="RaiseStaffNotification"/> writes the row.
/// </summary>
public sealed record StaffNotificationEvent(
    StaffNotificationCause Cause,
    Guid? CaseId,
    string Reference,
    string? Registration,
    string Route,
    Guid? CaseEngineerId,
    ActionActor? Actor,
    Guid? JobStarterStaffId = null);

public interface IRaiseStaffNotification
{
    /// <summary>The notification written, or null when the policy names nobody.</summary>
    Task<StaffNotification?> ExecuteAsync(StaffNotificationEvent notificationEvent, CancellationToken cancellationToken);
}

public sealed class RaiseStaffNotification(IStaffNotificationStore store) : IRaiseStaffNotification
{
    private readonly IStaffNotificationStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<StaffNotification?> ExecuteAsync(
        StaffNotificationEvent notificationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notificationEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationEvent.Reference);
        var recipient = StaffNotificationPolicy.Recipient(
            notificationEvent.Cause,
            notificationEvent.CaseEngineerId,
            notificationEvent.JobStarterStaffId,
            StaffNotificationPolicy.StaffId(notificationEvent.Actor));
        if (recipient is not { } staffId)
        {
            return null;
        }

        return await _store.AddAsync(
            new NewStaffNotification(
                staffId,
                notificationEvent.CaseId,
                notificationEvent.Reference.Trim(),
                notificationEvent.Registration,
                notificationEvent.Cause,
                StaffNotificationPolicy.ValidateRoute(notificationEvent.Route),
                notificationEvent.Actor?.SubjectId),
            cancellationToken);
    }
}

/// <summary>The bell: the signed-in person's own notifications and nothing else.</summary>
public interface IMyStaffNotifications
{
    Task<IReadOnlyList<StaffNotification>> ListAsync(ActionActor actor, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(ActionActor actor, CancellationToken cancellationToken);

    /// <summary>Marks the notification read and returns it, so the caller can open its route; null when it is not the person's.</summary>
    Task<StaffNotification?> OpenAsync(ActionActor actor, Guid notificationId, CancellationToken cancellationToken);

    Task<int> MarkAllReadAsync(ActionActor actor, CancellationToken cancellationToken);
}

public sealed class MyStaffNotifications(
    IStaffNotificationStore store,
    TimeProvider timeProvider) : IMyStaffNotifications
{
    private readonly IStaffNotificationStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public Task<IReadOnlyList<StaffNotification>> ListAsync(ActionActor actor, CancellationToken cancellationToken) =>
        _store.ListAsync(RequireStaff(actor), Since(), cancellationToken);

    public Task<int> CountUnreadAsync(ActionActor actor, CancellationToken cancellationToken) =>
        _store.CountUnreadAsync(RequireStaff(actor), Since(), cancellationToken);

    public Task<StaffNotification?> OpenAsync(ActionActor actor, Guid notificationId, CancellationToken cancellationToken)
    {
        if (notificationId == Guid.Empty)
        {
            throw new ArgumentException("A notification identifier is required.", nameof(notificationId));
        }

        return _store.MarkReadAsync(RequireStaff(actor), notificationId, _timeProvider.GetUtcNow(), cancellationToken);
    }

    public Task<int> MarkAllReadAsync(ActionActor actor, CancellationToken cancellationToken) =>
        _store.MarkAllReadAsync(RequireStaff(actor), _timeProvider.GetUtcNow(), cancellationToken);

    private DateTimeOffset Since() => _timeProvider.GetUtcNow() - StaffNotificationPolicy.Retention;

    private static Guid RequireStaff(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.AccessStaffApplication);
        return StaffNotificationPolicy.StaffId(actor)
            ?? throw new StaffAuthorizationException(StaffAccessRight.AccessStaffApplication);
    }
}

/// <summary>
/// The Worker's sweep: notifications past the retention drop off. Reads filter on
/// the same window, so the purge is housekeeping rather than the rule.
/// </summary>
public sealed class PurgeStaffNotifications(
    IStaffNotificationStore store,
    TimeProvider timeProvider)
{
    private readonly IStaffNotificationStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    public Task<int> ExecuteAsync(CancellationToken cancellationToken) =>
        _store.PurgeOlderThanAsync(_timeProvider.GetUtcNow() - StaffNotificationPolicy.Retention, cancellationToken);
}
