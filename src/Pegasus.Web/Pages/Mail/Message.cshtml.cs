using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Pages.Mail;

/// <summary>
/// One retained message.
/// </summary>
/// <remarks>
/// Read-only by construction: there is no handler on this page, so there is
/// nothing it can change. Classification, Case linking and folder moves are
/// allocated work that has not landed, and the screen says so rather than
/// offering a control that does nothing.
/// </remarks>
public sealed class MessageModel(GetRetainedMail getRetainedMail) : PageModel
{
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
