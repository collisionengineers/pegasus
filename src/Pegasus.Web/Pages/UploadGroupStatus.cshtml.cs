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
    IImageIntakeOriginResolver imageIntakeOriginResolver,
    TimeProvider timeProvider) : UploadConfirmationPageModel(caseDecision)
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

    public bool RefreshAutomatically => AutomaticRefreshMilliseconds is not null;

    /// <summary>
    /// How long before this page reloads itself: the shortest wait among the
    /// members still moving, so one member waiting on a scheduled retry never
    /// holds the whole submission back and never keeps the page reloading
    /// every two seconds on its account either. Null once nothing is moving.
    /// </summary>
    public int? AutomaticRefreshMilliseconds
    {
        get
        {
            // A member past the queue but whose group-level outcome is still
            // resolving could settle at any moment, and carries no due time
            // to wait on.
            if (Outcomes.Values.Any(outcome => outcome?.IsStillWorking == true))
            {
                return UploadStatusRefresh.MinimumMilliseconds;
            }

            // A member with no status row yet is still arriving, not finished.
            var moving = Statuses.Values
                .Where(status => status is null
                    or { Status: QueuedIntakeStatusKind.Received or QueuedIntakeStatusKind.Processing })
                .ToArray();
            return moving.Length == 0
                ? null
                : moving.Min(status =>
                    UploadStatusRefresh.DelayMilliseconds(status, timeProvider.GetUtcNow()));
        }
    }

    /// <summary>
    /// Set when any member still needs a staff decision: the submission is
    /// decided once, as one unit, never per file.
    /// </summary>
    public bool OpenGroupDecision { get; private set; }

    /// <summary>The still-open members' processed receipt ids, in member order.</summary>
    public IReadOnlyList<Guid> OpenMemberReceiptIds { get; private set; } = [];

    /// <summary>Every current group member in durable submission order.</summary>
    public IReadOnlyList<Guid> GroupMemberReceiptIds { get; private set; } = [];

    /// <summary>Rendered receipt versions for the current one-submission decision.</summary>
    public IReadOnlyDictionary<Guid, long> OpenMemberReceiptVersions { get; private set; } =
        new Dictionary<Guid, long>();

    public IReadOnlyList<UploadCaseSuggestion> GroupSuggestedDestinations { get; private set; } = [];

    /// <summary>Original per-member versions retained while an error is re-rendered.</summary>
    public IReadOnlyDictionary<Guid, long>? GroupConfirmationReceiptVersions { get; private set; }

    /// <summary>
    /// The original Case version and roster remain on an error render.  A
    /// partial retry is the same staff decision, not a new one based on the
    /// page's now-smaller set of open members.
    /// </summary>
    public long? GroupConfirmationCaseVersion { get; private set; }

    public bool OfferGroupRegistration { get; private set; }

    private Guid _firstOpenImageReceiptId;

    // This surface owns the group-only handler below. Refusing the inherited
    // single-file handler prevents a forged post from treating a group id as a
    // receipt route.
    protected override Task<bool> SurfaceContainsReceiptAsync(
        Guid surfaceId,
        Guid receiptId,
        CancellationToken cancellationToken) => Task.FromResult(false);

    protected override async Task<IReadOnlyList<Guid>> SearchReceiptIdsAsync(
        Guid surfaceId,
        CancellationToken cancellationToken)
    {
        // The search endpoint is invoked independently of the page GET.
        // Rebuild the same open decision roster, rather than sending every
        // historical group member to the destination policy (settled members
        // are correctly no longer viable and would otherwise empty the
        // intersection for the whole submission).
        if (await LoadAsync(surfaceId, cancellationToken) is not null)
        {
            return [];
        }
        return OpenMemberReceiptIds;
    }

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
        Guid operationId,
        Dictionary<Guid, long>? receiptVersions,
        long? caseVersion,
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
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
        {
            TempData["UploadConfirmationError"] = "A reason is required to add this to a case.";
            PreserveGroupForm(receiptVersions, caseVersion, operationId, caseId, reference, reason);
            return await RenderSurfaceAsync(id, cancellationToken);
        }

        try
        {
            if (operationId == Guid.Empty
                || !TryGetPostedRoster(receiptVersions, out var roster))
            {
                TempData["UploadConfirmationError"] = "This confirmation is incomplete. Refresh and try again.";
                PreserveGroupForm(receiptVersions, caseVersion, operationId, caseId, reference, reason);
                return await RenderSurfaceAsync(id, cancellationToken);
            }

            if (caseId is null)
            {
                var firstReceiptId = roster[0];
                var confirmation = await _caseDecision.PrepareAsync(
                    firstReceiptId, reference, reason, operationId, receiptVersions[firstReceiptId], actor, cancellationToken);
                var allViable = confirmation is not null
                    && (await _caseDecision.SearchForUploadsAsync(
                        roster, confirmation.Reference, actor, cancellationToken))
                    .Any(candidate => candidate.CaseId == confirmation.CaseId
                        && candidate.Version == confirmation.Input.ExpectedCaseVersion);
                if (!allViable)
                {
                    TempData["UploadConfirmationError"] = "No single viable case matched every file in this submission. Search and choose a case from the suggestions.";
                    PreserveGroupForm(receiptVersions, caseVersion, operationId, caseId, reference, reason);
                    return await RenderSurfaceAsync(id, cancellationToken);
                }

                UploadCaseConfirmation = confirmation;
                GroupConfirmationReceiptVersions = receiptVersions;
                GroupConfirmationCaseVersion = confirmation.Input.ExpectedCaseVersion;
                return await RenderSurfaceAsync(id, cancellationToken);
            }
            if (caseVersion is not { } reviewedCaseVersion || reviewedCaseVersion < 0)
            {
                TempData["UploadConfirmationError"] = "This confirmation is incomplete. Choose the case again.";
                PreserveGroupForm(receiptVersions, caseVersion, operationId, caseId, reference, reason);
                return await RenderSurfaceAsync(id, cancellationToken);
            }

            var result = await _caseDecision.AttachGroupAsync(
                id, roster, caseId, reference, reason, operationId,
                receiptVersions, reviewedCaseVersion, actor, cancellationToken);
            if (!result.Succeeded)
            {
                GroupConfirmationReceiptVersions = receiptVersions;
                GroupConfirmationCaseVersion = reviewedCaseVersion;
                UploadCaseConfirmation = new(
                    roster[0],
                    caseId.Value,
                    reference?.Trim() ?? "Selected case",
                    reason,
                    new(operationId, receiptVersions[roster[0]], reviewedCaseVersion));
                TempData["UploadConfirmationError"] = result.Message;
                return await RenderSurfaceAsync(id, cancellationToken);
            }
            TempData["Confirmation"] = result.Message;
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

        // A registered image group is reported once through
        // GroupRegistrationOutcome but AwaitingInstruction still has one
        // submission-level destination decision.  Its Image record's origin
        // is lifecycle data, not the posted receipt for every member.
        var open = memberResults
            .Where(result => result.outcome is { IsOpenDecision: true, Attach: not null })
            .ToArray();
        OpenGroupDecision = !RefreshAutomatically
            && open.Length > 0;
        OpenMemberReceiptIds = open
            .Select(result => result.status!.ProcessedReceiptId ?? result.status.StagedReceiptId)
            .ToArray();
        GroupMemberReceiptIds = memberResults
            .Select(result => result.status?.ProcessedReceiptId ?? result.status?.StagedReceiptId)
            .Where(receiptId => receiptId is not null)
            .Select(receiptId => receiptId!.Value)
            .ToArray();
        OpenMemberReceiptVersions = open.ToDictionary(
            result => result.status!.ProcessedReceiptId ?? result.status.StagedReceiptId,
            result => result.outcome!.Attach!.ReceiptVersion);
        if (haveActor && OpenMemberReceiptIds.Count > 0)
        {
            GroupSuggestedDestinations = await _caseDecision.GetSuggestionsForUploadsAsync(
                OpenMemberReceiptIds, actor!, cancellationToken);
        }
        var firstOpenImage = open.FirstOrDefault(result => result.outcome!.ThumbnailReceiptId is not null);
        OfferGroupRegistration = OpenGroupDecision
            && GroupRegistrationOutcome is null
            && firstOpenImage.outcome is not null;
        _firstOpenImageReceiptId = firstOpenImage.outcome?.ThumbnailReceiptId ?? Guid.Empty;

        return null;
    }

    protected override IActionResult RedirectToSurface(Guid id) =>
        RedirectToPage("/UploadGroupStatus", new { id });

    protected override async Task<IActionResult> RenderSurfaceAsync(
        Guid surfaceId,
        CancellationToken cancellationToken) =>
        (await LoadAsync(surfaceId, cancellationToken)) ?? Page();

    private bool TryGetPostedRoster(
        IReadOnlyDictionary<Guid, long>? receiptVersions,
        out IReadOnlyList<Guid> roster)
    {
        roster = [];
        if (receiptVersions is null || receiptVersions.Count == 0
            || receiptVersions.Any(pair => pair.Key == Guid.Empty || pair.Value < 0))
        {
            return false;
        }

        // Form keys can be reordered by the binder.  The group is the only
        // source of execution order, and it also prevents a post from naming
        // a receipt outside this submission.  The roster must include every
        // member that remains open now; completed members from an unchanged
        // retry are allowed in addition, never as a replacement.
        var ordered = GroupMemberReceiptIds
            .Where(receiptVersions.ContainsKey)
            .ToArray();
        if (ordered.Length != receiptVersions.Count)
        {
            return false;
        }
        if (OpenMemberReceiptIds.Any(member => !receiptVersions.ContainsKey(member)))
        {
            return false;
        }

        roster = ordered;
        return true;
    }

    private void PreserveGroupForm(
        IReadOnlyDictionary<Guid, long>? receiptVersions,
        long? caseVersion,
        Guid operationId,
        Guid? caseId,
        string? reference,
        string? reason)
    {
        GroupConfirmationReceiptVersions = receiptVersions;
        GroupConfirmationCaseVersion = caseVersion;
        if (receiptVersions is { Count: > 0 })
        {
            var first = GroupMemberReceiptIds.FirstOrDefault(receiptVersions.ContainsKey);
            if (first != Guid.Empty)
            {
                UploadCaseDraft = new(
                    first, operationId, receiptVersions[first], caseId, caseVersion,
                    reference, reason ?? string.Empty);
            }
        }
    }
}
