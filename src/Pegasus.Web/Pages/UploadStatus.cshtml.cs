using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class UploadStatusModel(IQueuedIntakeStatusQueries queries) : PageModel
{
    public QueuedIntakeStatus Status { get; private set; } = null!;
    public bool IsDuplicate { get; private set; }

    public bool RefreshAutomatically =>
        Status.Status is QueuedIntakeStatusKind.Received or QueuedIntakeStatusKind.Processing;

    public string Heading => Status.Status switch
    {
        QueuedIntakeStatusKind.Received => "Received",
        QueuedIntakeStatusKind.Processing => "Processing",
        QueuedIntakeStatusKind.Complete => "Complete",
        QueuedIntakeStatusKind.Failed => "Failed",
        _ => throw new InvalidOperationException("The queued intake status is not recognized.")
    };

    public string Message => IsDuplicate
        ? $"{Status.SourceFileName} was already received. No duplicate was created. {StateMessage}"
        : StateMessage;

    private string StateMessage => Status.Status switch
    {
        QueuedIntakeStatusKind.Received =>
            "The file is safely received and waiting for background processing.",
        QueuedIntakeStatusKind.Processing => "The file is being processed.",
        QueuedIntakeStatusKind.Complete => "Processing is complete.",
        QueuedIntakeStatusKind.Failed => OperatorLabels.IntakeFailure(Status.FailureCode) + ".",
        _ => throw new InvalidOperationException("The queued intake status is not recognized.")
    };

    /// <param name="duplicate">
    /// Carried on the URL, as <c>/Received/{id}?duplicate=true</c> already does,
    /// so the notice survives the page's own refreshes. It only changes wording.
    /// </param>
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        bool duplicate,
        CancellationToken cancellationToken)
    {
        var status = await queries.GetAsync(id, cancellationToken);
        if (status is null)
        {
            return NotFound();
        }

        Status = status;
        IsDuplicate = duplicate;
        return Page();
    }
}
