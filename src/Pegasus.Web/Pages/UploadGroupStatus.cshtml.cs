using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.ImageIntake;
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
    IUploadCaseDecision caseDecision,
    IRegisterImageIntake registerImageIntake,
    IImageIntakeOriginResolver imageIntakeOriginResolver) : UploadConfirmationPageModel(caseDecision)
{
    private readonly IUploadCaseDecision _caseDecision = caseDecision;

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

    public bool RefreshAutomatically =>
        Statuses.Values.Any(status =>
            status is null
                || status.Status is QueuedIntakeStatusKind.Received or QueuedIntakeStatusKind.Processing)
        || Outcomes.Values.Any(outcome => outcome?.IsStillWorking == true);

    public int? AutomaticRefreshMilliseconds
    {
        get
        {
            if (!RefreshAutomatically)
            {
                return null;
            }

            var delays = Statuses.Values
                .Where(status => status is null
                    || status.Status is QueuedIntakeStatusKind.Received or QueuedIntakeStatusKind.Processing)
                .Select(status => status is null
                    ? 2_000
                    : QueuedIntakeRefreshDelay.GetMilliseconds(status, DateTimeOffset.UtcNow))
                .ToList();
            if (Outcomes.Values.Any(outcome => outcome?.IsStillWorking == true))
            {
                delays.Add(2_000);
            }

            return delays.Min();
        }
    }

    /// <summary>
    /// Set when any member still needs a staff decision: the submission is
    /// decided once, as one unit, never per file.
    /// </summary>
    public bool OpenGroupDecision { get; private set; }

    /// <summary>The still-open members' processed receipt ids, in member order.</summary>
    public IReadOnlyList<Guid> OpenMemberReceiptIds { get; private set; } = [];

    public bool OfferGroupRegistration { get; private set; }

    private Guid _firstOpenImageReceiptId;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken) =>
        await LoadAsync(id, cancellationToken) ?? Page();

    public async Task<IActionResult> OnPostRegisterGroupAsync(
        Guid id,
        string? vehicleRegistration,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (await LoadAsync(id, cancellationToken) is { } notFound)
        {
            return notFound;
        }
        if (!OpenGroupDecision || !OfferGroupRegistration)
        {
            return RedirectToSurface(id);
        }

        var normalized = ImageIntakeLifecycleRules.NormalizeRegistrationInput(vehicleRegistration);
        if (normalized.Length == 0 || string.IsNullOrWhiteSpace(reason))
        {
            TempData["UploadConfirmationError"] = "A registration and a reason are required.";
            return RedirectToSurface(id);
        }

        try
        {
            var origin = await imageIntakeOriginResolver.ResolveOriginAsync(
                _firstOpenImageReceiptId, cancellationToken);
            if (origin is null)
            {
                TempData["UploadConfirmationError"] = "This submission is still being processed. Try again shortly.";
                return RedirectToSurface(id);
            }

            // The automation's own replay identity for this group, so exactly
            // one registration can ever exist for the submission whether the
            // pipeline or a staff decision made it.
            var record = await registerImageIntake.ExecuteAsync(
                new(
                    origin,
                    normalized,
                    actor,
                    $"image-intake-register:group:{id:N}",
                    reason,
                    id),
                cancellationToken);
            TempData["Confirmation"] = $"Registered as vehicle-image case {record.ImageIntakeReference}.";
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            TempData["UploadConfirmationError"] = "The registration must be letters and digits only.";
        }
        catch (InvalidOperationException)
        {
            TempData["UploadConfirmationError"] = "The submission could not be registered. Refresh and try again.";
        }

        return RedirectToSurface(id);
    }

    public async Task<IActionResult> OnPostAttachGroupAsync(
        Guid id,
        Guid? caseId,
        string? reference,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (await LoadAsync(id, cancellationToken) is { } notFound)
        {
            return notFound;
        }
        if (!OpenGroupDecision)
        {
            return RedirectToSurface(id);
        }
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["UploadConfirmationError"] = "A reason is required to add this to a case.";
            return RedirectToSurface(id);
        }

        try
        {
            var result = await _caseDecision.AttachGroupAsync(
                id, OpenMemberReceiptIds, caseId, reference, reason, actor, cancellationToken);
            TempData[result.Succeeded ? "Confirmation" : "UploadConfirmationError"] = result.Message;
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        return RedirectToSurface(id);
    }

    private async Task<IActionResult?> LoadAsync(Guid id, CancellationToken cancellationToken)
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
        // round-trip at a time. This page polls itself while a queue member or
        // its later group-level outcome is still working, so the saving is real.
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

        var open = memberResults
            .Where(result => result.outcome is { IsOpenDecision: true })
            .ToArray();
        OpenGroupDecision = GroupRegistrationOutcome is null
            && !RefreshAutomatically
            && open.Length > 0;
        OpenMemberReceiptIds = open
            .Select(result => result.status!.ProcessedReceiptId ?? result.status.StagedReceiptId)
            .ToArray();
        var firstOpenImage = open.FirstOrDefault(result => result.outcome!.ThumbnailReceiptId is not null);
        OfferGroupRegistration = OpenGroupDecision && firstOpenImage.outcome is not null;
        _firstOpenImageReceiptId = firstOpenImage.outcome?.ThumbnailReceiptId ?? Guid.Empty;

        return null;
    }

    protected override IActionResult RedirectToSurface(Guid id) =>
        RedirectToPage("/UploadGroupStatus", new { id });
}
