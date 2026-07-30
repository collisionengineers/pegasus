using System.ComponentModel;
using ModelContextProtocol.Server;
using Pegasus.Core.Documents;

namespace Pegasus.Web.Mcp;

internal sealed record BoxFileRequestMcpResult(
    BoxFileRequest FileRequest,
    bool IsReplay);

internal sealed record RequestUploadLinkMcpResult(
    Guid Id,
    Guid CaseId,
    RequestUploadStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc,
    int AcceptedFileCount,
    long AcceptedByteCount,
    string LimitsVersion,
    long Version,
    bool IsReplay)
{
    public static RequestUploadLinkMcpResult From(
        RequestUploadLink link,
        bool isReplay)
    {
        ArgumentNullException.ThrowIfNull(link);
        return new(
            link.Id,
            link.CaseId,
            link.Status,
            link.CreatedAtUtc,
            link.ExpiresAtUtc,
            link.RevokedAtUtc,
            link.AcceptedFileCount,
            link.AcceptedByteCount,
            link.LimitsVersion,
            link.Version,
            isReplay);
    }
}

[McpServerToolType]
internal sealed class RequestsCreateBoxMcpTool(
    ICreateBoxFileRequest createBoxFileRequest,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.RequestsCreateBox,
        Title = "Create Box file request",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Creates the approved case-scoped Box file request without returning its bearer URL.")]
    public Task<StaffMcpResult<BoxFileRequestMcpResult>> ExecuteAsync(
        Guid caseId,
        long expectedCaseVersion,
        string editLeaseToken,
        string operationKey,
        DateTimeOffset? expiresAtUtc,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            ValidateCaseRequest(
                caseId,
                expectedCaseVersion,
                ref editLeaseToken,
                ref operationKey);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            var result = await createBoxFileRequest.ExecuteAsync(
                new(
                    caseId,
                    staff.Actor,
                    operationKey,
                    expiresAtUtc,
                    expectedCaseVersion,
                    editLeaseToken),
                cancellationToken);
            return new BoxFileRequestMcpResult(result.FileRequest, result.IsReplay);
        });

    internal static void ValidateCaseRequest(
        Guid caseId,
        long expectedCaseVersion,
        ref string editLeaseToken,
        ref string operationKey)
    {
        StaffMcpInput.RequireIdentifier(caseId, nameof(caseId));
        StaffMcpInput.RequireVersion(expectedCaseVersion, nameof(expectedCaseVersion));
        editLeaseToken = StaffMcpInput.RequireLease(editLeaseToken);
        operationKey = StaffMcpInput.RequireOperationKey(operationKey);
    }
}

[McpServerToolType]
internal sealed class RequestsRevokeBoxMcpTool(
    IRevokeBoxFileRequest revokeBoxFileRequest,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.RequestsRevokeBox,
        Title = "Revoke Box file request",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Deactivates an exact case-scoped Box file request without deleting retained evidence.")]
    public Task<StaffMcpResult<BoxFileRequest>> ExecuteAsync(
        Guid caseId,
        Guid fileRequestId,
        long expectedFileRequestVersion,
        long expectedCaseVersion,
        string editLeaseToken,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            RequestsCreateBoxMcpTool.ValidateCaseRequest(
                caseId,
                expectedCaseVersion,
                ref editLeaseToken,
                ref operationKey);
            StaffMcpInput.RequireIdentifier(fileRequestId, nameof(fileRequestId));
            StaffMcpInput.RequireVersion(
                expectedFileRequestVersion,
                nameof(expectedFileRequestVersion));
            reason = StaffMcpInput.RequireReason(reason);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            return await revokeBoxFileRequest.ExecuteAsync(
                new(
                    caseId,
                    fileRequestId,
                    staff.Actor,
                    reason,
                    operationKey,
                    expectedFileRequestVersion,
                    expectedCaseVersion,
                    editLeaseToken),
                cancellationToken);
        });
}

[McpServerToolType]
internal sealed class RequestsCreateUploadMcpTool(
    ICreateRequestUploadLink createUploadLink,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.RequestsCreateUpload,
        Title = "Create Pegasus upload request",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Creates a case-scoped upload request only when an explicit accepted limits policy is active; no token is returned.")]
    public Task<StaffMcpResult<RequestUploadLinkMcpResult>> ExecuteAsync(
        Guid caseId,
        long expectedCaseVersion,
        string editLeaseToken,
        string operationKey,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            RequestsCreateBoxMcpTool.ValidateCaseRequest(
                caseId,
                expectedCaseVersion,
                ref editLeaseToken,
                ref operationKey);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            var result = await createUploadLink.ExecuteAsync(
                new(
                    caseId,
                    staff.Actor,
                    operationKey,
                    expectedCaseVersion,
                    editLeaseToken),
                cancellationToken);
            return RequestUploadLinkMcpResult.From(result.Link, result.IsReplay);
        });
}

[McpServerToolType]
internal sealed class RequestsRevokeUploadMcpTool(
    IRevokeRequestUploadLink revokeUploadLink,
    StaffMcpActorResolver actorResolver)
{
    [McpServerTool(
        Name = AlphaMcpToolNames.RequestsRevokeUpload,
        Title = "Revoke Pegasus upload request",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Revokes an exact case-scoped upload request using current case/request versions and the active lease.")]
    public Task<StaffMcpResult<StaffMcpMutationReceipt>> ExecuteAsync(
        Guid caseId,
        Guid requestId,
        long expectedRequestVersion,
        long expectedCaseVersion,
        string editLeaseToken,
        string operationKey,
        string reason,
        CancellationToken cancellationToken) =>
        StaffMcpCall.ExecuteAsync(async () =>
        {
            RequestsCreateBoxMcpTool.ValidateCaseRequest(
                caseId,
                expectedCaseVersion,
                ref editLeaseToken,
                ref operationKey);
            StaffMcpInput.RequireIdentifier(requestId, nameof(requestId));
            StaffMcpInput.RequireVersion(expectedRequestVersion, nameof(expectedRequestVersion));
            reason = StaffMcpInput.RequireReason(reason);
            var staff = await actorResolver.RequireAsync(
                StaffMcpPolicies.WriteScope,
                cancellationToken);
            await revokeUploadLink.ExecuteAsync(
                new(
                    caseId,
                    requestId,
                    staff.Actor,
                    reason,
                    operationKey,
                    expectedRequestVersion,
                    expectedCaseVersion,
                    editLeaseToken),
                cancellationToken);
        });
}
