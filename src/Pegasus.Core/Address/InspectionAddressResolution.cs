using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Address;

public enum InspectionAddressEvidenceKind
{
    PhysicalAddress,
    ImageBasedAssessment
}

public enum InspectionAddressResolutionState
{
    Unresolved,
    Suggested,
    Accepted,
    Corrected,

    /// <summary>
    /// A member of staff entered the inspection address because the source
    /// carried none. Appended last so the persisted ordinal of every existing
    /// state is unchanged.
    /// </summary>
    Supplied
}

public enum InspectionAddressStaffDecision
{
    AcceptSuggestion,
    CorrectSuggestion,

    /// <summary>
    /// Enter the physical location directly, where extraction found no
    /// address evidence at all. This is not inference: nothing is derived from
    /// the source, a person states the location and their identity is retained
    /// with it. EXT-18 prohibits inferring an address, not recording one a
    /// person supplied.
    /// </summary>
    SupplyAddress
}

public sealed record InspectionAddressProvenance(
    IntakeEvidenceSource Source,
    string SourceLabel,
    string PolicyKey,
    int PolicyVersion);

public sealed record InspectionAddressEvidence(
    string Value,
    InspectionAddressEvidenceKind Kind,
    InspectionAddressProvenance Provenance);

public sealed record InspectionAddressSuggestion(
    string Value,
    InspectionAddressEvidenceKind Kind,
    IReadOnlyList<InspectionAddressProvenance> Provenance,
    string Fingerprint);

public sealed record InspectionAddressEvaluation(
    InspectionAddressSuggestion? Suggestion,
    IReadOnlyList<InspectionAddressEvidence> ConflictingEvidence)
{
    public bool IsUnresolved => Suggestion is null;
}

public sealed record InspectionAddressResolutionSnapshot(
    Guid IntakeReceiptId,
    long ReceiptVersion,
    InspectionAddressResolutionState State,
    InspectionAddressEvaluation Evaluation,
    string? ResolvedValue,
    Guid? ResolvedByStaffId,
    DateTimeOffset? ResolvedAtUtc);

/// <param name="SuggestionFingerprint">
/// The fingerprint of the suggestion the staff member was looking at, proving
/// the evidence has not moved underneath them. Null or empty only for
/// <see cref="InspectionAddressStaffDecision.SupplyAddress"/>, where there is
/// no suggestion to fingerprint.
/// </param>
/// <param name="CorrectedValue">
/// The address the staff member typed: the correction for
/// <see cref="InspectionAddressStaffDecision.CorrectSuggestion"/> and the
/// supplied location for <see cref="InspectionAddressStaffDecision.SupplyAddress"/>.
/// One field rather than two, because it is the same thing — a value a person
/// keyed — and the decision already says which.
/// </param>
public sealed record InspectionAddressResolutionRequest(
    Guid IntakeReceiptId,
    long ExpectedReceiptVersion,
    string? SuggestionFingerprint,
    InspectionAddressStaffDecision Decision,
    string? CorrectedValue,
    ActionActor Actor,
    Guid OperationId,
    string CorrelationId);

/// <summary>
/// Which resolution states let a case be created.
/// </summary>
/// <remarks>
/// This test was written out twice — once in the staff screen and once in the
/// case-data snapshot factory — and each copy had to be found and changed
/// whenever a state was added. It is one rule about business evidence, so it
/// lives in Core and both callers ask it: the create screen decides whether to
/// ask for an address at all, and the snapshot factory decides whether a
/// resolved one belongs on the case.
/// </remarks>
public static class InspectionAddressResolutionPolicy
{
    /// <summary>
    /// Whether a person has settled the inspection address, by any of the
    /// three routes: accepting what was extracted, correcting it, or supplying
    /// it where nothing was extracted.
    /// </summary>
    public static bool IsStaffResolved(InspectionAddressResolutionState state) => state switch
    {
        InspectionAddressResolutionState.Accepted
            or InspectionAddressResolutionState.Corrected
            or InspectionAddressResolutionState.Supplied => true,
        InspectionAddressResolutionState.Unresolved
            or InspectionAddressResolutionState.Suggested => false,
        _ => throw new InvalidOperationException(
            $"Unknown inspection-address resolution state '{(int)state}'.")
    };

    /// <summary>
    /// Whether the inspection address permits case creation.
    /// </summary>
    /// <remarks>
    /// An Image Based Assessment provider records the mode itself as the
    /// address when the case is created, so there is nothing for a person to
    /// settle first; every other provider needs a settled physical location.
    /// </remarks>
    public static bool SatisfiesCaseCreation(
        InspectionAddressResolutionState state,
        bool providerIsImageBased) =>
        providerIsImageBased || IsStaffResolved(state);
}

public interface IInspectionAddressResolutionStore
{
    Task<InspectionAddressResolutionSnapshot?> GetAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken);

    Task<InspectionAddressResolutionSnapshot> ResolveAsync(
        InspectionAddressResolutionRequest request,
        CancellationToken cancellationToken);
}

public enum InspectionAddressChoiceKind
{
    ImageBasedAssessment,
    ClaimantAddress,
    RepairerLocation,
    StorageLocation,
    PreviousAddress,
    ManualEntry
}

public sealed record InspectionAddressChoicesData(
    string? ClaimantAddress,
    string? RepairerAddress,
    string? StorageLocation,
    IReadOnlyList<string> PreviousAddresses);

public sealed record InspectionAddressChoice(
    InspectionAddressChoiceKind Kind,
    string? Address)
{
    public bool IsAvailable => Kind is InspectionAddressChoiceKind.ImageBasedAssessment
        or InspectionAddressChoiceKind.ManualEntry
        || Address is not null;
}

public static class InspectionAddressChoices
{
    public static IReadOnlyList<InspectionAddressChoice> Resolve(
        InspectionAddressChoicesData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return
        [
            new(
                InspectionAddressChoiceKind.ImageBasedAssessment,
                Ext18InspectionAddressPolicy.ImageBasedAssessment),
            new(InspectionAddressChoiceKind.ClaimantAddress, data.ClaimantAddress),
            new(InspectionAddressChoiceKind.RepairerLocation, data.RepairerAddress),
            new(InspectionAddressChoiceKind.StorageLocation, data.StorageLocation),
            .. data.PreviousAddresses.Select(address =>
                new InspectionAddressChoice(InspectionAddressChoiceKind.PreviousAddress, address)),
            new(InspectionAddressChoiceKind.ManualEntry, null)
        ];
    }
}

public interface IInspectionAddressChoicesQueries
{
    Task<InspectionAddressChoicesData?> GetAsync(
        Guid caseId,
        CancellationToken cancellationToken);
}

public sealed class InspectionAddressResolutionConcurrencyException()
    : InvalidOperationException("The intake evidence changed before the inspection address could be resolved.");

public sealed record InspectionLocationChoice(
    Guid Id, string Label, InspectionAddressEvidenceKind Kind, string? Address,
    string? Postcode, string Role, InspectionLocationSourceKind SourceKind,
    Guid SourceRecordId, long SourceVersion);
public enum InspectionLocationSourceKind
{
    Claimant, Repairer, Storage, PriorPrincipalLocation, Directory, PrincipalDefault
}
public sealed record InspectionLocationChoicesQuery(
    ActionActor Actor, Guid CaseId, string Prefix);
public interface IInspectionLocationChoices
{
    Task<IReadOnlyList<InspectionLocationChoice>> SearchAsync(
        InspectionLocationChoicesQuery query, CancellationToken cancellationToken);
}
