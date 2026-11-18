using System.Security.Cryptography;
using System.Text;

namespace Pegasus.Core.Identity;

public enum ExternalCredentialProvider { GlassRepairEstimate }

public sealed record PerUserExternalCredentialReference(
    Guid PegasusUserId, ExternalCredentialProvider Provider, long CredentialGeneration,
    string NormalizedExternalAccountKey, bool Enabled, long Version);

/// <summary>
/// The one rule a Glass's login becomes an account key by: SHA-256 of the
/// provider's name and the upper-cased username, so a login is one key
/// whoever holds it. A staff member's stored login and the login the
/// deployment holds for the Automation both take theirs from here, so the same
/// login is one live session's slot whichever of them holds it (design 07
/// section 1). The username itself is never recorded on a session.
/// </summary>
public static class GlassAccountKey
{
    public static string Of(string username)
    {
        ArgumentNullException.ThrowIfNull(username);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            "glass_repair_estimate\n" + username.ToUpperInvariant())));
    }
}

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

    /// <summary>
    /// The actor's enabled credential for the provider, with its account key,
    /// without reading the secret material; null when they hold none. One read.
    /// </summary>
    Task<PerUserExternalCredentialReference?> GetEnabledReferenceAsync(
        ActionActor actor, ExternalCredentialProvider provider, CancellationToken cancellationToken);
}

public interface IPerUserExternalCredentialAdministration
{
    Task<PerUserExternalCredentialStatus> GetAsync(
        ActionActor actor, Guid pegasusUserId, ExternalCredentialProvider provider,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, PerUserExternalCredentialStatus>> GetManyAsync(
        ActionActor actor,
        IReadOnlyCollection<Guid> pegasusUserIds,
        ExternalCredentialProvider provider,
        CancellationToken cancellationToken);

    Task<PerUserExternalCredentialStatus> ReplaceAsync(
        ActionActor actor, Guid pegasusUserId, ExternalCredentialProvider provider,
        long expectedCredentialVersion, long expectedStaffAccountVersion,
        string username, string password, bool enabled,
        CancellationToken cancellationToken);
    Task ClearAsync(ActionActor actor, Guid pegasusUserId, ExternalCredentialProvider provider,
        long expectedCredentialVersion, long expectedStaffAccountVersion,
        CancellationToken cancellationToken);
}
