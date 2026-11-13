using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Mcp;

internal sealed record CaseQueryToolResult(
    string Action,
    Guid? QueryId,
    Guid? CaseId,
    long? CaseVersion,
    bool? HasOpenQuery,
    string OperationKey);

/// <summary>
/// Automation Actor query acts (FRD-10): open, mark done, reopen and assign,
/// through the same Core commands as the Case page. No edit lease, expected
/// version or reason. Reopen honours <see cref="CaseQueryRules.CanReopen"/>.
/// </summary>
[McpServerToolType]
internal sealed class CaseQueryMcpTools(
    IOpenCaseQuery openQuery,
    IMarkCaseQueryDone markDone,
    IReopenCaseQuery reopenQuery,
    IAssignCaseQuery assignQuery,
    ICaseWorkflowQueries workflows,
    AutomationMcpAuditor auditor)
{
    private static readonly string[] Actions = ["open", "mark_done", "reopen", "assign"];

    [McpServerTool(
        Name = "pegasus_case_query",
        Title = "Act on a case query",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("One query act, through the same Core command as the Case page, with no edit lease, expected version or reason. open logs a query (channel one of Email, Phone, WhatsApp or Other, optional details); a query on a linked message is pegasus_mail_open_query. A new query is Unassigned. mark_done closes one open query. reopen reopens one Done query while the Case is not archived or closed. assign sets the assignee (assigneeStaffId) or Unassigned when omitted. Needs a mcp:-prefixed operation key.")]
    public async Task<CaseQueryToolResult> ActAsync(
        [Description("open, mark_done, reopen or assign.")] string action,
        [Description(AutomationToolText.OperationKey)] string operationKey,
        [Description("open: the Case.")] Guid? caseId = null,
        [Description("mark_done, reopen and assign: the query.")] Guid? queryId = null,
        [Description("open of a logged query: Email, Phone, WhatsApp or Other.")] string? channel = null,
        [Description("open of a logged query: what the query is.")] string? details = null,
        [Description("assign: the staff identifier, from pegasus_vocabulary_get staff, or omit for Unassigned.")] Guid? assigneeStaffId = null,
        CancellationToken cancellationToken = default)
    {
        var subject = (queryId ?? caseId ?? Guid.Empty).ToString("D");
        return await auditor.CallAsync(
            "pegasus_case_query",
            subject,
            operationKey,
            async (context, key) =>
            {
                var verb = action?.Trim() ?? string.Empty;
                if (!Actions.Contains(verb, StringComparer.Ordinal))
                {
                    throw new McpException("The action must be one of: " + string.Join(", ", Actions) + ".");
                }

                var actor = context.Actor;
                switch (verb)
                {
                    case "open":
                    {
                        var id = AutomationMcpErrors.RequireId(caseId ?? Guid.Empty, "case identifier");
                        var opened = await openQuery.ExecuteAsync(
                            new(id, actor, key, null, ParseChannel(channel), details),
                            cancellationToken);
                        var workflow = await workflows.GetAsync(id, cancellationToken);
                        return new CaseQueryToolResult(
                            verb, opened, id, workflow?.Version, workflow?.HasOpenQuery, key);
                    }
                    case "mark_done":
                    {
                        var id = AutomationMcpErrors.RequireId(queryId ?? Guid.Empty, "query identifier");
                        var workflow = await markDone.ExecuteAsync(new(id, actor, key), cancellationToken);
                        return Result(verb, id, workflow, key);
                    }
                    case "reopen":
                    {
                        var id = AutomationMcpErrors.RequireId(queryId ?? Guid.Empty, "query identifier");
                        var workflow = await reopenQuery.ExecuteAsync(new(id, actor, key), cancellationToken);
                        return Result(verb, id, workflow, key);
                    }
                    default:
                    {
                        var id = AutomationMcpErrors.RequireId(queryId ?? Guid.Empty, "query identifier");
                        if (assigneeStaffId == Guid.Empty)
                        {
                            throw new McpException("An assignee identifier is required.");
                        }

                        var workflow = await assignQuery.ExecuteAsync(
                            new(id, actor, key, assigneeStaffId),
                            cancellationToken);
                        return Result(verb, id, workflow, key);
                    }
                }
            },
            cancellationToken);
    }

    private static CaseQueryToolResult Result(
        string action,
        Guid queryId,
        CaseWorkflowRecord workflow,
        string key) =>
        new(action, queryId, workflow.CaseId, workflow.Version, workflow.HasOpenQuery, key);

    private static CaseQueryChannel ParseChannel(string? value) =>
        AutomationMcpErrors.ParseName<CaseQueryChannel>(
            value,
            "open needs channel, one of: " + string.Join(", ", Enum.GetNames<CaseQueryChannel>()) + ".");
}
