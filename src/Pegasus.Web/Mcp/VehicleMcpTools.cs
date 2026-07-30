using System.ComponentModel;
using ModelContextProtocol.Server;
using Pegasus.Core.Vehicle;

namespace Pegasus.Web.Mcp;

[McpServerToolType]
internal sealed class VehicleRequestLookupMcpTool(
    IRequestVehicleLookup requestVehicleLookup,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.VehicleRequestLookup,
        Title = "Request vehicle lookup",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Requests suggestion-only vehicle evidence through the accepted runtime adapter gate.")]
    public Task<StaffMcpResult<RequestedVehicleLookup>> ExecuteAsync(
        Guid caseId,
        long expectedCaseVersion,
        string registration,
        string editLeaseToken,
        string operationKey,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
            StaffMcpInput.RequireVersion(expectedCaseVersion, nameof(expectedCaseVersion));
            registration = StaffMcpInput.RequireText(registration, nameof(registration), 20);
            editLeaseToken = StaffMcpInput.RequireLease(editLeaseToken);
            operationKey = StaffMcpInput.RequireOperationKey(operationKey);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            return await requestVehicleLookup.ExecuteAsync(
                new(
                    caseId,
                    expectedCaseVersion,
                    registration,
                    staff.Actor,
                    operationKey,
                    editLeaseToken),
                cancellationToken);
        });
}

[McpServerToolType]
internal sealed class VehicleAcceptSuggestionMcpTool(
    IAcceptVehicleSuggestion acceptVehicleSuggestion,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.VehicleAcceptSuggestion,
        Title = "Accept vehicle suggestion",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Accepts or explicitly corrects one exact vehicle suggestion through the Core policy.")]
    public Task<StaffMcpResult<AcceptedVehicleSuggestion>> ExecuteAsync(
        Guid caseId,
        long expectedCaseVersion,
        Guid lookupObservationId,
        VehicleSuggestionDecision decision,
        VehicleConfirmationValues? correction,
        string editLeaseToken,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
            StaffMcpInput.RequireIdentifier(lookupObservationId, nameof(lookupObservationId));
            StaffMcpInput.RequireVersion(expectedCaseVersion, nameof(expectedCaseVersion));
            editLeaseToken = StaffMcpInput.RequireLease(editLeaseToken);
            operationKey = StaffMcpInput.RequireOperationKey(operationKey);
            reason = StaffMcpInput.RequireReason(reason);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            return await acceptVehicleSuggestion.ExecuteAsync(
                new(
                    caseId,
                    expectedCaseVersion,
                    lookupObservationId,
                    decision,
                    correction,
                    staff.Actor,
                    operationKey,
                    reason,
                    editLeaseToken),
                cancellationToken);
        });
}
