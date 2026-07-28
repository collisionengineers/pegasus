using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;

namespace Pegasus.Core.ReferenceData;

public static class ReferenceDataPolicy
{
    public const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = JsonSerializerOptions.Strict;

    public static ProviderDomainValidationResult Validate(
        ProviderDomainPackageVersion requested,
        ReadOnlySpan<byte> canonicalPackageBytes)
    {
        var issues = ImmutableArray.CreateBuilder<ProviderDomainValidationIssue>();

        if (requested.SchemaVersion != SupportedSchemaVersion)
        {
            AddIssue(issues, ProviderDomainValidationIssueCode.SchemaMismatch, "$.requested.schemaVersion");
        }

        if (!IsCanonicalVersion(requested.Version))
        {
            AddIssue(issues, ProviderDomainValidationIssueCode.VersionMismatch, "$.requested.version");
        }

        if (!IsLowercaseSha256(requested.PackageSha256))
        {
            AddIssue(issues, ProviderDomainValidationIssueCode.PackageHashMismatch, "$.requested.packageSha256");
        }
        else
        {
            Span<byte> actualHash = stackalloc byte[32];
            SHA256.HashData(canonicalPackageBytes, actualHash);
            if (!MatchesLowercaseSha256(actualHash, requested.PackageSha256))
            {
                AddIssue(issues, ProviderDomainValidationIssueCode.PackageHashMismatch, "$.requested.packageSha256");
            }
        }

        ProviderDomainPackage? package;
        try
        {
            package = JsonSerializer.Deserialize<ProviderDomainPackage>(canonicalPackageBytes, SerializerOptions);
        }
        catch (JsonException)
        {
            AddIssue(issues, ProviderDomainValidationIssueCode.InvalidJson, "$");
            return new ProviderDomainValidationResult(issues.ToImmutable());
        }

        if (package is null)
        {
            AddIssue(issues, ProviderDomainValidationIssueCode.InvalidJson, "$");
            return new ProviderDomainValidationResult(issues.ToImmutable());
        }

        if (package.SchemaVersion != SupportedSchemaVersion || package.SchemaVersion != requested.SchemaVersion)
        {
            AddIssue(issues, ProviderDomainValidationIssueCode.SchemaMismatch, "$.schemaVersion");
        }

        if (!IsCanonicalVersion(package.Version) ||
            !StringComparer.Ordinal.Equals(package.Version, requested.Version))
        {
            AddIssue(issues, ProviderDomainValidationIssueCode.VersionMismatch, "$.version");
        }

        ValidateSource(package.Source, issues);
        ValidateProviders(package.Providers, package.Source?.RowCount ?? 0, issues);

        return new ProviderDomainValidationResult(issues.ToImmutable());
    }

    public static bool IsValidPackageVersion(ProviderDomainPackageVersion packageVersion) =>
        packageVersion.SchemaVersion == SupportedSchemaVersion &&
        IsCanonicalVersion(packageVersion.Version) &&
        IsLowercaseSha256(packageVersion.PackageSha256);

    public static bool IsCanonicalDomainSuffix(string? domainSuffix)
    {
        if (string.IsNullOrEmpty(domainSuffix) ||
            domainSuffix.Length > 254 ||
            domainSuffix[0] != '@')
        {
            return false;
        }

        var domain = domainSuffix.AsSpan(1);
        if (domain.Length == 0 || domain.IndexOf('@') >= 0)
        {
            return false;
        }

        var labelCount = 0;
        var labelStart = 0;
        for (var index = 0; index <= domain.Length; index++)
        {
            if (index != domain.Length && domain[index] != '.')
            {
                continue;
            }

            var label = domain[labelStart..index];
            if (!IsCanonicalDomainLabel(label))
            {
                return false;
            }

            labelCount++;
            labelStart = index + 1;
        }

        return labelCount >= 2;
    }

    public static ProviderDomainCandidates CreateCandidates(
        string? domainSuffix,
        ImmutableArray<string> providerCodes)
    {
        if (!IsCanonicalDomainSuffix(domainSuffix))
        {
            return EmptyCandidates(ProviderDomainCandidateStatus.InvalidSuffix);
        }

        if (providerCodes.IsDefaultOrEmpty)
        {
            return EmptyCandidates(ProviderDomainCandidateStatus.Unknown);
        }

        var sorted = providerCodes.ToArray();
        Array.Sort(sorted, StringComparer.Ordinal);

        var uniqueCount = 0;
        foreach (var code in sorted)
        {
            if (uniqueCount == 0 || !StringComparer.Ordinal.Equals(sorted[uniqueCount - 1], code))
            {
                sorted[uniqueCount++] = code;
            }
        }

        if (uniqueCount != sorted.Length)
        {
            Array.Resize(ref sorted, uniqueCount);
        }

        var codes = ImmutableArray.Create(sorted);
        var status = codes.Length switch
        {
            0 => ProviderDomainCandidateStatus.Unknown,
            1 => ProviderDomainCandidateStatus.Found,
            _ => ProviderDomainCandidateStatus.Ambiguous
        };
        return new ProviderDomainCandidates(status, codes);
    }

    public static bool TryExtractDomainSuffix(
        string? emailAddress,
        [NotNullWhen(true)] out string? domainSuffix)
    {
        domainSuffix = null;
        if (string.IsNullOrWhiteSpace(emailAddress))
        {
            return false;
        }

        var trimmed = emailAddress.Trim();
        var separator = trimmed.LastIndexOf('@');
        if (separator <= 0 || separator == trimmed.Length - 1)
        {
            return false;
        }

        var candidate = $"@{trimmed[(separator + 1)..].ToLowerInvariant()}";
        if (!IsCanonicalDomainSuffix(candidate))
        {
            return false;
        }

        domainSuffix = candidate;
        return true;
    }

    private static void ValidateSource(
        ProviderDomainSource? source,
        ImmutableArray<ProviderDomainValidationIssue>.Builder issues)
    {
        if (source is null)
        {
            AddIssue(issues, ProviderDomainValidationIssueCode.MissingValue, "$.source");
            return;
        }

        if (!IsNormalizedRepositoryRelativePosixPath(source.Path))
        {
            AddIssue(issues, ProviderDomainValidationIssueCode.InvalidSource, "$.source.path");
        }

        if (!IsLowercaseSha256(source.ContentSha256))
        {
            AddIssue(issues, ProviderDomainValidationIssueCode.InvalidSource, "$.source.contentSha256");
        }

        if (string.IsNullOrWhiteSpace(source.Sheet) ||
            source.Sheet.Length > 31 ||
            HasControlCharacter(source.Sheet))
        {
            AddIssue(issues, ProviderDomainValidationIssueCode.InvalidSource, "$.source.sheet");
        }

        if (source.RowCount <= 0)
        {
            AddIssue(issues, ProviderDomainValidationIssueCode.InvalidSource, "$.source.rowCount");
        }
    }

    private static void ValidateProviders(
        ImmutableArray<ProviderDomainReference> providers,
        int sourceRowCount,
        ImmutableArray<ProviderDomainValidationIssue>.Builder issues)
    {
        if (providers.IsDefault)
        {
            AddIssue(issues, ProviderDomainValidationIssueCode.MissingValue, "$.providers");
            return;
        }

        if (providers.IsEmpty)
        {
            AddIssue(issues, ProviderDomainValidationIssueCode.EmptyPackage, "$.providers");
            return;
        }

        var providerCodes = new HashSet<string>(StringComparer.Ordinal);
        var sourceRows = new HashSet<int>();

        for (var index = 0; index < providers.Length; index++)
        {
            var providerPath = $"$.providers[{index}]";
            var provider = providers[index];
            if (provider is null)
            {
                AddIssue(issues, ProviderDomainValidationIssueCode.MissingValue, providerPath);
                continue;
            }

            if (!IsCanonicalProviderCode(provider.Code))
            {
                AddIssue(issues, ProviderDomainValidationIssueCode.InvalidProviderCode, $"{providerPath}.code");
            }
            else if (!providerCodes.Add(provider.Code))
            {
                AddIssue(issues, ProviderDomainValidationIssueCode.DuplicateProviderCode, $"{providerPath}.code");
            }

            if (provider.SourceRow <= 0 || provider.SourceRow > sourceRowCount)
            {
                AddIssue(issues, ProviderDomainValidationIssueCode.InvalidSourceRow, $"{providerPath}.sourceRow");
            }
            else if (!sourceRows.Add(provider.SourceRow))
            {
                AddIssue(issues, ProviderDomainValidationIssueCode.DuplicateSourceRow, $"{providerPath}.sourceRow");
            }

            if (provider.DomainSuffixes.IsDefault)
            {
                AddIssue(issues, ProviderDomainValidationIssueCode.MissingValue, $"{providerPath}.domainSuffixes");
                continue;
            }

            if (provider.DomainSuffixes.IsEmpty)
            {
                AddIssue(issues, ProviderDomainValidationIssueCode.MissingValue, $"{providerPath}.domainSuffixes");
                continue;
            }

            var suffixes = new HashSet<string>(StringComparer.Ordinal);
            for (var suffixIndex = 0; suffixIndex < provider.DomainSuffixes.Length; suffixIndex++)
            {
                var suffixPath = $"{providerPath}.domainSuffixes[{suffixIndex}]";
                var suffix = provider.DomainSuffixes[suffixIndex];
                if (!IsCanonicalDomainSuffix(suffix))
                {
                    AddIssue(issues, ProviderDomainValidationIssueCode.InvalidDomainSuffix, suffixPath);
                }
                else if (!suffixes.Add(suffix))
                {
                    AddIssue(issues, ProviderDomainValidationIssueCode.DuplicateDomainSuffix, suffixPath);
                }
            }
        }
    }

    private static bool IsCanonicalVersion(string? value) =>
        IsSegmentedAsciiIdentifier(value, 64, lowercase: true);

    private static bool IsCanonicalProviderCode(string? value) =>
        IsSegmentedAsciiIdentifier(value, 20, lowercase: false);

    private static bool IsSegmentedAsciiIdentifier(string? value, int maxLength, bool lowercase)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maxLength || value[0] == '-' || value[^1] == '-')
        {
            return false;
        }

        var previousWasSeparator = false;
        foreach (var character in value)
        {
            if (character == '-')
            {
                if (previousWasSeparator)
                {
                    return false;
                }

                previousWasSeparator = true;
                continue;
            }

            var letter = lowercase
                ? character is >= 'a' and <= 'z'
                : character is >= 'A' and <= 'Z';
            if (!letter && character is not (>= '0' and <= '9'))
            {
                return false;
            }

            previousWasSeparator = false;
        }

        return true;
    }

    private static bool IsCanonicalDomainLabel(ReadOnlySpan<char> label)
    {
        if (label.Length is < 1 or > 63 || label[0] == '-' || label[^1] == '-')
        {
            return false;
        }

        foreach (var character in label)
        {
            if (character == '-' ||
                character is >= 'a' and <= 'z' ||
                character is >= '0' and <= '9')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsNormalizedRepositoryRelativePosixPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.Length > 512 ||
            path[0] == '/' ||
            path[^1] == '/' ||
            path.Contains('\\', StringComparison.Ordinal) ||
            path.Contains(':', StringComparison.Ordinal) ||
            path.Contains("//", StringComparison.Ordinal) ||
            HasControlCharacter(path))
        {
            return false;
        }

        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment is "." or "..")
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasControlCharacter(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLowercaseSha256(string? value)
    {
        if (value is null || value.Length != 64)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesLowercaseSha256(ReadOnlySpan<byte> hash, string expected)
    {
        const string hex = "0123456789abcdef";
        for (var index = 0; index < hash.Length; index++)
        {
            var value = hash[index];
            if (expected[index * 2] != hex[value >> 4] ||
                expected[(index * 2) + 1] != hex[value & 0x0f])
            {
                return false;
            }
        }

        return true;
    }

    private static ProviderDomainCandidates EmptyCandidates(ProviderDomainCandidateStatus status) =>
        new(status, ImmutableArray<string>.Empty);

    private static void AddIssue(
        ImmutableArray<ProviderDomainValidationIssue>.Builder issues,
        ProviderDomainValidationIssueCode code,
        string subject) =>
        issues.Add(new ProviderDomainValidationIssue(code, subject));
}
