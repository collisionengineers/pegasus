using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The standing links (FRD-28): a contact's, read with its contact's name,
/// Principal and whether it is a repairer, or the one website link. A write
/// names the version it read; a changed row is refused as
/// <see cref="StandingPhotoLinkError.StaleVersion"/>, and a second link for a
/// contact or a second website link, which the unique indexes stop, as
/// <see cref="StandingPhotoLinkError.AlreadyExists"/>, so the pages render
/// the refusal rather than a fault (<see cref="StandingPhotoLinkAdministration"/>).
/// Every write saves, in the same save, the one <c>ActionHistory</c> event the
/// change writes (<see cref="StandingPhotoLinkActs"/>; FRD-04; operator,
/// 10 November 2026; R846), its operation key as the correlation and its
/// before and after holding only the fields it changed.
/// </summary>
public sealed class EfStandingPhotoLinkStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IStandingPhotoLinkStore
{
    // The stored contact type codes (EfContactDirectoryAdministration).
    private const string PrincipalRole = "principal";
    private const string RepairerRole = "repairer";

    /// <summary>
    /// Where a contact's link's history is filed: under the contact, the
    /// area and subject its other history uses (EfContactDirectoryAdministration).
    /// </summary>
    internal const string ContactHistoryArea = "contact";

    /// <summary>The website link has no contact; its history sits under its own area, "Website photo link" (R846).</summary>
    internal const string WebsiteHistoryArea = "website_photo_link";

    public Task<StandingPhotoLink?> FindBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        return ReadOneAsync(link => link.Slug == slug, cancellationToken);
    }

    public Task<StandingPhotoLink?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ReadOneAsync(link => link.Id == id, cancellationToken);

    public Task<StandingPhotoLink?> FindForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken) =>
        ReadOneAsync(link => link.OrganizationId == organizationId && link.RetiredAtUtc == null, cancellationToken);

    public Task<StandingPhotoLink?> FindWebsiteAsync(CancellationToken cancellationToken)
    {
        var website = PhotoCaptureCodes.ToCode(StandingPhotoLinkKind.Website);
        return ReadOneAsync(link => link.Kind == website && link.RetiredAtUtc == null, cancellationToken);
    }

    public async Task<StandingPhotoLink> CreateAsync(
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
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        RequireChange(change);
        var entity = new StandingPhotoLinkEntity
        {
            Id = Guid.NewGuid(),
            Kind = PhotoCaptureCodes.ToCode(kind),
            OrganizationId = organizationId,
            Slug = slug,
            ForwardedMode = PhotoCaptureCodes.ToCode(forwardedMode),
            LiveCameraOnly = liveCameraOnly,
            LinkLifetimeDays = linkLifetimeDays,
            OriginalsInBackground = originalsInBackground,
            CreatedAtUtc = change.AtUtc,
            CreatedByKind = change.Actor.Kind.ToString(),
            CreatedBySubjectId = change.Actor.SubjectId,
            Version = 1
        };
        await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            // A key already recorded made the link it was sent for: the link
            // is there, whoever made it, and nothing more is written.
            if (await RecordedAsync(context, change.OperationKey, cancellationToken) is not null)
            {
                throw new StandingPhotoLinkException(StandingPhotoLinkError.AlreadyExists);
            }

            context.StandingPhotoLinks.Add(entity);
            var created = new FieldChanges();
            created.Set<string?>("slug", null, entity.Slug);
            created.Set<string?>("forwardedMode", null, entity.ForwardedMode);
            created.Set<bool?>("liveCameraOnly", null, entity.LiveCameraOnly);
            created.Set<int?>("linkLifetimeDays", null, entity.LinkLifetimeDays);
            created.Set<bool?>("originalsInBackground", null, entity.OriginalsInBackground);
            AddHistory(context, entity, StandingPhotoLinkActs.Created, change, created);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsSlugCollision(exception))
            {
                // The website's typed slug, taken between Core's check and this write.
                throw new StandingPhotoLinkException(StandingPhotoLinkError.SlugTaken);
            }
            catch (DbUpdateException exception) when (IsUniqueIndexCollision(exception))
            {
                throw new StandingPhotoLinkException(StandingPhotoLinkError.AlreadyExists);
            }
        }

        return await GetAsync(entity.Id, cancellationToken)
            ?? throw new InvalidOperationException("The standing photo link was not read back.");
    }

    private static bool IsUniqueIndexCollision(DbUpdateException exception) =>
        exception.GetBaseException() is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 };

    private static bool IsSlugCollision(DbUpdateException exception) =>
        exception.GetBaseException() is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 } sqlException
        && sqlException.Message.Contains("IX_StandingPhotoLinks_Slug", StringComparison.OrdinalIgnoreCase);

    public Task<StandingPhotoLink> UpdateDefaultsAsync(
        Guid id,
        long expectedVersion,
        PhotoCaptureMode forwardedMode,
        bool liveCameraOnly,
        int linkLifetimeDays,
        bool originalsInBackground,
        StandingPhotoLinkChange change,
        CancellationToken cancellationToken) =>
        UpdateAsync(id, expectedVersion, StandingPhotoLinkActs.DefaultsSaved, change, entity =>
        {
            var changes = new FieldChanges();
            var mode = PhotoCaptureCodes.ToCode(forwardedMode);
            changes.Set("forwardedMode", entity.ForwardedMode, mode);
            changes.Set("liveCameraOnly", entity.LiveCameraOnly, liveCameraOnly);
            changes.Set("linkLifetimeDays", entity.LinkLifetimeDays, linkLifetimeDays);
            changes.Set("originalsInBackground", entity.OriginalsInBackground, originalsInBackground);
            entity.ForwardedMode = mode;
            entity.LiveCameraOnly = liveCameraOnly;
            entity.LinkLifetimeDays = linkLifetimeDays;
            entity.OriginalsInBackground = originalsInBackground;
            return changes;
        }, cancellationToken);

    public Task<StandingPhotoLink> SetPausedAsync(
        Guid id,
        long expectedVersion,
        bool paused,
        StandingPhotoLinkChange change,
        CancellationToken cancellationToken) =>
        UpdateAsync(
            id,
            expectedVersion,
            paused ? StandingPhotoLinkActs.Paused : StandingPhotoLinkActs.Resumed,
            change,
            entity =>
            {
                var changes = new FieldChanges();
                DateTimeOffset? pausedAtUtc = paused ? change.AtUtc : null;
                changes.Set("pausedAtUtc", entity.PausedAtUtc, pausedAtUtc);
                entity.PausedAtUtc = pausedAtUtc;
                return changes;
            },
            cancellationToken);

    public Task<StandingPhotoLink> SetRetiredAsync(
        Guid id,
        long expectedVersion,
        StandingPhotoLinkChange change,
        CancellationToken cancellationToken) =>
        UpdateAsync(id, expectedVersion, StandingPhotoLinkActs.Retired, change, entity =>
        {
            var changes = new FieldChanges();
            var retiredAtUtc = entity.RetiredAtUtc ?? change.AtUtc;
            changes.Set("retiredAtUtc", entity.RetiredAtUtc, (DateTimeOffset?)retiredAtUtc);
            entity.RetiredAtUtc = retiredAtUtc;
            return changes;
        }, cancellationToken);

    public Task<StandingPhotoLink> ReplaceSlugAsync(
        Guid id,
        long expectedVersion,
        string newSlug,
        StandingPhotoLinkChange change,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newSlug);
        // The old slug is simply no longer stored, so it resolves like an unknown one.
        return UpdateAsync(id, expectedVersion, StandingPhotoLinkActs.Replaced, change, entity =>
        {
            var changes = new FieldChanges();
            changes.Set("slug", entity.Slug, newSlug);
            entity.Slug = newSlug;
            return changes;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<StandingPhotoLinkAddress>> ListAddressesAsync(
        Guid standingLinkId,
        CancellationToken cancellationToken)
    {
        // The form offers the contact's addresses first, then the tab's own,
        // each address once (R815).
        var book = await GetAddressBookAsync(standingLinkId, cancellationToken);
        var addresses = new List<StandingPhotoLinkAddress>(book.FromContact);
        foreach (var saved in book.Saved)
        {
            Add(addresses, saved.Name, saved.Address, postcode: null);
        }

        return addresses;
    }

    public async Task<StandingPhotoLinkAddressBook> GetAddressBookAsync(
        Guid standingLinkId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var fromContact = await FromContactAsync(context, standingLinkId, cancellationToken);
        IReadOnlyList<SavedStandingPhotoLinkAddress> saved = fromContact is null
            ? []
            : await context.StandingPhotoLinkAddresses.AsNoTracking()
                .Where(item => item.StandingPhotoLinkId == standingLinkId)
                .OrderBy(item => item.CreatedAtUtc)
                .ThenBy(item => item.Id)
                .Select(item => new SavedStandingPhotoLinkAddress(item.Id, item.Name, item.Address))
                .ToListAsync(cancellationToken);
        return new(fromContact ?? [], saved);
    }

    public Task<StandingPhotoLink> AddAddressAsync(
        Guid id,
        long expectedVersion,
        string name,
        string address,
        StandingPhotoLinkChange change,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        return UpdateAsync(id, expectedVersion, StandingPhotoLinkActs.AddressAdded, change, (context, _, _) =>
        {
            context.StandingPhotoLinkAddresses.Add(new StandingPhotoLinkAddressEntity
            {
                Id = Guid.NewGuid(),
                StandingPhotoLinkId = id,
                Name = name,
                Address = address,
                CreatedAtUtc = change.AtUtc
            });
            var changes = new FieldChanges();
            changes.Set<string?>("name", null, name);
            changes.Set<string?>("address", null, address);
            return Task.FromResult(changes);
        }, cancellationToken);
    }

    public Task<StandingPhotoLink> ChangeAddressAsync(
        Guid id,
        long expectedVersion,
        Guid addressId,
        string name,
        string address,
        StandingPhotoLinkChange change,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        return UpdateAsync(id, expectedVersion, StandingPhotoLinkActs.AddressChanged, change, async (context, _, token) =>
        {
            var saved = await SavedAddressAsync(context, id, addressId, token);
            var changes = new FieldChanges();
            changes.Set("name", saved.Name, name);
            changes.Set("address", saved.Address, address);
            saved.Name = name;
            saved.Address = address;
            return changes;
        }, cancellationToken);
    }

    public Task<StandingPhotoLink> RemoveAddressAsync(
        Guid id,
        long expectedVersion,
        Guid addressId,
        StandingPhotoLinkChange change,
        CancellationToken cancellationToken) =>
        UpdateAsync(id, expectedVersion, StandingPhotoLinkActs.AddressRemoved, change, async (context, _, token) =>
        {
            var saved = await SavedAddressAsync(context, id, addressId, token);
            var changes = new FieldChanges();
            changes.Set<string?>("name", saved.Name, null);
            changes.Set<string?>("address", saved.Address, null);
            context.StandingPhotoLinkAddresses.Remove(saved);
            return changes;
        }, cancellationToken);

    // An address of this link saved on its tab; one taken from the contact,
    // or another link's, is not found.
    private static async Task<StandingPhotoLinkAddressEntity> SavedAddressAsync(
        PegasusDbContext context,
        Guid standingLinkId,
        Guid addressId,
        CancellationToken cancellationToken) =>
        await context.StandingPhotoLinkAddresses
            .SingleOrDefaultAsync(item => item.Id == addressId && item.StandingPhotoLinkId == standingLinkId, cancellationToken)
        ?? throw new StandingPhotoLinkException(StandingPhotoLinkError.NotFound);

    /// <summary>
    /// The addresses taken from the link's contact: its own, or a Principal's
    /// inspection locations. Null for the website link, or an unknown one,
    /// which has no contact and keeps no addresses.
    /// </summary>
    private static async Task<List<StandingPhotoLinkAddress>?> FromContactAsync(
        PegasusDbContext context,
        Guid standingLinkId,
        CancellationToken cancellationToken)
    {
        var organization = await (
            from link in context.StandingPhotoLinks.AsNoTracking()
            join contact in context.Organizations.AsNoTracking() on link.OrganizationId equals (Guid?)contact.Id
            where link.Id == standingLinkId
            select new { contact.Id, contact.Name, contact.Role, contact.Address, contact.Postcode })
            .SingleOrDefaultAsync(cancellationToken);
        if (organization is null)
        {
            return null;
        }

        var addresses = new List<StandingPhotoLinkAddress>();
        if (!string.Equals(organization.Role, PrincipalRole, StringComparison.Ordinal))
        {
            Add(addresses, organization.Name, organization.Address, organization.Postcode);
            return addresses;
        }

        // A Principal's inspection locations, as its Cases offer them: its
        // default location first, then the reviewed locations linked to it.
        var principal = await PrincipalOf(context, organization.Id)
            .Select(item => new
            {
                item.Id,
                item.DefaultInspectionLocationLabel,
                item.DefaultInspectionAddress,
                item.DefaultInspectionPostcode
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (principal is null)
        {
            return addresses;
        }

        Add(
            addresses,
            principal.DefaultInspectionLocationLabel ?? organization.Name,
            principal.DefaultInspectionAddress,
            principal.DefaultInspectionPostcode);
        var linked = await (
            from link in context.InspectionLocationPrincipalLinks.AsNoTracking()
            join location in context.InspectionLocations.AsNoTracking() on link.InspectionLocationId equals location.Id
            where link.PrincipalId == principal.Id && location.Active
            orderby location.Name
            select new { location.Name, location.Address, location.Postcode })
            .ToListAsync(cancellationToken);
        foreach (var location in linked)
        {
            Add(addresses, location.Name, location.Address, location.Postcode);
        }

        return addresses;
    }

    public async Task<int> CountMintedSinceAsync(Guid standingLinkId, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.PhotoCaptures.CountAsync(
            capture => capture.StandingLinkId == standingLinkId && capture.CreatedAtUtc >= sinceUtc,
            cancellationToken);
    }

    private static void Add(List<StandingPhotoLinkAddress> addresses, string name, string? address, string? postcode)
    {
        // An address that already ends with its postcode is not given it twice.
        var street = address?.Trim();
        var code = postcode?.Trim();
        if (!string.IsNullOrEmpty(street) && !string.IsNullOrEmpty(code)
            && street.EndsWith(code, StringComparison.OrdinalIgnoreCase))
        {
            code = null;
        }

        var parts = new[] { street, code }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!)
            .ToArray();
        if (parts.Length == 0)
        {
            return;
        }

        var text = string.Join(", ", parts);
        if (addresses.Exists(existing => string.Equals(existing.Address, text, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        addresses.Add(new(name, text));
    }

    /// <summary>The Principal that belongs to the contact: its active one, else the newest of its lineage.</summary>
    internal static IQueryable<PrincipalEntity> PrincipalOf(PegasusDbContext context, Guid organizationId) =>
        context.Principals.AsNoTracking()
            .Where(item => item.OrganizationId == organizationId)
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.SuccessorId == null ? 0 : 1)
            .ThenBy(item => item.Id);

    private async Task<StandingPhotoLink?> ReadOneAsync(
        Expression<Func<StandingPhotoLinkEntity, bool>> predicate,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await ReadOneAsync(context, predicate, cancellationToken);
    }

    internal static async Task<StandingPhotoLink?> ReadOneAsync(
        PegasusDbContext context,
        Expression<Func<StandingPhotoLinkEntity, bool>> predicate,
        CancellationToken cancellationToken)
    {
        var row = await (
            from link in context.StandingPhotoLinks.AsNoTracking().Where(predicate)
            join contact in context.Organizations.AsNoTracking() on link.OrganizationId equals (Guid?)contact.Id into contacts
            from contact in contacts.DefaultIfEmpty()
            select new
            {
                Link = link,
                OrganizationName = contact == null ? null : contact.Name,
                Role = contact == null ? null : contact.Role,
                // The website link has no contact, so it is never stopped by one (R796).
                ContactActive = contact == null || contact.Active,
                PrincipalId = context.Principals
                    .Where(item => (Guid?)item.OrganizationId == link.OrganizationId)
                    .OrderByDescending(item => item.IsActive)
                    .ThenBy(item => item.SuccessorId == null ? 0 : 1)
                    .ThenBy(item => item.Id)
                    .Select(item => (Guid?)item.Id)
                    .FirstOrDefault()
            })
            .OrderByDescending(item => item.Link.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : Map(
                row.Link,
                row.OrganizationName,
                row.PrincipalId,
                string.Equals(row.Role, RepairerRole, StringComparison.Ordinal),
                row.ContactActive);
    }

    private Task<StandingPhotoLink> UpdateAsync(
        Guid id,
        long expectedVersion,
        string act,
        StandingPhotoLinkChange change,
        Func<StandingPhotoLinkEntity, FieldChanges> apply,
        CancellationToken cancellationToken) =>
        UpdateAsync(
            id,
            expectedVersion,
            act,
            change,
            (_, entity, _) => Task.FromResult(apply(entity)),
            cancellationToken);

    /// <summary>
    /// One versioned write of the link row, of any rows of its own the change
    /// makes in the same save (its saved addresses), and of the change's
    /// action history event (R846). The operation key is the event's
    /// correlation, so a key already recorded for this change of this link
    /// changes nothing and answers the link as it is, before the version is
    /// looked at; one recorded for anything else is refused, as the photo
    /// link's own acts refuse it (EfPhotoCaptureStore).
    /// </summary>
    private Task<StandingPhotoLink> UpdateAsync(
        Guid id,
        long expectedVersion,
        string act,
        StandingPhotoLinkChange change,
        Func<PegasusDbContext, StandingPhotoLinkEntity, CancellationToken, Task<FieldChanges>> apply,
        CancellationToken cancellationToken)
    {
        RequireChange(change);
        return StoreConcurrencyRetry.ExecuteAsync(
            async token =>
            {
                await using var context = await contextFactory.CreateDbContextAsync(token);
                var entity = await context.StandingPhotoLinks
                    .SingleOrDefaultAsync(item => item.Id == id, token)
                    ?? throw new StandingPhotoLinkException(StandingPhotoLinkError.NotFound);
                var recorded = await RecordedAsync(context, change.OperationKey, token);
                if (recorded is not null)
                {
                    var (area, subject) = HistorySubject(entity);
                    if (!string.Equals(recorded.AggregateType, area, StringComparison.Ordinal)
                        || !string.Equals(recorded.AggregateId, subject, StringComparison.Ordinal)
                        || !string.Equals(recorded.EventKind, act, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("The operation key was already used for a different standing photo link change.");
                    }

                    return await ReadOneAsync(context, item => item.Id == id, token)
                        ?? throw new InvalidOperationException("The standing photo link was not read back.");
                }

                if (entity.Version != expectedVersion)
                {
                    throw new StandingPhotoLinkException(StandingPhotoLinkError.StaleVersion);
                }

                var changes = await apply(context, entity, token);
                entity.Version++;
                AddHistory(context, entity, act, change, changes);
                try
                {
                    await context.SaveChangesAsync(token);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // The row moved between the read above and this write.
                    throw new StandingPhotoLinkException(StandingPhotoLinkError.StaleVersion);
                }
                catch (DbUpdateException exception) when (IsSlugCollision(exception))
                {
                    // A typed slug taken between Core's check and this write;
                    // retrying the same slug could only collide again.
                    throw new StandingPhotoLinkException(StandingPhotoLinkError.SlugTaken);
                }

                return await ReadOneAsync(context, item => item.Id == id, token)
                    ?? throw new InvalidOperationException("The standing photo link was not read back.");
            },
            cancellationToken,
            StoreConcurrencyRetry.IsDeadlockOrKeyCollision);
    }

    private static void RequireChange(StandingPhotoLinkChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentNullException.ThrowIfNull(change.Actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(change.OperationKey);
    }

    /// <summary>The standing link history event already recorded under the operation key, if any.</summary>
    private static Task<ActionHistoryEntity?> RecordedAsync(
        PegasusDbContext context,
        string operationKey,
        CancellationToken cancellationToken) =>
        context.ActionHistory.AsNoTracking()
            .Where(item => (item.AggregateType == ContactHistoryArea || item.AggregateType == WebsiteHistoryArea)
                && item.CorrelationId == operationKey)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Where the link's history is filed: its contact, or the website link itself.</summary>
    private static (string Area, string Subject) HistorySubject(StandingPhotoLinkEntity entity) =>
        entity.OrganizationId is { } organizationId
            ? (ContactHistoryArea, organizationId.ToString("D"))
            : (WebsiteHistoryArea, entity.Id.ToString("D"));

    private static void AddHistory(
        PegasusDbContext context,
        StandingPhotoLinkEntity entity,
        string act,
        StandingPhotoLinkChange change,
        FieldChanges changes)
    {
        var (area, subject) = HistorySubject(entity);
        context.ActionHistory.Add(DocumentActionHistory.Succeeded(
            area,
            subject,
            act,
            change.Actor,
            change.AtUtc,
            change.OperationKey,
            beforeJson: changes.BeforeJson,
            afterJson: changes.AfterJson));
    }

    /// <summary>
    /// The fields a change changed, each with its value before and after;
    /// a field it left as it was is in neither, and a side with no value
    /// (nothing before a creation, nothing after a removal) is null.
    /// </summary>
    private sealed class FieldChanges
    {
        private readonly Dictionary<string, object?> _before = new(StringComparer.Ordinal);
        private readonly Dictionary<string, object?> _after = new(StringComparer.Ordinal);

        public void Set<T>(string field, T before, T after)
        {
            if (EqualityComparer<T>.Default.Equals(before, after))
            {
                return;
            }

            if (before is not null)
            {
                _before[field] = before;
            }

            if (after is not null)
            {
                _after[field] = after;
            }
        }

        public string? BeforeJson => _before.Count == 0 ? null : DocumentActionHistory.Serialize(_before);

        public string? AfterJson => _after.Count == 0 ? null : DocumentActionHistory.Serialize(_after);
    }

    internal static StandingPhotoLink Map(
        StandingPhotoLinkEntity entity,
        string? organizationName,
        Guid? principalId,
        bool isRepairer,
        bool contactActive) => new(
        entity.Id,
        PhotoCaptureCodes.ParseStandingLinkKind(entity.Kind),
        entity.OrganizationId,
        organizationName,
        principalId,
        isRepairer,
        contactActive,
        entity.Slug,
        PhotoCaptureCodes.ParseMode(entity.ForwardedMode),
        entity.LiveCameraOnly,
        entity.LinkLifetimeDays,
        entity.OriginalsInBackground,
        entity.PausedAtUtc,
        entity.RetiredAtUtc,
        entity.CreatedAtUtc,
        entity.Version);
}
