using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class WorkCentreDismissalPersistenceTests
{
    private static readonly DateTimeOffset At = new(2031, 5, 1, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A later dismissal moves the record's instant on and records who; a
    /// dismissal that arrives with an earlier instant never moves it back.
    /// </summary>
    [Fact]
    public async Task ALaterDismissalMovesTheInstantOnAndAnEarlierOneNeverBack()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IWorkCentreDismissalStore>();
        var recordId = Guid.NewGuid();
        var first = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var second = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        await store.DismissAsync(recordId, At, first, CancellationToken.None);
        await store.DismissAsync(recordId, At.AddHours(1), second, CancellationToken.None);
        await store.DismissAsync(recordId, At.AddMinutes(30), first, CancellationToken.None);

        Assert.Equal(At.AddHours(1), (await store.ListAsync([recordId], CancellationToken.None))[recordId]);
        await using var context = await database.CreateContextAsync();
        Assert.Equal(
            second.SubjectId,
            context.Set<WorkCentreDismissalEntity>()
                .Single(item => item.RecordId == recordId).DismissedBySubjectId);
    }

    [Fact]
    public async Task ConcurrentDismissalsOfOneRecordLeaveOneRow()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var recordId = Guid.NewGuid();
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(async index =>
        {
            await using var scope = database.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IWorkCentreDismissalStore>()
                .DismissAsync(recordId, At.AddSeconds(index), staff, CancellationToken.None);
        }));

        await using var context = await database.CreateContextAsync();
        var row = Assert.Single(context.Set<WorkCentreDismissalEntity>().ToArray());
        Assert.Equal(At.AddSeconds(7), row.DismissedAtUtc);
    }

    /// <summary>The Work Centre asks about every row at once: more records than SQL Server's parameter limit.</summary>
    [Fact]
    public async Task ListingReadsOnlyTheDismissedOfManyRecords()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IWorkCentreDismissalStore>();
        var dismissed = Guid.NewGuid();
        await store.DismissAsync(dismissed, At, ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]), CancellationToken.None);
        var asked = Enumerable.Range(0, 2500).Select(_ => Guid.NewGuid()).Append(dismissed).ToArray();

        var read = await store.ListAsync(asked, CancellationToken.None);

        Assert.Equal(dismissed, Assert.Single(read).Key);
    }
}
