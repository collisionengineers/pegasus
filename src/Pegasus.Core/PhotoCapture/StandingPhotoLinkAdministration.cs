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
    InvalidDefaults,
    /// <summary>A typed website address that is not 6 to 60 lowercase letters, digits and hyphens.</summary>
    InvalidSlug,
    /// <summary>A typed website address a link already holds, a retired one included.</summary>
    SlugTaken,
    /// <summary>A saved address with no name or no address, or one longer than a contact's.</summary>
    InvalidAddress
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
/// link takes; a link is paused and resumed, given a new slug, retired for
/// good (R796), and its defaults changed within the lifetimes and modes
/// offered. A contact's slug is made here; the website's is the address the
/// Administrator typed, never one the code invents (R797). Every change
/// needs the contacts right. A change made over a stale read is refused as
/// <see cref="StandingPhotoLinkError.StaleVersion"/>, which the store raises
/// when the version it is given is not the row's. Every change, creating the
/// link included, carries its form's operation key and writes one action
/// history event naming who made it, as other Contacts changes do (FRD-04;
/// operator, 10 November 2026; R846); the same key sent again changes nothing.
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
        string operationKey,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var change = Change(actor, operationKey);
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("A contact is required.", nameof(organizationId));
        }

        if (await standingLinks.FindForOrganizationAsync(organizationId, cancellationToken) is not null)
        {
            throw new StandingPhotoLinkException(StandingPhotoLinkError.AlreadyExists);
        }

        return await CreateAsync(StandingPhotoLinkKind.Contact, organizationId, PhotoLinkToken.CreateSlug(), change, cancellationToken);
    }

    /// <summary>The one website link, at the address the Administrator typed (R797).</summary>
    public async Task<StandingPhotoLink> CreateWebsiteAsync(
        ActionActor actor,
        string operationKey,
        string? typedSlug,
        CancellationToken cancellationToken)
    {
        var change = Change(actor, operationKey);
        if (await standingLinks.FindWebsiteAsync(cancellationToken) is not null)
        {
            throw new StandingPhotoLinkException(StandingPhotoLinkError.AlreadyExists);
        }

        var slug = await TypedSlugAsync(typedSlug, cancellationToken);
        return await CreateAsync(StandingPhotoLinkKind.Website, null, slug, change, cancellationToken);
    }

    public Task<StandingPhotoLink> PauseAsync(
        ActionActor actor,
        string operationKey,
        Guid id,
        long expectedVersion,
        CancellationToken cancellationToken) =>
        standingLinks.SetPausedAsync(id, expectedVersion, paused: true, Change(actor, operationKey), cancellationToken);

    public Task<StandingPhotoLink> ResumeAsync(
        ActionActor actor,
        string operationKey,
        Guid id,
        long expectedVersion,
        CancellationToken cancellationToken) =>
        standingLinks.SetPausedAsync(id, expectedVersion, paused: false, Change(actor, operationKey), cancellationToken);

    /// <summary>
    /// A new slug; the old one answers like an unknown link from then on. A
    /// contact's is made here; the website's is <paramref name="typedSlug"/>,
    /// the address the Administrator typed (R797), which a contact's ignores.
    /// </summary>
    public async Task<StandingPhotoLink> ReplaceAsync(
        ActionActor actor,
        string operationKey,
        Guid id,
        long expectedVersion,
        string? typedSlug,
        CancellationToken cancellationToken)
    {
        var change = Change(actor, operationKey);
        var link = await standingLinks.GetAsync(id, cancellationToken)
            ?? throw new StandingPhotoLinkException(StandingPhotoLinkError.NotFound);
        var slug = link.Kind == StandingPhotoLinkKind.Website
            ? await TypedSlugAsync(typedSlug, cancellationToken)
            : PhotoLinkToken.CreateSlug();
        return await standingLinks.ReplaceSlugAsync(id, expectedVersion, slug, change, cancellationToken);
    }

    /// <summary>
    /// Ends the link for good (R796): its address answers like an unknown one
    /// and it cannot be resumed; the contact, or the website, may be given a
    /// new link.
    /// </summary>
    public Task<StandingPhotoLink> RetireAsync(
        ActionActor actor,
        string operationKey,
        Guid id,
        long expectedVersion,
        CancellationToken cancellationToken) =>
        standingLinks.SetRetiredAsync(id, expectedVersion, Change(actor, operationKey), cancellationToken);

    /// <summary>
    /// Saves an address on a contact's link (R806): shown as an inspection
    /// location on its form after the ones taken from the contact (R815).
    /// The website link has no contact and keeps no addresses.
    /// </summary>
    public async Task<StandingPhotoLink> AddAddressAsync(
        ActionActor actor,
        string operationKey,
        Guid id,
        long expectedVersion,
        string? name,
        string? address,
        CancellationToken cancellationToken)
    {
        var change = Change(actor, operationKey);
        var (savedName, savedAddress) = SavedAddress(name, address);
        await RequireContactLinkAsync(id, cancellationToken);
        return await standingLinks.AddAddressAsync(id, expectedVersion, savedName, savedAddress, change, cancellationToken);
    }

    /// <summary>Changes an address saved on the tab; the ones taken from the contact are not the tab's to change (R815).</summary>
    public async Task<StandingPhotoLink> ChangeAddressAsync(
        ActionActor actor,
        string operationKey,
        Guid id,
        long expectedVersion,
        Guid addressId,
        string? name,
        string? address,
        CancellationToken cancellationToken)
    {
        var change = Change(actor, operationKey);
        var (savedName, savedAddress) = SavedAddress(name, address);
        await RequireContactLinkAsync(id, cancellationToken);
        return await standingLinks.ChangeAddressAsync(
            id, expectedVersion, addressId, savedName, savedAddress, change, cancellationToken);
    }

    /// <summary>Removes an address saved on the tab (R815).</summary>
    public async Task<StandingPhotoLink> RemoveAddressAsync(
        ActionActor actor,
        string operationKey,
        Guid id,
        long expectedVersion,
        Guid addressId,
        CancellationToken cancellationToken)
    {
        var change = Change(actor, operationKey);
        await RequireContactLinkAsync(id, cancellationToken);
        return await standingLinks.RemoveAddressAsync(id, expectedVersion, addressId, change, cancellationToken);
    }

    public Task<StandingPhotoLink> UpdateDefaultsAsync(
        ActionActor actor,
        string operationKey,
        Guid id,
        long expectedVersion,
        PhotoCaptureMode forwardedMode,
        bool liveCameraOnly,
        int linkLifetimeDays,
        bool originalsInBackground,
        CancellationToken cancellationToken)
    {
        var change = Change(actor, operationKey);
        if (!CaptureLimits.IsLifetime(linkLifetimeDays) || !Enum.IsDefined(forwardedMode))
        {
            throw new StandingPhotoLinkException(StandingPhotoLinkError.InvalidDefaults);
        }

        return standingLinks.UpdateDefaultsAsync(
            id, expectedVersion, forwardedMode, liveCameraOnly, linkLifetimeDays, originalsInBackground, change, cancellationToken);
    }

    /// <summary>A saved address as stored: both parts trimmed and present, within a contact's own limits.</summary>
    private static (string Name, string Address) SavedAddress(string? name, string? address)
    {
        var trimmedName = name?.Trim();
        var trimmedAddress = address?.Trim();
        if (string.IsNullOrEmpty(trimmedName)
            || string.IsNullOrEmpty(trimmedAddress)
            || trimmedName.Length > Pegasus.Core.Cases.ContactDirectoryPolicy.MaximumNameLength
            || trimmedAddress.Length > Pegasus.Core.Cases.ContactDirectoryPolicy.MaximumAddressLength)
        {
            throw new StandingPhotoLinkException(StandingPhotoLinkError.InvalidAddress);
        }

        return (trimmedName, trimmedAddress);
    }

    private async Task RequireContactLinkAsync(Guid id, CancellationToken cancellationToken)
    {
        var link = await standingLinks.GetAsync(id, cancellationToken);
        if (link is not { Kind: StandingPhotoLinkKind.Contact })
        {
            throw new StandingPhotoLinkException(StandingPhotoLinkError.NotFound);
        }
    }

    /// <summary>
    /// The typed website address as stored: its slug shape is checked, and a
    /// slug any link holds, a retired one included, is taken, before anything
    /// is written.
    /// </summary>
    private async Task<string> TypedSlugAsync(string? typedSlug, CancellationToken cancellationToken)
    {
        var slug = PhotoLinkToken.NormalizeSlug(typedSlug)
            ?? throw new StandingPhotoLinkException(StandingPhotoLinkError.InvalidSlug);
        if (await standingLinks.FindBySlugAsync(slug, cancellationToken) is not null)
        {
            throw new StandingPhotoLinkException(StandingPhotoLinkError.SlugTaken);
        }

        return slug;
    }

    private Task<StandingPhotoLink> CreateAsync(
        StandingPhotoLinkKind kind,
        Guid? organizationId,
        string slug,
        StandingPhotoLinkChange change,
        CancellationToken cancellationToken) =>
        standingLinks.CreateAsync(
            kind,
            organizationId,
            slug,
            DefaultForwardedMode,
            DefaultLiveCameraOnly,
            CaptureLimits.DefaultLifetimeDays,
            DefaultOriginalsInBackground,
            change,
            cancellationToken);

    /// <summary>
    /// The change as its history event records it (R846): an actor with the
    /// contacts right, the form's operation key and now. The key is the
    /// event's correlation, so it is required and fits the history's column.
    /// </summary>
    private StandingPhotoLinkChange Change(ActionActor actor, string operationKey)
    {
        ArgumentNullException.ThrowIfNull(actor);
        StaffAuthorization.Require(actor, StaffAccessRight.ManageOrganizationsAndPrincipals);
        var key = operationKey?.Trim();
        if (string.IsNullOrEmpty(key) || key.Length > MaximumOperationKeyLength)
        {
            throw new ArgumentException("An operation key of at most 100 characters is required.", nameof(operationKey));
        }

        return new(actor, key, timeProvider.GetUtcNow());
    }

    private const int MaximumOperationKeyLength = 100;
}
