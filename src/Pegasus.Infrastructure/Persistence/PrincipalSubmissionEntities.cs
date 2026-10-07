namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One Principal API submission (API-01): the Principal binding processing
/// reads and the instruction that Principal declared. Its id is derived from
/// the Principal and the idempotency key; its intake receipt carries the token
/// <see cref="Id"/> in "N" form, and the submitted files are that receipt's
/// attachments.
/// </summary>
internal sealed class PrincipalSubmissionEntity
{
    public Guid Id { get; set; }
    public Guid PrincipalId { get; set; }
    public PrincipalEntity Principal { get; set; } = null!;
    public DateTimeOffset ReceivedAtUtc { get; set; }

    /// <summary>
    /// What the Principal declared, as submitted. Kept whole rather than spread
    /// across columns because it is retained evidence of one request, not
    /// queryable case data — the case's own fields are written from it at
    /// allocation and are what anything else reads.
    /// </summary>
    public required string DeclaredInstructionJson { get; set; }
}
