using System.Net;

namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// Box answered 429: its rate limit, with the wait it asked for when it sent
/// one. It is an <see cref="HttpRequestException"/> with the 429 status, so a
/// caller that only knows the status reads it exactly as before.
/// </summary>
internal sealed class BoxThrottledException(TimeSpan? retryAfter)
    : HttpRequestException("Box returned 429.", null, HttpStatusCode.TooManyRequests)
{
    /// <summary>Box's <c>Retry-After</c>, or null when it sent none.</summary>
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
