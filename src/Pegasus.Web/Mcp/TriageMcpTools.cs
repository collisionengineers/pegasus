using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;

namespace Pegasus.Web.Mcp;

internal sealed record TriageListToolResult(
    IReadOnlyList<TriageSummary> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    string CorrelationId);

internal sealed record TriageDetailToolResult(TriageDetail Detail, string CorrelationId);

[McpServerToolType]
internal sealed class TriageMcpTools(
    IListTriage listTriage,
    IGetTriage getTriage,
    IDownloadIntakeSource downloadSource,
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
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor)
{
    [McpServerTool(Name = "pegasus_triage_list", Title = "List Triage work", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists Triage records through the same paged Core query as the staff application.")]
    public async Task<TriageListToolResult> ListAsync(
        string? state = null,
        int page = 1,
        int pageSize = 25,
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
                var result = await listTriage.ExecuteAsync(
                    new(context.Actor, parsedState, page, pageSize), cancellationToken);
                return new TriageListToolResult(result.Items, result.Page, result.PageSize, result.TotalCount,
                    result.TotalPages, context.TraceIdentifier);
            }), cancellationToken);
    }

    [McpServerTool(Name = "pegasus_triage_get", Title = "Get Triage detail", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Gets one exact Triage record with findings, response evidence, candidates and immutable history.")]
    public async Task<TriageDetailToolResult> GetAsync(Guid triageId, CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        return await auditor.RecordAsync(context, "pegasus_triage_get", Resource(triageId), null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(triageId, "Triage identifier");
                var detail = await getTriage.ExecuteAsync(new(triageId, context.Actor), cancellationToken)
                    ?? throw new McpException("The Triage record was not found.");
                return new TriageDetailToolResult(detail, context.TraceIdentifier);
            }), cancellationToken);
    }

    [McpServerTool(Name = "pegasus_triage_source_download", Title = "Download Triage source", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Downloads the exact retained intake source for a Triage record, with integrity verification and bounded inline content.")]
    public async Task<IntakeSourceToolResult> DownloadSourceAsync(Guid triageId, int maxInlineBytes = 0, CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        return await auditor.RecordAsync(context, "pegasus_triage_source_download", Resource(triageId), null,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(triageId, "Triage identifier");
                var detail = await getTriage.ExecuteAsync(new(triageId, context.Actor), cancellationToken)
                    ?? throw new McpException("The Triage record was not found.");
                return await IntakeSourceMcpContent.DownloadAsync(downloadSource,
                    detail.Record.Origin.ReceiptId, context.Actor, maxInlineBytes,
                    context.TraceIdentifier, cancellationToken);
            }), cancellationToken);
    }

    [McpServerTool(Name = "pegasus_triage_await_information", Title = "Mark Triage awaiting information", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> AwaitInformationAsync(Guid triageId, long expectedVersion, string reason, string operationKey, CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_await_information", triageId, operationKey,
            (actor, key) => awaitInformation.ExecuteAsync(new(triageId, expectedVersion, actor, key, reason), cancellationToken), cancellationToken);

    [McpServerTool(Name = "pegasus_triage_record_finding", Title = "Record Triage finding", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> RecordFindingAsync(Guid triageId, long expectedVersion, string reason, RoadworthinessFinding? roadworthiness, AssessmentFinding? assessment, string operationKey, CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_record_finding", triageId, operationKey,
            (actor, key) => recordFinding.ExecuteAsync(new(triageId, expectedVersion, actor, key, reason, roadworthiness, assessment, null), cancellationToken), cancellationToken);

    [McpServerTool(Name = "pegasus_triage_supersede_finding", Title = "Supersede Triage finding", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> SupersedeFindingAsync(Guid triageId, long expectedVersion, Guid supersedesFindingId, string reason, RoadworthinessFinding? roadworthiness, AssessmentFinding? assessment, string operationKey, CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_supersede_finding", triageId, operationKey,
            (actor, key) => supersedeFinding.ExecuteAsync(new(triageId, expectedVersion, actor, key, reason, roadworthiness, assessment, supersedesFindingId), cancellationToken), cancellationToken);

    [McpServerTool(Name = "pegasus_triage_response_link", Title = "Link Triage response evidence", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> LinkResponseAsync(Guid triageId, long expectedVersion, Guid pollOutcomeId, Guid sentEvidenceId, string reason, string operationKey, CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_response_link", triageId, operationKey,
            async (actor, key) => { await linkResponse.ExecuteAsync(new(triageId, pollOutcomeId, sentEvidenceId, expectedVersion, actor, key, reason), cancellationToken); }, cancellationToken);

    [McpServerTool(Name = "pegasus_triage_response_unlink", Title = "Unlink Triage response evidence", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> UnlinkResponseAsync(Guid triageId, long expectedVersion, Guid sentEvidenceId, string reason, string operationKey, CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_response_unlink", triageId, operationKey,
            async (actor, key) => { await unlinkResponse.ExecuteAsync(new(triageId, sentEvidenceId, expectedVersion, actor, key, reason), cancellationToken); }, cancellationToken);

    [McpServerTool(Name = "pegasus_triage_complete", Title = "Complete Triage", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> CompleteAsync(Guid triageId, long expectedVersion, string reason, string operationKey, CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_complete", triageId, operationKey,
            (actor, key) => complete.ExecuteAsync(new(triageId, expectedVersion, actor, key, reason), cancellationToken), cancellationToken);

    [McpServerTool(Name = "pegasus_triage_cancel", Title = "Cancel Triage", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> CancelAsync(Guid triageId, long expectedVersion, string reason, string operationKey, CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_cancel", triageId, operationKey,
            (actor, key) => cancel.ExecuteAsync(new(triageId, expectedVersion, actor, key, reason), cancellationToken), cancellationToken);

    [McpServerTool(Name = "pegasus_triage_reopen", Title = "Reopen Triage", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> ReopenAsync(Guid triageId, long expectedVersion, string reason, string operationKey, CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_reopen", triageId, operationKey,
            (actor, key) => reopen.ExecuteAsync(new(triageId, expectedVersion, actor, key, reason), cancellationToken), cancellationToken);

    [McpServerTool(Name = "pegasus_triage_case_link", Title = "Link Triage to case", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> LinkCaseAsync(Guid triageId, Guid caseId, long expectedTriageVersion, long expectedCaseVersion, string caseEditLeaseToken, string reason, string operationKey, CancellationToken cancellationToken = default) =>
        MutateWithActorAsync("pegasus_triage_case_link", triageId, operationKey,
            async (actor, key) => { await linkCase.ExecuteAsync(new(triageId, caseId, expectedTriageVersion, expectedCaseVersion, actor, key, reason, caseEditLeaseToken), cancellationToken); }, cancellationToken);

    [McpServerTool(Name = "pegasus_triage_case_unlink", Title = "Unlink Triage from case", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> UnlinkCaseAsync(Guid triageId, Guid caseId, long expectedTriageVersion, long expectedCaseVersion, string caseEditLeaseToken, string reason, string operationKey, CancellationToken cancellationToken = default) =>
        MutateWithActorAsync("pegasus_triage_case_unlink", triageId, operationKey,
            async (actor, key) => { await unlinkCase.ExecuteAsync(new(triageId, caseId, expectedTriageVersion, expectedCaseVersion, actor, key, reason, caseEditLeaseToken), cancellationToken); }, cancellationToken);

    private Task<TriageDetailToolResult> MutateAsync(
        string tool, Guid triageId, string operationKey,
        Func<Pegasus.Core.Identity.ActionActor, string, Task> action,
        CancellationToken cancellationToken) =>
        MutateWithActorAsync(tool, triageId, operationKey, action, cancellationToken);

    private async Task<TriageDetailToolResult> MutateWithActorAsync(
        string tool, Guid triageId, string operationKey,
        Func<Pegasus.Core.Identity.ActionActor, string, Task> action,
        CancellationToken cancellationToken)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(context, tool, Resource(triageId), key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(triageId, "Triage identifier");
                await action(context.Actor, key);
                var detail = await getTriage.ExecuteAsync(new(triageId, context.Actor), cancellationToken)
                    ?? throw new McpException("The updated Triage record was not found.");
                return new TriageDetailToolResult(
                    detail,
                    AutomationMcpAuditor.CorrelationId(context, key));
            }), cancellationToken);
    }

    private static string Resource(Guid id) => id == Guid.Empty ? "invalid" : id.ToString("D");
}
