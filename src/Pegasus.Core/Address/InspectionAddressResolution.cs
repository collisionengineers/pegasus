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
    Corrected
}

public enum InspectionAddressStaffDecision
{
    AcceptSuggestion,
    CorrectSuggestion
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

public sealed record InspectionAddressResolutionRequest(
    Guid IntakeReceiptId,
    long ExpectedReceiptVersion,
    string SuggestionFingerprint,
    InspectionAddressStaffDecision Decision,
    string? CorrectedValue,
    ActionActor Actor,
    Guid OperationId,
    string CorrelationId);

public interface IInspectionAddressResolutionStore
{
    Task<InspectionAddressResolutionSnapshot?> GetAsync(
        Guid intakeReceiptId,
        CancellationToken cancellationToken);

    Task<InspectionAddressResolutionSnapshot> ResolveAsync(
        InspectionAddressResolutionRequest request,
        CancellationToken cancellationToken);
}

public sealed class InspectionAddressResolutionConcurrencyException()
    : InvalidOperationException("The intake evidence changed before the inspection address could be resolved.");
