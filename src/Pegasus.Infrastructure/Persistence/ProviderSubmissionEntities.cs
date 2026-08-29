namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One Provider API submission (API-01): the idempotency record for a
/// Principal's key, the Principal binding processing reads, and the instruction
/// that Principal declared. Its intake receipt carries the token
/// <see cref="Id"/> in "N" form, and the submitted files are that receipt's
/// attachments.
/// </summary>
internal sealed class ProviderSubmissionEntity
{
    public Guid Id { get; set; }
    public Guid PrincipalId { get; set; }
    public PrincipalEntity Principal { get; set; } = null!;
    public required string KeyId { get; set; }
    public required string IdempotencyKey { get; set; }
    public string? ProviderReference { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }

    /// <summary>
    /// What the Principal declared, as submitted. Kept whole rather than spread
    /// across columns because it is retained evidence of one request, not
    /// queryable case data — the case's own fields are written from it at
    /// allocation and are what anything else reads.
    /// </summary>
    public required string DeclaredInstructionJson { get; set; }

    /// <summary>
    /// The staged receipt the submission was retained as. Null only between the
    /// row being written and the retention that immediately follows it.
    /// </summary>
    public Guid? StagedReceiptId { get; set; }
}
