namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// An immutable Image-intake registration. Case association deliberately does
/// not live here: it derives from the origin receipt's single current
/// association, so the two records can never disagree.
/// </summary>
internal sealed class ImageIntakeEntity
{
    public Guid Id { get; set; }
    public Guid OriginReceiptId { get; set; }
    public IntakeReceiptEntity OriginReceipt { get; set; } = null!;
    public required string SourceChannel { get; set; }
    public required string ExternalReceiptToken { get; set; }
    public required string SourceHash { get; set; }
    public Guid EvaluationRevisionId { get; set; }

    /// <summary>
    /// The submission group this registration covers, when the group is the
    /// registration unit (INTK-015): at most one ImageIntake exists per
    /// group, enforced by a filtered unique index. Null for a single-receipt
    /// (non-grouped or legacy) registration.
    /// </summary>
    public Guid? SubmissionGroupId { get; set; }

    public required string NormalizedVehicleRegistration { get; set; }
    public required string ImageIntakeReference { get; set; }
    public Guid? PrincipalId { get; set; }
    public PrincipalEntity? Principal { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public required string CreatedByActorKind { get; set; }
    public required string CreatedByActorSubjectId { get; set; }
    public required string Reason { get; set; }
    public required string CreationOperationKey { get; set; }
    public required string RequestFingerprint { get; set; }
    public required string LifecycleState { get; set; }
    public long LifecycleVersion { get; set; }
    public Guid? MergedIntoCaseId { get; set; }
    public string? MergedIntoCaseReference { get; set; }
    public string? ClosureReason { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }

    /// <summary>
    /// External evidence-storage (Box) state for this Image-initiated Case:
    /// null (registered before this capability), "pending" (folder work
    /// queued), "confirmed" (folder + images stored), "merged" (contents
    /// folded into the paired case and the folder removed), or "failed"
    /// (work terminally failed; blob custody remains authoritative).
    /// </summary>
    public string? CustodyState { get; set; }
    public string? CustodyRootRemoteId { get; set; }
    public DateTimeOffset? CustodyConfirmedAtUtc { get; set; }
    public DateTimeOffset? CustodyMergedAtUtc { get; set; }
}

/// <summary>
/// The one list of persisted <see cref="ImageIntakeEntity.CustodyState"/>
/// values. Null on the entity means the registration predates image-case
/// custody.
/// </summary>
internal static class ImageCustodyStates
{
    public const string Pending = "pending";
    public const string Confirmed = "confirmed";
    public const string Merged = "merged";
    public const string Failed = "failed";
}

internal sealed class ImageIntakeLifecycleEventEntity
{
    public Guid Id { get; set; }
    public Guid ImageIntakeId { get; set; }
    public ImageIntakeEntity ImageIntake { get; set; } = null!;
    public required string EventType { get; set; }
    public required string ActorKind { get; set; }
    public required string ActorSubjectId { get; set; }
    public required string ActorRolesJson { get; set; }
    public required string Reason { get; set; }
    public required string OperationKey { get; set; }
    public required string RequestFingerprint { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public long BeforeVersion { get; set; }
    public long AfterVersion { get; set; }
    public Guid? CaseId { get; set; }
    public string? CaseReference { get; set; }
}

/// <summary>
/// Per-VRM Image Intake Reference allocation. Deliberately no ceiling: the
/// reference format expands past `-99` instead of exhausting, and a sequence
/// value is never reused.
/// </summary>
internal sealed class ImageIntakeSequenceEntity
{
    public required string NormalizedVehicleRegistration { get; set; }
    public int LastAllocatedSequence { get; set; }
}

/// <summary>
/// One recognition run's outcome for one retained source image, kept separate
/// from confirmed case data. Abstention and failure are recorded outcomes;
/// the later staff/system disposition never rewrites the original result.
/// </summary>
internal sealed class ImageVrmSuggestionEntity
{
    public Guid Id { get; set; }
    public Guid IntakeReceiptId { get; set; }
    public IntakeReceiptEntity IntakeReceipt { get; set; } = null!;
    public Guid IntakeAssetId { get; set; }
    public IntakeAssetEntity IntakeAsset { get; set; } = null!;
    public required string StorageKey { get; set; }
    public required string ContentHash { get; set; }
    public required string EngineKey { get; set; }
    public required string EngineVersion { get; set; }
    public required string ModelHashes { get; set; }
    public required string Outcome { get; set; }
    public string? SuggestedRegistration { get; set; }
    public double? Confidence { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
    public required string OperationKey { get; set; }
    public required string Disposition { get; set; }
    public string? DispositionActor { get; set; }
    public string? DispositionReason { get; set; }
    public string? DispositionOperationKey { get; set; }
    public DateTimeOffset? DisposedAtUtc { get; set; }
}
