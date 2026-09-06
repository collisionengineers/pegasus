using Pegasus.Core.Identity;

namespace Pegasus.Core.Cases;

public sealed record ClaimSourceRecord(
    Guid Id, string Name, string? ContactName, string? Telephone, string? Email,
    string? Notes, bool Active, long Version, DateTimeOffset UpdatedAtUtc);
public sealed record SaveClaimSourceRequest(
    ActionActor Actor, Guid Id, long ExpectedVersion, string Name, string? ContactName,
    string? Telephone, string? Email, string? Notes, bool Active, string Reason,
    string OperationKey);
public interface IClaimSourceAdministration
{
    Task<ClaimSourceRecord> SaveAsync(SaveClaimSourceRequest request, CancellationToken cancellationToken);
}
public interface IClaimSourceQueries
{
    Task<ClaimSourceRecord?> GetAsync(ActionActor actor, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ClaimSourceRecord>> SearchAsync(
        ActionActor actor, string prefix, int limit, CancellationToken cancellationToken);
}
