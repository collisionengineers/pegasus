using System.Collections.Immutable;
using CollisionSpike.Core.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace CollisionSpike.Infrastructure.Persistence;

internal sealed class EfProviderReferenceCatalog(
    IDbContextFactory<CollisionSpikeDbContext> contextFactory) : IProviderReferenceCatalog
{
    public async ValueTask<ProviderDomainCandidates> FindCandidatesByDomainSuffixAsync(
        ProviderDomainPackageVersion packageVersion,
        string domainSuffix,
        CancellationToken cancellationToken)
    {
        if (!ReferenceDataPolicy.IsValidPackageVersion(packageVersion))
        {
            return Empty(ProviderDomainCandidateStatus.PackageRejected);
        }

        if (!ReferenceDataPolicy.IsCanonicalDomainSuffix(domainSuffix))
        {
            return Empty(ProviderDomainCandidateStatus.InvalidSuffix);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await (
                from package in context.ProviderDomainPackages.AsNoTracking()
                where package.Version == packageVersion.Version
                join evidence in context.ProviderDomainEvidence.AsNoTracking()
                        .Where(item => item.DomainSuffix == domainSuffix)
                    on package.Version equals evidence.Version into evidenceRows
                from evidence in evidenceRows.DefaultIfEmpty()
                orderby evidence == null ? null : evidence.Code
                select new
                {
                    package.SchemaVersion,
                    package.PackageSha256,
                    ProviderCode = evidence == null ? null : evidence.Code
                })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return Empty(ProviderDomainCandidateStatus.PackageNotFound);
        }

        var storedPackage = rows[0];
        if (storedPackage.SchemaVersion != packageVersion.SchemaVersion ||
            !StringComparer.Ordinal.Equals(storedPackage.PackageSha256, packageVersion.PackageSha256))
        {
            return Empty(ProviderDomainCandidateStatus.PackageRejected);
        }

        var providerCodes = ImmutableArray.CreateBuilder<string>(rows.Count);
        foreach (var row in rows)
        {
            if (row.ProviderCode is not null)
            {
                providerCodes.Add(row.ProviderCode);
            }
        }

        return ReferenceDataPolicy.CreateCandidates(domainSuffix, providerCodes.ToImmutable());
    }

    private static ProviderDomainCandidates Empty(ProviderDomainCandidateStatus status) =>
        new(status, ImmutableArray<string>.Empty);
}
