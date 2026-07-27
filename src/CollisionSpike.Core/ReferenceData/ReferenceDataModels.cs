using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace CollisionSpike.Core.ReferenceData;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProviderDomainPackage(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("source")] ProviderDomainSource Source,
    [property: JsonPropertyName("providers")] ImmutableArray<ProviderDomainReference> Providers);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProviderDomainSource(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("contentSha256")] string ContentSha256,
    [property: JsonPropertyName("sheet")] string Sheet,
    [property: JsonPropertyName("rowCount")] int RowCount);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProviderDomainReference(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("sourceRow")] int SourceRow,
    [property: JsonPropertyName("domainSuffixes")] ImmutableArray<string> DomainSuffixes);

public enum ProviderDomainCandidateStatus
{
    Found = 1,
    Unknown = 2,
    Ambiguous = 3,
    InvalidSuffix = 4,
    PackageNotFound = 5,
    PackageRejected = 6
}

public sealed record ProviderDomainCandidates(
    ProviderDomainCandidateStatus Status,
    ImmutableArray<string> ProviderCodes);
