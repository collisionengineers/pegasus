using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Cases.Documents;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class DownloadModel(
    IDownloadCaseDocument downloadCaseDocument,
    ILogger<DownloadModel> logger) : StaffPageModel
{
    public async Task<IActionResult> OnGetAsync(
        Guid caseId,
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty || documentId == Guid.Empty || versionId == Guid.Empty)
        {
            return NotFound();
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var download = await downloadCaseDocument.ExecuteAsync(
                new(
                    caseId,
                    documentId,
                    versionId,
                    actor,
                    $"web-download:{Guid.NewGuid():N}"),
                cancellationToken);
            if (download is null)
            {
                return NotFound();
            }
            if (!TryValidateResponse(download, out var fileName, out var mediaType, out var sha256))
            {
                await download.DisposeAsync();
                LogUnsafeDocumentResponse(logger, caseId, documentId, versionId);
                return NotFound();
            }

            Response.Headers.CacheControl = "private, no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            Response.Headers["X-Content-SHA256"] = sha256;
            Response.ContentLength = download.ContentLength;
            return File(download.Content, mediaType, fileName);
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or InvalidDataException
            or IOException
            or UnauthorizedAccessException)
        {
            LogDocumentDownloadDenied(logger, caseId, documentId, versionId, exception);
            return NotFound();
        }
    }

    private static bool TryValidateResponse(
        DocumentDownload download,
        out string fileName,
        out string mediaType,
        out string sha256)
    {
        fileName = Path.GetFileName(download.FileName);
        mediaType = download.MediaType;
        sha256 = download.Sha256.ToLowerInvariant();
        return IsSafeFileName(download.FileName, fileName)
            && MediaTypeHeaderValue.TryParse(mediaType, out _)
            && download.ContentLength >= 0
            && sha256.Length == 64
            && sha256.All(char.IsAsciiHexDigit);
    }

    private static bool IsSafeFileName(string original, string fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && fileName.Length <= 255
        && fileName is not "." and not ".."
        && string.Equals(fileName, original, StringComparison.Ordinal)
        && !fileName.Contains('/', StringComparison.Ordinal)
        && !fileName.Contains('\\', StringComparison.Ordinal)
        && !fileName.Any(char.IsControl);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Case document download was denied for case {CaseId}, occurrence {OccurrenceId}, version {VersionId}.")]
    private static partial void LogDocumentDownloadDenied(
        ILogger logger,
        Guid caseId,
        Guid occurrenceId,
        Guid versionId,
        Exception exception);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Case document download returned unsafe metadata for case {CaseId}, occurrence {OccurrenceId}, version {VersionId}.")]
    private static partial void LogUnsafeDocumentResponse(
        ILogger logger,
        Guid caseId,
        Guid occurrenceId,
        Guid versionId);
}
