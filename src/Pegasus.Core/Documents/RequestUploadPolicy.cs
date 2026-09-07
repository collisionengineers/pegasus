using System.Collections.Frozen;
using System.Security.Cryptography;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Documents;

public enum RequestUploadStatus
{
    Pending,
    Active,
    Expired,
    Exhausted,
    Revoked,
    Failed
}

public enum RequestUploadDecision
{
    Accepted,
    Replay,
    Unavailable,
    RateLimited,
    InvalidFile,
    LimitExceeded,
    OperationConflict,

    /// <summary>
    /// The link was issued against a different accepted limits version. This
    /// is a typed refusal rather than an exception because it is an ordinary,
    /// recoverable state of a long-lived link — the sender did nothing wrong
    /// and the case owner can reissue.
    /// </summary>
    LimitsVersionMismatch,

    /// <summary>
    /// Custody did not take the bytes: it refused them, or the hand-over
    /// neither confirmed nor refused. Nothing was kept, so this is never
    /// rendered as a success, and the sender may send the same file again
    /// under the same operation key — that key reconciles an uncertain
    /// hand-over instead of offering the bytes twice.
    /// </summary>
    /// <remarks>
    /// None of the other members says this. <see cref="Unavailable"/> means
    /// the link itself is gone and hides the Case, <see cref="RateLimited"/>
    /// would name a limit that was not reached, and
    /// <see cref="InvalidFile"/> / <see cref="LimitExceeded"/> would blame a
    /// file that policy had already accepted.
    /// </remarks>
    NotRetained,

    /// <summary>
    /// Custody took the bytes durably but has not confirmed them yet. The
    /// submission stands and must not be sent again, so this is not a refusal;
    /// it is also not <see cref="Accepted"/>, because nothing may tell the
    /// sender their document is retained before custody has said it is.
    /// </summary>
    /// <remarks>
    /// The store, not the policy, decides this: it is what a Pending custody
    /// disposition becomes. Keeping it out of <see cref="Accepted"/> is what
    /// stops the one surface that speaks to the sender making a claim about
    /// custody that custody has not made.
    /// </remarks>
    AcceptedPending
}

public sealed class RequestUploadLimits
{
    private readonly FrozenSet<string> allowedMediaTypes;

    public RequestUploadLimits(
        string version,
        TimeSpan lifetime,
        int maximumFileCount,
        long maximumFileBytes,
        long maximumRequestBytes,
        IEnumerable<string> allowedMediaTypes,
        int rateLimit,
        TimeSpan rateLimitWindow)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(lifetime, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFileCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFileBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumRequestBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rateLimit);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rateLimitWindow, TimeSpan.Zero);
        ArgumentNullException.ThrowIfNull(allowedMediaTypes);

        if (maximumRequestBytes < maximumFileBytes)
        {
            throw new ArgumentException(
                "The request byte limit cannot be lower than the per-file byte limit.",
                nameof(maximumRequestBytes));
        }

        var normalizedMediaTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mediaType in allowedMediaTypes)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);
            normalizedMediaTypes.Add(mediaType.Trim());
        }

        if (normalizedMediaTypes.Count == 0)
        {
            throw new ArgumentException("At least one media type must be allowed.", nameof(allowedMediaTypes));
        }

        this.allowedMediaTypes = normalizedMediaTypes.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        Version = version;
        Lifetime = lifetime;
        MaximumFileCount = maximumFileCount;
        MaximumFileBytes = maximumFileBytes;
        MaximumRequestBytes = maximumRequestBytes;
        RateLimit = rateLimit;
        RateLimitWindow = rateLimitWindow;
    }

    public string Version { get; }

    public TimeSpan Lifetime { get; }

    public int MaximumFileCount { get; }

    public long MaximumFileBytes { get; }

    public long MaximumRequestBytes { get; }

    public IReadOnlySet<string> AllowedMediaTypes => allowedMediaTypes;

    public int RateLimit { get; }

    public TimeSpan RateLimitWindow { get; }

    public bool AllowsMediaType(string mediaType) =>
        !string.IsNullOrWhiteSpace(mediaType) && allowedMediaTypes.Contains(mediaType.Trim());
}

public sealed class RequestUploadSecret
{
    internal RequestUploadSecret(string token)
    {
        Token = token;
    }

    public string Token { get; }

    public override string ToString() => "[REDACTED]";
}

public sealed record RequestUploadTokenIssue(RequestUploadSecret Secret, string TokenDigest);

public static class RequestUploadToken
{
    public const int TokenSizeBytes = 32;

    public static RequestUploadTokenIssue Create()
    {
        Span<byte> bytes = stackalloc byte[TokenSizeBytes];
        RandomNumberGenerator.Fill(bytes);
        var token = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return new(new(token), ComputeDigest(token));
    }

    public static string ComputeDigest(string token)
    {
        Span<byte> tokenBytes = stackalloc byte[43];
        if (!TryWriteToken(token, tokenBytes))
        {
            throw new ArgumentException(
                "The request upload token is malformed.",
                nameof(token));
        }

        return ComputeLowercaseSha256(tokenBytes);
    }

    internal static string ComputeLowercaseSha256(ReadOnlySpan<byte> value)
    {
        const string LowercaseHexCharacters = "0123456789abcdef";

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(value, digest);
        Span<char> characters = stackalloc char[SHA256.HashSizeInBytes * 2];
        for (var index = 0; index < digest.Length; index++)
        {
            var digestByte = digest[index];
            characters[index * 2] = LowercaseHexCharacters[digestByte >> 4];
            characters[(index * 2) + 1] = LowercaseHexCharacters[digestByte & 0x0f];
        }

        return new string(characters);
    }

    public static bool Matches(string token, string expectedDigest)
    {
        if (expectedDigest is null
            || expectedDigest.Length != SHA256.HashSizeInBytes * 2)
        {
            return false;
        }

        Span<byte> tokenBytes = stackalloc byte[43];
        if (!TryWriteToken(token, tokenBytes))
        {
            return false;
        }

        Span<byte> expected = stackalloc byte[SHA256.HashSizeInBytes];
        for (var index = 0; index < expected.Length; index++)
        {
            var highNibble = ParseHexDigit(expectedDigest[index * 2]);
            var lowNibble = ParseHexDigit(expectedDigest[(index * 2) + 1]);
            if ((highNibble | lowNibble) < 0)
            {
                return false;
            }

            expected[index] = (byte)((highNibble << 4) | lowNibble);
        }

        Span<byte> actual = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(tokenBytes, actual);
        return CryptographicOperations.FixedTimeEquals(actual, expected);

        static int ParseHexDigit(char character)
        {
            if (character is >= '0' and <= '9')
            {
                return character - '0';
            }

            if (character is >= 'a' and <= 'f')
            {
                return character - 'a' + 10;
            }

            if (character is >= 'A' and <= 'F')
            {
                return character - 'A' + 10;
            }

            return -1;
        }
    }

    private static bool TryWriteToken(string? token, Span<byte> destination)
    {
        if (token is null || token.Length != destination.Length)
        {
            return false;
        }

        for (var index = 0; index < token.Length; index++)
        {
            var character = token[index];
            if (!(character is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-' or '_'))
            {
                return false;
            }

            destination[index] = (byte)character;
        }

        return true;
    }
}

public sealed record RequestUploadLink(
    Guid Id,
    Guid CaseId,
    string TokenDigest,
    RequestUploadStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc,
    int AcceptedFileCount,
    long AcceptedByteCount,
    string LimitsVersion,
    long Version,
    string? Recipient = null,
    string? Reason = null);

public sealed record CreateRequestUploadLinkCommand(
    Guid CaseId,
    ActionActor Actor,
    string OperationKey,
    long ExpectedCaseVersion,
    string EditLeaseToken,
    string? Recipient = null,
    string? Reason = null);

public sealed record CreateRequestUploadLinkResult(
    RequestUploadLink Link,
    RequestUploadSecret? Secret,
    bool IsReplay);

public sealed record RevokeRequestUploadLinkCommand(
    Guid CaseId,
    Guid RequestId,
    ActionActor Actor,
    string Reason,
    string OperationKey,
    long ExpectedRequestVersion,
    long ExpectedCaseVersion,
    string EditLeaseToken);

public sealed class DocumentRequestUnavailableException()
    : InvalidOperationException(
        "Document request links are unavailable until an accepted limits version is configured.");

public sealed record RequestUploadFile(
    string FileName,
    string MediaType,
    ReadOnlyMemory<byte> Content,
    string OperationKey);

public sealed record RequestUploadAttempt(
    string Token,
    RequestUploadFile File,
    int AttemptsInCurrentRateWindow);

public sealed record RequestUploadAuthorization(
    RequestUploadDecision Decision,
    string? ContentHash,
    string? SafeFileName,
    bool IsReplay,
    bool MayReissue = false)
{
    public bool MayEnterCustody => Decision == RequestUploadDecision.Accepted;
}

public sealed record UploadToRequestCommand(
    string Token,
    RequestUploadFile File,
    int AttemptsInCurrentRateWindow,
    Guid? ReplacementOccurrenceId = null);

public sealed record UploadToRequestResult(
    RequestUploadDecision Decision,
    Guid? ReceiptId,
    bool IsReplay);

public sealed record FinalizeRequestUploadResult(
    RequestUploadDecision Decision,
    bool IsReplay);

/// <summary>
/// The operation key one public submission is addressed by, and the single
/// server-issued variant of it.
/// </summary>
/// <remarks>
/// <para>
/// A sender's key is the identity of one deliberate submission of one exact
/// file: the page mints one per load, re-presents an outstanding one rather
/// than replacing it, and a retry under it is that same submission. A second,
/// <em>different</em> file sent while the first is still outstanding is not
/// that. The outstanding key still names the first file and has to keep naming
/// it, so the second file is a new deliberate submission and needs a key of
/// its own.
/// </para>
/// <para>
/// That key is derived rather than minted at random, so a retry of the second
/// file is still a retry: one root and one set of bytes always name the same
/// submission. The digest is in the key only to tell one file from another
/// under the same root - the root remains the intent identity, and nothing
/// here substitutes a link-and-hash identity for it across two deliberate
/// submissions (Stream A, PR 673 comment 5560737585).
/// </para>
/// <para>
/// Derivation is always from the root, never from an already-derived key, so
/// the shape is exactly two: a root, or a root and one digest. That bounds the
/// key at <see cref="MaximumLength"/> however many different files are sent
/// through one link.
/// </para>
/// </remarks>
public static class RequestUploadOperationKey
{
    /// <summary>
    /// What separates the root from the digest. Outside the hexadecimal both
    /// halves are, so it can never be mistaken for part of either.
    /// </summary>
    private const char ContentSeparator = '~';

    private const int RootLength = 32;
    private const int DigestLength = 64;

    /// <summary>
    /// The longest key this shape produces: a root, the separator and one
    /// digest. Every column that carries the key, scoped or not, holds it.
    /// </summary>
    public const int MaximumLength = RootLength + 1 + DigestLength;

    /// <summary>
    /// The key as it is stored and compared, or false when it is not a key
    /// this server issued. Normalizing is a lowercasing and a trim only: a
    /// sender that sends back something else entirely is refused rather than
    /// corrected.
    /// </summary>
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim().ToLowerInvariant();
        var separator = candidate.IndexOf(ContentSeparator, StringComparison.Ordinal);
        if (separator < 0)
        {
            if (!IsDigits(candidate, RootLength))
            {
                return false;
            }

            normalized = candidate;
            return true;
        }
        if (!IsDigits(candidate.AsSpan(0, separator), RootLength)
            || !IsDigits(candidate.AsSpan(separator + 1), DigestLength))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    /// <summary>
    /// The intent identity a key belongs to: itself, or the root it was
    /// derived from. Every derivation starts here, so no key ever carries two
    /// digests.
    /// </summary>
    public static string Root(string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        var separator = operationKey.IndexOf(ContentSeparator, StringComparison.Ordinal);
        return separator < 0 ? operationKey : operationKey[..separator];
    }

    /// <summary>
    /// The key one deliberate submission of these exact bytes is addressed by
    /// under this root. The same bytes always give the same key, which is what
    /// makes the second file's own retry a retry rather than a third file.
    /// </summary>
    public static string ForContent(string operationKey, string sha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        return $"{Root(operationKey)}{ContentSeparator}{sha256.Trim().ToLowerInvariant()}";
    }

    private static bool IsDigits(ReadOnlySpan<char> value, int length)
    {
        if (value.Length != length)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiHexDigitLower(character))
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Everything the public page may know. It carries no request reference, no
/// expiry and no Case identity - only the limits, current session state and
/// server-issued occurrence slots the sender needs for this submission.
/// </summary>
/// <param name="UnresolvedOperationKey">
/// The key of a submission this link has already taken that has not resolved -
/// arrived, uncertain, or accepted and not yet confirmed - or null when the
/// link has nothing outstanding. While one stands, the page presents that key
/// again instead of a new one, so a retry reconciles the submission custody
/// may already hold rather than becoming a second one.
/// </param>
public sealed record RequestUploadPublicView(
    IReadOnlySet<string> AllowedMediaTypes,
    long MaximumFileBytes,
    string? UnresolvedOperationKey = null,
    PublicUploadSessionState SessionState = PublicUploadSessionState.NotStarted,
    IReadOnlyList<RequestUploadOccurrenceView>? Occurrences = null)
{
    public IReadOnlyList<RequestUploadOccurrenceView> Files =>
        Occurrences ?? Array.Empty<RequestUploadOccurrenceView>();
}

public sealed record RequestUploadOccurrenceView(
    Guid Id,
    string FileName,
    IncomingArtifactCustodyState CustodyState);

/// <summary>
/// The one submission session a public link may have. The window is fixed, not
/// sliding: it opens when the first file's content <em>and</em> custody are
/// both confirmed, and closes fifteen minutes later whatever else arrives. Its
/// life is <see cref="StartedAtUtc"/> null (nothing has landed yet), then open,
/// then either finalized or expired — and both of those refuse bytes.
/// </summary>
public sealed record PublicUploadSession(
    Guid Id,
    Guid RequestUploadLinkId,
    string LimitsVersion,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinalizedAtUtc,
    DateTimeOffset? ExpiresAtUtc,
    long Version)
{
    public bool HasStarted => StartedAtUtc is not null;

    public bool IsFinalized => FinalizedAtUtc is not null;
}

/// <summary>
/// Why a session refuses. <see cref="Open"/> is the only state that accepts.
/// </summary>
public enum PublicUploadSessionState
{
    /// <summary>Nothing has been confirmed yet; the window has not opened.</summary>
    NotStarted,

    /// <summary>Open and inside the fixed window.</summary>
    Open,

    /// <summary>Finalized: the sender said they were done and it was recorded.</summary>
    Finalized,

    /// <summary>The fixed window closed.</summary>
    Expired
}

/// <summary>
/// One addressable slot in a session. The identity is server-issued so an
/// addition or a replacement names the slot rather than a file name: two files
/// called the same thing are two occurrences and neither overwrites the other.
/// </summary>
public sealed record PublicUploadOccurrence(
    Guid Id,
    Guid SessionId,
    string OperationKey,
    string ProposedName,
    string MediaType,
    long Size,
    string Sha256,
    IncomingArtifactCustodyState CustodyState,
    Guid? DocumentId = null,
    Guid? DocumentVersionId = null)
{
    /// <summary>
    /// Only a confirmed occurrence counts. A pending, failed or uncertain one
    /// renders its own typed state, never success, and never lets the session
    /// be finalized on its behalf.
    /// </summary>
    public bool CountsTowardsTheSession =>
        CustodyState == IncomingArtifactCustodyState.Confirmed;
}

/// <summary>
/// The fixed-window rule for a public submission session.
/// </summary>
public static class PublicUploadSessionPolicy
{
    /// <summary>
    /// The window a confirmed first file opens. Fifteen minutes, from that
    /// moment, not extended by anything that arrives later.
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Opens the window. Only a file whose content and custody are both
    /// confirmed may start one, so a run of failed attempts leaves the session
    /// exactly where it was and the sender keeps the full window once
    /// something actually lands.
    /// </summary>
    public static PublicUploadSession Start(
        PublicUploadSession session,
        PublicUploadOccurrence firstConfirmed,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(firstConfirmed);
        if (!firstConfirmed.CountsTowardsTheSession)
        {
            throw new ArgumentException(
                "Only a confirmed occurrence starts the submission window.",
                nameof(firstConfirmed));
        }
        if (session.HasStarted)
        {
            // A later success never extends a window that is already open.
            return session;
        }

        return session with
        {
            StartedAtUtc = nowUtc,
            ExpiresAtUtc = nowUtc.Add(Window),
            Version = checked(session.Version + 1)
        };
    }

    public static PublicUploadSessionState Evaluate(
        PublicUploadSession session,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.IsFinalized)
        {
            return PublicUploadSessionState.Finalized;
        }
        if (!session.HasStarted)
        {
            return PublicUploadSessionState.NotStarted;
        }

        return session.ExpiresAtUtc is { } expiresAtUtc && nowUtc >= expiresAtUtc
            ? PublicUploadSessionState.Expired
            : PublicUploadSessionState.Open;
    }

    /// <summary>
    /// Whether more bytes may be accepted. A session that has not started yet
    /// accepts — that is how it starts.
    /// </summary>
    public static bool AcceptsBytes(PublicUploadSession session, DateTimeOffset nowUtc) =>
        Evaluate(session, nowUtc) is PublicUploadSessionState.NotStarted
            or PublicUploadSessionState.Open;

    /// <summary>
    /// Finalization is replay-safe on the operation key: the first call
    /// records the moment and every later one returns the same session
    /// unchanged. A session that never started, or that expired first, cannot
    /// be finalized.
    /// </summary>
    public static PublicUploadSession Finalize(
        PublicUploadSession session,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.IsFinalized)
        {
            return session;
        }

        return Evaluate(session, nowUtc) == PublicUploadSessionState.Open
            ? session with
            {
                FinalizedAtUtc = nowUtc,
                Version = checked(session.Version + 1)
            }
            : throw new InvalidOperationException(
                "Only an open submission session can be finalized.");
    }
}

public interface ICreateRequestUploadLink
{
    Task<CreateRequestUploadLinkResult> ExecuteAsync(
        CreateRequestUploadLinkCommand command,
        CancellationToken cancellationToken = default);
}

public interface IRevokeRequestUploadLink
{
    Task ExecuteAsync(
        RevokeRequestUploadLinkCommand command,
        CancellationToken cancellationToken = default);
}

public interface IUploadToRequest
{
    Task<UploadToRequestResult> ExecuteAsync(
        UploadToRequestCommand command,
        CancellationToken cancellationToken = default);

    Task<FinalizeRequestUploadResult> FinalizeAsync(
        string token,
        CancellationToken cancellationToken = default);
}

public interface IGetRequestUpload
{
    Task<RequestUploadPublicView?> ExecuteAsync(
        string token,
        CancellationToken cancellationToken = default);
}

public sealed class RequestUploadPolicy
{
    private readonly RequestUploadLimits limits;
    private readonly TimeProvider timeProvider;

    public RequestUploadPolicy(RequestUploadLimits limits, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(limits);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.limits = limits;
        this.timeProvider = timeProvider;
    }

    public static RequestUploadTokenIssue CreateToken() => RequestUploadToken.Create();

    public static CreateRequestUploadLinkCommand NormalizeCreate(
        CreateRequestUploadLinkCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command with
        {
            Recipient = NormalizeMetadata(command.Recipient, 500, nameof(command.Recipient)),
            Reason = NormalizeMetadata(command.Reason, 1000, nameof(command.Reason))
        };
    }

    public DateTimeOffset CalculateExpiry(DateTimeOffset createdAtUtc)
    {
        if (createdAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The request upload creation time must be UTC.", nameof(createdAtUtc));
        }

        return createdAtUtc.Add(limits.Lifetime);
    }

    public RequestUploadAuthorization Authorize(
        RequestUploadLink link,
        RequestUploadAttempt attempt,
        string? existingOperationContentHash = null)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(attempt.File);

        // A long-lived link outliving a limits change is an ordinary state of
        // the world, not a programming error, so it is a typed refusal the
        // public page can render and the case owner can act on — never an
        // exception used as control flow.
        if (!string.Equals(link.LimitsVersion, limits.Version, StringComparison.Ordinal))
        {
            return new(RequestUploadDecision.LimitsVersionMismatch, null, null, false, MayReissue: true);
        }

        if (!HasAcceptedLifetime(link)
            || !RequestUploadToken.Matches(attempt.Token, link.TokenDigest)
            || link.Status is not (RequestUploadStatus.Active or RequestUploadStatus.Exhausted)
            || link.RevokedAtUtc is not null
            || timeProvider.GetUtcNow() >= link.ExpiresAtUtc
            || link.AcceptedFileCount < 0
            || link.AcceptedByteCount < 0)
        {
            return Unavailable();
        }

        if (attempt.AttemptsInCurrentRateWindow < 0
            || attempt.AttemptsInCurrentRateWindow >= limits.RateLimit)
        {
            return new(RequestUploadDecision.RateLimited, null, null, false);
        }

        if (string.IsNullOrWhiteSpace(attempt.File.OperationKey))
        {
            return new(RequestUploadDecision.InvalidFile, null, null, false);
        }

        var contentHash = RequestUploadToken.ComputeLowercaseSha256(attempt.File.Content.Span);
        if (existingOperationContentHash is not null)
        {
            return string.Equals(existingOperationContentHash, contentHash, StringComparison.Ordinal)
                ? new(RequestUploadDecision.Replay, contentHash, null, true)
                : new(RequestUploadDecision.OperationConflict, null, null, false);
        }

        if (link.Status != RequestUploadStatus.Active
            || link.AcceptedFileCount >= limits.MaximumFileCount
            || link.AcceptedByteCount >= limits.MaximumRequestBytes)
        {
            return new(RequestUploadDecision.LimitExceeded, null, null, false);
        }

        if (attempt.File.Content.IsEmpty
            || !limits.AllowsMediaType(attempt.File.MediaType)
            || string.IsNullOrWhiteSpace(attempt.File.FileName))
        {
            return new(RequestUploadDecision.InvalidFile, null, null, false);
        }

        var contentLength = attempt.File.Content.Length;
        if (contentLength > limits.MaximumFileBytes
            || link.AcceptedFileCount == int.MaxValue
            || link.AcceptedFileCount + 1 > limits.MaximumFileCount
            || contentLength > limits.MaximumRequestBytes - link.AcceptedByteCount)
        {
            return new(RequestUploadDecision.LimitExceeded, null, null, false);
        }

        var safeFileName = GetSafeFileName(attempt.File.FileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return new(RequestUploadDecision.InvalidFile, null, null, false);
        }

        return new(RequestUploadDecision.Accepted, contentHash, safeFileName, false);
    }

    private bool HasAcceptedLifetime(RequestUploadLink link)
    {
        if (link.CreatedAtUtc.Offset != TimeSpan.Zero || link.ExpiresAtUtc.Offset != TimeSpan.Zero)
        {
            return false;
        }

        try
        {
            return link.ExpiresAtUtc == link.CreatedAtUtc.Add(limits.Lifetime);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static string? GetSafeFileName(string fileName)
    {
        var normalized = fileName.Replace('\\', '/');
        var safeFileName = Path.GetFileName(normalized);
        return safeFileName is "." or ".."
            || safeFileName.Any(char.IsControl)
            ? null
            : safeFileName;
    }

    private static string? NormalizeMetadata(
        string? value,
        int maximumLength,
        string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("Upload-request metadata cannot be blank.", parameterName);
        }
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Upload-request metadata cannot exceed {maximumLength} characters.");
        }
        return normalized;
    }

    private static RequestUploadAuthorization Unavailable() =>
        new(RequestUploadDecision.Unavailable, null, null, false);
}
