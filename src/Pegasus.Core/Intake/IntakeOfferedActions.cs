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

/// <summary>One member's Register images answer: the offer and the readings it carries.</summary>
public sealed record MemberImageRegistrationOffer(
    bool CanRegisterImages,
    IReadOnlyList<ImageVrmSuggestion> RegistrationReadings);

/// <summary>
/// What an upload group's Register images needs from its member receipts, read
/// once for the group: an entry per receipt in the order given, every
/// receipt's readings (the group page shows the non-eligible members' too), and
/// the first Image Intake record a member reaches.
/// </summary>
public sealed record GroupImageRegistrationOffers(
    IReadOnlyList<MemberImageRegistrationOffer> Members,
    IReadOnlyDictionary<Guid, IReadOnlyList<ImageVrmSuggestion>> ReadingsByReceipt,
    ImageIntakeDetail? ImageIntake)
{
    public static readonly GroupImageRegistrationOffers None = new(
        [],
        new Dictionary<Guid, IReadOnlyList<ImageVrmSuggestion>>(),
        null);
}

public sealed class GetIntakeOfferedActions(
    IImageIntakeQueries imageIntakes,
    ITriageQueries triages,
    ITriagePrincipalGate triagePrincipalGate,
    IVrmSuggestionStore vrmSuggestions)
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

        var readings = ImageIntakeLifecycleRules.IsImageRegistrationEligible(receipt)
            ? await _vrmSuggestions.ListForReceiptAsync(receipt.Id, cancellationToken)
            : [];
        var canRegisterImages = IsRegistrationOfferable(receipt)
            && await _imageIntakes.GetByOriginReceiptAsync(receipt.Id, cancellationToken) is null;

        // Open the Triage opens a Triage Case, which takes the receipt's
        // Principal: without an established one it is not offered.
        var canOpenTriage = receipt.Decision == IntakeDecision.NeedsSorting
            && receipt.Evidence.Count(evidence => evidence.Finding == IntakeEvidenceFinding.AcceptedTriageMatch) == 1
            && await _triages.GetByOriginReceiptAsync(receipt.Id, cancellationToken) is null
            && await _triagePrincipalGate.GetEstablishedPrincipalIdAsync(receipt.Id, cancellationToken) is not null;

        return new(canCreateCase, canOpenTriage, canRegisterImages, readings);
    }

    /// <summary>
    /// The Register images part of <see cref="ExecuteAsync"/> for an upload
    /// group's member receipts: each entry's offer and readings equal what
    /// <see cref="ExecuteAsync"/> answers for that receipt alone. The readings of
    /// every receipt are read in one read, each receipt's Image Intake origin
    /// record at most once (shared between its offer and the group's first
    /// record), and Create case and Open the Triage are not computed.
    /// </summary>
    public async Task<GroupImageRegistrationOffers> ExecuteForGroupAsync(
        ActionActor actor,
        IReadOnlyList<IntakeReceipt> receipts,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(receipts);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (receipts.Count == 0)
        {
            return GroupImageRegistrationOffers.None;
        }

        var found = await _vrmSuggestions.ListForReceiptsAsync(
            receipts.Select(receipt => receipt.Id).Distinct().ToArray(),
            cancellationToken);
        var readingsByReceipt = new Dictionary<Guid, IReadOnlyList<ImageVrmSuggestion>>();
        // The offers' origin reads and the group's search for its first record
        // are one set read: which members reach a record, and the first one's
        // detail.
        var origins = await _imageIntakes.GetByOriginReceiptsAsync(
            receipts.Select(receipt => receipt.Id).ToArray(),
            cancellationToken);
        var members = new List<MemberImageRegistrationOffer>(receipts.Count);
        foreach (var receipt in receipts)
        {
            IReadOnlyList<ImageVrmSuggestion> readings = found.TryGetValue(receipt.Id, out var read) ? read : [];
            readingsByReceipt[receipt.Id] = readings;
            members.Add(new(
                IsRegistrationOfferable(receipt) && !origins.ReceiptIds.Contains(receipt.Id),
                ImageIntakeLifecycleRules.IsImageRegistrationEligible(receipt) ? readings : []));
        }

        return new(members, readingsByReceipt, origins.First);
    }

    // Register images: the staff route for unlinked image-only material with
    // no mail category (FRD-19), still awaiting sorting.
    private static bool IsRegistrationOfferable(IntakeReceipt receipt) =>
        ImageIntakeLifecycleRules.IsImageRegistrationEligible(receipt)
        && receipt.Decision == IntakeDecision.NeedsSorting;
}
