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

public enum ActorKind
{
    Staff,
    SystemWorker,
    RequestLink,
    Bootstrap
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

    public bool IsInRole(StaffRole role) => Roles.Contains(role);

    public static ActionActor Staff(Guid staffId, IEnumerable<StaffRole> roles)
    {
        if (staffId == Guid.Empty)
        {
            throw new ArgumentException("A staff actor requires a non-empty staff identifier.", nameof(staffId));
        }

        ArgumentNullException.ThrowIfNull(roles);
        var roleSet = roles.ToFrozenSet();
        if (roleSet.Count == 0)
        {
            throw new ArgumentException("An enabled staff actor requires at least one current role.", nameof(roles));
        }

        return new ActionActor(ActorKind.Staff, staffId.ToString("D"), roleSet);
    }

    public static ActionActor SystemWorker(string workerId) =>
        CreateNonStaff(ActorKind.SystemWorker, workerId, nameof(workerId));

    public static ActionActor RequestLink(Guid requestId)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A request-link actor requires a non-empty request identifier.", nameof(requestId));
        }

        return new ActionActor(ActorKind.RequestLink, requestId.ToString("D"), NoRoles);
    }

    internal static ActionActor Bootstrap(string manifestSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestSha256);
        if (manifestSha256.Length != 64 || manifestSha256.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException(
                "A bootstrap actor requires a 64-character hexadecimal manifest SHA-256.",
                nameof(manifestSha256));
        }

        return new ActionActor(
            ActorKind.Bootstrap,
            manifestSha256.ToLowerInvariant(),
            NoRoles);
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

public sealed record SecurityEvent(
    Guid Id,
    SecurityEventType Type,
    SecurityEventOutcome Outcome,
    string SubjectId,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId,
    string? ReasonCode = null);

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
}
