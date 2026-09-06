using Pegasus.Core.Identity;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;

namespace Pegasus.Core.AiWork;

/// <summary>
/// The AI job ledger (AI-10, ADR-0035): a durable, pull-based catalogue of
/// named AI jobs that external clients claim through the Automation Actor
/// under a bounded lease. Pegasus never runs a job and never applies a
/// job's result; a completed job points at a draft or carries a proposal
/// that a staff act confirms through the record's own action (FRD-11
/// § AI Job List).
/// </summary>
public enum AiJobKind
{
    Estimate,
    UnidentifiedResolution,
    QueryResponse,
    UnidentifiedQueuePass,
    MarketResearch
}

public enum AiJobState
{
    Queued,
    Taken,
    DraftReady,
    Completed,
    Failed,
    Cancelled,
    Expired
}

public enum AiJobSubjectKind
{
    Case,
    Unidentified,
    Queue
}

public enum AiJobResultKind
{
    Estimate,
    ProposedResolution,
    DraftReply,
    MarketResearch
}

public static class AiJobStates
{
    public static bool IsTerminal(AiJobState state) =>
        state is AiJobState.Completed
            or AiJobState.Failed
            or AiJobState.Cancelled
            or AiJobState.Expired;
}

/// <summary>
/// The result a client names on a job: a pointer (an estimate or draft
/// reference written through the attributed Actor tools) and/or bounded
/// text (a proposed destination with its reason, a drafted reply).
/// </summary>
public sealed record AiJobResult(
    AiJobResultKind Kind,
    string? Reference,
    string? Text);

public sealed record AiJobRecord(
    Guid JobId,
    AiJobKind Kind,
    AiJobSubjectKind SubjectKind,
    Guid? SubjectId,
    string SubjectReference,
    string Instruction,
    int? TargetPercentOfEngineerValue,
    decimal? EngineerValueAtSend,
    AiJobState State,
    ActorKind CreatedByKind,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string? TakenBy,
    DateTimeOffset? TakenAtUtc,
    DateTimeOffset? LeaseExpiresAtUtc,
    string? ProgressNote,
    AiJobResultKind? ResultKind,
    string? ResultReference,
    string? ResultText,
    DateTimeOffset? ClosedAtUtc,
    string? ClosureReason,
    long Version);

public sealed record AiJobCounts(int Active, int Failed);

/// <summary>
/// The caller's request to start a job. The subject is resolved by
/// <see cref="ICreateAiJob"/> against the record it names; the queue pass
/// names no subject.
/// </summary>
public sealed record CreateAiJobCommand(
    AiJobKind Kind,
    Guid? SubjectId,
    string? SubjectReference,
    string Instruction,
    int? TargetPercentOfEngineerValue,
    ActionActor Actor,
    string OperationKey);

/// <summary>The resolved, validated job handed to the store.</summary>
public sealed record NewAiJob(
    AiJobKind Kind,
    AiJobSubjectKind SubjectKind,
    Guid? SubjectId,
    string SubjectReference,
    string Instruction,
    int? TargetPercentOfEngineerValue,
    decimal? EngineerValueAtSend,
    ActionActor Actor,
    string OperationKey,
    TimeSpan Expiry);

/// <summary>
/// One state change. Taken → Taken with a lease renews the claim (progress);
/// Taken → Queued releases or expires it; DraftReady carries the result;
/// every terminal state carries a reason where the FRD requires one.
/// </summary>
public sealed record AiJobTransition(
    Guid JobId,
    long ExpectedVersion,
    AiJobState TargetState,
    ActionActor Actor,
    string OperationKey,
    string? Reason = null,
    string? ProgressNote = null,
    AiJobResult? Result = null,
    DateTimeOffset? LeaseExpiresAtUtc = null);

public sealed record TakeAiJobCommand(
    Guid JobId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey);

public sealed record ReleaseAiJobCommand(
    Guid JobId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string? Reason);

public sealed record ReportAiJobProgressCommand(
    Guid JobId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string ProgressNote);

public sealed record CompleteAiJobCommand(
    Guid JobId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    AiJobResult Result);

public sealed record FailAiJobCommand(
    Guid JobId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason);

public sealed record CancelAiJobCommand(
    Guid JobId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason);

public sealed record ConfirmAiJobCommand(
    Guid JobId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey);

public sealed record CompleteMarketResearchAiJobCommand(
    Guid JobId,
    long ExpectedJobVersion,
    Guid CaseId,
    long ExpectedCaseVersion,
    string EditLeaseToken,
    ActionActor Actor,
    string OperationKey,
    string FileName,
    string MediaType,
    ReadOnlyMemory<byte> Content,
    DateOnly RecordedDate,
    TimeOnly RecordedTime,
    long Mileage,
    decimal RetailValue,
    decimal TradeValue);

public sealed record MarketResearchAiJobCompletion(
    AiJobRecord Job,
    AddCaseDocumentResult Document,
    CaseValuation Valuation,
    bool IsReplay);

public interface IMarketResearchAiJobCompletionStore
{
    Task<MarketResearchAiJobCompletion> CompleteAsync(
        CompleteMarketResearchAiJobCommand command,
        CancellationToken cancellationToken);
}

public interface ICompleteMarketResearchAiJob
{
    Task<MarketResearchAiJobCompletion> ExecuteAsync(
        CompleteMarketResearchAiJobCommand command,
        CancellationToken cancellationToken);
}

public interface IAiJobStore
{
    Task<AiJobRecord> CreateAsync(NewAiJob job, CancellationToken cancellationToken);

    Task<AiJobRecord?> GetAsync(Guid jobId, CancellationToken cancellationToken);

    Task<AiJobRecord> TransitionAsync(AiJobTransition transition, CancellationToken cancellationToken);
}

public interface IAiJobQueries
{
    /// <summary>Every non-terminal job, oldest first.</summary>
    Task<IReadOnlyList<AiJobRecord>> ListOpenAsync(CancellationToken cancellationToken);

    Task<AiJobQueryPage> ListOpenPageAsync(
        AiJobKind? kind,
        string grantId,
        DateTimeOffset? afterCreatedAtUtc,
        Guid? afterJobId,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AiJobRecord>> ListForSubjectAsync(
        Guid subjectId,
        CancellationToken cancellationToken);

    /// <summary>The most recently created jobs, newest first.</summary>
    Task<IReadOnlyList<AiJobRecord>> ListRecentAsync(int max, CancellationToken cancellationToken);

    Task<AiJobCounts> GetCountsAsync(CancellationToken cancellationToken);
}

public sealed record AiJobQueryPage(
    IReadOnlyList<AiJobRecord> Jobs,
    bool HasMore);

public interface ICreateAiJob
{
    Task<AiJobRecord> ExecuteAsync(CreateAiJobCommand command, CancellationToken cancellationToken);
}

/// <summary>The Automation client's side of the ledger.</summary>
public interface IWorkAiJob
{
    Task<AiJobRecord> TakeAsync(TakeAiJobCommand command, CancellationToken cancellationToken);

    Task<AiJobRecord> ReleaseAsync(ReleaseAiJobCommand command, CancellationToken cancellationToken);

    Task<AiJobRecord> ReportProgressAsync(
        ReportAiJobProgressCommand command,
        CancellationToken cancellationToken);

    Task<AiJobRecord> CompleteAsync(CompleteAiJobCommand command, CancellationToken cancellationToken);

    Task<AiJobRecord> FailAsync(FailAiJobCommand command, CancellationToken cancellationToken);
}

public interface ICancelAiJob
{
    Task<AiJobRecord> ExecuteAsync(CancelAiJobCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// The staff act that closes a Draft ready job as Completed once its result
/// has been consumed (or needs no separate act).
/// </summary>
public interface IConfirmAiJob
{
    Task<AiJobRecord> ExecuteAsync(ConfirmAiJobCommand command, CancellationToken cancellationToken);
}
