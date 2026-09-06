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

public sealed record CaseEngineerChoice(Guid StaffId, string DisplayName);

public interface ICaseEngineerChoices
{
    Task<IReadOnlyList<CaseEngineerChoice>> GetAsync(
        ActionActor actor,
        CancellationToken cancellationToken);
}
