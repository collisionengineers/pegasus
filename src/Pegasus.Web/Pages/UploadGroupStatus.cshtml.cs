using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Intake;
using Pegasus.Core.Identity;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages;

[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class UploadGroupStatusModel(
    IIntakeSubmissionGroupStore groups,
    IQueuedIntakeStatusQueries statuses,
    IUploadOutcomeQueries outcomeQueries,
    IUploadCaseDecision caseDecision) : UploadConfirmationPageModel(caseDecision)
{
    public IntakeSubmissionGroup Group { get; private set; } = null!;
    public IReadOnlyDictionary<Guid, QueuedIntakeStatus?> Statuses { get; private set; } =
        new Dictionary<Guid, QueuedIntakeStatus?>();

    /// <summary>
    /// The confirmation outcome per member, built independently per file —
    /// a grouped image upload can terminal-decide its members independently
    /// (a mixed batch's instruction document takes its own route), so this
    /// makes no group-wide assumption and reports each member's own outcome.
    /// </summary>
    public IReadOnlyDictionary<Guid, UploadOutcomeView?> Outcomes { get; private set; } =
        new Dictionary<Guid, UploadOutcomeView?>();

    /// <summary>
    /// Set only when every member's outcome is the same Image-initiated Case
    /// registration. The group is the registration unit (one reference for
    /// the whole submission), so the page reports that registration once for
    /// the group instead of repeating the identical outcome per file. Any
    /// other mix of outcomes keeps the per-file report.
    /// </summary>
    public UploadOutcomeView? GroupRegistrationOutcome { get; private set; }

    public bool RefreshAutomatically => Statuses.Values.Any(status =>
        status is null
            || status.Status is QueuedIntakeStatusKind.Received or QueuedIntakeStatusKind.Processing);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var group = await groups.GetAsync(id, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        Group = group;
        var haveActor = TryGetActor(out var actor);

        // Each member's status read, and — once terminal — its confirmation
        // outcome, is an independent read against its own DbContext (every
        // store behind these ports is IDbContextFactory-backed, not shared),
        // so a group's members are read concurrently rather than one durable
        // round-trip at a time. This page polls itself repeatedly while any
        // member is still Received/Processing, so the saving is real.
        var memberResults = await Task.WhenAll(group.Members.Select(async member =>
        {
            var status = await statuses.GetAsync(member.StagedReceiptId, cancellationToken);
            UploadOutcomeView? outcome = null;
            if (status is { Status: QueuedIntakeStatusKind.Complete or QueuedIntakeStatusKind.Failed }
                && haveActor)
            {
                outcome = await outcomeQueries.BuildAsync(status, group.Id, actor!, cancellationToken);
            }

            return (member.StagedReceiptId, status, outcome);
        }));

        Statuses = memberResults.ToDictionary(result => result.StagedReceiptId, result => result.status);
        Outcomes = memberResults.ToDictionary(result => result.StagedReceiptId, result => result.outcome);
        var outcomes = memberResults.Select(result => result.outcome).ToArray();
        if (outcomes.Length > 1
            && outcomes.All(outcome => outcome is { Kind: UploadOutcomeKind.ImageCaseRegistered })
            && outcomes.Select(outcome => outcome!.PrimaryAction?.Url).Distinct().Count() == 1)
        {
            GroupRegistrationOutcome = outcomes[0];
        }

        return Page();
    }

    protected override IActionResult RedirectToSurface(Guid id) =>
        RedirectToPage("/UploadGroupStatus", new { id });
}
