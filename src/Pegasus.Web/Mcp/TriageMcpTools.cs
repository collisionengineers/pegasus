using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Pegasus.Core;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;

namespace Pegasus.Web.Mcp;

internal sealed record TriageListToolResult(
    IReadOnlyList<TriageSummary> Items,
    string? NextCursor,
    int Limit,
    string CorrelationId);

internal sealed record TriageDetailToolResult(TriageDetail Detail, string CorrelationId);

internal enum TriageLinkAction
{
    Link,
    Unlink
}

/// <summary>
/// Triage automation (FRD-10 § Triage automation contract): every tool calls
/// the same Core query or command staff use on a Triage Case, under
/// <c>automation.intake</c>, identifying the Triage by its Case id. A mutation
/// presents a Triage lease from <c>pegasus_edit_begin</c>, or holds the lease
/// for its one command when none is presented, which is how staff change a
/// Triage too (FRD-14). Each mutation returns the updated Triage detail.
/// </summary>
[McpServerToolType]
internal sealed class TriageMcpTools(
    IListTriagePage listTriage,
    IGetTriage getTriage,
    IGetIntakeSourceMetadata getSourceMetadata,
    IDownloadIntakeSource downloadSource,
    IRenderImageForDelivery images,
    IExtractPdfPageText pdfText,
    IAwaitTriageInformation awaitInformation,
    IRecordTriageFinding recordFinding,
    ISupersedeTriageFinding supersedeFinding,
    ILinkTriageResponseEvidence linkResponse,
    IUnlinkTriageResponseEvidence unlinkResponse,
    ICompleteTriage complete,
    ICancelTriage cancel,
    IReopenTriage reopen,
    ILinkTriageCase linkCase,
    IUnlinkTriageCase unlinkCase,
    AutomationEditLease leases,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor)
{
    private const string TriageIdDescription = "The Triage Case identifier (caseId from pegasus_triage_list).";
    private const string VersionDescription = "The Triage version the caller observed; a stale value fails closed.";
    private const string KeyDescription = "Caller idempotency key prefixed 'mcp:'.";
    private const string ReasonDescription = "Why, for the Triage history (at most 500 characters).";
    private const string LeaseDescription = "Edit lease token from pegasus_edit_begin for multi-step work; omit it and the tool holds the Triage lease for this one command.";

    [McpServerTool(Name = "pegasus_triage_list", Title = "List Triage work", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists a bounded page of Triage records through the protected Core continuation query. Each item carries the Triage Case id, its t. reference, state and version.")]
    public async Task<TriageListToolResult> ListAsync(
        [Description("Optional state filter: Open, AwaitingInformation, FindingRecorded, Completed or Cancelled.")] string? state = null,
        [Description("Opaque cursor returned by the previous page; omit for the first page.")] string? cursor = null,
        [Description("Page size between 1 and 100; omit for 50.")] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        return await auditor.RecordAsync(context, "pegasus_triage_list", "triage", null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                TriageState? parsedState = null;
                if (!string.IsNullOrWhiteSpace(state))
                {
                    if (!Enum.TryParse<TriageState>(state.Trim(), true, out var parsed)
                        || !Enum.IsDefined(parsed))
                    {
                        throw new McpException("The Triage state is not recognized.");
                    }
                    parsedState = parsed;
                }
                var effectiveLimit = CursorPaging.NormalizeLimit(limit);
                var result = await listTriage.ExecuteAsync(
                    new(context.Actor, parsedState, cursor, effectiveLimit), cancellationToken);
                return new TriageListToolResult(
                    result.Items,
                    result.NextCursor,
                    effectiveLimit,
                    context.TraceIdentifier);
            }), cancellationToken);
    }

    [McpServerTool(Name = "pegasus_triage_get", Title = "Get Triage detail", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Gets one exact Triage record with findings, response evidence, candidates and immutable history.")]
    public async Task<TriageDetailToolResult> GetAsync(
        [Description(TriageIdDescription)] Guid caseId,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        return await auditor.RecordAsync(context, "pegasus_triage_get", Resource(caseId), null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "Triage identifier");
                var detail = await getTriage.ExecuteAsync(new(caseId, context.Actor), cancellationToken)
                    ?? throw new McpException("The Triage record was not found.");
                return new TriageDetailToolResult(detail, context.TraceIdentifier);
            }), cancellationToken);
    }

    [McpServerTool(Name = "pegasus_triage_source_download", Title = "Download Triage source", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns the retained origin source of a Triage record as native content: an image as a JPEG image block re-encoded to fit maxInlineBytes, a PDF as its page text, a small text file as text; anything else as metadata only. Every result carries the file name, media type, size, SHA-256 and an authenticated contentUrl for the original bytes (same bearer token, Intake scope).")]
    public async Task<CallToolResult> DownloadSourceAsync(
        [Description(TriageIdDescription)] Guid caseId,
        [Description("Byte budget for inline content; 0 selects 100 KiB, at most 10 MiB.")] int maxInlineBytes = 0,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        return await auditor.RecordAsync(context, "pegasus_triage_source_download", Resource(caseId), null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "Triage identifier");
                var detail = await getTriage.ExecuteAsync(new(caseId, context.Actor), cancellationToken)
                    ?? throw new McpException("The Triage record was not found.");
                var origin = detail.Record.Origin
                    ?? throw new McpException("The Triage record has no retained intake source.");
                return await IntakeSourceMcpContent.DownloadAsync(getSourceMetadata, downloadSource, images, pdfText,
                    origin.ReceiptId, context.Actor, maxInlineBytes,
                    context.TraceIdentifier, cancellationToken);
            }), cancellationToken);
    }

    [McpServerTool(Name = "pegasus_triage_await_information", Title = "Mark Triage awaiting information", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Marks the Triage Awaiting information, as the staff action does; it writes its own history text and takes no reason.")]
    public Task<TriageDetailToolResult> AwaitInformationAsync(
        [Description(TriageIdDescription)] Guid caseId,
        [Description(VersionDescription)] long expectedVersion,
        [Description(KeyDescription)] string operationKey,
        [Description(LeaseDescription)] string? editLeaseToken = null,
        CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_await_information", caseId, expectedVersion, operationKey, editLeaseToken,
            (actor, key, token) => awaitInformation.ExecuteAsync(new(caseId, expectedVersion, actor, key) { EditLeaseToken = token }, cancellationToken), cancellationToken);

    [McpServerTool(Name = "pegasus_triage_record_finding", Title = "Record Triage finding", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Records a Triage finding (roadworthiness and/or assessment) with a reason. Name supersedesFindingId to supersede an earlier finding instead of adding one; the earlier finding stays in history.")]
    public Task<TriageDetailToolResult> RecordFindingAsync(
        [Description(TriageIdDescription)] Guid caseId,
        [Description(VersionDescription)] long expectedVersion,
        [Description(ReasonDescription)] string reason,
        [Description(KeyDescription)] string operationKey,
        [Description("Roadworthy or Unroadworthy; omit when the finding is assessment only.")] RoadworthinessFinding? roadworthiness = null,
        [Description("The assessment finding; omit when the finding is roadworthiness only.")] AssessmentFinding? assessment = null,
        [Description("The earlier finding this one supersedes; omit to add a finding.")] Guid? supersedesFindingId = null,
        [Description(LeaseDescription)] string? editLeaseToken = null,
        CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_record_finding", caseId, expectedVersion, operationKey, editLeaseToken,
            (actor, key, token) => supersedesFindingId is { } supersedes
                ? supersedeFinding.ExecuteAsync(new(caseId, expectedVersion, actor, key, reason, roadworthiness, assessment, AutomationMcpErrors.RequireId(supersedes, "superseded finding identifier")) { EditLeaseToken = token }, cancellationToken)
                : recordFinding.ExecuteAsync(new(caseId, expectedVersion, actor, key, reason, roadworthiness, assessment, null) { EditLeaseToken = token }, cancellationToken),
            cancellationToken);

    [McpServerTool(Name = "pegasus_triage_response_evidence", Title = "Link or unlink Triage response evidence", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Link attaches exact sent evidence (and the poll outcome it answers) to the Triage as its response; Unlink detaches that evidence. The candidates are listed by pegasus_triage_get.")]
    public Task<TriageDetailToolResult> ResponseEvidenceAsync(
        [Description(TriageIdDescription)] Guid caseId,
        [Description(VersionDescription)] long expectedVersion,
        [Description("Link or Unlink.")] string action,
        [Description("The sent evidence identifier.")] Guid sentEvidenceId,
        [Description(ReasonDescription)] string reason,
        [Description(KeyDescription)] string operationKey,
        [Description("Link only: the poll outcome the evidence answers.")] Guid? pollOutcomeId = null,
        [Description(LeaseDescription)] string? editLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        var parsedAction = ParseAction(action);
        return MutateAsync("pegasus_triage_response_evidence", caseId, expectedVersion, operationKey, editLeaseToken,
            async (actor, key, token) =>
            {
                if (parsedAction == TriageLinkAction.Link)
                {
                    var outcome = pollOutcomeId
                        ?? throw new McpException("Link needs the pollOutcomeId the evidence answers.");
                    await linkResponse.ExecuteAsync(new(caseId, AutomationMcpErrors.RequireId(outcome, "poll outcome identifier"), sentEvidenceId, expectedVersion, actor, key, reason) { EditLeaseToken = token }, cancellationToken);
                }
                else
                {
                    await unlinkResponse.ExecuteAsync(new(caseId, sentEvidenceId, expectedVersion, actor, key, reason) { EditLeaseToken = token }, cancellationToken);
                }
            }, cancellationToken);
    }

    [McpServerTool(Name = "pegasus_triage_complete", Title = "Complete Triage", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Completes the Triage, as the staff action does; it writes its own history text and takes no reason.")]
    public Task<TriageDetailToolResult> CompleteAsync(
        [Description(TriageIdDescription)] Guid caseId,
        [Description(VersionDescription)] long expectedVersion,
        [Description(KeyDescription)] string operationKey,
        [Description(LeaseDescription)] string? editLeaseToken = null,
        CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_complete", caseId, expectedVersion, operationKey, editLeaseToken,
            (actor, key, token) => complete.ExecuteAsync(new(caseId, expectedVersion, actor, key) { EditLeaseToken = token }, cancellationToken), cancellationToken);

    [McpServerTool(Name = "pegasus_triage_cancel", Title = "Cancel Triage", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Cancels the Triage with a reason. A cancelled Triage can be reopened.")]
    public Task<TriageDetailToolResult> CancelAsync(
        [Description(TriageIdDescription)] Guid caseId,
        [Description(VersionDescription)] long expectedVersion,
        [Description(ReasonDescription)] string reason,
        [Description(KeyDescription)] string operationKey,
        [Description(LeaseDescription)] string? editLeaseToken = null,
        CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_cancel", caseId, expectedVersion, operationKey, editLeaseToken,
            (actor, key, token) => cancel.ExecuteAsync(new(caseId, expectedVersion, actor, key, reason) { EditLeaseToken = token }, cancellationToken), cancellationToken);

    [McpServerTool(Name = "pegasus_triage_reopen", Title = "Reopen Triage", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Reopens a completed or cancelled Triage with a reason.")]
    public Task<TriageDetailToolResult> ReopenAsync(
        [Description(TriageIdDescription)] Guid caseId,
        [Description(VersionDescription)] long expectedVersion,
        [Description(ReasonDescription)] string reason,
        [Description(KeyDescription)] string operationKey,
        [Description(LeaseDescription)] string? editLeaseToken = null,
        CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_reopen", caseId, expectedVersion, operationKey, editLeaseToken,
            (actor, key, token) => reopen.ExecuteAsync(new(caseId, expectedVersion, actor, key, reason) { EditLeaseToken = token }, cancellationToken), cancellationToken);

    [McpServerTool(Name = "pegasus_triage_case_link", Title = "Link or unlink Triage and instruction Case", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Link joins the Triage to the instruction Case it belongs with; Unlink separates them. Both records change, so both versions are checked. Present a lease token for each record held for multi-step work, or omit them and the tool holds each lease for this one command.")]
    public Task<TriageDetailToolResult> CaseLinkAsync(
        [Description(TriageIdDescription)] Guid caseId,
        [Description("The instruction Case identifier.")] Guid instructionCaseId,
        [Description("Link or Unlink.")] string action,
        [Description(VersionDescription)] long expectedTriageVersion,
        [Description("The instruction Case version the caller observed; a stale value fails closed.")] long expectedCaseVersion,
        [Description(ReasonDescription)] string reason,
        [Description(KeyDescription)] string operationKey,
        [Description(LeaseDescription)] string? editLeaseToken = null,
        [Description("The instruction Case's edit lease token from pegasus_edit_begin; omit it and the tool holds the Case lease for this one command.")] string? caseEditLeaseToken = null,
        CancellationToken cancellationToken = default)
    {
        var parsedAction = ParseAction(action);
        return MutateAsync("pegasus_triage_case_link", caseId, expectedTriageVersion, operationKey, editLeaseToken,
            (actor, key, token) => leases.RunCaseAsync(
                AutomationMcpErrors.RequireId(instructionCaseId, "instruction Case identifier"),
                expectedCaseVersion,
                caseEditLeaseToken,
                actor,
                key,
                async caseToken =>
                {
                    if (parsedAction == TriageLinkAction.Link)
                    {
                        await linkCase.ExecuteAsync(new(caseId, instructionCaseId, expectedTriageVersion, expectedCaseVersion, actor, key, reason, caseToken) { EditLeaseToken = token }, cancellationToken);
                    }
                    else
                    {
                        await unlinkCase.ExecuteAsync(new(caseId, instructionCaseId, expectedTriageVersion, expectedCaseVersion, actor, key, reason, caseToken) { EditLeaseToken = token }, cancellationToken);
                    }
                    return true;
                },
                cancellationToken),
            cancellationToken);
    }

    private async Task<TriageDetailToolResult> MutateAsync(
        string tool,
        Guid caseId,
        long expectedVersion,
        string operationKey,
        string? editLeaseToken,
        Func<ActionActor, string, string, Task> action,
        CancellationToken cancellationToken)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(context, tool, Resource(caseId), key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "Triage identifier");
                await leases.RunTriageAsync(
                    caseId,
                    expectedVersion,
                    editLeaseToken,
                    context.Actor,
                    key,
                    async token =>
                    {
                        await action(context.Actor, key, token);
                        return true;
                    },
                    cancellationToken);
                var detail = await getTriage.ExecuteAsync(new(caseId, context.Actor), cancellationToken)
                    ?? throw new McpException("The updated Triage record was not found.");
                return new TriageDetailToolResult(
                    detail,
                    AutomationMcpAuditor.CorrelationId(context, key));
            }), cancellationToken);
    }

    private static string Resource(Guid id) => id == Guid.Empty ? "invalid" : id.ToString("D");

    private static TriageLinkAction ParseAction(string? action) =>
        Enum.TryParse<TriageLinkAction>(action?.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new McpException("action must be Link or Unlink.");
}
