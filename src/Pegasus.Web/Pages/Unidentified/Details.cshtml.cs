using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Unidentified;

/// <summary>
/// The Unidentified record (v26, received file D2): everything the removed
/// received-file page hosted for material no route identified. Seven actions —
/// Open file, Open message, Request again, Link to Case, Create case, Register
/// images, Close with reason — plus Open the Triage, registration readings with
/// Dismiss, and Reopen on a closed item. Nothing here links to a receipt page.
/// </summary>
[Authorize(Roles = StaffRoleNames.Administrator + "," + StaffRoleNames.Engineer + "," + StaffRoleNames.User)]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed partial class DetailsModel(
    IGetUnidentifiedItemContext getContext,
    IGetIntake getIntake,
    IUnidentifiedStore store,
    ICloseUnidentified closeUnidentified,
    IReopenUnidentified reopenUnidentified,
    IIntakeAssociationDestinationQueries destinations,
    IAcquireCaseEditLease acquireLease,
    IReleaseCaseEditLease releaseLease,
    ILinkIntake linkIntake,
    IImageIntakeOriginResolver originResolver,
    IRegisterImageIntake registerImageIntake,
    IVrmSuggestionStore vrmSuggestions,
    ICreateTriageFromIntake createTriage,
    ReconcileUnidentifiedDestinations reconcile,
    IIntakeSubmissionGroupStore submissionGroups,
    IGetPreCaseImagePreparations getPreparations,
    Pegasus.Core.Documents.IReadImageTagVocabulary tagVocabulary,
    TimeProvider timeProvider,
    ILogger<DetailsModel> logger) : StaffPageModel
{
    public const string LinkDialog = "link";
    public const string RegisterDialog = "register";
    public const string TriageDialog = "triage";
    public const string CloseDialog = "close";
    public const string ReopenDialog = "reopen";

    public UnidentifiedItemContext Context { get; private set; } = null!;

    /// <summary>The item's image's recorded crop and tags (pre-Case crop and tag, v26).</summary>
    public IReadOnlyDictionary<Guid, PreCaseImagePreparation> Preparations { get; private set; } =
        new Dictionary<Guid, PreCaseImagePreparation>();

    /// <summary>The tag vocabulary the viewer's Tag select offers.</summary>
    public IReadOnlyList<Pegasus.Core.Documents.ImageTag> ImageTags { get; private set; } = [];

    public UnidentifiedItem Item => Context.Item;

    public IntakeReceipt? Receipt => Context.Receipt;

    public IntakeSubmissionGroup? SubmissionGroup { get; private set; }

    /// <summary>
    /// The receipt or completed members that supply the material this item
    /// represents. A group origin has no single receipt of its own.
    /// </summary>
    public IReadOnlyList<IntakeReceipt> MaterialReceipts { get; private set; } = [];

    /// <summary>The retained originals and non-image e-mail attachments.</summary>
    public IReadOnlyList<RetainedIntakeFile> RetainedFiles => MaterialReceipts
        .SelectMany(receipt => IntakeFileIdentity.Ordered(receipt)
            .Where(asset => asset.Kind == IntakeAssetKind.Source
                || (receipt.SourceIdentity.Channel == IntakeSourceChannel.Mailbox
                    && asset.Kind == IntakeAssetKind.Attachment
                    && !InstructionEvidenceImages.IsImage(asset.MediaType)))
            .Select(asset => new RetainedIntakeFile(
                receipt.Id,
                asset,
                asset.Kind == IntakeAssetKind.Source)))
        .ToArray();

    public IReadOnlyList<UnidentifiedHistoryEntry> History { get; private set; } = [];

    public bool IsOpen => Item.State == UnidentifiedState.Open;

    /// <summary>The dialog to open on load: a no-script search round trip or a refused post keeps its dialog open.</summary>
    [BindProperty(SupportsGet = true, Name = "dialog")]
    public string? OpenDialog { get; set; }

    [BindProperty(SupportsGet = true, Name = "caseQuery")]
    public string? CaseQuery { get; set; }

    public IReadOnlyList<IntakeAssociationDestination> CaseResults { get; private set; } = [];

    /// <summary>Link to Case is offered for retained material a Case can take.</summary>
    public bool CanLinkCase => IsOpen && Receipt is { } receipt && IntakeAssociationDestinationPolicy.CanOffer(receipt);

    /// <summary>A manual upload group is linked or registered from its own group decision.</summary>
    public bool IsUploadGroup => SubmissionGroup?.Channel == IntakeSourceChannel.ManualUpload;

    public bool CanCreateCase => IsOpen && Receipt is { } receipt
        && receipt.AcceptedCaseId is null
        && receipt.AllocationState is null
        && (IntakeDecisionPolicy.CanBecomeCase(receipt.Decision) || receipt.Decision == IntakeDecision.OcrRequired);

    public bool CanRegisterImages => Context.CanRegisterImages;

    public bool CanOpenTriage => Context.CanOpenTriage;

    /// <summary>The original retained file, separately from extracted photographs.</summary>
    public IntakeAssetRecord? SourceAsset => Receipt is { } receipt
        ? IntakeFileIdentity.SourceAsset(receipt)
        : null;

    /// <summary>
    /// The selected photographs this item contains. A PDF may carry several;
    /// its source media type must not suppress their review surface.
    /// </summary>
    public IReadOnlyList<RetainedIntakeImage> EvidenceImages => MaterialReceipts
        .SelectMany(receipt => InstructionEvidenceImages.Select(receipt.AssetRecords)
            .Select(asset => new RetainedIntakeImage(receipt.Id, asset)))
        .ToArray();

    /// <summary>
    /// VRM observations from each represented receipt. Group action semantics
    /// remain with its Upload Group page, so this list is review-only there.
    /// </summary>
    public IReadOnlyList<ImageVrmSuggestion> RegistrationReadings { get; private set; } = [];

    public bool IsSourceStored => SourceAsset?.CustodyState == IncomingArtifactCustodyState.Confirmed;

    /// <summary>Open file: the retained original, through the kept source route.</summary>
    public string? OpenFileHref => Receipt is { } receipt && Context.SourceMessageId is null && IsSourceStored
        ? $"/Received/{receipt.Id:D}/Source"
        : Receipt is { } emailReceipt && IsSourceStored && !IsEmail
            ? $"/Received/{emailReceipt.Id:D}/Source"
            : null;

    public string? OpenMessageHref => Context.SourceMessageId is { } messageId ? $"/Inbox/{messageId:D}" : null;

    /// <summary>
    /// Request again composes a reply to the message the material came in.
    /// Material without a source message has no Request again action.
    /// </summary>
    public string? RequestAgainHref => IsOpen && Context.SourceMessageId is { } messageId
        ? $"/Inbox/{messageId:D}?compose=reply"
        : null;

    public bool IsEmail => UnidentifiedMediaKindPolicy.Classify(Receipt?.SourceIdentity.Channel, Receipt?.MediaType)
        == UnidentifiedMediaKind.Email;

    public string MaterialLabel => OperatorLabels.UnidentifiedMediaKind(
        UnidentifiedMediaKindPolicy.Classify(Receipt?.SourceIdentity.Channel, Receipt?.MediaType));

    /// <summary>The filename or the e-mail's subject and sender; never an identifier.</summary>
    public string Handle
    {
        get
        {
            if (Receipt is not { } receipt)
            {
                return SubmissionGroup is { } group ? $"{group.Members.Count} retained files" : "Not available";
            }

            if (IsEmail)
            {
                var subject = receipt.Evidence.FirstOrDefault(item => item.Source == IntakeEvidenceSource.Subject)?.Detail;
                return OperatorLabels.EmailHandle(subject, receipt.MailRouteDecision?.EffectiveSender?.Address);
            }

            return receipt.SourceFileName;
        }
    }

    public string SourceLabel => Receipt is { } receipt
        ? OperatorLabels.SourceChannel(receipt.SourceIdentity.Channel)
        : SubmissionGroup is { } group ? OperatorLabels.SourceChannel(group.Channel) : "Not available";

    /// <summary>"Could not be read · PDF" for an unreadable item; null for every other reason.</summary>
    public string? CouldNotBeRead => Item.ReasonCode == UnidentifiedReasonCode.CouldNotBeRead
        ? string.IsNullOrWhiteSpace(Item.FileKind) ? "Could not be read" : $"Could not be read · {Item.FileKind}"
        : null;

    public string? ClosedOutcome => Item.IsClosed
        ? string.IsNullOrWhiteSpace(Item.ResolutionReason) ? "Closed" : $"Closed · {Item.ResolutionReason}"
        : null;

    [TempData(Key = "UnidentifiedStatus")]
    public string? StatusMessage { get; set; }

    [TempData(Key = "UnidentifiedError")]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken) =>
        await LoadAsync(id, cancellationToken) ?? Page();

    /// <summary>Link to Case: the chosen Case's lease is claimed for the link and consumed by it.</summary>
    public async Task<IActionResult> OnPostLinkCaseAsync(
        Guid id,
        Guid caseId,
        string operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        CaseEditLease? lease = null;
        try
        {
            var context = await getContext.ExecuteAsync(actor, id, cancellationToken);
            if (context?.Receipt is not { } receipt || context.Item.State != UnidentifiedState.Open)
            {
                throw new InvalidOperationException("The item can no longer be linked.");
            }

            var destination = await destinations.GetAsync(receipt, caseId, actor, cancellationToken)
                ?? throw new InvalidOperationException("The selected Case is not available for this material.");
            lease = await acquireLease.ExecuteAsync(
                new ClaimCaseEditLeaseRequest(caseId, destination.Version, actor, NewOperationKey()),
                cancellationToken);
            await linkIntake.ExecuteAsync(
                new LinkIntakeRequest(
                    receipt.Id,
                    caseId,
                    receipt.Version,
                    lease.Version,
                    lease.Token,
                    actor,
                    operationKey,
                    $"Linked from {context.Item.Reference}."),
                cancellationToken);
            lease = null;
            await SynchronizeAsync(receipt.Id, actor, cancellationToken);
            StatusMessage = $"{context.Item.Reference} was linked to {destination.Reference}.";
            return RedirectToPage(new { id });
        }
        catch (StaffAuthorizationException)
        {
            await ReleaseQuietlyAsync(caseId, actor, lease);
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCommandFailed(logger, "link_case", id, exception);
            await ReleaseQuietlyAsync(caseId, actor, lease);
            ErrorMessage = "The material was not linked because the Case or the item changed, someone is editing the Case, or the link is not permitted.";
            return RedirectToPage(new { id, dialog = LinkDialog, caseQuery = CaseQuery });
        }
    }

    /// <summary>Register images: the item's material becomes an Image-initiated Case under the registration given.</summary>
    public Task<IActionResult> OnPostRegisterImagesAsync(
        Guid id,
        string? vehicleRegistration,
        string reason,
        string operationKey,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            id,
            "register_images",
            RegisterDialog,
            async (actor, context) =>
            {
                if (!context.CanRegisterImages || context.Receipt is not { } receipt)
                {
                    throw new InvalidOperationException("Images cannot be registered from this item.");
                }

                var normalized = ImageIntakeLifecycleRules.NormalizeRegistrationInput(vehicleRegistration);
                var origin = await originResolver.ResolveOriginAsync(receipt.Id, cancellationToken)
                    ?? throw new InvalidOperationException("The material has no completed evaluation to register from.");
                var record = await registerImageIntake.ExecuteAsync(
                    new(origin, normalized, actor, operationKey, reason),
                    cancellationToken);
                await ConfirmMatchingReadingsAsync(context, record, actor, cancellationToken);
                await SynchronizeAsync(receipt.Id, actor, cancellationToken);
                return $"Registered as vehicle images {record.ImageIntakeReference}.";
            },
            cancellationToken);

    /// <summary>
    /// Open the Triage: a Triage request held here for want of a registration is
    /// promoted by someone supplying one; the receipt's accepted Triage-match
    /// record is passed back as recorded.
    /// </summary>
    public Task<IActionResult> OnPostOpenTriageAsync(
        Guid id,
        string? vehicleRegistration,
        string operationKey,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            id,
            "open_triage",
            TriageDialog,
            async (actor, context) =>
            {
                StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
                if (!context.CanOpenTriage || context.Receipt is not { } receipt)
                {
                    throw new InvalidOperationException("A Triage cannot be opened from this item.");
                }

                var acceptedMatch = receipt.Evidence.Single(
                    evidence => evidence.Finding == IntakeEvidenceFinding.AcceptedTriageMatch);
                var origin = await originResolver.ResolveOriginAsync(receipt.Id, cancellationToken)
                    ?? throw new InvalidOperationException("The material has no completed evaluation to open a Triage from.");
                await createTriage.ExecuteAsync(
                    new(
                        new TriageOrigin(origin.ReceiptId, origin.SourceIdentity, origin.SourceHash, origin.EvaluationRevisionId),
                        ImageIntakeLifecycleRules.NormalizeRegistrationInput(vehicleRegistration),
                        acceptedMatch,
                        actor,
                        $"triage-from-staff:{operationKey}"),
                    cancellationToken);
                await SynchronizeAsync(receipt.Id, actor, cancellationToken);
                return "The Triage was opened.";
            },
            cancellationToken);

    /// <summary>Close with reason (received file D5): the item leaves the open list and stays reachable under Closed.</summary>
    public Task<IActionResult> OnPostCloseAsync(
        Guid id,
        long expectedVersion,
        string reason,
        string operationKey,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            id,
            "close",
            CloseDialog,
            async (actor, context) =>
            {
                await closeUnidentified.ExecuteAsync(
                    new CloseUnidentifiedRequest(id, expectedVersion, actor, operationKey, reason, timeProvider.GetUtcNow()),
                    cancellationToken);
                return $"{context.Item.Reference} was closed.";
            },
            cancellationToken);

    /// <summary>Reopen a resolved item with a reason; it returns to the open list.</summary>
    public Task<IActionResult> OnPostReopenAsync(
        Guid id,
        long expectedVersion,
        string reason,
        string operationKey,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            id,
            "reopen",
            ReopenDialog,
            async (actor, context) =>
            {
                await reopenUnidentified.ExecuteAsync(
                    new ReopenUnidentifiedRequest(id, expectedVersion, actor, operationKey, reason, timeProvider.GetUtcNow()),
                    cancellationToken);
                return $"{context.Item.Reference} was reopened.";
            },
            cancellationToken);

    /// <summary>Dismiss a registration reading with a reason.</summary>
    public Task<IActionResult> OnPostDismissReadingAsync(
        Guid id,
        Guid suggestionId,
        string reason,
        string operationKey,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            id,
            "dismiss_reading",
            null,
            async (actor, context) =>
            {
                if (context.RegistrationReadings.All(reading => reading.Id != suggestionId))
                {
                    throw new InvalidOperationException("The reading is not on this item.");
                }

                await vrmSuggestions.SetDispositionAsync(
                    new(suggestionId, ImageVrmSuggestionDisposition.Dismissed, actor, reason, operationKey),
                    cancellationToken);
                return "The registration reading was dismissed.";
            },
            cancellationToken);

    public static string ReadingLabel(ImageVrmSuggestion reading) => reading.Outcome switch
    {
        VrmRecognitionOutcomeKind.Suggested => reading.SuggestedRegistration ?? "Not recorded",
        VrmRecognitionOutcomeKind.NoReadableResult => "No readable registration",
        VrmRecognitionOutcomeKind.TechnicalFailure => "Technical failure",
        VrmRecognitionOutcomeKind.Unavailable => "Recognition unavailable",
        _ => OperatorLabels.Humanise(reading.Outcome.ToString())
    };

    public static string DispositionLabel(ImageVrmSuggestionDisposition disposition) => disposition switch
    {
        ImageVrmSuggestionDisposition.Pending => "Not decided",
        ImageVrmSuggestionDisposition.Dismissed => "Dismissed",
        ImageVrmSuggestionDisposition.Confirmed => "Accepted",
        _ => OperatorLabels.Humanise(disposition.ToString())
    };

    public static string CustodyLabel(IncomingArtifactCustodyState state) => state switch
    {
        IncomingArtifactCustodyState.Pending => "Storing",
        IncomingArtifactCustodyState.Confirmed => "Stored",
        IncomingArtifactCustodyState.Unknown => "Storage status unknown",
        IncomingArtifactCustodyState.Failed => "Storage failed",
        _ => OperatorLabels.Humanise(state.ToString())
    };

    private async Task<IActionResult?> LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        UnidentifiedItemContext? context;
        try
        {
            context = await getContext.ExecuteAsync(actor, id, cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }

        if (context is null)
        {
            return NotFound();
        }

        Context = context;
        MaterialReceipts = context.Receipt is { } originReceipt ? [originReceipt] : [];
        RegistrationReadings = context.RegistrationReadings;
        if (context.Item.Origin.Kind == UnidentifiedOriginKind.SubmissionGroup)
        {
            SubmissionGroup = await submissionGroups.GetAsync(context.Item.Origin.Id, cancellationToken);
            if (SubmissionGroup is not null)
            {
                var receipts = new List<IntakeReceipt>();
                var readings = new List<ImageVrmSuggestion>();
                foreach (var receiptId in SubmissionGroup.Members
                             .OrderBy(member => member.Ordinal)
                             .Select(member => member.ProcessedReceiptId)
                             .OfType<Guid>()
                             .Distinct())
                {
                    var memberReceipt = await getIntake.ExecuteAsync(
                        new GetIntakeQuery(receiptId, actor), cancellationToken);
                    if (memberReceipt is null)
                    {
                        continue;
                    }

                    receipts.Add(memberReceipt);
                    readings.AddRange(await vrmSuggestions.ListForReceiptAsync(receiptId, cancellationToken));
                }

                MaterialReceipts = receipts;
                RegistrationReadings = readings;
            }
        }

        var imageAssetIds = EvidenceImages.Select(image => image.Asset.Id).ToArray();
        if (imageAssetIds.Length > 0)
        {
            Preparations = await getPreparations.ExecuteAsync(actor, imageAssetIds, cancellationToken);
            ImageTags = await tagVocabulary.ListAsync(cancellationToken);
        }

        History = await store.HistoryAsync(id, cancellationToken);
        if (CanLinkCase && Receipt is { } receipt)
        {
            var query = CaseQuery?.Trim();
            CaseResults = string.IsNullOrWhiteSpace(query) || query.Length < 2
                ? await destinations.GetSuggestedAsync(receipt, actor, cancellationToken)
                : await destinations.SearchAsync(receipt, query.Length > 300 ? query[..300] : query, actor, cancellationToken);
        }

        ViewData["WorkingSetRecord"] = new WorkingSetRecord(
            $"/Unidentified/{id:D}",
            WorkingSetRecord.Kinds.Unidentified,
            context.Item.Reference);
        return null;
    }

    private async Task<IActionResult> ExecuteAsync(
        Guid id,
        string commandName,
        string? dialog,
        Func<Pegasus.Core.Identity.ActionActor, UnidentifiedItemContext, Task<string>> execute,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var context = await getContext.ExecuteAsync(actor, id, cancellationToken);
            if (context is null)
            {
                return NotFound();
            }

            StatusMessage = await execute(actor, context);
            return RedirectToPage(new { id });
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCommandFailed(logger, commandName, id, exception);
            ErrorMessage = exception is ArgumentException argument
                ? argument.Message
                : "The action was not applied because the item changed or the action is not permitted. Reload and try again.";
            return dialog is null ? RedirectToPage(new { id }) : RedirectToPage(new { id, dialog });
        }
    }

    /// <summary>
    /// Settles the item against the destination the material now has, through
    /// the one owner of that rule, reading the receipt afresh so the new link,
    /// registration or Triage is in hand. Advisory: the periodic sweep is the
    /// backstop, except for a permanently taken operation key, which surfaces.
    /// </summary>
    private async Task SynchronizeAsync(Guid receiptId, Pegasus.Core.Identity.ActionActor actor, CancellationToken cancellationToken)
    {
        try
        {
            if (await getIntake.ExecuteAsync(new GetIntakeQuery(receiptId, actor), cancellationToken) is { } receipt)
            {
                await reconcile.SynchronizeForReceiptAsync(receipt, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not UnidentifiedOperationConflictException && IntakeExceptionPolicy.IsRecoverable(exception))
        {
            LogCommandFailed(logger, "synchronize", receiptId, exception);
        }
    }

    /// <summary>A reading the staff registration used is confirmed; bookkeeping over a committed registration.</summary>
    private async Task ConfirmMatchingReadingsAsync(
        UnidentifiedItemContext context,
        ImageIntakeRecord record,
        Pegasus.Core.Identity.ActionActor actor,
        CancellationToken cancellationToken)
    {
        foreach (var reading in context.RegistrationReadings.Where(reading =>
                     reading.Disposition == ImageVrmSuggestionDisposition.Pending
                     && reading.Outcome == VrmRecognitionOutcomeKind.Suggested
                     && string.Equals(reading.SuggestedRegistration, record.NormalizedVehicleRegistration, StringComparison.Ordinal)))
        {
            try
            {
                await vrmSuggestions.SetDispositionAsync(
                    new(reading.Id, ImageVrmSuggestionDisposition.Confirmed, actor,
                        "The staff registration used this suggested registration.", $"vrm-confirm:{reading.Id:N}"),
                    cancellationToken);
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                LogCommandFailed(logger, "confirm_reading", reading.Id, exception);
            }
        }
    }

    private async Task ReleaseQuietlyAsync(Guid caseId, Pegasus.Core.Identity.ActionActor actor, CaseEditLease? lease)
    {
        if (lease is null)
        {
            return;
        }

        try
        {
            await releaseLease.ExecuteAsync(
                new ReleaseCaseEditLeaseRequest(caseId, actor, NewOperationKey(), lease.Token),
                CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogCommandFailed(logger, "release_lease", caseId, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unidentified command {CommandName} failed for {RecordId}.")]
    private static partial void LogCommandFailed(ILogger logger, string commandName, Guid recordId, Exception exception);
}
