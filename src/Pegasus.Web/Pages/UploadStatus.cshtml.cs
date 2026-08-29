using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class UploadStatusModel(
    IQueuedIntakeStatusQueries queries,
    IUploadOutcomeQueries outcomeQueries,
    IUploadCaseDecision caseDecision,
    TimeProvider timeProvider) : UploadConfirmationPageModel(caseDecision)
{
    public QueuedIntakeStatus Status { get; private set; } = null!;
    public bool IsDuplicate { get; private set; }

    /// <summary>
    /// The confirmation outcome, once processing has left Received/Processing.
    /// Built from the same status read this page already queries — no second
    /// endpoint, no second poll.
    /// </summary>
    public UploadOutcomeView? Outcome { get; private set; }

    /// <summary>
    /// How long before this page reloads itself, or null once the file has
    /// stopped moving and there is nothing left to wait for.
    /// </summary>
    public int? AutomaticRefreshMilliseconds =>
        Status.Status is QueuedIntakeStatusKind.Received or QueuedIntakeStatusKind.Processing
            ? UploadStatusRefresh.DelayMilliseconds(Status, timeProvider.GetUtcNow())
            : null;

    public string Heading => Status.Status switch
    {
        QueuedIntakeStatusKind.Received => "Received",
        QueuedIntakeStatusKind.Processing => "Processing",
        QueuedIntakeStatusKind.Complete => "Complete",
        QueuedIntakeStatusKind.Failed => "Failed",
        _ => throw new InvalidOperationException("The queued intake status is not recognized.")
    };

    /// <summary>
    /// The terminal failure value when the authenticated principal has no
    /// usable staff actor and the richer outcome cannot be built.
    /// </summary>
    public string? FailureReason =>
        Status.Status == QueuedIntakeStatusKind.Failed && Outcome is null
            ? OperatorLabels.IntakeFailure(Status.FailureCode)
            : null;

    /// <param name="duplicate">
    /// Carried on the URL, as <c>/Received/{id}?duplicate=true</c> already does,
    /// so the duplicate value survives the page's own refreshes.
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

        // The confirmation decision needs a full receipt read for a terminal
        // status; Received/Processing never reach the branch that needs one.
        if (status.Status is QueuedIntakeStatusKind.Complete or QueuedIntakeStatusKind.Failed
            && TryGetActor(out var actor))
        {
            Outcome = await outcomeQueries.BuildAsync(status, submissionGroupId: null, actor, cancellationToken);
        }

        return Page();
    }

    protected override IActionResult RedirectToSurface(Guid id) =>
        RedirectToPage("/UploadStatus", new { id });
}
