using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Pegasus.Core.Triage;

namespace Pegasus.Web.Mcp;

[McpServerToolType]
internal sealed class TriageMcpTools(
    ITriageQueries queries,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = "pegasus_triage_list",
        Title = "List Triage work",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Lists the durable Triage queue, optionally filtered by its exact lifecycle state.")]
    public async Task<IReadOnlyList<TriageSummary>> ListAsync(
        [Description("Optional exact Triage lifecycle-state filter.")] TriageState? state,
        CancellationToken cancellationToken)
    {
        await actorResolver.RequireAsync(StaffMcpPolicies.ReadScope, cancellationToken);
        if (state is not null && !Enum.IsDefined(state.Value))
        {
            throw new McpException("The Triage state is not recognized.");
        }

        return await queries.ListAsync(state, cancellationToken);
    }

    [McpServerTool(
        Name = "pegasus_triage_get",
        Title = "Get Triage detail",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false)]
    [Description("Gets one durable Triage record with findings and linked sent-response evidence.")]
    public async Task<TriageDetail> GetAsync(
        [Description("The durable Triage identifier.")] Guid triageId,
        CancellationToken cancellationToken)
    {
        await actorResolver.RequireAsync(StaffMcpPolicies.ReadScope, cancellationToken);
        if (triageId == Guid.Empty)
        {
            throw new McpException("'triageId' must be a non-empty identifier.");
        }

        return await queries.GetAsync(triageId, cancellationToken)
            ?? throw new McpException("The Triage record was not found.");
    }
}
