using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class UnroadworthyReasonBankPersistenceTests
{
    [Fact]
    public async Task ConcurrentCoreSavesTurnTheUniqueKeyRaceIntoOneSavedResultAndOneNull()
    {
        var listReads = new ListReadBarrier();
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureDatabase: options => options.AddInterceptors(listReads));
        await using var firstScope = database.CreateAsyncScope();
        await using var secondScope = database.CreateAsyncScope();

        var firstSave = firstScope.ServiceProvider.GetRequiredService<ISaveUnroadworthyReason>();
        var secondSave = secondScope.ServiceProvider.GetRequiredService<ISaveUnroadworthyReason>();
        var firstRequest = new SaveUnroadworthyReasonRequest(
            "QDOS", "The brake line is severed.",
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]));
        var secondRequest = new SaveUnroadworthyReasonRequest(
            "QDOS", "The brake line is severed.",
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]));

        UnroadworthyReason?[]? results = null;
        var exception = await Record.ExceptionAsync(async () =>
        {
            results = await Task.WhenAll(
                firstSave.ExecuteAsync(firstRequest, CancellationToken.None),
                secondSave.ExecuteAsync(secondRequest, CancellationToken.None));
        });

        Assert.Null(exception);
        Assert.NotNull(results);
        Assert.Equal(1, results!.Count(result => result is not null));

        await using var context = await database.CreateContextAsync();
        Assert.Equal(1, await context.UnroadworthyReasons
            .CountAsync(row => row.PrincipalCode == "QDOS" && row.Text == "the brake line is severed"));
    }

    private sealed class ListReadBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource<bool> released =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int reads;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.CommandSource == CommandSource.LinqQuery
                && command.CommandText.Contains("[UnroadworthyReasons]", StringComparison.Ordinal))
            {
                var read = Interlocked.Increment(ref reads);
                if (read <= 2)
                {
                    if (read == 2)
                    {
                        released.TrySetResult(true);
                    }
                    await released.Task.WaitAsync(cancellationToken);
                }
            }

            return result;
        }
    }
}
