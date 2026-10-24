namespace Pegasus.Core.Identity;

public sealed record CaseEngineerEligibility(bool AccountExists, bool IsEnabled);

public sealed record CaseEngineerChoice(Guid StaffId, string DisplayName);

public interface ICaseEngineerChoices
{
    Task<IReadOnlyList<CaseEngineerChoice>> GetAsync(
        ActionActor actor,
        CancellationToken cancellationToken);
}
