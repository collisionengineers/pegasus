using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>
/// EXT-18/S05: read-only search over the Administrator-maintained directory
/// locations F/G1 added as <see cref="OrganizationDirectoryEntryEntity"/>.
/// This directory is one of the four local sources
/// <see cref="InspectionAddressChoicesQueries"/> unions for a case's location
/// suggestions; no external address provider, no fuzzy or geographic
/// matching, and never more than the shared internal 20-row cap.
/// </summary>
public sealed class EfOrganizationDirectory(IDbContextFactory<PegasusDbContext> contextFactory)
    : IOrganizationDirectoryQueries
{
    private readonly IDbContextFactory<PegasusDbContext> _contextFactory =
        contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public async Task<IReadOnlyList<OrganizationDirectoryRecord>> SearchAsync(
        OrganizationDirectoryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(query.Actor);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);

        var namePrefix = InspectionLocationMatchPolicy.NormalizeNamePrefix(query.Prefix ?? string.Empty);
        var postcodePrefix = InspectionLocationMatchPolicy.NormalizePostcodePrefix(query.Prefix ?? string.Empty);
        if (!InspectionLocationMatchPolicy.MeetsMinimumLength(namePrefix)
            && !InspectionLocationMatchPolicy.MeetsMinimumLength(postcodePrefix))
        {
            return [];
        }

        var limit = InspectionLocationMatchPolicy.ClampLimit(query.Limit);
        var roleCode = query.Role is { } role ? ToRoleCode(role) : null;

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        // C06 review R-10: the exact-before-prefix rank must be applied
        // *before* the bounded fetch, not only after it — otherwise an exact
        // postcode match whose name sorts late is cut by Take(limit * 4)
        // before it is ever re-ranked. This repeats
        // InspectionLocationMatchPolicy.IsExactMatch's expression inline
        // because an EF Core query cannot translate a call to it into SQL;
        // that policy method is the one other owner of this rule.
        var candidates = await context.Set<OrganizationDirectoryEntryEntity>()
            .AsNoTracking()
            .Where(entry => entry.Active)
            .Where(entry => roleCode == null || entry.Role == roleCode)
            .Where(entry =>
                entry.NormalizedName.StartsWith(namePrefix)
                || (entry.NormalizedPostcode != null && entry.NormalizedPostcode.StartsWith(postcodePrefix)))
            .OrderByDescending(entry =>
                entry.NormalizedName == namePrefix
                || (entry.NormalizedPostcode != null && entry.NormalizedPostcode == postcodePrefix))
            .ThenBy(entry => entry.NormalizedName)
            .ThenBy(entry => entry.Id)
            .Take(limit * 4)
            .ToArrayAsync(cancellationToken);

        return candidates
            .OrderByDescending(entry => InspectionLocationMatchPolicy.IsExactMatch(
                entry.NormalizedName, entry.NormalizedPostcode, namePrefix, postcodePrefix))
            .ThenBy(entry => entry.NormalizedName)
            .ThenBy(entry => entry.NormalizedPostcode)
            .ThenBy(entry => entry.Id)
            .Take(limit)
            .Select(ToRecord)
            .ToArray();
    }

    private static OrganizationDirectoryRecord ToRecord(OrganizationDirectoryEntryEntity entity) =>
        new(
            entity.Id,
            ParseRole(entity.Role),
            entity.Name,
            entity.Contact,
            entity.Telephone,
            entity.Email,
            entity.Address,
            entity.Postcode,
            entity.Active,
            entity.Version,
            entity.SourceKind,
            entity.SourceRecordId ?? entity.Id,
            entity.SourceVersion,
            entity.UpdatedAtUtc);

    private static string ToRoleCode(OrganizationDirectoryRole role) => role switch
    {
        OrganizationDirectoryRole.Repairer => "repairer",
        OrganizationDirectoryRole.Storage => "storage",
        OrganizationDirectoryRole.InspectionLocation => "inspection_location",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static OrganizationDirectoryRole ParseRole(string role) => role switch
    {
        "repairer" => OrganizationDirectoryRole.Repairer,
        "storage" => OrganizationDirectoryRole.Storage,
        "inspection_location" => OrganizationDirectoryRole.InspectionLocation,
        _ => throw new InvalidOperationException("The persisted organization directory role is invalid.")
    };
}
