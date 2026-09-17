using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Triage;

namespace Pegasus.Core.Intake.Unidentified;

/// <summary>
/// Everything the Unidentified record hosts now that the received-file page is
/// gone (Received file D2): the item, the material it concerns, the registration
/// readings on it, the Image-initiated Case or Triage it already opened, and
/// which of Register images and Open the Triage apply. Every part is read from
/// the port that already owns it; nothing here is a second copy of a rule.
/// </summary>
public sealed record UnidentifiedItemContext(
    UnidentifiedItem Item,
    IntakeReceipt? Receipt,
    IReadOnlyList<ImageVrmSuggestion> RegistrationReadings,
    ImageIntakeDetail? ImageIntake,
    TriageSummary? Triage,
    bool CanRegisterImages,
    bool CanOpenTriage)
{
    /// <summary>The Inbox message to open, when the material came by e-mail.</summary>
    public Guid? SourceMessageId => Item.SourceMessageId;

    /// <summary>The original file to open in the viewer.</summary>
    public Guid? SourceAssetId => Item.SourceAssetId ?? (Receipt is null ? null : IntakeFileIdentity.SourceAsset(Receipt)?.Id);

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

public interface IGetUnidentifiedItemContext
{
    Task<UnidentifiedItemContext?> ExecuteAsync(ActionActor actor, Guid unidentifiedItemId, CancellationToken cancellationToken);
}

public sealed class GetUnidentifiedItemContext(
    IUnidentifiedStore store,
    IIntakeReceiptQueries receipts,
    IVrmSuggestionStore vrmSuggestions,
    IImageIntakeQueries imageIntakes,
    ITriageQueries triages) : IGetUnidentifiedItemContext
{
    private readonly IUnidentifiedStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IIntakeReceiptQueries _receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
    private readonly IVrmSuggestionStore _vrmSuggestions = vrmSuggestions ?? throw new ArgumentNullException(nameof(vrmSuggestions));
    private readonly IImageIntakeQueries _imageIntakes = imageIntakes ?? throw new ArgumentNullException(nameof(imageIntakes));
    private readonly ITriageQueries _triages = triages ?? throw new ArgumentNullException(nameof(triages));

    public async Task<UnidentifiedItemContext?> ExecuteAsync(
        ActionActor actor,
        Guid unidentifiedItemId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
        if (unidentifiedItemId == Guid.Empty)
        {
            throw new ArgumentException("An Unidentified item identifier is required.", nameof(unidentifiedItemId));
        }

        var item = await _store.GetAsync(unidentifiedItemId, cancellationToken);
        if (item is null)
        {
            return null;
        }

        if (item.Origin.Kind != UnidentifiedOriginKind.Receipt)
        {
            return new(item, null, [], null, null, CanRegisterImages: false, CanOpenTriage: false);
        }

        var receipt = await _receipts.GetAsync(item.Origin.Id, cancellationToken);
        if (receipt is null)
        {
            return new(item, null, [], null, null, CanRegisterImages: false, CanOpenTriage: false);
        }

        var imageIntake = await _imageIntakes.GetByOriginReceiptAsync(receipt.Id, cancellationToken);
        var triage = await _triages.GetByOriginReceiptAsync(receipt.Id, cancellationToken);
        var isImageEligible = ImageIntakeLifecycleRules.IsImageAutomationEligible(receipt);
        var readings = isImageEligible
            ? await _vrmSuggestions.ListForReceiptAsync(receipt.Id, cancellationToken)
            : [];
        var open = item.State == UnidentifiedState.Open;
        return new(
            item,
            receipt,
            readings,
            imageIntake,
            triage,
            CanRegisterImages: open && isImageEligible && imageIntake is null && receipt.Decision == IntakeDecision.NeedsSorting,
            CanOpenTriage: open && triage is null
                && receipt.Decision == IntakeDecision.NeedsSorting
                && receipt.Evidence.Count(evidence => evidence.Finding == IntakeEvidenceFinding.AcceptedTriageMatch) == 1);
    }
}
