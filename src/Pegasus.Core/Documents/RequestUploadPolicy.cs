using System.Collections.Frozen;
using System.Security.Cryptography;
using Pegasus.Core.Identity;

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
    OperationConflict
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
    bool IsReplay)
{
    public bool MayEnterCustody => Decision == RequestUploadDecision.Accepted;
}

public sealed record UploadToRequestCommand(
    string Token,
    RequestUploadFile File,
    int AttemptsInCurrentRateWindow);

public sealed record UploadToRequestResult(
    RequestUploadDecision Decision,
    Guid? ReceiptId,
    bool IsReplay);

public sealed record RequestUploadPublicView(
    IReadOnlySet<string> AllowedMediaTypes,
    long MaximumFileBytes);

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

        if (!string.Equals(link.LimitsVersion, limits.Version, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The request upload link references a different accepted limits version.");
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
