using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Cases;

public enum CaseDataValueKind
{
    Fact,
    Suggestion,
    Confirmed
}

public enum CaseDataSourceKind
{
    IntakeEvidence,
    MailRoute,
    CaseAcceptance,
    StaffCorrection,
    VehicleLookup,
    ProviderSetting,

    /// <summary>
    /// Stated by the instructing Principal over the Provider API. FRD-02 already
    /// names the provider API as a field provenance in its own right, distinct
    /// from extraction and from staff entry.
    /// </summary>
    ProviderApi
}

public enum CaseInspectionMode
{
    PhysicalAddress,
    ImageBasedAssessment
}

public sealed record CaseDataSource(
    CaseDataSourceKind Kind,
    string Identity,
    string Label,
    string PolicyKey,
    int PolicyVersion);

public sealed record CaseDataValue<T>(
    T Value,
    CaseDataValueKind Kind,
    CaseDataSource Source,
    string? ConfirmedByActor = null,
    DateTimeOffset? ConfirmedAtUtc = null)
    where T : notnull
{
    public bool IsAccepted => Kind is CaseDataValueKind.Fact or CaseDataValueKind.Confirmed;
}

public sealed record CaseField<T>(
    CaseDataValue<T>? Fact,
    CaseDataValue<T>? Suggestion,
    CaseDataValue<T>? Confirmed)
    where T : notnull
{
    public CaseDataValue<T>? Current => Confirmed ?? Fact ?? Suggestion;
}

public sealed record CaseOriginIdentity(
    Guid IntakeReceiptId,
    IntakeSourceChannel Channel,
    string ExternalReceiptToken,
    string SourceHash,
    DateTimeOffset ReceivedAtUtc,
    string SourceReaderKey,
    string SourceReaderVersion,
    string? ExtractionPolicyKey,
    int? ExtractionPolicyVersion);

public sealed record CaseProviderData(CaseField<string> WorkProviderCode);

/// <summary>
/// The claimant. <see cref="ContactNumber"/> and <see cref="Address"/> are the
/// claimant's own — distinct from <see cref="CaseContactData"/>, which is the
/// file handler Pegasus corresponds with about the case. EVA keeps the same
/// separation (ClmTelNo against the inspection-location contact), and the
/// claimant address is what its claimant block needs.
/// </summary>
public sealed record CaseClaimantData(
    CaseField<string> Name,
    CaseField<string> ContactNumber,
    CaseField<string> Address);

public sealed record CaseClaimData(CaseField<string> Number);

public sealed record CaseVehicleData(
    CaseField<string> Registration,
    CaseField<string> Make,
    CaseField<string> Model,
    CaseField<long> Mileage,
    CaseField<string> MileageUnit);

public sealed record CaseAccidentData(
    CaseField<DateOnly> IncidentDate,
    CaseField<string> Circumstances);

public sealed record CaseContactData(
    CaseField<string> Name,
    CaseField<string> EmailAddress,
    CaseField<string> PhoneNumber);

public sealed record CaseInstructionData(
    CaseField<DateOnly> InstructionDate,
    CaseField<string> VatStatus);

public sealed record CaseInspectionData(
    CaseField<DateOnly> InspectionDate,
    CaseField<DateOnly> Deadline,
    CaseField<string> Address,
    CaseField<CaseInspectionMode> Mode,
    CaseField<string>? StorageLocation = null,
    CaseField<string>? RepairerAddress = null);

public sealed record CaseCompletenessEvaluation(
    bool SatisfiesPolicy,
    string PolicyKey,
    int PolicyVersion);

public sealed record CaseCompletenessProjection(
    CaseCompleteness Values,
    CaseCompletenessEvaluation Evaluation);

public sealed record CaseDataProjection(
    CaseIdentity Identity,
    CaseOriginIdentity Origin,
    DateTimeOffset AcceptedAtUtc,
    long Version,
    CaseLifecycleState State,
    CaseCompletenessProjection Completeness,
    CaseProviderData Provider,
    CaseClaimantData Claimant,
    CaseClaimData Claim,
    CaseVehicleData Vehicle,
    CaseAccidentData Accident,
    CaseContactData Contact,
    CaseInstructionData Instruction,
    CaseInspectionData Inspection);

public sealed record CaseEditableData(
    string? ClaimantName = null,
    string? ClaimNumber = null,
    string? VehicleRegistration = null,
    string? VehicleMake = null,
    string? VehicleModel = null,
    long? VehicleMileage = null,
    string? VehicleMileageUnit = null,
    string? AccidentCircumstances = null,
    DateOnly? IncidentDate = null,
    string? ContactName = null,
    string? ContactEmailAddress = null,
    string? ContactPhoneNumber = null,
    DateOnly? InstructionDate = null,
    string? VatStatus = null,
    DateOnly? InspectionDate = null,
    DateOnly? InspectionDeadline = null,
    string? InspectionAddress = null,
    CaseInspectionMode? InspectionMode = null,
    // Appended, never inserted: this record is constructed positionally
    // (AssessmentMcpTools), so an inserted parameter would silently shift every
    // value after it.
    string? ClaimantContactNumber = null,
    string? ClaimantAddress = null,
    string? StorageLocation = null);

public sealed record ConfirmCompletenessRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    CaseCompleteness Completeness)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public sealed record SaveCaseRequest(
    Guid CaseId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason,
    string EditLeaseToken,
    CaseEditableData Data)
    : CaseMutationRequest(CaseId, ExpectedVersion, Actor, OperationKey, Reason, EditLeaseToken);

public interface ICaseDataQueries
{
    Task<CaseDataProjection?> GetAsync(Guid caseId, CancellationToken cancellationToken);
}

public interface ICaseDataStore : ICaseDataQueries
{
    Task<CaseDataProjection> ConfirmCompletenessAsync(
        ConfirmCompletenessRequest request,
        CaseCompletenessEvaluation evaluation,
        CancellationToken cancellationToken);

    Task<CaseDataProjection> SaveAsync(
        SaveCaseRequest request,
        CancellationToken cancellationToken);
}

public interface IConfirmCompleteness
{
    Task<CaseDataProjection> ExecuteAsync(
        ConfirmCompletenessRequest request,
        CancellationToken cancellationToken);
}

public interface ISaveCase
{
    Task<CaseDataProjection> ExecuteAsync(
        SaveCaseRequest request,
        CancellationToken cancellationToken);
}
