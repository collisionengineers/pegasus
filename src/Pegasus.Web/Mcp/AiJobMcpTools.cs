using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.AiWork;

namespace Pegasus.Web.Mcp;

internal sealed record AiJobToolItem(
    Guid JobId,
    string Kind,
    string SubjectKind,
    Guid? SubjectId,
    string SubjectReference,
    string Instruction,
    int? TargetPercentOfEngineerValue,
    decimal? EngineerValueAtSend,
    string State,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string? TakenBy,
    DateTimeOffset? LeaseExpiresAtUtc,
    string? ProgressNote,
    string? ResultKind,
    string? ResultReference,
    string? ResultText,
    string? ClosureReason,
    long Version);

internal sealed record AiJobToolList(
    IReadOnlyList<AiJobToolItem> Jobs,
    string CorrelationId);

/// <summary>
/// The AI job ledger tools (ADR-0035, FRD-10 § AI job and estimate tools):
/// the pull side of the ledger for an external AI client. Every tool
/// requires the <c>automation.jobs</c> scope; creation is limited to the
/// scheduled Unidentified-queue pass (EPIC-011 D5); take and progress are
/// refused while the Administrator Send to AI switch is off.
/// </summary>
[McpServerToolType]
internal sealed class AiJobMcpTools(
    IAiJobQueries queries,
    ICreateAiJob create,
    IWorkAiJob work,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor)
{
    [McpServerTool(
        Name = "pegasus_ai_job_list",
        Title = "List AI jobs",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Lists every queued AI job and the jobs this client currently holds, oldest first. A Taken job whose lease has lapsed is listed as Queued.")]
    public async Task<AiJobToolList> ListAsync(
        [Description("Optional exact kind: Estimate, UnidentifiedResolution, QueryResponse or UnidentifiedQueuePass.")] string? kind = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.JobsScope, cancellationToken);
        return await auditor.RecordDenialAsync(
            context,
            "pegasus_ai_job_list",
            "ai_job",
            null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AiJobKind? filter = string.IsNullOrWhiteSpace(kind) ? null : ParseKind(kind);
                var jobs = await queries.ListOpenAsync(cancellationToken);
                var visible = jobs
                    .Where(job => job.State == AiJobState.Queued
                        || (job.State is AiJobState.Taken or AiJobState.DraftReady
                            && string.Equals(job.TakenBy, context.ClientId, StringComparison.Ordinal)))
                    .Where(job => filter is null || job.Kind == filter)
                    .Select(Map)
                    .ToArray();
                return new AiJobToolList(visible, context.TraceIdentifier);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_ai_job_create",
        Title = "Create an AI job",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Creates an Unidentified-queue pass job — the only kind an external scheduler may start. Requires a mcp:-prefixed operation key; replaying the same key returns the same job.")]
    public async Task<AiJobToolItem> CreateAsync(
        [Description("Must be UnidentifiedQueuePass.")] string kind,
        [Description("Short instruction for the pass, at most 500 characters.")] string instruction,
        string operationKey,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.JobsScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_ai_job_create",
            kind?.Trim() ?? "invalid",
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                if (ParseKind(kind) != AiJobKind.UnidentifiedQueuePass)
                {
                    throw new McpException(
                        "The Automation Actor creates only UnidentifiedQueuePass jobs.");
                }

                var created = await create.ExecuteAsync(
                    new(
                        AiJobKind.UnidentifiedQueuePass,
                        null,
                        null,
                        instruction,
                        null,
                        context.Actor,
                        key),
                    cancellationToken);
                return Map(created);
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_ai_job_take",
        Title = "Take an AI job",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Claims one queued job under a 30-minute lease held by this client. Refused when the job is not queued, its version has moved, or the Administrator has stopped AI work.")]
    public async Task<AiJobToolItem> TakeAsync(
        Guid jobId,
        long expectedVersion,
        string operationKey,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.JobsScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_ai_job_take",
            jobId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
                Map(await work.TakeAsync(
                    new(RequireJobId(jobId), expectedVersion, context.Actor, key),
                    cancellationToken))),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_ai_job_progress",
        Title = "Report AI job progress",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Renews this client's lease and records a short progress note. Refused after cancellation, lease expiry, or while AI work is stopped.")]
    public async Task<AiJobToolItem> ProgressAsync(
        Guid jobId,
        long expectedVersion,
        [Description("At most 500 characters.")] string progressNote,
        string operationKey,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.JobsScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordDenialAsync(
            context,
            "pegasus_ai_job_progress",
            jobId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
                Map(await work.ReportProgressAsync(
                    new(RequireJobId(jobId), expectedVersion, context.Actor, key, progressNote),
                    cancellationToken))),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_ai_job_complete",
        Title = "Complete an AI job",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Marks this client's job Draft ready, naming the draft or proposal it produced: an estimate reference, a proposed Unidentified destination with its reason, or draft reply text. Nothing is applied to the record; staff confirm through the record's own action.")]
    public async Task<AiJobToolItem> CompleteAsync(
        Guid jobId,
        long expectedVersion,
        [Description("Estimate, ProposedResolution or DraftReply — must match the job kind.")] string resultKind,
        [Description("Reference to the draft written through the attributed tools, at most 200 characters.")] string? resultReference,
        [Description("Proposal or draft text, at most 4000 characters.")] string? resultText,
        string operationKey,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.JobsScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_ai_job_complete",
            jobId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                if (!Enum.TryParse<AiJobResultKind>(resultKind?.Trim(), ignoreCase: true, out var parsed)
                    || !Enum.IsDefined(parsed))
                {
                    throw new McpException("The result kind is not recognized.");
                }

                return Map(await work.CompleteAsync(
                    new(
                        RequireJobId(jobId),
                        expectedVersion,
                        context.Actor,
                        key,
                        new(parsed, resultReference, resultText)),
                    cancellationToken));
            }),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_ai_job_fail",
        Title = "Fail an AI job",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Marks this client's job Failed with a reason. The job is not re-queued automatically.")]
    public async Task<AiJobToolItem> FailAsync(
        Guid jobId,
        long expectedVersion,
        [Description("At most 500 characters.")] string reason,
        string operationKey,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.JobsScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_ai_job_fail",
            jobId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
                Map(await work.FailAsync(
                    new(RequireJobId(jobId), expectedVersion, context.Actor, key, reason),
                    cancellationToken))),
            cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_ai_job_release",
        Title = "Release an AI job",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns this client's taken job to Queued before its lease ends, optionally with a reason.")]
    public async Task<AiJobToolItem> ReleaseAsync(
        Guid jobId,
        long expectedVersion,
        string? reason,
        string operationKey,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.JobsScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_ai_job_release",
            jobId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
                Map(await work.ReleaseAsync(
                    new(RequireJobId(jobId), expectedVersion, context.Actor, key, reason),
                    cancellationToken))),
            cancellationToken);
    }

    private static Guid RequireJobId(Guid jobId) => AutomationMcpErrors.RequireId(jobId, "job identifier");

    private static AiJobKind ParseKind(string? kind) =>
        Enum.TryParse<AiJobKind>(kind?.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new McpException("The AI job kind is not recognized.");

    private static AiJobToolItem Map(AiJobRecord job) => new(
        job.JobId,
        job.Kind.ToString(),
        job.SubjectKind.ToString(),
        job.SubjectId,
        job.SubjectReference,
        job.Instruction,
        job.TargetPercentOfEngineerValue,
        job.EngineerValueAtSend,
        job.State.ToString(),
        job.CreatedBy,
        job.CreatedAtUtc,
        job.ExpiresAtUtc,
        job.TakenBy,
        job.LeaseExpiresAtUtc,
        job.ProgressNote,
        job.ResultKind?.ToString(),
        job.ResultReference,
        job.ResultText,
        job.ClosureReason,
        job.Version);
}
