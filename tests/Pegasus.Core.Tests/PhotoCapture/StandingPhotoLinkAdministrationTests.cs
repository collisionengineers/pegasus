using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;
using static Pegasus.Core.Tests.PhotoCapture.PhotoCaptureTestData;

namespace Pegasus.Core.Tests.PhotoCapture;

/// <summary>
/// The staff changes to a standing photo link (FRD-28 Standing photo links;
/// R774, R780) have one owner: a contact gets one link and the website one,
/// each with the defaults a new link takes; every change needs the contacts
/// right; a change over a stale read is the store's typed refusal. A link
/// whose contact is deactivated starts nothing and a retired one never again
/// (R796); the website's address is the one the Administrator typed (R797).
/// </summary>
public sealed class StandingPhotoLinkAdministrationTests
{
    private static readonly ActionActor Administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
    private static readonly ActionActor Engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
    private static readonly Guid Bodyshop = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private const string WebsiteSlug = "collision-photos";

    [Fact]
    public async Task AContactsLinkIsCreatedWithTheDefaultsAndASlugOfItsOwn()
    {
        var store = new FakeStandingLinks();
        var administration = new StandingPhotoLinkAdministration(store, new FixedClock(Now));

        var link = await administration.CreateForContactAsync(Administrator, Bodyshop, CancellationToken.None);

        Assert.Equal(StandingPhotoLinkKind.Contact, link.Kind);
        Assert.Equal(Bodyshop, link.OrganizationId);
        Assert.True(PhotoLinkToken.HasSlugShape(link.Slug));
        Assert.Equal(PhotoCaptureMode.Guided, link.ForwardedMode);
        Assert.True(link.LiveCameraOnly);
        Assert.Equal(CaptureLimits.DefaultLifetimeDays, link.LinkLifetimeDays);
        Assert.True(link.OriginalsInBackground);
        Assert.True(link.IsActive);
        Assert.Equal(Now, link.CreatedAtUtc);
        Assert.Equal(1, link.Version);
    }

    [Fact]
    public async Task AContactHasOneStandingLinkAndTheWebsiteHasOne()
    {
        var store = new FakeStandingLinks();
        var administration = new StandingPhotoLinkAdministration(store, new FixedClock(Now));
        await administration.CreateForContactAsync(Administrator, Bodyshop, CancellationToken.None);
        var website = await administration.CreateWebsiteAsync(Administrator, WebsiteSlug, CancellationToken.None);

        var second = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.CreateForContactAsync(Administrator, Bodyshop, CancellationToken.None));
        var secondWebsite = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.CreateWebsiteAsync(Administrator, "another-address", CancellationToken.None));

        Assert.Equal(StandingPhotoLinkError.AlreadyExists, second.Error);
        Assert.Equal(StandingPhotoLinkError.AlreadyExists, secondWebsite.Error);
        Assert.Equal(StandingPhotoLinkKind.Website, website.Kind);
        Assert.Null(website.OrganizationId);
        Assert.Equal(2, store.Links.Count);
    }

    [Fact]
    public async Task EveryChangeNeedsTheContactsRight()
    {
        var store = new FakeStandingLinks();
        var administration = new StandingPhotoLinkAdministration(store, new FixedClock(Now));
        var link = await administration.CreateForContactAsync(Administrator, Bodyshop, CancellationToken.None);
        var writes = store.Writes;

        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => administration.CreateForContactAsync(Engineer, Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => administration.CreateWebsiteAsync(Engineer, WebsiteSlug, CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => administration.PauseAsync(Engineer, link.Id, link.Version, CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => administration.ReplaceAsync(Engineer, link.Id, link.Version, typedSlug: null, CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => administration.RetireAsync(Engineer, link.Id, link.Version, CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => administration.UpdateDefaultsAsync(
                Engineer, link.Id, link.Version, PhotoCaptureMode.Quick, false, 3, false, CancellationToken.None));
        Assert.Equal(writes, store.Writes);
    }

    [Fact]
    public async Task PauseResumeAndReplaceCarryTheVersionAndAStaleOneIsRefused()
    {
        var store = new FakeStandingLinks();
        var administration = new StandingPhotoLinkAdministration(store, new FixedClock(Now));
        var created = await administration.CreateForContactAsync(Administrator, Bodyshop, CancellationToken.None);

        var paused = await administration.PauseAsync(Administrator, created.Id, created.Version, CancellationToken.None);
        Assert.Equal(Now, paused.PausedAtUtc);
        Assert.False(paused.IsActive);
        Assert.Equal(2, paused.Version);

        var stale = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.ResumeAsync(Administrator, created.Id, created.Version, CancellationToken.None));
        Assert.Equal(StandingPhotoLinkError.StaleVersion, stale.Error);
        Assert.False(store.Links.Single().IsActive);

        var resumed = await administration.ResumeAsync(Administrator, created.Id, paused.Version, CancellationToken.None);
        Assert.True(resumed.IsActive);

        var replaced = await administration.ReplaceAsync(Administrator, created.Id, resumed.Version, typedSlug: null, CancellationToken.None);
        Assert.NotEqual(created.Slug, replaced.Slug);
        Assert.True(PhotoLinkToken.HasSlugShape(replaced.Slug));
        Assert.Equal(4, replaced.Version);
    }

    [Fact]
    public async Task DefaultsOutsideTheOfferedLifetimesAreRefusedBeforeAnyWrite()
    {
        var store = new FakeStandingLinks();
        var administration = new StandingPhotoLinkAdministration(store, new FixedClock(Now));
        var link = await administration.CreateForContactAsync(Administrator, Bodyshop, CancellationToken.None);
        var writes = store.Writes;

        var refused = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.UpdateDefaultsAsync(
                Administrator, link.Id, link.Version, PhotoCaptureMode.Quick, false, 5, false, CancellationToken.None));
        Assert.Equal(StandingPhotoLinkError.InvalidDefaults, refused.Error);
        Assert.Equal(writes, store.Writes);

        var updated = await administration.UpdateDefaultsAsync(
            Administrator, link.Id, link.Version, PhotoCaptureMode.Quick, false, 14, false, CancellationToken.None);
        Assert.Equal(PhotoCaptureMode.Quick, updated.ForwardedMode);
        Assert.False(updated.LiveCameraOnly);
        Assert.Equal(14, updated.LinkLifetimeDays);
        Assert.False(updated.OriginalsInBackground);
        Assert.Equal(2, updated.Version);
    }

    [Fact]
    public async Task TheWebsiteLinkTakesTheSlugTheAdministratorTyped()
    {
        var store = new FakeStandingLinks();
        var administration = new StandingPhotoLinkAdministration(store, new FixedClock(Now));

        var website = await administration.CreateWebsiteAsync(Administrator, " Collision-Photos ", CancellationToken.None);

        Assert.Equal(StandingPhotoLinkKind.Website, website.Kind);
        Assert.Equal(WebsiteSlug, website.Slug);
    }

    [Fact]
    public async Task AWebsiteSlugOfTheWrongShapeOrAlreadyInUseIsRefusedBeforeAnyWrite()
    {
        var store = new FakeStandingLinks();
        var administration = new StandingPhotoLinkAdministration(store, new FixedClock(Now));
        var contact = await administration.CreateForContactAsync(Administrator, Bodyshop, CancellationToken.None);
        var writes = store.Writes;

        var missing = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.CreateWebsiteAsync(Administrator, null, CancellationToken.None));
        var tooShort = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.CreateWebsiteAsync(Administrator, "photo", CancellationToken.None));
        var badCharacters = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.CreateWebsiteAsync(Administrator, "collision photos", CancellationToken.None));
        var taken = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.CreateWebsiteAsync(Administrator, contact.Slug, CancellationToken.None));

        Assert.Equal(StandingPhotoLinkError.InvalidSlug, missing.Error);
        Assert.Equal(StandingPhotoLinkError.InvalidSlug, tooShort.Error);
        Assert.Equal(StandingPhotoLinkError.InvalidSlug, badCharacters.Error);
        Assert.Equal(StandingPhotoLinkError.SlugTaken, taken.Error);
        Assert.Equal(writes, store.Writes);
    }

    [Fact]
    public async Task ReplacingTheWebsiteLinkTakesTheNewTypedSlug()
    {
        var store = new FakeStandingLinks();
        var administration = new StandingPhotoLinkAdministration(store, new FixedClock(Now));
        var website = await administration.CreateWebsiteAsync(Administrator, WebsiteSlug, CancellationToken.None);
        var contact = await administration.CreateForContactAsync(Administrator, Bodyshop, CancellationToken.None);

        var same = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.ReplaceAsync(Administrator, website.Id, website.Version, WebsiteSlug, CancellationToken.None));
        var invalid = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.ReplaceAsync(Administrator, website.Id, website.Version, typedSlug: null, CancellationToken.None));
        var replaced = await administration.ReplaceAsync(Administrator, website.Id, website.Version, "photos-for-us", CancellationToken.None);
        // A contact's slug is always made by Pegasus, whatever is typed.
        var contactReplaced = await administration.ReplaceAsync(Administrator, contact.Id, contact.Version, "photos-for-them", CancellationToken.None);

        Assert.Equal(StandingPhotoLinkError.SlugTaken, same.Error);
        Assert.Equal(StandingPhotoLinkError.InvalidSlug, invalid.Error);
        Assert.Equal("photos-for-us", replaced.Slug);
        Assert.Equal(PhotoLinkToken.SlugLength, contactReplaced.Slug.Length);
        Assert.NotEqual(contact.Slug, contactReplaced.Slug);
    }

    [Fact]
    public async Task ARetiredLinkStopsAndTheContactMayHaveANewOne()
    {
        var store = new FakeStandingLinks();
        var clock = new FixedClock(Now);
        var administration = new StandingPhotoLinkAdministration(store, clock);
        var created = await administration.CreateForContactAsync(Administrator, Bodyshop, CancellationToken.None);
        // No capture store is given: a slug that resolved would never reach it.
        var access = new PhotoCaptureAccess(null!, store, clock);

        var stale = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.RetireAsync(Administrator, created.Id, created.Version + 1, CancellationToken.None));
        var retired = await administration.RetireAsync(Administrator, created.Id, created.Version, CancellationToken.None);
        var resumed = await administration.ResumeAsync(Administrator, created.Id, retired.Version, CancellationToken.None);
        var replacement = await administration.CreateForContactAsync(Administrator, Bodyshop, CancellationToken.None);

        Assert.Equal(StandingPhotoLinkError.StaleVersion, stale.Error);
        Assert.Equal(Now, retired.RetiredAtUtc);
        Assert.False(retired.StartsCaptures);
        Assert.False(resumed.IsActive);
        Assert.False((await access.ResolveSlugAsync(created.Slug, CancellationToken.None)).IsAvailable);
        Assert.NotEqual(created.Id, replacement.Id);
        Assert.NotEqual(created.Slug, replacement.Slug);
        Assert.True((await access.ResolveSlugAsync(replacement.Slug, CancellationToken.None)).IsAvailable);
    }

    [Fact]
    public async Task AnInactiveContactsLinkStartsNoCaptures()
    {
        var store = new FakeStandingLinks();
        var clock = new FixedClock(Now);
        var created = await new StandingPhotoLinkAdministration(store, clock)
            .CreateForContactAsync(Administrator, Bodyshop, CancellationToken.None);
        // No capture store is given: a link that started a capture would reach it.
        var access = new PhotoCaptureAccess(null!, store, clock);
        var mint = new MintFromStandingLink(null!, store, null!, clock);
        var form = new StandingCaptureForm("RY65 OFG", null, "Sam Taylor", null, null, PhotoCaptureDetails.Empty);
        var deactivated = created with { ContactActive = false };
        store.Links[0] = deactivated;

        Assert.True(deactivated.IsActive);
        Assert.False(deactivated.StartsCaptures);
        Assert.False((await access.ResolveSlugAsync(created.Slug, CancellationToken.None)).IsAvailable);
        var started = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(
            () => mint.StartAsync(deactivated, form, CancellationToken.None));
        var handedOn = await Assert.ThrowsAsync<PhotoCaptureRefusalException>(
            () => mint.HandoffAsync(deactivated, form, CancellationToken.None));
        Assert.Equal(PhotoCaptureRefusals.Unavailable, started.Code);
        Assert.Equal(PhotoCaptureRefusals.Unavailable, handedOn.Code);

        // Active again, the same address starts captures again.
        store.Links[0] = created;
        Assert.True((await access.ResolveSlugAsync(created.Slug, CancellationToken.None)).IsAvailable);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>The store as Core sees it: rows with versions, a stale version refused as the typed refusal.</summary>
    private sealed class FakeStandingLinks : IStandingPhotoLinkStore
    {
        public List<StandingPhotoLink> Links { get; } = [];

        public int Writes { get; private set; }

        public Task<StandingPhotoLink?> FindBySlugAsync(string slug, CancellationToken cancellationToken) =>
            Task.FromResult(Links.FirstOrDefault(link => link.Slug == slug));

        public Task<StandingPhotoLink?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Links.FirstOrDefault(link => link.Id == id));

        public Task<StandingPhotoLink?> FindForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
            Task.FromResult(Links.FirstOrDefault(link => link.OrganizationId == organizationId && link.RetiredAtUtc is null));

        public Task<StandingPhotoLink?> FindWebsiteAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Links.FirstOrDefault(link => link.Kind == StandingPhotoLinkKind.Website && link.RetiredAtUtc is null));

        public Task<StandingPhotoLink> CreateAsync(
            StandingPhotoLinkKind kind,
            Guid? organizationId,
            string slug,
            PhotoCaptureMode forwardedMode,
            bool liveCameraOnly,
            int linkLifetimeDays,
            bool originalsInBackground,
            ActionActor actor,
            DateTimeOffset createdAtUtc,
            CancellationToken cancellationToken)
        {
            Writes++;
            var link = new StandingPhotoLink(
                Guid.NewGuid(), kind, organizationId, organizationId is null ? null : "AMC Bodyshop", null, kind == StandingPhotoLinkKind.Contact,
                ContactActive: true, slug, forwardedMode, liveCameraOnly, linkLifetimeDays, originalsInBackground,
                PausedAtUtc: null, RetiredAtUtc: null, createdAtUtc, Version: 1);
            Links.Add(link);
            return Task.FromResult(link);
        }

        public Task<StandingPhotoLink> UpdateDefaultsAsync(
            Guid id,
            long expectedVersion,
            PhotoCaptureMode forwardedMode,
            bool liveCameraOnly,
            int linkLifetimeDays,
            bool originalsInBackground,
            CancellationToken cancellationToken) =>
            Update(id, expectedVersion, link => link with
            {
                ForwardedMode = forwardedMode,
                LiveCameraOnly = liveCameraOnly,
                LinkLifetimeDays = linkLifetimeDays,
                OriginalsInBackground = originalsInBackground
            });

        public Task<StandingPhotoLink> SetPausedAsync(Guid id, long expectedVersion, DateTimeOffset? pausedAtUtc, CancellationToken cancellationToken) =>
            Update(id, expectedVersion, link => link with { PausedAtUtc = pausedAtUtc });

        public Task<StandingPhotoLink> SetRetiredAsync(Guid id, long expectedVersion, DateTimeOffset retiredAtUtc, CancellationToken cancellationToken) =>
            Update(id, expectedVersion, link => link with { RetiredAtUtc = link.RetiredAtUtc ?? retiredAtUtc });

        public Task<StandingPhotoLink> ReplaceSlugAsync(Guid id, long expectedVersion, string newSlug, DateTimeOffset replacedAtUtc, CancellationToken cancellationToken) =>
            Update(id, expectedVersion, link => link with { Slug = newSlug });

        public Task<IReadOnlyList<StandingPhotoLinkAddress>> ListAddressesAsync(Guid standingLinkId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StandingPhotoLinkAddress>>([]);

        public Task<int> CountMintedSinceAsync(Guid standingLinkId, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
            Task.FromResult(0);

        private Task<StandingPhotoLink> Update(Guid id, long expectedVersion, Func<StandingPhotoLink, StandingPhotoLink> change)
        {
            var index = Links.FindIndex(link => link.Id == id);
            if (index < 0)
            {
                throw new StandingPhotoLinkException(StandingPhotoLinkError.NotFound);
            }

            if (Links[index].Version != expectedVersion)
            {
                throw new StandingPhotoLinkException(StandingPhotoLinkError.StaleVersion);
            }

            Writes++;
            Links[index] = change(Links[index]) with { Version = expectedVersion + 1 };
            return Task.FromResult(Links[index]);
        }
    }
}
