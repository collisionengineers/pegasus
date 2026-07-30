namespace Pegasus.Core.ActionHistory;

public sealed record BusinessAction(
    Guid Id,
    Guid? CaseId,
    Guid? TriageId,
    string ActorKind,
    Guid ActorId,
    string Caller,
    string Action,
    DateTimeOffset OccurredAtUtc,
    Guid CorrelationId,
    string? BeforeJson,
    string? AfterJson,
    string Outcome,
    string? Reason);

public interface IPermanentActionHistoryQueries
{
    Task<IReadOnlyList<BusinessAction>> ListForCaseAsync(Guid caseId, CancellationToken cancellationToken);
    Task<IReadOnlyList<BusinessAction>> ListForTriageAsync(Guid triageId, CancellationToken cancellationToken);
}
