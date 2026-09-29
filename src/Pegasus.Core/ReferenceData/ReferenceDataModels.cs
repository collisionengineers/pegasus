using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Pegasus.Core.ReferenceData;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrincipalDomainPackage(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("source")] PrincipalDomainSource Source,
    [property: JsonPropertyName("providers")] ImmutableArray<PrincipalDomainReference> Principals);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrincipalDomainSource(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("contentSha256")] string ContentSha256,
    [property: JsonPropertyName("sheet")] string Sheet,
    [property: JsonPropertyName("rowCount")] int RowCount);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PrincipalDomainReference(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("sourceRow")] int SourceRow,
    [property: JsonPropertyName("domainSuffixes")] ImmutableArray<string> DomainSuffixes);

public enum PrincipalDomainCandidateStatus
{
    Found = 1,
    Unknown = 2,
    Ambiguous = 3,
    InvalidSuffix = 4,
    PackageNotFound = 5,
    PackageRejected = 6
}

public sealed record PrincipalDomainCandidates(
    PrincipalDomainCandidateStatus Status,
    ImmutableArray<string> PrincipalCodes);
