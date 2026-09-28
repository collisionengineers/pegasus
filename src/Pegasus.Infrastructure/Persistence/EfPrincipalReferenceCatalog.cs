using System.Collections.Immutable;
using Pegasus.Core.ReferenceData;
using Microsoft.EntityFrameworkCore;

namespace Pegasus.Infrastructure.Persistence;

internal sealed class EfPrincipalReferenceCatalog(
    IDbContextFactory<PegasusDbContext> contextFactory) : IPrincipalReferenceCatalog
{
    public async ValueTask<PrincipalDomainCandidates> FindCandidatesByDomainSuffixAsync(
        PrincipalDomainPackageVersion packageVersion,
        string domainSuffix,
        CancellationToken cancellationToken)
    {
        if (!ReferenceDataPolicy.IsValidPackageVersion(packageVersion))
        {
            return Empty(PrincipalDomainCandidateStatus.PackageRejected);
        }

        if (!ReferenceDataPolicy.IsCanonicalDomainSuffix(domainSuffix))
        {
            return Empty(PrincipalDomainCandidateStatus.InvalidSuffix);
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await (
                from package in context.PrincipalDomainPackages.AsNoTracking()
                where package.Version == packageVersion.Version
                join evidence in context.PrincipalDomainEvidence.AsNoTracking()
                        .Where(item => item.DomainSuffix == domainSuffix)
                    on package.Version equals evidence.Version into evidenceRows
                from evidence in evidenceRows.DefaultIfEmpty()
                orderby evidence == null ? null : evidence.Code
                select new
                {
                    package.SchemaVersion,
                    package.PackageSha256,
                    PrincipalCode = evidence == null ? null : evidence.Code
                })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return Empty(PrincipalDomainCandidateStatus.PackageNotFound);
        }

        var storedPackage = rows[0];
        if (storedPackage.SchemaVersion != packageVersion.SchemaVersion ||
            !StringComparer.Ordinal.Equals(storedPackage.PackageSha256, packageVersion.PackageSha256))
        {
            return Empty(PrincipalDomainCandidateStatus.PackageRejected);
        }

        var principalCodes = ImmutableArray.CreateBuilder<string>(rows.Count);
        foreach (var row in rows)
        {
            if (row.PrincipalCode is not null)
            {
                principalCodes.Add(row.PrincipalCode);
            }
        }

        return ReferenceDataPolicy.CreateCandidates(domainSuffix, principalCodes.ToImmutable());
    }

    private static PrincipalDomainCandidates Empty(PrincipalDomainCandidateStatus status) =>
        new(status, ImmutableArray<string>.Empty);
}
