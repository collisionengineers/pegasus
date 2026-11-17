using Pegasus.Core.Address;
using Pegasus.Core.Cases;

namespace Pegasus.Core.Intake;

/// <summary>
/// The steps of making a Case from a received item that the staff Create case
/// page and the Automation Actor's <c>accept</c> share (ADR-0064, R1042): which
/// decisions a person may create a Case on, the item's inspection address
/// resolution as both read it, the draft address of an image-based Principal,
/// whether the item's images are complete, and the item's own standalone Audit
/// evidence. Where the two differ by ruling they
/// keep their own step: an unsettled inspection address refuses the page's
/// submit but not <c>accept</c>, whose Case starts Not ready (R965).
/// </summary>
public sealed class IntakeCaseCreation(
    IStandaloneAuditEvidenceQueries standaloneAuditEvidence,
    IInspectionAddressResolutionStore addressResolutions)
{
    private readonly IStandaloneAuditEvidenceQueries standaloneAuditEvidence =
        standaloneAuditEvidence ?? throw new ArgumentNullException(nameof(standaloneAuditEvidence));

    private readonly IInspectionAddressResolutionStore addressResolutions =
        addressResolutions ?? throw new ArgumentNullException(nameof(addressResolutions));

    /// <summary>
    /// Whether a person may create a Case on an item with this decision: one
    /// that can become a Case, and the hand-keyed and unsorted items whose
    /// draft correction normalises the decision.
    /// </summary>
    public static bool AcceptsByHand(IntakeDecision decision) =>
        decision is IntakeDecision.OcrRequired or IntakeDecision.NeedsSorting
        || IntakeDecisionPolicy.CanBecomeCase(decision);

    /// <summary>
    /// The item's inspection address resolution: the stored one, else an
    /// Unresolved one over the item's own evaluation, with nothing settled even
    /// when the evaluation holds a suggestion.
    /// </summary>
    public async Task<InspectionAddressResolutionSnapshot> AddressResolutionAsync(
        IntakeReceipt receipt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return await addressResolutions.GetAsync(receipt.Id, cancellationToken)
            ?? new(
                receipt.Id,
                receipt.Version,
                InspectionAddressResolutionState.Unresolved,
                Ext18InspectionAddressPolicy.Evaluate(receipt),
                null,
                null,
                null,
                null);
    }

    /// <summary>
    /// The draft's inspection address for a Principal whose own recorded mode
    /// is image-based: the settled address, else the one extraction suggested,
    /// else Image Based Assessment.
    /// </summary>
    public static string ImageBasedInspectionAddress(InspectionAddressResolutionSnapshot resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        return resolution.ResolvedValue
            ?? resolution.Evaluation.Suggestion?.Value
            ?? Ext18InspectionAddressPolicy.ImageBasedAssessment;
    }

    /// <summary>Whether the item holds at least one instruction evidence photograph.</summary>
    public static bool ImagesComplete(IntakeReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return InstructionEvidenceImages.Select(receipt.AssetRecords).Count > 0;
    }

    /// <summary>The standalone Audit evidence confirmed on this item itself, if any.</summary>
    public async Task<Guid?> StandaloneAuditEvidenceIdAsync(
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        var evidence = await standaloneAuditEvidence.GetForReceiptAsync(receiptId, cancellationToken);
        return evidence is { IntakeReceiptId: var evidenceReceiptId } && evidenceReceiptId == receiptId
            ? evidence.Id
            : null;
    }
}
