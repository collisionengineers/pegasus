using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Infrastructure.Persistence;

/// <summary>Bounded location suggestions from the canonical Contacts directory.</summary>
public sealed class EfOrganizationDirectory(IDbContextFactory<PegasusDbContext> contextFactory)
    : IOrganizationDirectoryQueries
{
    public async Task<IReadOnlyList<OrganizationDirectoryRecord>> SearchAsync(
        OrganizationDirectoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        StaffAuthorization.Require(query.Actor, StaffAccessRight.PerformCasework);
        var name = InspectionLocationMatchPolicy.NormalizeNamePrefix(query.Prefix ?? string.Empty);
        var postcode = InspectionLocationMatchPolicy.NormalizePostcodePrefix(query.Prefix ?? string.Empty);
        if (!InspectionLocationMatchPolicy.MeetsMinimumLength(name)
            && !InspectionLocationMatchPolicy.MeetsMinimumLength(postcode)) return [];
        var role = query.Role switch
        {
            null => null,
            OrganizationDirectoryRole.Repairer => "repairer",
            OrganizationDirectoryRole.Storage => "storage",
            _ => throw new ArgumentOutOfRangeException(nameof(query))
        };
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Organizations.AsNoTracking()
            .Where(item => item.Active && item.Address != null && item.Address != "")
            .Where(item => item.ContactRoles.Any(contactRole => role == null
                ? contactRole.Role == "repairer" || contactRole.Role == "storage"
                : contactRole.Role == role))
            .Where(item => item.NormalizedName.StartsWith(name)
                || (item.Postcode != null && item.Postcode.Replace(" ", "").StartsWith(postcode)))
            .OrderByDescending(item => item.NormalizedName == name
                || (item.Postcode != null && item.Postcode.Replace(" ", "") == postcode))
            .ThenBy(item => item.NormalizedName).ThenBy(item => item.Id)
            .Take(InspectionLocationMatchPolicy.ClampLimit(query.Limit))
            .Select(item => new
            {
                item.Id, item.Name, item.ContactPerson, item.Telephone, item.Email,
                item.Address, item.Postcode, item.Active, item.Version,
                IsRepairer = item.ContactRoles.Any(contactRole => contactRole.Role == "repairer")
            }).ToArrayAsync(cancellationToken);
        return rows.Select(item => new OrganizationDirectoryRecord(item.Id,
            query.Role ?? (item.IsRepairer ? OrganizationDirectoryRole.Repairer : OrganizationDirectoryRole.Storage),
            item.Name, item.ContactPerson, item.Telephone, item.Email, item.Address!, item.Postcode,
            item.Active, item.Version)).ToArray();
    }
}
