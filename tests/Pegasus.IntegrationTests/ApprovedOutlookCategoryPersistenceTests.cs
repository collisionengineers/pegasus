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

        var operationConflict = await Assert.ThrowsAsync<ApprovedOutlookCategoryUpdateException>(() =>
            update.ExecuteAsync(request with { DisplayName = "A different category" }, default));
        Assert.Equal(ApprovedOutlookCategoryUpdateError.OperationConflict, operationConflict.Error);

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

        var stale = await Assert.ThrowsAsync<ApprovedOutlookCategoryUpdateException>(() =>
            update.ExecuteAsync(request with
            {
                ExpectedVersion = created.Version,
                OperationKey = Guid.NewGuid().ToString("N")
            }, default));
        Assert.Equal(ApprovedOutlookCategoryUpdateError.VersionConflict, stale.Error);
        Assert.Equal(disabled.Version, stale.CurrentVersion);

        await using var context = await database.CreateContextAsync();
        var history = await context.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateType == "approved_outlook_category")
            .OrderBy(item => item.PolicyVersion)
            .ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Null(history[0].BeforeJson);
        Assert.Contains("\"DisplayName\":\"Awaiting engineer\"", history[0].AfterJson, StringComparison.Ordinal);
        Assert.Contains("\"Version\":1", history[0].AfterJson, StringComparison.Ordinal);
        Assert.Contains("\"State\":0", history[0].AfterJson, StringComparison.Ordinal);
        Assert.Contains("\"Version\":1", history[1].BeforeJson, StringComparison.Ordinal);
        Assert.Contains("\"State\":0", history[1].BeforeJson, StringComparison.Ordinal);
        Assert.Contains("\"Version\":2", history[1].AfterJson, StringComparison.Ordinal);
        Assert.Contains("\"State\":1", history[1].AfterJson, StringComparison.Ordinal);
        Assert.Equal("Retire the approved display name", history[1].Reason);
        Assert.Equal(1, await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS [Value] FROM [ApprovedOutlookCategories]").SingleAsync());
    }

    [Fact]
    public async Task ConcurrentReplayIsIdempotentAndCompetingUpdateCommitsOnce()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var id = Guid.NewGuid();
        var create = new UpdateApprovedOutlookCategoryRequest(
            id, "Awaiting engineer", ApprovedOutlookCategoryState.Active, 0,
            actor, "Approve the exact display name", Guid.NewGuid().ToString("N"));

        var replays = await Task.WhenAll(
            ExecuteAsync(database, create),
            ExecuteAsync(database, create));
        Assert.All(replays, result => Assert.Equal(replays[0], result));

        var first = create with
        {
            DisplayName = "Awaiting allocation", ExpectedVersion = 1,
            Reason = "Choose the first competing update", OperationKey = Guid.NewGuid().ToString("N")
        };
        var second = create with
        {
            DisplayName = "Awaiting review", ExpectedVersion = 1,
            Reason = "Choose the second competing update", OperationKey = Guid.NewGuid().ToString("N")
        };
        var outcomes = await Task.WhenAll(CaptureAsync(database, first), CaptureAsync(database, second));

        Assert.Single(outcomes, outcome => outcome.Result is not null);
        var loser = Assert.Single(outcomes, outcome => outcome.Error is not null).Error!;
        var conflict = Assert.IsType<ApprovedOutlookCategoryUpdateException>(loser);
        Assert.Equal(ApprovedOutlookCategoryUpdateError.VersionConflict, conflict.Error);
        Assert.Equal(2, conflict.CurrentVersion);

        await using var context = await database.CreateContextAsync();
        Assert.Equal(1, await context.Database.SqlQuery<int>(
            $"SELECT COUNT(*) AS [Value] FROM [ApprovedOutlookCategories]").SingleAsync());
        Assert.Equal(2, await context.ActionHistory.CountAsync(
            item => item.AggregateType == "approved_outlook_category"));
    }

    private static async Task<ApprovedOutlookCategory> ExecuteAsync(
        LocalDbTestDatabase database,
        UpdateApprovedOutlookCategoryRequest request)
    {
        await using var scope = database.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UpdateApprovedOutlookCategory>()
            .ExecuteAsync(request, default);
    }

    private static async Task<(ApprovedOutlookCategory? Result, Exception? Error)> CaptureAsync(
        LocalDbTestDatabase database,
        UpdateApprovedOutlookCategoryRequest request)
    {
        try
        {
            return (await ExecuteAsync(database, request), null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }
}
