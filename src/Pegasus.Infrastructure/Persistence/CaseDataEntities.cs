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
