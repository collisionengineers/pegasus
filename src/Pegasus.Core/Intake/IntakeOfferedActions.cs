using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Triage;

namespace Pegasus.Core.Intake;

/// <summary>
/// Which of Create case, Open the Triage and Register images a receipt offers
/// right now, read from the ports that own each fact. The Unidentified record
/// and the Inbox message page both ask here, so an action is offered, or not,
/// for the same receipt in the same way on both. The registration readings are
/// read with the image offer, so the dialog can suggest the registration.
/// </summary>
public sealed record IntakeOfferedActions(
    bool CanCreateCase,
    bool CanOpenTriage,
    bool CanRegisterImages,
    IReadOnlyList<ImageVrmSuggestion> RegistrationReadings)
{
    public static readonly IntakeOfferedActions None = new(false, false, false, []);

    /// <summary>The registration the readings suggest, when exactly one pending suggestion agrees.</summary>
    public string? SuggestedRegistration => RegistrationReadings
        .Where(reading => reading.Outcome == VrmRecognitionOutcomeKind.Suggested
            && reading.Disposition == ImageVrmSuggestionDisposition.Pending
            && !string.IsNullOrWhiteSpace(reading.SuggestedRegistration))
        .Select(reading => reading.SuggestedRegistration)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(2)
        .ToArray() is [var single] ? single : null;
}

public interface IGetIntakeOfferedActions
{
    Task<IntakeOfferedActions> ExecuteAsync(ActionActor actor, IntakeReceipt receipt, CancellationToken cancellationToken);
}

public sealed class GetIntakeOfferedActions(
    IImageIntakeQueries imageIntakes,
    ITriageQueries triages,
    ITriagePrincipalGate triagePrincipalGate,
    IVrmSuggestionStore vrmSuggestions) : IGetIntakeOfferedActions
{
    private readonly IImageIntakeQueries _imageIntakes = imageIntakes ?? throw new ArgumentNullException(nameof(imageIntakes));
    private readonly ITriageQueries _triages = triages ?? throw new ArgumentNullException(nameof(triages));
    private readonly ITriagePrincipalGate _triagePrincipalGate =
        triagePrincipalGate ?? throw new ArgumentNullException(nameof(triagePrincipalGate));
    private readonly IVrmSuggestionStore _vrmSuggestions = vrmSuggestions ?? throw new ArgumentNullException(nameof(vrmSuggestions));

    public async Task<IntakeOfferedActions> ExecuteAsync(
        ActionActor actor,
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(receipt);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);

        // Create case: material awaiting sorting that no Case has taken.
        var canCreateCase = receipt.AcceptedCaseId is null
            && receipt.AllocationState is null
            && (IntakeDecisionPolicy.CanBecomeCase(receipt.Decision) || receipt.Decision == IntakeDecision.OcrRequired);

        // Register images: the staff route for image-only material (the
        // automatic route, or a message classified as images received).
        var isImageEligible = ImageIntakeLifecycleRules.IsImageRegistrationEligible(receipt);
        var readings = isImageEligible
            ? await _vrmSuggestions.ListForReceiptAsync(receipt.Id, cancellationToken)
            : [];
        var canRegisterImages = isImageEligible
            && receipt.Decision == IntakeDecision.NeedsSorting
            && await _imageIntakes.GetByOriginReceiptAsync(receipt.Id, cancellationToken) is null;

        // Open the Triage opens a Triage Case, which takes the receipt's
        // Principal: without an established one it is not offered.
        var canOpenTriage = receipt.Decision == IntakeDecision.NeedsSorting
            && receipt.Evidence.Count(evidence => evidence.Finding == IntakeEvidenceFinding.AcceptedTriageMatch) == 1
            && await _triages.GetByOriginReceiptAsync(receipt.Id, cancellationToken) is null
            && await _triagePrincipalGate.GetEstablishedPrincipalIdAsync(receipt.Id, cancellationToken) is not null;

        return new(canCreateCase, canOpenTriage, canRegisterImages, readings);
    }
}
