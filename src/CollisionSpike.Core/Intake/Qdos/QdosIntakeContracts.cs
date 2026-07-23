namespace CollisionSpike.Core.Intake.Qdos;

public enum QdosIntakeDecision
{
    ConfirmedQdos,
    NeedsSorting,
    Unsupported,
    OcrRequired,
    TechnicalFailure
}

public enum QdosEvidenceSource
{
    EmailBody,
    PdfContent,
    Sender,
    Subject,
    FileName,
    MimeType,
    SystemDefault
}

public enum QdosEvidenceStrength
{
    Strong,
    Weak
}

public enum QdosEvidenceFinding
{
    SupportsQdos,
    ContradictsTransport,
    ExtractedField,
    ConflictingField,
    MissingField,
    Information
}

public enum IntakeSourceReadStatus
{
    Readable,
    Unsupported,
    TechnicalFailure
}

public sealed record QdosIntakeSource(
    string FileName,
    string MediaType,
    ReadOnlyMemory<byte> Content,
    DateTimeOffset ReceivedAtUtc,
    string Actor,
    bool CaseCreationAuthorized);

public sealed record IntakeContentFragment(
    QdosEvidenceSource Source,
    string SourceLabel,
    string Text);

public sealed record IntakeTransportEvidence(
    QdosEvidenceSource Source,
    string Value);

public sealed record IntakeSourceIssue(
    string Code,
    string Reason,
    QdosEvidenceSource Source);

public sealed record IntakeSourceReadResult(
    IntakeSourceReadStatus Status,
    IReadOnlyList<IntakeContentFragment> Content,
    IReadOnlyList<IntakeTransportEvidence> TransportEvidence,
    IReadOnlyList<IntakeSourceIssue> Issues,
    bool RequiresOcr,
    string? FailureCode = null,
    string? FailureReason = null);

public sealed record QdosEvidence(
    QdosEvidenceSource Source,
    QdosEvidenceStrength Strength,
    QdosEvidenceFinding Finding,
    string Signal,
    string Detail);

public sealed record QdosFieldCandidate(
    string Value,
    QdosEvidenceSource Source,
    string SourceLabel);

public sealed record QdosReviewField(
    string Name,
    string? SuggestedValue,
    IReadOnlyList<QdosFieldCandidate> Candidates,
    bool IsDefaulted,
    bool HasConflict);

public sealed record QdosIntakeRecord(
    Guid Id,
    string SourceFileName,
    string MediaType,
    long SourceLength,
    string SourceHash,
    DateTimeOffset ReceivedAtUtc,
    QdosIntakeDecision Decision,
    string DecisionReason,
    IReadOnlyList<QdosEvidence> Evidence,
    IReadOnlyList<QdosReviewField> Fields,
    IReadOnlyList<string> MissingFields,
    Guid? CaseId,
    string? CaseReference,
    string? FailureCode,
    string? FailureReason,
    bool IsDuplicate);

public sealed record QdosIntakeDraft(
    string SourceFileName,
    string MediaType,
    long SourceLength,
    string SourceHash,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset ProcessedAtUtc,
    int ReferenceYear,
    string Actor,
    bool CaseCreationAuthorized,
    QdosIntakeDecision Decision,
    string DecisionReason,
    IReadOnlyList<QdosEvidence> Evidence,
    IReadOnlyList<QdosReviewField> Fields,
    IReadOnlyList<string> MissingFields,
    string? FailureCode,
    string? FailureReason);

public sealed record QdosQueueCounts(int Review, int NeedsSorting);

public sealed record QdosIntakeSummary(
    Guid Id,
    string SourceFileName,
    DateTimeOffset ReceivedAtUtc,
    QdosIntakeDecision Decision,
    string? CaseReference,
    string? FailureReason);

public interface IQdosIntakeSourceReader
{
    Task<IntakeSourceReadResult> ReadAsync(QdosIntakeSource source, CancellationToken cancellationToken);
}

public interface IQdosIntakeStore
{
    Task<QdosIntakeRecord> StoreAsync(QdosIntakeDraft draft, CancellationToken cancellationToken);
}

public interface IQdosIntakeQueries
{
    Task<QdosQueueCounts> GetCountsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<QdosIntakeSummary>> ListAsync(
        QdosIntakeDecision? decision,
        CancellationToken cancellationToken);

    Task<QdosIntakeRecord?> GetAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class CaseReferenceSequenceExhaustedException(string principalCode, int year)
    : Exception($"The {principalCode} reference sequence for {year} has reached 999.")
{
    public string PrincipalCode { get; } = principalCode;

    public int Year { get; } = year;
}
