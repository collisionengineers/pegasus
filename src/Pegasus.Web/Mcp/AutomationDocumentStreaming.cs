using Pegasus.Core.Documents;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Mcp;

/// <summary>
/// The original bytes behind a download tool's <c>contentUrl</c>: the same
/// bearer token, the owning scope checked again, Case or receipt membership
/// re-resolved, and the exact SHA-256 as the ETag. There are no public signed
/// links; a client without the token gets the ordinary challenge.
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
        var actor = await resolver.RequireAsync(AutomationMcp.DocumentsScope, cancellationToken);
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
        IGetIntakeSourceMetadata metadataReader,
        IDownloadIntakeSource downloadSource,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var actor = await resolver.RequireAsync(AutomationMcp.IntakeScope, cancellationToken);
        var metadata = await metadataReader.ExecuteAsync(new(receiptId, actor.Actor), cancellationToken);
        if (metadata is null) return Results.NotFound();

        var download = await downloadSource.ExecuteAsync(new(receiptId, actor.Actor), cancellationToken);
        if (download is null
            || download.ContentLength != metadata.ContentLength
            || !string.Equals(download.Sha256, metadata.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound();
        }

        httpContext.Response.Headers.ETag = $"\"{metadata.Sha256}\"";
        return Results.File(
            download.Content.ToArray(),
            metadata.MediaType,
            metadata.FileName,
            enableRangeProcessing: true);
    }
}
