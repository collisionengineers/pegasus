using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class UnroadworthyReasonBankPersistenceTests
{
    private static readonly string[] StandardWordings =
    [
        "as the steering and suspension geometry has been compromised",
        "as the rear lamp assemblies are inoperative",
        "as the headlamp assemblies are inoperative",
        "as the vehicle presents sharp edges likely to cause injury",
        "as the supplementary restraint systems have deployed",
        "as structural distortion is evident to the body shell",
        "as there is a loss of essential fluids",
    ];

    /// <summary>
    /// The seven standard wordings are the bank's first rows, in the order the
    /// code held them (operator, 11 November 2026; R953).
    /// </summary>
    [Fact]
    public async Task AFreshDatabaseOffersTheStandardWordingsInTheirOrder()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IUnroadworthyReasonBankStore>();

        Assert.Equal(StandardWordings, (await store.ListAsync(CancellationToken.None)).Select(item => item.Text));
    }

    /// <summary>
    /// The migration to one list (R952): a wording two Principals saved keeps
    /// its oldest row, every other saved wording stays, and the standard
    /// wordings list ahead of them.
    /// </summary>
    [Fact]
    public async Task TheMigrationMergesEveryPrincipalsWordingsBehindTheStandardOnes()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync(migrate: false);
        await using (var context = await database.CreateContextAsync())
        {
            await context.Database.GetService<IMigrator>()
                .MigrateAsync("20261110233000_PhotoCaptureSettleIndexes");
        }
        await database.ExecuteAsync(
            """
            INSERT INTO [UnroadworthyReasons] ([Id], [PrincipalCode], [Text], [CreatedBy], [CreatedAtUtc]) VALUES
                ('11111111-1111-4111-8111-111111111111', N'QDOS', N'the brake line is severed', N'first', '2026-11-03T09:00:00+00:00'),
                ('22222222-2222-4222-8222-222222222222', N'ACME', N'The brake line is severed', N'second', '2026-11-04T09:00:00+00:00'),
                ('33333333-3333-4333-8333-333333333333', N'ACME', N'the wheels are missing', N'second', '2026-11-05T09:00:00+00:00');
            """);

        await database.MigrateAsync();

        await using var scope = database.CreateAsyncScope();
        var wordings = await scope.ServiceProvider.GetRequiredService<IUnroadworthyReasonBankStore>()
            .ListAsync(CancellationToken.None);
        string[] expected = [.. StandardWordings, "the brake line is severed", "the wheels are missing"];
        Assert.Equal(expected, wordings.Select(item => item.Text));
        Assert.Equal("first", wordings[7].CreatedBy);
    }

    /// <summary>
    /// Reword and Remove act only on the wording the page rendered (R953): a
    /// row changed since is stale, a wording the bank already offers is
    /// refused, and a repeated Save finds its own wording in place. Each write
    /// drops the kept list.
    /// </summary>
    [Fact]
    public async Task AnAdministratorRewordsAndRemovesOnlyTheWordingThePageRendered()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var store = services.GetRequiredService<IUnroadworthyReasonBankStore>();
        var reword = services.GetRequiredService<RewordUnroadworthyReason>();
        var remove = services.GetRequiredService<RemoveUnroadworthyReason>();
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var saved = await services.GetRequiredService<ISaveUnroadworthyReason>().ExecuteAsync(
            new SaveUnroadworthyReasonRequest("The brake line is severed.", administrator),
            CancellationToken.None);
        Assert.NotNull(saved);

        Assert.Equal(UnroadworthyReasonChange.Stale, await reword.ExecuteAsync(
            new(saved.Id, "the brake line was severed", "the brake pipe is severed", administrator),
            CancellationToken.None));
        Assert.Equal(UnroadworthyReasonChange.AlreadyOffered, await reword.ExecuteAsync(
            new(saved.Id, saved.Text, StandardWordings[6], administrator), CancellationToken.None));
        Assert.Equal(UnroadworthyReasonChange.Done, await reword.ExecuteAsync(
            new(saved.Id, saved.Text, "The brake pipe is severed.", administrator), CancellationToken.None));
        Assert.Equal(UnroadworthyReasonChange.Done, await reword.ExecuteAsync(
            new(saved.Id, saved.Text, "The brake pipe is severed.", administrator), CancellationToken.None));
        Assert.Equal(
            "the brake pipe is severed",
            Assert.Single(await store.ListAsync(CancellationToken.None), item => item.Id == saved.Id).Text);

        Assert.Equal(UnroadworthyReasonChange.Stale, await remove.ExecuteAsync(
            new(saved.Id, saved.Text, administrator), CancellationToken.None));
        Assert.Equal(UnroadworthyReasonChange.Done, await remove.ExecuteAsync(
            new(saved.Id, "the brake pipe is severed", administrator), CancellationToken.None));
        Assert.Equal(StandardWordings, (await store.ListAsync(CancellationToken.None)).Select(item => item.Text));
    }

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
            "The brake line is severed.",
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]));
        var secondRequest = new SaveUnroadworthyReasonRequest(
            "The brake line is severed.",
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]));

        // The list is kept now (#1950), but concurrent misses are not combined:
        // both saves still read it and meet at the barrier.
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
            .CountAsync(row => row.Text == "the brake line is severed"));
    }

    [Fact]
    public async Task TheWordingsAreKeptUntilAWriteEvictsThem()
    {
        var counter = new SqlStatementCounter();
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureDatabase: options => options.AddInterceptors(counter));
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IUnroadworthyReasonBankStore>();
        var cold = await store.ListAsync(CancellationToken.None);

        counter.Reset();
        Assert.Equal(cold, await store.ListAsync(CancellationToken.None));
        Assert.Equal(0, counter.Count);

        var saved = await scope.ServiceProvider.GetRequiredService<ISaveUnroadworthyReason>().ExecuteAsync(
            new SaveUnroadworthyReasonRequest(
                "The steering rack is detached.",
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer])),
            CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Contains(
            await store.ListAsync(CancellationToken.None),
            item => item.Id == saved.Id);
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
