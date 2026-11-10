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

        var link = await administration.CreateForContactAsync(Administrator, Key(), Bodyshop, CancellationToken.None);

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
        await administration.CreateForContactAsync(Administrator, Key(), Bodyshop, CancellationToken.None);
        var website = await administration.CreateWebsiteAsync(Administrator, Key(), WebsiteSlug, CancellationToken.None);

        var second = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.CreateForContactAsync(Administrator, Key(), Bodyshop, CancellationToken.None));
        var secondWebsite = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.CreateWebsiteAsync(Administrator, Key(), "another-address", CancellationToken.None));

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
        var link = await administration.CreateForContactAsync(Administrator, Key(), Bodyshop, CancellationToken.None);
        var writes = store.Writes;

        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => administration.CreateForContactAsync(Engineer, Key(), Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => administration.CreateWebsiteAsync(Engineer, Key(), WebsiteSlug, CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => administration.PauseAsync(Engineer, Key(), link.Id, link.Version, CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => administration.ReplaceAsync(Engineer, Key(), link.Id, link.Version, typedSlug: null, CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => administration.RetireAsync(Engineer, Key(), link.Id, link.Version, CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => administration.UpdateDefaultsAsync(
                Engineer, Key(), link.Id, link.Version, PhotoCaptureMode.Quick, false, 3, false, CancellationToken.None));
        Assert.Equal(writes, store.Writes);
    }

    /// <summary>
    /// Every change hands the store who made it, the form's operation key and
    /// the time, which its history event records (FRD-04; operator,
    /// 10 November 2026; R846); a change with no key is refused before any write.
    /// </summary>
    [Fact]
    public async Task EveryChangeCarriesItsActorKeyAndTimeAndNeedsAKey()
    {
        var store = new FakeStandingLinks();
        var administration = new StandingPhotoLinkAdministration(store, new FixedClock(Now));
        const string createKey = "create-key";
        const string pauseKey = "pause-key";
        var link = await administration.CreateForContactAsync(Administrator, createKey, Bodyshop, CancellationToken.None);
        await administration.PauseAsync(Administrator, $"  {pauseKey}  ", link.Id, link.Version, CancellationToken.None);
        var writes = store.Writes;

        await Assert.ThrowsAsync<ArgumentException>(
            () => administration.ResumeAsync(Administrator, "  ", link.Id, link.Version + 1, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(
            () => administration.CreateWebsiteAsync(Administrator, new string('k', 101), WebsiteSlug, CancellationToken.None));

        Assert.Equal(writes, store.Writes);
        Assert.Collection(
            store.Changes,
            created => Assert.Equal(new StandingPhotoLinkChange(Administrator, createKey, Now), created),
            paused => Assert.Equal(new StandingPhotoLinkChange(Administrator, pauseKey, Now), paused));
    }

    [Fact]
    public async Task PauseResumeAndReplaceCarryTheVersionAndAStaleOneIsRefused()
    {
        var store = new FakeStandingLinks();
        var administration = new StandingPhotoLinkAdministration(store, new FixedClock(Now));
        var created = await administration.CreateForContactAsync(Administrator, Key(), Bodyshop, CancellationToken.None);

        var paused = await administration.PauseAsync(Administrator, Key(), created.Id, created.Version, CancellationToken.None);
        Assert.Equal(Now, paused.PausedAtUtc);
        Assert.False(paused.IsActive);
        Assert.Equal(2, paused.Version);

        var stale = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.ResumeAsync(Administrator, Key(), created.Id, created.Version, CancellationToken.None));
        Assert.Equal(StandingPhotoLinkError.StaleVersion, stale.Error);
        Assert.False(store.Links.Single().IsActive);

        var resumed = await administration.ResumeAsync(Administrator, Key(), created.Id, paused.Version, CancellationToken.None);
        Assert.True(resumed.IsActive);

        var replaced = await administration.ReplaceAsync(Administrator, Key(), created.Id, resumed.Version, typedSlug: null, CancellationToken.None);
        Assert.NotEqual(created.Slug, replaced.Slug);
        Assert.True(PhotoLinkToken.HasSlugShape(replaced.Slug));
        Assert.Equal(4, replaced.Version);
    }

    [Fact]
    public async Task DefaultsOutsideTheOfferedLifetimesAreRefusedBeforeAnyWrite()
    {
        var store = new FakeStandingLinks();
        var administration = new StandingPhotoLinkAdministration(store, new FixedClock(Now));
        var link = await administration.CreateForContactAsync(Administrator, Key(), Bodyshop, CancellationToken.None);
        var writes = store.Writes;

        var refused = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.UpdateDefaultsAsync(
                Administrator, Key(), link.Id, link.Version, PhotoCaptureMode.Quick, false, 5, false, CancellationToken.None));
        Assert.Equal(StandingPhotoLinkError.InvalidDefaults, refused.Error);
        Assert.Equal(writes, store.Writes);

        var updated = await administration.UpdateDefaultsAsync(
            Administrator, Key(), link.Id, link.Version, PhotoCaptureMode.Quick, false, 14, false, CancellationToken.None);
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

        var website = await administration.CreateWebsiteAsync(Administrator, Key(), " Collision-Photos ", CancellationToken.None);

        Assert.Equal(StandingPhotoLinkKind.Website, website.Kind);
        Assert.Equal(WebsiteSlug, website.Slug);
    }

    [Fact]
    public async Task AWebsiteSlugOfTheWrongShapeOrAlreadyInUseIsRefusedBeforeAnyWrite()
    {
        var store = new FakeStandingLinks();
        var administration = new StandingPhotoLinkAdministration(store, new FixedClock(Now));
        var contact = await administration.CreateForContactAsync(Administrator, Key(), Bodyshop, CancellationToken.None);
        var writes = store.Writes;

        var missing = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.CreateWebsiteAsync(Administrator, Key(), null, CancellationToken.None));
        var tooShort = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.CreateWebsiteAsync(Administrator, Key(), "photo", CancellationToken.None));
        var badCharacters = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.CreateWebsiteAsync(Administrator, Key(), "collision photos", CancellationToken.None));
        var taken = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.CreateWebsiteAsync(Administrator, Key(), contact.Slug, CancellationToken.None));

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
        var website = await administration.CreateWebsiteAsync(Administrator, Key(), WebsiteSlug, CancellationToken.None);
        var contact = await administration.CreateForContactAsync(Administrator, Key(), Bodyshop, CancellationToken.None);

        var same = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.ReplaceAsync(Administrator, Key(), website.Id, website.Version, WebsiteSlug, CancellationToken.None));
        var invalid = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.ReplaceAsync(Administrator, Key(), website.Id, website.Version, typedSlug: null, CancellationToken.None));
        var replaced = await administration.ReplaceAsync(Administrator, Key(), website.Id, website.Version, "photos-for-us", CancellationToken.None);
        // A contact's slug is always made by Pegasus, whatever is typed.
        var contactReplaced = await administration.ReplaceAsync(Administrator, Key(), contact.Id, contact.Version, "photos-for-them", CancellationToken.None);

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
        var created = await administration.CreateForContactAsync(Administrator, Key(), Bodyshop, CancellationToken.None);
        // No capture store is given: a slug that resolved would never reach it.
        var access = new PhotoCaptureAccess(null!, store, clock);

        var stale = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.RetireAsync(Administrator, Key(), created.Id, created.Version + 1, CancellationToken.None));
        var retired = await administration.RetireAsync(Administrator, Key(), created.Id, created.Version, CancellationToken.None);
        var resumed = await administration.ResumeAsync(Administrator, Key(), created.Id, retired.Version, CancellationToken.None);
        var replacement = await administration.CreateForContactAsync(Administrator, Key(), Bodyshop, CancellationToken.None);

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
    public async Task ASavedAddressIsAddedChangedAndRemovedUnderTheLinksVersion()
    {
        var store = new FakeStandingLinks();
        var administration = new StandingPhotoLinkAdministration(store, new FixedClock(Now));
        var link = await administration.CreateForContactAsync(Administrator, Key(), Bodyshop, CancellationToken.None);

        var added = await administration.AddAddressAsync(
            Administrator, Key(), link.Id, link.Version, " Back yard ", " 1 High Street, Leeds ", CancellationToken.None);
        var saved = Assert.Single((await store.GetAddressBookAsync(link.Id, CancellationToken.None)).Saved);
        Assert.Equal("Back yard", saved.Name);
        Assert.Equal("1 High Street, Leeds", saved.Address);
        Assert.Equal(link.Version + 1, added.Version);

        var changed = await administration.ChangeAddressAsync(
            Administrator, Key(), link.Id, added.Version, saved.Id, "Front yard", "2 High Street, Leeds", CancellationToken.None);
        Assert.Equal("Front yard", Assert.Single((await store.GetAddressBookAsync(link.Id, CancellationToken.None)).Saved).Name);

        var stale = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.RemoveAddressAsync(Administrator, Key(), link.Id, added.Version, saved.Id, CancellationToken.None));
        Assert.Equal(StandingPhotoLinkError.StaleVersion, stale.Error);
        Assert.Single((await store.GetAddressBookAsync(link.Id, CancellationToken.None)).Saved);

        await administration.RemoveAddressAsync(Administrator, Key(), link.Id, changed.Version, saved.Id, CancellationToken.None);
        Assert.True((await store.GetAddressBookAsync(link.Id, CancellationToken.None)).IsEmpty);
    }

    [Fact]
    public async Task ASavedAddressNeedsANameAnAddressAContactsLinkAndTheContactsRight()
    {
        var store = new FakeStandingLinks();
        var administration = new StandingPhotoLinkAdministration(store, new FixedClock(Now));
        var link = await administration.CreateForContactAsync(Administrator, Key(), Bodyshop, CancellationToken.None);
        var website = await administration.CreateWebsiteAsync(Administrator, Key(), WebsiteSlug, CancellationToken.None);
        var writes = store.Writes;

        var noName = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.AddAddressAsync(Administrator, Key(), link.Id, link.Version, "  ", "1 High Street", CancellationToken.None));
        var noAddress = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.AddAddressAsync(Administrator, Key(), link.Id, link.Version, "Back yard", null, CancellationToken.None));
        var onTheWebsite = await Assert.ThrowsAsync<StandingPhotoLinkException>(
            () => administration.AddAddressAsync(Administrator, Key(), website.Id, website.Version, "Back yard", "1 High Street", CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => administration.AddAddressAsync(Engineer, Key(), link.Id, link.Version, "Back yard", "1 High Street", CancellationToken.None));

        Assert.Equal(StandingPhotoLinkError.InvalidAddress, noName.Error);
        Assert.Equal(StandingPhotoLinkError.InvalidAddress, noAddress.Error);
        Assert.Equal(StandingPhotoLinkError.NotFound, onTheWebsite.Error);
        Assert.Equal(writes, store.Writes);
        Assert.Empty(store.Addresses);
    }

    [Fact]
    public async Task AnInactiveContactsLinkStartsNoCaptures()
    {
        var store = new FakeStandingLinks();
        var clock = new FixedClock(Now);
        var created = await new StandingPhotoLinkAdministration(store, clock)
            .CreateForContactAsync(Administrator, Key(), Bodyshop, CancellationToken.None);
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

    private static string Key() => Guid.NewGuid().ToString("N");

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>The store as Core sees it: rows with versions, a stale version refused as the typed refusal.</summary>
    private sealed class FakeStandingLinks : IStandingPhotoLinkStore
    {
        public List<StandingPhotoLink> Links { get; } = [];

        public int Writes { get; private set; }

        /// <summary>Who made each write, under which key and when, as the store's history event records it (R846).</summary>
        public List<StandingPhotoLinkChange> Changes { get; } = [];

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
            StandingPhotoLinkChange change,
            CancellationToken cancellationToken)
        {
            Writes++;
            Changes.Add(change);
            var link = new StandingPhotoLink(
                Guid.NewGuid(), kind, organizationId, organizationId is null ? null : "AMC Bodyshop", null, kind == StandingPhotoLinkKind.Contact,
                ContactActive: true, slug, forwardedMode, liveCameraOnly, linkLifetimeDays, originalsInBackground,
                PausedAtUtc: null, RetiredAtUtc: null, change.AtUtc, Version: 1);
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
            StandingPhotoLinkChange change,
            CancellationToken cancellationToken) =>
            Update(id, expectedVersion, change, link => link with
            {
                ForwardedMode = forwardedMode,
                LiveCameraOnly = liveCameraOnly,
                LinkLifetimeDays = linkLifetimeDays,
                OriginalsInBackground = originalsInBackground
            });

        public Task<StandingPhotoLink> SetPausedAsync(Guid id, long expectedVersion, bool paused, StandingPhotoLinkChange change, CancellationToken cancellationToken) =>
            Update(id, expectedVersion, change, link => link with { PausedAtUtc = paused ? change.AtUtc : null });

        public Task<StandingPhotoLink> SetRetiredAsync(Guid id, long expectedVersion, StandingPhotoLinkChange change, CancellationToken cancellationToken) =>
            Update(id, expectedVersion, change, link => link with { RetiredAtUtc = link.RetiredAtUtc ?? change.AtUtc });

        public Task<StandingPhotoLink> ReplaceSlugAsync(Guid id, long expectedVersion, string newSlug, StandingPhotoLinkChange change, CancellationToken cancellationToken) =>
            Update(id, expectedVersion, change, link => link with { Slug = newSlug });

        public List<(Guid LinkId, SavedStandingPhotoLinkAddress Address)> Addresses { get; } = [];

        public Task<IReadOnlyList<StandingPhotoLinkAddress>> ListAddressesAsync(Guid standingLinkId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StandingPhotoLinkAddress>>(
                [.. Addresses.Where(item => item.LinkId == standingLinkId).Select(item => new StandingPhotoLinkAddress(item.Address.Name, item.Address.Address))]);

        public Task<StandingPhotoLinkAddressBook> GetAddressBookAsync(Guid standingLinkId, CancellationToken cancellationToken) =>
            Task.FromResult(new StandingPhotoLinkAddressBook(
                [],
                [.. Addresses.Where(item => item.LinkId == standingLinkId).Select(item => item.Address)]));

        public Task<StandingPhotoLink> AddAddressAsync(
            Guid id,
            long expectedVersion,
            string name,
            string address,
            StandingPhotoLinkChange change,
            CancellationToken cancellationToken) =>
            Update(id, expectedVersion, change, link =>
            {
                Addresses.Add((id, new SavedStandingPhotoLinkAddress(Guid.NewGuid(), name, address)));
                return link;
            });

        public Task<StandingPhotoLink> ChangeAddressAsync(
            Guid id,
            long expectedVersion,
            Guid addressId,
            string name,
            string address,
            StandingPhotoLinkChange change,
            CancellationToken cancellationToken) =>
            Update(id, expectedVersion, change, link =>
            {
                Addresses[SavedIndex(id, addressId)] = (id, new SavedStandingPhotoLinkAddress(addressId, name, address));
                return link;
            });

        public Task<StandingPhotoLink> RemoveAddressAsync(
            Guid id,
            long expectedVersion,
            Guid addressId,
            StandingPhotoLinkChange change,
            CancellationToken cancellationToken) =>
            Update(id, expectedVersion, change, link =>
            {
                Addresses.RemoveAt(SavedIndex(id, addressId));
                return link;
            });

        private int SavedIndex(Guid linkId, Guid addressId)
        {
            var index = Addresses.FindIndex(item => item.LinkId == linkId && item.Address.Id == addressId);
            return index >= 0 ? index : throw new StandingPhotoLinkException(StandingPhotoLinkError.NotFound);
        }

        public Task<int> CountMintedSinceAsync(Guid standingLinkId, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
            Task.FromResult(0);

        private Task<StandingPhotoLink> Update(
            Guid id,
            long expectedVersion,
            StandingPhotoLinkChange recorded,
            Func<StandingPhotoLink, StandingPhotoLink> change)
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
            Changes.Add(recorded);
            Links[index] = change(Links[index]) with { Version = expectedVersion + 1 };
            return Task.FromResult(Links[index]);
        }
    }
}
