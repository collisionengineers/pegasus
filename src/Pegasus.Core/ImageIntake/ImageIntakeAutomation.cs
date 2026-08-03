using System.Diagnostics;
using System.Security.Cryptography;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.ImageIntake;

/// <summary>
/// The post-persistence intake hook for image-only material: scan every
/// retained image, record every outcome, and — at the provisional bar —
/// automatically register the Image intake and associate the one unambiguous
/// eligible Case. Never blocks or fails intake: every failure leaves the
/// receipt exactly as the pipeline decided it.
/// </summary>
public interface IImageIntakeAutomation
{
    Task<IntakeReceipt> ApplyAsync(IntakeReceipt receipt, CancellationToken cancellationToken);
}

public sealed class NoImageIntakeAutomation : IImageIntakeAutomation
{
    public Task<IntakeReceipt> ApplyAsync(IntakeReceipt receipt, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return Task.FromResult(receipt);
    }
}

public sealed class ImageIntakeAutomation(
    IVrmRecognitionEngine engine,
    IVrmSuggestionStore suggestionStore,
    IIntakeArtifactStore artifactStore,
    IImageIntakeOriginResolver originResolver,
    IImageIntakeQueries imageIntakeQueries,
    IRegisterImageIntake registerImageIntake,
    IImageIntakeCaseCandidates caseCandidates,
    IIntakeMutationStore intakeMutationStore,
    IIntakeReceiptQueries receiptQueries,
    TimeProvider timeProvider) : IImageIntakeAutomation
{
    public const string ActorId = "image-intake-automation";

    private static readonly ActivitySource Telemetry = new("Pegasus.Core.ImageIntake");

    public async Task<IntakeReceipt> ApplyAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (!IsImageOnly(receipt))
        {
            return receipt;
        }

        using var activity = Telemetry.StartActivity("image_intake_automation");
        activity?.SetTag("intake.receipt_id", receipt.Id);

        var existing = await imageIntakeQueries.GetByOriginReceiptAsync(receipt.Id, cancellationToken);
        if (existing is not null)
        {
            activity?.SetTag("image_intake.outcome", "already_registered");
            return receipt;
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
            return receipt;
        }

        var registration = confidentRegistrations[0];
        var updated = await TryRegisterAndAssociateAsync(
            receipt,
            registration,
            suggestions,
            activity,
            cancellationToken);
        return updated ?? receipt;
    }

    /// <summary>
    /// Image-only material: at least one retained asset, every retained asset
    /// is an image, and evaluation produced no instruction evidence. Anything
    /// else is instruction-bearing and never registers an Image intake.
    /// </summary>
    private static bool IsImageOnly(IntakeReceipt receipt) =>
        receipt.Decision == IntakeDecision.NeedsSorting
        && receipt.InstructionDraft is null
        && receipt.Fields.Count == 0
        && receipt.AssetRecords.Count > 0
        && receipt.AssetRecords.All(asset =>
            asset.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase));

    private async Task<IReadOnlyList<ImageVrmSuggestion>> ScanAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        var results = new List<ImageVrmSuggestion>(receipt.AssetRecords.Count);
        foreach (var asset in receipt.AssetRecords)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

    private async Task<IntakeReceipt?> TryRegisterAndAssociateAsync(
        IntakeReceipt receipt,
        string registration,
        IReadOnlyList<ImageVrmSuggestion> suggestions,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        var actor = ActionActor.SystemWorker(ActorId);
        try
        {
            var origin = await originResolver.ResolveOriginAsync(receipt.Id, cancellationToken);
            if (origin is null)
            {
                activity?.SetTag("image_intake.outcome", "origin_unresolved");
                return null;
            }

            var record = await registerImageIntake.ExecuteAsync(
                new(
                    origin,
                    registration,
                    actor,
                    $"image-intake-register:{receipt.Id:N}",
                    "Automatic registration from a confident vehicle-registration read on the retained image evidence."),
                cancellationToken);
            activity?.SetTag("image_intake.reference", record.ImageIntakeReference);
            await ConfirmUsedSuggestionsAsync(suggestions, registration, actor, cancellationToken);
            await TryAssociateAsync(receipt, record, actor, activity, cancellationToken);
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
                || !string.Equals(suggestion.SuggestedRegistration, registration, StringComparison.Ordinal)
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
    /// key is receipt-scoped): exactly one eligible pre-report Case with the
    /// confirmed registration and no prior association. Later changes are
    /// reasoned staff decisions and are never re-run automatically.
    /// </summary>
    private async Task TryAssociateAsync(
        IntakeReceipt receipt,
        ImageIntakeRecord record,
        ActionActor actor,
        Activity? activity,
        CancellationToken cancellationToken)
    {
        if (receipt.CurrentCaseId is not null)
        {
            activity?.SetTag("image_intake.association", "already_associated");
            return;
        }

        try
        {
            var candidates = await caseCandidates.FindEligibleByRegistrationAsync(
                record.NormalizedVehicleRegistration,
                cancellationToken);
            activity?.SetTag("image_intake.case_candidates", candidates.Count);
            if (candidates.Count != 1)
            {
                activity?.SetTag(
                    "image_intake.association",
                    candidates.Count == 0 ? "no_candidate" : "ambiguous");
                return;
            }

            var candidate = candidates[0];
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
            activity?.SetTag("image_intake.association", "associated");
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            activity?.SetTag("image_intake.association", "failed");
        }
    }
}
