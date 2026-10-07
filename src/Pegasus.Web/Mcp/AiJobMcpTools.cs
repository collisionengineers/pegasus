using System.ComponentModel;
using Pegasus.Core;
using System.Globalization;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Intake;

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
    Release,
    Cancel,
    Confirm
}

/// <summary>
/// The AI job ledger tools (ADR-0035, FRD-10 § AI job and estimate tools):
/// the ledger for an external AI client. Every tool requires the
/// <c>automation.jobs</c> scope. The Automation Actor creates any kind, through
/// the same Core command and subject checks as the staff action that starts it
/// (ADR-0064: a job is how an external agent picks up work); take and progress
/// are refused while the Administrator Send to AI switch is off. The
/// transitions of a job are one tool with an action, since each is the same
/// job id, expected version and operation key with one extra field: the
/// client's own Take, Progress, Complete, Fail and Release, and the
/// staff-equivalent Cancel and Confirm.
/// </summary>
[McpServerToolType]
internal sealed class AiJobMcpTools(
    IAiJobQueries queries,
    ICreateAiJob create,
    IStartMarketResearch startMarketResearch,
    IWorkAiJob work,
    ICancelAiJob cancel,
    IConfirmAiJob confirm,
    ICompleteMarketResearchAiJob completeMarketResearch,
    GetRetainedMail getRetainedMail,
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
    [Description("Creates one AI job of any kind through the same Core command and subject checks as the staff action that starts it, so external agents on automated runs can queue work for each other. Estimate needs caseId (a Case With Engineer or later that has an Engineer's Value) and takes an optional instruction and targetPercentOfEngineerValue. MarketResearch needs caseId and guideMonth; while one is already queued or taken for the Case, that job is returned instead. QueryResponse needs messageId, a retained post-report query linked to its Case. UnidentifiedResolution needs unidentifiedReference, an open item. UnidentifiedQueuePass needs instruction. Refused while the Administrator has stopped AI work. Requires a mcp:-prefixed operation key; replaying the same key returns the same job.")]
    public async Task<AiJobToolItem> CreateAsync(
        [Description("Estimate, MarketResearch, QueryResponse, UnidentifiedResolution or UnidentifiedQueuePass.")] string kind,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("Estimate: optional direction, at most 500 characters; omitted, the job asks for an estimate for the Case. UnidentifiedQueuePass: required, at most 500 characters. The other kinds carry the fixed instruction staff send, and a value is refused for them.")] string? instruction = null,
        [Description("Estimate and MarketResearch: the Case identifier.")] Guid? caseId = null,
        [Description("Estimate only: optional target from 0 to 80 percent of the Case's recorded Engineer's Value; guidance for the drafter, never an accepted figure.")] int? targetPercentOfEngineerValue = null,
        [Description("MarketResearch only: the guide month the research is for, yyyy-MM.")] string? guideMonth = null,
        [Description("QueryResponse only: the retained message identifier (pegasus_mail_list) of the post-report query the reply answers.")] Guid? messageId = null,
        [Description("UnidentifiedResolution only: the open item's exact U-reference, for example U17.")] string? unidentifiedReference = null,
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
                var parsedKind = ParseKind(kind);
                RefuseUnused(parsedKind, caseId, targetPercentOfEngineerValue, guideMonth, messageId, unidentifiedReference);
                if (parsedKind is not (AiJobKind.Estimate or AiJobKind.UnidentifiedQueuePass)
                    && instruction is not null)
                {
                    throw new McpException(
                        $"The {parsedKind} job carries the fixed instruction staff send; omit instruction.");
                }

                var created = parsedKind switch
                {
                    AiJobKind.Estimate => await create.ExecuteAsync(
                        new(
                            AiJobKind.Estimate,
                            RequireCase(caseId, parsedKind),
                            null,
                            instruction ?? string.Empty,
                            targetPercentOfEngineerValue,
                            context.Actor,
                            key),
                        cancellationToken),
                    AiJobKind.MarketResearch => await startMarketResearch.ExecuteAsync(
                        new(
                            RequireCase(caseId, parsedKind),
                            guideMonth is null
                                ? throw new McpException("A MarketResearch job needs guideMonth, yyyy-MM.")
                                : ParseGuideMonth(guideMonth),
                            context.Actor,
                            key),
                        cancellationToken),
                    AiJobKind.QueryResponse => await CreateQueryResponseAsync(
                        context, key, messageId, cancellationToken),
                    AiJobKind.UnidentifiedResolution => await create.ExecuteAsync(
                        new(
                            AiJobKind.UnidentifiedResolution,
                            null,
                            string.IsNullOrWhiteSpace(unidentifiedReference)
                                ? throw new McpException("An UnidentifiedResolution job needs unidentifiedReference.")
                                : unidentifiedReference.Trim(),
                            AiJobPolicy.UnidentifiedResolutionInstruction,
                            null,
                            context.Actor,
                            key),
                        cancellationToken),
                    _ => await create.ExecuteAsync(
                        new(
                            AiJobKind.UnidentifiedQueuePass,
                            null,
                            null,
                            instruction ?? throw new McpException("An UnidentifiedQueuePass job needs instruction."),
                            null,
                            context.Actor,
                            key),
                        cancellationToken)
                };
                return Map(created);
            }),
            cancellationToken);
    }

    /// <summary>
    /// A Query response answers one retained post-report query linked to its
    /// Case, as the message page's action does; its instruction is the
    /// message identifier.
    /// </summary>
    private async Task<AiJobRecord> CreateQueryResponseAsync(
        AutomationActorContext context,
        string key,
        Guid? messageId,
        CancellationToken cancellationToken)
    {
        var id = AutomationMcpErrors.RequireId(
            messageId ?? throw new McpException("A QueryResponse job needs messageId."),
            "retained message identifier");
        var detail = await getRetainedMail.ExecuteAsync(context.Actor, id, cancellationToken)
            ?? throw new McpException("The retained message was not found.");
        if (!AiJobPolicy.IsQueryResponseSource(detail) || detail.Summary.CaseId is not { } queryCaseId)
        {
            throw new McpException(
                "A QueryResponse job answers a retained post-report query that is linked to its Case.");
        }

        return await create.ExecuteAsync(
            new(
                AiJobKind.QueryResponse,
                queryCaseId,
                detail.Summary.CaseReference,
                id.ToString("D"),
                null,
                context.Actor,
                key),
            cancellationToken);
    }

    /// <summary>A subject field another kind needs is refused rather than ignored.</summary>
    private static void RefuseUnused(
        AiJobKind kind,
        Guid? caseId,
        int? targetPercent,
        string? guideMonth,
        Guid? messageId,
        string? unidentifiedReference)
    {
        var unused = new List<string>();
        if (caseId is not null && kind is not (AiJobKind.Estimate or AiJobKind.MarketResearch))
        {
            unused.Add("caseId");
        }
        if (targetPercent is not null && kind != AiJobKind.Estimate)
        {
            unused.Add("targetPercentOfEngineerValue");
        }
        if (guideMonth is not null && kind != AiJobKind.MarketResearch)
        {
            unused.Add("guideMonth");
        }
        if (messageId is not null && kind != AiJobKind.QueryResponse)
        {
            unused.Add("messageId");
        }
        if (unidentifiedReference is not null && kind != AiJobKind.UnidentifiedResolution)
        {
            unused.Add("unidentifiedReference");
        }
        if (unused.Count > 0)
        {
            throw new McpException($"The {kind} job does not take {string.Join(", ", unused)}.");
        }
    }

    private static Guid RequireCase(Guid? caseId, AiJobKind kind) =>
        AutomationMcpErrors.RequireId(
            caseId ?? throw new McpException($"The {kind} job needs caseId."),
            "case identifier");

    [McpServerTool(
        Name = "pegasus_ai_job_transition",
        Title = "Move an AI job",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Moves one AI job. The client's own transitions: Take claims a queued job under a 30-minute lease. Progress renews the lease and records progressNote. Complete marks a non-MarketResearch job Draft ready with resultKind (Estimate, ProposedResolution or DraftReply, matching the job kind) and the optional resultReference and resultText; nothing is applied to the record. Fail marks the job Failed with reason; it is not re-queued. Release returns a taken job to Queued before its lease ends, with an optional reason. Progress, Complete, Fail and Release act only on a job this client holds. The staff-equivalent transitions act on any client's job: Cancel stops a queued, taken or Draft ready job with reason; Confirm marks a Draft ready QueryResponse or UnidentifiedQueuePass job Completed once its result has been used, as the Work Centre's Complete job does (an Estimate job completes when staff use its estimate, an UnidentifiedResolution job through the item's own resolution). Take and Progress are refused while the Administrator has stopped AI work. MarketResearch completes through pegasus_ai_job_complete_market_research.")]
    public async Task<AiJobToolItem> TransitionAsync(
        [Description("The job identifier from pegasus_ai_job_list.")] Guid jobId,
        [Description("The job version the caller observed; a stale value fails closed.")] long expectedVersion,
        [Description("Take, Progress, Complete, Fail, Release, Cancel or Confirm.")] string action,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("Progress: the note, at most 500 characters.")] string? progressNote = null,
        [Description("Complete: Estimate, ProposedResolution or DraftReply.")] string? resultKind = null,
        [Description("Complete: reference to the draft written through the attributed tools, at most 200 characters.")] string? resultReference = null,
        [Description("Complete: proposal or draft text, at most 4000 characters.")] string? resultText = null,
        [Description("Fail and Cancel: the reason, at most 500 characters. Release: optional.")] string? reason = null,
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
                AiJobTransitionAction.Cancel => cancel.ExecuteAsync(
                    new(id, expectedVersion, context.Actor, key, Require(reason, "Cancel", "reason")), cancellationToken),
                AiJobTransitionAction.Confirm => ConfirmAsync(id, expectedVersion, context, key, cancellationToken),
                _ => throw new McpException(ActionValues)
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

    /// <summary>
    /// Confirm closes only a job the Work Centre completes by hand; the others
    /// complete through their record's own act (FRD-27).
    /// </summary>
    private async Task<AiJobRecord> ConfirmAsync(
        Guid jobId,
        long expectedVersion,
        AutomationActorContext context,
        string key,
        CancellationToken cancellationToken)
    {
        var job = (await queries.ListOpenAsync(cancellationToken))
            .FirstOrDefault(candidate => candidate.JobId == jobId);
        if (job is null || !AiJobPolicy.CompletesByHand(job))
        {
            throw new McpException(
                "Only a Draft ready QueryResponse or UnidentifiedQueuePass job is confirmed by hand.");
        }

        return await confirm.ExecuteAsync(new(jobId, expectedVersion, context.Actor, key), cancellationToken);
    }

    private const string ActionValues = "action must be Take, Progress, Complete, Fail, Release, Cancel or Confirm.";

    private static Guid RequireJobId(Guid jobId) => AutomationMcpErrors.RequireId(jobId, "job identifier");

    private static string Require(string? value, string action, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new McpException($"{action} needs {name}.")
            : value;

    private static AiJobTransitionAction ParseAction(string? action) =>
        Enum.TryParse<AiJobTransitionAction>(action?.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new McpException(ActionValues);

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
