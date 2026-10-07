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
/// A submission-group item has no receipt of its own: its group, and the member
/// receipt a group registration originates from, stand in for it.
/// </summary>
public sealed record UnidentifiedItemContext(
    UnidentifiedItem Item,
    IntakeReceipt? Receipt,
    IReadOnlyList<ImageVrmSuggestion> RegistrationReadings,
    ImageIntakeDetail? ImageIntake,
    TriageSummary? Triage,
    bool CanRegisterImages,
    bool CanOpenTriage,
    IntakeSubmissionGroup? SubmissionGroup = null,
    IntakeReceipt? GroupRegistrationReceipt = null)
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
    IImageIntakeQueries imageIntakes,
    ITriageQueries triages,
    IGetIntakeOfferedActions offers,
    IIntakeSubmissionGroupStore submissionGroups) : IGetUnidentifiedItemContext
{
    private readonly IUnidentifiedStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IIntakeReceiptQueries _receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
    private readonly IImageIntakeQueries _imageIntakes = imageIntakes ?? throw new ArgumentNullException(nameof(imageIntakes));
    private readonly ITriageQueries _triages = triages ?? throw new ArgumentNullException(nameof(triages));
    private readonly IGetIntakeOfferedActions _offers = offers ?? throw new ArgumentNullException(nameof(offers));
    private readonly IIntakeSubmissionGroupStore _submissionGroups =
        submissionGroups ?? throw new ArgumentNullException(nameof(submissionGroups));

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

        if (item.Origin.Kind == UnidentifiedOriginKind.SubmissionGroup)
        {
            return await GroupContextAsync(actor, item, cancellationToken);
        }

        var receipt = await _receipts.GetAsync(item.Origin.Id, cancellationToken);
        if (receipt is null)
        {
            return new(item, null, [], null, null, CanRegisterImages: false, CanOpenTriage: false);
        }

        var imageIntake = await _imageIntakes.GetByOriginReceiptAsync(receipt.Id, cancellationToken);
        var triage = await _triages.GetByOriginReceiptAsync(receipt.Id, cancellationToken);
        // The receipt's offers are one owner's answer; an open item adds only
        // that it is still open.
        var offered = await _offers.ExecuteAsync(actor, receipt, cancellationToken);
        var open = item.State == UnidentifiedState.Open;
        return new(
            item,
            receipt,
            offered.RegistrationReadings,
            imageIntake,
            triage,
            CanRegisterImages: open && offered.CanRegisterImages,
            CanOpenTriage: open && offered.CanOpenTriage);
    }

    // The group registers as one Image intake (FRD-19), originating from its
    // lowest-ordinal member the receipt offer would let register. It is offered
    // only once every member has been processed and the upload is not
    // discarded, while no member has reached a Case or an Image intake: the
    // group goes to one destination, never split.
    private async Task<UnidentifiedItemContext> GroupContextAsync(
        ActionActor actor,
        UnidentifiedItem item,
        CancellationToken cancellationToken)
    {
        var group = await _submissionGroups.GetAsync(item.Origin.Id, cancellationToken);
        if (group is null)
        {
            return new(item, null, [], null, null, CanRegisterImages: false, CanOpenTriage: false);
        }

        var complete = group.Discard is null
            && group.Members.Count > 0
            && group.Members.Count == group.ExpectedMemberCount;
        var readings = new List<ImageVrmSuggestion>();
        ImageIntakeDetail? imageIntake = null;
        IntakeReceipt? registrationReceipt = null;
        var undecided = true;
        foreach (var member in group.Members.OrderBy(member => member.Ordinal))
        {
            if (member.ProcessedReceiptId is not { } receiptId
                || await _receipts.GetAsync(receiptId, cancellationToken) is not { } receipt)
            {
                complete = false;
                continue;
            }

            if (receipt.CurrentCaseId is not null || receipt.Decision == IntakeDecision.ImageIntakeRegistered)
            {
                undecided = false;
            }

            imageIntake ??= await _imageIntakes.GetByOriginReceiptAsync(receipt.Id, cancellationToken);
            var offered = await _offers.ExecuteAsync(actor, receipt, cancellationToken);
            readings.AddRange(offered.RegistrationReadings);
            if (offered.CanRegisterImages)
            {
                registrationReceipt ??= receipt;
            }
        }

        var canRegister = item.State == UnidentifiedState.Open
            && complete
            && undecided
            && imageIntake is null
            && registrationReceipt is not null;
        return new(
            item,
            null,
            readings,
            imageIntake,
            null,
            CanRegisterImages: canRegister,
            CanOpenTriage: false,
            group,
            canRegister ? registrationReceipt : null);
    }
}
