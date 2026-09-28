using System.Collections.Immutable;

namespace Pegasus.Core.ReferenceData;

public readonly record struct PrincipalDomainPackageVersion(
    int SchemaVersion,
    string Version,
    string PackageSha256);

public enum PrincipalDomainValidationIssueCode
{
    InvalidJson = 1,
    SchemaMismatch = 2,
    VersionMismatch = 3,
    PackageHashMismatch = 4,
    MissingValue = 5,
    InvalidSource = 6,
    InvalidPrincipalCode = 7,
    DuplicatePrincipalCode = 8,
    InvalidSourceRow = 9,
    DuplicateSourceRow = 10,
    InvalidDomainSuffix = 11,
    DuplicateDomainSuffix = 12,
    EmptyPackage = 13
}

public sealed record PrincipalDomainValidationIssue(
    PrincipalDomainValidationIssueCode Code,
    string Subject);

public sealed record PrincipalDomainValidationResult(
    ImmutableArray<PrincipalDomainValidationIssue> Issues)
{
    public bool IsValid => Issues.IsDefaultOrEmpty;
}

public interface IPrincipalReferenceCatalog
{
    ValueTask<PrincipalDomainCandidates> FindCandidatesByDomainSuffixAsync(
        PrincipalDomainPackageVersion packageVersion,
        string domainSuffix,
        CancellationToken cancellationToken);
}
