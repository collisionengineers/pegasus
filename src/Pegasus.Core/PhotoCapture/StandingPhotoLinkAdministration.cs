using Pegasus.Core.Identity;

namespace Pegasus.Core.PhotoCapture;

public enum StandingPhotoLinkError
{
    /// <summary>The contact already has a standing link, or the website link already exists.</summary>
    AlreadyExists,
    NotFound,
    /// <summary>The link changed after it was read; the change was made over a stale version.</summary>
    StaleVersion,
    /// <summary>A lifetime or mode the link does not offer.</summary>
    InvalidDefaults
}

public sealed class StandingPhotoLinkException(StandingPhotoLinkError error)
    : Exception("The standing photo link change was not accepted.")
{
    public StandingPhotoLinkError Error { get; } = error;
}

/// <summary>
/// The staff changes to a standing photo link (FRD-28 Standing photo links;
/// R774, R780): a Principal, Claim Source or Repairer contact gets one
/// standing link and the website one, each created with the defaults a new
/// link takes; a link is paused and resumed, given a new slug, and its
/// defaults changed within the lifetimes and modes offered. Every change
/// needs the contacts right. A change made over a stale read is refused as
/// <see cref="StandingPhotoLinkError.StaleVersion"/>, which the store raises
/// when the version it is given is not the row's.
/// </summary>
public sealed class StandingPhotoLinkAdministration(
    IStandingPhotoLinkStore standingLinks,
    TimeProvider timeProvider)
{
    /// <summary>The defaults a new standing link starts with: guided captures, the live camera only, originals in the background.</summary>
    public const PhotoCaptureMode DefaultForwardedMode = PhotoCaptureMode.Guided;
    public const bool DefaultLiveCameraOnly = true;
    public const bool DefaultOriginalsInBackground = true;

    public async Task<StandingPhotoLink> CreateForContactAsync(
        ActionActor actor,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        Require(actor);
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("A contact is required.", nameof(organizationId));
        }

        if (await standingLinks.FindForOrganizationAsync(organizationId, cancellationToken) is not null)
        {
            throw new StandingPhotoLinkException(StandingPhotoLinkError.AlreadyExists);
        }

        return await CreateAsync(StandingPhotoLinkKind.Contact, organizationId, actor, cancellationToken);
    }

    public async Task<StandingPhotoLink> CreateWebsiteAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        Require(actor);
        if (await standingLinks.FindWebsiteAsync(cancellationToken) is not null)
        {
            throw new StandingPhotoLinkException(StandingPhotoLinkError.AlreadyExists);
        }

        return await CreateAsync(StandingPhotoLinkKind.Website, null, actor, cancellationToken);
    }

    public Task<StandingPhotoLink> PauseAsync(ActionActor actor, Guid id, long expectedVersion, CancellationToken cancellationToken)
    {
        Require(actor);
        return standingLinks.SetPausedAsync(id, expectedVersion, timeProvider.GetUtcNow(), cancellationToken);
    }

    public Task<StandingPhotoLink> ResumeAsync(ActionActor actor, Guid id, long expectedVersion, CancellationToken cancellationToken)
    {
        Require(actor);
        return standingLinks.SetPausedAsync(id, expectedVersion, null, cancellationToken);
    }

    /// <summary>A new slug; the old one answers like an unknown link from then on.</summary>
    public Task<StandingPhotoLink> ReplaceAsync(ActionActor actor, Guid id, long expectedVersion, CancellationToken cancellationToken)
    {
        Require(actor);
        return standingLinks.ReplaceSlugAsync(id, expectedVersion, PhotoLinkToken.CreateSlug(), timeProvider.GetUtcNow(), cancellationToken);
    }

    public Task<StandingPhotoLink> UpdateDefaultsAsync(
        ActionActor actor,
        Guid id,
        long expectedVersion,
        PhotoCaptureMode forwardedMode,
        bool liveCameraOnly,
        int linkLifetimeDays,
        bool originalsInBackground,
        CancellationToken cancellationToken)
    {
        Require(actor);
        if (!CaptureLimits.IsLifetime(linkLifetimeDays) || !Enum.IsDefined(forwardedMode))
        {
            throw new StandingPhotoLinkException(StandingPhotoLinkError.InvalidDefaults);
        }

        return standingLinks.UpdateDefaultsAsync(
            id, expectedVersion, forwardedMode, liveCameraOnly, linkLifetimeDays, originalsInBackground, cancellationToken);
    }

    private Task<StandingPhotoLink> CreateAsync(
        StandingPhotoLinkKind kind,
        Guid? organizationId,
        ActionActor actor,
        CancellationToken cancellationToken) =>
        standingLinks.CreateAsync(
            kind,
            organizationId,
            PhotoLinkToken.CreateSlug(),
            DefaultForwardedMode,
            DefaultLiveCameraOnly,
            CaptureLimits.DefaultLifetimeDays,
            DefaultOriginalsInBackground,
            actor,
            timeProvider.GetUtcNow(),
            cancellationToken);

    private static void Require(ActionActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ManageOrganizationsAndPrincipals);
    }
}
