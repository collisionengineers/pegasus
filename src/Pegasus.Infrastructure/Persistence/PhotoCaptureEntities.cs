namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One vehicle's photo link and what it received (FRD-28). Its status is
/// derived on read and never stored; the counters bound what it may take
/// (<c>CaptureLimits</c>) and are written with each item in the same
/// transaction, under the row's lock.
/// </summary>
internal sealed class PhotoCaptureEntity
{
    public Guid Id { get; set; }
    public required string Origin { get; set; }
    public Guid? StandingLinkId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? CaseId { get; set; }

    /// <summary>The Case's reference, kept with the Case id so the lists search it without a join.</summary>
    public string? CaseReference { get; set; }
    public Guid? SubmissionGroupId { get; set; }
    public required string Destination { get; set; }
    public string? RoutingReason { get; set; }
    public string? NormalizedRegistration { get; set; }
    public DateOnly? IncidentDate { get; set; }
    public string? CustomerName { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }

    /// <summary>The sender's typed details (<c>PhotoCaptureDetails</c>) as web-default JSON.</summary>
    public required string DetailsJson { get; set; }
    public string? Note { get; set; }
    public required string Mode { get; set; }
    public bool LiveCameraOnly { get; set; }
    public bool OriginalsInBackground { get; set; }
    public Guid? IssuedByStaffId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public DateTimeOffset? OpenedAtUtc { get; set; }
    public DateTimeOffset? FirstItemAtUtc { get; set; }
    public DateTimeOffset? LastItemAtUtc { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
    public DateTimeOffset? SettledAtUtc { get; set; }
    public int SettledBatches { get; set; }
    public int AcceptedItemCount { get; set; }
    public long AcceptedBytes { get; set; }

    // The per-kind totals the caps count (CaptureLimits).
    public int PhotoCount { get; set; }
    public int OriginalCount { get; set; }
    public int DocumentCount { get; set; }
    public int VideoCount { get; set; }
    public int ReminderCount { get; set; }
    public DateTimeOffset? LastRemindedAtUtc { get; set; }
    public DateTimeOffset? CallLoggedAtUtc { get; set; }
    public long Version { get; set; }
}

/// <summary>
/// One token issued for a capture, by the SHA-256 digest of its secret only:
/// the link in one message, one copy, one hand-off or one phone session.
/// </summary>
internal sealed class PhotoCaptureTokenEntity
{
    public required string TokenDigest { get; set; }
    public Guid CaptureId { get; set; }
    public required string Purpose { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; }
    public Guid? IssuedByStaffId { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}

/// <summary>One photo, document or video a capture received, unique by the id the phone minted for it.</summary>
internal sealed class PhotoCaptureItemEntity
{
    public Guid Id { get; set; }
    public Guid CaptureId { get; set; }
    public Guid ClientItemId { get; set; }
    public required string Kind { get; set; }
    public string? Area { get; set; }
    public string? Stage { get; set; }
    public int Position { get; set; }
    public int Batch { get; set; }
    public int Ordinal { get; set; }
    public int StoryGroup { get; set; }
    public int StorySequence { get; set; }
    public required string FileName { get; set; }
    public required string MediaType { get; set; }
    public DateTimeOffset? TakenAtUtc { get; set; }
    public bool LiveCamera { get; set; }
    public bool InReportOnArrival { get; set; }
    public required string ContentHash { get; set; }
    public long ContentLength { get; set; }
    public Guid? StagedReceiptId { get; set; }
    public Guid? IntakeReceiptId { get; set; }
    public Guid? IntakeAssetId { get; set; }
    public bool OriginalReceived { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
}

/// <summary>
/// A standing link: a contact's, or the one website link. It only starts
/// captures; a replaced slug simply stops resolving.
/// </summary>
internal sealed class StandingPhotoLinkEntity
{
    public Guid Id { get; set; }
    public required string Kind { get; set; }
    public Guid? OrganizationId { get; set; }
    public required string Slug { get; set; }
    public required string ForwardedMode { get; set; }
    public bool LiveCameraOnly { get; set; }
    public int LinkLifetimeDays { get; set; }
    public bool OriginalsInBackground { get; set; }
    public DateTimeOffset? PausedAtUtc { get; set; }
    public DateTimeOffset? RetiredAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public required string CreatedByKind { get; set; }
    public required string CreatedBySubjectId { get; set; }
    public long Version { get; set; }
}
