using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Documents;
using Pegasus.Core.Eva;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Cases.Documents;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class ExportModel(
    IExportCaseBundle exportCaseBundle,
    ILogger<ExportModel> logger) : CaseMutationPageModel(logger)
{
    private const long MaximumArchiveBytes = 100L * 1024 * 1024;

    /// <summary>
    /// CASE-019: the case's own export — the EVA-format archive of its
    /// photographs and the thirteen mapped fields.
    ///
    /// A GET, because it is a read: no case version, no operation key, no edit
    /// lease. The operator asked for their file, not to change anything. The
    /// POST below is the separate selective export of chosen document
    /// versions, which does edit the case's document custody and keeps its
    /// lease.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            return NotFound();
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var export = await exportCaseBundle.ExecuteAsync(new(caseId, actor), cancellationToken);
            if (export is null)
            {
                return NotFound();
            }
            if (export.Bundle is not { } bundle
                || SafeArchiveName(bundle.FileName) is not { } fileName)
            {
                TempData["CaseError"] = export.BlockingReasons.Count > 0
                    ? string.Join(" ", export.BlockingReasons)
                    : "The case could not be exported.";
                return RedirectToDetails(caseId);
            }

            Response.Headers.CacheControl = "private, no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            return File(bundle.Content, "application/zip", fileName);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        // PLAT-039: an export reads every photograph out of Box, so a custody
        // transport failure is an ordinary way for it to fail. Without
        // HttpRequestException here the operator got the generic error page
        // instead of their case with a reason on it.
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or InvalidDataException
            or IOException
            or HttpRequestException
            or UnauthorizedAccessException)
        {
            LogDocumentExportFailed(logger, caseId, exception);
            TempData["CaseError"] = "The case could not be exported.";
            return RedirectToDetails(caseId);
        }
    }

    private static string? SafeArchiveName(string value) =>
        IsSafeArchiveName(value, Path.GetFileName(value)) ? value : null;

    private static bool IsSafeArchiveName(string original, string fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && fileName.Length <= 255
        && fileName is not "." and not ".."
        && fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
        && string.Equals(fileName, original, StringComparison.Ordinal)
        && !fileName.Contains('/', StringComparison.Ordinal)
        && !fileName.Contains('\\', StringComparison.Ordinal)
        && !fileName.Any(char.IsControl);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Case document export failed for case {CaseId}.")]
    private static partial void LogDocumentExportFailed(
        ILogger logger,
        Guid caseId,
        Exception exception);
}
