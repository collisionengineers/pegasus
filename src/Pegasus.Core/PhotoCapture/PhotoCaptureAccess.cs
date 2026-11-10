namespace Pegasus.Core.PhotoCapture;

/// <summary>
/// What the capture surface answers a presented credential with: the live
/// capture or standing link, or one <see cref="Unavailable"/> that stands for
/// unknown, expired, revoked and paused alike, for a capture whose Case is
/// archived (R793), and for a standing link whose contact is deactivated
/// (R796). They are never told apart
/// on the wire (FRD-28, ADR-0090): the sender's next step is the same, to
/// ask for a new link, and one answer gives a guesser nothing.
/// <see cref="TokenPurpose"/> is the presented token's purpose, for a capture.
/// </summary>
public sealed record CaptureAccess(PhotoCapture? Capture, StandingPhotoLink? StandingLink, PhotoLinkTokenPurpose? TokenPurpose = null)
{
    public static CaptureAccess Unavailable { get; } = new(null, null);

    public bool IsAvailable => Capture is not null || StandingLink is not null;
}

public sealed class PhotoCaptureAccess(
    IPhotoCaptureStore captures,
    IStandingPhotoLinkStore standingLinks,
    TimeProvider timeProvider)
{
    public const string CapturePrefix = "c.";
    public const string StandingPrefix = "s.";

    /// <summary>
    /// Resolves the <c>X-Capture-Link</c> header value: <c>c.</c> and a
    /// token, or <c>s.</c> and a slug. The shape is checked before any store
    /// is read.
    /// </summary>
    public Task<CaptureAccess> ResolveAsync(string? credential, CancellationToken cancellationToken)
    {
        if (credential is null)
        {
            return Task.FromResult(CaptureAccess.Unavailable);
        }

        if (credential.StartsWith(CapturePrefix, StringComparison.Ordinal))
        {
            return ResolveTokenAsync(credential[CapturePrefix.Length..], cancellationToken);
        }

        if (credential.StartsWith(StandingPrefix, StringComparison.Ordinal))
        {
            return ResolveSlugAsync(credential[StandingPrefix.Length..], cancellationToken);
        }

        return Task.FromResult(CaptureAccess.Unavailable);
    }

    public async Task<CaptureAccess> ResolveTokenAsync(string? token, CancellationToken cancellationToken)
    {
        if (!PhotoLinkToken.HasTokenShape(token))
        {
            return CaptureAccess.Unavailable;
        }

        var found = await captures.FindByTokenDigestAsync(PhotoLinkToken.Digest(token!), cancellationToken);
        if (found is null)
        {
            return CaptureAccess.Unavailable;
        }

        var (capture, tokenRevoked, caseArchived, purpose) = found.Value;
        return IsLive(capture, timeProvider.GetUtcNow(), caseArchived) && !tokenRevoked
            ? new(capture, null, purpose)
            : CaptureAccess.Unavailable;
    }

    /// <summary>
    /// Whether a call on a token of this purpose records the capture Opened.
    /// A Session token is the phone that made the capture (a hand-off's staff
    /// phone sending the form's paperwork, or "Take photos now", which is
    /// Opened when it is made), so it never opens the link for its sender.
    /// </summary>
    public static bool MarksOpened(PhotoLinkTokenPurpose? purpose) => purpose != PhotoLinkTokenPurpose.Session;

    public async Task<CaptureAccess> ResolveSlugAsync(string? slug, CancellationToken cancellationToken)
    {
        var normalized = PhotoLinkToken.NormalizeSlug(slug);
        if (normalized is null)
        {
            return CaptureAccess.Unavailable;
        }

        // A paused or retired link, and one whose contact is deactivated (R796), answer as unknown.
        var link = await standingLinks.FindBySlugAsync(normalized, cancellationToken);
        return link is { StartsCaptures: true } ? new(null, link) : CaptureAccess.Unavailable;
    }

    /// <summary>
    /// A capture takes additions until it expires or is cancelled; a sent one
    /// stays open to more (add-only). Once its Case is archived the link is
    /// dead and takes nothing more (operator, 9 November 2026; R793).
    /// </summary>
    public static bool IsLive(PhotoCapture capture, DateTimeOffset nowUtc, bool caseArchived = false)
    {
        ArgumentNullException.ThrowIfNull(capture);
        return !caseArchived && !capture.IsRevoked && nowUtc < capture.ExpiresAtUtc;
    }
}
