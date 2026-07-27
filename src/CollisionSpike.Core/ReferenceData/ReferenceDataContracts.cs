using System.Collections.Immutable;

namespace CollisionSpike.Core.ReferenceData;

// Callers must request a concrete schema and package hash; there is intentionally no
// "current" or "latest" package fallback.
public readonly record struct ReferenceDataPackageVersion(
    int SchemaVersion,
    string PackageSha256);

public enum ReferenceDataPackageLoadStatus
{
    Loaded = 1,
    NotFound = 2,
    Rejected = 3
}

public sealed record ReferenceDataPackageLoadResult(
    ReferenceDataPackageLoadStatus Status,
    ReferenceDataPackage? Package,
    ImmutableArray<ReferenceDataValidationIssue> Issues);

public enum OrganizationResolutionStatus
{
    Resolved = 1,
    UnknownOrganizationId = 2,
    UnknownAlias = 3,
    AmbiguousOrganizationId = 4,
    AmbiguousAlias = 5,
    InvalidExactValue = 6
}

public sealed record OrganizationResolution(
    OrganizationResolutionStatus Status,
    Organization? Organization,
    ImmutableArray<string> CandidateOrganizationIds);

public interface IReferenceDataCatalog
{
    ValueTask<ReferenceDataPackageLoadResult> LoadAsync(
        ReferenceDataPackageVersion packageVersion,
        CancellationToken cancellationToken);

    // Implementations must compare the supplied value ordinally to the stored ID.
    // They must not normalize, fuzzily match, or infer a Principal from a sender.
    ValueTask<OrganizationResolution> ResolveExactOrganizationIdAsync(
        ReferenceDataPackageVersion packageVersion,
        string organizationId,
        CancellationToken cancellationToken);

    // Implementations must compare the supplied value ordinally to stored aliases.
    // This port never accepts sender or message-route data and never selects a Principal.
    ValueTask<OrganizationResolution> ResolveExactOrganizationAliasAsync(
        ReferenceDataPackageVersion packageVersion,
        string organizationAlias,
        CancellationToken cancellationToken);
}
