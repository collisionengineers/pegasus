using System.ComponentModel;
using ModelContextProtocol.Server;
using Pegasus.Core.Operations;

namespace Pegasus.Web.Mcp;

internal sealed record WorkCentreDismissToolResult(
    Guid RecordId,
    string? OperationKey);

/// <summary>
/// Work Centre Dismiss for the Automation Actor (FRD-15, ADR-0064): the same
/// Core command as the row's Dismiss, under <c>automation.cases</c>. Core takes
/// no key, so a caller's key only labels the call's audit row (R1042).
/// </summary>
[McpServerToolType]
internal sealed class WorkCentreMcpTools(
    DismissWorkCentreItem dismissItem,
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
    [Description("Dismisses a record from the Work Centre for everyone, as a row's Dismiss does: its rows leave Needs attention, New cases, System Actions and AI Actions until the record next qualifies (the next chase falls due, the Case is held again or re-enters Review, the Triage or job changes state, the item is reopened, or a later system action is recorded on that record). The record itself does not change and there is no undo. An optional mcp:-prefixed operation key labels the call's history.")]
    public async Task<WorkCentreDismissToolResult> DismissAsync(
        [Description("The record behind the row: a Case identifier (a Triage Case's too), an Unidentified item's id (pegasus_unidentified_get), an AI job's id (pegasus_ai_job_list), or a System Action's underlying receipt, image intake or query id.")] Guid recordId,
        [Description("Optional caller key prefixed 'mcp:' that labels the call's history.")] string? operationKey = null,
        CancellationToken cancellationToken = default)
    {
        return await auditor.CallWithOptionalKeyAsync(
            "pegasus_work_centre_dismiss",
            AutomationMcpAuditor.Resource(recordId),
            operationKey,
            (context, key) => DismissForAsync(context, recordId, key, cancellationToken),
            cancellationToken);
    }

    private async Task<WorkCentreDismissToolResult> DismissForAsync(
        AutomationActorContext context,
        Guid recordId,
        string? key,
        CancellationToken cancellationToken)
    {
        await dismissItem.ExecuteAsync(
            new(AutomationMcpErrors.RequireId(recordId, "record identifier"), context.Actor),
            cancellationToken);
        return new WorkCentreDismissToolResult(recordId, key);
    }
}
