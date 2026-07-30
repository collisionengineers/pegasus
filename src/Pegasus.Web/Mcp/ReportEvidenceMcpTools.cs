using System.ComponentModel;
using ModelContextProtocol.Server;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Mcp;

[McpServerToolType]
internal sealed class ReportsLinkEvidenceMcpTool(
    ILinkReportEvidence linkReportEvidence,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.ReportsLinkEvidence,
        Title = "Link report evidence",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Links one exact retained approved-mailbox report-Sent evidence item to a case.")]
    public Task<StaffMcpResult<CaseMcpWorkflowSummary>> ExecuteAsync(
        Guid caseId,
        Guid evidenceId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ExecuteLinkAsync(
            caseId,
            evidenceId,
            expectedVersion,
            operationKey,
            reason,
            editLeaseToken,
            actorResolver,
            (staff, key, normalizedReason, lease) => linkReportEvidence.ExecuteAsync(
                new(
                    caseId,
                    expectedVersion,
                    staff.Actor,
                    key,
                    normalizedReason,
                    lease,
                    evidenceId),
                cancellationToken),
            cancellationToken);

    internal static Task<StaffMcpResult<CaseMcpWorkflowSummary>> ExecuteLinkAsync(
        Guid caseId,
        Guid evidenceId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        StaffMcpActorResolver actorResolver,
        Func<StaffMcpActor, string, string, string, Task<CaseWorkflowRecord>> execute,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
            StaffMcpInput.RequireIdentifier(evidenceId, nameof(evidenceId));
            StaffMcpInput.RequireVersion(expectedVersion, nameof(expectedVersion));
            operationKey = StaffMcpInput.RequireOperationKey(operationKey);
            reason = StaffMcpInput.RequireReason(reason);
            editLeaseToken = StaffMcpInput.RequireLease(editLeaseToken);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            return CaseMcpWorkflowSummary.From(
                await execute(staff, operationKey, reason, editLeaseToken));
        });
}

[McpServerToolType]
internal sealed class ReportsUnlinkEvidenceMcpTool(
    IUnlinkReportEvidence unlinkReportEvidence,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.ReportsUnlinkEvidence,
        Title = "Unlink report evidence",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Unlinks one exact retained report-Sent evidence item while preserving permanent history.")]
    public Task<StaffMcpResult<CaseMcpWorkflowSummary>> ExecuteAsync(
        Guid caseId,
        Guid evidenceId,
        long expectedVersion,
        string operationKey,
        string reason,
        string editLeaseToken,
        CancellationToken cancellationToken) =>
        ReportsLinkEvidenceMcpTool.ExecuteLinkAsync(
            caseId,
            evidenceId,
            expectedVersion,
            operationKey,
            reason,
            editLeaseToken,
            actorResolver,
            (staff, key, normalizedReason, lease) => unlinkReportEvidence.ExecuteAsync(
                new(
                    caseId,
                    expectedVersion,
                    staff.Actor,
                    key,
                    normalizedReason,
                    lease,
                    evidenceId),
                cancellationToken),
            cancellationToken);
}
