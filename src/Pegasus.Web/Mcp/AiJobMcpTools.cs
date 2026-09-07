using System.ComponentModel;
using Pegasus.Core;
using System.Globalization;
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
    string? Continuation,
    string CorrelationId);

internal sealed record MarketResearchCompletionToolResult(
    AiJobToolItem Job,
    Guid DocumentOccurrenceId,
    Guid DocumentVersionId,
    Guid ValuationId,
    bool IsReplay);

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
    ICompleteMarketResearchAiJob completeMarketResearch,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor,
    ICursorProtector cursors)
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
        [Description("Optional exact kind: Estimate, UnidentifiedResolution, QueryResponse, UnidentifiedQueuePass or MarketResearch.")] string? kind = null,
        [Description("Opaque continuation returned by the preceding call.")] string? continuation = null,
        [Description("Page size from 1 to 100; 0 selects 50.")] int pageSize = 0,
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
                var limit = CursorPaging.NormalizeLimit(pageSize == 0 ? null : pageSize);
                var normalizedFilter = filter?.ToString() ?? string.Empty;
                var cursorScope = CursorPaging.CreateScope(
                    "pegasus_ai_job_list", context.Actor, normalizedFilter, "created-id-asc");
                DateTimeOffset? afterCreated = null;
                Guid? afterId = null;
                if (!string.IsNullOrWhiteSpace(continuation))
                {
                    var position = cursors.Unprotect(continuation, cursorScope);
                    afterCreated = CursorPaging.DecodeUtcTimestamp(position.SortKey);
                    afterId = position.Id;
                }
                var page = await queries.ListOpenPageAsync(
                    filter, context.GrantId, afterCreated, afterId, limit, cancellationToken);
                var next = page.HasMore && page.Jobs.Count > 0
                    ? cursors.Protect(
                        cursorScope,
                        CursorPaging.EncodeUtcTimestamp(page.Jobs[^1].CreatedAtUtc),
                        page.Jobs[^1].JobId)
                    : null;
                return new AiJobToolList(page.Jobs.Select(Map).ToArray(), next, context.TraceIdentifier);
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
    [Description("Marks this client's non-MarketResearch job Draft ready, naming the draft or proposal it produced: an estimate reference, a proposed Unidentified destination with its reason, or draft reply text. MarketResearch uses pegasus_ai_job_complete_market_research. Nothing is applied to the record; staff confirm through the record's own action.")]
    public async Task<AiJobToolItem> CompleteAsync(
        Guid jobId,
        long expectedVersion,
        [Description("Estimate, ProposedResolution or DraftReply — must match the job kind.")] string resultKind,
        string operationKey,
        [Description("Reference to the draft written through the attributed tools, at most 200 characters.")] string? resultReference = null,
        [Description("Proposal or draft text, at most 4000 characters.")] string? resultText = null,
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
        Name = "pegasus_ai_job_complete_market_research",
        Title = "Complete market research AI job",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Completes this client's MarketResearch job as Draft ready with one retained findings document and one AI market research valuation. Requires automation.jobs; first claim case edit authority with pegasus_case_edit_begin under automation.cases and present that lease here. Nothing is accepted automatically.")]
    public async Task<MarketResearchCompletionToolResult> CompleteMarketResearchAsync(
        Guid jobId,
        long expectedJobVersion,
        Guid caseId,
        long expectedCaseVersion,
        string editLeaseToken,
        string operationKey,
        [Description("Leaf name for the findings document, at most 255 characters.")] string fileName,
        [Description("Media type for the findings document, at most 200 characters.")] string mediaType,
        [Description("Base64 findings document, at most 10 MiB after decoding.")] string contentBase64,
        [Description("Valuation date, yyyy-MM-dd.")] string recordedDate,
        [Description("Valuation time, HH:mm or HH:mm:ss.")] string recordedTime,
        long mileage,
        decimal retailValue,
        decimal tradeValue,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.JobsScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_ai_job_complete_market_research",
            jobId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                var completion = await completeMarketResearch.ExecuteAsync(
                    new(
                        RequireJobId(jobId),
                        expectedJobVersion,
                        AutomationMcpErrors.RequireId(caseId, "case identifier"),
                        expectedCaseVersion,
                        editLeaseToken,
                        context.Actor,
                        key,
                        AutomationMcpErrors.RequireFileName(fileName),
                        AutomationMcpErrors.RequireMediaType(mediaType),
                        AutomationMcpErrors.DecodeContent(
                            contentBase64,
                            AutomationMcpErrors.MaximumDocumentBytes,
                            "Findings document content"),
                        ParseDate(recordedDate),
                        ParseTime(recordedTime),
                        mileage,
                        retailValue,
                        tradeValue),
                    cancellationToken);
                return new MarketResearchCompletionToolResult(
                    Map(completion.Job),
                    completion.Document.Occurrence.Id,
                    completion.Document.Version.Id,
                    completion.Valuation.ValuationId,
                    completion.IsReplay);
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
        string operationKey,
        [Description("Optional, at most 500 characters.")] string? reason = null,
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

    private static DateOnly ParseDate(string value) =>
        DateOnly.TryParseExact(value?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : throw new McpException("recordedDate must use yyyy-MM-dd.");

    private static TimeOnly ParseTime(string value) =>
        TimeOnly.TryParseExact(
            value?.Trim(),
            ["HH:mm", "HH:mm:ss"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : throw new McpException("recordedTime must use HH:mm or HH:mm:ss.");

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
