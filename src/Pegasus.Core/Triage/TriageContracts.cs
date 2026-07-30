using Pegasus.Core.Access;
using Pegasus.Core.ActionHistory;

namespace Pegasus.Core.Triage;

public enum TriageState
{
    Open,
    AwaitingInformation,
    FindingRecorded,
    Completed,
    Cancelled
}

public enum RoadworthinessFinding
{
    Roadworthy,
    Unroadworthy
}

public enum AssessmentFinding
{
    Repairable,
    TotalLoss
}

public enum TriageCommandFailure
{
    NotFound,
    Denied,
    RegistrationRequired,
    InvalidState,
    FindingRequired,
    ReasonRequired,
    ReplyEvidenceUnavailable,
    ReplyEvidenceMissing,
    ReplyEvidenceAmbiguous,
    CaseNotFound,
    StaleVersion
}

public sealed record TriageQuery(
    TriageState? State = null,
    Guid? AssigneeId = null,
    string? Registration = null,
    int Page = 1,
    int PageSize = 50)
{
    public string? NormalizedRegistration => NormalizeRegistration(Registration);

    public static string? NormalizeRegistration(string? registration)
    {
        if (string.IsNullOrWhiteSpace(registration)) return null;
        var value = new string(registration.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return value.Length == 0 ? null : value;
    }
}

public sealed record TriageSummary(
    Guid Id,
    Guid SourceId,
    string Registration,
    string? AssigneeName,
    TriageState State,
    DateTimeOffset LastChangedAtUtc,
    long Version);

public sealed record TriageFindingRevision(
    Guid Id,
    RoadworthinessFinding? Roadworthiness,
    AssessmentFinding? Assessment,
    string? Reason,
    DateTimeOffset RecordedAtUtc,
    Guid ActorId);

public sealed record TriageReplyEvidence(
    string ExternalMessageId,
    string ConversationId,
    string ApprovedMailbox,
    DateTimeOffset SentAtUtc,
    string ReplyHash);

public sealed record TriageCaseLink(Guid CaseId, DateTimeOffset LinkedAtUtc, DateTimeOffset? UnlinkedAtUtc, string? Reason);

public sealed record TriageHistoryEntry(
    Guid Id,
    string Action,
    string Outcome,
    DateTimeOffset OccurredAtUtc,
    string ActorName,
    string? Reason);

public sealed record TriageDetail(
    Guid Id,
    Guid SourceId,
    string Registration,
    Guid? AssigneeId,
    string? AssigneeName,
    TriageState State,
    TriageFindingRevision? CurrentFinding,
    IReadOnlyList<TriageFindingRevision> FindingRevisions,
    TriageReplyEvidence? ReplyEvidence,
    TriageCaseLink? CurrentCaseLink,
    IReadOnlyList<TriageCaseLink> CaseLinkHistory,
    long Version,
    IReadOnlyList<TriageHistoryEntry> History);

public sealed record TriageCommandResult(TriageDetail? Detail, TriageCommandFailure? Failure, string? Message = null)
{
    public bool Succeeded => Detail is not null && Failure is null;
    public static TriageCommandResult Failed(TriageCommandFailure failure, string? message = null) => new(null, failure, message);
}

public interface ITriageQueries
{
    Task<IReadOnlyList<TriageSummary>> ListAsync(TriageQuery query, StaffActor actor, CancellationToken cancellationToken);
    Task<TriageDetail?> GetAsync(Guid id, StaffActor actor, CancellationToken cancellationToken);
    Task<int> GetOpenCountAsync(StaffActor actor, CancellationToken cancellationToken);
}

public interface ITriageStore : ITriageQueries, IPermanentActionHistoryQueries
{
    Task<TriageCommandResult> AssignAsync(Guid id, long expectedVersion, StaffActor actor, Guid assigneeId, string assigneeName, CancellationToken cancellationToken);
    Task<TriageCommandResult> MarkAwaitingInformationAsync(Guid id, long expectedVersion, StaffActor actor, string reason, CancellationToken cancellationToken);
    Task<TriageCommandResult> RecordFindingAsync(Guid id, long expectedVersion, StaffActor actor, RoadworthinessFinding? roadworthiness, AssessmentFinding? assessment, string? reason, CancellationToken cancellationToken);
    Task<TriageCommandResult> CompleteAsync(Guid id, long expectedVersion, StaffActor actor, TriageReplyEvidence evidence, CancellationToken cancellationToken);
    Task<TriageCommandResult> CancelAsync(Guid id, long expectedVersion, StaffActor actor, string reason, CancellationToken cancellationToken);
    Task<TriageCommandResult> ReopenAsync(Guid id, long expectedVersion, StaffActor actor, string reason, CancellationToken cancellationToken);
    Task<TriageCommandResult> LinkCaseAsync(Guid id, long expectedVersion, StaffActor actor, Guid caseId, CancellationToken cancellationToken);
    Task<TriageCommandResult> UnlinkCaseAsync(Guid id, long expectedVersion, StaffActor actor, string reason, CancellationToken cancellationToken);
}
