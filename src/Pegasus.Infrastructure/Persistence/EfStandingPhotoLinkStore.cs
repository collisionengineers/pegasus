using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Identity;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// The standing links (FRD-28): a contact's, read with its contact's name,
/// Principal and whether it is a repairer, or the one website link. A write
/// names the version it read; a changed row is a concurrency failure.
/// </summary>
public sealed class EfStandingPhotoLinkStore(
    IDbContextFactory<PegasusDbContext> contextFactory) : IStandingPhotoLinkStore
{
    // The stored contact type codes (EfContactDirectoryAdministration).
    private const string PrincipalRole = "principal";
    private const string RepairerRole = "repairer";

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
        ActionActor actor,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentNullException.ThrowIfNull(actor);
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
            CreatedAtUtc = createdAtUtc,
            CreatedByKind = actor.Kind.ToString(),
            CreatedBySubjectId = actor.SubjectId,
            Version = 1
        };
        await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            context.StandingPhotoLinks.Add(entity);
            await context.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(entity.Id, cancellationToken)
            ?? throw new InvalidOperationException("The standing photo link was not read back.");
    }

    public Task<StandingPhotoLink> UpdateDefaultsAsync(
        Guid id,
        long expectedVersion,
        PhotoCaptureMode forwardedMode,
        bool liveCameraOnly,
        int linkLifetimeDays,
        bool originalsInBackground,
        CancellationToken cancellationToken) =>
        UpdateAsync(id, expectedVersion, entity =>
        {
            entity.ForwardedMode = PhotoCaptureCodes.ToCode(forwardedMode);
            entity.LiveCameraOnly = liveCameraOnly;
            entity.LinkLifetimeDays = linkLifetimeDays;
            entity.OriginalsInBackground = originalsInBackground;
        }, cancellationToken);

    public Task<StandingPhotoLink> SetPausedAsync(
        Guid id,
        long expectedVersion,
        DateTimeOffset? pausedAtUtc,
        CancellationToken cancellationToken) =>
        UpdateAsync(id, expectedVersion, entity => entity.PausedAtUtc = pausedAtUtc, cancellationToken);

    public Task<StandingPhotoLink> ReplaceSlugAsync(
        Guid id,
        long expectedVersion,
        string newSlug,
        DateTimeOffset replacedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newSlug);
        // The old slug is simply no longer stored, so it resolves like an unknown one.
        return UpdateAsync(id, expectedVersion, entity => entity.Slug = newSlug, cancellationToken);
    }

    public async Task<IReadOnlyList<StandingPhotoLinkAddress>> ListAddressesAsync(
        Guid standingLinkId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var organization = await (
            from link in context.StandingPhotoLinks.AsNoTracking()
            join contact in context.Organizations.AsNoTracking() on link.OrganizationId equals (Guid?)contact.Id
            where link.Id == standingLinkId
            select new { contact.Id, contact.Name, contact.Role, contact.Address, contact.Postcode })
            .SingleOrDefaultAsync(cancellationToken);
        if (organization is null)
        {
            // The website link, or an unknown one: no saved addresses.
            return [];
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
        var parts = new[] { address, postcode }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim())
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
            : Map(row.Link, row.OrganizationName, row.PrincipalId, string.Equals(row.Role, RepairerRole, StringComparison.Ordinal));
    }

    private Task<StandingPhotoLink> UpdateAsync(
        Guid id,
        long expectedVersion,
        Action<StandingPhotoLinkEntity> change,
        CancellationToken cancellationToken) =>
        StoreConcurrencyRetry.ExecuteAsync(
            async token =>
            {
                await using var context = await contextFactory.CreateDbContextAsync(token);
                var entity = await context.StandingPhotoLinks
                    .SingleOrDefaultAsync(item => item.Id == id, token)
                    ?? throw new KeyNotFoundException("The standing photo link was not found.");
                if (entity.Version != expectedVersion)
                {
                    throw new DbUpdateConcurrencyException("The standing photo link changed since it was read.");
                }

                change(entity);
                entity.Version++;
                await context.SaveChangesAsync(token);
                return await ReadOneAsync(context, item => item.Id == id, token)
                    ?? throw new InvalidOperationException("The standing photo link was not read back.");
            },
            cancellationToken,
            StoreConcurrencyRetry.IsDeadlockOrKeyCollision);

    internal static StandingPhotoLink Map(
        StandingPhotoLinkEntity entity,
        string? organizationName,
        Guid? principalId,
        bool isRepairer) => new(
        entity.Id,
        PhotoCaptureCodes.ParseStandingLinkKind(entity.Kind),
        entity.OrganizationId,
        organizationName,
        principalId,
        isRepairer,
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
