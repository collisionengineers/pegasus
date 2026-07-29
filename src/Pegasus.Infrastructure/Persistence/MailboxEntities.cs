namespace Pegasus.Infrastructure.Persistence;

internal sealed class ApprovedInboxPollStateEntity
{
    public required string MailboxId { get; set; }
    public required string MailboxAddress { get; set; }
    public string? Cursor { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public string? LeaseToken { get; set; }
    public DateTimeOffset? LeaseExpiresAtUtc { get; set; }
    public DateTimeOffset? LastCompletedAtUtc { get; set; }
    public string? LastFailureCode { get; set; }
}

internal sealed class IntakeMailRouteDecisionEntity
{
    public Guid IntakeReceiptId { get; set; }
    public IntakeReceiptEntity IntakeReceipt { get; set; } = null!;
    public required string Disposition { get; set; }
    public string? RouteOwnerCode { get; set; }
    public string? RouteKind { get; set; }
    public string? WorkProviderCode { get; set; }
    public required string PredicatesJson { get; set; }
    public required string Reason { get; set; }
    public required string PolicyKey { get; set; }
    public int PolicyVersion { get; set; }
    public required string TransportIdentitiesJson { get; set; }
    public required string OriginalIdentitiesJson { get; set; }
    public string? EffectiveSenderAddress { get; set; }
    public string? EffectiveSenderSourceLabel { get; set; }
}
