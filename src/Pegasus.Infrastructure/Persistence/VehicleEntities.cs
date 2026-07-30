namespace Pegasus.Infrastructure.Persistence;

internal sealed class VehicleLookupRequestEntity
{
    public Guid WorkItemId { get; set; }
    public ExternalWorkItemEntity WorkItem { get; set; } = null!;
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public required string Registration { get; set; }
    public required string OperationKey { get; set; }
    public required string RequestFingerprint { get; set; }
    public required string RequestedByKind { get; set; }
    public required string RequestedBySubjectId { get; set; }
    public required string RequestedByRolesJson { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
    public long ResultingCaseVersion { get; set; }
    public List<VehicleLookupObservationEntity> Observations { get; set; } = [];
}

internal sealed class VehicleLookupObservationEntity
{
    public Guid Id { get; set; }
    public Guid WorkItemId { get; set; }
    public VehicleLookupRequestEntity Request { get; set; } = null!;
    public int AttemptNumber { get; set; }
    public required string Outcome { get; set; }
    public required string Registration { get; set; }
    public required string Provider { get; set; }
    public required string ProviderVersion { get; set; }
    public required string ResponseIdentity { get; set; }
    public DateTimeOffset RetrievedAtUtc { get; set; }
    public DateTimeOffset? EffectiveAtUtc { get; set; }
    public DateTimeOffset? SourceObservedAtUtc { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int? ManufactureYear { get; set; }
    public int? EngineCapacityCc { get; set; }
    public string? FuelType { get; set; }
    public required string MotTestsJson { get; set; }
    public long? MileageValue { get; set; }
    public string? MileageUnit { get; set; }
    public DateOnly? MileageObservedOn { get; set; }
    public string? MileageMethodKey { get; set; }
    public int? MileageMethodVersion { get; set; }
    public int? MileageSupportingObservationCount { get; set; }
    public string? FailureCode { get; set; }
    public bool? FailureRetryable { get; set; }
    public long? FailureRetryAfterTicks { get; set; }
    public DateTimeOffset RecordedAtUtc { get; set; }
    public List<VehicleConfirmationEntity> Confirmations { get; set; } = [];
}

internal sealed class VehicleConfirmationEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public CaseEntity Case { get; set; } = null!;
    public Guid LookupObservationId { get; set; }
    public VehicleLookupObservationEntity LookupObservation { get; set; } = null!;
    public required string Decision { get; set; }
    public required string Registration { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public long? Mileage { get; set; }
    public string? MileageUnit { get; set; }
    public required string ActorKind { get; set; }
    public required string ActorSubjectId { get; set; }
    public required string ActorRolesJson { get; set; }
    public required string OperationKey { get; set; }
    public required string RequestFingerprint { get; set; }
    public required string Reason { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public long BeforeCaseVersion { get; set; }
    public long AfterCaseVersion { get; set; }
    public required string PolicyKey { get; set; }
    public int PolicyVersion { get; set; }
}
