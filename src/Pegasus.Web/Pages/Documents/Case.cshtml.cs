using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Documents;

namespace Pegasus.Web.Pages.Documents;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class CaseModel(
    IAddCaseDocument addCaseDocument,
    ICreateRequestUploadLink createRequestUploadLink,
    IRevokeRequestUploadLink revokeRequestUploadLink,
    ILogger<CaseModel> logger) : PageModel
{
    private const long MaximumStaffUploadBytes = 10 * 1024 * 1024;

    [BindProperty(SupportsGet = true)]
    public Guid CaseId { get; set; }

    [BindProperty]
    public IFormFile? Upload { get; set; }

    [BindProperty]
    public DocumentSemanticRole SemanticRole { get; set; } = DocumentSemanticRole.Other;

    [BindProperty]
    public string UploadOperationKey { get; set; } = string.Empty;

    [BindProperty]
    public string RequestOperationKey { get; set; } = string.Empty;

    [BindProperty]
    public Guid RequestId { get; set; }

    [BindProperty]
    public long RequestVersion { get; set; }

    [BindProperty]
    public string RevokeOperationKey { get; set; } = string.Empty;

    [BindProperty]
    public string RevokeReason { get; set; } = string.Empty;

    public string? StatusMessage { get; private set; }

    public string? RequestUploadUrl { get; private set; }

    public IReadOnlyList<DocumentSemanticRole> SemanticRoles { get; } = Enum.GetValues<DocumentSemanticRole>();

    public IActionResult OnGet()
    {
        if (CaseId == Guid.Empty)
        {
            return NotFound();
        }

        ResetOperationKeys();
        return Page();
    }

    public async Task<IActionResult> OnPostUploadAsync(CancellationToken cancellationToken)
    {
        PrepareOperationKeys();
        if (CaseId == Guid.Empty)
        {
            return NotFound();
        }

        if (!Guid.TryParseExact(UploadOperationKey, "N", out var operationId))
        {
            ModelState.AddModelError(string.Empty, "The upload operation is invalid. Reload the page and try again.");
        }

        if (Upload is null)
        {
            ModelState.AddModelError(nameof(Upload), "Choose a document to upload.");
        }
        else if (Upload.Length == 0)
        {
            ModelState.AddModelError(nameof(Upload), "The selected document is empty.");
        }
        else if (Upload.Length > MaximumStaffUploadBytes)
        {
            ModelState.AddModelError(nameof(Upload), "The selected document must be 10 MB or smaller.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        await using var content = new MemoryStream((int)Upload!.Length);
        await Upload.CopyToAsync(content, cancellationToken);

        try
        {
            var result = await addCaseDocument.ExecuteAsync(
                new(
                    CaseId,
                    Path.GetFileName(Upload.FileName),
                    string.IsNullOrWhiteSpace(Upload.ContentType)
                        ? "application/octet-stream"
                        : Upload.ContentType,
                    content.ToArray(),
                    SemanticRole,
                    DocumentSource.StaffUpload,
                    $"staff-upload:{operationId:N}",
                    actor,
                    $"staff-upload:{operationId:N}",
                    ExpectedCaseVersion: null),
                cancellationToken);

            StatusMessage = result.IsReplay
                ? "This document upload was already completed."
                : "The document was retained in case custody.";
            Upload = null;
            UploadOperationKey = NewOperationKey();
            return Page();
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or IOException
            or UnauthorizedAccessException
            or Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            LogCaseDocumentUploadFailed(logger, CaseId, exception);
            ModelState.AddModelError(string.Empty, "The document could not be retained for this case.");
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCreateRequestAsync(CancellationToken cancellationToken)
    {
        PrepareOperationKeys();
        if (CaseId == Guid.Empty)
        {
            return NotFound();
        }

        if (!Guid.TryParseExact(RequestOperationKey, "N", out var operationId))
        {
            ModelState.AddModelError(string.Empty, "The request operation is invalid. Reload the page and try again.");
            return Page();
        }

        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var result = await createRequestUploadLink.ExecuteAsync(
                new(CaseId, actor, $"request-upload:{operationId:N}"),
                cancellationToken);
            if (result.Secret is null)
            {
                StatusMessage = "This request was already created. Its secret link cannot be displayed again; create a new request if the original link was not retained.";
            }
            else
            {
                RequestUploadUrl = Url.Page(
                    "/Requests/Upload",
                    pageHandler: null,
                    values: new { token = result.Secret.Token },
                    protocol: Request.Scheme);
                StatusMessage = $"Upload request created. It expires at {result.Link.ExpiresAtUtc:u}. Copy the link now; it will not be shown again.";
                RequestId = result.Link.Id;
                RequestVersion = result.Link.Version;
                RevokeOperationKey = NewOperationKey();
            }

            RequestOperationKey = NewOperationKey();
            return Page();
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            LogDocumentRequestCreationFailed(logger, CaseId, exception);
            ModelState.AddModelError(string.Empty, "An upload request could not be created for this case.");
            return Page();
        }
    }

    public async Task<IActionResult> OnPostRevokeRequestAsync(CancellationToken cancellationToken)
    {
        PrepareOperationKeys();
        if (CaseId == Guid.Empty)
        {
            return NotFound();
        }

        var operationId = Guid.Empty;
        if (RequestId == Guid.Empty
            || RequestVersion <= 0
            || !Guid.TryParseExact(RevokeOperationKey, "N", out operationId))
        {
            ModelState.AddModelError(string.Empty, "The upload request details are invalid.");
        }

        if (string.IsNullOrWhiteSpace(RevokeReason))
        {
            ModelState.AddModelError(nameof(RevokeReason), "Enter a reason for revoking the request.");
        }
        else if (RevokeReason.Length > 2000)
        {
            ModelState.AddModelError(nameof(RevokeReason), "The revocation reason must be 2,000 characters or fewer.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            await revokeRequestUploadLink.ExecuteAsync(
                new(
                    RequestId,
                    actor,
                    RevokeReason.Trim(),
                    $"request-revoke:{operationId:N}",
                    RequestVersion),
                cancellationToken);
            StatusMessage = "The upload request was revoked.";
            RequestId = Guid.Empty;
            RequestVersion = 0;
            RevokeOperationKey = NewOperationKey();
            RevokeReason = string.Empty;
            return Page();
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            LogDocumentRequestRevocationFailed(logger, RequestId, exception);
            ModelState.AddModelError(string.Empty, "The upload request could not be revoked because it is unavailable or has changed.");
            return Page();
        }
    }

    private bool TryGetActor(out string actor)
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(subject, out var staffId) && staffId != Guid.Empty)
        {
            actor = $"staff:{staffId:D}";
            return true;
        }

        actor = string.Empty;
        return false;
    }

    private void PrepareOperationKeys()
    {
        if (string.IsNullOrWhiteSpace(UploadOperationKey))
        {
            UploadOperationKey = NewOperationKey();
        }

        if (string.IsNullOrWhiteSpace(RequestOperationKey))
        {
            RequestOperationKey = NewOperationKey();
        }

        if (string.IsNullOrWhiteSpace(RevokeOperationKey))
        {
            RevokeOperationKey = NewOperationKey();
        }
    }

    private void ResetOperationKeys()
    {
        UploadOperationKey = NewOperationKey();
        RequestOperationKey = NewOperationKey();
        RevokeOperationKey = NewOperationKey();
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Case document upload failed for case {CaseId}.")]
    private static partial void LogCaseDocumentUploadFailed(
        ILogger logger,
        Guid caseId,
        Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Document request creation failed for case {CaseId}.")]
    private static partial void LogDocumentRequestCreationFailed(
        ILogger logger,
        Guid caseId,
        Exception exception);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Document request revocation failed for request {RequestId}.")]
    private static partial void LogDocumentRequestRevocationFailed(
        ILogger logger,
        Guid requestId,
        Exception exception);

    private static string NewOperationKey() => Guid.NewGuid().ToString("N");
}
