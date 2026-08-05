using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;

namespace Pegasus.Web.Pages.Intake;

public sealed class IndexModel(
    IListIntake listIntake,
    IIntakeSubmission intakeSubmission,
    IImageIntakeQueries imageIntakeQueries,
    GetEmailOperations getEmailOperations,
    RetryMailboxProcessing retryMailboxProcessing,
    TimeProvider timeProvider) : PageModel
{
    private Dictionary<Guid, ImageIntakeSummary> _imageIntakesByReceipt = [];

    private const int PageSize = 25;

    [BindProperty(SupportsGet = true, Name = "decision")]
    public string? DecisionFilter { get; set; }

    /// <summary>
    /// Which direction the operator is looking at.
    /// </summary>
    /// <remarks>
    /// Sent used to be a separate screen, reachable only from a dashboard card
    /// that said "Unavailable". It is the same list of messages seen from the
    /// other end, so it is a tab.
    /// </remarks>
    [BindProperty(SupportsGet = true, Name = "view")]
    public string? DirectionFilter { get; set; }

    public bool ShowingSent => string.Equals(DirectionFilter, "sent", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The "did anything fail to arrive?" view — the one question the separate
    /// Email screen existed to answer, now one click from the list an operator
    /// already has open.
    /// </summary>
    public bool ShowingFailed => string.Equals(DecisionFilter, "failed", StringComparison.OrdinalIgnoreCase);

    public EmailOperationsProjection MailOperations { get; private set; } = new([], [], false, false);

    public int CurrentPage { get; private set; } = 1;

    [BindProperty]
    public IFormFile? Upload { get; set; }

    [BindProperty]
    public string ExternalReceiptToken { get; set; } = string.Empty;

    public IntakeListPage Results { get; private set; } = new([], 1, PageSize, 0);

    public IntakeDecision? Decision { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "page")] int? pageNumber,
        CancellationToken cancellationToken)
    {
        CurrentPage = pageNumber ?? 1;
        ExternalReceiptToken = CreateExternalReceiptToken();
        if (!TryParseDecision(DecisionFilter, out var decision))
        {
            return NotFound();
        }

        Decision = decision;
        return await LoadAsync(cancellationToken) ? Page() : Forbid();
    }

    public async Task<IActionResult> OnPostReceiveIntakeAsync(
        [FromQuery(Name = "page")] int? pageNumber,
        CancellationToken cancellationToken)
    {
        CurrentPage = pageNumber ?? 1;
        if (!TryParseDecision(DecisionFilter, out var decision))
        {
            return NotFound();
        }
        Decision = decision;

        if (string.IsNullOrWhiteSpace(ExternalReceiptToken))
        {
            ExternalReceiptToken = CreateExternalReceiptToken();
        }
        else if (!Guid.TryParseExact(ExternalReceiptToken, "N", out var receiptId))
        {
            ModelState.AddModelError(string.Empty, "The upload receipt is invalid. Refresh the page and try again.");
        }
        else
        {
            ExternalReceiptToken = receiptId.ToString("N");
        }

        if (Upload is null)
        {
            ModelState.AddModelError(nameof(Upload), "Choose an email, document, PDF or image to upload.");
        }
        else if (Upload.Length == 0)
        {
            ModelState.AddModelError(nameof(Upload), "The selected file is empty.");
        }
        else if (Upload.Length > IntakeEnvelopeLimits.MaximumContentLength)
        {
            ModelState.AddModelError(nameof(Upload), "The selected file must be 10 MB or smaller.");
        }

        if (!ModelState.IsValid)
        {
            return await LoadAsync(cancellationToken) ? Page() : Forbid();
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
        try
        {
            var result = await intakeSubmission.ExecuteAsync(
                new(
                    Path.GetFileName(Upload.FileName),
                    string.IsNullOrWhiteSpace(Upload.ContentType)
                        ? "application/octet-stream"
                        : Upload.ContentType,
                    memory.ToArray(),
                    timeProvider.GetUtcNow(),
                    $"staff:{actor.SubjectId}",
                    new(IntakeSourceChannel.ManualUpload, ExternalReceiptToken)),
                $"manual-upload:{ExternalReceiptToken}",
                cancellationToken);
            if (result.Disposition == IntakeSubmissionDisposition.Queued)
            {
                TempData["IntakeQueueStatus"] = result.IsDuplicate ? "duplicate" : "queued";
                return RedirectToPage(
                    "/Intake/Index",
                    new
                    {
                        decision = DecisionFilter,
                        page = Math.Max(1, CurrentPage),
                        queuedReceiptId = result.ReceiptId,
                        duplicate = result.IsDuplicate
                    });
            }

            return RedirectToPage(
                "/Intake/Details",
                new { id = result.ReceiptId, duplicate = result.IsDuplicate });
        }
        catch (IntakeSourceIdentityConflictException)
        {
            ModelState.AddModelError(
                string.Empty,
                "This upload receipt was already used for different content. Refresh the page and try again.");
        }
        catch (IntakeArtifactRetentionException)
        {
            ModelState.AddModelError(
                string.Empty,
                "The instruction source could not be retained. Retry using the same upload receipt.");
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            ModelState.AddModelError(
                string.Empty,
                "The intake receipt could not be queued because of a technical failure.");
        }

        return await LoadAsync(cancellationToken) ? Page() : Forbid();
    }

    /// <summary>
    /// Retries mailbox processing from the row that failed.
    /// </summary>
    /// <remarks>
    /// The handler, its expected failure-code and due-time guards and its
    /// replay behaviour are the ones the separate Email screen used, unchanged.
    /// Only where the operator finds it has moved: into the row, on the list
    /// they already have open.
    /// </remarks>
    public async Task<IActionResult> OnPostRetryMailboxAsync(
        string mailboxId,
        EmailOperationDirection direction,
        string expectedFailureCode,
        DateTimeOffset expectedDueAtUtc,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!StaffActorFactory.TryCreate(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            return Forbid();
        }
        // The upload form's fields belong to a different form on this page and
        // are not posted by this one; without clearing them, their binding
        // errors would fail a retry that carries everything it needs.
        ModelState.Remove(nameof(Upload));
        ModelState.Remove(nameof(ExternalReceiptToken));

        if (!ModelState.IsValid || !Enum.IsDefined(direction))
        {
            TempData["IntakeQueueStatus"] = "retry_invalid";
            return RedirectToPage(new { decision = DecisionFilter, view = DirectionFilter });
        }

        try
        {
            var result = await retryMailboxProcessing.ExecuteAsync(
                new(mailboxId, direction, expectedFailureCode, expectedDueAtUtc, actor, operationKey),
                cancellationToken);
            TempData["IntakeQueueStatus"] = result.IsReplay ? "retry_replayed" : "retry_scheduled";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            TempData["IntakeQueueStatus"] = "retry_changed";
        }

        return RedirectToPage(new { decision = DecisionFilter, view = DirectionFilter });
    }

    public static string MailStateLabel(EmailOperationState state) => state switch
    {
        EmailOperationState.Pending => "Pending",
        EmailOperationState.Succeeded => "Succeeded",
        EmailOperationState.Failed => "Failed",
        _ => "Unknown"
    };

    /// <summary>
    /// A failure an operator can act on, never the recorded failure code.
    /// </summary>
    public static string MailFailureSentence(string? failureCode) => failureCode switch
    {
        null or "" => "This message could not be processed.",
        "source_unavailable" => "The message could not be read from the mailbox.",
        "sent_mailbox_not_approved" => "The mailbox it was sent from is not an approved mailbox.",
        "sent_source_throttled" => "The mailbox refused further reads for a while.",
        "sent_evidence_poll_failure" => "The sent folder could not be read.",
        _ => "The last message from this mailbox could not be processed."
    };

    public static string DecisionLabel(IntakeDecision decision) => decision switch
    {
        IntakeDecision.CaseCreated => "Case created",
        IntakeDecision.NeedsSorting => "Needs sorting",
        IntakeDecision.BlockedIntake => "Blocked intake",
        IntakeDecision.OcrRequired => "Document text required",
        IntakeDecision.TechnicalFailure => "Technical failure",
        IntakeDecision.Unsupported => "Unsupported",
        IntakeDecision.ImageIntakeRegistered => "Image intake registered",
        _ => throw new InvalidOperationException($"Unknown intake decision '{(int)decision}'.")
    };

    /// <summary>
    /// The precise processing outcome for a row: a registered receipt whose
    /// Image intake is currently associated shows `Associated with Case`,
    /// derived live from the receipt's single association.
    /// </summary>
    public string RowOutcomeLabel(IntakeReceiptSummary item) =>
        item.Decision == IntakeDecision.ImageIntakeRegistered
            && _imageIntakesByReceipt.TryGetValue(item.Id, out var imageIntake)
            && imageIntake.AssociatedCaseId is not null
            ? "Associated with Case"
            : DecisionLabel(item.Decision);

    private async Task<bool> LoadAsync(CancellationToken cancellationToken)
    {
        if (!StaffActorFactory.TryCreate(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            return false;
        }

        CurrentPage = Math.Max(1, CurrentPage);
        MailOperations = await getEmailOperations.ExecuteAsync(actor, cancellationToken);
        Results = await listIntake.ExecuteAsync(
            new(actor, Decision, CurrentPage, PageSize),
            cancellationToken);
        var registeredIds = Results.Items
            .Where(item => item.Decision == IntakeDecision.ImageIntakeRegistered)
            .Select(item => item.Id)
            .ToArray();
        _imageIntakesByReceipt = registeredIds.Length == 0
            ? new Dictionary<Guid, ImageIntakeSummary>()
            : (await imageIntakeQueries.ListByOriginReceiptsAsync(registeredIds, cancellationToken))
                .ToDictionary(summary => summary.OriginReceiptId);
        return true;
    }

    private static bool TryParseDecision(string? value, out IntakeDecision? decision)
    {
        decision = value switch
        {
            // "failed" is a mailbox-processing view, not an intake decision:
            // it answers "did anything not come in?", which is a different
            // question from "what did processing decide about what did".
            null or "" or "failed" => null,
            "case_created" => IntakeDecision.CaseCreated,
            "needs_sorting" => IntakeDecision.NeedsSorting,
            "blocked_intake" => IntakeDecision.BlockedIntake,
            "unsupported" => IntakeDecision.Unsupported,
            "ocr_required" => IntakeDecision.OcrRequired,
            "technical_failure" => IntakeDecision.TechnicalFailure,
            "image_intake_registered" => IntakeDecision.ImageIntakeRegistered,
            _ => null
        };
        return string.IsNullOrWhiteSpace(value)
            || decision is not null
            || string.Equals(value, "failed", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateExternalReceiptToken() => Guid.NewGuid().ToString("N");
}
