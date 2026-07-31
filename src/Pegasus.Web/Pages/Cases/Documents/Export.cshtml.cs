using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Cases.Documents;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class ExportModel(
    IExportCaseDocuments exportCaseDocuments,
    ILogger<ExportModel> logger) : PageModel
{
    private const int MaximumSelections = 100;
    private const long MaximumArchiveBytes = 100L * 1024 * 1024;
    private const string LeaseTokenKey = "CaseLeaseToken";
    private const string LeaseCaseIdKey = "CaseLeaseCaseId";
    private static readonly string[] LeaseStateKeys =
    [
        LeaseTokenKey,
        LeaseCaseIdKey,
        "CaseClaimLeaseOperationKey",
        "CaseClaimLeaseCaseId",
        "CaseRenewLeaseOperationKey",
        "CaseReleaseLeaseOperationKey"
    ];

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
            StoreLeaseAuthority(caseId, editLeaseToken);
            return RedirectToPage("/Cases/Details", new { id = caseId });
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
                return RedirectToPage("/Cases/Details", new { id = caseId });
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
            if (exception is CaseEditLeaseExpiredException or CaseEditLeaseConflictException)
            {
                ClearLeaseState();
            }
            else
            {
                StoreLeaseAuthority(caseId, editLeaseToken);
            }
            TempData["CaseError"] =
                "The selected document versions are unavailable or the export could not be completed.";
            return RedirectToPage("/Cases/Details", new { id = caseId });
        }
    }

    private static bool IsSafeArchiveName(string original, string fileName) =>
        !string.IsNullOrWhiteSpace(fileName)
        && fileName.Length <= 255
        && fileName is not "." and not ".."
        && fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
        && string.Equals(fileName, original, StringComparison.Ordinal)
        && !fileName.Contains('/', StringComparison.Ordinal)
        && !fileName.Contains('\\', StringComparison.Ordinal)
        && !fileName.Any(char.IsControl);

    private void StoreLeaseAuthority(Guid caseId, string leaseToken)
    {
        if (!string.IsNullOrWhiteSpace(leaseToken))
        {
            TempData[LeaseCaseIdKey] = caseId.ToString("D");
            TempData[LeaseTokenKey] = leaseToken;
        }
    }

    private void ClearLeaseState()
    {
        foreach (var key in LeaseStateKeys)
        {
            TempData.Remove(key);
        }
    }

    private bool TryGetActor(out ActionActor actor)
    {
        if (StaffActorFactory.TryCreate(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var resolved))
        {
            actor = resolved;
            return true;
        }

        actor = null!;
        return false;
    }

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
