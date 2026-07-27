using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace CollisionSpike.Core.ReferenceData;

public enum ReferenceDataValidationIssueCode
{
    SchemaMismatch = 1,
    CountMismatch = 2,
    AmbiguousAlias = 3,
    UnknownId = 4,
    InvalidRoleCombination = 5,
    MissingProvenance = 6,
    NonUnreviewedImportedCandidate = 7,
    DuplicateId = 8,
    InvalidIdentity = 9,
    ManifestMismatch = 10,
    AmbiguousProviderDefault = 11
}

public sealed record ReferenceDataValidationIssue(
    ReferenceDataValidationIssueCode Code,
    string Subject,
    string Detail);

public sealed record ReferenceDataValidationResult(
    ImmutableArray<ReferenceDataValidationIssue> Issues)
{
    public bool IsValid => Issues.IsDefaultOrEmpty;
}

public static class ReferenceDataPolicy
{
    public const int SupportedSchemaVersion = 1;
    public const int ExpectedSourceCaseCount = 17_737;
    public const int ExpectedUnmappedCaseIdCount = 410;
    public const int ExpectedProviderCount = 88;
    public const int ExpectedProviderLocationRelationshipCount = 1_638;
    public const int ExpectedPhysicalRelationshipCount = 1_555;
    public const int ExpectedImageBasedAssessmentRelationshipCount = 66;
    public const int ExpectedNotSuppliedRelationshipCount = 17;
    public const int ExpectedPhysicalMissingPostcodeRelationshipCount = 74;
    public const int ExpectedUniqueNormalizedLocationCandidateCount = 649;

    public static ReferenceDataValidationResult Validate(ReferenceDataPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        var issues = ImmutableArray.CreateBuilder<ReferenceDataValidationIssue>();
        if (package.SchemaVersion != SupportedSchemaVersion)
        {
            AddIssue(
                issues,
                ReferenceDataValidationIssueCode.SchemaMismatch,
                "schemaVersion",
                $"Expected schema version {SupportedSchemaVersion}, but found {package.SchemaVersion}.");
        }

        var sourceArtifactIds = new HashSet<string>(StringComparer.Ordinal);
        ValidateSourceArtifacts(package.SourceArtifacts, sourceArtifactIds, issues);

        if (package.Manifest is null)
        {
            AddIssue(
                issues,
                ReferenceDataValidationIssueCode.MissingProvenance,
                "manifest",
                "A versioned reference-data package requires a manifest.");
        }
        else
        {
            ValidateManifest(package.Manifest, package.SourceArtifacts, issues);
        }

        var duplicateOrganizationIds = new HashSet<string>(StringComparer.Ordinal);
        var organizations = ValidateOrganizations(
            package.Organizations,
            sourceArtifactIds,
            duplicateOrganizationIds,
            issues);

        ValidateProviders(
            package.Providers,
            organizations,
            duplicateOrganizationIds,
            sourceArtifactIds,
            issues);

        ValidateOrganizationCandidates(package.OrganizationCandidates, sourceArtifactIds, issues);
        var uniqueNormalizedLocationCandidateCount = ValidateLocationCandidates(
            package.LocationCandidates,
            organizations,
            duplicateOrganizationIds,
            sourceArtifactIds,
            issues);

        if (package.Manifest?.Counts is null)
        {
            AddIssue(
                issues,
                ReferenceDataValidationIssueCode.MissingProvenance,
                "manifest.counts",
                "A versioned reference-data package requires declared counts.");
        }
        else
        {
            ValidateCounts(
                package.Manifest.Counts,
                package,
                uniqueNormalizedLocationCandidateCount,
                issues);
        }

        return new ReferenceDataValidationResult(issues.ToImmutable());
    }

    // This derives only the deterministic artifact key. It reads no files and accepts
    // only a normalized repository-relative POSIX path, never a machine path.
    public static string CreateSourceArtifactId(string normalizedRepositoryRelativePosixPath)
    {
        if (!IsNormalizedRepositoryRelativePosixPath(normalizedRepositoryRelativePosixPath))
        {
            throw new ArgumentException(
                "A source artifact path must be normalized, repository-relative, and POSIX-separated.",
                nameof(normalizedRepositoryRelativePosixPath));
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedRepositoryRelativePosixPath));
        return $"artifact:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    // Exact resolution deliberately does not normalize inputs, look at sender data, or
    // select a role. Catalog implementations can use this policy after loading a valid package.
    public static OrganizationResolution ResolveExactOrganizationId(
        ReferenceDataPackage package,
        string organizationId)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (string.IsNullOrWhiteSpace(organizationId))
        {
            return new OrganizationResolution(
                OrganizationResolutionStatus.InvalidExactValue,
                null,
                ImmutableArray<string>.Empty);
        }

        Organization? resolved = null;
        var isAmbiguous = false;
        foreach (var organization in package.Organizations)
        {
            if (organization is null || !StringComparer.Ordinal.Equals(organization.Id, organizationId))
            {
                continue;
            }

            if (resolved is null)
            {
                resolved = organization;
            }
            else
            {
                isAmbiguous = true;
            }
        }

        if (resolved is null)
        {
            return new OrganizationResolution(
                OrganizationResolutionStatus.UnknownOrganizationId,
                null,
                ImmutableArray<string>.Empty);
        }

        if (isAmbiguous)
        {
            return new OrganizationResolution(
                OrganizationResolutionStatus.AmbiguousOrganizationId,
                null,
                ImmutableArray.Create(organizationId));
        }

        return new OrganizationResolution(
            OrganizationResolutionStatus.Resolved,
            resolved,
            ImmutableArray<string>.Empty);
    }

    public static OrganizationResolution ResolveExactOrganizationAlias(
        ReferenceDataPackage package,
        string alias)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (string.IsNullOrWhiteSpace(alias))
        {
            return new OrganizationResolution(
                OrganizationResolutionStatus.InvalidExactValue,
                null,
                ImmutableArray<string>.Empty);
        }

        Organization? resolved = null;
        ImmutableArray<string>.Builder? candidates = null;

        foreach (var organization in package.Organizations)
        {
            if (organization is null || !HasExactAlias(organization, alias))
            {
                continue;
            }

            if (resolved is null)
            {
                resolved = organization;
                continue;
            }

            candidates ??= ImmutableArray.CreateBuilder<string>(2);
            if (candidates.Count == 0)
            {
                candidates.Add(resolved.Id);
            }

            candidates.Add(organization.Id);
        }

        if (resolved is null)
        {
            return new OrganizationResolution(
                OrganizationResolutionStatus.UnknownAlias,
                null,
                ImmutableArray<string>.Empty);
        }

        if (candidates is not null)
        {
            return new OrganizationResolution(
                OrganizationResolutionStatus.AmbiguousAlias,
                null,
                candidates.ToImmutable());
        }

        return new OrganizationResolution(
            OrganizationResolutionStatus.Resolved,
            resolved,
            ImmutableArray<string>.Empty);
    }

    // Defaults are at most reviewable suggestions. The returned value and provenance are
    // deliberately withheld whenever instruction content or a staff decision already exists.
    public static ProviderDefaultAssessment AssessProviderDefault(
        ProviderDefault providerDefault,
        bool hasExplicitInstructionContent,
        bool hasStaffReview)
    {
        ArgumentNullException.ThrowIfNull(providerDefault);

        if (hasStaffReview)
        {
            return new ProviderDefaultAssessment(
                ProviderDefaultDisposition.BlockedByStaffReview,
                null,
                ImmutableArray<SourceOccurrence>.Empty);
        }

        if (hasExplicitInstructionContent)
        {
            return new ProviderDefaultAssessment(
                ProviderDefaultDisposition.BlockedByExplicitInstruction,
                null,
                ImmutableArray<SourceOccurrence>.Empty);
        }

        return new ProviderDefaultAssessment(
            ProviderDefaultDisposition.AvailableForReview,
            providerDefault.Value,
            providerDefault.SourceOccurrences);
    }

    private static void ValidateSourceArtifacts(
        ImmutableArray<SourceArtifact> sourceArtifacts,
        HashSet<string> sourceArtifactIds,
        ImmutableArray<ReferenceDataValidationIssue>.Builder issues)
    {
        for (var index = 0; index < sourceArtifacts.Length; index++)
        {
            var subject = $"sourceArtifacts[{index}]";
            var artifact = sourceArtifacts[index];
            if (artifact is null)
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.MissingProvenance,
                    subject,
                    "A source artifact entry is required.");
                continue;
            }

            var hasId = IsPresent(artifact.Id);
            if (!hasId)
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.MissingProvenance,
                    subject,
                    "Source artifact ID is required.");
            }
            else if (!sourceArtifactIds.Add(artifact.Id))
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.DuplicateId,
                    subject,
                    $"Source artifact ID '{artifact.Id}' is not unique.");
            }

            if (!IsNormalizedRepositoryRelativePosixPath(artifact.Path))
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.InvalidIdentity,
                    subject,
                    "Source artifact path must be normalized, repository-relative, and POSIX-separated.");
            }
            else if (hasId && !StringComparer.Ordinal.Equals(artifact.Id, CreateSourceArtifactId(artifact.Path)))
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.InvalidIdentity,
                    subject,
                    "Source artifact ID does not match the deterministic path-derived identity.");
            }

            if (!IsLowercaseSha256(artifact.ContentSha256))
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.MissingProvenance,
                    subject,
                    "Source artifact contentSha256 must be a lowercase SHA-256 value.");
            }

            if (!IsPresent(artifact.Role))
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.MissingProvenance,
                    subject,
                    "Source artifact role is required.");
            }
        }
    }

    private static void ValidateManifest(
        ReferenceDataManifest manifest,
        ImmutableArray<SourceArtifact> sourceArtifacts,
        ImmutableArray<ReferenceDataValidationIssue>.Builder issues)
    {
        if (!IsPresent(manifest.GeneratorVersion))
        {
            AddIssue(
                issues,
                ReferenceDataValidationIssueCode.MissingProvenance,
                "manifest.generatorVersion",
                "Generator version is required.");
        }

        if (!IsLowercaseSha256(manifest.PackageSha256))
        {
            AddIssue(
                issues,
                ReferenceDataValidationIssueCode.MissingProvenance,
                "manifest.packageSha256",
                "Package hash must be a lowercase SHA-256 value.");
        }

        if (manifest.Inputs.Length != sourceArtifacts.Length)
        {
            AddIssue(
                issues,
                ReferenceDataValidationIssueCode.CountMismatch,
                "manifest.inputs",
                $"Manifest declares {manifest.Inputs.Length} ordered inputs, but sourceArtifacts contains {sourceArtifacts.Length}.");
        }

        var length = Math.Min(manifest.Inputs.Length, sourceArtifacts.Length);
        for (var index = 0; index < length; index++)
        {
            if (!SameArtifact(manifest.Inputs[index], sourceArtifacts[index]))
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.ManifestMismatch,
                    $"manifest.inputs[{index}]",
                    "Manifest input does not match the source artifact at the same ordered position.");
            }
        }
    }

    private static Dictionary<string, Organization> ValidateOrganizations(
        ImmutableArray<Organization> organizations,
        HashSet<string> sourceArtifactIds,
        HashSet<string> duplicateOrganizationIds,
        ImmutableArray<ReferenceDataValidationIssue>.Builder issues)
    {
        var byId = new Dictionary<string, Organization>(StringComparer.Ordinal);
        var aliasOwners = new Dictionary<string, string>(StringComparer.Ordinal);
        var reportedAmbiguousAliases = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < organizations.Length; index++)
        {
            var subject = $"organizations[{index}]";
            var organization = organizations[index];
            if (organization is null)
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.InvalidIdentity,
                    subject,
                    "Organization entry is required.");
                continue;
            }

            var hasId = IsPresent(organization.Id);
            if (!hasId)
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.InvalidIdentity,
                    subject,
                    "Organization ID is required.");
            }
            else if (!byId.TryAdd(organization.Id, organization))
            {
                duplicateOrganizationIds.Add(organization.Id);
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.DuplicateId,
                    subject,
                    $"Organization ID '{organization.Id}' is not unique.");
            }

            if (!IsPresent(organization.CanonicalName))
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.InvalidIdentity,
                    subject,
                    "Canonical organization name is required.");
            }

            ValidateRoles(organization.Roles, $"{subject}.roles", issues);
            ValidateOccurrences(
                organization.SourceOccurrences,
                required: true,
                $"{subject}.sourceOccurrences",
                sourceArtifactIds,
                issues);

            for (var aliasIndex = 0; aliasIndex < organization.Aliases.Length; aliasIndex++)
            {
                var aliasSubject = $"{subject}.aliases[{aliasIndex}]";
                var alias = organization.Aliases[aliasIndex];
                if (alias is null)
                {
                    AddIssue(
                        issues,
                        ReferenceDataValidationIssueCode.MissingProvenance,
                        aliasSubject,
                        "Alias entry is required.");
                    continue;
                }

                if (!IsPresent(alias.Value))
                {
                    AddIssue(
                        issues,
                        ReferenceDataValidationIssueCode.InvalidIdentity,
                        aliasSubject,
                        "Alias value is required.");
                }
                else if (hasId)
                {
                    if (aliasOwners.TryGetValue(alias.Value, out var ownerId))
                    {
                        if (!StringComparer.Ordinal.Equals(ownerId, organization.Id) && reportedAmbiguousAliases.Add(alias.Value))
                        {
                            AddIssue(
                                issues,
                                ReferenceDataValidationIssueCode.AmbiguousAlias,
                                aliasSubject,
                                $"Alias '{alias.Value}' identifies both '{ownerId}' and '{organization.Id}'.");
                        }
                    }
                    else
                    {
                        aliasOwners.Add(alias.Value, organization.Id);
                    }
                }

                // A generated alias may inherit its organization's source occurrence when
                // the input contains no distinct alias spelling. It is still not resolvable
                // without the owning organization's provenance.
                if (alias.SourceOccurrences.IsDefaultOrEmpty)
                {
                    if (organization.SourceOccurrences.IsDefaultOrEmpty)
                    {
                        AddIssue(
                            issues,
                            ReferenceDataValidationIssueCode.MissingProvenance,
                            aliasSubject,
                            "Alias has no source occurrence and its organization has no source provenance.");
                    }
                }
                else
                {
                    ValidateOccurrences(
                        alias.SourceOccurrences,
                        required: false,
                        $"{aliasSubject}.sourceOccurrences",
                        sourceArtifactIds,
                        issues);
                }
            }
        }

        return byId;
    }

    private static void ValidateProviders(
        ImmutableArray<Provider> providers,
        Dictionary<string, Organization> organizations,
        HashSet<string> duplicateOrganizationIds,
        HashSet<string> sourceArtifactIds,
        ImmutableArray<ReferenceDataValidationIssue>.Builder issues)
    {
        var providerOrganizationIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < providers.Length; index++)
        {
            var subject = $"providers[{index}]";
            var provider = providers[index];
            if (provider is null)
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.UnknownId,
                    subject,
                    "Provider entry is required.");
                continue;
            }

            if (!IsPresent(provider.OrganizationId))
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.UnknownId,
                    subject,
                    "Provider organization ID is required.");
            }
            else
            {
                if (!providerOrganizationIds.Add(provider.OrganizationId))
                {
                    AddIssue(
                        issues,
                        ReferenceDataValidationIssueCode.DuplicateId,
                        subject,
                        $"Provider organization ID '{provider.OrganizationId}' occurs more than once.");
                }

                if (!organizations.TryGetValue(provider.OrganizationId, out var organization) ||
                    duplicateOrganizationIds.Contains(provider.OrganizationId))
                {
                    AddIssue(
                        issues,
                        ReferenceDataValidationIssueCode.UnknownId,
                        subject,
                        $"Provider organization ID '{provider.OrganizationId}' is unknown or ambiguous.");
                }
                else if (!HasRole(organization, OrganizationRole.Principal))
                {
                    AddIssue(
                        issues,
                        ReferenceDataValidationIssueCode.InvalidRoleCombination,
                        subject,
                        "A provider must reference an organization with the Principal role.");
                }
            }

            ValidateProviderDefaults(provider.Defaults, $"{subject}.defaults", sourceArtifactIds, issues);
        }
    }

    private static void ValidateProviderDefaults(
        ImmutableArray<ProviderDefault> defaults,
        string subject,
        HashSet<string> sourceArtifactIds,
        ImmutableArray<ReferenceDataValidationIssue>.Builder issues)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < defaults.Length; index++)
        {
            var defaultSubject = $"{subject}[{index}]";
            var providerDefault = defaults[index];
            if (providerDefault is null)
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.MissingProvenance,
                    defaultSubject,
                    "Provider default entry is required.");
                continue;
            }

            if (!IsPresent(providerDefault.Key))
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.InvalidIdentity,
                    defaultSubject,
                    "Provider default key is required.");
            }
            else if (!keys.Add(providerDefault.Key))
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.AmbiguousProviderDefault,
                    defaultSubject,
                    $"Provider default key '{providerDefault.Key}' occurs more than once.");
            }

            ValidateOccurrences(
                providerDefault.SourceOccurrences,
                required: true,
                $"{defaultSubject}.sourceOccurrences",
                sourceArtifactIds,
                issues);
        }
    }

    private static void ValidateOrganizationCandidates(
        ImmutableArray<OrganizationCandidate> candidates,
        HashSet<string> sourceArtifactIds,
        ImmutableArray<ReferenceDataValidationIssue>.Builder issues)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < candidates.Length; index++)
        {
            var subject = $"organizationCandidates[{index}]";
            var candidate = candidates[index];
            if (candidate is null)
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.MissingProvenance,
                    subject,
                    "Organization candidate entry is required.");
                continue;
            }

            ValidateCandidateIdentity(candidate.Id, ids, subject, issues);
            ValidateCandidateProvenance(
                candidate.SourceArtifactId,
                candidate.SourceSheet,
                candidate.SourceRow,
                subject,
                sourceArtifactIds,
                issues);
            ValidateFields(candidate.RawFields, $"{subject}.rawFields", issues);
            ValidateFields(candidate.NormalizedFields, $"{subject}.normalizedFields", issues);
            ValidateCandidateReviewState(candidate.ReviewState, subject, issues);
            ValidateDuplicateGroupId(candidate.DuplicateGroupId, subject, issues);
        }
    }

    private static int ValidateLocationCandidates(
        ImmutableArray<LocationCandidate> candidates,
        Dictionary<string, Organization> organizations,
        HashSet<string> duplicateOrganizationIds,
        HashSet<string> sourceArtifactIds,
        ImmutableArray<ReferenceDataValidationIssue>.Builder issues)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var normalizedIdentityGroups = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < candidates.Length; index++)
        {
            var subject = $"locationCandidates[{index}]";
            var candidate = candidates[index];
            if (candidate is null)
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.MissingProvenance,
                    subject,
                    "Location candidate entry is required.");
                continue;
            }

            var hasId = ValidateCandidateIdentity(candidate.Id, ids, subject, issues);
            if (!IsPresent(candidate.ProviderOrganizationId) ||
                !organizations.TryGetValue(candidate.ProviderOrganizationId, out var providerOrganization) ||
                duplicateOrganizationIds.Contains(candidate.ProviderOrganizationId))
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.UnknownId,
                    subject,
                    $"Provider organization ID '{candidate.ProviderOrganizationId}' is unknown or ambiguous.");
            }
            else if (!HasRole(providerOrganization, OrganizationRole.Principal))
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.InvalidRoleCombination,
                    subject,
                    "A provider/location relationship must reference a Principal organization.");
            }

            ValidateCandidateProvenance(
                candidate.SourceArtifactId,
                candidate.SourceSheet,
                candidate.SourceRow,
                subject,
                sourceArtifactIds,
                issues);
            ValidateFields(candidate.RawFields, $"{subject}.rawFields", issues);
            ValidateFields(candidate.NormalizedFields, $"{subject}.normalizedFields", issues);
            ValidateCandidateReviewState(candidate.ReviewState, subject, issues);
            ValidateDuplicateGroupId(candidate.DuplicateGroupId, subject, issues);

            if (hasId)
            {
                var normalizedIdentity = IsPresent(candidate.DuplicateGroupId)
                    ? candidate.DuplicateGroupId!
                    : candidate.Id;
                normalizedIdentityGroups.Add(normalizedIdentity);
            }
        }

        return normalizedIdentityGroups.Count;
    }

    private static void ValidateCounts(
        ReferenceDataCounts counts,
        ReferenceDataPackage package,
        int uniqueNormalizedLocationCandidateCount,
        ImmutableArray<ReferenceDataValidationIssue>.Builder issues)
    {
        ValidateDeclaredCount("manifest.counts.activeOrganizations", counts.ActiveOrganizations, package.Organizations.Length, issues);
        ValidateDeclaredCount(
            "manifest.counts.organizationCandidates",
            counts.OrganizationCandidates,
            package.OrganizationCandidates.Length,
            issues);
        ValidateDeclaredCount("manifest.counts.providers", counts.Providers, package.Providers.Length, issues);
        ValidateDeclaredCount(
            "manifest.counts.providerLocationRelationships",
            counts.ProviderLocationRelationships,
            package.LocationCandidates.Length,
            issues);
        ValidateDeclaredCount(
            "manifest.counts.uniqueNormalizedLocationCandidates",
            counts.UniqueNormalizedLocationCandidates,
            uniqueNormalizedLocationCandidateCount,
            issues);

        var declaredRelationshipBreakdown =
            (long)counts.PhysicalRelationships +
            counts.ImageBasedAssessmentRelationships +
            counts.NotSuppliedRelationships;
        if (declaredRelationshipBreakdown != counts.ProviderLocationRelationships)
        {
            AddIssue(
                issues,
                ReferenceDataValidationIssueCode.CountMismatch,
                "manifest.counts.providerLocationRelationships",
                "Physical, Image Based Assessment, and Not supplied relationship counts must equal the total relationship count.");
        }

        ValidateObservedBaselineCount("sourceCases", counts.SourceCases, ExpectedSourceCaseCount, issues);
        ValidateObservedBaselineCount("unmappedCaseIds", counts.UnmappedCaseIds, ExpectedUnmappedCaseIdCount, issues);
        ValidateObservedBaselineCount("providers", package.Providers.Length, ExpectedProviderCount, issues);
        ValidateObservedBaselineCount(
            "providerLocationRelationships",
            package.LocationCandidates.Length,
            ExpectedProviderLocationRelationshipCount,
            issues);
        ValidateObservedBaselineCount(
            "physicalRelationships",
            counts.PhysicalRelationships,
            ExpectedPhysicalRelationshipCount,
            issues);
        ValidateObservedBaselineCount(
            "imageBasedAssessmentRelationships",
            counts.ImageBasedAssessmentRelationships,
            ExpectedImageBasedAssessmentRelationshipCount,
            issues);
        ValidateObservedBaselineCount(
            "notSuppliedRelationships",
            counts.NotSuppliedRelationships,
            ExpectedNotSuppliedRelationshipCount,
            issues);
        ValidateObservedBaselineCount(
            "physicalMissingPostcodeRelationships",
            counts.PhysicalMissingPostcodeRelationships,
            ExpectedPhysicalMissingPostcodeRelationshipCount,
            issues);
        ValidateObservedBaselineCount(
            "uniqueNormalizedLocationCandidates",
            uniqueNormalizedLocationCandidateCount,
            ExpectedUniqueNormalizedLocationCandidateCount,
            issues);
    }

    private static void ValidateDeclaredCount(
        string subject,
        int declared,
        int actual,
        ImmutableArray<ReferenceDataValidationIssue>.Builder issues)
    {
        if (declared != actual)
        {
            AddIssue(
                issues,
                ReferenceDataValidationIssueCode.CountMismatch,
                subject,
                $"Declared count {declared} does not match actual count {actual}.");
        }
    }

    private static void ValidateObservedBaselineCount(
        string name,
        int actual,
        int expected,
        ImmutableArray<ReferenceDataValidationIssue>.Builder issues)
    {
        if (actual != expected)
        {
            AddIssue(
                issues,
                ReferenceDataValidationIssueCode.CountMismatch,
                $"observedBaseline.{name}",
                $"Expected {expected}, but found {actual}.");
        }
    }

    private static void ValidateRoles(
        ImmutableArray<OrganizationRole> roles,
        string subject,
        ImmutableArray<ReferenceDataValidationIssue>.Builder issues)
    {
        if (roles.IsDefaultOrEmpty)
        {
            AddIssue(
                issues,
                ReferenceDataValidationIssueCode.InvalidRoleCombination,
                subject,
                "An organization must declare at least one supported role.");
            return;
        }

        var hasPrincipal = false;
        var hasIntermediary = false;
        var hasRepairer = false;
        foreach (var role in roles)
        {
            switch (role)
            {
                case OrganizationRole.Principal when !hasPrincipal:
                    hasPrincipal = true;
                    break;
                case OrganizationRole.Intermediary when !hasIntermediary:
                    hasIntermediary = true;
                    break;
                case OrganizationRole.Repairer when !hasRepairer:
                    hasRepairer = true;
                    break;
                default:
                    AddIssue(
                        issues,
                        ReferenceDataValidationIssueCode.InvalidRoleCombination,
                        subject,
                        $"Role '{role}' is unsupported or duplicated.");
                    break;
            }
        }
    }

    private static bool HasRole(Organization organization, OrganizationRole expectedRole)
    {
        foreach (var role in organization.Roles)
        {
            if (role == expectedRole)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasExactAlias(Organization organization, string alias)
    {
        foreach (var organizationAlias in organization.Aliases)
        {
            if (organizationAlias is not null && StringComparer.Ordinal.Equals(organizationAlias.Value, alias))
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateOccurrences(
        ImmutableArray<SourceOccurrence> occurrences,
        bool required,
        string subject,
        HashSet<string> sourceArtifactIds,
        ImmutableArray<ReferenceDataValidationIssue>.Builder issues)
    {
        if (occurrences.IsDefaultOrEmpty)
        {
            if (required)
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.MissingProvenance,
                    subject,
                    "At least one source occurrence is required.");
            }

            return;
        }

        for (var index = 0; index < occurrences.Length; index++)
        {
            var occurrence = occurrences[index];
            var occurrenceSubject = $"{subject}[{index}]";
            if (occurrence is null)
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.MissingProvenance,
                    occurrenceSubject,
                    "Source occurrence entry is required.");
                continue;
            }

            ValidateCandidateProvenance(
                occurrence.SourceArtifactId,
                occurrence.SourceSheet,
                occurrence.SourceRow,
                occurrenceSubject,
                sourceArtifactIds,
                issues);

            if (occurrence.RawFields is not null)
            {
                ValidateFields(occurrence.RawFields, $"{occurrenceSubject}.rawFields", issues);
            }

            if (occurrence.NormalizedFields is not null)
            {
                ValidateFields(occurrence.NormalizedFields, $"{occurrenceSubject}.normalizedFields", issues);
            }
        }
    }

    private static bool ValidateCandidateIdentity(
        string? candidateId,
        HashSet<string> ids,
        string subject,
        ImmutableArray<ReferenceDataValidationIssue>.Builder issues)
    {
        if (string.IsNullOrWhiteSpace(candidateId))
        {
            AddIssue(
                issues,
                ReferenceDataValidationIssueCode.InvalidIdentity,
                subject,
                "Candidate ID is required.");
            return false;
        }

        var validatedCandidateId = candidateId;
        if (!ids.Add(validatedCandidateId))
        {
            AddIssue(
                issues,
                ReferenceDataValidationIssueCode.DuplicateId,
                subject,
                $"Candidate ID '{validatedCandidateId}' is not unique.");
            return false;
        }

        return true;
    }

    private static void ValidateCandidateProvenance(
        string? sourceArtifactId,
        string? sourceSheet,
        int sourceRow,
        string subject,
        HashSet<string> sourceArtifactIds,
        ImmutableArray<ReferenceDataValidationIssue>.Builder issues)
    {
        if (string.IsNullOrWhiteSpace(sourceArtifactId))
        {
            AddIssue(
                issues,
                ReferenceDataValidationIssueCode.MissingProvenance,
                subject,
                "Source artifact ID is required.");
        }
        else
        {
            var validatedSourceArtifactId = sourceArtifactId;
            if (!sourceArtifactIds.Contains(validatedSourceArtifactId))
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.UnknownId,
                    subject,
                    $"Source artifact ID '{validatedSourceArtifactId}' is unknown.");
            }
        }

        if (!IsPresent(sourceSheet))
        {
            AddIssue(
                issues,
                ReferenceDataValidationIssueCode.MissingProvenance,
                subject,
                "Source sheet is required.");
        }

        if (sourceRow <= 0)
        {
            AddIssue(
                issues,
                ReferenceDataValidationIssueCode.MissingProvenance,
                subject,
                "Source row must be positive.");
        }
    }

    private static void ValidateFields(
        ImmutableDictionary<string, string?>? fields,
        string subject,
        ImmutableArray<ReferenceDataValidationIssue>.Builder issues)
    {
        if (fields is null || fields.Count == 0)
        {
            AddIssue(
                issues,
                ReferenceDataValidationIssueCode.MissingProvenance,
                subject,
                "Source field evidence is required.");
            return;
        }

        foreach (var field in fields)
        {
            if (!IsPresent(field.Key))
            {
                AddIssue(
                    issues,
                    ReferenceDataValidationIssueCode.MissingProvenance,
                    subject,
                    "Source field names must be present.");
                break;
            }
        }
    }

    private static void ValidateCandidateReviewState(
        CandidateReviewState reviewState,
        string subject,
        ImmutableArray<ReferenceDataValidationIssue>.Builder issues)
    {
        if (reviewState != CandidateReviewState.Unreviewed)
        {
            AddIssue(
                issues,
                ReferenceDataValidationIssueCode.NonUnreviewedImportedCandidate,
                subject,
                $"Imported candidate review state '{reviewState}' is not Unreviewed.");
        }
    }

    private static void ValidateDuplicateGroupId(
        string? duplicateGroupId,
        string subject,
        ImmutableArray<ReferenceDataValidationIssue>.Builder issues)
    {
        if (duplicateGroupId is not null && !IsPresent(duplicateGroupId))
        {
            AddIssue(
                issues,
                ReferenceDataValidationIssueCode.InvalidIdentity,
                subject,
                "Duplicate group ID must be null or a non-empty stable identity.");
        }
    }

    private static bool SameArtifact(SourceArtifact? left, SourceArtifact? right) =>
        left is not null &&
        right is not null &&
        StringComparer.Ordinal.Equals(left.Id, right.Id) &&
        StringComparer.Ordinal.Equals(left.Path, right.Path) &&
        StringComparer.Ordinal.Equals(left.ContentSha256, right.ContentSha256) &&
        StringComparer.Ordinal.Equals(left.Role, right.Role);

    private static bool IsNormalizedRepositoryRelativePosixPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path[0] == '/' ||
            path.Contains('\\', StringComparison.Ordinal) ||
            path.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        var segmentStart = 0;
        for (var index = 0; index <= path.Length; index++)
        {
            if (index != path.Length && path[index] != '/')
            {
                continue;
            }

            var length = index - segmentStart;
            if (length == 0 ||
                (length == 1 && path[segmentStart] == '.') ||
                (length == 2 && path[segmentStart] == '.' && path[segmentStart + 1] == '.'))
            {
                return false;
            }

            segmentStart = index + 1;
        }

        return true;
    }

    private static bool IsLowercaseSha256(string? value)
    {
        if (value is null || value.Length != 64)
        {
            return false;
        }

        foreach (var character in value)
        {
            if ((character < '0' || character > '9') && (character < 'a' || character > 'f'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPresent([NotNullWhen(true)] string? value) => !string.IsNullOrWhiteSpace(value);

    private static void AddIssue(
        ImmutableArray<ReferenceDataValidationIssue>.Builder issues,
        ReferenceDataValidationIssueCode code,
        string subject,
        string detail) => issues.Add(new ReferenceDataValidationIssue(code, subject, detail));
}
