using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Core.Custody;

namespace Pegasus.Core.ImageIntake;

public sealed class RegisterImageIntake(
    IImageIntakeStore store,
    ICommittedExternalWorkPublisher committedExternalWorkPublisher) : IRegisterImageIntake
{
    private readonly IImageIntakeStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<ImageIntakeRecord> ExecuteAsync(
        RegisterImageIntakeRequest request,
        CancellationToken cancellationToken)
    {
        ImageIntakeLifecycleRules.ValidateRegister(request);
        var replay = await _store.ProbeRegisterReplayAsync(request, cancellationToken);
        if (replay is not null)
        {
            return replay.Result;
        }

        var registered = await _store.RegisterAsync(request, cancellationToken);
        if (registered.PendingExternalWorkId is { } workItemId)
        {
            await committedExternalWorkPublisher.PublishAsync(workItemId, cancellationToken);
        }

        return registered;
    }
}

public static class ImageIntakeLifecycleRules
{
    /// <summary>
    /// Recording, replacing or clearing the known principal is casework, not a
    /// lifecycle transition, so it takes no operation key and no reason. A
    /// null principal is the `Not known` state and is accepted; only an empty
    /// identifier is rejected.
    /// </summary>
    public static void ValidateSetPrincipal(SetImageIntakePrincipalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor, nameof(request));
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        RequireId(request.ImageIntakeId, nameof(request.ImageIntakeId));
        if (request.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException("A principal identifier cannot be empty.", nameof(request));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(request.ExpectedVersion);
    }

    /// <summary>
    /// Merge is reached from the automatic pairing paths as well as a staff
    /// link, so it accepts the system worker on the same terms as automatic
    /// registration.
    /// </summary>
    public static void ValidateMerge(MergeImageInitiatedCaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor, nameof(request));
        RequireRegistrationActor(request.Actor);
        RequireId(request.ImageIntakeId, nameof(request.ImageIntakeId));
        RequireId(request.CaseId, nameof(request.CaseId));
        ValidateOperation(request.OperationKey);
        RequireText(request.Reason, "A reason is required.", 500, nameof(request.Reason));
        ArgumentOutOfRangeException.ThrowIfNegative(request.ExpectedVersion);
    }

    /// <summary>
    /// Staff closure is always a reasoned casework decision — never automatic,
    /// so unlike merge it admits no system-worker actor.
    /// </summary>
    public static void ValidateClose(CloseImageInitiatedCaseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor, nameof(request));
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        RequireId(request.ImageIntakeId, nameof(request.ImageIntakeId));
        ValidateOperation(request.OperationKey);
        RequireText(request.Reason, "A reason is required.", 500, nameof(request.Reason));
        ArgumentOutOfRangeException.ThrowIfNegative(request.ExpectedVersion);
    }

    /// <summary>
    /// `Awaiting instruction` is the one state a transition may leave;
    /// `Merged into instruction case` and `Staff-closed` are permanent
    /// outcomes. Core owns which states are terminal — the store enforces it
    /// by calling this before it mutates the row.
    /// </summary>
    public static void RequireTransitionable(ImageInitiatedCaseState current)
    {
        if (current != ImageInitiatedCaseState.AwaitingInstruction)
        {
            throw new InvalidOperationException("A terminal Image-initiated Case cannot be changed.");
        }
    }

    private static void RequireId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An identifier is required.", parameterName);
        }
    }
    /// <summary>
    /// A Case is eligible for Image-intake association only before report
    /// delivery: an editable pre-report workflow state and no retained
    /// report-sent evidence. Terminal and post-report states are never
    /// eligible. This governs making an association (staff or automatic);
    /// reasoned reversal of an existing association remains available.
    /// </summary>
    public static bool IsCaseEligibleForAssociation(
        CaseLifecycleState state,
        bool hasReportSentEvidence) =>
        !hasReportSentEvidence
        && state is CaseLifecycleState.NotReady
            or CaseLifecycleState.Held
            or CaseLifecycleState.Review
            or CaseLifecycleState.ReportPreparation;

    /// <summary>
    /// Image evidence material: at least one selected photograph and no
    /// extracted instruction evidence. The retained source can be an image,
    /// a PDF containing photographs, or an email carrying photographs; it is
    /// preserved alongside the selected images and does not itself disqualify
    /// the route. This is the one owner of the rule; the automation, intake
    /// surface and registration write path all consume it.
    /// </summary>
    public static bool IsImageOnlyMaterial(IntakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return receipt.InstructionDraft is null
            && receipt.Fields.Count == 0
            && InstructionEvidenceImages.HasSelectedPhotographs(receipt);
    }

    /// <summary>
    /// The narrower automatic route for image evidence. A photograph does not
    /// supersede a known instruction/report/triage or an established Case
    /// association, and only direct image, PDF and email sources are admitted.
    /// This keeps an incidental photograph in arbitrary office material from
    /// changing its normal intake destination.
    /// </summary>
    public static bool IsImageAutomationEligible(IntakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (!IsImageOnlyMaterial(receipt)
            || receipt.CurrentCaseId is not null
            || ProcessIntake.IsTriageRequest(receipt)
            || receipt.Evidence.Any(item =>
                item is
                {
                    Source: IntakeEvidenceSource.SystemDefault,
                    Finding: IntakeEvidenceFinding.Information,
                    Signal: IntakeEvidenceSignals.RecognizedNonImageDocument
                        or IntakeEvidenceSignals.AmbiguousInstructionSelection
                        or IntakeEvidenceSignals.ConflictingInstructionSelection
                })
            || !IsSupportedImageEvidenceSource(receipt.MediaType))
        {
            return false;
        }

        return receipt.MailClassificationDecision is not { } classification
            || MailOperationalDestinationPolicy.Map(classification).Destination
                == MailOperationalDestination.Unidentified;
    }

    /// <summary>
    /// The media-type prefix that makes retained material an image. Query
    /// query layers use this prefix only as a coarse filter before applying
    /// <see cref="InstructionEvidenceImages"/> in memory.
    /// </summary>
    public const string ImageMediaTypePrefix = "image/";

    private static bool IsSupportedImageEvidenceSource(string mediaType) =>
        InstructionEvidenceImages.IsImage(mediaType)
        || mediaType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
        || mediaType.Equals(EmailSourceFormat.MediaType, StringComparison.OrdinalIgnoreCase)
        || mediaType.Equals("application/vnd.ms-outlook", StringComparison.OrdinalIgnoreCase);

    public static bool IsImageOnlyMaterial(
        bool hasInstructionDraft,
        int extractedFieldCount,
        IEnumerable<IntakeAssetRecord> retainedAssets)
    {
        ArgumentNullException.ThrowIfNull(retainedAssets);
        return !hasInstructionDraft
            && extractedFieldCount == 0
            && InstructionEvidenceImages.Select(retainedAssets).Count != 0;
    }

    public static void ValidateRegister(RegisterImageIntakeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Origin, nameof(request));
        ValidateOrigin(request.Origin);
        ValidateNormalizedRegistration(request.NormalizedVehicleRegistration);
        ArgumentNullException.ThrowIfNull(request.Actor, nameof(request));
        RequireRegistrationActor(request.Actor);
        ValidateOperation(request.OperationKey);
        RequireText(request.Reason, "A reason is required.", 500, nameof(request));
    }

    /// <summary>
    /// Registration is a reasoned staff casework action or the intake
    /// pipeline's automatic registration under the system worker actor
    /// (operator-directed 2026-08-03; the provisional recognition bar governs
    /// when the pipeline may register).
    /// </summary>
    private static void RequireRegistrationActor(ActionActor actor)
    {
        if (actor.Kind == ActorKind.SystemWorker)
        {
            return;
        }

        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
    }

    /// <summary>
    /// The one owner for turning staff-typed registration input into the
    /// normalized form <see cref="ValidateNormalizedRegistration"/> accepts:
    /// uppercase ASCII letters and digits, separators removed.
    /// </summary>
    public static string NormalizeRegistrationInput(string? raw) =>
        new((raw ?? string.Empty)
            .ToUpperInvariant()
            .Where(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character))
            .ToArray());

    internal static void ValidateNormalizedRegistration(string registration)
    {
        RequireText(registration, "A normalized vehicle registration is required.", 20, nameof(registration));
        if (registration.Any(character => !(char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character))))
        {
            throw new ArgumentException(
                "The vehicle registration must be uppercase ASCII letters and digits with no separators.",
                nameof(registration));
        }
    }

    private static void ValidateOrigin(ImageIntakeOrigin origin)
    {
        if (origin.ReceiptId == Guid.Empty)
        {
            throw new ArgumentException("An originating intake receipt is required.", nameof(origin));
        }

        ArgumentNullException.ThrowIfNull(origin.SourceIdentity, nameof(origin));
        if (!Enum.IsDefined(origin.SourceIdentity.Channel))
        {
            throw new ArgumentOutOfRangeException(nameof(origin), "The intake source channel is invalid.");
        }

        RequireText(
            origin.SourceIdentity.ExternalReceiptToken,
            "The source receipt token is required.",
            200,
            nameof(origin));
        RequireText(origin.SourceHash, "The source hash is required.", 64, nameof(origin));
        if (origin.EvaluationRevisionId == Guid.Empty)
        {
            throw new ArgumentException("A completed intake evaluation revision is required.", nameof(origin));
        }

        if (origin.SourceHash.Length != 64
            || origin.SourceHash.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException("The source hash must be a SHA-256 hexadecimal value.", nameof(origin));
        }
    }

    private static void ValidateOperation(string operationKey) =>
        RequireText(operationKey, "An operation key is required.", 100, nameof(operationKey));

    private static void RequireText(string value, string message, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, parameterName);
        }

        if (value.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The value cannot exceed {maximumLength} characters.");
        }
    }
}
