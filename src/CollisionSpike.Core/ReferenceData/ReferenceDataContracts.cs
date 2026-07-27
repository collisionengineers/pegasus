using System.Collections.Immutable;

namespace CollisionSpike.Core.ReferenceData;

public readonly record struct ProviderDomainPackageVersion(
    int SchemaVersion,
    string Version,
    string PackageSha256);

public enum ProviderDomainValidationIssueCode
{
    InvalidJson = 1,
    SchemaMismatch = 2,
    VersionMismatch = 3,
    PackageHashMismatch = 4,
    MissingValue = 5,
    InvalidSource = 6,
    InvalidProviderCode = 7,
    DuplicateProviderCode = 8,
    InvalidSourceRow = 9,
    DuplicateSourceRow = 10,
    InvalidDomainSuffix = 11,
    DuplicateDomainSuffix = 12,
    EmptyPackage = 13
}

public sealed record ProviderDomainValidationIssue(
    ProviderDomainValidationIssueCode Code,
    string Subject);

public sealed record ProviderDomainValidationResult(
    ImmutableArray<ProviderDomainValidationIssue> Issues)
{
    public bool IsValid => Issues.IsDefaultOrEmpty;
}

public interface IProviderReferenceCatalog
{
    ValueTask<ProviderDomainCandidates> FindCandidatesByDomainSuffixAsync(
        ProviderDomainPackageVersion packageVersion,
        string domainSuffix,
        CancellationToken cancellationToken);
}
