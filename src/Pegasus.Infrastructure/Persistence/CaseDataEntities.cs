namespace Pegasus.Infrastructure.Persistence;

internal sealed class CaseDataSnapshotEntity
{
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public Guid OriginIntakeReceiptId { get; set; }
    public required string OriginSourceChannel { get; set; }
    public required string OriginExternalReceiptToken { get; set; }
    public required string OriginSourceHash { get; set; }
    public DateTimeOffset OriginReceivedAtUtc { get; set; }
    public required string SourceReaderKey { get; set; }
    public required string SourceReaderVersion { get; set; }
    public string? ExtractionPolicyKey { get; set; }
    public int? ExtractionPolicyVersion { get; set; }
    public required string CompletenessPolicyKey { get; set; }
    public int CompletenessPolicyVersion { get; set; }
    public bool CompletenessPolicySatisfied { get; set; }
    public DateTimeOffset AcceptedAtUtc { get; set; }
    public List<CaseDataFieldEntity> Fields { get; set; } = [];
}

internal sealed class CaseDataFieldEntity
{
    public Guid CaseId { get; set; }
    public CaseDataSnapshotEntity Snapshot { get; set; } = null!;
    public required string FieldName { get; set; }
    public required string ValueKind { get; set; }
    public required string ValueType { get; set; }
    public required string Value { get; set; }
    public required string SourceKind { get; set; }
    public required string SourceIdentity { get; set; }
    public required string SourceLabel { get; set; }
    public required string PolicyKey { get; set; }
    public int PolicyVersion { get; set; }
    public string? ConfirmedByActor { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
}

internal static class CaseDataFieldNames
{
    public const string WorkProviderCode = "work_provider_code";
    public const string ClaimantName = "claimant_name";
    public const string ClaimantContactNumber = "claimant_contact_number";
    public const string ClaimantAddress = "claimant_address";
    public const string ClaimNumber = "claim_number";
    public const string VehicleRegistration = "vehicle_registration";
    public const string VehicleMake = "vehicle_make";
    public const string VehicleModel = "vehicle_model";
    public const string VehicleMileage = "vehicle_mileage";
    public const string VehicleMileageUnit = "vehicle_mileage_unit";
    public const string AccidentCircumstances = "accident_circumstances";
    public const string IncidentDate = "incident_date";
    public const string ContactName = "contact_name";
    public const string ContactEmailAddress = "contact_email_address";
    public const string ContactPhoneNumber = "contact_phone_number";
    public const string InstructionDate = "instruction_date";
    public const string VatStatus = "vat_status";
    public const string InspectionDate = "inspection_date";
    public const string InspectionDeadline = "inspection_deadline";
    public const string InspectionAddress = "inspection_address";
    public const string InspectionMode = "inspection_mode";
    public const string StorageLocation = "storage_location";

    // The v1 Case workspace facts (CASE-047). They are ordinary case-data
    // rows, so the record gains names here rather than columns anywhere.
    public const string RepairerAddress = "repairer_address";
    public const string ClaimSourceId = "claim_source_id";
    public const string ClaimSourceVersion = "claim_source_version";
    public const string ClaimSourceName = "claim_source_name";
    public const string ClaimSourceContactName = "claim_source_contact_name";
    public const string ClaimSourceContactTelephone = "claim_source_contact_telephone";
    public const string ClaimSourceContactEmailAddress = "claim_source_contact_email";
    public const string ClaimSourceCaseNote = "claim_source_case_note";
    public const string StorageBusinessId = "storage_business_id";
    public const string StorageBusinessVersion = "storage_business_version";
    public const string StorageBusinessName = "storage_business_name";
    public const string StorageBusinessContactName = "storage_business_contact_name";
    public const string StorageBusinessContactTelephone = "storage_business_contact_telephone";
    public const string StorageBusinessContactEmailAddress = "storage_business_contact_email";
    public const string VehicleMileageDisplayUnit = "vehicle_mileage_display_unit";
    public const string InspectionAddressTreatment = "inspection_address_treatment";
    public const string InspectionLocationChoice = "inspection_location_choice";
    public const string InspectionLocationSourceKind = "inspection_location_source_kind";
    public const string InspectionLocationSourceId = "inspection_location_source_id";
    public const string InspectionLocationSourceVersion = "inspection_location_source_version";
    public const string InspectionLocationSourceLabel = "inspection_location_source_label";
    public const string InspectionVehiclePresent = "inspection_vehicle_present";
    public const string InspectionCondition = "inspection_condition";
    public const string InspectionContactName = "inspection_contact_name";
    public const string InspectionContactTelephone = "inspection_contact_telephone";
    public const string InspectionContactEmailAddress = "inspection_contact_email";
    public const string InspectionNotes = "inspection_notes";

    public static readonly string[] All =
    [
        WorkProviderCode,
        ClaimantName,
        ClaimantContactNumber,
        ClaimantAddress,
        ClaimNumber,
        VehicleRegistration,
        VehicleMake,
        VehicleModel,
        VehicleMileage,
        VehicleMileageUnit,
        AccidentCircumstances,
        IncidentDate,
        ContactName,
        ContactEmailAddress,
        ContactPhoneNumber,
        InstructionDate,
        VatStatus,
        InspectionDate,
        InspectionDeadline,
        InspectionAddress,
        InspectionMode,
        StorageLocation,
        RepairerAddress,
        ClaimSourceId,
        ClaimSourceVersion,
        ClaimSourceName,
        ClaimSourceContactName,
        ClaimSourceContactTelephone,
        ClaimSourceContactEmailAddress,
        ClaimSourceCaseNote,
        StorageBusinessId,
        StorageBusinessVersion,
        StorageBusinessName,
        StorageBusinessContactName,
        StorageBusinessContactTelephone,
        StorageBusinessContactEmailAddress,
        VehicleMileageDisplayUnit,
        InspectionAddressTreatment,
        InspectionLocationChoice,
        InspectionLocationSourceKind,
        InspectionLocationSourceId,
        InspectionLocationSourceVersion,
        InspectionLocationSourceLabel,
        InspectionVehiclePresent,
        InspectionCondition,
        InspectionContactName,
        InspectionContactTelephone,
        InspectionContactEmailAddress,
        InspectionNotes
    ];
}

internal static class CaseDataCodes
{
    public const string Fact = "fact";
    public const string Suggestion = "suggestion";
    public const string Confirmed = "confirmed";

    public const string Text = "text";
    public const string Integer = "integer";
    public const string Date = "date";
    public const string InspectionMode = "inspection_mode";

    public const string IntakeEvidence = "intake_evidence";
    public const string MailRoute = "mail_route";
    public const string CaseAcceptance = "case_acceptance";
    public const string StaffCorrection = "staff_correction";
    public const string VehicleLookup = "vehicle_lookup";
    public const string ProviderSetting = "provider_setting";
    public const string ProviderApi = "provider_api";
}
