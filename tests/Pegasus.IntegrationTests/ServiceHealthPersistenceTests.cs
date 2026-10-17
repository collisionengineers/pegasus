using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.AiWork;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The Service health rows that read tables no other port exposes
/// — the Sent-items poll cursors, the intake dispatcher by state, and the
/// pending-work reads — resolved through the registered
/// adapters against a real database.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ServiceHealthPersistenceTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    private const string Mailbox = "instructions@collisionengineers.co.uk";

    [Fact]
    public async Task SentEvidencePollStatusReadsEachCursorRow()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using (var context = await database.CreateContextAsync())
        {
            context.ApprovedSentPollStates.AddRange(
                new ApprovedSentPollStateEntity
                {
                    MailboxId = "mailbox-b",
                    MailboxAddress = "reports@collisionengineers.co.uk",
                    SentFolderIdentity = "sent-b",
                    DueAtUtc = FixedUtcNow.AddMinutes(5),
                    LastCompletedAtUtc = null,
                    LastFailureCode = "graph_unavailable"
                },
                new ApprovedSentPollStateEntity
                {
                    MailboxId = "mailbox-a",
                    MailboxAddress = Mailbox,
                    SentFolderIdentity = "sent-a",
                    DueAtUtc = FixedUtcNow.AddMinutes(5),
                    LastCompletedAtUtc = FixedUtcNow.AddMinutes(-4),
                    LastFailureCode = null
                });
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IServiceHealthQueries>();

        var status = await queries.ListSentEvidencePollStatusAsync(CancellationToken.None);

        Assert.Collection(
            status,
            row => Assert.Equal(new SentEvidencePollStatus(Mailbox, FixedUtcNow.AddMinutes(5), FixedUtcNow.AddMinutes(-4), null), row),
            row => Assert.Equal(new SentEvidencePollStatus("reports@collisionengineers.co.uk", FixedUtcNow.AddMinutes(5), null, "graph_unavailable"), row));
    }

    [Fact]
    public async Task IntakeDispatchHealthCountsByStateAndNamesTheNewestCompletion()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using (var context = await database.CreateContextAsync())
        {
            context.IntakeStagedReceipts.AddRange(
                Staged("pending", completedAtUtc: null),
                Staged("processing", completedAtUtc: null),
                Staged("retry_scheduled", completedAtUtc: null),
                Staged("failed", completedAtUtc: null),
                Staged("completed", FixedUtcNow.AddMinutes(-9)),
                Staged("completed", FixedUtcNow.AddMinutes(-2)));
            await context.SaveChangesAsync();
        }

        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IServiceHealthQueries>();

        var health = await queries.GetIntakeDispatchHealthAsync(CancellationToken.None);

        Assert.Equal(new IntakeDispatchHealth(2, 1, 1, FixedUtcNow.AddMinutes(-2)), health);
    }

    [Fact]
    public async Task IntakeDispatchHealthOnAnEmptyQueueHasNoEvidence()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IServiceHealthQueries>();

        Assert.Equal(new IntakeDispatchHealth(0, 0, 0, null), await queries.GetIntakeDispatchHealthAsync(CancellationToken.None));
    }

    /// <summary>
    /// The snapshot reads its sources in the same statements whatever rows they
    /// hold. Overlapping the reads changes how long the page waits, not how many
    /// statements it sends, so the count must not move; the Health page adds the
    /// ingress switch (through the request's own context), the shell's reads and
    /// the metrics read below.
    /// </summary>
    [Fact]
    public async Task TheSnapshotSendsTheSameStatementsWhateverRowsItReads()
    {
        var counter = new SqlStatementCounter();
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureDatabase: options => options.AddInterceptors(counter));
        await using var scope = database.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var health = new GetServiceHealth(
            services.GetRequiredService<IApprovedMailboxPollStatusQueries>(),
            services.GetRequiredService<IServiceHealthQueries>(),
            services.GetRequiredService<GetRequestOperations>(),
            services.GetRequiredService<IAiJobQueries>(),
            services.GetRequiredService<ISendToAiControl>(),
            new IngressSwitch(),
            new EfAutomationActivityStore(
                services.GetRequiredService<IDbContextFactory<PegasusDbContext>>()),
            TimeProvider.System);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        counter.Reset();
        var snapshot = await health.ExecuteAsync(actor, CancellationToken.None);
        var withNoRows = counter.Count;

        await SeedHealthRowsAsync(database);
        counter.Reset();
        _ = await health.ExecuteAsync(actor, CancellationToken.None);
        var withRows = counter.Count;

        Assert.True(withNoRows > 0, "The interceptor observed no statements at all.");
        Assert.Equal(
            new[]
            {
                ServiceHealthArea.Intake,
                ServiceHealthArea.Custody,
                ServiceHealthArea.Ai,
                ServiceHealthArea.Automation
            },
            snapshot.Rows.Select(row => row.Area));
        Assert.True(
            withNoRows == withRows,
            $"The snapshot sent {withNoRows} statements with no rows and {withRows} once rows were added.");
    }

    /// <summary>The metrics read, which the page now runs beside the snapshot, sends the same statements whatever rows it reads.</summary>
    [Fact]
    public async Task TheAdministrationMetricsReadSendsTheSameStatementsWhateverRowsItReads()
    {
        var counter = new SqlStatementCounter();
        await using var database = await LocalDbTestDatabase.CreateAsync(
            configureDatabase: options => options.AddInterceptors(counter));
        await using var scope = database.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IAdministrationHealthMetricsQueries>();

        counter.Reset();
        await queries.GetAsync(FixedUtcNow, CancellationToken.None);
        var withNoRows = counter.Count;

        await SeedHealthRowsAsync(database);
        counter.Reset();
        await queries.GetAsync(FixedUtcNow, CancellationToken.None);
        var withRows = counter.Count;

        Assert.True(withNoRows > 0, "The interceptor observed no statements at all.");
        Assert.True(
            withNoRows == withRows,
            $"The metrics read sent {withNoRows} statements with no rows and {withRows} once rows were added.");
    }

    /// <summary>
    /// The Health page sends its shell's statements plus at most eleven of its
    /// own for the snapshot, whatever the row count. It does not read the
    /// administration metrics; the Logs page does. The shell is measured on the
    /// Administration hub, which renders the same layout, the same filters and
    /// the same middleware and reads nothing else. The Automation ingress switch
    /// is unconfigured in this host, so it sends none. The page reads the
    /// Sent-items cursors once, for the poll list.
    /// </summary>
    [Fact]
    public async Task TheHealthPageSendsTheShellsStatementsAndElevenOfItsOwnWhateverTheRowCount()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            useIntegrationTestAuthentication: true,
            commandInterceptor: counter);
        using var client = IntakeWebDriver.CreateClient(factory);
        // The first request to each page pays one-off start-up reads; count the next.
        _ = await IntakeWebDriver.GetHtmlAsync(client, "/Administration");
        _ = await IntakeWebDriver.GetHtmlAsync(client, "/Administration/Health");

        counter.Reset();
        _ = await IntakeWebDriver.GetHtmlAsync(client, "/Administration");
        var shell = counter.Count;
        Assert.True(shell > 0, "The interceptor observed no statements at all.");

        counter.Reset();
        _ = await IntakeWebDriver.GetHtmlAsync(client, "/Administration/Health");
        var withNoRows = counter.Count;
        const int snapshotStatements = 11;
        Assert.True(
            withNoRows <= shell + snapshotStatements,
            $"The Health page sent {withNoRows} statements; at most {shell + snapshotStatements} are allowed"
            + $" (the shell {shell} and the snapshot {snapshotStatements}).");
        Assert.Equal(1, counter.CountContaining("[ApprovedSentPollStates]"));

        await using (var context = await factory.Database.CreateContextAsync())
        {
            for (var index = 0; index < 3; index++)
            {
                context.ApprovedSentPollStates.Add(new ApprovedSentPollStateEntity
                {
                    MailboxId = $"health-page-{index}",
                    MailboxAddress = $"health-page-{index}@collisionengineers.co.uk",
                    SentFolderIdentity = $"sent-{index}",
                    DueAtUtc = FixedUtcNow.AddMinutes(5),
                    LastCompletedAtUtc = FixedUtcNow.AddMinutes(-4),
                    LastFailureCode = index == 0 ? "graph_unavailable" : null
                });
            }

            await context.SaveChangesAsync();
        }

        counter.Reset();
        var page = await IntakeWebDriver.GetHtmlAsync(client, "/Administration/Health");
        Assert.Contains("health-page-1@collisionengineers.co.uk", page, StringComparison.Ordinal);
        Assert.Equal(withNoRows, counter.Count);
        Assert.Equal(1, counter.CountContaining("[ApprovedSentPollStates]"));
    }

    private sealed class IngressSwitch : IAutomationIngressStatusQueries
    {
        public Task<bool> IsEnabledAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    }

    /// <summary>Rows in the tables the snapshot and the metrics read: staged receipts in every state and Sent-items cursors, one failing.</summary>
    private static async Task SeedHealthRowsAsync(LocalDbTestDatabase database)
    {
        await using var context = await database.CreateContextAsync();
        context.IntakeStagedReceipts.AddRange(
            Staged("pending", completedAtUtc: null),
            Staged("processing", completedAtUtc: null),
            Staged("retry_scheduled", completedAtUtc: null),
            Staged("failed", completedAtUtc: null),
            Staged("completed", FixedUtcNow.AddMinutes(-9)),
            Staged("completed", FixedUtcNow.AddMinutes(-2)));
        for (var index = 0; index < 3; index++)
        {
            context.ApprovedSentPollStates.Add(new ApprovedSentPollStateEntity
            {
                MailboxId = $"health-seed-{index}",
                MailboxAddress = $"health-seed-{index}@collisionengineers.co.uk",
                SentFolderIdentity = $"sent-{index}",
                DueAtUtc = FixedUtcNow.AddMinutes(5),
                LastCompletedAtUtc = FixedUtcNow.AddMinutes(-4),
                LastFailureCode = index == 0 ? "graph_unavailable" : null
            });
        }

        await context.SaveChangesAsync();
    }

    private static IntakeStagedReceiptEntity Staged(string state, DateTimeOffset? completedAtUtc)
    {
        var id = Guid.NewGuid();
        return new()
        {
            Id = id,
            SourceFileName = "instruction.eml",
            MediaType = "message/rfc822",
            SourceLength = 1,
            SourceHash = new string('1', 64),
            SourceChannel = "mailbox",
            ExternalReceiptToken = $"health:{id:N}",
            ReceivedAtUtc = FixedUtcNow.AddMinutes(-10),
            Actor = "worker",
            StorageKey = $"staged/{id:N}",
            StagedAtUtc = FixedUtcNow.AddMinutes(-10),
            WorkItem = new IntakeWorkItemEntity
            {
                Id = Guid.NewGuid(),
                StagedReceiptId = id,
                OperationKey = $"health-op:{id:N}",
                State = state,
                AttemptCount = state == "pending" ? 0 : 1,
                DueAtUtc = FixedUtcNow,
                CompletedAtUtc = completedAtUtc
            }
        };
    }

    private static ExternalWorkItemEntity Work(Guid caseId, string kind, string state) =>
        new()
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
            Kind = kind,
            OperationKey = $"work:{Guid.NewGuid():N}",
            State = state,
            AttemptCount = state == "completed" ? 1 : 0,
            DueAtUtc = FixedUtcNow
        };
}
