using System.Diagnostics;
using System.Security.Cryptography;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

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
/// unchanged INTK-007 scope. What the caller does with a pending outcome is
/// owned by <c>ProcessQueuedIntake.ApplyImageIntakeAutomationAsync</c>.
/// </param>
public sealed record ImageIntakeAutomationOutcome(IntakeReceipt Receipt, bool GroupPending = false);

/// <summary>
/// The post-persistence intake hook for image-only material: scan every
/// retained image, record every outcome, and — at the provisional bar —
/// automatically register the Image intake and associate the one unambiguous
/// eligible Case. Never blocks or fails intake: every failure leaves the
/// receipt exactly as the pipeline decided it.
/// </summary>
public interface IImageIntakeAutomation
{
    Task<ImageIntakeAutomationOutcome> ApplyAsync(IntakeReceipt receipt, CancellationToken cancellationToken);
}

public sealed class NoImageIntakeAutomation : IImageIntakeAutomation
{
    public Task<ImageIntakeAutomationOutcome> ApplyAsync(IntakeReceipt receipt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return Task.FromResult(new ImageIntakeAutomationOutcome(receipt));
    }
}

public sealed class ImageIntakeAutomation(
    IVrmRecognitionEngine engine,
    IVrmSuggestionStore suggestionStore,
    IIntakeArtifactStore artifactStore,
    IImageIntakeOriginResolver originResolver,
    IImageIntakeStore imageIntakeStore,
    IRegisterImageIntake registerImageIntake,
    IImageIntakeCaseCandidates caseCandidates,
    IIntakeMutationStore intakeMutationStore,
    IIntakeReceiptQueries receiptQueries,
    IImageIntakeCasePairing casePairing,
    TimeProvider timeProvider,
    IIntakeSubmissionGroupStore? groupStore = null) : IImageIntakeAutomation
{
    public const string ActorId = "image-intake-automation";

    private static readonly ActivitySource Telemetry = new("Pegasus.Core.ImageIntake");

    public async Task<ImageIntakeAutomationOutcome> ApplyAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (!IsImageOnly(receipt))
        {
            return new(receipt);
        }

        using var activity = Telemetry.StartActivity("image_intake_automation");
        activity?.SetTag("intake.receipt_id", receipt.Id);

        if (groupStore is not null)
        {
            var groupOutcome = await TryApplyGroupAsync(receipt, activity, cancellationToken);
            if (groupOutcome is not null)
            {
                return groupOutcome;
            }
        }

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
                return new(await receiptQueries.GetAsync(receipt.Id, cancellationToken) ?? receipt);
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                return new(receipt);
            }
        }

        var suggestions = await ScanAsync(receipt, cancellationToken);
        var confidentRegistrations = suggestions
            .Where(suggestion => suggestion.Outcome == VrmRecognitionOutcomeKind.Suggested
                && suggestion.SuggestedRegistration is not null
                && suggestion.Confidence
                    >= VrmRecognitionProvisionalBar.MinimumAutomaticConfidence)
            .Select(suggestion => suggestion.SuggestedRegistration!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        activity?.SetTag("image_intake.distinct_confident_registrations", confidentRegistrations.Length);
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
            groupDecision: null,
            cancellationToken);
        return new(updated ?? receipt);
    }

    private async Task<ImageIntakeAutomationOutcome?> TryApplyGroupAsync(
        IntakeReceipt receipt,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        var group = await groupStore!.FindForMemberSourceAsync(
            receipt.SourceIdentity,
            cancellationToken);
        if (group is null)
        {
            return null;
        }

        var members = await groupStore.ListMembersAsync(group.Id, cancellationToken);
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

        // A batch can mix vehicle images with other material (e.g. an
        // instruction document uploaded in the same request); only the
        // image-only members are vehicle evidence, so only they run
        // recognition and count toward the group's routing decision. The
        // triggering receipt is always image-only (checked in ApplyAsync
        // before this method is called), so this is never empty.
        var imageReceipts = receipts.Where(IsImageOnly).ToArray();

        var recognitions = new List<ImageIntakeGroupMemberRecognition>(imageReceipts.Length);
        var scans = new List<(IntakeReceipt Receipt, IReadOnlyList<ImageVrmSuggestion> Suggestions)>();
        foreach (var memberReceipt in imageReceipts)
        {
            var suggestions = await ScanAsync(memberReceipt, cancellationToken);
            scans.Add((memberReceipt, suggestions));
            var best = suggestions
                .Where(suggestion => suggestion.Outcome == VrmRecognitionOutcomeKind.Suggested
                    && suggestion.SuggestedRegistration is not null)
                .OrderByDescending(suggestion => suggestion.Confidence)
                .FirstOrDefault();
            recognitions.Add(new(
                memberReceipt.Id,
                IsTerminal(suggestions),
                best?.Outcome ?? (suggestions.Count > 0
                    ? suggestions[0].Outcome
                    : VrmRecognitionOutcomeKind.NoReadableResult),
                best?.SuggestedRegistration,
                best?.Confidence,
                suggestions.FirstOrDefault(suggestion => suggestion.FailureCode is not null)?.FailureCode));
        }

        var registrationCandidates = recognitions
            .Where(recognition => recognition.Outcome == VrmRecognitionOutcomeKind.Suggested
                && recognition.NormalizedRegistration is not null
                && recognition.Confidence >= VrmRecognitionProvisionalBar.MinimumAutomaticConfidence)
            .Select(recognition => recognition.NormalizedRegistration!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var eligibleCaseCount = registrationCandidates.Length == 1
            ? (await caseCandidates.FindEligibleByRegistrationAsync(
                registrationCandidates[0],
                cancellationToken)).Count
            : 0;
        var routing = ImageIntakeGroupRoutingPolicy.Evaluate(
            recognitions,
            imageReceipts.Length,
            eligibleCaseCount);
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
            // explicit conflicting_vrms outcome (INTK-007 scope, unchanged).
            return new(receipt);
        }

        // Every member that resolves this pass registers; a member whose
        // registration attempt loses a transient concurrency race (e.g. two
        // siblings' work items racing the same VRM's sequence row) is not
        // silently dropped — it is reported back as pending so the caller
        // defers it via the durable-work retry convention instead of ever
        // letting it fall through to the instruction-fallback/Unidentified
        // path while the group could still resolve.
        var allRegistered = true;
        foreach (var (memberReceipt, suggestions) in scans)
        {
            var updated = await TryRegisterAndAssociateAsync(
                memberReceipt,
                routing.NormalizedRegistration,
                suggestions,
                activity,
                routing.Decision,
                cancellationToken);
            if (updated is null)
            {
                allRegistered = false;
            }
        }

        var finalReceipt = await receiptQueries.GetAsync(receipt.Id, cancellationToken) ?? receipt;
        var pending = !allRegistered && finalReceipt.Decision == IntakeDecision.NeedsSorting;
        return new(finalReceipt, pending);

        static bool IsTerminal(IReadOnlyList<ImageVrmSuggestion> suggestions) =>
            suggestions.Count > 0
            && suggestions.All(suggestion => suggestion.Outcome is
                VrmRecognitionOutcomeKind.Suggested
                or VrmRecognitionOutcomeKind.NoReadableResult
                or VrmRecognitionOutcomeKind.TechnicalFailure
                or VrmRecognitionOutcomeKind.Unavailable);
    }

    private static bool IsImageOnly(IntakeReceipt receipt) =>
        receipt.Decision == IntakeDecision.NeedsSorting
        && ImageIntakeLifecycleRules.IsImageOnlyMaterial(receipt);

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
        var results = new List<ImageVrmSuggestion>(receipt.AssetRecords.Count);
        foreach (var asset in receipt.AssetRecords)
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
            var best = result.Candidates
                .Where(candidate => IsUsableRegistration(candidate.NormalizedRegistration))
                .OrderByDescending(candidate => candidate.Confidence)
                .FirstOrDefault();
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
            try
            {
                results.Add(await suggestionStore.RecordAsync(draft, cancellationToken));
            }
            catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
            {
                // Recording is idempotent by operation key; a persistence
                // failure here must not block intake. The asset simply has no
                // recorded outcome this run.
            }
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

    /// <param name="groupDecision">
    /// The group's own routing decision, when this receipt is a member of a
    /// completed image group; <see langword="null"/> for the legacy
    /// single-receipt path that has no group to defer to. A group decision
    /// other than <see cref="ImageIntakeGroupRoutingDecision.AssociateExistingCase"/>
    /// — most importantly <see cref="ImageIntakeGroupRoutingDecision.HandOffToImageIntake"/>,
    /// chosen because the eligible-case count was zero or ambiguous — must
    /// register the ImageIntake evidence but must never associate a Case:
    /// the per-member candidate search below can find a single exact match
    /// among an otherwise-ambiguous candidate set, and that near-match must
    /// not overrule the group's own fail-closed decision.
    /// </param>
    private async Task<IntakeReceipt?> TryRegisterAndAssociateAsync(
        IntakeReceipt receipt,
        string read,
        IReadOnlyList<ImageVrmSuggestion> suggestions,
        Activity? activity,
        ImageIntakeGroupRoutingDecision? groupDecision,
        CancellationToken cancellationToken)
    {
        var actor = ActionActor.SystemWorker(ActorId);
        var associationAllowed = groupDecision is null
            or ImageIntakeGroupRoutingDecision.AssociateExistingCase;
        try
        {
            var origin = await originResolver.ResolveOriginAsync(receipt.Id, cancellationToken);
            if (origin is null)
            {
                activity?.SetTag("image_intake.outcome", "origin_unresolved");
                return null;
            }

            // Candidate selection before registration: an exact confirmed
            // registration wins; otherwise a single one-missing-character
            // candidate completes the truncated read with its confirmed
            // value (operator-directed 2026-08-03) — the case's
            // instruction-supplied registration is the registered identity,
            // never the incomplete read. Ambiguity of any kind means no
            // automatic association. This search only runs when the group
            // (or the legacy single-receipt path) has not already decided
            // the group hands off without association.
            var candidates = associationAllowed && receipt.CurrentCaseId is null
                ? await caseCandidates.FindEligibleByRegistrationAsync(read, cancellationToken)
                : [];
            activity?.SetTag("image_intake.case_candidates", candidates.Count);
            var exactMatches = candidates
                .Where(candidate => string.Equals(
                    candidate.ConfirmedRegistration,
                    read,
                    StringComparison.Ordinal))
                .ToArray();
            var target = exactMatches.Length == 1
                ? exactMatches[0]
                : exactMatches.Length == 0 && candidates.Count == 1
                    ? candidates[0]
                    : null;
            var registration = target?.ConfirmedRegistration ?? read;
            var reason = target is not null
                && !string.Equals(registration, read, StringComparison.Ordinal)
                ? $"Automatic registration: the confident read {read} matches case {target.CaseReference}'s confirmed registration with one character missing; registered with the confirmed value."
                : "Automatic registration from a confident vehicle-registration read on the retained image evidence.";
            var record = await registerImageIntake.ExecuteAsync(
                new(
                    origin,
                    registration,
                    actor,
                    $"image-intake-register:{receipt.Id:N}",
                    reason),
                cancellationToken);
            activity?.SetTag("image_intake.reference", record.ImageIntakeReference);
            await ConfirmUsedSuggestionsAsync(
                suggestions,
                record.NormalizedVehicleRegistration,
                actor,
                cancellationToken);
            if (!associationAllowed)
            {
                activity?.SetTag("image_intake.association", "handed_off_to_image_intake");
            }
            else if (target is not null)
            {
                await TryAssociateAsync(receipt, target, actor, activity, cancellationToken);
            }
            else if (receipt.CurrentCaseId is not null)
            {
                activity?.SetTag("image_intake.association", "already_associated");
            }
            else
            {
                activity?.SetTag(
                    "image_intake.association",
                    candidates.Count == 0 ? "no_candidate" : "ambiguous");
            }

            return await receiptQueries.GetAsync(receipt.Id, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            activity?.SetTag("image_intake.outcome", "registration_failed");
            return null;
        }
    }

    private async Task ConfirmUsedSuggestionsAsync(
        IReadOnlyList<ImageVrmSuggestion> suggestions,
        string registration,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        foreach (var suggestion in suggestions)
        {
            if (suggestion.Outcome != VrmRecognitionOutcomeKind.Suggested
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

    /// <summary>
    /// The automatic association runs at most once per receipt (its operation
    /// key is receipt-scoped) against the single unambiguous eligible
    /// candidate selected before registration. Later changes are reasoned
    /// staff decisions and are never re-run automatically.
    /// </summary>
    private async Task TryAssociateAsync(
        IntakeReceipt receipt,
        ImageIntakeCaseCandidate candidate,
        ActionActor actor,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        try
        {
            await intakeMutationStore.AutoLinkAsync(
                new(
                    receipt.Id,
                    candidate.CaseId,
                    candidate.CaseVersion,
                    actor,
                    $"image-intake-associate:{receipt.Id:N}",
                    $"Automatic association: the confirmed registration matches case {candidate.CaseReference} unambiguously."),
                timeProvider.GetUtcNow(),
                cancellationToken);
            await casePairing.SyncMergeAfterLinkAsync(receipt.Id, candidate.CaseId, actor, cancellationToken);
            activity?.SetTag("image_intake.association", "associated");
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            activity?.SetTag("image_intake.association", "failed");
        }
    }
}
