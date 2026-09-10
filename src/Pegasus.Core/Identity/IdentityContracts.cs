using System.Collections.Frozen;

namespace Pegasus.Core.Identity;

public enum StaffRole
{
    Administrator,
    Engineer,
    User
}

public static class StaffRoleNames
{
    public const string Administrator = nameof(StaffRole.Administrator);
    public const string Engineer = nameof(StaffRole.Engineer);
    public const string User = nameof(StaffRole.User);

    public static IReadOnlyList<string> All { get; } =
        [Administrator, Engineer, User];
}

/// <summary>
/// Defines the application role hierarchy. An Administrator holds every staff
/// capability while retaining a single stored role.
/// </summary>
public static class StaffRoleCapabilities
{
    public static bool MeetsRequirement(StaffRole grantedRole, StaffRole requiredRole) =>
        grantedRole == StaffRole.Administrator || grantedRole == requiredRole;
}

public enum ActorKind
{
    Staff,
    SystemWorker,
    RequestLink,
    Automation,
    Provider
}

public sealed class ActionActor
{
    private static readonly FrozenSet<StaffRole> NoRoles = Array.Empty<StaffRole>().ToFrozenSet();

    private ActionActor(
        ActorKind kind,
        string subjectId,
        FrozenSet<StaffRole> roles)
    {
        Kind = kind;
        SubjectId = subjectId;
        Roles = roles;
    }

    public ActorKind Kind { get; }

    public string SubjectId { get; }

    public IReadOnlySet<StaffRole> Roles { get; }

    public bool IsInRole(StaffRole role) =>
        Roles.Any(grantedRole => StaffRoleCapabilities.MeetsRequirement(grantedRole, role));

    public static ActionActor Staff(Guid staffId, IEnumerable<StaffRole> roles)
    {
        if (staffId == Guid.Empty)
        {
            throw new ArgumentException("A staff actor requires a non-empty staff identifier.", nameof(staffId));
        }

        ArgumentNullException.ThrowIfNull(roles);
        var assignedRoles = roles.ToArray();
        if (assignedRoles.Length != 1)
        {
            throw new ArgumentException(
                "An enabled staff actor requires exactly one current role.",
                nameof(roles));
        }
        if (!Enum.IsDefined(assignedRoles[0]))
        {
            throw new ArgumentOutOfRangeException(
                nameof(roles),
                "A staff actor requires recognized current roles.");
        }

        return new ActionActor(
            ActorKind.Staff,
            staffId.ToString("D"),
            new[] { assignedRoles[0] }.ToFrozenSet());
    }

    public static ActionActor SystemWorker(string workerId) =>
        CreateNonStaff(ActorKind.SystemWorker, workerId, nameof(workerId));

    public static ActionActor Automation(string actorId) =>
        CreateNonStaff(ActorKind.Automation, actorId, nameof(actorId));

    /// <summary>
    /// A Provider API caller (API-01): the authenticated Principal is the
    /// subject, so every submission is attributable to that Principal and
    /// never to a credential, an e-mail domain or a tenant (FRD-09).
    /// </summary>
    public static ActionActor Provider(Guid principalId)
    {
        if (principalId == Guid.Empty)
        {
            throw new ArgumentException("A provider actor requires a non-empty principal identifier.", nameof(principalId));
        }

        return new ActionActor(ActorKind.Provider, principalId.ToString("D"), NoRoles);
    }

    public static ActionActor RequestLink(Guid requestId)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A request-link actor requires a non-empty request identifier.", nameof(requestId));
        }

        return new ActionActor(ActorKind.RequestLink, requestId.ToString("D"), NoRoles);
    }

    private static ActionActor CreateNonStaff(ActorKind kind, string subjectId, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId, parameterName);
        return new ActionActor(kind, subjectId.Trim(), NoRoles);
    }
}

public enum SecurityEventType
{
    SignIn,
    PasswordChanged,
    Token,
    Client,
    RateLimited,
    SecurityStampChanged,
    SecurityConfigurationChanged
}

public enum SecurityEventOutcome
{
    Succeeded,
    Denied,
    Failed
}

/// <summary>
/// One security event. <see cref="SubjectId"/> is what the event is <em>about</em>
/// — the account disabled, the client refused, the credential presented — and is
/// never assumed to be who caused it. <see cref="ActorKind"/> and
/// <see cref="ActorSubjectId"/> name the acting principal, so an administration
/// action reads as the operator who took it rather than as the account it landed
/// on. Both are absent together when the request carried no attributable
/// principal at all (an anonymous rate-limited call, a failed sign-in that never
/// identified anyone); readers label such a row by what it is instead of
/// inventing an actor.
/// </summary>
public sealed record SecurityEvent(
    Guid Id,
    SecurityEventType Type,
    SecurityEventOutcome Outcome,
    string SubjectId,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    string? ReasonCode = null,
    ActorKind? ActorKind = null,
    string? ActorSubjectId = null)
{
    /// <summary>
    /// The same event attributed to the principal that caused it, taken from the
    /// authorization actor the use case already carries.
    /// </summary>
    public SecurityEvent By(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return this with { ActorKind = actor.Kind, ActorSubjectId = actor.SubjectId };
    }

    /// <summary>
    /// The same event attributed to a principal whose kind and subject are known
    /// without a full authorization actor — a staff sign-in decision, where the
    /// subject is established but the current role set is not what is being
    /// recorded.
    /// </summary>
    public SecurityEvent By(ActorKind kind, string subjectId) =>
        string.IsNullOrWhiteSpace(subjectId)
            ? this
            : this with { ActorKind = kind, ActorSubjectId = subjectId.Trim() };
}

public sealed record ActionHistoryEntry(
    Guid Id,
    string AggregateType,
    string AggregateId,
    string EventKind,
    ActionActor Actor,
    DateTimeOffset OccurredAtUtc,
    string Outcome,
    string CorrelationId,
    string? Reason = null,
    string? BeforeJson = null,
    string? AfterJson = null,
    string? PolicyVersion = null);

public interface ISecurityEventWriter
{
    Task AppendAsync(SecurityEvent securityEvent, CancellationToken cancellationToken);
}

public interface IActionHistoryWriter
{
    Task AppendAsync(ActionHistoryEntry entry, CancellationToken cancellationToken);

    /// <summary>
    /// Appends an entry whose <see cref="ActionHistoryEntry.Id"/> is derived
    /// from the operation it records rather than fresh, and answers whether
    /// this call is the one that wrote it. False means another writer recorded
    /// the same operation first and its row stands — an outcome to act on, not
    /// a fault to hide.
    /// </summary>
    Task<bool> TryAppendAsync(ActionHistoryEntry entry, CancellationToken cancellationToken);
}
