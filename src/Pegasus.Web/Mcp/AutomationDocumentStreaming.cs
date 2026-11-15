using ModelContextProtocol;
using Pegasus.Core.Documents;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Mcp;

/// <summary>
/// The original bytes behind a download tool's <c>contentUrl</c>: the same
/// bearer token, the owning scope checked again, Case or receipt membership
/// re-resolved, and the exact SHA-256 as the ETag. There are no public signed
/// links; a client without the token gets the ordinary challenge, and a
/// stopped client's still-valid token gets 403.
/// </summary>
internal static class AutomationDocumentStreaming
{
    public static async Task<IResult> GetAsync(
        Guid occurrenceId,
        Guid versionId,
        Guid caseId,
        AutomationActorResolver resolver,
        IGetCaseDocumentMetadata metadataReader,
        IReadLogicalDocumentVersion contentReader,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var actor = await TryRequireScopeAsync(resolver, AutomationMcp.DocumentsScope, cancellationToken);
        if (actor is null) return Results.StatusCode(StatusCodes.Status403Forbidden);
        var metadata = await metadataReader.ExecuteAsync(
            new(caseId, occurrenceId, versionId, actor.Actor), cancellationToken);
        if (metadata is null) return Results.NotFound();

        var content = await contentReader.OpenAsync(
            new(
                Actor: actor.Actor,
                DocumentId: metadata.DocumentId,
                VersionId: versionId,
                IntakeAssetId: null,
                CaseId: caseId,
                IntakeReceiptId: null,
                ExpectedSha256: metadata.Sha256,
                ExpectedContentLength: metadata.ContentLength),
            cancellationToken);
        httpContext.Response.Headers.ETag = $"\"{metadata.Sha256}\"";
        return Results.Stream(
            content.Content,
            metadata.MediaType,
            metadata.FileName,
            enableRangeProcessing: true);
    }

    public static async Task<IResult> GetIntakeSourceAsync(
        Guid receiptId,
        AutomationActorResolver resolver,
        GetIntakeSourceMetadata metadataReader,
        IDownloadIntakeSource downloadSource,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var actor = await TryRequireScopeAsync(resolver, AutomationMcp.IntakeScope, cancellationToken);
        if (actor is null) return Results.StatusCode(StatusCodes.Status403Forbidden);
        var authorized = await metadataReader.ExecuteAsync(new(receiptId, actor.Actor), cancellationToken);
        if (authorized is null) return Results.NotFound();

        var metadata = authorized.Metadata;
        var download = await downloadSource.ExecuteAsync(authorized, actor.Actor, cancellationToken);
        if (download.ContentLength != metadata.ContentLength
            || !string.Equals(download.Sha256, metadata.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound();
        }

        httpContext.Response.Headers.ETag = $"\"{metadata.Sha256}\"";
        return Results.Bytes(
            download.Content,
            metadata.MediaType,
            metadata.FileName,
            enableRangeProcessing: true);
    }

    /// <summary>
    /// The resolver refuses a stopped client, whose token is still valid, by
    /// throwing for the MCP transport to answer, and records the refusal. A
    /// plain HTTP route has no such answer, so it is a 403 here.
    /// </summary>
    private static async Task<AutomationActorContext?> TryRequireScopeAsync(
        AutomationActorResolver resolver,
        string requiredScope,
        CancellationToken cancellationToken)
    {
        try
        {
            return await resolver.RequireScopeAsync(requiredScope, cancellationToken);
        }
        catch (McpException)
        {
            return null;
        }
    }
}
