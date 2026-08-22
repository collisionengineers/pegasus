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
    IExportCaseDocuments exportCaseDocuments,
    IExportCaseBundle exportCaseBundle,
    ILogger<ExportModel> logger) : CaseMutationPageModel(logger)
{
    private const int MaximumSelections = 100;
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
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or InvalidDataException
            or IOException
            or UnauthorizedAccessException)
        {
            LogDocumentExportFailed(logger, caseId, exception);
            TempData["CaseError"] = "The case could not be exported.";
            return RedirectToDetails(caseId);
        }
    }

    public async Task<IActionResult> OnPostAsync(
        Guid caseId,
        string[]? selection,
        long expectedVersion,
        string operationKey,
        string editLeaseToken,
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
        if (!Guid.TryParseExact(operationKey, "N", out var operationId)
            || !TryParseSelections(selection, out var selections))
        {
            TempData["CaseError"] = "Select one or more valid document versions to export.";
            PreserveLeaseState(caseId, editLeaseToken);
            return RedirectToDetails(caseId);
        }

        try
        {
            var export = await exportCaseDocuments.ExecuteAsync(
                new(
                    caseId,
                    selections,
                    actor,
                    operationId.ToString("N"),
                    MaximumArchiveBytes,
                    expectedVersion,
                    editLeaseToken),
                cancellationToken);
            var fileName = Path.GetFileName(export.FileName);
            if (!IsSafeArchiveName(export.FileName, fileName)
                || export.Manifest.Count != selections.Count
                || export.Manifest.Select(item => new DocumentExportSelection(item.OccurrenceId, item.VersionId))
                    .Distinct()
                    .Count() != selections.Count
                || export.Manifest.Any(item => item.ContentLength < 0
                    || item.Sha256.Length != 64
                    || !item.Sha256.All(char.IsAsciiHexDigit)
                    || !selections.Contains(new(item.OccurrenceId, item.VersionId))))
            {
                await export.DisposeAsync();
                LogUnsafeDocumentExport(logger, caseId);
                ClearLeaseState();
                TempData["CaseError"] = "The selected documents could not be exported safely.";
                return RedirectToDetails(caseId);
            }

            ClearLeaseState();
            Response.Headers.CacheControl = "private, no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            return File(export.Content, "application/zip", fileName);
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or InvalidDataException
            or IOException
            or UnauthorizedAccessException)
        {
            LogDocumentExportFailed(logger, caseId, exception);
            // A stale version keeps the lease here: the editor re-selects rather than reacquires.
            if (IsLeaseLoss(exception))
            {
                ClearLeaseState();
            }
            else
            {
                PreserveLeaseState(caseId, editLeaseToken);
            }
            TempData["CaseError"] =
                "The selected document versions are unavailable or the export could not be completed.";
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

    private static bool TryParseSelections(
        string[]? values,
        out IReadOnlyList<DocumentExportSelection> selections)
    {
        if (values is null || values.Length is < 1 or > MaximumSelections)
        {
            selections = [];
            return false;
        }

        var parsed = new List<DocumentExportSelection>(values.Length);
        foreach (var value in values)
        {
            var separator = value.IndexOf(':');
            if (separator <= 0
                || separator == value.Length - 1
                || value.IndexOf(':', separator + 1) >= 0
                || !Guid.TryParse(value.AsSpan(0, separator), out var occurrenceId)
                || !Guid.TryParse(value.AsSpan(separator + 1), out var versionId)
                || occurrenceId == Guid.Empty
                || versionId == Guid.Empty)
            {
                selections = [];
                return false;
            }

            parsed.Add(new(occurrenceId, versionId));
        }

        if (parsed.Count != parsed.Distinct().Count())
        {
            selections = [];
            return false;
        }

        selections = parsed;
        return true;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Case document export failed for case {CaseId}.")]
    private static partial void LogDocumentExportFailed(
        ILogger logger,
        Guid caseId,
        Exception exception);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Case document export returned unsafe metadata for case {CaseId}.")]
    private static partial void LogUnsafeDocumentExport(ILogger logger, Guid caseId);
}
