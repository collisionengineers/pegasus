using Pegasus.Core.Identity;

namespace Pegasus.Core.Cases;

public enum OrganizationDirectoryRole { Repairer, Storage, InspectionLocation }
public sealed record OrganizationDirectoryRecord(
    Guid Id, OrganizationDirectoryRole Role, string Name, string? ContactName,
    string? Telephone, string? Email, string Address, string? Postcode,
    bool Active, long Version, string SourceKind, Guid SourceRecordId,
    long SourceVersion, DateTimeOffset UpdatedAtUtc);
public sealed record OrganizationDirectoryQuery(
    ActionActor Actor, string Prefix, OrganizationDirectoryRole? Role, int Limit = 20);
public interface IOrganizationDirectoryQueries
{
    Task<IReadOnlyList<OrganizationDirectoryRecord>> SearchAsync(
        OrganizationDirectoryQuery query, CancellationToken cancellationToken);
}
