namespace Pegasus.Core.Identity;

public sealed record CaseEngineerEligibility(
    bool AccountExists,
    bool IsEnabled,
    bool HasEngineerRole);

public interface ICaseEngineerEligibility
{
    Task<CaseEngineerEligibility> GetAsync(
        Guid staffId,
        CancellationToken cancellationToken);
}
