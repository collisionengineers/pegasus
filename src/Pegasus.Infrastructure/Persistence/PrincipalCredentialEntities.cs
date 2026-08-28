namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// One Provider API credential per Principal (API-04, TICK-061). Only the
/// PBKDF2 verifier is stored — the clear secret is returned once by the
/// issue command and never written anywhere. A reset replaces KeyId and
/// SecretHash in place, so the row's identity is the Principal.
/// </summary>
internal sealed class PrincipalApiCredentialEntity
{
    public Guid PrincipalId { get; set; }
    public PrincipalEntity Principal { get; set; } = null!;
    public required string KeyId { get; set; }
    public required string SecretHash { get; set; }
    public required string State { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset? RotatedAtUtc { get; set; }
    public DateTimeOffset? PausedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public long Version { get; set; }
}
