namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One current assessment field value with recording and confirmation
/// provenance. The current row never erases evidence: every change is
/// recorded with its before and after values in the permanent action
/// history written by the same transaction.
/// </summary>
internal sealed class CaseAssessmentFieldEntity
{
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public required string FieldPath { get; set; }
    public required string Value { get; set; }
    public required string RecordedByKind { get; set; }
    public required string RecordedBy { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public string? ConfirmedBy { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
}

internal sealed class CaseEstimateLineEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public int Position { get; set; }
    public required string LineType { get; set; }
    public string? GuideCode { get; set; }
    public string? Description { get; set; }
    public decimal? WorkUnits { get; set; }
    public decimal? PaintWorkUnits { get; set; }
    public int? Quantity { get; set; }
    public decimal? Price { get; set; }
    public bool Unpriced { get; set; }
    public string? PartNumber { get; set; }
    public string? Betterment { get; set; }
    public string? Status { get; set; }
    public string? EvidenceLabel { get; set; }
    public string? Justification { get; set; }
    public required string RecordedByKind { get; set; }
    public required string RecordedBy { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public string? ConfirmedBy { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
    public string? Operation { get; set; }
    public decimal? Materials { get; set; }
    public string? OriginalValuesJson { get; set; }
    public string? CurrentValuesJson { get; set; }
    public string? SourceDocumentIdentity { get; set; }
    public Guid? SourceDocumentVersionId { get; set; }
    public string? SourceDocumentSha256 { get; set; }
    public string? SourceRowIdentity { get; set; }
    public string? AmendedBy { get; set; }
    public DateTimeOffset? AmendedAtUtc { get; set; }
    public Guid? RepairSpecificationId { get; set; }
    public CaseRepairSpecificationEntity? RepairSpecification { get; set; }
}

internal sealed class CaseRepairSpecificationEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public int Version { get; set; }
    public required string State { get; set; }
    public required string SourceRoute { get; set; }
    public string? SourceArtifactReference { get; set; }
    public string? SourceVersion { get; set; }
    public string? SourceSha256 { get; set; }
    public decimal? CalculationLabour { get; set; }
    public decimal? CalculationParts { get; set; }
    public decimal? CalculationPaintMaterials { get; set; }
    public decimal? CalculationSpecialistOther { get; set; }
    public bool? RepairerVatRegistered { get; set; }
    public decimal? CalculationVat { get; set; }
    public decimal? CalculationTotal { get; set; }
    public string? CalculationPolicyVersion { get; set; }
    public required string CreatedBy { get; set; }
    public required string CreationOperationKey { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? AcceptedBy { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
    public Guid? SupersedesSpecificationId { get; set; }
    public string? SupersessionReason { get; set; }

    // Named estimate header (ENG-026). Exactly one row per case carries
    // IsCurrent; the Current estimate is the one the report costs from.
    public required string Name { get; set; }
    public int? RepairDays { get; set; }
    public decimal? LabourRate { get; set; }
    public decimal? PaintMaterials { get; set; }
    public decimal? OtherCosts { get; set; }
    public Guid? RateCardId { get; set; }
    public long? RateCardVersion { get; set; }
    public decimal? PartsDiscountPercent { get; set; }
    public decimal? MaterialsDiscountPercent { get; set; }
    public decimal? SpecialistDiscountPercent { get; set; }
    public decimal? OverallDiscountPercent { get; set; }
    public bool? LabourVatApplicable { get; set; }
    public bool? PartsVatApplicable { get; set; }
    public bool? MaterialsVatApplicable { get; set; }
    public bool? SpecialistVatApplicable { get; set; }
    public string RepairerVatStatus { get; set; } = "Unknown";
    public string? VatOverrideReason { get; set; }
    public string? CalculationBreakdownJson { get; set; }
    public decimal VatPercent { get; set; }
    public string? Notes { get; set; }
    public bool IsCurrent { get; set; }
    public Guid? AiJobId { get; set; }
    public string? DiscardedBy { get; set; }
    public DateTimeOffset? DiscardedAtUtc { get; set; }
    public string? DiscardReason { get; set; }
    public string? LastOperationKey { get; set; }
    public List<CaseEstimateLineEntity> Lines { get; set; } = [];
}

internal sealed class CaseValuationEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public required string Source { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public DateOnly? GuideMonth { get; set; }
    public long Mileage { get; set; }
    public decimal RetailValue { get; set; }
    public decimal TradeValue { get; set; }
    public required string RecordedBy { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public string? LastEditedBy { get; set; }
    public DateTimeOffset? LastEditedAtUtc { get; set; }
}

/// <summary>
/// The Send to AI work request (AI-09): the Core-owned tracking record for
/// one pointer hand-off. Idempotent per (case, operation key); state
/// transitions are optimistic on Version.
/// </summary>
internal sealed class AiWorkRequestEntity
{
    public Guid RequestId { get; set; }
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public required string CaseReference { get; set; }
    public long CaseVersionAtSend { get; set; }
    public required string CapabilityScope { get; set; }
    public required string Instruction { get; set; }
    public required string State { get; set; }
    public required string OperationKey { get; set; }
    public required string RequestHash { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public required string CreatedBy { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? HandedOffAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public string? ClosureReason { get; set; }
    public string? ReplyStatus { get; set; }
    public string? ReplyMessage { get; set; }
    public long Version { get; set; }
}

/// <summary>
/// Single-row Administrator switch for the Send to AI outbound hand-off,
/// mirroring the Automation client kill-switch pattern.
/// </summary>
internal sealed class SendToAiControlEntity
{
    public const string SingletonId = "send-to-ai";

    public required string Id { get; set; }
    public bool Enabled { get; set; }
    public int Version { get; set; }

    // Administration-entered connector settings (MCP-07). Null means the
    // composed configuration value applies. The token is stored protected
    // and is never readable back through Administration.
    public string? ChannelBaseUrl { get; set; }
    public double? TimeoutSeconds { get; set; }
    public string? ChannelTokenProtected { get; set; }
    public DateTimeOffset? TokenRotatedAtUtc { get; set; }
}

/// <summary>
/// One AI job on the pull-based ledger (AI-10, ADR-0035). Creation is
/// idempotent per operation key; transitions are optimistic on Version.
/// A subject that is a Case is correlated to it by SubjectId; no foreign
/// key, because the subject may also be an Unidentified item or the queue.
/// </summary>
internal sealed class AiJobEntity
{
    public Guid JobId { get; set; }
    public required string Kind { get; set; }
    public required string SubjectKind { get; set; }
    public Guid? SubjectId { get; set; }
    public required string SubjectReference { get; set; }
    public required string Instruction { get; set; }
    public int? TargetPercentOfEngineerValue { get; set; }
    public decimal? EngineerValueAtSend { get; set; }
    public required string State { get; set; }
    public required string OperationKey { get; set; }
    public required string RequestHash { get; set; }
    public required string CreatedByKind { get; set; }
    public required string CreatedBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public string? TakenBy { get; set; }
    public DateTimeOffset? TakenAtUtc { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public string? ProgressNote { get; set; }
    public string? ResultKind { get; set; }
    public string? ResultReference { get; set; }
    public string? ResultText { get; set; }
    public Guid? MarketResearchDocumentOccurrenceId { get; set; }
    public Guid? MarketResearchDocumentVersionId { get; set; }
    public Guid? MarketResearchValuationId { get; set; }
    public DateOnly? MarketResearchRecordedDate { get; set; }
    public TimeOnly? MarketResearchRecordedTime { get; set; }
    public long? MarketResearchMileage { get; set; }
    public decimal? MarketResearchRetailValue { get; set; }
    public decimal? MarketResearchTradeValue { get; set; }
    public string? MarketResearchCompletionHash { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public string? ClosureReason { get; set; }
    public string? LastOperationKey { get; set; }
    public long Version { get; set; }
}
