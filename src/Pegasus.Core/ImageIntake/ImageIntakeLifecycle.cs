using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.ImageIntake;

public sealed class RegisterImageIntake(IImageIntakeStore store) : IRegisterImageIntake
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

        return await _store.RegisterAsync(request, cancellationToken);
    }
}

public static class ImageIntakeLifecycleRules
{
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
    /// Image-only material: at least one retained asset, every retained asset
    /// an image, and evaluation produced no instruction evidence. Anything
    /// else is instruction-bearing and never registers an Image intake. This
    /// is the one owner of the rule; the automation, the intake surface, and
    /// the registration write path all consume it.
    /// </summary>
    public static bool IsImageOnlyMaterial(IntakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return IsImageOnlyMaterial(
            receipt.InstructionDraft is not null,
            receipt.Fields.Count,
            receipt.AssetRecords.Select(asset => asset.MediaType));
    }

    public static bool IsImageOnlyMaterial(
        bool hasInstructionDraft,
        int extractedFieldCount,
        IEnumerable<string> retainedAssetMediaTypes)
    {
        ArgumentNullException.ThrowIfNull(retainedAssetMediaTypes);
        var sawAsset = false;
        foreach (var mediaType in retainedAssetMediaTypes)
        {
            if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            sawAsset = true;
        }

        return !hasInstructionDraft && extractedFieldCount == 0 && sawAsset;
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
