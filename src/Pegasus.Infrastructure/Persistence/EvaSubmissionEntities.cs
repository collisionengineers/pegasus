namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One attempt to submit a case to EVA over the API (EXT-04).
///
/// Every attempt is recorded, not only the successful one, because the four
/// outcomes FRD-07 requires stay distinct are only distinguishable if the
/// failures survive. A rejected submission and an unknown one look identical
/// from the case otherwise, and they call for opposite responses.
///
/// This is deliberately not <c>EvaFirstHandoffProxies</c>. That table is
/// check-constrained to claim neither external delivery nor Engineer
/// assignment, because producing the drag-and-drop package proves neither. An
/// accepted API submission *is* external delivery, so it needs a record that
/// is allowed to say so.
/// </summary>
internal sealed class EvaSubmissionEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }

    /// <summary>
    /// The case version this attempt carried, so an outcome can be read
    /// against the data that produced it rather than against the case as it
    /// stands now.
    /// </summary>
    public long WorkflowVersion { get; set; }

    /// <summary>
    /// The Pegasus case reference sent as EVA's <c>ExternalRef</c>. EVA
    /// enforces no uniqueness on it — the same value submitted twice creates
    /// two claims — so it is recorded here as evidence of what was sent, and
    /// the unique index below is what actually prevents the second send.
    /// </summary>
    public string ExternalRef { get; set; } = string.Empty;

    /// <summary>
    /// The outcome name from <c>EvaSubmissionOutcome</c>. Stored as text so
    /// the database rows read the same way the code does, and constrained to
    /// the four members.
    /// </summary>
    public string Outcome { get; set; } = string.Empty;

    /// <summary>
    /// True only for an outcome that reached EVA and returned an identifier.
    /// It exists as its own column so the filtered unique index below can key
    /// on it — a computed or parsed condition could not.
    /// </summary>
    public bool IsSucceeded { get; set; }

    /// <summary>EVA's response envelope identifier.</summary>
    public string? EvaId { get; set; }

    /// <summary>
    /// The File Reference EVA embeds in its message text, which is the number
    /// an operator quotes when they ring EVA about a case.
    /// </summary>
    public string? FileReference { get; set; }

    public string? FailureCode { get; set; }
    public string? FailureDetail { get; set; }
    public int ImagesSent { get; set; }
    public int AttemptCount { get; set; }
    public string ActorSubjectId { get; set; } = string.Empty;
    public DateTimeOffset SubmittedAtUtc { get; set; }
}
