using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
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

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task WipeBoundarySurvivesMissingPollStateAndGraphScopeRefresh(
        bool previouslyPolled,
        bool refreshScope)
    {
        using var workspace = new MailboxWorkspace();
        await using var database = await CreateDatabaseAsync(workspace);
        await using var scope = database.CreateAsyncScope();
        var mailboxes = scope.ServiceProvider.GetRequiredService<IApprovedIntakeMailboxes>();
        var pollStore = scope.ServiceProvider.GetRequiredService<IApprovedInboxPollStore>();
        var mailboxId = TestMailboxId.From("instructions");
        var mailbox = Assert.IsType<ApprovedIntakeMailbox>(
            await mailboxes.GetPollableAsync(mailboxId, CancellationToken.None));
        var cutoff = new DateTimeOffset(2031, 9, 7, 10, 0, 0, TimeSpan.Zero);
        if (previouslyPolled)
        {
            var previous = Assert.IsType<ApprovedInboxPollLease>(await pollStore.ClaimAsync(
                mailbox, cutoff.AddMinutes(-2), TimeSpan.FromMinutes(1), CancellationToken.None));
            await pollStore.CompleteAsync(
                mailboxId, previous.LeaseToken, "old-cursor", cutoff.AddMinutes(-1), CancellationToken.None);
        }

        var sql = await File.ReadAllTextAsync(Path.Combine(
            CorpusPackage.RepositoryRoot, "scripts", "Reset-IntakeMailBoundary.sql"));
        await using (var context = await scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<PegasusDbContext>>().CreateDbContextAsync())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            await context.Database.ExecuteSqlRawAsync(sql,
                new Microsoft.Data.SqlClient.SqlParameter("@CutoffUtc", cutoff));
            await transaction.CommitAsync();
        }

        if (refreshScope)
        {
            await database.ExecuteAsync($"""
                UPDATE ApprovedMailboxes
                SET InboxFolderIdentity = 'rebound-inbox', MailboxGeneration = MailboxGeneration + 1
                WHERE Id = '{mailboxId:D}';
                """);
        }

        var currentMailbox = Assert.IsType<ApprovedIntakeMailbox>(
            await mailboxes.GetPollableAsync(mailboxId, CancellationToken.None));
        Assert.Equal(mailbox.ActivatedAtUtc, currentMailbox.ActivatedAtUtc);
        Assert.Equal(mailbox.Address, currentMailbox.Address);
        var claim = Assert.IsType<ApprovedInboxPollLease>(await pollStore.ClaimAsync(
            currentMailbox, cutoff, TimeSpan.FromMinutes(1), CancellationToken.None));
        Assert.Equal(cutoff, claim.StartBoundaryUtc);
        Assert.Equal(mailbox.ActivatedAtUtc, claim.ActivatedAtUtc);
        Assert.Equal(currentMailbox.Generation, claim.Generation);
        Assert.Null(claim.Cursor);

        await pollStore.AdvanceAsync(
            mailboxId, claim.LeaseToken, "recovery-cursor", cutoff, CancellationToken.None);
        await pollStore.CompleteNotificationAsync(mailboxId, claim.LeaseToken, CancellationToken.None);
        var nextClaim = Assert.IsType<ApprovedInboxPollLease>(await pollStore.ClaimAsync(
            currentMailbox, cutoff, TimeSpan.FromMinutes(1), CancellationToken.None));
        Assert.Equal("recovery-cursor", nextClaim.Cursor);
        Assert.Equal(cutoff, nextClaim.StartBoundaryUtc);
    }

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

    /// <summary>
    /// FABLE-06: the exact same message (same RFC Internet Message-ID) delivered
    /// to two approved mailboxes is two durable messages, not one deduplicated
    /// occurrence.
    /// </summary>
    /// <remarks>
    /// FRD-08 "Inbound mailbox identity" names the rule directly: "Mailbox
    /// identity plus RFC Internet Message-ID is the durable message and intake
    /// duplicate boundary" and "the same RFC identity may occur independently in
    /// two approved mailboxes." <c>PollApprovedInbox</c>'s
    /// <c>externalReceiptToken</c> (<c>MailboxIntake.cs</c>, message preparation)
    /// folds the mailbox identity in beside the message identity —
    /// <c>$"{mailboxId.Length}:{mailboxId}{sourceMessageIdentity}"</c> — so the
    /// same Message-ID from two mailboxes derives two distinct tokens.
    /// <c>DurableIntake.ExecuteAsync</c>'s de-duplication
    /// (<c>workStore.FindBySourceIdentityAsync</c>) looks a source up by that
    /// token alone, so the second mailbox's delivery finds no existing receipt
    /// under its own token and stages its own row rather than folding into the
    /// first mailbox's. The design therefore stages two receipts and allocates
    /// two staged-receipt tokens for one piece of mail seen twice, never one.
    /// </remarks>
    [Fact]
    public async Task SameMessageDeliveredToTwoApprovedMailboxesStagesTwoReceipts()
    {
        const string sharedMessageId = "fable-06-shared-message@example.invalid";
        using var workspace = new MailboxWorkspace();
        workspace.WriteMessage(DefaultFolder, "0001-shared.eml", messageId: sharedMessageId);
        workspace.WriteMessage(SecondFolder, "0001-shared.eml", messageId: sharedMessageId);

        await using var database = await CreateDatabaseAsync(workspace);
        await database.ExecuteAsync(AddSecondMailboxSql(
            state: "Approved",
            mailboxIdentity: SecondMailboxId,
            inboxFolderIdentity: SecondFolder));

        await using (var scope = database.CreateAsyncScope())
        {
            var poll = scope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
            Assert.Equal(2, await poll.ExecuteAsync(10, WorkerActor, CancellationToken.None));
        }

        // Two staged receipts, one per mailbox — the identical Message-ID never
        // collapses them into one, because the mailbox identity is folded into
        // the receipt token beside it.
        Assert.Equal(
            2L,
            await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeStagedReceipts"));
        Assert.Equal(
            2L,
            await database.ScalarAsync<long>(
                "SELECT COUNT(DISTINCT ExternalReceiptToken) FROM IntakeStagedReceipts"));
        Assert.Equal(
            2L,
            await database.ScalarAsync<long>("SELECT COUNT(*) FROM RetainedMailboxMessages"));
    }

    /// <summary>
    /// A change notification can name a message the mail source cannot show yet:
    /// Exchange has accepted it but not replicated it to the folder the delta reads.
    /// The wake must not invent it, must not advance the recovery cursor past it,
    /// and — once it is visible — both routes together must leave exactly one
    /// occurrence.
    /// </summary>
    [Fact]
    public async Task NotifiedMessageInvisibleAtWakeIsStillIngestedExactlyOnce()
    {
        using var workspace = new MailboxWorkspace();
        await using var database = await CreateDatabaseAsync(workspace);
        var mailboxId = TestMailboxId.From("instructions");
        var generation = await database.ScalarAsync<long>(
            $"SELECT MailboxGeneration FROM ApprovedMailboxes WHERE Id = '{mailboxId:D}'");

        // The notification arrives before the message is visible to any read.
        await using (var scope = database.CreateAsyncScope())
        {
            var poll = scope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
            Assert.Equal(0, await poll.ExecuteNotificationAsync(
                mailboxId,
                generation,
                new string('A', 64),
                WorkerActor,
                CancellationToken.None));
        }

        Assert.Equal(0L, await AdvancedInboxCursorsAsync(database));
        Assert.Equal(0L, await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeStagedReceipts"));

        // Replication completes and the same notification is redelivered.
        const string fileName = "0001-notified.eml";
        workspace.WriteMessage(DefaultFolder, fileName);
        var immutableMessageId = ImmutableMessageId(workspace, DefaultFolder, fileName);
        await using (var scope = database.CreateAsyncScope())
        {
            var poll = scope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
            Assert.Equal(1, await poll.ExecuteNotificationAsync(
                mailboxId, generation, immutableMessageId, WorkerActor, CancellationToken.None));
            Assert.Equal(1, await poll.ExecuteNotificationAsync(
                mailboxId, generation, immutableMessageId, WorkerActor, CancellationToken.None));
        }

        // A wake reads one message; the recovery cursor stays exactly where it was,
        // so the sweep still owns the message and cannot skip it.
        Assert.Equal(0L, await AdvancedInboxCursorsAsync(database));

        await using (var scope = database.CreateAsyncScope())
        {
            var poll = scope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
            Assert.Equal(1, await poll.ExecuteAsync(10, WorkerActor, CancellationToken.None));
        }

        Assert.Equal(1L, await AdvancedInboxCursorsAsync(database));
        Assert.Equal(
            1L,
            await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeStagedReceipts"));
        Assert.Equal(
            1L,
            await database.ScalarAsync<long>("SELECT COUNT(*) FROM RetainedMailboxMessages"));
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

    /// <summary>
    /// Re-enabling is a fresh start, not a resumption: the stored cursor and start
    /// boundary are replaced, so mail that arrived while the mailbox was disabled
    /// never becomes a backlog, and the new generation makes the mailbox a
    /// subscription-maintenance candidate again while its old subscription row
    /// survives to be replaced.
    /// </summary>
    [Fact]
    public async Task ReEnablingAMailboxStartsAFreshCycleAndNeedsANewSubscription()
    {
        using var workspace = new MailboxWorkspace();
        workspace.WriteMessage(
            DefaultFolder,
            "0001-before.eml",
            DateTime.UtcNow.AddMinutes(-2));
        await using var database = await CreateDatabaseAsync(workspace);
        var mailboxId = TestMailboxId.From("instructions");
        await using var scope = database.CreateAsyncScope();
        var poll = scope.ServiceProvider.GetRequiredService<PollApprovedInbox>();
        Assert.Equal(1, await poll.ExecuteAsync(10, WorkerActor, CancellationToken.None));
        Assert.Equal(1L, await AdvancedInboxCursorsAsync(database));

        var subscriptions = scope.ServiceProvider
            .GetRequiredService<IApprovedMailboxSubscriptionStore>();
        var nowUtc = DateTimeOffset.UtcNow;
        await subscriptions.SaveAsync(
            new(
                mailboxId,
                "current-subscription",
                "users/instructions/mailFolders/inbox/messages",
                nowUtc.AddDays(2),
                ApprovedMailboxSubscriptionLifecycleState.Active,
                nowUtc,
                null,
                1),
            null,
            CancellationToken.None);
        Assert.Empty(await subscriptions.ListMaintenanceCandidatesAsync(
            nowUtc,
            CancellationToken.None));

        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var command = scope.ServiceProvider.GetRequiredService<UpdateApprovedMailbox>();
        var mailboxes = scope.ServiceProvider.GetRequiredService<ListApprovedMailboxes>();
        var editScopes = scope.ServiceProvider.GetRequiredService<IEditScopeLeases>();
        async Task SetStateAsync(ApprovedMailboxState state, string operationKey)
        {
            var current = (await mailboxes.ExecuteAsync(actor, CancellationToken.None))
                .Single(item => item.Id == mailboxId);
            var lease = await editScopes.ClaimAsync(
                new(
                    EditScopeKind.ApprovedMailbox,
                    current.Id,
                    current.Version,
                    actor,
                    $"{operationKey}-edit"),
                CancellationToken.None);
            await command.ExecuteAsync(
                new(
                    current.Id,
                    current.Address,
                    current.RouteScopes,
                    state,
                    current.Version,
                    actor,
                    operationKey,
                    current.MailboxIdentity,
                    current.InboxFolderIdentity,
                    current.SentFolderIdentity,
                    current.FolderBindings,
                    current.VerifiedEncodedMessageSizeLimit)
                {
                    EditLeaseToken = lease.Token
                },
                CancellationToken.None);
        }

        await SetStateAsync(ApprovedMailboxState.Disabled, "estate-disable");
        workspace.WriteMessage(
            DefaultFolder,
            "0002-while-disabled.eml",
            DateTime.UtcNow.AddMinutes(-1));
        await SetStateAsync(ApprovedMailboxState.Approved, "estate-reenable");

        // The cursor is gone and the boundary is the new activation, for the new
        // generation: nothing resumes the cycle the disabled mailbox left behind.
        Assert.Equal(0L, await AdvancedInboxCursorsAsync(database));
        Assert.Equal(
            1L,
            await database.ScalarAsync<long>(
                """
                SELECT COUNT(*) FROM ApprovedInboxPollStates state
                INNER JOIN ApprovedMailboxes mailbox ON mailbox.Id = state.ApprovedMailboxId
                WHERE state.StartBoundaryUtc = mailbox.ActivatedAtUtc
                  AND state.Generation = mailbox.MailboxGeneration;
                """));

        // The surviving subscription belongs to the old generation, so maintenance
        // has to replace it before notifications can wake this mailbox again.
        var candidate = Assert.Single(await subscriptions.ListMaintenanceCandidatesAsync(
            nowUtc,
            CancellationToken.None));
        Assert.Equal("current-subscription", candidate.Subscription?.SubscriptionId);
        Assert.Equal(1L, candidate.Subscription?.Generation);
        Assert.NotEqual(candidate.Subscription!.Generation, candidate.Generation);

        // Both messages predate the new boundary, so neither is ingested a second
        // time and the one that arrived while disabled is not a backlog.
        await poll.ExecuteAsync(10, WorkerActor, CancellationToken.None);
        Assert.Equal(
            1L,
            await database.ScalarAsync<long>("SELECT COUNT(*) FROM IntakeStagedReceipts"));
        Assert.Equal(
            1L,
            await database.ScalarAsync<long>("SELECT COUNT(*) FROM RetainedMailboxMessages"));
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

    private static Task<long> AdvancedInboxCursorsAsync(LocalDbTestDatabase database) =>
        database.ScalarAsync<long>(
            "SELECT COUNT(*) FROM ApprovedInboxPollStates WHERE [Cursor] IS NOT NULL");

    /// <summary>
    /// The immutable identity the local source derives for a file, restated here
    /// because a notification carries that identity from outside Pegasus: the test
    /// has to name the message before anything has read it.
    /// </summary>
    private static string ImmutableMessageId(
        MailboxWorkspace workspace,
        string folderIdentity,
        string fileName)
    {
        var content = File.ReadAllBytes(Path.Combine(workspace.Root, folderIdentity, fileName));
        var contentHash = Convert.ToHexString(SHA256.HashData(content));
        return Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes($"{fileName.Length}:{fileName}{contentHash}")));
    }

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

        /// <param name="receivedAtUtc">
        /// The local source reads a file's last write time as the received time, so
        /// a test that depends on an ordering against an activation boundary states
        /// it rather than racing the clock.
        /// </param>
        /// <param name="messageId">
        /// Overrides the otherwise-random RFC Internet-Message-ID MimeKit assigns,
        /// so a test can put the exact same durable message identity in two
        /// mailboxes deliberately (FABLE-06) instead of two messages that merely
        /// share a file name.
        /// </param>
        internal void WriteMessage(
            string folderIdentity,
            string fileName,
            DateTime? receivedAtUtc = null,
            string? messageId = null)
        {
            var path = Path.Combine(Root, folderIdentity, fileName);
            File.WriteAllBytes(path, CreateMessage(fileName, messageId));
            if (receivedAtUtc is { } received)
            {
                File.SetLastWriteTimeUtc(path, received);
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }

        /// <summary>
        /// Distinct content per file by default (a fresh random RFC Message-ID
        /// among them), so two mailboxes cannot accidentally share a source
        /// identity and make an isolation failure look like a pass. A caller that
        /// deliberately wants the same durable message identity in two mailboxes
        /// (FABLE-06) passes an explicit <paramref name="messageId"/>.
        /// </summary>
        private static byte[] CreateMessage(string fileName, string? messageId = null)
        {
            var message = new MimeMessage
            {
                Subject = $"Estate fixture {fileName}",
                Body = new TextPart("plain") { Text = $"Fixture body for {fileName}." }
            };
            if (messageId is not null)
            {
                message.MessageId = messageId;
            }
            message.From.Add(new MailboxAddress("Sender", "sender@example.invalid"));
            message.To.Add(new MailboxAddress("Approved Inbox", SeededAddress));
            using var stream = new MemoryStream();
            message.WriteTo(stream);
            return stream.ToArray();
        }
    }
}
