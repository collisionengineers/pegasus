using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Web.Presentation;
using Labels = Pegasus.Web.Presentation.OperatorLabels.Upload;

namespace Pegasus.Web.Pages;

/// <summary>
/// A stored upload of several files (v30 Upload E): the files inspected one
/// at a time on the left, the upload's one decision on the right. One
/// submission, one decision — never per file.
/// </summary>
[Authorize(
    Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
public sealed class UploadGroupStatusModel(
    IIntakeSubmissionGroupStore groups,
    IQueuedIntakeStatusQueries statuses,
    IUploadOutcomeQueries outcomeQueries,
    IUploadCaseDecision caseDecision,
    IGetIntake intake,
    ISearchCases searchCases,
    IDiscardIntakeSubmissionGroup discardSubmission,
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

    /// <summary>Each member's receipt (staged or processed), for its size, kind and image.</summary>
    public IReadOnlyDictionary<Guid, IntakeReceipt?> Receipts { get; private set; } =
        new Dictionary<Guid, IntakeReceipt?>();

    /// <summary>
    /// Set only when every member's outcome is the same Image-initiated Case
    /// registration. The group is the registration unit (one reference for
    /// the whole submission), so the page reports that registration once for
    /// the group instead of repeating the identical outcome per file.
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

    /// <summary>The current roster versions used by the terminal discard command.</summary>
    public IReadOnlyDictionary<Guid, long> GroupMemberReceiptVersions { get; private set; } =
        new Dictionary<Guid, long>();

    /// <summary>
    /// The page only offers discard after every member has completed. The
    /// serializable store is still authoritative for associations,
    /// allocations, registrations, and a changed durable roster.
    /// </summary>
    public bool CanDiscard { get; private set; }

    public IReadOnlyList<UploadCaseSuggestion> GroupSuggestedDestinations { get; private set; } = [];

    /// <summary>Original per-member versions retained while an error is re-rendered.</summary>
    public IReadOnlyDictionary<Guid, long>? GroupConfirmationReceiptVersions { get; private set; }

    /// <summary>
    /// The original Case version and roster remain on an error render.  A
    /// partial retry is the same staff decision, not a new one based on the
    /// page's now-smaller set of open members.
    /// </summary>
    public long? GroupConfirmationCaseVersion { get; private set; }

    /// <summary>
    /// The one decision for the upload (Upload planning, 13 September): attached to a
    /// Case, registered as vehicle images, or Unidentified. Null while any member is
    /// still moving or the members do not share one destination (the open decision
    /// then owns the choice).
    /// </summary>
    public sealed record SubmissionDecision(string Label, string Tone, string Message, UploadOutcomeAction? Action);

    public SubmissionDecision? Decision { get; private set; }

    /// <summary>Members processing could not read; each travels with the group to its destination.</summary>
    public int CouldNotBeReadCount => Group.Members.Count(member => member.CouldNotBeRead == true);

    /// <summary>Every member could not be read, so the group is one Unidentified item.</summary>
    public bool NoMemberCouldBeRead => Group.Members.Count > 0 && Group.Members.All(member => member.CouldNotBeRead == true);

    /// <summary>What the page draws.</summary>
    public UploadReviewView Review { get; private set; } = null!;

    private static SubmissionDecision? DecideSubmission(UploadOutcomeView?[] outcomes)
    {
        if (outcomes.Length == 0 || outcomes.Any(outcome => outcome is null || outcome.IsStillWorking))
        {
            return null;
        }

        var settled = outcomes.Select(outcome => outcome!).ToArray();
        var first = settled[0];
        var oneDestination = settled.Select(outcome => outcome.PrimaryAction?.Url).Distinct().Count() == 1;
        if (!oneDestination)
        {
            return null;
        }

        return settled switch
        {
            _ when settled.All(outcome => outcome.Kind == UploadOutcomeKind.Attached) =>
                new(OperatorLabels.UploadDecision.Attached, "green", first.Message, first.PrimaryAction),
            _ when settled.All(outcome => outcome.Kind == UploadOutcomeKind.ImageCaseRegistered) =>
                new(OperatorLabels.UploadDecision.VehicleImages, "green", first.Message, first.PrimaryAction),
            _ when settled.All(outcome => outcome.Kind is UploadOutcomeKind.NeedsReview or UploadOutcomeKind.Resolved) =>
                new(OperatorLabels.UploadDecision.Unidentified, "amber", first.Message, first.PrimaryAction),
            _ => null
        };
    }

    private bool _groupReadyForAttachment;

    // This surface owns the group-only handler below. Refusing the inherited
    // single-file handler prevents a forged post from treating a group id as a
    // receipt route.
    protected override Task<bool> SurfaceContainsReceiptAsync(
        Guid surfaceId,
        Guid receiptId,
        CancellationToken cancellationToken) => Task.FromResult(false);

    protected override IReadOnlyList<Guid> SearchReceiptIds => OpenGroupDecision ? OpenMemberReceiptIds : [];

    /// <param name="q">The Find term.</param>
    /// <param name="inspect">The file inspected on the left, one-based; the first when absent.</param>
    /// <param name="discard">Opens the discard confirmation, for a browser without script.</param>
    public async Task<IActionResult> OnGetAsync(
        Guid id,
        string? q,
        int? inspect,
        bool discard,
        CancellationToken cancellationToken)
    {
        if (await LoadAsync(id, cancellationToken) is { } notFound)
        {
            return notFound;
        }

        if (TryGetActor(out var actor))
        {
            await SearchAsync(q, actor, cancellationToken);
        }

        await BuildReviewAsync(id, inspect, discard, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAttachGroupAsync(
        Guid id,
        Guid? caseId,
        string? reference,
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
        try
        {
            if (operationId == Guid.Empty
                || !_groupReadyForAttachment
                || receiptVersions is null
                || !TryGetPostedRoster(receiptVersions, out var roster))
            {
                TempData["UploadConfirmationError"] = "This submission is incomplete or a file is not ready. No further files were added. Refresh and review every file before trying again.";
                PreserveGroupForm(receiptVersions, caseVersion, operationId, caseId, reference);
                return await RenderSurfaceAsync(id, cancellationToken);
            }

            // A chosen or typed Case takes the rendered confirmation step
            // before any write: the dialog repeats the exact target and binds
            // every member's reviewed receipt version and the Case version.
            if (caseId is null || caseVersion is null)
            {
                var firstReceiptId = roster[0];
                var confirmation = caseId is { } chosen
                    ? await _caseDecision.PrepareByCaseAsync(
                        firstReceiptId, chosen, operationId, receiptVersions[firstReceiptId], actor, cancellationToken)
                    : await _caseDecision.PrepareAsync(
                        firstReceiptId, reference, operationId, receiptVersions[firstReceiptId], actor, cancellationToken);
                if (confirmation is null
                    || !(await _caseDecision.SearchForUploadsAsync(
                        roster, confirmation.Reference, actor, cancellationToken))
                    .Any(candidate => candidate.CaseId == confirmation.CaseId
                        && candidate.Version == confirmation.Input.ExpectedCaseVersion))
                {
                    TempData["UploadConfirmationError"] = "No single viable case matched every file in this submission. Search and choose a case from the suggestions.";
                    PreserveGroupForm(receiptVersions, caseVersion, operationId, caseId, reference);
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
                PreserveGroupForm(receiptVersions, caseVersion, operationId, caseId, reference);
                return await RenderSurfaceAsync(id, cancellationToken);
            }

            var result = await _caseDecision.AttachGroupAsync(
                id, roster, caseId, reference, operationId,
                receiptVersions, reviewedCaseVersion, actor, cancellationToken);
            if (!result.Succeeded)
            {
                GroupConfirmationReceiptVersions = receiptVersions;
                GroupConfirmationCaseVersion = reviewedCaseVersion;
                UploadCaseConfirmation = new(
                    roster[0],
                    caseId.Value,
                    reference?.Trim() ?? "Selected case",
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

    public async Task<IActionResult> OnPostDiscardGroupAsync(
        Guid id,
        long? groupVersion,
        Dictionary<Guid, long>? receiptVersions,
        Guid operationId,
        bool consequencesConfirmed,
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
        if (!consequencesConfirmed)
        {
            TempData["UploadConfirmationError"] = Labels.DiscardMissing;
            return RedirectToSurface(id);
        }
        if (!CanDiscard || groupVersion is null || receiptVersions is null
            || !HasCurrentDiscardRoster(groupVersion.Value, receiptVersions))
        {
            TempData["UploadConfirmationError"] =
                "This submission changed or is still being processed. Refresh and try again.";
            return RedirectToSurface(id);
        }

        try
        {
            var result = await discardSubmission.ExecuteAsync(
                new(id, groupVersion.Value, receiptVersions, actor!, operationId),
                cancellationToken);
            if (result.Succeeded)
            {
                TempData["Confirmation"] = result.Message;
            }
            else
            {
                TempData["UploadConfirmationError"] = result.Message;
            }
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

        // Each member's status read, its receipt and — once terminal — its
        // confirmation outcome are independent reads against their own
        // DbContext (every store behind these ports is IDbContextFactory-backed,
        // not shared), so a group's members are read concurrently rather than
        // one durable round-trip at a time. This page polls itself while a
        // queue member or its later group-level outcome is still working, so
        // the saving is real.
        var memberResults = await Task.WhenAll(group.Members.Select(async member =>
        {
            var status = await statuses.GetAsync(member.StagedReceiptId, cancellationToken);
            UploadOutcomeView? outcome = null;
            IntakeReceipt? receipt = null;
            if (haveActor)
            {
                if (status is { Status: QueuedIntakeStatusKind.Complete or QueuedIntakeStatusKind.Failed })
                {
                    outcome = await outcomeQueries.BuildAsync(status, group.Id, actor!, cancellationToken);
                }

                receipt = await intake.ExecuteAsync(
                    new(status?.ProcessedReceiptId ?? member.StagedReceiptId, actor!), cancellationToken);
            }

            return (member.StagedReceiptId, status, outcome, receipt);
        }));

        Statuses = memberResults.ToDictionary(result => result.StagedReceiptId, result => result.status);
        Outcomes = memberResults.ToDictionary(result => result.StagedReceiptId, result => result.outcome);
        Receipts = memberResults.ToDictionary(result => result.StagedReceiptId, result => result.receipt);
        var outcomes = memberResults.Select(result => result.outcome).ToArray();
        Decision = DecideSubmission(outcomes);
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
        _groupReadyForAttachment = group.Channel == IntakeSourceChannel.ManualUpload
            && group.Members.Count == group.ExpectedMemberCount
            && group.Members.Count > 0
            && memberResults.All(result => result.status is { Status: QueuedIntakeStatusKind.Complete }
                && result.outcome is not null
                && (result.outcome.Attach is not null
                    || result.outcome.Kind is UploadOutcomeKind.Attached or UploadOutcomeKind.Resolved)
                && !result.outcome.IsStillWorking);
        OpenGroupDecision = _groupReadyForAttachment
            && (open.Length == group.Members.Count
                || (GroupConfirmationReceiptVersions is not null && UploadCaseConfirmation is not null));
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
        GroupMemberReceiptVersions = new Dictionary<Guid, long>();
        CanDiscard = false;
        if (haveActor && group.Discard is null && group.Channel == IntakeSourceChannel.ManualUpload
            && group.Members.Count == group.ExpectedMemberCount
            && memberResults.All(result => result.status is { Status: QueuedIntakeStatusKind.Complete,
                ProcessedReceiptId: not null } && result.receipt is not null))
        {
            var receipts = memberResults.Select(result => result.receipt!).ToArray();
            if (receipts.Select(receipt => receipt.Id).Distinct().Count() == group.Members.Count)
            {
                GroupMemberReceiptVersions = receipts.ToDictionary(
                    receipt => receipt.Id,
                    receipt => receipt.Version);
                CanDiscard = GroupMemberReceiptVersions.Count == group.Members.Count;
            }
        }
        if (haveActor && OpenGroupDecision)
        {
            GroupSuggestedDestinations = await _caseDecision.GetSuggestionsForUploadsAsync(
                OpenMemberReceiptIds, actor!, cancellationToken);
        }

        return null;
    }

    private async Task BuildReviewAsync(Guid id, int? inspect, bool discard, CancellationToken cancellationToken)
    {
        var haveActor = TryGetActor(out var actor);
        var files = Group.Members
            .OrderBy(member => member.Ordinal)
            .Select(member => ReviewFile(member))
            .ToArray();
        var phase = UploadReviewPhase.Report;
        UploadReviewReport? report = null;
        UploadReviewDestination? destination = null;
        var record = GroupRegistrationOutcome?.Record
            ?? Outcomes.Values.Select(outcome => outcome?.Record).FirstOrDefault(item => item is not null);
        if (Group.Discard is not null)
        {
            phase = UploadReviewPhase.Discarded;
        }
        else if (RefreshAutomatically)
        {
            phase = UploadReviewPhase.Pending;
        }
        else if (Group.Members.Count != Group.ExpectedMemberCount || Statuses.Values.Any(status => status is null))
        {
            report = new(Labels.ReviewRequiredEyebrow, Labels.IncompleteTitle, Labels.IncompleteSentence, null, OfferRefresh: true);
        }
        else if (OpenGroupDecision)
        {
            phase = UploadReviewPhase.Decision;
        }
        else if (Decision is { Label: var label } decided)
        {
            if (label == OperatorLabels.UploadDecision.Attached)
            {
                phase = UploadReviewPhase.Attached;
                var reference = Receipts.Values.Select(receipt => receipt?.CurrentCaseReference).FirstOrDefault(value => value is not null);
                destination = haveActor
                    ? await UploadReviewDestinations.LookupAsync(searchCases, actor!, reference, decided.Action?.Url, cancellationToken)
                    : null;
            }
            else if (label == OperatorLabels.UploadDecision.VehicleImages)
            {
                report = new(Labels.CompleteEyebrow, $"Registered as {Labels.ImageIntake}", record is null ? decided.Message : $"{Labels.RegisteredAutomatically} · {record.State}", decided.Action);
            }
            else
            {
                var first = Outcomes.Values.First(outcome => outcome is not null)!;
                var unidentified = decided.Action is { } action && action.Url.Contains("/Unidentified/", StringComparison.Ordinal)
                    ? action with { Label = Labels.OpenUnidentified }
                    : decided.Action;
                report = NoMemberCouldBeRead
                    ? new(Labels.ReviewRequiredEyebrow, Labels.UnreadableTitle, Labels.UnreadableSentence, unidentified)
                    : new(first.Kind == UploadOutcomeKind.Resolved ? Labels.CompleteEyebrow : Labels.ReviewRequiredEyebrow, first.StateLabel, first.Message, unidentified);
            }
        }
        else if (Statuses.Values.All(status => status is { Status: QueuedIntakeStatusKind.Failed }))
        {
            var reason = Outcomes.Values.Select(outcome => outcome?.Message).FirstOrDefault(message => message is not null)
                ?? OperatorLabels.IntakeFailure(Statuses.Values.First()!.FailureCode);
            report = new(Labels.ReviewRequiredEyebrow, "The files could not be processed", reason, null);
        }
        else
        {
            report = new(Labels.ReviewRequiredEyebrow, Labels.MixedOutcomesTitle, Labels.MixedOutcomesSentence, null);
            files = files.Select(file => file with
            {
                Action = Outcomes.GetValueOrDefault(file.StagedReceiptId)?.PrimaryAction
            }).ToArray();
        }

        var receiptVersions = GroupConfirmationReceiptVersions ?? OpenMemberReceiptVersions;
        var known = GroupSuggestedDestinations.Concat(SearchResults);
        var proposal = OpenGroupDecision
            ? Outcomes.Values.FirstOrDefault(outcome => outcome is { Kind: UploadOutcomeKind.ReadyToCreate, PrimaryAction: not null })?.PrimaryAction
            : null;
        Review = new UploadReviewView(
            phase,
            Group.ReceivedAtUtc,
            files,
            Math.Clamp((inspect ?? 1) - 1, 0, Math.Max(0, files.Length - 1)),
            $"/Upload/Group/{id:D}",
            "AttachGroup",
            UploadCaseConfirmation?.Input.OperationId ?? UploadCaseDraft?.OperationId ?? Guid.NewGuid(),
            receiptVersions)
        {
            NowUtc = timeProvider.GetUtcNow(),
            AutoRefreshMilliseconds = AutomaticRefreshMilliseconds,
            Error = TempData["UploadConfirmationError"] as string,
            Candidates = GroupSuggestedDestinations.Select(UploadReviewCandidate.From).ToArray(),
            SearchResults = SearchResults.Select(UploadReviewCandidate.From).ToArray(),
            SearchTerm = SearchTerm,
            SearchFailed = SearchFailed,
            Record = record is null ? null : new UploadReviewRecord(record.Reference, $"{Labels.RegisteredAutomatically} · {record.State}", record.Url),
            Proposal = proposal is null ? null : proposal with { Label = Labels.Proposal },
            Destination = destination,
            Report = report,
            Confirmation = ReviewConfirmation(UploadCaseConfirmation, known),
            CouldNotBeReadCount = CouldNotBeReadCount,
            CanDiscard = CanDiscard,
            GroupVersion = Group.Version,
            DiscardVersions = GroupMemberReceiptVersions,
            OpenDiscard = discard && CanDiscard
        };
    }

    private UploadReviewFile ReviewFile(IntakeSubmissionGroupMember member)
    {
        var status = Statuses.GetValueOrDefault(member.StagedReceiptId);
        var outcome = Outcomes.GetValueOrDefault(member.StagedReceiptId);
        var receipt = Receipts.GetValueOrDefault(member.StagedReceiptId);
        var unreadable = member.CouldNotBeRead == true;
        var (label, tone) = (Group.Discard, status?.Status, outcome?.Kind) switch
        {
            (not null, _, _) => (Labels.StateDiscarded, string.Empty),
            (_, null or QueuedIntakeStatusKind.Received, _) => (Labels.StateReceived, "is-running"),
            (_, QueuedIntakeStatusKind.Processing, _) => (Labels.StateProcessing, "is-running"),
            (_, QueuedIntakeStatusKind.Failed, _) => (Labels.StateFailed, "is-error"),
            (_, _, UploadOutcomeKind.Working) => (Labels.StateProcessing, "is-running"),
            (_, _, UploadOutcomeKind.Attached) => (unreadable ? Labels.StateAddedUnreadable : Labels.StateAdded, "is-done"),
            _ when unreadable => (Labels.StateCouldNotBeRead, "is-error"),
            _ => (Labels.StateReady, string.Empty)
        };
        var imageReceiptId = outcome?.ThumbnailReceiptId
            ?? (receipt is { MediaType: var mediaType } && mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? receipt.Id : null);
        return new UploadReviewFile(
            member.Ordinal,
            member.StagedReceiptId,
            member.SourceFileName,
            receipt?.SourceLength,
            Labels.Kind(receipt?.MediaType, member.SourceFileName),
            imageReceiptId is { } imageId ? $"/Received/{imageId:D}/Image" : null,
            receipt is null ? null : $"/Received/{receipt.Id:D}/Source",
            label,
            tone,
            unreadable);
    }

    protected override IActionResult RedirectToSurface(Guid id) =>
        RedirectToPage("/UploadGroupStatus", new { id });

    protected override async Task<IActionResult> RenderSurfaceAsync(
        Guid surfaceId,
        CancellationToken cancellationToken)
    {
        if (await LoadAsync(surfaceId, cancellationToken) is { } notFound)
        {
            return notFound;
        }

        await BuildReviewAsync(surfaceId, null, false, cancellationToken);
        return Page();
    }

    private bool TryGetPostedRoster(
        Dictionary<Guid, long>? receiptVersions,
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
        // a receipt outside this submission. Include every member, including
        // completed members whose identical decision the attachment owner must
        // prove before continuing a partial retry.
        var ordered = GroupMemberReceiptIds
            .Where(receiptVersions.ContainsKey)
            .ToArray();
        if (ordered.Length != receiptVersions.Count || ordered.Length != GroupMemberReceiptIds.Count)
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
        Dictionary<Guid, long>? receiptVersions,
        long? caseVersion,
        Guid operationId,
        Guid? caseId,
        string? reference)
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
                    reference);
            }
        }
    }

    private bool HasCurrentDiscardRoster(
        long groupVersion,
        Dictionary<Guid, long> receiptVersions) =>
        groupVersion == Group.Version
        && receiptVersions.Count == GroupMemberReceiptVersions.Count
        && receiptVersions.All(item => GroupMemberReceiptVersions.TryGetValue(item.Key, out var current)
            && current == item.Value);
}
