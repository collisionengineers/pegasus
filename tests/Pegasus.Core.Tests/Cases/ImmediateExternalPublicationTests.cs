using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

public sealed class ImmediateExternalPublicationTests
{
    [Fact]
    public async Task AcceptancePublishesTheCustodyWorkCreatedByItsCommittedTransaction()
    {
        var workItemId = Guid.NewGuid();
        var publisher = new RecordingPublisher();
        var acceptance = new AcceptIntake(
            new AcceptanceStore(workItemId),
            new ConfigurationStore(),
            new InspectionModeStore(),
            publisher);

        var result = await acceptance.ExecuteAsync(AcceptanceRequest(), CancellationToken.None);

        Assert.False(result.IsDuplicate);
        Assert.Equal([workItemId], publisher.WorkItemIds);
    }

    [Fact]
    public async Task ReplacementPublishesTheCustodyWorkCreatedByItsCommittedTransaction()
    {
        var workItemId = Guid.NewGuid();
        var publisher = new RecordingPublisher();
        var replacement = new CreateLinkedReplacement(new ReplacementStore(workItemId), publisher);

        var result = await replacement.ExecuteAsync(
            new(
                Guid.NewGuid(),
                0,
                Staff(),
                "replacement-1",
                "Correct source identity.",
                "lease-token",
                "QDOS"),
            CancellationToken.None);

        Assert.False(result.IsDuplicate);
        Assert.Equal([workItemId], publisher.WorkItemIds);
    }

    private static AcceptIntakeRequest AcceptanceRequest() =>
        new(
            Guid.NewGuid(),
            0,
            Staff(),
            "acceptance-1",
            "Accept confirmed instruction.",
            CaseType.Inspection,
            "QDOS",
            new(true, true, true, true));

    private static ActionActor Staff() =>
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    private sealed class RecordingPublisher : ICommittedExternalWorkPublisher
    {
        public List<Guid> WorkItemIds { get; } = [];

        public Task PublishAsync(Guid workItemId, CancellationToken cancellationToken)
        {
            WorkItemIds.Add(workItemId);
            return Task.CompletedTask;
        }
    }

    private sealed class AcceptanceStore(Guid workItemId) : ICaseAcceptanceStore
    {
        public Task<CaseAcceptanceOutcome> AcceptAsync(
            CaseAcceptanceRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(Outcome(workItemId));
    }

    private sealed class ReplacementStore(Guid workItemId) : ILinkedCaseReplacementStore
    {
        public Task<CaseAcceptanceOutcome> CreateAsync(
            CreateLinkedReplacementRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(Outcome(workItemId));
    }

    private sealed class ConfigurationStore : ICaseWorkflowConfiguration
    {
        public Task<CaseWorkflowConfiguration> GetCurrentAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new CaseWorkflowConfiguration("test", 1));
    }

    private sealed class InspectionModeStore : IProviderInspectionModeStore
    {
        public Task<CaseInspectionMode?> GetForPrincipalAsync(
            string principalCode,
            CancellationToken cancellationToken) =>
            Task.FromResult<CaseInspectionMode?>(null);
    }

    private static CaseAcceptanceOutcome Outcome(Guid workItemId) =>
        new(
            new CaseIdentity(Guid.NewGuid(), "QDOS", 2031, 1, "QDS31001"),
            CaseInitialState.Review,
            CaseCustodyState.Pending,
            workItemId,
            false);
}
