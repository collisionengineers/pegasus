using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class ApprovedOutlookCategoryPersistenceTests
{
    [Fact]
    public async Task CatalogueIsVersionedReplaySafeUniqueAndDisabledRatherThanDeleted()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var update = scope.ServiceProvider.GetRequiredService<UpdateApprovedOutlookCategory>();
        var store = scope.ServiceProvider.GetRequiredService<IApprovedOutlookCategoryStore>();
        var resolver = scope.ServiceProvider.GetRequiredService<IApprovedOutlookCategoryResolver>();
        var id = Guid.NewGuid();
        var request = new UpdateApprovedOutlookCategoryRequest(
            id, "Awaiting engineer", ApprovedOutlookCategoryState.Active, 0,
            actor, "Approve the Outlook display name", Guid.NewGuid().ToString("N"));

        var created = await update.ExecuteAsync(request, default);
        Assert.Equal(created, await update.ExecuteAsync(request, default));
        Assert.Equal(created, await resolver.ResolveActiveAsync(id, default));

        var duplicate = request with
        {
            CategoryId = Guid.NewGuid(), DisplayName = "AWAITING ENGINEER",
            OperationKey = Guid.NewGuid().ToString("N")
        };
        var conflict = await Assert.ThrowsAsync<ApprovedOutlookCategoryUpdateException>(() =>
            update.ExecuteAsync(duplicate, default));
        Assert.Equal(ApprovedOutlookCategoryUpdateError.DuplicateDisplayName, conflict.Error);

        var disabled = await update.ExecuteAsync(request with
        {
            State = ApprovedOutlookCategoryState.Disabled, ExpectedVersion = created.Version,
            Reason = "Retire the approved display name", OperationKey = Guid.NewGuid().ToString("N")
        }, default);
        Assert.Equal(ApprovedOutlookCategoryState.Disabled, disabled.State);
        Assert.Null(await resolver.ResolveActiveAsync(id, default));
        Assert.Single(await store.ListAsync(default));

        await using var context = await database.CreateContextAsync();
        Assert.Equal(2, await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS [Value] FROM [ActionHistory] WHERE [AggregateType] = 'approved_outlook_category'").SingleAsync());
        Assert.Equal(1, await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS [Value] FROM [ApprovedOutlookCategories]").SingleAsync());
    }
}
