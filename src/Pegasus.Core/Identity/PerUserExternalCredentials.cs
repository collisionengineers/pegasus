namespace Pegasus.Core.Identity;

public enum ExternalCredentialProvider { GlassRepairEstimate }

public sealed record PerUserExternalCredentialReference(
    Guid PegasusUserId, ExternalCredentialProvider Provider, long CredentialGeneration,
    string NormalizedExternalAccountKey, bool Enabled, long Version);

/// <summary>Server-only secret material. Never serialize to a browser, MCP, audit or log.</summary>
public sealed class PerUserExternalCredentialMaterial(
    PerUserExternalCredentialReference reference, string username, string password)
{
    public PerUserExternalCredentialReference Reference { get; } = reference;
    public string Username { get; } = username;
    public string Password { get; } = password;
    public override string ToString() => nameof(PerUserExternalCredentialMaterial);
}

public sealed record PerUserExternalCredentialStatus(
    Guid PegasusUserId, ExternalCredentialProvider Provider, bool Configured, bool Enabled,
    string? Username, long CredentialGeneration, long Version, DateTimeOffset? UpdatedAtUtc);

public interface IPerUserExternalCredentialReader
{
    Task<PerUserExternalCredentialMaterial?> GetEnabledAsync(
        ActionActor actor, ExternalCredentialProvider provider, CancellationToken cancellationToken);
}

public interface IPerUserExternalCredentialAdministration
{
    Task<PerUserExternalCredentialStatus> GetAsync(
        ActionActor actor, Guid pegasusUserId, ExternalCredentialProvider provider,
        CancellationToken cancellationToken);
    Task<PerUserExternalCredentialStatus> ReplaceAsync(
        ActionActor actor, Guid pegasusUserId, ExternalCredentialProvider provider,
        long expectedVersion, string username, string password, bool enabled,
        CancellationToken cancellationToken);
    Task ClearAsync(ActionActor actor, Guid pegasusUserId, ExternalCredentialProvider provider,
        long expectedVersion, CancellationToken cancellationToken);
}
