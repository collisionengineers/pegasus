using System.ComponentModel;
using System.Globalization;
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
    string OperationKey,
    string CorrelationId);

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
    AutomationActorResolver resolver,
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
    [Description("One query act, through the same Core command as the Case page, with no edit lease, expected version or reason. open logs a query (channel one of Email, Phone, WhatsApp or Other, optional details) or opens one on a linked message (messageId); a new query is Unassigned. mark_done closes one open query. reopen reopens one Done query while the Case is not archived or closed. assign sets the assignee (assigneeStaffId) or Unassigned when omitted. Needs a mcp:-prefixed operation key.")]
    public async Task<CaseQueryToolResult> ActAsync(
        [Description("open, mark_done, reopen or assign.")] string action,
        [Description("Caller idempotency key prefixed 'mcp:'.")] string operationKey,
        [Description("open: the Case.")] Guid? caseId = null,
        [Description("mark_done, reopen and assign: the query.")] Guid? queryId = null,
        [Description("open from a linked e-mail: the retained message. Omit it to log a query by channel.")] Guid? messageId = null,
        [Description("open of a logged query: Email, Phone, WhatsApp or Other.")] string? channel = null,
        [Description("open of a logged query: what the query is.")] string? details = null,
        [Description("assign: the staff identifier, or omit for Unassigned.")] Guid? assigneeStaffId = null,
        CancellationToken cancellationToken = default)
    {
        var context = await resolver.RequireAsync("pegasus_case_query", cancellationToken);
        var key = AutomationMcpErrors.RequireOperationKey(operationKey);
        var subject = (queryId ?? caseId ?? Guid.Empty).ToString("D");
        return await auditor.RecordAsync(
            context,
            "pegasus_case_query",
            subject,
            key,
            () => AutomationMcpErrors.ExecuteAsync(async () =>
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
                            new(id, actor, key, messageId, ParseChannel(channel, messageId), details),
                            cancellationToken);
                        var workflow = await workflows.GetAsync(id, cancellationToken);
                        return new CaseQueryToolResult(
                            verb, opened, id, workflow?.Version, workflow?.HasOpenQuery, key,
                            AutomationMcpAuditor.CorrelationId(context, key));
                    }
                    case "mark_done":
                    {
                        var id = AutomationMcpErrors.RequireId(queryId ?? Guid.Empty, "query identifier");
                        var workflow = await markDone.ExecuteAsync(new(id, actor, key), cancellationToken);
                        return Result(verb, id, workflow, key, context);
                    }
                    case "reopen":
                    {
                        var id = AutomationMcpErrors.RequireId(queryId ?? Guid.Empty, "query identifier");
                        var workflow = await reopenQuery.ExecuteAsync(new(id, actor, key), cancellationToken);
                        return Result(verb, id, workflow, key, context);
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
                        return Result(verb, id, workflow, key, context);
                    }
                }
            }),
            cancellationToken);
    }

    private static CaseQueryToolResult Result(
        string action,
        Guid queryId,
        CaseWorkflowRecord workflow,
        string key,
        AutomationActorContext context) =>
        new(action, queryId, workflow.CaseId, workflow.Version, workflow.HasOpenQuery, key,
            AutomationMcpAuditor.CorrelationId(context, key));

    private static CaseQueryChannel? ParseChannel(string? value, Guid? messageId)
    {
        if (messageId is not null)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                throw new McpException("open takes a message or a channel, not both.");
            }

            return null;
        }

        return Enum.TryParse<CaseQueryChannel>(value?.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
            && !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? parsed
            : throw new McpException(
                "open needs channel, one of: " + string.Join(", ", Enum.GetNames<CaseQueryChannel>()) + ", or a messageId.");
    }
}
