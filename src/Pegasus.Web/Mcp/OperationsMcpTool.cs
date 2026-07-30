using System.ComponentModel;
using ModelContextProtocol.Server;
using Pegasus.Core.Operations;

namespace Pegasus.Web.Mcp;

[McpServerToolType]
internal sealed class OperationsGetMcpTool(
    IGetOperationsSnapshot getOperationsSnapshot,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.OperationsGet,
        Title = "Get operations snapshot",
        ReadOnly = true,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Gets one bounded, current operations snapshot for the authenticated staff member.")]
    public Task<StaffMcpResult<OperationsSnapshot>> ExecuteAsync(
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.ReadScope,
                cancellationToken);
            return await getOperationsSnapshot.ExecuteAsync(
                staff.Actor,
                cancellationToken);
        });
}
