using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages;

/// <summary>
/// Manual submission, on a surface of its own.
/// </summary>
/// <remarks>
/// This was a panel above the Inbox list whose button posted to
/// <c>action=""</c>: the handler URL was never generated, so the browser POSTed
/// to the page with no handler, nothing matched, and Razor Pages silently
/// re-rendered. HTTP 200, no receipt, no work item, no error shown — the only
/// manual submission path in the product was a dead button.
///
/// The route is declared here as a plain page with an unnamed handler, so the
/// form posts to its own URL and there is no handler name to fail to generate.
/// </remarks>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class UploadModel(
    IIntakeSubmission intakeSubmission,
    IIntakeReceiptQueries receiptQueries,
    TimeProvider timeProvider) : PageModel
{
    public static string MaximumSizeLabel =>
        OperatorLabels.FileSize(IntakeEnvelopeLimits.MaximumContentLength);

    [BindProperty]
    public IFormFile? Upload { get; set; }

    [BindProperty]
    public string ExternalReceiptToken { get; set; } = string.Empty;

    /// <summary>
    /// The sentence describing what happened to a file that is still being
    /// processed, carried across the redirect.
    /// </summary>
    public string? OutcomeMessage { get; private set; }

    public void OnGet()
    {
        ExternalReceiptToken = Guid.NewGuid().ToString("N");
        OutcomeMessage = TempData["UploadOutcomeMessage"] as string;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // The upload receipt is the replay key. A malformed one means the form
        // state cannot be trusted, so the post is refused rather than quietly
        // given a fresh key — which would turn a replay into a second receipt.
        if (Guid.TryParseExact(ExternalReceiptToken, "N", out var token))
        {
            ExternalReceiptToken = token.ToString("N");
        }
        else
        {
            ModelState.AddModelError(
                string.Empty,
                "The upload receipt is invalid. Refresh the page and try again.");
        }

        if (Upload is null)
        {
            ModelState.AddModelError(nameof(Upload), "Choose a file to upload.");
        }
        else if (Upload.Length == 0)
        {
            ModelState.AddModelError(nameof(Upload), "That file is empty.");
        }
        else if (Upload.Length > IntakeEnvelopeLimits.MaximumContentLength)
        {
            // Stated as the size the operator chose, against the limit, rather
            // than as a rejection they have to work out for themselves.
            ModelState.AddModelError(
                nameof(Upload),
                $"This file is {OperatorLabels.FileSize(Upload.Length)}. "
                + $"Files must be {MaximumSizeLabel} or smaller.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (!StaffActorFactory.TryCreate(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            return Forbid();
        }

        await using var memory = new MemoryStream((int)Upload!.Length);
        await Upload.CopyToAsync(memory, cancellationToken);
        var fileName = Path.GetFileName(Upload.FileName);
        try
        {
            var result = await intakeSubmission.ExecuteAsync(
                new(
                    fileName,
                    string.IsNullOrWhiteSpace(Upload.ContentType)
                        ? "application/octet-stream"
                        : Upload.ContentType,
                    memory.ToArray(),
                    timeProvider.GetUtcNow(),
                    $"staff:{actor.SubjectId}",
                    new(IntakeSourceChannel.ManualUpload, ExternalReceiptToken)),
                $"manual-upload:{ExternalReceiptToken}",
                cancellationToken);

            // Post-redirect-get, landing on what the upload produced. The
            // operator used to be told "The instruction has been retained and
            // queued for processing" while the list below still read "No
            // intake receipts match this view" — the item existed nowhere they
            // could see. Now the confirmation and the thing itself arrive
            // together.
            var outcome = await DescribeAsync(result, fileName, cancellationToken);
            TempData["UploadOutcomeMessage"] = outcome.Message;

            // Queued work has not been processed yet, so there is no item to
            // open: the receipt does not exist until the Worker writes it.
            // Saying so on this page is the honest answer. Sending the operator
            // to a record that is not there yet would be a 404 dressed as a
            // success.
            if (result.Disposition == IntakeSubmissionDisposition.Queued)
            {
                return RedirectToPage(new { received = result.ReceiptId });
            }

            return outcome.CaseId is { } createdCaseId
                ? RedirectToPage("/Cases/Details", new { id = createdCaseId })
                : RedirectToPage(
                    "/Intake/Details",
                    new { id = result.ReceiptId, duplicate = result.IsDuplicate });
        }
        catch (IntakeSourceIdentityConflictException)
        {
            ModelState.AddModelError(
                string.Empty,
                "That upload was already used for a different file. Try again.");
        }
        catch (IntakeArtifactRetentionException)
        {
            ModelState.AddModelError(
                string.Empty,
                "The file could not be stored. Try again, or contact an administrator if it keeps failing.");
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            ModelState.AddModelError(
                string.Empty,
                "The file could not be processed. Try again, or contact an administrator if it keeps failing.");
        }

        return Page();
    }

    /// <summary>
    /// What actually happened to the file, in the operator's terms.
    /// </summary>
    /// <remarks>
    /// A successful upload used to say "The instruction has been retained and
    /// queued for processing" while the list below still read "No intake
    /// receipts match this view" — the item existed nowhere the operator could
    /// see. Each outcome now names what the file became and links to it.
    /// </remarks>
    private async Task<UploadOutcome> DescribeAsync(
        IntakeSubmissionResult result,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (result.IsDuplicate)
        {
            return new(
                $"{fileName} was already received. No duplicate was created.",
                result.ReceiptId,
                null,
                false);
        }

        if (result.Disposition == IntakeSubmissionDisposition.Queued)
        {
            return new(
                $"{fileName} was received and is being processed.",
                result.ReceiptId,
                null,
                false);
        }

        var receipt = await receiptQueries.GetAsync(result.ReceiptId, cancellationToken);
        if (receipt?.CurrentCaseId is { } caseId)
        {
            return new($"{fileName} received — a case was created.", null, caseId, false);
        }

        return receipt?.Decision switch
        {
            IntakeDecision.NeedsSorting => new(
                $"{fileName} received — it needs sorting.", result.ReceiptId, null, false),
            IntakeDecision.BlockedIntake => new(
                $"{fileName} received — it is blocked.", result.ReceiptId, null, false),
            IntakeDecision.OcrRequired => new(
                $"{fileName} received — it needs text extraction.", result.ReceiptId, null, false),
            IntakeDecision.ImageIntakeRegistered => new(
                $"{fileName} received — it was registered as vehicle images.",
                result.ReceiptId,
                null,
                false),
            IntakeDecision.Unsupported or IntakeDecision.TechnicalFailure => new(
                $"{fileName} could not be processed. Try again, or contact an administrator if it keeps failing.",
                result.ReceiptId,
                null,
                true),
            _ => new($"{fileName} was received.", result.ReceiptId, null, false)
        };
    }

    public sealed record UploadOutcome(
        string Message,
        Guid? ReceiptId,
        Guid? CaseId,
        bool IsFailure);
}
