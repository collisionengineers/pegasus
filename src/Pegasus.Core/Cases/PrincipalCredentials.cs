using System.Security.Cryptography;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Cases;

/// <summary>
/// API-04 (FRD-09, FRD-04 § Principals administration; EPIC-011 D8): one
/// Provider API credential per Principal. The clear secret exists only in
/// the outcome of the issue/reset command; the store keeps a one-way
/// verifier and nothing here ever carries it.
/// </summary>
public enum PrincipalCredentialState
{
    Active,
    Paused,
    Revoked
}

public enum PrincipalCredentialError
{
    PrincipalNotFound,
    PrincipalInactive,
    CredentialNotFound,
    CredentialRevoked,
    CredentialAlreadyPaused,
    CredentialNotPaused,
    StaleVersion,
    OperationConflict
}

public sealed class PrincipalCredentialException(PrincipalCredentialError error)
    : Exception("The principal credential request could not be completed.")
{
    public PrincipalCredentialError Error { get; } = error;
}

/// <summary>
/// Status and timestamps only — never the verifier.
/// </summary>
public sealed record PrincipalCredentialRecord(
    Guid PrincipalId,
    string KeyId,
    PrincipalCredentialState State,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset? RotatedAtUtc,
    DateTimeOffset? PausedAtUtc,
    DateTimeOffset? RevokedAtUtc,
    long Version);

/// <summary>
/// Shared by issue/reset, pause, resume and revoke. <see cref="ExpectedVersion"/>
/// is the credential's version, 0 when the Principal has none yet.
/// </summary>
public sealed record PrincipalCredentialCommandRequest(
    Guid PrincipalId,
    long ExpectedVersion,
    ActionActor Actor,
    string OperationKey,
    string Reason);

/// <summary>
/// <see cref="Secret"/> is the clear secret, present exactly once: the first
/// completion of an operation key returns it, a replay of the same key
/// returns the record and <c>null</c> because nothing recoverable was kept.
/// </summary>
public sealed record IssuePrincipalCredentialOutcome(
    PrincipalCredentialRecord Credential,
    string? Secret);

public sealed record PrincipalCredentialIssueResult(
    PrincipalCredentialRecord Credential,
    bool Replayed);

public sealed record PrincipalCredentialVerification(
    PrincipalCredentialRecord Credential,
    bool PrincipalIsActive);

/// <summary>
/// A credential that authenticated. <see cref="MaySubmit"/> is false while
/// the credential is paused: the caller is known, and new submissions are
/// refused while authenticated reads of prior receipts and results remain
/// available (operator decision, TICK-061).
/// </summary>
public sealed record PrincipalCredentialAuthentication(
    Guid PrincipalId,
    string KeyId,
    PrincipalCredentialState State)
{
    public bool MaySubmit => State == PrincipalCredentialState.Active;
}

public interface IPrincipalCredentialStore
{
    Task<PrincipalCredentialIssueResult> IssueAsync(
        PrincipalCredentialCommandRequest request,
        string keyId,
        string secret,
        CancellationToken cancellationToken);

    Task<PrincipalCredentialRecord> PauseAsync(
        PrincipalCredentialCommandRequest request,
        CancellationToken cancellationToken);

    Task<PrincipalCredentialRecord> ResumeAsync(
        PrincipalCredentialCommandRequest request,
        CancellationToken cancellationToken);

    Task<PrincipalCredentialRecord> RevokeAsync(
        PrincipalCredentialCommandRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Null when the key is unknown or the secret does not verify. The
    /// state decision stays in <see cref="AuthenticatePrincipalCredential"/>.
    /// </summary>
    Task<PrincipalCredentialVerification?> VerifySecretAsync(
        string keyId,
        string secret,
        CancellationToken cancellationToken);
}

public interface IPrincipalCredentialQueries
{
    Task<PrincipalCredentialRecord?> GetAsync(
        Guid principalId,
        CancellationToken cancellationToken);
}

public interface IIssuePrincipalCredential
{
    Task<IssuePrincipalCredentialOutcome> ExecuteAsync(
        PrincipalCredentialCommandRequest request,
        CancellationToken cancellationToken);
}

public interface IPausePrincipalCredential
{
    Task<PrincipalCredentialRecord> ExecuteAsync(
        PrincipalCredentialCommandRequest request,
        CancellationToken cancellationToken);
}

public interface IResumePrincipalCredential
{
    Task<PrincipalCredentialRecord> ExecuteAsync(
        PrincipalCredentialCommandRequest request,
        CancellationToken cancellationToken);
}

public interface IRevokePrincipalCredential
{
    Task<PrincipalCredentialRecord> ExecuteAsync(
        PrincipalCredentialCommandRequest request,
        CancellationToken cancellationToken);
}

public interface IGetPrincipalCredential
{
    Task<PrincipalCredentialRecord?> ExecuteAsync(
        ActionActor actor,
        Guid principalId,
        CancellationToken cancellationToken);
}

/// <summary>
/// The Provider API authentication decision (TICK-058 composes the
/// transport). Unknown key, wrong secret, revoked credential and inactive
/// Principal all refuse with null; a paused credential authenticates with
/// <see cref="PrincipalCredentialAuthentication.MaySubmit"/> false.
/// </summary>
public interface IAuthenticatePrincipalCredential
{
    Task<PrincipalCredentialAuthentication?> ExecuteAsync(
        string keyId,
        string secret,
        CancellationToken cancellationToken);
}

public sealed class IssuePrincipalCredential(IPrincipalCredentialStore store)
    : IIssuePrincipalCredential
{
    private readonly IPrincipalCredentialStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public async Task<IssuePrincipalCredentialOutcome> ExecuteAsync(
        PrincipalCredentialCommandRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = PrincipalCredentialPolicy.Normalize(request);
        var keyId = PrincipalCredentialPolicy.GenerateKeyId();
        var secret = PrincipalCredentialPolicy.GenerateSecret(keyId);
        var result = await _store.IssueAsync(normalized, keyId, secret, cancellationToken);
        return new(result.Credential, result.Replayed ? null : secret);
    }
}

public sealed class PausePrincipalCredential(IPrincipalCredentialStore store)
    : IPausePrincipalCredential
{
    private readonly IPrincipalCredentialStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<PrincipalCredentialRecord> ExecuteAsync(
        PrincipalCredentialCommandRequest request,
        CancellationToken cancellationToken) =>
        _store.PauseAsync(PrincipalCredentialPolicy.Normalize(request), cancellationToken);
}

public sealed class ResumePrincipalCredential(IPrincipalCredentialStore store)
    : IResumePrincipalCredential
{
    private readonly IPrincipalCredentialStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<PrincipalCredentialRecord> ExecuteAsync(
        PrincipalCredentialCommandRequest request,
        CancellationToken cancellationToken) =>
        _store.ResumeAsync(PrincipalCredentialPolicy.Normalize(request), cancellationToken);
}

public sealed class RevokePrincipalCredential(IPrincipalCredentialStore store)
    : IRevokePrincipalCredential
{
    private readonly IPrincipalCredentialStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public Task<PrincipalCredentialRecord> ExecuteAsync(
        PrincipalCredentialCommandRequest request,
        CancellationToken cancellationToken) =>
        _store.RevokeAsync(PrincipalCredentialPolicy.Normalize(request), cancellationToken);
}

public sealed class GetPrincipalCredential(IPrincipalCredentialQueries queries)
    : IGetPrincipalCredential
{
    private readonly IPrincipalCredentialQueries _queries =
        queries ?? throw new ArgumentNullException(nameof(queries));

    public Task<PrincipalCredentialRecord?> ExecuteAsync(
        ActionActor actor,
        Guid principalId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ManageOrganizationsAndPrincipals);
        if (principalId == Guid.Empty)
        {
            throw new ArgumentException("A principal identifier is required.", nameof(principalId));
        }

        return _queries.GetAsync(principalId, cancellationToken);
    }
}

public sealed class AuthenticatePrincipalCredential(IPrincipalCredentialStore store)
    : IAuthenticatePrincipalCredential
{
    private readonly IPrincipalCredentialStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public async Task<PrincipalCredentialAuthentication?> ExecuteAsync(
        string keyId,
        string secret,
        CancellationToken cancellationToken)
    {
        if (!PrincipalCredentialPolicy.IsWellFormed(keyId, secret))
        {
            return null;
        }

        var verification = await _store.VerifySecretAsync(keyId, secret, cancellationToken);
        return PrincipalCredentialPolicy.Authenticate(verification);
    }
}

public static class PrincipalCredentialPolicy
{
    public const string SecretPrefix = "pgs_";
    public const int KeyIdLength = 16;
    private const int KeyIdBytes = 12;
    private const int SecretBytes = 32;
    private const int SecretRandomLength = 43;
    public const int SecretLength = 4 + KeyIdLength + 1 + SecretRandomLength;

    public static string GenerateKeyId() =>
        Base64Url(RandomNumberGenerator.GetBytes(KeyIdBytes));

    /// <summary>
    /// <c>pgs_&lt;key id&gt;_&lt;32 random bytes, base64url&gt;</c>. The
    /// key id rides inside the secret so a presented secret names the row it
    /// must verify against.
    /// </summary>
    public static string GenerateSecret(string keyId)
    {
        RequireKeyId(keyId);
        return SecretPrefix + keyId + "_" + Base64Url(RandomNumberGenerator.GetBytes(SecretBytes));
    }

    /// <summary>
    /// Shape check only, before any store call: right length, the
    /// <c>pgs_</c> prefix, and the embedded key id equal to the presented
    /// one. It never says whether a credential exists.
    /// </summary>
    public static bool IsWellFormed(string? keyId, string? secret) =>
        keyId is { Length: KeyIdLength }
        && keyId.All(IsBase64UrlCharacter)
        && secret is { Length: SecretLength }
        && secret.StartsWith(SecretPrefix, StringComparison.Ordinal)
        && string.CompareOrdinal(secret, SecretPrefix.Length, keyId, 0, KeyIdLength) == 0
        && secret[SecretPrefix.Length + KeyIdLength] == '_'
        && secret.Skip(SecretPrefix.Length + KeyIdLength + 1).All(IsBase64UrlCharacter);

    public static PrincipalCredentialAuthentication? Authenticate(
        PrincipalCredentialVerification? verification)
    {
        if (verification is null
            || !verification.PrincipalIsActive
            || verification.Credential.State == PrincipalCredentialState.Revoked)
        {
            return null;
        }

        var credential = verification.Credential;
        return new(credential.PrincipalId, credential.KeyId, credential.State);
    }

    public static PrincipalCredentialCommandRequest Normalize(
        PrincipalCredentialCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(
            request.Actor,
            StaffAccessRight.ManageOrganizationsAndPrincipals);
        if (request.PrincipalId == Guid.Empty)
        {
            throw new ArgumentException(
                "A principal identifier is required.",
                nameof(request));
        }
        if (request.ExpectedVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The expected version cannot be negative.");
        }

        return request with
        {
            OperationKey = OrganizationAdministrationPolicy.NormalizeRequiredText(
                request.OperationKey,
                OrganizationAdministrationPolicy.MaximumOperationKeyLength,
                nameof(request.OperationKey)),
            Reason = OrganizationAdministrationPolicy.NormalizeRequiredText(
                request.Reason,
                OrganizationAdministrationPolicy.MaximumReasonLength,
                nameof(request.Reason))
        };
    }

    /// <summary>
    /// First issue or reset. A reset replaces the key id and verifier in the
    /// same row, so the previous secret stops verifying the moment this
    /// commits; a revoked credential may be reissued the same way.
    /// </summary>
    public static PrincipalCredentialRecord PlanIssue(
        PrincipalCredentialRecord? current,
        Principal principal,
        long expectedVersion,
        string keyId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(principal);
        RequireKeyId(keyId);
        if (!principal.IsActive)
        {
            throw new PrincipalCredentialException(PrincipalCredentialError.PrincipalInactive);
        }
        if ((current?.Version ?? 0) != expectedVersion)
        {
            throw new PrincipalCredentialException(PrincipalCredentialError.StaleVersion);
        }

        return current is null
            ? new(principal.Id, keyId, PrincipalCredentialState.Active, now, null, null, null, 1)
            : current with
            {
                KeyId = keyId,
                State = PrincipalCredentialState.Active,
                RotatedAtUtc = now,
                PausedAtUtc = null,
                RevokedAtUtc = null,
                Version = checked(current.Version + 1)
            };
    }

    public static PrincipalCredentialRecord PlanPause(
        PrincipalCredentialRecord? current,
        long expectedVersion,
        DateTimeOffset now)
    {
        var credential = RequireLive(current, expectedVersion);
        if (credential.State == PrincipalCredentialState.Paused)
        {
            throw new PrincipalCredentialException(
                PrincipalCredentialError.CredentialAlreadyPaused);
        }

        return credential with
        {
            State = PrincipalCredentialState.Paused,
            PausedAtUtc = now,
            Version = checked(credential.Version + 1)
        };
    }

    public static PrincipalCredentialRecord PlanResume(
        PrincipalCredentialRecord? current,
        long expectedVersion)
    {
        var credential = RequireLive(current, expectedVersion);
        if (credential.State != PrincipalCredentialState.Paused)
        {
            throw new PrincipalCredentialException(
                PrincipalCredentialError.CredentialNotPaused);
        }

        return credential with
        {
            State = PrincipalCredentialState.Active,
            PausedAtUtc = null,
            Version = checked(credential.Version + 1)
        };
    }

    public static PrincipalCredentialRecord PlanRevoke(
        PrincipalCredentialRecord? current,
        long expectedVersion,
        DateTimeOffset now)
    {
        var credential = RequireLive(current, expectedVersion);
        return credential with
        {
            State = PrincipalCredentialState.Revoked,
            PausedAtUtc = null,
            RevokedAtUtc = now,
            Version = checked(credential.Version + 1)
        };
    }

    private static PrincipalCredentialRecord RequireLive(
        PrincipalCredentialRecord? current,
        long expectedVersion)
    {
        if (current is null)
        {
            throw new PrincipalCredentialException(PrincipalCredentialError.CredentialNotFound);
        }
        if (current.State == PrincipalCredentialState.Revoked)
        {
            throw new PrincipalCredentialException(PrincipalCredentialError.CredentialRevoked);
        }
        if (current.Version != expectedVersion)
        {
            throw new PrincipalCredentialException(PrincipalCredentialError.StaleVersion);
        }

        return current;
    }

    private static void RequireKeyId(string keyId)
    {
        if (keyId is not { Length: KeyIdLength } || !keyId.All(IsBase64UrlCharacter))
        {
            throw new ArgumentException(
                $"A key identifier is {KeyIdLength} base64url characters.",
                nameof(keyId));
        }
    }

    private static bool IsBase64UrlCharacter(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '-' or '_';

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
