using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Mail;

/// <summary>
/// One retained message.
/// </summary>
/// <remarks>
/// Reads one retained message and exposes only the Core-owned correction command;
/// Case linking and mailbox mutation remain separate capabilities.
/// </remarks>
public sealed class MessageModel(
    GetRetainedMail getRetainedMail,
    CorrectRetainedMailClassification correctClassification) : PageModel
{
    public static IReadOnlyList<MailClassificationSelection.SelectionOption> ClassificationOptions =>
        MailClassificationSelection.Options;

    /// <summary>
    /// The list scope this message was opened from, carried through untouched so
    /// Back reconstructs the exact position the operator left.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "mailbox")]
    public string? MailboxFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "folder")]
    public string? FolderFilter { get; set; }

    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int? PageNumber { get; set; }

    [BindProperty(SupportsGet = true, Name = "section")]
    public string? Section { get; set; }

    [BindProperty]
    public int ExpectedClassificationVersion { get; set; }

    [BindProperty]
    public string? ClassificationKey { get; set; }

    [BindProperty]
    public string? OtherClassificationName { get; set; }

    [BindProperty]
    public string? OtherClassificationReasoning { get; set; }

    [BindProperty]
    public string? CorrectionReason { get; set; }

    [TempData]
    public string? ClassificationNotice { get; set; }

    public RetainedMailDetail Detail { get; private set; } = null!;

    public MailFolderScope ListFolder { get; private set; } = MailFolderScope.Inbox;

    /// <summary>
    /// True where the message is no longer inside the list scope it was opened
    /// from. It still renders; the screen states the mismatch and offers the way
    /// back rather than replacing the message with a not-found.
    /// </summary>
    public bool OutsideListScope { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!StaffActorFactory.TryCreate(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            return Forbid();
        }

        if (!IndexModel.TryParseFolder(FolderFilter, out var listFolder))
        {
            return NotFound();
        }

        ListFolder = listFolder;
        MailboxFilter = string.IsNullOrWhiteSpace(MailboxFilter) ? null : MailboxFilter.Trim();

        RetainedMailDetail? detail;
        try
        {
            detail = await getRetainedMail.ExecuteAsync(actor, id, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        if (detail is null)
        {
            return NotFound();
        }

        Detail = detail;
        OutsideListScope = detail.Folder != listFolder
            || (MailboxFilter is { } mailbox
                && !string.Equals(mailbox, detail.Summary.MailboxId, StringComparison.Ordinal));
        return Page();
    }

    public async Task<IActionResult> OnPostCorrectClassificationAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!StaffActorFactory.TryCreate(
                User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                User.FindAll(ClaimTypes.Role).Select(claim => claim.Value),
                out var actor))
        {
            return Forbid();
        }
        if (!TryCategory(out var category))
        {
            ModelState.AddModelError(nameof(ClassificationKey), "Choose a valid classification and complete any Other details.");
        }
        if (string.IsNullOrWhiteSpace(CorrectionReason))
        {
            ModelState.AddModelError(nameof(CorrectionReason), "Explain why this classification is being corrected.");
        }
        if (!ModelState.IsValid)
        {
            return await ReloadAsync(actor, id, cancellationToken);
        }

        try
        {
            var result = await correctClassification.ExecuteAsync(
                actor,
                new(id, ExpectedClassificationVersion, category!, CorrectionReason!),
                cancellationToken);
            if (result is null)
            {
                return NotFound();
            }
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (MailClassificationConcurrencyException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadAsync(actor, id, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await ReloadAsync(actor, id, cancellationToken);
        }

        ClassificationNotice = "Classification corrected. The previous decision and evidence remain in permanent history.";
        return RedirectToPage(new
        {
            id,
            mailbox = MailboxFilter,
            folder = FolderFilter,
            pageNumber = PageNumber
        });
    }

    private async Task<IActionResult> ReloadAsync(
        ActionActor actor,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!IndexModel.TryParseFolder(FolderFilter, out var listFolder))
        {
            return NotFound();
        }
        ListFolder = listFolder;
        var detail = await getRetainedMail.ExecuteAsync(actor, id, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }
        Detail = detail;
        OutsideListScope = detail.Folder != listFolder
            || (MailboxFilter is { } mailbox
                && !string.Equals(mailbox, detail.Summary.MailboxId, StringComparison.Ordinal));
        return Page();
    }

    private bool TryCategory(out MailCategory? category) =>
        MailClassificationSelection.TryParse(
            ClassificationKey,
            OtherClassificationName,
            OtherClassificationReasoning,
            out category);

    public string ActiveSection => Section switch
    {
        "attachments" => "attachments",
        "thread" => "thread",
        _ => "message"
    };

    public string? FolderRouteValue =>
        ListFolder == MailFolderScope.Inbox ? null : IndexModel.FolderCode(ListFolder);

    public int? PageRouteValue => PageNumber is > 1 ? PageNumber : null;

    public static string ClassificationLabel(MailClassificationOutcome? outcome) => outcome switch
    {
        MailClassificationOutcome.Classified => "Classified",
        MailClassificationOutcome.Ambiguous => "Ambiguous",
        MailClassificationOutcome.Unclassified => "Unclassified",
        _ => "Not yet processed"
    };

    /// <summary>
    /// The operational destination for a classification decision, computed
    /// live from the Core policy rather than a second persisted value: the
    /// destination is a pure function of the already-loaded decision, so
    /// there is nothing to keep in sync.
    /// </summary>
    public static MailOperationalDestinationResult Destination(MailClassificationResult result) =>
        MailOperationalDestinationPolicy.Map(result);

    public static string DecisionLabel(MailClassificationResult result) => result.Category is { } category
        ? $"{(category.Direction == MailDirection.Sent ? "Sent: " : string.Empty)}{category.Name}{(category.Subtype is null ? string.Empty : "/" + category.Subtype)}"
        : ClassificationLabel(result.Outcome);

    public static string QueueLabel(MailRouteDisposition? disposition) => disposition switch
    {
        MailRouteDisposition.Accepted => "Accepted",
        MailRouteDisposition.NoMatch => "No match",
        MailRouteDisposition.NeedsSorting => "Unidentified",
        _ => "Not yet processed"
    };

    public static string OutcomeLabel(RetainedMailSummary summary) => summary switch
    {
        { CaseId: not null } => "Case created",
        { AllocationState.Status: IntakeAllocationProjectionStatus.Pending } => "Creating case",
        { AllocationState.Status: IntakeAllocationProjectionStatus.FailedRecoverable
            or IntakeAllocationProjectionStatus.FailedBlocked } => "Case not created",
        _ => OutcomeLabel(summary.ProcessingOutcome)
    };

    private static string OutcomeLabel(IntakeDecision? decision) => decision switch
    {
        IntakeDecision.CaseCreated => "Ready for case allocation",
        IntakeDecision.NeedsSorting => "Unidentified",
        IntakeDecision.BlockedIntake => "Blocked",
        IntakeDecision.OcrRequired => "Document text required",
        IntakeDecision.TechnicalFailure => "Technical failure",
        IntakeDecision.Unsupported => "Unsupported",
        IntakeDecision.ImageIntakeRegistered => "Vehicle images registered",
        _ => "Not yet processed"
    };
}
