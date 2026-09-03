namespace Pegasus.Core.Workflow;

public sealed class DefaultCaseWorkflowConfiguration : ICaseWorkflowConfiguration
{
    private static readonly CaseWorkflowConfiguration Current = new(
        PolicyKey: "case-workflow",
        PolicyVersion: 1);

    public Task<CaseWorkflowConfiguration> GetCurrentAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Current);
}
