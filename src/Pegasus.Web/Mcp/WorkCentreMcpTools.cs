using System.ComponentModel;
using ModelContextProtocol.Server;
using Pegasus.Core.Operations;

namespace Pegasus.Web.Mcp;

internal sealed record WorkCentreDismissToolResult(
    Guid RecordId,
    string OperationKey,
    string CorrelationId);

/// <summary>
/// Work Centre Dismiss for the Automation Actor (FRD-15, ADR-0064): the same
/// Core command as the row's Dismiss, under <c>automation.cases</c>.
/// </summary>
[McpServerToolType]
internal sealed class WorkCentreMcpTools(
    IDismissWorkCentreItem dismissItem,
    AutomationActorResolver resolver,
    AutomationMcpAuditor auditor)
{
    [McpServerTool(
        Name = "pegasus_work_centre_dismiss",
        Title = "Dismiss a Work Centre record",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Dismisses a record from the Work Centre for everyone, as a row's Dismiss does: its rows leave Needs attention, New cases and AI jobs until the record next qualifies (the next chase falls due, the Case is held again or re-enters Review, the Triage or job changes state, the item is reopened). The record itself does not change and there is no undo. Requires a mcp:-prefixed operation key for the call's history.")]
    public async Task<WorkCentreDismissToolResult> DismissAsync(
        [Description("The record behind the row: a Case identifier (a Triage Case's too), an Unidentified item's id (pegasus_unidentified_get) or an AI job's id (pegasus_ai_job_list).")] Guid recordId,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync(AutomationMcp.CasesScope, cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        return await auditor.RecordAsync(
            context,
            "pegasus_work_centre_dismiss",
            recordId == Guid.Empty ? "invalid" : recordId.ToString("D"),
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
            {
                await dismissItem.ExecuteAsync(
                    new(AutomationMcpErrors.RequireId(recordId, "record identifier"), context.Actor),
                    cancellationToken);
                return new WorkCentreDismissToolResult(
                    recordId,
                    key,
                    AutomationMcpAuditor.CorrelationId(context, key));
            }),
            cancellationToken);
    }
}
