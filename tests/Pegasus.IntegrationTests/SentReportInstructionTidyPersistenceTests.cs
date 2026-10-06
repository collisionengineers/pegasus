using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// ADR-0063. The send journal lists a confirmed report send whose answered
/// instruction has not been tidied, records each attempt under the operation's
/// version guard, and stops listing after three failures.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class SentReportInstructionTidyPersistenceTests
{
    private static readonly DateTimeOffset SentAtUtc = new(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset NowUtc = new(2026, 10, 5, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task OnlyAConfirmedReportSendThatAnsweredARetainedInstructionIsDueOldestFirst()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var older = Operation(estate, SentAtUtc.AddMinutes(-30));
        var newer = Operation(estate, SentAtUtc);
        var notReport = Operation(estate, SentAtUtc, purpose: StaffMailPurpose.GeneralCorrespondence);
        var notSent = Operation(estate, SentAtUtc, state: StaffMailState.Submitted);
        var noInstruction = Operation(estate, SentAtUtc, original: false);
        var tidied = Operation(estate, SentAtUtc);
        tidied.InstructionMoveState = nameof(SentReportInstructionTidyOutcome.Moved);
        tidied.InstructionMoveAttempts = 1;
        var exhausted = Operation(estate, SentAtUtc);
        exhausted.InstructionMoveAttempts = TidySentReportInstructions.MaximumAttempts;
        var retrying = Operation(estate, SentAtUtc.AddMinutes(-10));
        retrying.InstructionMoveAttempts = 2;
        retrying.InstructionMoveFailureCode = "move_failed";
        await using (var context = await database.CreateContextAsync())
        {
            context.Set<StaffMailSendOperationEntity>().AddRange(
                newer, older, notReport, notSent, noInstruction, tidied, exhausted, retrying);
            await context.SaveChangesAsync();
        }

        var due = await Store(database).ListDueAsync(10, CancellationToken.None);

        Assert.Equal([older.Id, retrying.Id, newer.Id], due.Select(item => item.OperationId));
        var first = due[0];
        Assert.Equal(estate.CaseId, first.CaseId);
        Assert.Equal(estate.RetainedMessageId, first.RetainedMessageId);
        Assert.Equal("engineers-mailbox", first.MailboxIdentity);
        Assert.Equal("immutable-instruction", first.ImmutableMessageId);
        Assert.Equal(older.Version, first.OperationVersion);
        Assert.Equal(0, first.Attempts);
        Assert.Equal(2, due[1].Attempts);
        Assert.Single(await Store(database).ListDueAsync(1, CancellationToken.None));
    }

    /// <summary>
    /// A mailbox an Administrator has withdrawn is never written to: a send
    /// whose instruction is held in a mailbox that is not Approved is not due,
    /// and is due again once the mailbox is.
    /// </summary>
    [Fact]
    public async Task ASendWhoseMailboxIsNotApprovedIsNotDue()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var operation = Operation(estate, SentAtUtc);
        await AddAsync(database, operation);
        var store = Store(database);

        await database.ExecuteAsync(
            $"UPDATE ApprovedMailboxes SET State = 'Disabled' WHERE Id = '{estate.MailboxId:D}'");

        Assert.Empty(await store.ListDueAsync(10, CancellationToken.None));

        await database.ExecuteAsync(
            $"UPDATE ApprovedMailboxes SET State = 'Approved' WHERE Id = '{estate.MailboxId:D}'");

        Assert.Equal(operation.Id, Assert.Single(await store.ListDueAsync(10, CancellationToken.None)).OperationId);
    }

    [Fact]
    public async Task AMovedInstructionIsRecordedOnceWithItsHistoryAndNoLongerListed()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var operation = Operation(estate, SentAtUtc);
        await AddAsync(database, operation);
        var store = Store(database);

        Assert.True(await store.RecordAsync(
            operation.Id, operation.Version, SentReportInstructionTidyOutcome.Moved, null, NowUtc, CancellationToken.None));
        // A second worker that listed the row before the first recorded it loses the guard.
        Assert.False(await store.RecordAsync(
            operation.Id, operation.Version, SentReportInstructionTidyOutcome.Moved, null, NowUtc, CancellationToken.None));

        await using var context = await database.CreateContextAsync();
        var row = await context.Set<StaffMailSendOperationEntity>().AsNoTracking().SingleAsync(item => item.Id == operation.Id);
        Assert.Equal("Moved", row.InstructionMoveState);
        Assert.Equal(1, row.InstructionMoveAttempts);
        Assert.Equal(NowUtc, row.InstructionMovedAtUtc);
        Assert.Null(row.InstructionMoveFailureCode);
        Assert.Equal(operation.Version + 1, row.Version);
        var history = await context.CaseHistory.AsNoTracking().Where(item => item.CaseId == estate.CaseId).ToArrayAsync();
        var entry = Assert.Single(history);
        Assert.Equal("instruction_moved_to_deleted_items", entry.EventType);
        Assert.Equal(NowUtc, entry.OccurredAtUtc);
        Assert.Empty(await store.ListDueAsync(10, CancellationToken.None));
    }

    [Fact]
    public async Task AnInstructionAlreadyInDeletedItemsIsRecordedWithoutAMovedTime()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var operation = Operation(estate, SentAtUtc);
        await AddAsync(database, operation);

        Assert.True(await Store(database).RecordAsync(
            operation.Id, operation.Version, SentReportInstructionTidyOutcome.AlreadyMoved, null, NowUtc, CancellationToken.None));

        await using var context = await database.CreateContextAsync();
        var row = await context.Set<StaffMailSendOperationEntity>().AsNoTracking().SingleAsync(item => item.Id == operation.Id);
        Assert.Equal("AlreadyMoved", row.InstructionMoveState);
        Assert.Null(row.InstructionMovedAtUtc);
        Assert.Equal(
            "instruction_moved_to_deleted_items",
            Assert.Single(await context.CaseHistory.AsNoTracking().ToArrayAsync()).EventType);
    }

    [Fact]
    public async Task AMissingMessageIsRecordedOnTheSendRowAndWritesNoCaseHistory()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var operation = Operation(estate, SentAtUtc);
        await AddAsync(database, operation);

        Assert.True(await Store(database).RecordAsync(
            operation.Id, operation.Version, SentReportInstructionTidyOutcome.MessageMissing, null, NowUtc, CancellationToken.None));

        await using var context = await database.CreateContextAsync();
        var row = await context.Set<StaffMailSendOperationEntity>().AsNoTracking().SingleAsync(item => item.Id == operation.Id);
        Assert.Equal("MessageMissing", row.InstructionMoveState);
        Assert.Empty(await context.CaseHistory.AsNoTracking().ToArrayAsync());
        Assert.Empty(await Store(database).ListDueAsync(10, CancellationToken.None));
    }

    [Fact]
    public async Task AFailureStaysDueUntilTheThirdAttemptThenStopsAndEveryAttemptIsInTheHistory()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var operation = Operation(estate, SentAtUtc);
        await AddAsync(database, operation);
        var store = Store(database);

        for (var attempt = 1; attempt <= TidySentReportInstructions.MaximumAttempts; attempt++)
        {
            var due = Assert.Single(await store.ListDueAsync(10, CancellationToken.None));
            Assert.Equal(attempt - 1, due.Attempts);
            Assert.True(await store.RecordAsync(
                due.OperationId,
                due.OperationVersion,
                SentReportInstructionTidyOutcome.Failed,
                "mailbox_not_permitted",
                NowUtc.AddMinutes(attempt),
                CancellationToken.None));
        }

        Assert.Empty(await store.ListDueAsync(10, CancellationToken.None));
        await using var context = await database.CreateContextAsync();
        var row = await context.Set<StaffMailSendOperationEntity>().AsNoTracking().SingleAsync(item => item.Id == operation.Id);
        Assert.Equal("Failed", row.InstructionMoveState);
        Assert.Equal(3, row.InstructionMoveAttempts);
        Assert.Equal("mailbox_not_permitted", row.InstructionMoveFailureCode);
        Assert.Null(row.InstructionMovedAtUtc);
        var history = await context.CaseHistory.AsNoTracking().Where(item => item.CaseId == estate.CaseId).ToArrayAsync();
        Assert.Equal(3, history.Length);
        Assert.All(history, entry => Assert.Equal("instruction_move_failed", entry.EventType));
        Assert.Equal(3, history.Select(entry => entry.OperationKey).Distinct().Count());
    }

    [Fact]
    public async Task AFailureNeedsAFailureCode()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var operation = Operation(estate, SentAtUtc);
        await AddAsync(database, operation);

        await Assert.ThrowsAsync<ArgumentException>(() => Store(database).RecordAsync(
            operation.Id, operation.Version, SentReportInstructionTidyOutcome.Failed, " ", NowUtc, CancellationToken.None));
    }

    private static EfSentReportInstructionTidyStore Store(LocalDbTestDatabase database)
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

    private static StaffMailSendOperationEntity Operation(
        Estate estate,
        DateTimeOffset sentAtUtc,
        StaffMailPurpose purpose = StaffMailPurpose.CaseReport,
        StaffMailState state = StaffMailState.Sent,
        bool original = true)
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
            ContextId = estate.GenerationId,
            ContextVersion = 1,
            ComposeMode = original ? StaffMailComposeMode.Reply : StaffMailComposeMode.New,
            OriginalRetainedMessageId = original ? estate.RetainedMessageId : null,
            OriginalImmutableMessageId = original ? "immutable-instruction" : null,
            RecipientsJson = "[]",
            Subject = "report",
            Body = "report",
            AttachmentsJson = "[]",
            State = state,
            CorrelationMarker = $"mail:{id:N}",
            CreatedAtUtc = sentAtUtc,
            RequestedAtUtc = sentAtUtc,
            ObservedSentAtUtc = state == StaffMailState.Sent ? sentAtUtc : null,
            Version = 3,
            ConcurrencyToken = Guid.NewGuid()
        };
    }

    private sealed record Estate(Guid CaseId, Guid GenerationId, Guid MailboxId, Guid RetainedMessageId);

    private static async Task<Estate> SeedEstateAsync(LocalDbTestDatabase database)
    {
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var generationId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();
        var retainedMessageId = Guid.NewGuid();
        await using var db = await database.CreateContextAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {"Instruction tidy test organization"}, {0L})");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {SentAtUtc})");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, ReportSendingRulesJson, Version) VALUES ({principalId}, {organizationId}, {"TDY"}, {lineageId}, {true}, {EfOrganizationAdministration.DefaultReportSendingJson}, {0L})");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2026}, {1}, {"TDY260001"}, {"inspection"}, {"review"}, {"pending"}, {true}, {true}, {SentAtUtc}, {0L}, {Guid.NewGuid()})");
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
        db.ApprovedMailboxes.Add(new()
        {
            Id = mailboxId,
            Address = "engineers@example.invalid",
            AllowInboundIntake = true,
            AllowStaffSend = true,
            AllowSentEvidence = true,
            MailboxGeneration = 1,
            VerifiedEncodedMessageSizeLimit = 1_000_000,
            State = "Approved",
            MailboxIdentity = "engineers-mailbox",
            InboxFolderIdentity = "inbox",
            SentFolderIdentity = "sent",
            ActivatedAtUtc = SentAtUtc.AddDays(-1),
            Version = 1
        });
        db.ApprovedInboxPollStates.Add(new()
        {
            ApprovedMailboxId = mailboxId,
            MailboxAddress = "engineers@example.invalid",
            ScopeFingerprint = new string('A', 64),
            Generation = 1,
            ActivatedAtUtc = SentAtUtc.AddDays(-1),
            StartBoundaryUtc = SentAtUtc.AddDays(-1),
            DueAtUtc = SentAtUtc,
            LastCompletedAtUtc = SentAtUtc
        });
        db.Set<RetainedMailboxMessageEntity>().Add(new()
        {
            Id = retainedMessageId,
            MailboxId = mailboxId,
            MailboxAddress = "engineers@example.invalid",
            FolderScope = "inbox",
            FolderIdentity = "inbox",
            ImmutableMessageId = "immutable-instruction",
            InternetMessageIdentity = "<instruction@example.invalid>",
            ConversationIdentity = "conversation",
            ExternalReceiptToken = $"retained:{retainedMessageId:N}",
            ToAddressesJson = "[]",
            CcAddressesJson = "[]",
            SourceLength = 1,
            SourceSha256 = new string('A', 64),
            ReceivedAtUtc = SentAtUtc.AddHours(-2),
            RetainedAtUtc = SentAtUtc.AddHours(-2)
        });
        await db.SaveChangesAsync();
        return new(caseId, generationId, mailboxId, retainedMessageId);
    }
}
