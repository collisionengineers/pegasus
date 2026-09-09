using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class WorkflowConfigurationPersistenceTests
{
    [Fact]
    public async Task ConfigurationPersistsRequirementsAndRejectsStaleOwnerWithoutPartialChanges()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var factory = new PooledDbContextFactory<PegasusDbContext>(new DbContextOptionsBuilder<PegasusDbContext>()
            .UseSqlServer(database.ConnectionString).Options);
        var store = new EfWorkflowConfigurationStore(factory, TimeProvider.System);
        var scopes = new EfEditScopeStore(factory, TimeProvider.System);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var before = await store.GetCurrentAsync(default);
        var lease = await scopes.ClaimAsync(new(EditScopeKind.NamedConfiguration, GetWorkflowConfiguration.RecordId,
            before.PolicyVersion, actor, "edit-config"), default);
        var request = new UpdateWorkflowConfigurationRequest(before.PolicyVersion, actor, "Images optional", "save-config")
        { RequireImages = false, ChaseIntervalDays = 12, EditLeaseToken = lease.Token };
        var saved = await store.UpdateAsync(request, default);
        Assert.True(CaseCompletenessPolicy.Evaluate(new(true, false), saved).SatisfiesPolicy);
        Assert.Equal(12, (await store.GetCurrentAsync(default)).ChaseIntervalDays);
        await Assert.ThrowsAsync<WorkflowConfigurationVersionConflictException>(() =>
            store.UpdateAsync(request with { OperationKey = "stale-save", ChaseIntervalDays = 20 }, default));
        Assert.Equal(12, (await store.GetCurrentAsync(default)).ChaseIntervalDays);
        Assert.Equal(saved, await store.UpdateAsync(request, default));
    }

    [Fact]
    public async Task RateCardEditsRequireLeaseAndPersistVersionedDisabledState()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var factory = new PooledDbContextFactory<PegasusDbContext>(new DbContextOptionsBuilder<PegasusDbContext>()
            .UseSqlServer(database.ConnectionString).Options);
        var store = new EfLabourRateCardStore(factory, TimeProvider.System);
        var scopes = new EfEditScopeStore(factory, TimeProvider.System);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var card = await store.SaveAsync(new(Guid.NewGuid(), "Panel and paint", 65m, true, 0, actor,
            "Create rate", "new-rate", ""), default);
        var change = new SaveLabourRateCardRequest(card.Id, card.Name, 80m, false, card.Version, actor,
            "Retire rate", "edit-rate", "");
        await Assert.ThrowsAsync<EditScopeExpiredException>(() => store.SaveAsync(change, default));
        var lease = await scopes.ClaimAsync(new(EditScopeKind.LabourRateCard, card.Id, card.Version, actor, "claim-rate"), default);
        var saved = await store.SaveAsync(change with { EditLeaseToken = lease.Token }, default);
        Assert.False(saved.Enabled);
        Assert.Equal(2, saved.Version);
        Assert.Equal(80m, Assert.Single(await store.ListAsync(default)).HourlyRate);
    }

    [Fact]
    public void ConfiguredCalendarIntervalCrossesClockChangeWithoutChangingLocalTime()
    {
        var start = new DateTimeOffset(2026, 3, 28, 10, 30, 0, TimeSpan.Zero);
        var chase = CaseChaseSchedule.FirstChaseAt(start, 2);
        Assert.Equal(new DateTimeOffset(2026, 3, 30, 9, 30, 0, TimeSpan.Zero), chase);
        var remaining = CaseChaseSchedule.RemainingInterval(chase, start);
        Assert.Equal(chase, CaseChaseSchedule.ResumeAt(start, remaining));
    }
}
