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
/// separate cursors, a disabled mailbox that stops without losing anything, and the
/// read-only configuration fallback that keeps the already-deployed mailbox polling
/// while its identities are unset.
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
                "SELECT MailboxAddress FROM ApprovedInboxPollStates WHERE MailboxId = 'instructions'"));
        Assert.Equal(
            SecondAddress,
            await database.ScalarAsync<string>(
                $"SELECT MailboxAddress FROM ApprovedInboxPollStates WHERE MailboxId = '{SecondMailboxId}'"));
        Assert.NotEqual(
            await database.ScalarAsync<string>(
                "SELECT [Cursor] FROM ApprovedInboxPollStates WHERE MailboxId = 'instructions'"),
            await database.ScalarAsync<string>(
                $"SELECT [Cursor] FROM ApprovedInboxPollStates WHERE MailboxId = '{SecondMailboxId}'"));
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
    public async Task ConfigurationSuppliesIdentitiesOnlyForTheMatchingUnsetMailbox()
    {
        using var workspace = new MailboxWorkspace();
        await using var database = await CreateDatabaseAsync(workspace);

        // A second Approved inbound row with no identities and no configuration match.
        await database.ExecuteAsync(AddSecondMailboxSql(
            state: "Approved",
            mailboxIdentity: null,
            inboxFolderIdentity: null));

        await using var scope = database.CreateAsyncScope();
        var pollable = await scope.ServiceProvider
            .GetRequiredService<IApprovedIntakeMailboxes>()
            .ListPollableAsync(CancellationToken.None);

        var only = Assert.Single(pollable);
        Assert.Equal(SeededAddress, only.Address);
        Assert.Equal("instructions", only.MailboxId);
        Assert.Equal(DefaultFolder, only.InboxFolderIdentity);
    }

    [Fact]
    public async Task SavedIdentitiesWinOverConfigurationForTheSameAddress()
    {
        using var workspace = new MailboxWorkspace();
        await using var database = await CreateDatabaseAsync(workspace);

        // The seeded row now carries identities that differ from configuration.
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
        Assert.Equal("saved-mailbox", only.MailboxId);
        Assert.Equal("saved-inbox", only.InboxFolderIdentity);
    }

    [Fact]
    public async Task WebReadsTheEstateAsSavedAndNeverBorrowsConfiguredIdentities()
    {
        // No AddLocalApprovedInbox, so no configuration fallback: this is the Web shape.
        await using var database = await LocalDbTestDatabase.CreateAsync();

        await using var scope = database.CreateAsyncScope();
        var pollable = await scope.ServiceProvider
            .GetRequiredService<IApprovedIntakeMailboxes>()
            .ListPollableAsync(CancellationToken.None);

        // The seeded row has no saved identities, so nothing is pollable from Web's view.
        Assert.Empty(pollable);
    }

    private static readonly ActionActor WorkerActor =
        ActionActor.SystemWorker("approved-inbox-poller");

    private static Task<string> SecondCursorAsync(LocalDbTestDatabase database) =>
        database.ScalarAsync<string>(
            $"SELECT [Cursor] FROM ApprovedInboxPollStates WHERE MailboxId = '{SecondMailboxId}'");

    private static Task<LocalDbTestDatabase> CreateDatabaseAsync(MailboxWorkspace workspace) =>
        LocalDbTestDatabase.CreateAsync(
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

    private static string AddSecondMailboxSql(
        string state,
        string? mailboxIdentity,
        string? inboxFolderIdentity) =>
        $"""
        INSERT INTO ApprovedMailboxes
            (Id, Address, AllowInboundIntake, AllowSentEvidence, State,
             MailboxIdentity, InboxFolderIdentity, SentFolderIdentity, Version)
        VALUES
            ('{SecondMailboxRowId:D}', '{SecondAddress}', 1, 0, '{state}',
             {Literal(mailboxIdentity)}, {Literal(inboxFolderIdentity)}, NULL, 1);
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
