using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Mcp;

[McpServerToolType]
internal sealed class CaseWorkflowMcpTools(
    ICaseWorkflowQueries queries,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = "pegasus_case_workflow_get",
        Title = "Get case workflow",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Gets the durable workflow, immutable identity, evidence links, due work, and optimistic version for one accepted case. Mutations remain in the authenticated staff Web caller because the current MCP contract permits only idempotent, non-destructive tools and cannot safely issue an edit lease.")]
    public async Task<CaseWorkflowRecord> GetAsync(Guid caseId, CancellationToken cancellationToken)
    {
        await actorResolver.RequireAsync(StaffMcpPolicies.ReadScope, cancellationToken);
        if (caseId == Guid.Empty)
        {
            throw new McpException("'caseId' must be a non-empty identifier.");
        }

        return await queries.GetAsync(caseId, cancellationToken)
            ?? throw new McpException("The case workflow was not found.");
    }
}
