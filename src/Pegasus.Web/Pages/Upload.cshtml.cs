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
///
/// The file is now read while the operator waits, through
/// <see cref="ProcessIntakeSubmission"/>. The queue-only composition this page
/// used before could only ever return
/// <c>IntakeSubmissionDisposition.Queued</c>, so every upload ended on this
/// page reading "is being processed" and waited on a Worker timer; the two
/// branches that sent the operator to a case or to a receipt were unreachable.
/// An upload now ends where the file ended up.
/// </remarks>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed partial class UploadModel(
    ProcessIntakeSubmission intakeSubmission,
    IIntakeReceiptQueries receiptQueries,
    TimeProvider timeProvider,
    ILogger<UploadModel> logger) : PageModel
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

            // Still being worked on. An upload normally finishes while the
            // operator waits, but when another upload's lock contention pushes
            // this one onto its retry the honest answer is that it arrived and
            // is in progress — not that it failed. Saying it failed sent staff
            // to re-upload a file that was already on its way to a case.
            if (result.Disposition == IntakeSubmissionDisposition.Queued)
            {
                TempData["UploadOutcomeMessage"] =
                    $"{fileName} was received and is being processed. It will appear here shortly.";
                // The received-items list, which is the "/Received" route.
                return RedirectToPage("/Intake/Index");
            }

            // Post-redirect-get, landing on what the upload produced. The
            // operator used to be told "The instruction has been retained and
            // queued for processing" while the list below still read "No
            // intake receipts match this view" — the item existed nowhere they
            // could see. Now the confirmation and the thing itself arrive
            // together.
            var receipt = await receiptQueries.GetAsync(result.ReceiptId, cancellationToken);
            var outcome = Describe(receipt, result, fileName);

            // A file that could not be read produced no material to act on, so
            // there is nowhere to send the operator: the failure belongs on the
            // page they are still looking at.
            if (outcome.IsFailure)
            {
                ModelState.AddModelError(string.Empty, outcome.Message);
                return Page();
            }

            TempData["UploadOutcomeMessage"] = outcome.Message;
            if (outcome.CaseId is { } createdCaseId)
            {
                return RedirectToPage("/Cases/Details", new { id = createdCaseId });
            }

            // Everything else is readable material that has not become a case.
            // That is the create screen's job, prefilled with whatever
            // extraction found, rather than a list the operator has to go
            // hunting through.
            return outcome.OpensCreateScreen
                ? RedirectToPage(
                    "/Cases/Create",
                    new { receiptId = result.ReceiptId })
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
            // The operator is told to try again; without this nobody can tell
            // them why, because the only record of the cause was the message
            // itself, which deliberately does not carry one.
            LogUploadFailed(logger, fileName, exception);
            ModelState.AddModelError(
                string.Empty,
                "The file could not be processed. Try again, or contact an administrator if it keeps failing.");
        }

        return Page();
    }

    [LoggerMessage(
        EventId = 1310,
        Level = LogLevel.Warning,
        Message = "A staff upload of {FileName} could not be processed.")]
    private static partial void LogUploadFailed(ILogger logger, string fileName, Exception exception);

    /// <summary>
    /// What actually happened to the file, in the operator's terms, and where
    /// that puts them next.
    /// </summary>
    /// <remarks>
    /// A successful upload used to say "The instruction has been retained and
    /// queued for processing" while the list below still read "No intake
    /// receipts match this view" — the item existed nowhere the operator could
    /// see. Each outcome now names what the file became and goes there.
    ///
    /// Three destinations, and the reason for each: a file that allocated its
    /// own case opens that case; a file that was read but did not allocate one
    /// opens the create screen, because deciding that is the next thing to do;
    /// a file that could not be read has no next thing, so it reports on the
    /// upload page.
    /// </remarks>
    private static UploadOutcome Describe(
        IntakeReceipt? receipt,
        IntakeSubmissionResult result,
        string fileName)
    {
        if (receipt is null)
        {
            // Processing persisted an evaluation the queries cannot read back.
            // Nothing can be said about the file, so nothing is claimed.
            return new(
                $"{fileName} was received, but what happened to it could not be read back. "
                + "Try again, or contact an administrator if it keeps failing.",
                result.ReceiptId,
                null,
                IsFailure: true,
                OpensCreateScreen: false);
        }

        var duplicatePrefix = result.IsDuplicate
            ? $"{fileName} was already received. No duplicate was created"
            : $"{fileName} received";

        if (receipt.CurrentCaseId is { } caseId)
        {
            return new(
                $"{duplicatePrefix} — a case was created.",
                receipt.Id,
                caseId,
                IsFailure: false,
                OpensCreateScreen: false);
        }

        if (receipt.AllocationState is { } allocation)
        {
            return allocation.Status switch
            {
                IntakeAllocationProjectionStatus.Pending => new(
                    $"{duplicatePrefix} — case creation is in progress.",
                    receipt.Id,
                    null,
                    IsFailure: false,
                    OpensCreateScreen: false),
                IntakeAllocationProjectionStatus.FailedRecoverable
                    or IntakeAllocationProjectionStatus.FailedBlocked => new(
                    $"{duplicatePrefix} — case not created. "
                        + (allocation.SafeReason ?? "No reference was allocated."),
                    receipt.Id,
                    null,
                    IsFailure: false,
                    OpensCreateScreen: false),
                _ => throw new InvalidOperationException(
                    $"Allocation state '{allocation.Status}' has no case association.")
            };
        }

        return receipt.Decision switch
        {
            // Read, but not definitive enough to allocate on its own. The
            // extracted detail is on the create screen for a person to confirm.
            IntakeDecision.NeedsSorting or IntakeDecision.CaseCreated => new(
                $"{duplicatePrefix} — check the details and create the case.",
                receipt.Id,
                null,
                IsFailure: false,
                OpensCreateScreen: true),

            // Little or no text came out of the document. The create screen is
            // still the right place: it is where the detail gets keyed in.
            IntakeDecision.OcrRequired => new(
                $"{duplicatePrefix} — little text could be read from it, so the details need entering by hand.",
                receipt.Id,
                null,
                IsFailure: false,
                OpensCreateScreen: true),

            // A reasoned refusal and a registered image set both have a record
            // that explains them; neither is a case waiting to be made.
            IntakeDecision.BlockedIntake => new(
                $"{duplicatePrefix} — it is blocked, with the reason recorded.",
                receipt.Id,
                null,
                IsFailure: false,
                OpensCreateScreen: false),
            IntakeDecision.ImageIntakeRegistered => new(
                $"{duplicatePrefix} — it was registered as vehicle images.",
                receipt.Id,
                null,
                IsFailure: false,
                OpensCreateScreen: false),

            IntakeDecision.Unsupported or IntakeDecision.TechnicalFailure => new(
                receipt.FailureReason is { Length: > 0 } reason
                    ? $"{fileName} could not be processed: {reason}"
                    : $"{fileName} could not be processed. Try again, or contact an administrator if it keeps failing.",
                receipt.Id,
                null,
                IsFailure: true,
                OpensCreateScreen: false),

            _ => throw new InvalidOperationException(
                $"Unknown intake decision value '{(int)receipt.Decision}'.")
        };
    }

    public sealed record UploadOutcome(
        string Message,
        Guid? ReceiptId,
        Guid? CaseId,
        bool IsFailure,
        bool OpensCreateScreen);
}
