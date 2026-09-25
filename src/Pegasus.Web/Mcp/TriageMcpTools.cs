using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Mcp;

internal sealed record TriageListToolResult(
    IReadOnlyList<TriageSummary> Items,
    string? NextCursor,
    int Limit,
    string CorrelationId);

internal sealed record TriageDetailToolResult(TriageDetail Detail, string CorrelationId);

internal sealed record TriageEditLeaseToolResult(
    Guid CaseId,
    string Reference,
    string EditLeaseToken,
    string Holder,
    long TriageVersion,
    DateTimeOffset ExpiresAtUtc,
    string OperationKey,
    string CorrelationId);

internal sealed record TriageEditReleaseToolResult(
    Guid CaseId,
    string Reference,
    bool Released,
    string OperationKey,
    string CorrelationId);

[McpServerToolType]
internal sealed class TriageMcpTools(
    IListTriagePage listTriage,
    IGetTriage getTriage,
    IGetIntakeSourceMetadata getSourceMetadata,
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
    IEditScopeLeases editScopes,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor)
{
    [McpServerTool(Name = "pegasus_triage_list", Title = "List Triage work", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Lists a bounded page of Triage records through the protected Core continuation query.")]
    public async Task<TriageListToolResult> ListAsync(
        string? state = null,
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
    public async Task<TriageDetailToolResult> GetAsync(Guid caseId, CancellationToken cancellationToken = default)
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

    [McpServerTool(Name = "pegasus_triage_source_download", Title = "Download Triage source", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Downloads the exact retained intake source for a Triage record, with integrity verification and bounded inline content.")]
    public async Task<IntakeSourceToolResult> DownloadSourceAsync(Guid caseId, int maxInlineBytes = 0, CancellationToken cancellationToken = default)
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
                return await IntakeSourceMcpContent.DownloadAsync(getSourceMetadata, downloadSource,
                    origin.ReceiptId, context.Actor, maxInlineBytes,
                    context.TraceIdentifier, cancellationToken);
            }), cancellationToken);
    }

    [McpServerTool(Name = "pegasus_triage_edit_begin", Title = "Begin Triage edit lease", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Claims the server-owned short-lived Triage edit lease that every Triage mutation must present. It uses the same ownership guard as staff editing and fails closed for another holder or a stale version.")]
    public async Task<TriageEditLeaseToolResult> EditBeginAsync(
        Guid caseId,
        long expectedVersion,
        string operationKey,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(context, "pegasus_triage_edit_begin", Resource(caseId), normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "Triage identifier");
                var lease = await editScopes.ClaimAsync(
                    new(EditScopeKind.Triage, caseId, expectedVersion, context.Actor, normalizedKey),
                    cancellationToken);
                return EditLeaseResult(
                    caseId, await ReferenceAsync(caseId, context, cancellationToken), lease, normalizedKey, context);
            }), cancellationToken);
    }

    [McpServerTool(Name = "pegasus_triage_edit_renew", Title = "Renew Triage edit lease", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Renews a Triage edit lease claimed with pegasus_triage_edit_begin. It fails closed when the automation actor is not the holder or the lease has expired.")]
    public async Task<TriageEditLeaseToolResult> EditRenewAsync(
        Guid caseId,
        string editLeaseToken,
        string operationKey,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordDenialAsync(context, "pegasus_triage_edit_renew", Resource(caseId), normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "Triage identifier");
                var lease = await editScopes.HeartbeatAsync(
                    new(EditScopeKind.Triage, caseId, context.Actor, RequireEditLeaseToken(editLeaseToken)),
                    cancellationToken);
                return EditLeaseResult(
                    caseId, await ReferenceAsync(caseId, context, cancellationToken), lease, normalizedKey, context);
            }), cancellationToken);
    }

    [McpServerTool(Name = "pegasus_triage_edit_end", Title = "End Triage edit lease", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Releases a Triage edit lease previously claimed with pegasus_triage_edit_begin.")]
    public async Task<TriageEditReleaseToolResult> EditEndAsync(
        Guid caseId,
        string editLeaseToken,
        string operationKey,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        var normalizedKey = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(context, "pegasus_triage_edit_end", Resource(caseId), normalizedKey,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "Triage identifier");
                await editScopes.ReleaseAsync(
                    new(EditScopeKind.Triage, caseId, context.Actor, normalizedKey,
                        RequireEditLeaseToken(editLeaseToken)),
                    cancellationToken);
                return new TriageEditReleaseToolResult(
                    caseId,
                    await ReferenceAsync(caseId, context, cancellationToken),
                    Released: true,
                    normalizedKey,
                    AutomationMcpAuditor.CorrelationId(context, normalizedKey));
            }), cancellationToken);
    }

    [McpServerTool(Name = "pegasus_triage_await_information", Title = "Mark Triage awaiting information", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> AwaitInformationAsync(Guid caseId, long expectedVersion, string editLeaseToken, string reason, string operationKey, CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_await_information", caseId, operationKey, editLeaseToken,
            (actor, key, token) => awaitInformation.ExecuteAsync(new(caseId, expectedVersion, actor, key, reason) { EditLeaseToken = token }, cancellationToken), cancellationToken);

    [McpServerTool(Name = "pegasus_triage_record_finding", Title = "Record Triage finding", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> RecordFindingAsync(Guid caseId, long expectedVersion, string editLeaseToken, string reason, RoadworthinessFinding? roadworthiness, AssessmentFinding? assessment, string operationKey, CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_record_finding", caseId, operationKey, editLeaseToken,
            (actor, key, token) => recordFinding.ExecuteAsync(new(caseId, expectedVersion, actor, key, reason, roadworthiness, assessment, null) { EditLeaseToken = token }, cancellationToken), cancellationToken);

    [McpServerTool(Name = "pegasus_triage_supersede_finding", Title = "Supersede Triage finding", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> SupersedeFindingAsync(Guid caseId, long expectedVersion, Guid supersedesFindingId, string editLeaseToken, string reason, RoadworthinessFinding? roadworthiness, AssessmentFinding? assessment, string operationKey, CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_supersede_finding", caseId, operationKey, editLeaseToken,
            (actor, key, token) => supersedeFinding.ExecuteAsync(new(caseId, expectedVersion, actor, key, reason, roadworthiness, assessment, supersedesFindingId) { EditLeaseToken = token }, cancellationToken), cancellationToken);

    [McpServerTool(Name = "pegasus_triage_response_link", Title = "Link Triage response evidence", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> LinkResponseAsync(Guid caseId, long expectedVersion, Guid pollOutcomeId, Guid sentEvidenceId, string editLeaseToken, string reason, string operationKey, CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_response_link", caseId, operationKey, editLeaseToken,
            async (actor, key, token) => { await linkResponse.ExecuteAsync(new(caseId, pollOutcomeId, sentEvidenceId, expectedVersion, actor, key, reason) { EditLeaseToken = token }, cancellationToken); }, cancellationToken);

    [McpServerTool(Name = "pegasus_triage_response_unlink", Title = "Unlink Triage response evidence", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> UnlinkResponseAsync(Guid caseId, long expectedVersion, Guid sentEvidenceId, string editLeaseToken, string reason, string operationKey, CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_response_unlink", caseId, operationKey, editLeaseToken,
            async (actor, key, token) => { await unlinkResponse.ExecuteAsync(new(caseId, sentEvidenceId, expectedVersion, actor, key, reason) { EditLeaseToken = token }, cancellationToken); }, cancellationToken);

    [McpServerTool(Name = "pegasus_triage_complete", Title = "Complete Triage", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> CompleteAsync(Guid caseId, long expectedVersion, string editLeaseToken, string reason, string operationKey, CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_complete", caseId, operationKey, editLeaseToken,
            (actor, key, token) => complete.ExecuteAsync(new(caseId, expectedVersion, actor, key, reason) { EditLeaseToken = token }, cancellationToken), cancellationToken);

    [McpServerTool(Name = "pegasus_triage_cancel", Title = "Cancel Triage", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> CancelAsync(Guid caseId, long expectedVersion, string editLeaseToken, string reason, string operationKey, CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_cancel", caseId, operationKey, editLeaseToken,
            (actor, key, token) => cancel.ExecuteAsync(new(caseId, expectedVersion, actor, key, reason) { EditLeaseToken = token }, cancellationToken), cancellationToken);

    [McpServerTool(Name = "pegasus_triage_reopen", Title = "Reopen Triage", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> ReopenAsync(Guid caseId, long expectedVersion, string editLeaseToken, string reason, string operationKey, CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_reopen", caseId, operationKey, editLeaseToken,
            (actor, key, token) => reopen.ExecuteAsync(new(caseId, expectedVersion, actor, key, reason) { EditLeaseToken = token }, cancellationToken), cancellationToken);

    [McpServerTool(Name = "pegasus_triage_case_link", Title = "Link Triage to case", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> LinkCaseAsync(Guid caseId, Guid instructionCaseId, long expectedTriageVersion, long expectedCaseVersion, string editLeaseToken, string caseEditLeaseToken, string reason, string operationKey, CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_case_link", caseId, operationKey, editLeaseToken,
            async (actor, key, token) => { await linkCase.ExecuteAsync(new(caseId, instructionCaseId, expectedTriageVersion, expectedCaseVersion, actor, key, reason, caseEditLeaseToken) { EditLeaseToken = token }, cancellationToken); }, cancellationToken);

    [McpServerTool(Name = "pegasus_triage_case_unlink", Title = "Unlink Triage from case", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    public Task<TriageDetailToolResult> UnlinkCaseAsync(Guid caseId, Guid instructionCaseId, long expectedTriageVersion, long expectedCaseVersion, string editLeaseToken, string caseEditLeaseToken, string reason, string operationKey, CancellationToken cancellationToken = default) =>
        MutateAsync("pegasus_triage_case_unlink", caseId, operationKey, editLeaseToken,
            async (actor, key, token) => { await unlinkCase.ExecuteAsync(new(caseId, instructionCaseId, expectedTriageVersion, expectedCaseVersion, actor, key, reason, caseEditLeaseToken) { EditLeaseToken = token }, cancellationToken); }, cancellationToken);

    private Task<TriageDetailToolResult> MutateAsync(
        string tool, Guid caseId, string operationKey, string editLeaseToken,
        Func<Pegasus.Core.Identity.ActionActor, string, string, Task> action,
        CancellationToken cancellationToken) =>
        MutateWithActorAsync(tool, caseId, operationKey, editLeaseToken, action, cancellationToken);

    private async Task<TriageDetailToolResult> MutateWithActorAsync(
        string tool, Guid caseId, string operationKey, string editLeaseToken,
        Func<Pegasus.Core.Identity.ActionActor, string, string, Task> action,
        CancellationToken cancellationToken)
    {
        var context = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(context, tool, Resource(caseId), key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                AutomationMcpErrors.RequireId(caseId, "Triage identifier");
                await action(context.Actor, key, RequireEditLeaseToken(editLeaseToken));
                var detail = await getTriage.ExecuteAsync(new(caseId, context.Actor), cancellationToken)
                    ?? throw new McpException("The updated Triage record was not found.");
                return new TriageDetailToolResult(
                    detail,
                    AutomationMcpAuditor.CorrelationId(context, key));
            }), cancellationToken);
    }

    private static string Resource(Guid id) => id == Guid.Empty ? "invalid" : id.ToString("D");

    private static string RequireEditLeaseToken(string editLeaseToken) =>
        string.IsNullOrWhiteSpace(editLeaseToken)
            ? throw new McpException("An active Triage edit lease token is required.")
            : editLeaseToken;

    /// <summary>The Triage Case's Case/PO, returned with its lease (decision V).</summary>
    private async Task<string> ReferenceAsync(
        Guid caseId,
        AutomationActorContext context,
        CancellationToken cancellationToken) =>
        (await getTriage.ExecuteAsync(new(caseId, context.Actor), cancellationToken)
            ?? throw new McpException("The Triage record was not found.")).Record.Reference;

    private static TriageEditLeaseToolResult EditLeaseResult(
        Guid caseId,
        string reference,
        EditScopeLease lease,
        string operationKey,
        AutomationActorContext context) => new(
        caseId,
        reference,
        lease.Token,
        lease.Holder,
        lease.RecordVersion,
        lease.ExpiresAtUtc,
        operationKey,
        AutomationMcpAuditor.CorrelationId(context, operationKey));
}
