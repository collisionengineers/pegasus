using System.Diagnostics;
using System.Security.Cryptography;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.ThirdPartyReports;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.Notifications;

namespace Pegasus.Core.ImageIntake;

/// <summary>
/// What one automation pass concluded about a receipt.
/// </summary>
/// <param name="Receipt">The receipt's state after this pass.</param>
/// <param name="GroupPending">
/// True only when <paramref name="Receipt"/> is a member of a submission
/// group whose own outcome did not complete this pass — the group is still
/// waiting on sibling members/recognition, or this receipt's own
/// registration attempt lost a transient concurrency race. Always false for
/// the non-group path and for a group that resolved to a legitimate terminal
/// outcome (no usable or ambiguous VRM, technical failure), which are
/// unchanged Unidentified scope. What the caller does with a pending outcome is
/// owned by <c>ProcessQueuedIntake.ApplyImageIntakeAutomationAsync</c>.
/// </param>
/// <param name="AssociatedCase">
/// True when the images were filed on an existing Case without registering an
/// Image intake (operator, 11 October 2026): the caller promotes the receipt's
/// evidence to that Case.
/// </param>
/// <param name="AwaitingStaffDecision">
/// True when a manual upload matched one or more existing Cases and waits for
/// staff to choose a destination: nothing is registered, held or made
/// Unidentified while it waits.
/// </param>
public sealed record ImageIntakeAutomationOutcome(
    IntakeReceipt Receipt,
    bool GroupPending = false,
    RegisterUnidentifiedRequest? UnidentifiedGroup = null,
    bool AssociatedCase = false,
    bool AwaitingStaffDecision = false);

/// <summary>
/// The post-persistence intake hook for image-only material: scan every
/// retained image, record every outcome, and — at the provisional bar —
/// automatically file mailed images on the one unambiguous eligible Case, hold
/// a manual upload that matched Cases for a staff destination, or register the
/// Image intake (operator, 11 October 2026). Never blocks or fails intake:
/// every failure leaves the receipt exactly as the pipeline decided it.
/// </summary>
public interface IImageIntakeAutomation
{
    Task<ImageIntakeAutomationOutcome> ApplyAsync(IntakeReceipt receipt, CancellationToken cancellationToken);
}

public sealed class ImageIntakeAutomation(
    IVrmRecognitionEngine engine,
    IVrmSuggestionStore suggestionStore,
    IIntakeArtifactStore artifactStore,
    IImageIntakeOriginResolver originResolver,
    IImageIntakeStore imageIntakeStore,
    IRegisterImageIntake registerImageIntake,
    IImageIntakeCaseCandidates caseCandidates,
    IIntakeReceiptQueries receiptQueries,
    IImageIntakeCasePairing casePairing,
    TimeProvider timeProvider,
    IAutomaticCaseAssociationStore associationStore,
    IIntakeSubmissionGroupStore? groupStore = null,
    ICaseStaffNotifier? caseNotifier = null,
    RecordedVehicleCaptureReading? captureReadings = null) : IImageIntakeAutomation
{
    public const string ActorId = "image-intake-automation";
    public const string MatchPolicyKey = "image_registration_match";
    public const int MatchPolicyVersion = 1;
    private const string SystemActor = "system-worker:" + ActorId;

    private static readonly ActivitySource Telemetry = new("Pegasus.Core.ImageIntake");

    public async Task<ImageIntakeAutomationOutcome> ApplyAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (!ImageIntakeLifecycleRules.IsImageAutomationEligible(receipt)
            || receipt.Decision is not (IntakeDecision.NeedsSorting or IntakeDecision.ImageIntakeRegistered))
        {
            return new(receipt);
        }

        using var activity = Telemetry.StartActivity("image_intake_automation");
        activity?.SetTag("intake.receipt_id", receipt.Id);

        var existing = await imageIntakeStore.GetByOriginReceiptAsync(receipt.Id, cancellationToken);
        if (existing is not null)
        {
            activity?.SetTag("image_intake.outcome", "already_registered");
            try
            {
                // A policy re-evaluation recomputes the decision without
                // knowledge of the permanent registration; re-assert it.
                await imageIntakeStore.EnsureRegisteredReceiptDecisionAsync(
                    receipt.Id,
                    cancellationToken);
                RecordPairingOutcome(activity,
                    await casePairing.PairRegisteredReceiptAsync(receipt.Id, cancellationToken, existing));
                return new(await receiptQueries.GetAsync(receipt.Id, cancellationToken) ?? receipt);
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                activity?.SetTag("image_intake.failure_type", exception.GetType().Name);
                activity?.SetStatus(ActivityStatusCode.Error, "registered_replay_failed");
                return new(receipt);
            }
        }

        if (groupStore is not null)
        {
            var groupOutcome = await TryApplyGroupAsync(receipt, activity, cancellationToken);
            if (groupOutcome is not null)
            {
                return groupOutcome;
            }
        }

        if (await PrintedRegistrationAsync(receipt, activity, cancellationToken) is { } printed)
        {
            // A capture PDF prints the vehicle's registration; its photographs
            // pair on that, never on plate reads (FRD-02 EXT-17).
            activity?.SetTag("image_intake.registration_source", "document");
            return await TryRegisterAndAssociateAsync(
                receipt,
                printed,
                [],
                activity,
                cancellationToken,
                isPrinted: true) ?? new(receipt);
        }

        var suggestions = await ScanAsync(receipt, cancellationToken);
        if (suggestions.Any(suggestion => suggestion.Outcome is
                VrmRecognitionOutcomeKind.TechnicalFailure
                or VrmRecognitionOutcomeKind.Unavailable))
        {
            // A single PDF can carry several photographs. A readable plate
            // on one does not make a failed recognition on another harmless:
            // the whole evidence set remains technically incomplete and is
            // handed to staff with that explicit reason.
            activity?.SetTag("image_intake.outcome", "recognition_failure");
            return new(receipt, UnidentifiedGroup:
                BuildTechnicalFailureRegistrationRequest(receipt));
        }
        var confidentRegistrations = VrmRecognitionProvisionalBar.DistinctAcceptedRegistrations(suggestions);
        activity?.SetTag("image_intake.distinct_confident_registrations", confidentRegistrations.Length);
        if (confidentRegistrations.Length > VrmRecognitionProvisionalBar.RequiredDistinctRegistrations)
        {
            // A standalone PDF is still a set of photographs. Conflicting
            // confident reads must be visible as an identification conflict,
            // not collapsed to the generic no-readable-identification route.
            activity?.SetTag("image_intake.outcome", "conflicting_vrms");
            return new(receipt, UnidentifiedGroup:
                BuildConflictingRegistrationRequest(receipt));
        }
        if (confidentRegistrations.Length
            != VrmRecognitionProvisionalBar.RequiredDistinctRegistrations)
        {
            activity?.SetTag("image_intake.outcome", "below_bar");
            return new(receipt);
        }

        var registration = confidentRegistrations[0];
        var updated = await TryRegisterAndAssociateAsync(
            receipt,
            registration,
            suggestions,
            activity,
            cancellationToken);
        return updated ?? new(receipt);
    }

    private static RegisterUnidentifiedRequest BuildTechnicalFailureRegistrationRequest(
        IntakeReceipt receipt) => new(
            UnidentifiedOrigin.Receipt(receipt.Id),
            UnidentifiedReasonCode.TechnicalProcessingFailure,
            "image_recognition_failure",
            ActionActor.SystemWorker(ActorId),
            $"unidentified:image-receipt:{receipt.Id:N}:recognition-failure",
            receipt.ReceivedAtUtc)
        {
            SourceAssetId = IntakeFileIdentity.SourceAsset(receipt)?.Id
        };

    private static RegisterUnidentifiedRequest BuildConflictingRegistrationRequest(
        IntakeReceipt receipt) => new(
            UnidentifiedOrigin.Receipt(receipt.Id),
            UnidentifiedReasonCode.ConflictingIdentification,
            "conflicting_vrms",
            ActionActor.SystemWorker(ActorId),
            $"unidentified:image-receipt:{receipt.Id:N}:conflicting-vrms",
            receipt.ReceivedAtUtc)
        {
            SourceAssetId = IntakeFileIdentity.SourceAsset(receipt)?.Id
        };

    private async Task<ImageIntakeAutomationOutcome?> TryApplyGroupAsync(
        IntakeReceipt receipt,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        var group = await groupStore!.FindForMemberSourceAsync(
            receipt.SourceIdentity,
            cancellationToken);
        if (group?.Discard is not null)
        {
            // The durable manual discard terminalizes the submission without
            // deleting custody. It also shuts this route before a completed
            // queue replay can register or associate its retained images.
            activity?.SetTag("image_intake.group_outcome", "discarded");
            return new(receipt);
        }
        if (group is null || !group.HasSiblingMembers)
        {
            // Every manual upload is a submission group, but this
            // decision table scopes itself to a submission with more than
            // one member: a one-member group is a lone image and stays on
            // the single-image path below, whose exact-match candidate rule
            // (operator-directed 2026-08-03) differs from the group's
            // fail-closed eligible-case count.
            return null;
        }

        var members = group.Members;
        if (members.Count < group.ExpectedMemberCount)
        {
            // Fewer member rows exist than the originating submission
            // declared: later files in the same batch are still being
            // staged. This is distinct from the per-row check below, which
            // can only see rows that already exist. Retriable, not terminal:
            // the caller must defer rather than fall back to Unidentified.
            activity?.SetTag("image_intake.group_outcome", "waiting_for_members");
            return new(receipt, GroupPending: true);
        }

        var receipts = new List<IntakeReceipt>(members.Count);
        foreach (var member in members.OrderBy(member => member.Ordinal))
        {
            var memberReceipt = await receiptQueries.FindBySourceIdentityAsync(
                new(
                    group.Channel,
                    GroupedIntakeMemberToken.Create(group.SubmissionToken, member.Ordinal)),
                cancellationToken);
            if (memberReceipt is null)
            {
                activity?.SetTag("image_intake.group_outcome", "waiting_for_members");
                return new(receipt, GroupPending: true);
            }

            receipts.Add(memberReceipt);
        }

        // A batch can mix vehicle images with known report/Triage/associated
        // material; only automation-eligible members run recognition or are
        // stamped by this registration. A member already registered by this
        // group remains in the evidence set for replay stability even after a
        // later association, so its identity cannot be split into a second
        // Image intake.
        var imageReceipts = receipts
            .Where(memberReceipt =>
                ImageIntakeLifecycleRules.IsImageAutomationEligible(memberReceipt)
                || memberReceipt.Decision == IntakeDecision.ImageIntakeRegistered)
            .ToArray();

        var recognitions = new List<ImageIntakeGroupMemberRecognition>(imageReceipts.Length);
        var scans = new List<(IntakeReceipt Receipt, IReadOnlyList<ImageVrmSuggestion> Suggestions)>();
        foreach (var memberReceipt in imageReceipts)
        {
            var suggestions = await ScanAsync(memberReceipt, cancellationToken);
            scans.Add((memberReceipt, suggestions));
            // Each selected photograph has an independent recognition result.
            // Do not collapse a receipt to its best read: two photographs in
            // one PDF can disagree, and that disagreement must route the
            // whole submission to Unidentified rather than hide one result.
            foreach (var suggestion in suggestions)
            {
                recognitions.Add(new(
                    memberReceipt.Id,
                    suggestion.Outcome is VrmRecognitionOutcomeKind.Suggested
                        or VrmRecognitionOutcomeKind.NoReadableResult
                        or VrmRecognitionOutcomeKind.TechnicalFailure
                        or VrmRecognitionOutcomeKind.Unavailable,
                    suggestion.Outcome,
                    suggestion.SuggestedRegistration,
                    suggestion.Confidence,
                    suggestion.FailureCode,
                    CouldNotBeRead: IntakeDecisionPolicy.CouldNotBeRead(memberReceipt.Decision)));
            }
            if (suggestions.Count == 0)
            {
                // A selected asset with no durable result is retriable, not a
                // no-readable-registration vote.
                recognitions.Add(new(
                    memberReceipt.Id,
                    IsTerminal: false,
                    VrmRecognitionOutcomeKind.NoReadableResult,
                    null,
                    null,
                    CouldNotBeRead: IntakeDecisionPolicy.CouldNotBeRead(memberReceipt.Decision)));
            }
        }

        var registrationCandidates = VrmRecognitionProvisionalBar.DistinctAcceptedRegistrations(recognitions);
        IReadOnlyList<ImageIntakeCaseCandidate> eligibleCases = registrationCandidates.Length == 1
            ? await caseCandidates.FindEligibleByRegistrationAsync(
                registrationCandidates[0],
                cancellationToken)
            : [];
        var routing = ImageIntakeGroupRoutingPolicy.Evaluate(
            recognitions,
            imageReceipts.Length,
            eligibleCases.Count);
        activity?.SetTag("image_intake.group_id", group.Id);
        activity?.SetTag("image_intake.group_outcome", routing.Decision.ToString());
        activity?.SetTag("image_intake.group_reason", routing.ReasonCode);
        if (routing.Decision is ImageIntakeGroupRoutingDecision.WaitingForMembers
            or ImageIntakeGroupRoutingDecision.WaitingForRecognition)
        {
            // Recognition has not finished for every member yet. Retriable,
            // not terminal.
            return new(receipt, GroupPending: true);
        }

        if (routing.Decision is ImageIntakeGroupRoutingDecision.RouteToUnidentified
            or ImageIntakeGroupRoutingDecision.TechnicalFailure
            || routing.NormalizedRegistration is null)
        {
            // No accepted VRM is available for ImageIntake registration. This
            // is a legitimate, resolved group outcome (not retriable): keep
            // the intact group available for Unidentified, including the
            // explicit conflicting_vrms outcome (Unidentified scope, unchanged).
            return new(receipt, UnidentifiedGroup:
                ImageIntakeGroupRoutingPolicy.BuildUnidentifiedRegistrationRequest(group, routing.ReasonCode));
        }

        if (group.Channel == IntakeSourceChannel.ManualUpload
            && eligibleCases.Count >= 1
            && MatchWaitIsOpen(group))
        {
            // A usable registration that matches existing Cases registers
            // nothing while staff decide
            // (operator, 11 October 2026); once staff have linked a member the
            // decision is theirs to finish, so it neither waits nor registers.
            if (receipts.Any(HasStaffAssociation))
            {
                activity?.SetTag("image_intake.group_outcome", "staff_started");
                return new(receipt);
            }

            activity?.SetTag("image_intake.group_outcome", "awaiting_staff_decision");
            return new(receipt, AwaitingStaffDecision: true);
        }

        // The group is the registration unit: exactly one
        // ImageIntake exists per submission group, registered once under a
        // group-scoped operation key, with every image-only member's receipt
        // moved to the registered decision against that one reference. A
        // group whose registration attempt loses a transient concurrency
        // race is not silently dropped — it is reported back as pending so
        // the caller defers it via the durable-work retry convention instead
        // of ever letting a member fall through to the instruction-fallback/
        // Unidentified path while the group could still resolve.
        var registered = await TryRegisterGroupAsync(
            group,
            imageReceipts[0],
            scans,
            routing,
            eligibleCases,
            activity,
            cancellationToken);

        var finalReceipt = await receiptQueries.GetAsync(receipt.Id, cancellationToken) ?? receipt;
        var pending = !registered && finalReceipt.Decision == IntakeDecision.NeedsSorting;
        return new(finalReceipt, pending);

    }

    /// <summary>
    /// Registers the whole submission group as one ImageIntake. The group's
    /// primary member — its lowest-ordinal image-only member — carries the
    /// registration origin, and every racing sibling computes the same
    /// primary from the same durable membership, so the group-scoped
    /// operation key (<c>image-intake-register:group:{id}</c>) replays
    /// instead of duplicating no matter which member's work item runs it.
    /// The store moves every image-only member receipt to the registered
    /// decision in the registration transaction; the re-assert loop below
    /// covers the replay path (a straggler re-driven after the group already
    /// registered), whose probe returns the record without touching
    /// receipts.
    /// </summary>
    /// <remarks>
    /// A group decision other than
    /// <see cref="ImageIntakeGroupRoutingDecision.AssociateExistingCase"/> —
    /// most importantly <see cref="ImageIntakeGroupRoutingDecision.HandOffToImageIntake"/>,
    /// chosen because the eligible-case count was zero or ambiguous — must
    /// register the ImageIntake evidence but must never associate a Case:
    /// the group's own fail-closed decision is the sole authority for
    /// whether association happens.
    /// </remarks>
    private async Task<bool> TryRegisterGroupAsync(
        IntakeSubmissionGroup group,
        IntakeReceipt primary,
        IReadOnlyList<(IntakeReceipt Receipt, IReadOnlyList<ImageVrmSuggestion> Suggestions)> scans,
        ImageIntakeGroupRoutingResult routing,
        IReadOnlyList<ImageIntakeCaseCandidate> eligibleCases,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        var actor = ActionActor.SystemWorker(ActorId);
        var read = routing.NormalizedRegistration!;
        // Manual submissions always retain staff confirmation over the case
        // link, even where one photograph read a unique existing case.
        var associationAllowed = group.Channel != IntakeSourceChannel.ManualUpload
            && routing.Decision == ImageIntakeGroupRoutingDecision.AssociateExistingCase;
        try
        {
            var origin = await originResolver.ResolveOriginAsync(primary.Id, cancellationToken);
            if (origin is null)
            {
                activity?.SetTag("image_intake.outcome", "origin_unresolved");
                return false;
            }

            // The group's routing decision already fixed WHETHER
            // association happens; the selection below only fixes WHICH
            // value is registered.
            var target = SelectAssociationTarget(
                associationAllowed ? eligibleCases : [],
                read,
                principalId: null);
            var registration = target?.ConfirmedRegistration ?? read;
            var reason = target is not null
                && !string.Equals(registration, read, StringComparison.Ordinal)
                ? $"Automatic registration: the group's confident read {read} matches case {target.CaseReference}'s confirmed registration with one character missing; registered with the confirmed value."
                : "Automatic registration of the whole submission group from a confident vehicle-registration read on its retained image evidence.";
            var record = await registerImageIntake.ExecuteAsync(
                new(
                    origin,
                    registration,
                    actor,
                    $"image-intake-register:group:{group.Id:N}",
                    reason,
                    SubmissionGroupId: group.Id),
                cancellationToken);
            activity?.SetTag("image_intake.reference", record.ImageIntakeReference);
            if (!associationAllowed)
            {
                activity?.SetTag("image_intake.association", "handed_off_to_image_intake");
            }

            foreach (var (memberReceipt, suggestions) in scans)
            {
                await ConfirmUsedSuggestionsAsync(
                    suggestions,
                    record.NormalizedVehicleRegistration,
                    actor,
                    cancellationToken);
                if (memberReceipt.Decision == IntakeDecision.NeedsSorting)
                {
                    await imageIntakeStore.EnsureRegisteredReceiptDecisionAsync(
                        memberReceipt.Id,
                        cancellationToken);
                }
            }

            if (target is not null)
            {
                RecordPairingOutcome(activity,
                    await casePairing.PairRegisteredReceiptAsync(primary.Id, cancellationToken));
            }

            return true;
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            activity?.SetTag("image_intake.outcome", "registration_failed");
            activity?.SetTag("image_intake.failure_type", exception.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, "registration_failed");
            return false;
        }
    }

    /// <summary>
    /// The one Case a confident read may register and associate against: an
    /// exact confirmed registration wins; otherwise a lone candidate
    /// completes a truncated read with its confirmed value. A printed
    /// registration is complete, so it matches exactly or not at all.
    /// Ambiguity of any kind resolves to none.
    /// </summary>
    private static ImageIntakeCaseCandidate? SelectAssociationTarget(
        IReadOnlyList<ImageIntakeCaseCandidate> unscoped,
        string read,
        Guid? principalId,
        bool isPrinted = false)
    {
        // Same Principal scope as the registered-target policy. No caller
        // knows a Principal at registration; the parameter keeps one rule.
        var candidates = ImageIntakeCasePairing.ScopeToPrincipal(unscoped, principalId);
        var exactMatches = candidates
            .Where(candidate => string.Equals(
                candidate.ConfirmedRegistration,
                read,
                StringComparison.Ordinal))
            .ToArray();
        if (exactMatches.Length == 1)
        {
            return exactMatches[0];
        }

        return !isPrinted && exactMatches.Length == 0 && candidates.Count == 1 ? candidates[0] : null;
    }

    /// <summary>
    /// The registration a capture PDF on this receipt prints, as intake
    /// recorded it, or null. A lookup fault falls back to plate reads.
    /// </summary>
    private async Task<string?> PrintedRegistrationAsync(
        IntakeReceipt receipt,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        if (captureReadings is null)
        {
            return null;
        }

        try
        {
            return (await captureReadings.FindAsync(receipt, cancellationToken))?.Registration;
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            activity?.SetTag("image_intake.capture_reading_failure", exception.GetType().Name);
            return null;
        }
    }

    private async Task<IReadOnlyList<ImageVrmSuggestion>> ScanAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        // Recognition is re-triggered by every group event (a sibling member
        // arriving, a replay); without this, every trigger re-ran the ONNX
        // engine on every already-scanned member. A recorded suggestion for
        // the exact asset/content is a durable terminal outcome (recording
        // is idempotent by operation key, so there is at most one), so it is
        // reused rather than recomputed.
        var recordedByAsset = (await suggestionStore.ListForReceiptAsync(receipt.Id, cancellationToken))
            .ToDictionary(suggestion => suggestion.IntakeAssetId);
        var selectedAssets = InstructionEvidenceImages.Select(receipt.AssetRecords);
        var results = new List<ImageVrmSuggestion>(selectedAssets.Count);
        foreach (var asset in selectedAssets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (recordedByAsset.TryGetValue(asset.Id, out var recorded)
                && string.Equals(recorded.StorageKey, asset.StorageKey, StringComparison.Ordinal)
                && string.Equals(recorded.ContentHash, asset.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(recorded);
                continue;
            }

            var result = await RecognizeAsync(asset, cancellationToken);
            var best = VrmRecognitionProvisionalBar.OfferedCandidate(result.Candidates
                .Where(candidate => IsUsableRegistration(candidate.NormalizedRegistration))
                .ToArray());
            var draft = new ImageVrmSuggestionDraft(
                receipt.Id,
                asset.Id,
                asset.StorageKey,
                asset.ContentHash,
                result.EngineKey,
                result.EngineVersion,
                result.ModelHashes,
                best is null && result.Kind == VrmRecognitionOutcomeKind.Suggested
                    ? VrmRecognitionOutcomeKind.NoReadableResult
                    : result.Kind,
                best?.NormalizedRegistration,
                best?.Confidence,
                result.FailureCode,
                result.FailureReason,
                $"vrm-scan:{receipt.Id:N}:{asset.Id:N}");
            // Recognition is only a completed decision once its asset-scoped
            // outcome is durable. Omitting a failed write would let a later
            // photograph register the receipt from a partial evidence set;
            // propagate so durable intake retries the whole idempotent scan.
            results.Add(await suggestionStore.RecordAsync(draft, cancellationToken));
        }

        return results;
    }

    private async Task<VrmRecognitionResult> RecognizeAsync(
        IntakeAssetRecord asset,
        CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte>? bytes;
        try
        {
            bytes = await artifactStore.ReadAsync(asset.StorageKey, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return Failure("artifact_read_failure", "The retained image could not be read.");
        }

        if (bytes is null)
        {
            return Failure("artifact_missing", "The retained image is no longer available.");
        }

        var contentHash = Convert.ToHexString(SHA256.HashData(bytes.Value.Span));
        if (!string.Equals(contentHash, asset.ContentHash, StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                "artifact_integrity_failure",
                "The retained image bytes no longer match their recorded hash.");
        }

        try
        {
            return await engine.RecognizeAsync(bytes.Value, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            return Failure("recognition_failure", "Vehicle-registration recognition failed for this image.");
        }

        static VrmRecognitionResult Failure(string code, string reason) => new(
            VrmRecognitionOutcomeKind.TechnicalFailure,
            [],
            "unavailable",
            "0",
            string.Empty,
            code,
            reason);
    }

    private static bool IsUsableRegistration(string registration) =>
        !string.IsNullOrWhiteSpace(registration)
        && registration.Length <= 20
        && registration.All(character =>
            char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character));

    /// <summary>
    /// Whether a manual upload's matched Cases are still waiting for staff:
    /// not declined, and younger than the grouped-image hold window.
    /// </summary>
    private bool MatchWaitIsOpen(IntakeSubmissionGroup group) =>
        group.ImageMatchDeclined is null
        && timeProvider.GetUtcNow() - group.ReceivedAtUtc < ReconcileGroupedImageIntake.EscapeAfter;

    private static bool HasStaffAssociation(IntakeReceipt receipt) =>
        receipt.ManualAssociationActorKind == ActorKind.Staff;

    /// <summary>
    /// The legacy single-receipt registration path — a receipt that is not a
    /// member of any submission group. Group members never come through
    /// here: their whole group registers once via
    /// <see cref="TryRegisterGroupAsync"/>. A mailbox image whose confident
    /// read has one unambiguous Case is filed on that Case without an Image
    /// intake; a manual upload that matches Cases waits for staff.
    /// </summary>
    private async Task<ImageIntakeAutomationOutcome?> TryRegisterAndAssociateAsync(
        IntakeReceipt receipt,
        string read,
        IReadOnlyList<ImageVrmSuggestion> suggestions,
        Activity? activity,
        CancellationToken cancellationToken,
        bool isPrinted = false)
    {
        var actor = ActionActor.SystemWorker(ActorId);
        // Candidate selection before registration: the case's
        // instruction-supplied registration is the registered identity,
        // never the incomplete read (operator-directed 2026-08-03).
        var candidates = receipt.CurrentCaseId is null
            ? await caseCandidates.FindEligibleByRegistrationAsync(read, cancellationToken)
            : [];
        activity?.SetTag("image_intake.case_candidates", candidates.Count);
        var isManual = receipt.SourceIdentity.Channel == IntakeSourceChannel.ManualUpload;
        var target = isManual
            ? null
            : SelectAssociationTarget(candidates, read, principalId: null, isPrinted);
        if (target is not null)
        {
            // The images join the matched Case; no Image intake or Box folder
            // is made for them (operator, 11 October 2026).
            var reason = isPrinted
                ? $"Automatic association: the registration {read} printed on the capture document matches case {target.CaseReference}'s confirmed registration."
                : string.Equals(target.ConfirmedRegistration, read, StringComparison.Ordinal)
                ? $"Automatic association: the confident read {read} matches case {target.CaseReference}'s confirmed registration."
                : $"Automatic association: the confident read {read} matches case {target.CaseReference}'s confirmed registration with one character missing.";
            var association = await associationStore.AssociateFromMatchAsync(
                new(
                    receipt.Id,
                    target.CaseId,
                    MatchPolicyKey,
                    MatchPolicyVersion,
                    SystemActor,
                    $"image-case-association:{receipt.Id:N}",
                    reason),
                timeProvider.GetUtcNow(),
                cancellationToken);
            if (association == AutomaticCaseAssociationOutcome.Associated && caseNotifier is not null)
            {
                await caseNotifier.NotifyMailArrivalAsync(target.CaseId, receipt.Id, null, cancellationToken);
            }

            await ConfirmUsedSuggestionsAsync(suggestions, target.ConfirmedRegistration, actor, cancellationToken);
            activity?.SetTag("image_intake.association", "filed_on_case");
            return new(
                await receiptQueries.GetAsync(receipt.Id, cancellationToken) ?? receipt,
                AssociatedCase: true);
        }

        if (isManual && candidates.Count >= 1 && groupStore is not null
            && await groupStore.FindForMemberSourceAsync(receipt.SourceIdentity, cancellationToken)
                is { } group
            && MatchWaitIsOpen(group)
            && !HasStaffAssociation(receipt))
        {
            activity?.SetTag("image_intake.association", "awaiting_staff_decision");
            return new(receipt, AwaitingStaffDecision: true);
        }

        var origin = await originResolver.ResolveOriginAsync(receipt.Id, cancellationToken);
        if (origin is null)
        {
            activity?.SetTag("image_intake.outcome", "origin_unresolved");
            return null;
        }

        var record = await registerImageIntake.ExecuteAsync(
            new(
                origin,
                read,
                actor,
                $"image-intake-register:{receipt.Id:N}",
                isPrinted
                    ? "Automatic registration from the registration printed on the capture document."
                    : "Automatic registration from a confident vehicle-registration read on the retained image evidence."),
            cancellationToken);
        activity?.SetTag("image_intake.reference", record.ImageIntakeReference);
        await ConfirmUsedSuggestionsAsync(
            suggestions,
            record.NormalizedVehicleRegistration,
            actor,
            cancellationToken);
        if (receipt.CurrentCaseId is not null)
        {
            activity?.SetTag("image_intake.association", "already_associated");
        }
        else
        {
            activity?.SetTag(
                "image_intake.association",
                candidates.Count == 0 ? "no_candidate" : "ambiguous");
        }

        return new(await receiptQueries.GetAsync(receipt.Id, cancellationToken) ?? receipt);
    }

    private async Task ConfirmUsedSuggestionsAsync(
        IReadOnlyList<ImageVrmSuggestion> suggestions,
        string registration,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        foreach (var suggestion in suggestions)
        {
            if (suggestion.Disposition != ImageVrmSuggestionDisposition.Pending
                || suggestion.Outcome != VrmRecognitionOutcomeKind.Suggested
                || suggestion.SuggestedRegistration is null
                || !VrmRegistrationMatching.IsMatch(suggestion.SuggestedRegistration, registration)
                || suggestion.Confidence < VrmRecognitionProvisionalBar.MinimumAutomaticConfidence)
            {
                continue;
            }

            try
            {
                await suggestionStore.SetDispositionAsync(
                    new(
                        suggestion.Id,
                        ImageVrmSuggestionDisposition.Confirmed,
                        actor,
                        "The automatic registration used this confident read.",
                        $"vrm-confirm:{suggestion.Id:N}"),
                    cancellationToken);
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                // Disposition is bookkeeping over an already-recorded
                // suggestion; a failure never blocks the registration.
            }
        }
    }

    private static void RecordPairingOutcome(Activity? activity, ImageIntakePairingResult result)
    {
        activity?.SetTag("image_intake.pairing_merged", result.Merged);
        activity?.SetTag("image_intake.pairing_failures", result.Failures);
        if (result.Failures > 0)
        {
            activity?.SetTag("image_intake.failure_type", result.FirstFailure);
            activity?.SetStatus(ActivityStatusCode.Error, "association_failed");
        }
    }
}
