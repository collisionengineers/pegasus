namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One Provider API submission (API-01, TICK-058): the idempotency record
/// for a Principal's key and the Principal binding processing reads for the
/// intake submission group whose token is <see cref="Id"/> in "N" form. The
/// files themselves are ordinary staged receipts on the provider_api channel.
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
}
