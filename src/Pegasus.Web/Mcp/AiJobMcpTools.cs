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

internal enum AiJobTransitionAction
{
    Take,
    Progress,
    Complete,
    Fail,
    Release
}

/// <summary>
/// The AI job ledger tools (ADR-0035, FRD-10 § AI job and estimate tools):
/// the pull side of the ledger for an external AI client. Every tool
/// requires the <c>automation.jobs</c> scope; creation is limited to the
/// scheduled Unidentified-queue pass (operator decision D5); take and progress are
/// refused while the Administrator Send to AI switch is off. The five
/// transitions of a held job are one tool with an action, since each is the
/// same job id, expected version and operation key with one extra field.
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
    [Description("Creates an Unidentified-queue pass job, the only kind an external scheduler may start. Requires a mcp:-prefixed operation key; replaying the same key returns the same job.")]
    public async Task<AiJobToolItem> CreateAsync(
        [Description("Must be UnidentifiedQueuePass.")] string kind,
        [Description("Short instruction for the pass, at most 500 characters.")] string instruction,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
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
        Name = "pegasus_ai_job_transition",
        Title = "Move an AI job",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Moves one AI job for this client. Take claims a queued job under a 30-minute lease. Progress renews the lease and records progressNote. Complete marks a non-MarketResearch job Draft ready with resultKind (Estimate, ProposedResolution or DraftReply, matching the job kind) and the optional resultReference and resultText; nothing is applied to the record, staff confirm through the record's own action. Fail marks the job Failed with reason; it is not re-queued. Release returns a taken job to Queued before its lease ends, with an optional reason. Take and Progress are refused while the Administrator has stopped AI work. MarketResearch completes through pegasus_ai_job_complete_market_research.")]
    public async Task<AiJobToolItem> TransitionAsync(
        [Description("The job identifier from pegasus_ai_job_list.")] Guid jobId,
        [Description("The job version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("Take, Progress, Complete, Fail or Release.")] string action,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("Progress: the note, at most 500 characters.")] string? progressNote = null,
        [Description("Complete: Estimate, ProposedResolution or DraftReply.")] string? resultKind = null,
        [Description("Complete: reference to the draft written through the attributed tools, at most 200 characters.")] string? resultReference = null,
        [Description("Complete: proposal or draft text, at most 4000 characters.")] string? resultText = null,
        [Description("Fail: the reason, at most 500 characters. Release: optional.")] string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var parsedAction = ParseAction(action);
        var context = await resolver.RequireAsync(AutomationMcp.JobsScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        Task<AiJobToolItem> Run() => AutomationMcpErrors.ExecuteAsync(async () =>
        {
            var id = RequireJobId(jobId);
            var job = await (parsedAction switch
            {
                AiJobTransitionAction.Take => work.TakeAsync(
                    new(id, expectedVersion, context.Actor, key), cancellationToken),
                AiJobTransitionAction.Progress => work.ReportProgressAsync(
                    new(id, expectedVersion, context.Actor, key, Require(progressNote, "Progress", "progressNote")),
                    cancellationToken),
                AiJobTransitionAction.Complete => work.CompleteAsync(
                    new(id, expectedVersion, context.Actor, key, new(ParseResultKind(resultKind), resultReference, resultText)),
                    cancellationToken),
                AiJobTransitionAction.Fail => work.FailAsync(
                    new(id, expectedVersion, context.Actor, key, Require(reason, "Fail", "reason")), cancellationToken),
                AiJobTransitionAction.Release => work.ReleaseAsync(
                    new(id, expectedVersion, context.Actor, key, reason), cancellationToken),
                _ => throw new McpException("action must be Take, Progress, Complete, Fail or Release.")
            });
            return Map(job);
        });

        // Progress is lease telemetry, not permanent history; only its refusal is material.
        return parsedAction == AiJobTransitionAction.Progress
            ? await auditor.RecordDenialAsync(context, "pegasus_ai_job_transition", jobId.ToString("D"), key, Run, cancellationToken)
            : await auditor.RecordAsync(context, "pegasus_ai_job_transition", jobId.ToString("D"), key, Run, cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_ai_job_complete_market_research",
        Title = "Complete market research AI job",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Completes this client's MarketResearch job as Draft ready with one retained findings document and one AI market research valuation. Requires automation.jobs only: it takes no Case edit lease and no Case version, because a source card is not a Case field edit and the Engineer is usually still editing the Case, so it never waits on or ends their session. Nothing is accepted automatically.")]
    public async Task<MarketResearchCompletionToolResult> CompleteMarketResearchAsync(
        [Description("The MarketResearch job identifier this client holds.")] Guid jobId,
        [Description("The job version the caller observed; a stale value fails closed.")] long expectedJobVersion,
        [Description("The Case the research is for.")] Guid caseId,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("Leaf name for the findings document, at most 255 characters.")] string fileName,
        [Description("Media type for the findings document, at most 200 characters.")] string mediaType,
        [Description("Base64 findings document, at most 10 MiB after decoding.")] string contentBase64,
        [Description("Valuation date, yyyy-MM-dd.")] string recordedDate,
        [Description("Valuation time, HH:mm or HH:mm:ss.")] string recordedTime,
        [Description("The mileage the valuation assumes.")] long mileage,
        [Description("Retail value in pounds.")] decimal retailValue,
        [Description("Trade value in pounds.")] decimal tradeValue,
        [Description("Optional guide month the research is for, yyyy-MM; the valuation for that month replaces an earlier card for it.")] string? guideMonth = null,
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
                        tradeValue,
                        guideMonth is null ? null : ParseGuideMonth(guideMonth)),
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

    private static Guid RequireJobId(Guid jobId) => AutomationMcpErrors.RequireId(jobId, "job identifier");

    private static string Require(string? value, string action, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new McpException($"{action} needs {name}.")
            : value;

    private static AiJobTransitionAction ParseAction(string? action) =>
        Enum.TryParse<AiJobTransitionAction>(action?.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new McpException("action must be Take, Progress, Complete, Fail or Release.");

    private static AiJobResultKind ParseResultKind(string? resultKind) =>
        Enum.TryParse<AiJobResultKind>(resultKind?.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new McpException("Complete needs resultKind: Estimate, ProposedResolution or DraftReply.");

    private static AiJobKind ParseKind(string? kind) =>
        Enum.TryParse<AiJobKind>(kind?.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new McpException("The AI job kind is not recognized.");

    private static DateOnly ParseDate(string value) =>
        DateOnly.TryParseExact(value?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : throw new McpException("recordedDate must use yyyy-MM-dd.");

    private static DateOnly ParseGuideMonth(string value) =>
        DateOnly.TryParseExact(value.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : throw new McpException("guideMonth must use yyyy-MM.");

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
