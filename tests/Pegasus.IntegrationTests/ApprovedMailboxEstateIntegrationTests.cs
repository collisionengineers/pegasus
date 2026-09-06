using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The approved estate, not deployment configuration, decides which mailboxes inbound
/// intake polls. These prove that against the real database: two mailboxes with
/// separate cursors and a disabled mailbox that stops without losing anything.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ApprovedMailboxEstateIntegrationTests
{
    private const string SeededAddress = "instructions@collisionengineers.co.uk";
    private const string SecondAddress = "claims@collisionengineers.co.uk";
    private const string SecondMailboxId = "claims-mailbox";
    private const string SecondFolder = "claims-inbox";
    private const string DefaultFolder = "inbox";

    private static readonly Guid SecondMailboxRowId =
        Guid.Parse("7c2f1a5e-9d10-4a4f-9d63-2f1c6b0a44e1");

    [Fact]
    public async Task PollsTwoApprovedMailboxesAndKeepsSeparatePollStates()
    {
        using var workspace = new MailboxWorkspace();
        workspace.WriteMessage(DefaultFolder, "0001-first.eml");
        workspace.WriteMessage(SecondFolder, "0001-second.eml");

        await using var database = await CreateDatabaseAsync(workspace);
        await database.ExecuteAsync(AddSecondMailboxSql(
            state: "Approved",
            mailboxIdentity: SecondMailboxId,
            inboxFolderIdentity: SecondFolder));

        await using (var scope = database.CreateAsyncScope())
        {
            var poll = scope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
            Assert.Equal(2, await poll.ExecuteAsync(10, WorkerActor, CancellationToken.None));
            Assert.Equal(0, await poll.ExecuteAsync(10, WorkerActor, CancellationToken.None));
        }

        // One cursor row per mailbox, each bound to its own address.
        Assert.Equal(
            2L,
            await database.ScalarAsync<long>("SELECT COUNT(*) FROM ApprovedInboxPollStates"));
        Assert.Equal(
            SeededAddress,
            await database.ScalarAsync<string>(
                $"SELECT MailboxAddress FROM ApprovedInboxPollStates WHERE ApprovedMailboxId = '{TestMailboxId.From("instructions"):D}'"));
        Assert.Equal(
            SecondAddress,
            await database.ScalarAsync<string>(
                $"SELECT MailboxAddress FROM ApprovedInboxPollStates WHERE ApprovedMailboxId = '{SecondMailboxRowId:D}'"));
        Assert.NotEqual(
            await database.ScalarAsync<string>(
                $"SELECT [Cursor] FROM ApprovedInboxPollStates WHERE ApprovedMailboxId = '{TestMailboxId.From("instructions"):D}'"),
            await database.ScalarAsync<string>(
                $"SELECT [Cursor] FROM ApprovedInboxPollStates WHERE ApprovedMailboxId = '{SecondMailboxRowId:D}'"));
        Assert.Equal(
            2L,
            await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeStagedReceipts"));
    }

    [Fact]
    public async Task DisablingAMailboxStopsPollingAndPreservesItsCursor()
    {
        using var workspace = new MailboxWorkspace();
        workspace.WriteMessage(SecondFolder, "0001-before.eml");

        await using var database = await CreateDatabaseAsync(workspace);
        await database.ExecuteAsync(AddSecondMailboxSql(
            state: "Approved",
            mailboxIdentity: SecondMailboxId,
            inboxFolderIdentity: SecondFolder));

        await using (var scope = database.CreateAsyncScope())
        {
            var poll = scope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
            Assert.Equal(1, await poll.ExecuteAsync(10, WorkerActor, CancellationToken.None));
        }

        var cursorBeforeDisable = await SecondCursorAsync(database);
        var receiptsBeforeDisable = await database.ScalarAsync<long>(
            "SELECT COUNT(*) FROM IntakeStagedReceipts");

        // Disable, then post new mail that would otherwise be ingested.
        await database.ExecuteAsync(
            $"UPDATE ApprovedMailboxes SET State = 'Disabled' WHERE Id = '{SecondMailboxRowId:D}';");
        workspace.WriteMessage(SecondFolder, "0002-after.eml");

        await using (var scope = database.CreateAsyncScope())
        {
            var poll = scope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
            Assert.Equal(0, await poll.ExecuteAsync(10, WorkerActor, CancellationToken.None));
        }

        // Nothing new was ingested, and nothing already retained was touched: the cursor
        // row survives byte-identical, so re-enabling resumes rather than replays.
        Assert.Equal(
            receiptsBeforeDisable,
            await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeStagedReceipts"));
        Assert.Equal(cursorBeforeDisable, await SecondCursorAsync(database));

        await database.ExecuteAsync(
            $"UPDATE ApprovedMailboxes SET State = 'Approved' WHERE Id = '{SecondMailboxRowId:D}';");

        await using (var scope = database.CreateAsyncScope())
        {
            var poll = scope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
            // Only the message added while it was disabled: resumed, not re-ingested.
            Assert.Equal(1, await poll.ExecuteAsync(10, WorkerActor, CancellationToken.None));
        }

        Assert.Equal(
            receiptsBeforeDisable + 1,
            await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeStagedReceipts"));
    }

    [Fact]
    public async Task SavedIdentitiesDefineThePollableMailbox()
    {
        using var workspace = new MailboxWorkspace();
        await using var database = await CreateDatabaseAsync(workspace);

        // The seeded row becomes pollable once its identities are saved.
        await database.ExecuteAsync(
            """
            UPDATE ApprovedMailboxes
            SET MailboxIdentity = 'saved-mailbox', InboxFolderIdentity = 'saved-inbox'
            WHERE Address = 'instructions@collisionengineers.co.uk';
            """);

        await using var scope = database.CreateAsyncScope();
        var pollable = await scope.ServiceProvider
            .GetRequiredService<IApprovedIntakeMailboxes>()
            .ListPollableAsync(CancellationToken.None);

        var only = Assert.Single(pollable);
        Assert.Equal("saved-mailbox", only.GraphMailboxId);
        Assert.Equal("saved-inbox", only.InboxFolderIdentity);
    }

    [Fact]
    public async Task WebReadsTheEstateAsSavedAndNeverBorrowsConfiguredIdentities()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        await using var scope = database.CreateAsyncScope();
        var pollable = await scope.ServiceProvider
            .GetRequiredService<IApprovedIntakeMailboxes>()
            .ListPollableAsync(CancellationToken.None);

        // The seeded row has no saved identities, so nothing is pollable from Web's view.
        Assert.Empty(pollable);
    }

    [Fact]
    public async Task OldGenerationMaintenanceSuccessCannotOverwriteTheCurrentSubscription()
    {
        await using var database = await CreateSubscriptionRaceDatabaseAsync();
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IApprovedMailboxSubscriptionStore>();

        await Assert.ThrowsAsync<ApprovedMailboxSubscriptionMaintenanceLostException>(() =>
            store.SaveAsync(
                Subscription("old-subscription", 1, "stale-success"),
                "old-subscription",
                CancellationToken.None));

        Assert.Equal("current-subscription", await CurrentSubscriptionIdAsync(database));
        Assert.Equal(2L, await CurrentSubscriptionGenerationAsync(database));
        Assert.Equal("current-state", await CurrentSubscriptionFailureAsync(database));
    }

    [Fact]
    public async Task OldGenerationMaintenanceFailureCannotStampTheCurrentSubscription()
    {
        await using var database = await CreateSubscriptionRaceDatabaseAsync();
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IApprovedMailboxSubscriptionStore>();

        await Assert.ThrowsAsync<ApprovedMailboxSubscriptionMaintenanceLostException>(() =>
            store.RecordMaintenanceFailureAsync(
                TestMailboxId.From("instructions"),
                1,
                "old-subscription",
                "stale-failure",
                new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero),
                CancellationToken.None));

        Assert.Equal("current-subscription", await CurrentSubscriptionIdAsync(database));
        Assert.Equal(2L, await CurrentSubscriptionGenerationAsync(database));
        Assert.Equal("current-state", await CurrentSubscriptionFailureAsync(database));
    }

    [Fact]
    public async Task SameGenerationRecreationReplacesTheExpectedPriorSubscription()
    {
        await using var database = await CreateInitialSubscriptionDatabaseAsync();
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IApprovedMailboxSubscriptionStore>();

        await store.SaveAsync(
            Subscription("replacement-subscription", 1, null),
            "old-subscription",
            CancellationToken.None);

        Assert.Equal("replacement-subscription", await CurrentSubscriptionIdAsync(database));
        Assert.Equal(1L, await CurrentSubscriptionGenerationAsync(database));
    }

    [Fact]
    public async Task ConcurrentSameGenerationReplacementCannotBeOverwritten()
    {
        await using var database = await CreateInitialSubscriptionDatabaseAsync();
        await database.ExecuteAsync(
            "UPDATE ApprovedMailboxSubscriptions SET SubscriptionId = 'concurrent-subscription';");
        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IApprovedMailboxSubscriptionStore>();

        await Assert.ThrowsAsync<ApprovedMailboxSubscriptionMaintenanceLostException>(() =>
            store.SaveAsync(
                Subscription("stale-result-subscription", 1, null),
                "old-subscription",
                CancellationToken.None));

        Assert.Equal("concurrent-subscription", await CurrentSubscriptionIdAsync(database));
    }

    private static readonly ActionActor WorkerActor =
        ActionActor.SystemWorker("approved-inbox-poller");

    private static ApprovedMailboxSubscription Subscription(
        string subscriptionId,
        long generation,
        string? failureCode) => new(
            TestMailboxId.From("instructions"),
            subscriptionId,
            "users/instructions/mailFolders/inbox/messages",
            new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero),
            ApprovedMailboxSubscriptionLifecycleState.Active,
            new DateTimeOffset(2026, 9, 6, 11, 0, 0, TimeSpan.Zero),
            failureCode,
            generation);

    private static async Task<LocalDbTestDatabase> CreateSubscriptionRaceDatabaseAsync()
    {
        var database = await CreateInitialSubscriptionDatabaseAsync();
        var mailboxId = TestMailboxId.From("instructions");
        await database.ExecuteAsync(
            $"""
            UPDATE ApprovedMailboxes SET MailboxGeneration = 2 WHERE Id = '{mailboxId:D}';
            UPDATE ApprovedMailboxSubscriptions
            SET SubscriptionId = 'current-subscription', Generation = 2,
                LastMaintenanceFailureCode = 'current-state'
            WHERE ApprovedMailboxId = '{mailboxId:D}';
            """);
        return database;
    }

    private static async Task<LocalDbTestDatabase> CreateInitialSubscriptionDatabaseAsync()
    {
        var database = await LocalDbTestDatabase.CreateAsync();
        var mailboxId = TestMailboxId.From("instructions");
        await database.ExecuteAsync(
            $"""
            UPDATE ApprovedMailboxes
            SET MailboxIdentity = 'instructions', InboxFolderIdentity = 'inbox',
                ActivatedAtUtc = '2000-01-01T00:00:00+00:00', MailboxGeneration = 1
            WHERE Id = '{mailboxId:D}';
            """);
        await using var scope = database.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IApprovedMailboxSubscriptionStore>()
            .SaveAsync(Subscription("old-subscription", 1, null), null, CancellationToken.None);
        return database;
    }

    private static Task<string> CurrentSubscriptionIdAsync(LocalDbTestDatabase database) =>
        database.ScalarAsync<string>("SELECT SubscriptionId FROM ApprovedMailboxSubscriptions");

    private static Task<long> CurrentSubscriptionGenerationAsync(LocalDbTestDatabase database) =>
        database.ScalarAsync<long>("SELECT Generation FROM ApprovedMailboxSubscriptions");

    private static Task<string> CurrentSubscriptionFailureAsync(LocalDbTestDatabase database) =>
        database.ScalarAsync<string>("SELECT LastMaintenanceFailureCode FROM ApprovedMailboxSubscriptions");

    private static Task<string> SecondCursorAsync(LocalDbTestDatabase database) =>
        database.ScalarAsync<string>(
            $"SELECT [Cursor] FROM ApprovedInboxPollStates WHERE ApprovedMailboxId = '{SecondMailboxRowId:D}'");

    private static async Task<LocalDbTestDatabase> CreateDatabaseAsync(MailboxWorkspace workspace)
    {
        var database = await LocalDbTestDatabase.CreateAsync(
            localArtifactRootFactory: _ => workspace.ArtifactRoot,
            configureServices: services =>
            {
                services.AddScoped<IIntakeWorkStore, EfIntakeWorkStore>();
                services.AddScoped<ReceiveIntake>();
                services.AddLocalApprovedInbox(_ => new(
                    LocalApprovedInboxOptions.RequiredRuntimeProfile,
                    "instructions",
                    SeededAddress,
                    workspace.Root,
                    DefaultFolder));
            });
        await database.ExecuteAsync(
            $"""
            UPDATE ApprovedMailboxes
            SET MailboxIdentity = 'instructions', InboxFolderIdentity = '{DefaultFolder}',
                ActivatedAtUtc = '2000-01-01T00:00:00+00:00'
            WHERE Id = '{TestMailboxId.From("instructions"):D}';
            """);
        return database;
    }

    private static string AddSecondMailboxSql(
        string state,
        string? mailboxIdentity,
        string? inboxFolderIdentity) =>
        $"""
        INSERT INTO ApprovedMailboxes
            (Id, Address, AllowInboundIntake, AllowSentEvidence, State,
             MailboxIdentity, InboxFolderIdentity, SentFolderIdentity, ActivatedAtUtc,
             MailboxGeneration, Version)
        VALUES
            ('{SecondMailboxRowId:D}', '{SecondAddress}', 1, 0, '{state}',
             {Literal(mailboxIdentity)}, {Literal(inboxFolderIdentity)}, NULL,
             '2000-01-01T00:00:00+00:00', 1, 1);
        """;

    private static string Literal(string? value) =>
        value is null ? "NULL" : $"'{value}'";

    private sealed class MailboxWorkspace : IDisposable
    {
        internal MailboxWorkspace()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "Pegasus.ApprovedMailboxEstateIntegrationTests",
                Guid.NewGuid().ToString("N"));
            ArtifactRoot = Path.Combine(Root, "artifacts");
            Directory.CreateDirectory(Path.Combine(Root, DefaultFolder));
            Directory.CreateDirectory(Path.Combine(Root, SecondFolder));
        }

        internal string Root { get; }

        internal string ArtifactRoot { get; }

        internal void WriteMessage(string folderIdentity, string fileName) =>
            File.WriteAllBytes(
                Path.Combine(Root, folderIdentity, fileName),
                CreateMessage(fileName));

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        /// <summary>
        /// Distinct content per file, so two mailboxes cannot accidentally share a source
        /// identity and make an isolation failure look like a pass.
        /// </summary>
        private static byte[] CreateMessage(string fileName)
        {
            var message = new MimeMessage
            {
                Subject = $"Estate fixture {fileName}",
                Body = new TextPart("plain") { Text = $"Fixture body for {fileName}." }
            };
            message.From.Add(new MailboxAddress("Sender", "sender@example.invalid"));
            message.To.Add(new MailboxAddress("Approved Inbox", SeededAddress));
            using var stream = new MemoryStream();
            message.WriteTo(stream);
            return stream.ToArray();
        }
    }
}
