using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// ADR-0071, R902. The send journal lists a confirmed send that carried the Delete
/// case correspondence tick, names the Case's received e-mails as they stand
/// linked now (Inbox scope, approved mailboxes, held when the send was
/// prepared), records each run under the operation's version guard, and
/// stops listing after three failed runs.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class SentCaseCorrespondenceTidyPersistenceTests
{
    private const string MailboxChannel = "mailbox";
    private const string UploadChannel = "manual_upload";
    private const string InboxScope = "inbox";
    private const string SentScope = "sent";
    private const string UploadScope = "upload";
    private const string Actor = "sent-mail-correspondence-tidy";
    private static readonly DateTimeOffset SentAtUtc = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PreparedAtUtc = SentAtUtc.AddMinutes(-5);
    private static readonly DateTimeOffset NowUtc = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

    private enum Linking
    {
        AcceptedLink,
        ActiveAssociation,
        // Accepted, then unlinked by a member of staff: the association row is inactive.
        ReversedAssociation
    }

    [Fact]
    public async Task OnlyATickedConfirmedSendUnderTheAttemptBoundIsDueOldestSentItemFirst()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var older = Operation(estate, SentAtUtc.AddMinutes(-30));
        var newer = Operation(estate, SentAtUtc);
        var retrying = Operation(estate, SentAtUtc.AddMinutes(-10), StaffMailPurpose.GeneralCorrespondence);
        retrying.CorrespondenceMoveAttempts = 2;
        retrying.CorrespondenceMoveFailureCode = TidySentCaseCorrespondence.MoveFailedFailureCode;
        var unticked = Operation(estate, SentAtUtc.AddMinutes(-40), tidies: false);
        var submitted = Operation(estate, SentAtUtc.AddMinutes(-40), state: StaffMailState.Submitted);
        var done = Operation(estate, SentAtUtc.AddMinutes(-40));
        done.CorrespondenceMoveState = EfSentCaseCorrespondenceTidyStore.DoneState;
        done.CorrespondenceMoveAttempts = 1;
        var exhausted = Operation(estate, SentAtUtc.AddMinutes(-40));
        exhausted.CorrespondenceMoveAttempts = TidySentCaseCorrespondence.MaximumAttempts;
        await using (var context = await database.CreateContextAsync())
        {
            context.Set<StaffMailSendOperationEntity>().AddRange(
                newer, older, retrying, unticked, submitted, done, exhausted);
            await context.SaveChangesAsync();
        }
        var store = Store(database);

        var due = await store.ListDueAsync(10, CancellationToken.None);

        Assert.Equal([older.Id, retrying.Id, newer.Id], due.Select(item => item.OperationId));
        Assert.Equal(older.Version, due[0].OperationVersion);
        Assert.Equal(0, due[0].Attempts);
        Assert.Equal(2, due[1].Attempts);
        Assert.Equal(older.Id, Assert.Single(await store.ListDueAsync(1, CancellationToken.None)).OperationId);
    }

    [Fact]
    public async Task TheCaseIsTheReportGenerationsForAReportSendAndTheContextForAReplyOrATriageOutcomeReply()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var report = Operation(estate, SentAtUtc.AddMinutes(-3));
        var reply = Operation(
            estate, SentAtUtc.AddMinutes(-2), StaffMailPurpose.GeneralCorrespondence, contextId: estate.OtherCaseId);
        var triageCaseId = Guid.NewGuid();
        var triageReply = Operation(
            estate, SentAtUtc.AddMinutes(-1), StaffMailPurpose.TriageOutcomeReply, contextId: triageCaseId);
        await using (var context = await database.CreateContextAsync())
        {
            context.Set<StaffMailSendOperationEntity>().AddRange(report, reply, triageReply);
            await context.SaveChangesAsync();
        }

        var due = (await Store(database).ListDueAsync(10, CancellationToken.None))
            .ToDictionary(item => item.OperationId);

        Assert.Equal(estate.CaseId, due[report.Id].CaseId);
        Assert.Equal(estate.OtherCaseId, due[reply.Id].CaseId);
        Assert.Equal(triageCaseId, due[triageReply.Id].CaseId);
        // The cut-off is when the send was prepared, not when its Sent item was seen.
        Assert.Equal(report.CreatedAtUtc, due[report.Id].PreparedAtUtc);
        Assert.Equal(reply.CreatedAtUtc, due[reply.Id].PreparedAtUtc);
        Assert.Equal(triageReply.CreatedAtUtc, due[triageReply.Id].PreparedAtUtc);
    }

    [Fact]
    public async Task OnlyTheMessagesLinkedToTheCaseNowAreNamedOldestFirst()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var accepted = await ReceiveAsync(
            database, estate.CaseId, Linking.AcceptedLink, estate.MailboxId, receivedAtUtc: PreparedAtUtc.AddHours(-2));
        var associated = await ReceiveAsync(
            database, estate.CaseId, Linking.ActiveAssociation, estate.MailboxId, receivedAtUtc: PreparedAtUtc.AddHours(-3));
        await ReceiveAsync(database, estate.CaseId, Linking.ReversedAssociation, estate.MailboxId);
        await ReceiveAsync(database, estate.OtherCaseId, Linking.AcceptedLink, estate.MailboxId);

        var messages = await Store(database).ListMessagesAsync(estate.CaseId, PreparedAtUtc, CancellationToken.None);

        Assert.Equal([associated, accepted], messages.Select(item => item.RetainedMessageId));
        Assert.All(messages, item => Assert.Equal("engineers-mailbox", item.MailboxIdentity));
        Assert.Equal(
            [ImmutableId(associated), ImmutableId(accepted)],
            messages.Select(item => item.ImmutableMessageId));
    }

    [Fact]
    public async Task ATriageCasesRequestEmailIsAmongItsMessages()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var (triageCaseId, messageId) = await SeedTriageAsync(database, estate);
        var store = Store(database);

        var messages = await store.ListMessagesAsync(triageCaseId, PreparedAtUtc, CancellationToken.None);

        Assert.Equal(messageId, Assert.Single(messages).RetainedMessageId);
        Assert.Empty(await store.ListMessagesAsync(estate.CaseId, PreparedAtUtc, CancellationToken.None));
    }

    [Fact]
    public async Task SentItemsAndUploadedEmailsAreNeverNamed()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var inbox = await ReceiveAsync(database, estate.CaseId, Linking.AcceptedLink, estate.MailboxId);
        await ReceiveAsync(database, estate.CaseId, Linking.AcceptedLink, estate.MailboxId, SentScope);
        await ReceiveAsync(
            database, estate.CaseId, Linking.ActiveAssociation, mailboxId: null, UploadScope, UploadChannel);

        var messages = await Store(database).ListMessagesAsync(estate.CaseId, PreparedAtUtc, CancellationToken.None);

        Assert.Equal(inbox, Assert.Single(messages).RetainedMessageId);
    }

    /// <summary>
    /// A mailbox an Administrator has withdrawn is never written to, nor is
    /// one whose Graph identity has not been supplied.
    /// </summary>
    [Fact]
    public async Task AMessageInAMailboxThatIsNotApprovedAndIdentifiedIsNotNamed()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var approved = await ReceiveAsync(database, estate.CaseId, Linking.AcceptedLink, estate.MailboxId);
        await ReceiveAsync(database, estate.CaseId, Linking.AcceptedLink, estate.WithdrawnMailboxId);
        await ReceiveAsync(
            database,
            estate.CaseId,
            Linking.AcceptedLink,
            AdministrationPolicyModelConfiguration.InitialInstructionsMailboxId);

        var messages = await Store(database).ListMessagesAsync(estate.CaseId, PreparedAtUtc, CancellationToken.None);

        Assert.Equal(approved, Assert.Single(messages).RetainedMessageId);
    }

    [Fact]
    public async Task OnlyMailHeldWhenTheSendWasPreparedIsNamed()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var earlier = await ReceiveAsync(
            database, estate.CaseId, Linking.AcceptedLink, estate.MailboxId,
            receivedAtUtc: PreparedAtUtc.AddHours(-1));
        var atCutOff = await ReceiveAsync(
            database, estate.CaseId, Linking.AcceptedLink, estate.MailboxId,
            receivedAtUtc: PreparedAtUtc.AddMinutes(-30), retainedAtUtc: PreparedAtUtc);
        // Received before the send was prepared, but retained only after it.
        await ReceiveAsync(
            database, estate.CaseId, Linking.AcceptedLink, estate.MailboxId,
            receivedAtUtc: PreparedAtUtc.AddMinutes(-20), retainedAtUtc: PreparedAtUtc.AddSeconds(1));

        var messages = await Store(database).ListMessagesAsync(estate.CaseId, PreparedAtUtc, CancellationToken.None);

        Assert.Equal([earlier, atCutOff], messages.Select(item => item.RetainedMessageId));
    }

    [Fact]
    public async Task ADismissedMessageIsStillNamed()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var dismissed = await ReceiveAsync(
            database, estate.CaseId, Linking.AcceptedLink, estate.MailboxId,
            dismissedAtUtc: PreparedAtUtc.AddHours(-1));

        var messages = await Store(database).ListMessagesAsync(estate.CaseId, PreparedAtUtc, CancellationToken.None);

        Assert.Equal(dismissed, Assert.Single(messages).RetainedMessageId);
    }

    /// <summary>
    /// R903: the tidy moves what the Case's Correspondence tab shows as
    /// received, less uploaded files, and never Pegasus's own Sent items.
    /// </summary>
    [Fact]
    public async Task TheMessagesAreTheCorrespondenceTabsReceivedEmailsLessUploads()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var accepted = await ReceiveAsync(
            database, estate.CaseId, Linking.AcceptedLink, estate.MailboxId, receivedAtUtc: PreparedAtUtc.AddHours(-4));
        var associated = await ReceiveAsync(
            database, estate.CaseId, Linking.ActiveAssociation, estate.MailboxId, receivedAtUtc: PreparedAtUtc.AddHours(-3));
        var dismissed = await ReceiveAsync(
            database, estate.CaseId, Linking.AcceptedLink, estate.MailboxId,
            receivedAtUtc: PreparedAtUtc.AddHours(-2), dismissedAtUtc: PreparedAtUtc.AddHours(-1));
        await ReceiveAsync(database, estate.CaseId, Linking.ReversedAssociation, estate.MailboxId);
        await ReceiveAsync(database, estate.OtherCaseId, Linking.AcceptedLink, estate.MailboxId);
        var uploaded = await ReceiveAsync(
            database, estate.CaseId, Linking.ActiveAssociation, mailboxId: null, UploadScope, UploadChannel);
        var sentItem = await AddSentItemAsync(database, estate);

        var messages = await Store(database).ListMessagesAsync(
            estate.CaseId, NowUtc.AddDays(1), CancellationToken.None);
        await using var context = await database.CreateContextAsync();
        var correspondence = await EfCaseQueryStore.ReadCorrespondenceEmailsAsync(
            context, estate.CaseId, CancellationToken.None);

        Assert.Equal([accepted, associated, dismissed], messages.Select(item => item.RetainedMessageId));
        Assert.Equal(
            messages.Select(item => item.RetainedMessageId).Append(uploaded).Append(sentItem).Order().ToArray(),
            correspondence.Select(item => item.RetainedMessageId).Order().ToArray());
    }

    [Fact]
    public async Task AStaleVersionRecordsNothing()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var operation = Operation(estate, SentAtUtc);
        await AddAsync(database, operation);
        var store = Store(database);

        Assert.False(await store.RecordAsync(
            operation.Id, operation.Version - 1, new(1, 0, 0, 0), null, NowUtc, CancellationToken.None));

        await using var context = await database.CreateContextAsync();
        var row = await ReadAsync(context, operation.Id);
        Assert.Null(row.CorrespondenceMoveState);
        Assert.Equal(0, row.CorrespondenceMoveAttempts);
        Assert.Null(row.CorrespondenceMovedAtUtc);
        Assert.Equal(operation.Version, row.Version);
        Assert.Empty(await context.CaseHistory.AsNoTracking().ToArrayAsync());
        Assert.Equal(operation.Id, Assert.Single(await store.ListDueAsync(10, CancellationToken.None)).OperationId);
    }

    [Fact]
    public async Task AMovedRunIsDoneWithOneCaseHistoryRowAndNoLongerDue()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var operation = Operation(estate, SentAtUtc);
        await AddAsync(database, operation);
        var store = Store(database);

        Assert.True(await store.RecordAsync(
            operation.Id, operation.Version, new(2, 1, 1, 0), null, NowUtc, CancellationToken.None));
        // A second worker that listed the row before the first recorded it loses the guard.
        Assert.False(await store.RecordAsync(
            operation.Id, operation.Version, new(2, 1, 1, 0), null, NowUtc, CancellationToken.None));

        await using var context = await database.CreateContextAsync();
        var row = await ReadAsync(context, operation.Id);
        Assert.Equal(EfSentCaseCorrespondenceTidyStore.DoneState, row.CorrespondenceMoveState);
        Assert.Equal(1, row.CorrespondenceMoveAttempts);
        Assert.Equal(NowUtc, row.CorrespondenceMovedAtUtc);
        Assert.Null(row.CorrespondenceMoveFailureCode);
        Assert.Equal(operation.Version + 1, row.Version);
        // A report send's Case is its generation's.
        var entry = Assert.Single(await context.CaseHistory.AsNoTracking().ToArrayAsync());
        Assert.Equal(estate.CaseId, entry.CaseId);
        Assert.Equal("correspondence_moved_to_deleted_items", entry.EventType);
        Assert.Equal(Actor, entry.Actor);
        Assert.Equal(
            "The Case's received e-mails were moved to Deleted Items after the send: 2 moved, 1 already there, 1 no longer in the mailbox.",
            entry.Reason);
        Assert.Equal($"correspondence-tidy:{operation.Id:N}:1", entry.OperationKey);
        Assert.Equal(NowUtc, entry.OccurredAtUtc);
        Assert.Empty(await store.ListDueAsync(10, CancellationToken.None));
    }

    [Fact]
    public async Task MessagesAlreadyInDeletedItemsAreDoneWithoutAMovedTimeOnTheContextCase()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var operation = Operation(
            estate, SentAtUtc, StaffMailPurpose.GeneralCorrespondence, contextId: estate.OtherCaseId);
        await AddAsync(database, operation);

        Assert.True(await Store(database).RecordAsync(
            operation.Id, operation.Version, new(0, 2, 0, 0), null, NowUtc, CancellationToken.None));

        await using var context = await database.CreateContextAsync();
        var row = await ReadAsync(context, operation.Id);
        Assert.Equal(EfSentCaseCorrespondenceTidyStore.DoneState, row.CorrespondenceMoveState);
        Assert.Null(row.CorrespondenceMovedAtUtc);
        var entry = Assert.Single(await context.CaseHistory.AsNoTracking().ToArrayAsync());
        Assert.Equal(estate.OtherCaseId, entry.CaseId);
        Assert.Equal("correspondence_moved_to_deleted_items", entry.EventType);
        Assert.Equal(
            "The Case's received e-mails were moved to Deleted Items after the send: 0 moved, 2 already there, 0 no longer in the mailbox.",
            entry.Reason);
    }

    /// <summary>
    /// Messages all gone from the mailbox, or a Case with no received e-mail,
    /// changed nothing on the Case: the run is recorded on the send row alone.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    public async Task ARunThatFoundNothingToMoveIsDoneWithoutCaseHistory(int missing)
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var operation = Operation(estate, SentAtUtc);
        await AddAsync(database, operation);
        var store = Store(database);

        Assert.True(await store.RecordAsync(
            operation.Id, operation.Version, new(0, 0, missing, 0), null, NowUtc, CancellationToken.None));

        await using var context = await database.CreateContextAsync();
        var row = await ReadAsync(context, operation.Id);
        Assert.Equal(EfSentCaseCorrespondenceTidyStore.DoneState, row.CorrespondenceMoveState);
        Assert.Equal(1, row.CorrespondenceMoveAttempts);
        Assert.Null(row.CorrespondenceMovedAtUtc);
        Assert.Empty(await context.CaseHistory.AsNoTracking().ToArrayAsync());
        Assert.Empty(await store.ListDueAsync(10, CancellationToken.None));
    }

    [Fact]
    public async Task AFailureStaysDueUntilTheThirdRunThenStopsAndEveryRunIsInTheHistory()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var operation = Operation(estate, SentAtUtc);
        await AddAsync(database, operation);
        var store = Store(database);
        const string code = TidySentCaseCorrespondence.MoveFailedFailureCode;

        for (var attempt = 1; attempt <= TidySentCaseCorrespondence.MaximumAttempts; attempt++)
        {
            var due = Assert.Single(await store.ListDueAsync(10, CancellationToken.None));
            Assert.Equal(attempt - 1, due.Attempts);
            Assert.True(await store.RecordAsync(
                due.OperationId,
                due.OperationVersion,
                new(0, 0, 0, 2),
                code,
                NowUtc.AddMinutes(attempt),
                CancellationToken.None));
        }

        Assert.Empty(await store.ListDueAsync(10, CancellationToken.None));
        await using var context = await database.CreateContextAsync();
        var row = await ReadAsync(context, operation.Id);
        Assert.Equal(EfSentCaseCorrespondenceTidyStore.FailedState, row.CorrespondenceMoveState);
        Assert.Equal(TidySentCaseCorrespondence.MaximumAttempts, row.CorrespondenceMoveAttempts);
        Assert.Equal(code, row.CorrespondenceMoveFailureCode);
        Assert.Null(row.CorrespondenceMovedAtUtc);
        var history = await context.CaseHistory.AsNoTracking()
            .Where(item => item.CaseId == estate.CaseId)
            .OrderBy(item => item.OccurredAtUtc)
            .ToArrayAsync();
        Assert.All(history, entry => Assert.Equal("correspondence_move_failed", entry.EventType));
        Assert.Equal(
            Enumerable.Range(1, TidySentCaseCorrespondence.MaximumAttempts)
                .Select(attempt => $"correspondence-tidy:{operation.Id:N}:{attempt}"),
            history.Select(entry => entry.OperationKey));
        Assert.Equal(
            Enumerable.Range(1, TidySentCaseCorrespondence.MaximumAttempts)
                .Select(attempt => $"2 of the Case's received e-mails could not be moved to Deleted Items ({code}); attempt {attempt} of 3."),
            history.Select(entry => entry.Reason));
    }

    [Fact]
    public async Task ASuccessfulRunAfterAFailureIsDoneAndClearsTheFailureCode()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var operation = Operation(estate, SentAtUtc);
        await AddAsync(database, operation);
        var store = Store(database);
        Assert.True(await store.RecordAsync(
            operation.Id,
            operation.Version,
            new(0, 0, 0, 1),
            TidySentCaseCorrespondence.MailboxNotPermittedFailureCode,
            NowUtc,
            CancellationToken.None));
        var due = Assert.Single(await store.ListDueAsync(10, CancellationToken.None));
        Assert.Equal(1, due.Attempts);

        Assert.True(await store.RecordAsync(
            due.OperationId, due.OperationVersion, new(1, 0, 0, 0), null, NowUtc.AddMinutes(5), CancellationToken.None));

        await using var context = await database.CreateContextAsync();
        var row = await ReadAsync(context, operation.Id);
        Assert.Equal(EfSentCaseCorrespondenceTidyStore.DoneState, row.CorrespondenceMoveState);
        Assert.Equal(2, row.CorrespondenceMoveAttempts);
        Assert.Null(row.CorrespondenceMoveFailureCode);
        Assert.Equal(NowUtc.AddMinutes(5), row.CorrespondenceMovedAtUtc);
        Assert.Equal(
            ["correspondence_move_failed", "correspondence_moved_to_deleted_items"],
            (await context.CaseHistory.AsNoTracking().OrderBy(item => item.OccurredAtUtc).ToArrayAsync())
                .Select(entry => entry.EventType));
    }

    [Fact]
    public async Task AFailureNeedsAFailureCode()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var operation = Operation(estate, SentAtUtc);
        await AddAsync(database, operation);

        await Assert.ThrowsAsync<ArgumentException>(() => Store(database).RecordAsync(
            operation.Id, operation.Version, new(0, 0, 0, 1), null, NowUtc, CancellationToken.None));
    }

    /// <summary>
    /// R904: a chaser, a forward, a new message and a photo link never carry
    /// the tick, and the database refuses one that does.
    /// </summary>
    [Theory]
    [InlineData(StaffMailPurpose.CaseChaser, StaffMailComposeMode.New)]
    [InlineData(StaffMailPurpose.TriageChaser, StaffMailComposeMode.New)]
    [InlineData(StaffMailPurpose.PhotoLink, StaffMailComposeMode.New)]
    [InlineData(StaffMailPurpose.GeneralCorrespondence, StaffMailComposeMode.New)]
    [InlineData(StaffMailPurpose.GeneralCorrespondence, StaffMailComposeMode.Forward)]
    public async Task TheTickIsRefusedOnASendThatMayNotCarryIt(
        StaffMailPurpose purpose,
        StaffMailComposeMode composeMode)
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);

        await Assert.ThrowsAsync<DbUpdateException>(() => AddAsync(
            database, Operation(estate, SentAtUtc, purpose, composeMode, contextId: estate.CaseId)));
    }

    [Fact]
    public async Task TheDatabaseAcceptsTheTickExactlyWhereTheSendMayCarryIt()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);

        foreach (var purpose in Enum.GetValues<StaffMailPurpose>())
        {
            foreach (var composeMode in Enum.GetValues<StaffMailComposeMode>())
            {
                var operation = Operation(estate, SentAtUtc, purpose, composeMode, contextId: estate.CaseId);
                if (StaffMailSendCommand.MayTidyCaseCorrespondence(purpose, composeMode))
                {
                    await AddAsync(database, operation);
                }
                else
                {
                    await Assert.ThrowsAsync<DbUpdateException>(() => AddAsync(database, operation));
                }
            }
        }
    }

    private static EfSentCaseCorrespondenceTidyStore Store(LocalDbTestDatabase database)
    {
        var scope = database.CreateAsyncScope();
        return new(scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>());
    }

    private static async Task AddAsync(LocalDbTestDatabase database, StaffMailSendOperationEntity operation)
    {
        await using var context = await database.CreateContextAsync();
        context.Set<StaffMailSendOperationEntity>().Add(operation);
        await context.SaveChangesAsync();
    }

    private static Task<StaffMailSendOperationEntity> ReadAsync(PegasusDbContext context, Guid operationId) =>
        context.Set<StaffMailSendOperationEntity>().AsNoTracking().SingleAsync(item => item.Id == operationId);

    private static string ImmutableId(Guid retainedMessageId) => $"immutable-{retainedMessageId:N}";

    /// <summary>A ticked send, prepared five minutes before its Sent item was seen.</summary>
    private static StaffMailSendOperationEntity Operation(
        Estate estate,
        DateTimeOffset sentAtUtc,
        StaffMailPurpose purpose = StaffMailPurpose.CaseReport,
        StaffMailComposeMode composeMode = StaffMailComposeMode.Reply,
        StaffMailState state = StaffMailState.Sent,
        bool tidies = true,
        Guid? contextId = null)
    {
        var id = Guid.NewGuid();
        return new()
        {
            Id = id,
            ActorSubjectId = Guid.NewGuid().ToString("D"),
            MailboxId = estate.MailboxId,
            MailboxGeneration = 1,
            OperationKey = $"send:{id:N}",
            PayloadHash = new string('7', 64),
            Purpose = purpose,
            ContextId = contextId ?? (purpose == StaffMailPurpose.CaseReport ? estate.GenerationId : estate.CaseId),
            ContextVersion = 1,
            ComposeMode = composeMode,
            TidiesCaseCorrespondence = tidies,
            RecipientsJson = "[]",
            Subject = "Correspondence",
            Body = "Correspondence",
            AttachmentsJson = "[]",
            State = state,
            CorrelationMarker = $"mail:{id:N}",
            CreatedAtUtc = sentAtUtc.AddMinutes(-5),
            RequestedAtUtc = sentAtUtc.AddMinutes(-5),
            ObservedSentAtUtc = state == StaffMailState.Sent ? sentAtUtc : null,
            Version = 3,
            ConcurrencyToken = Guid.NewGuid()
        };
    }

    /// <summary>
    /// One received e-mail: its receipt, its retained row and the link that
    /// puts it on (or took it off) the Case. Returns the retained message id.
    /// </summary>
    private static async Task<Guid> ReceiveAsync(
        LocalDbTestDatabase database,
        Guid caseId,
        Linking linking,
        Guid? mailboxId,
        string folderScope = InboxScope,
        string channel = MailboxChannel,
        DateTimeOffset? receivedAtUtc = null,
        DateTimeOffset? retainedAtUtc = null,
        DateTimeOffset? dismissedAtUtc = null)
    {
        var receiptId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var token = $"retained:{messageId:N}";
        var receivedAt = receivedAtUtc ?? PreparedAtUtc.AddHours(-2);
        await using var db = await database.CreateContextAsync();
        db.IntakeReceipts.Add(Receipt(receiptId, channel, token, receivedAt));
        db.RetainedMailboxMessages.Add(
            Message(messageId, mailboxId, folderScope, token, receivedAt, retainedAtUtc ?? receivedAt, dismissedAtUtc));
        switch (linking)
        {
            case Linking.AcceptedLink:
                AddAcceptedLink(db, receiptId, caseId, receivedAt);
                break;
            case Linking.ActiveAssociation:
                AddAssociation(db, receiptId, caseId, receivedAt, isActive: true);
                break;
            case Linking.ReversedAssociation:
                AddAcceptedLink(db, receiptId, caseId, receivedAt);
                AddAssociation(db, receiptId, caseId, receivedAt, isActive: false);
                break;
        }
        await db.SaveChangesAsync();
        return messageId;
    }

    /// <summary>
    /// A Sent item Pegasus retained for a send it made on the Case, which the
    /// Correspondence tab lists. Returns the retained message id.
    /// </summary>
    private static async Task<Guid> AddSentItemAsync(LocalDbTestDatabase database, Estate estate)
    {
        var messageId = Guid.NewGuid();
        var operation = Operation(
            estate, SentAtUtc, StaffMailPurpose.GeneralCorrespondence, tidies: false, contextId: estate.CaseId);
        operation.ObservedSentImmutableMessageId = ImmutableId(messageId);
        await using var db = await database.CreateContextAsync();
        db.Set<StaffMailSendOperationEntity>().Add(operation);
        db.RetainedMailboxMessages.Add(
            Message(messageId, estate.MailboxId, SentScope, $"sent:{messageId:N}", SentAtUtc, SentAtUtc, null));
        await db.SaveChangesAsync();
        return messageId;
    }

    private static async Task<(Guid TriageCaseId, Guid MessageId)> SeedTriageAsync(
        LocalDbTestDatabase database,
        Estate estate)
    {
        var triageCaseId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var token = $"retained:{messageId:N}";
        var receivedAtUtc = PreparedAtUtc.AddHours(-2);
        await using var db = await database.CreateContextAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({triageCaseId}, {estate.PrincipalId}, {estate.LineageId}, {2026}, {3}, {"t.CCT260003"}, {"triage"}, NULL, {"pending"}, {false}, {false}, {SentAtUtc}, {0L}, {Guid.NewGuid()})");
        await CaseWorkFixture.InsertPrimaryWorksAsync(db);
        var receipt = Receipt(receiptId, MailboxChannel, token, receivedAtUtc);
        db.IntakeReceipts.Add(receipt);
        db.RetainedMailboxMessages.Add(
            Message(messageId, estate.MailboxId, InboxScope, token, receivedAtUtc, receivedAtUtc, null));
        db.Triage.Add(new TriageEntity
        {
            CaseId = triageCaseId,
            OriginReceiptId = receiptId,
            SourceChannel = receipt.SourceChannel,
            ExternalReceiptToken = receipt.ExternalReceiptToken,
            SourceHash = receipt.SourceHash,
            EvaluationRevisionId = Guid.NewGuid(),
            NormalizedVehicleRegistration = "AB12CDE",
            State = "open",
            CreatedAtUtc = receivedAtUtc,
            CreationOperationKey = $"triage:{receiptId:N}",
            Version = 0,
            ConcurrencyToken = Guid.NewGuid()
        });
        await db.SaveChangesAsync();
        return (triageCaseId, messageId);
    }

    private static IntakeReceiptEntity Receipt(
        Guid id,
        string channel,
        string token,
        DateTimeOffset receivedAtUtc) => new()
    {
        Id = id,
        SourceFileName = "correspondence.eml",
        MediaType = "message/rfc822",
        SourceLength = 1,
        SourceHash = new string('A', 64),
        SourceChannel = channel,
        ExternalReceiptToken = token,
        ReceivedAtUtc = receivedAtUtc,
        ProcessedAtUtc = receivedAtUtc,
        SourceReaderKey = "correspondence-tidy-test",
        SourceReaderVersion = "1",
        Version = 0,
        Decision = "case_created",
        DecisionReason = "correspondence tidy test",
        EvidenceJson = "{\"version\":1,\"data\":[]}",
        FieldsJson = "{\"version\":1,\"data\":[]}",
        OcrCandidatesJson = "{\"version\":1,\"data\":[]}"
    };

    /// <summary>A retained row; an uploaded email has no mailbox.</summary>
    private static RetainedMailboxMessageEntity Message(
        Guid id,
        Guid? mailboxId,
        string folderScope,
        string token,
        DateTimeOffset receivedAtUtc,
        DateTimeOffset retainedAtUtc,
        DateTimeOffset? dismissedAtUtc) => new()
    {
        Id = id,
        MailboxId = mailboxId,
        MailboxAddress = mailboxId is null ? "upload" : "engineers@example.invalid",
        FolderScope = folderScope,
        FolderIdentity = folderScope,
        ImmutableMessageId = ImmutableId(id),
        InternetMessageIdentity = $"<{id:N}@example.invalid>",
        ConversationIdentity = "conversation",
        ExternalReceiptToken = token,
        ToAddressesJson = "[]",
        CcAddressesJson = "[]",
        SourceLength = 1,
        SourceSha256 = new string('A', 64),
        ReceivedAtUtc = receivedAtUtc,
        RetainedAtUtc = retainedAtUtc,
        DismissedAtUtc = dismissedAtUtc,
        DismissedBySubjectId = dismissedAtUtc is null ? null : Guid.NewGuid().ToString("D")
    };

    private static void AddAcceptedLink(
        PegasusDbContext db,
        Guid receiptId,
        Guid caseId,
        DateTimeOffset linkedAtUtc)
    {
        var custodyWorkId = Guid.NewGuid();
        db.ExternalWorkItems.Add(new()
        {
            Id = custodyWorkId,
            CaseId = caseId,
            Kind = "create_case_custody",
            OperationKey = $"custody:{receiptId:N}",
            State = "completed",
            AttemptCount = 1,
            DueAtUtc = linkedAtUtc
        });
        db.CaseIntakeLinks.Add(new()
        {
            IntakeReceiptId = receiptId,
            CaseId = caseId,
            CustodyWorkId = custodyWorkId,
            LinkedAtUtc = linkedAtUtc,
            ActorKind = "Staff",
            ActorSubjectId = Guid.NewGuid().ToString("D"),
            ActorRolesJson = "[]",
            Reason = "Correspondence tidy test.",
            OperationKey = $"accept:{receiptId:N}"
        });
    }

    private static void AddAssociation(
        PegasusDbContext db,
        Guid receiptId,
        Guid caseId,
        DateTimeOffset linkedAtUtc,
        bool isActive) =>
        db.IntakeManualAssociations.Add(new()
        {
            IntakeReceiptId = receiptId,
            CaseId = caseId,
            IsActive = isActive,
            Version = isActive ? 0 : 1,
            LinkedAtUtc = linkedAtUtc,
            UnlinkedAtUtc = isActive ? null : linkedAtUtc.AddMinutes(1),
            ActorKind = "Staff",
            ActorSubjectId = Guid.NewGuid().ToString("D"),
            ActorRolesJson = "[]",
            LastOperationKey = $"associate:{receiptId:N}"
        });

    private sealed record Estate(
        Guid PrincipalId,
        Guid LineageId,
        Guid CaseId,
        Guid OtherCaseId,
        Guid GenerationId,
        Guid MailboxId,
        Guid WithdrawnMailboxId);

    private static async Task<Estate> SeedEstateAsync(LocalDbTestDatabase database)
    {
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var otherCaseId = Guid.NewGuid();
        var generationId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var withdrawnMailboxId = Guid.NewGuid();
        await using var db = await database.CreateContextAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Role, Id, Name, Version) VALUES (N'principal', {organizationId}, {"Correspondence tidy test organization"}, {0L})");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {SentAtUtc})");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, ReportSendingRulesJson, Version) VALUES ({principalId}, {organizationId}, {"CCT"}, {lineageId}, {true}, {EfOrganizationAdministration.DefaultReportSendingJson}, {0L})");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2026}, {1}, {"CCT260001"}, {"inspection"}, {"review"}, {"pending"}, {true}, {true}, {SentAtUtc}, {0L}, {Guid.NewGuid()})");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({otherCaseId}, {principalId}, {lineageId}, {2026}, {2}, {"CCT260002"}, {"inspection"}, {"review"}, {"pending"}, {true}, {true}, {SentAtUtc}, {0L}, {Guid.NewGuid()})");
        await CaseWorkFixture.InsertPrimaryWorksAsync(db);
        db.Set<CaseReportGenerationEntity>().Add(new()
        {
            Id = generationId,
            CaseId = caseId,
            WorkId = caseId,
            SnapshotHash = new string('a', 64),
            SnapshotJson = "{}",
            TemplateVersion = "1",
            RendererVersion = "1",
            State = "Generated",
            GeneratedAtUtc = SentAtUtc
        });
        db.ApprovedMailboxes.AddRange(
            new ApprovedMailboxEntity
            {
                Id = mailboxId,
                Address = "engineers@example.invalid",
                AllowIncoming = true,
                AllowOutgoing = true,
                MailboxGeneration = 1,
                State = "Approved",
                MailboxIdentity = "engineers-mailbox",
                InboxFolderIdentity = "inbox",
                SentFolderIdentity = "sent",
                ActivatedAtUtc = SentAtUtc.AddDays(-1),
                Version = 1
            },
            new ApprovedMailboxEntity
            {
                Id = withdrawnMailboxId,
                Address = "withdrawn@example.invalid",
                AllowIncoming = true,
                AllowOutgoing = true,
                MailboxGeneration = 1,
                State = "Disabled",
                MailboxIdentity = "withdrawn-mailbox",
                InboxFolderIdentity = "inbox",
                SentFolderIdentity = "sent",
                ActivatedAtUtc = SentAtUtc.AddDays(-1),
                Version = 1
            });
        await db.SaveChangesAsync();
        return new(principalId, lineageId, caseId, otherCaseId, generationId, mailboxId, withdrawnMailboxId);
    }
}
