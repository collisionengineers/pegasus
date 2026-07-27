using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace CollisionSpike.Core.ReferenceData;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrganizationRole
{
    Principal = 1,
    Intermediary = 2,
    Repairer = 3
}

// Only imported candidates exist in this release. Promotion is an operator-review concern
// outside this package and must produce a separate active organization or location record.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CandidateReviewState
{
    Unreviewed = 1
}

public sealed record ReferenceDataPackage(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("manifest")] ReferenceDataManifest Manifest,
    [property: JsonPropertyName("sourceArtifacts")] ImmutableArray<SourceArtifact> SourceArtifacts,
    [property: JsonPropertyName("organizations")] ImmutableArray<Organization> Organizations,
    [property: JsonPropertyName("providers")] ImmutableArray<Provider> Providers,
    [property: JsonPropertyName("organizationCandidates")] ImmutableArray<OrganizationCandidate> OrganizationCandidates,
    [property: JsonPropertyName("locationCandidates")] ImmutableArray<LocationCandidate> LocationCandidates);

public sealed record ReferenceDataManifest(
    [property: JsonPropertyName("generatorVersion")] string GeneratorVersion,
    [property: JsonPropertyName("inputs")] ImmutableArray<SourceArtifact> Inputs,
    [property: JsonPropertyName("packageSha256")] string PackageSha256,
    [property: JsonPropertyName("counts")] ReferenceDataCounts Counts);

public sealed record ReferenceDataCounts(
    [property: JsonPropertyName("sourceCases")] int SourceCases,
    [property: JsonPropertyName("unmappedCaseIds")] int UnmappedCaseIds,
    [property: JsonPropertyName("providers")] int Providers,
    [property: JsonPropertyName("providerLocationRelationships")] int ProviderLocationRelationships,
    [property: JsonPropertyName("physicalRelationships")] int PhysicalRelationships,
    [property: JsonPropertyName("imageBasedAssessmentRelationships")] int ImageBasedAssessmentRelationships,
    [property: JsonPropertyName("notSuppliedRelationships")] int NotSuppliedRelationships,
    [property: JsonPropertyName("physicalMissingPostcodeRelationships")] int PhysicalMissingPostcodeRelationships,
    [property: JsonPropertyName("uniqueNormalizedLocationCandidates")] int UniqueNormalizedLocationCandidates,
    [property: JsonPropertyName("activeOrganizations")] int ActiveOrganizations,
    [property: JsonPropertyName("organizationCandidates")] int OrganizationCandidates);

public sealed record SourceArtifact(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("contentSha256")] string ContentSha256,
    [property: JsonPropertyName("role")] string Role);

public sealed record SourceOccurrence(
    [property: JsonPropertyName("sourceArtifactId")] string SourceArtifactId,
    [property: JsonPropertyName("sourceSheet")] string SourceSheet,
    [property: JsonPropertyName("sourceRow")] int SourceRow,
    [property: JsonPropertyName("sourceColumn")] string? SourceColumn = null,
    [property: JsonPropertyName("rawFields")] ImmutableDictionary<string, string?>? RawFields = null,
    [property: JsonPropertyName("normalizedFields")] ImmutableDictionary<string, string?>? NormalizedFields = null);

// This is a reusable identity. It deliberately contains neither case assignments nor
// sender/route predicates: both belong to their own policy owners.
public sealed record Organization(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("canonicalName")] string CanonicalName,
    [property: JsonPropertyName("aliases")] ImmutableArray<OrganizationAlias> Aliases,
    [property: JsonPropertyName("roles")] ImmutableArray<OrganizationRole> Roles,
    [property: JsonPropertyName("sourceOccurrences")] ImmutableArray<SourceOccurrence> SourceOccurrences);

public sealed record OrganizationAlias(
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("sourceOccurrences")] ImmutableArray<SourceOccurrence> SourceOccurrences);

public sealed record Provider(
    [property: JsonPropertyName("organizationId")] string OrganizationId,
    [property: JsonPropertyName("defaults")] ImmutableArray<ProviderDefault> Defaults);

// Value intentionally remains nullable. Its source evidence is retained even when the
// source has no usable value, rather than manufacturing a replacement value.
public sealed record ProviderDefault(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("value")] string? Value,
    [property: JsonPropertyName("sourceOccurrences")] ImmutableArray<SourceOccurrence> SourceOccurrences);

// Candidate rows are evidence, not active identities or runtime selectors.
public sealed record OrganizationCandidate(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("sourceArtifactId")] string SourceArtifactId,
    [property: JsonPropertyName("sourceSheet")] string SourceSheet,
    [property: JsonPropertyName("sourceRow")] int SourceRow,
    [property: JsonPropertyName("rawFields")] ImmutableDictionary<string, string?> RawFields,
    [property: JsonPropertyName("normalizedFields")] ImmutableDictionary<string, string?> NormalizedFields,
    [property: JsonPropertyName("reviewState")] CandidateReviewState ReviewState,
    [property: JsonPropertyName("duplicateGroupId")] string? DuplicateGroupId);

// Each row preserves a provider/location relationship. Duplicate grouping is evidence
// for review only and never authorizes an automatic merge or selection.
public sealed record LocationCandidate(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("providerOrganizationId")] string ProviderOrganizationId,
    [property: JsonPropertyName("sourceArtifactId")] string SourceArtifactId,
    [property: JsonPropertyName("sourceSheet")] string SourceSheet,
    [property: JsonPropertyName("sourceRow")] int SourceRow,
    [property: JsonPropertyName("rawFields")] ImmutableDictionary<string, string?> RawFields,
    [property: JsonPropertyName("normalizedFields")] ImmutableDictionary<string, string?> NormalizedFields,
    [property: JsonPropertyName("reviewState")] CandidateReviewState ReviewState,
    [property: JsonPropertyName("duplicateGroupId")] string? DuplicateGroupId);

public enum ProviderDefaultDisposition
{
    AvailableForReview = 1,
    BlockedByExplicitInstruction = 2,
    BlockedByStaffReview = 3
}

public sealed record ProviderDefaultAssessment(
    ProviderDefaultDisposition Disposition,
    string? SuggestedValue,
    ImmutableArray<SourceOccurrence> SourceOccurrences)
{
    public bool CanBePresentedForReview => Disposition == ProviderDefaultDisposition.AvailableForReview;
}
