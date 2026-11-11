using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// ADR-0071, R902. The send journal lists a confirmed send that carried the Delete
/// case correspondence tick, names the Case's received e-mails as they stand
/// linked now (Inbox scope, Approved and activated mailboxes, held when the
/// send was prepared) with the one original of a staff forward held in
/// instructions@ or pegasustest@ (R916, R917, R956, R957), records each run
/// under the operation's version guard where the Notes tab reads it (R959),
/// and stops listing after three failed runs.
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
    private const string EngineersAddress = "engineers@example.invalid";
    private const string DeactivatedAddress = "deactivated@example.invalid";
    private static readonly DateTimeOffset SentAtUtc = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset PreparedAtUtc = SentAtUtc.AddMinutes(-5);
    private static readonly DateTimeOffset NowUtc = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid InstructionsMailboxId =
        AdministrationPolicyModelConfiguration.InitialInstructionsMailboxId;
    private const long WorkflowVersion = 4;

    private enum Linking
    {
        // Received and never linked to a Case.
        None,
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
    /// one whose Graph identity has not been supplied, nor one that is not
    /// activated, as a production copy leaves every mailbox on the test
    /// instance (R916).
    /// </summary>
    [Fact]
    public async Task AMessageInAMailboxThatIsNotApprovedIdentifiedAndActivatedIsNotNamed()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var approved = await ReceiveAsync(database, estate.CaseId, Linking.AcceptedLink, estate.MailboxId);
        await ReceiveAsync(database, estate.CaseId, Linking.AcceptedLink, estate.WithdrawnMailboxId);
        await ReceiveAsync(database, estate.CaseId, Linking.AcceptedLink, estate.DeactivatedMailboxId);
        await ReceiveAsync(
            database,
            estate.CaseId,
            Linking.AcceptedLink,
            AdministrationPolicyModelConfiguration.InitialInstructionsMailboxId);

        var messages = await Store(database).ListMessagesAsync(estate.CaseId, PreparedAtUtc, CancellationToken.None);

        Assert.Equal(approved, Assert.Single(messages).RetainedMessageId);
    }

    /// <summary>
    /// R917: a staff forward held in instructions@ takes its original with it,
    /// from the mailbox that forwarded it, though the forward is the only
    /// e-mail linked to the Case. An original that is linked too is named once.
    /// </summary>
    [Fact]
    public async Task TheOriginalOfAStaffForwardHeldInInstructionsIsNamedThoughItHoldsNoCase()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        await EnableInstructionsMailboxAsync(database);
        var unlinked = await ReceiveOriginalAsync(
            database, estate.CaseId, Linking.None, estate.MailboxId, PreparedAtUtc.AddHours(-6));
        var linked = await ReceiveOriginalAsync(
            database, estate.CaseId, Linking.AcceptedLink, estate.MailboxId, PreparedAtUtc.AddHours(-5));
        var forward = await ReceiveForwardAsync(
            database, estate.CaseId, InstructionsMailboxId, "Engineers@Example.invalid", unlinked, PreparedAtUtc.AddHours(-4));
        var second = await ReceiveForwardAsync(
            database, estate.CaseId, InstructionsMailboxId, EngineersAddress, linked, PreparedAtUtc.AddHours(-3));

        var messages = await Store(database).ListMessagesAsync(estate.CaseId, PreparedAtUtc, CancellationToken.None);

        Assert.Equal([unlinked, linked, forward, second], messages.Select(item => item.RetainedMessageId));
        Assert.Equal(
            ["engineers-mailbox", "engineers-mailbox", "instructions-mailbox", "instructions-mailbox"],
            messages.Select(item => item.MailboxIdentity));
        Assert.Equal(ImmutableId(unlinked), messages[0].ImmutableMessageId);
    }

    /// <summary>
    /// A forward held in the test instance's own mailbox, pegasustest@, names
    /// its original as one held in instructions@ does (operator, 11 November
    /// 2026; R956).
    /// </summary>
    [Fact]
    public async Task TheOriginalOfAStaffForwardHeldInTheTestInstanceMailboxIsNamed()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var original = await ReceiveOriginalAsync(
            database, estate.CaseId, Linking.None, estate.MailboxId, PreparedAtUtc.AddHours(-6));
        var forward = await ReceiveForwardAsync(
            database, estate.CaseId, estate.TestInstanceMailboxId, EngineersAddress, original, PreparedAtUtc.AddHours(-4));

        var messages = await Store(database).ListMessagesAsync(estate.CaseId, PreparedAtUtc, CancellationToken.None);

        Assert.Equal([original, forward], messages.Select(item => item.RetainedMessageId));
        Assert.Equal(["engineers-mailbox", "pegasustest-mailbox"], messages.Select(item => item.MailboxIdentity));
    }

    /// <summary>
    /// One original for each forward (R957): the forward of a later message of
    /// a thread names the earlier ones too, and those stay in the forwarding
    /// mailbox unless they are themselves linked to the Case.
    /// </summary>
    [Fact]
    public async Task OnlyTheForwardedMessageGoesWithAForwardNotTheEarlierMessagesOfItsThread()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        await EnableInstructionsMailboxAsync(database);
        var root = await ReceiveOriginalAsync(
            database, estate.CaseId, Linking.None, estate.MailboxId, PreparedAtUtc.AddHours(-9));
        var linkedEarlier = await ReceiveOriginalAsync(
            database, estate.CaseId, Linking.AcceptedLink, estate.MailboxId, PreparedAtUtc.AddHours(-8));
        var forwarded = await ReceiveOriginalAsync(
            database, estate.CaseId, Linking.None, estate.MailboxId, PreparedAtUtc.AddHours(-7));
        var forward = await ReceiveForwardAsync(
            database, estate.CaseId, InstructionsMailboxId, EngineersAddress, forwarded, PreparedAtUtc.AddHours(-4),
            root, linkedEarlier);

        var messages = await Store(database).ListMessagesAsync(estate.CaseId, PreparedAtUtc, CancellationToken.None);

        Assert.DoesNotContain(root, messages.Select(item => item.RetainedMessageId));
        Assert.Equal([linkedEarlier, forwarded, forward], messages.Select(item => item.RetainedMessageId));
    }

    /// <summary>
    /// R917's bounds: an original that holds another Case is left (R958), as is
    /// one in a mailbox that is not activated or that did not send the forward,
    /// and only a forward held in instructions@ or pegasustest@ names an
    /// original at all.
    /// </summary>
    [Fact]
    public async Task AnOriginalThatHoldsAnotherCaseOrSitsOutsideTheForwardingMailboxIsLeft()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        await EnableInstructionsMailboxAsync(database);
        var heldElsewhere = await ReceiveOriginalAsync(
            database, estate.OtherCaseId, Linking.AcceptedLink, estate.MailboxId, PreparedAtUtc.AddHours(-9));
        var notActivated = await ReceiveOriginalAsync(
            database, estate.CaseId, Linking.None, estate.DeactivatedMailboxId, PreparedAtUtc.AddHours(-9));
        var notTheForwarder = await ReceiveOriginalAsync(
            database, estate.CaseId, Linking.None, estate.MailboxId, PreparedAtUtc.AddHours(-9));
        var notInstructions = await ReceiveOriginalAsync(
            database, estate.CaseId, Linking.None, estate.MailboxId, PreparedAtUtc.AddHours(-9));
        var first = await ReceiveForwardAsync(
            database, estate.CaseId, InstructionsMailboxId, EngineersAddress, heldElsewhere, PreparedAtUtc.AddHours(-4));
        var second = await ReceiveForwardAsync(
            database, estate.CaseId, InstructionsMailboxId, DeactivatedAddress, notActivated, PreparedAtUtc.AddHours(-3));
        var third = await ReceiveForwardAsync(
            database, estate.CaseId, InstructionsMailboxId, DeactivatedAddress, notTheForwarder, PreparedAtUtc.AddHours(-2));
        // Held in engineers@, not instructions@ or pegasustest@: a forward there names no original (R652).
        var fourth = await ReceiveForwardAsync(
            database, estate.CaseId, estate.MailboxId, EngineersAddress, notInstructions, PreparedAtUtc.AddHours(-1));

        var messages = await Store(database).ListMessagesAsync(estate.CaseId, PreparedAtUtc, CancellationToken.None);

        Assert.Equal([first, second, third, fourth], messages.Select(item => item.RetainedMessageId));
    }

    /// <summary>
    /// R975: a staff forward's original that Pegasus holds no copy of, as
    /// when the mailbox that forwarded it is not read for Incoming mail, is
    /// named by that mailbox and the Message-ID the forward answers, so the
    /// tidy finds it in the mailbox itself. Only the forwarded message is
    /// named, not the earlier messages of its thread (R957).
    /// </summary>
    [Fact]
    public async Task AStaffForwardsOriginalPegasusNeverRetainedIsNamedInTheMailboxThatForwardedIt()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        await EnableInstructionsMailboxAsync(database);
        var neverRetained = Guid.NewGuid();
        var earlierNeverRetained = Guid.NewGuid();
        var forward = await ReceiveForwardAsync(
            database, estate.CaseId, InstructionsMailboxId, "Engineers@Example.invalid", neverRetained,
            PreparedAtUtc.AddHours(-4), earlierNeverRetained);
        var store = Store(database);

        var originals = await store.ListUnretainedForwardOriginalsAsync(estate.CaseId, PreparedAtUtc, CancellationToken.None);

        Assert.Equal(
            new UnretainedForwardOriginal("engineers-mailbox", "sent", ForwardedIdentity(neverRetained)),
            Assert.Single(originals));
        // The forward itself is still the retained path's.
        Assert.Equal(
            forward,
            Assert.Single(await store.ListMessagesAsync(estate.CaseId, PreparedAtUtc, CancellationToken.None)).RetainedMessageId);
    }

    /// <summary>
    /// An original Pegasus retained is never looked for in the mailbox: it is
    /// moved by its own identity, or left because it holds another Case
    /// (R958), whichever the retained path decides.
    /// </summary>
    [Fact]
    public async Task ARetainedOriginalIsNeverNamedAsUnretained()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        await EnableInstructionsMailboxAsync(database);
        var retained = await ReceiveOriginalAsync(
            database, estate.CaseId, Linking.None, estate.MailboxId, PreparedAtUtc.AddHours(-6));
        var heldElsewhere = await ReceiveOriginalAsync(
            database, estate.OtherCaseId, Linking.AcceptedLink, estate.MailboxId, PreparedAtUtc.AddHours(-6));
        await ReceiveForwardAsync(
            database, estate.CaseId, InstructionsMailboxId, EngineersAddress, retained, PreparedAtUtc.AddHours(-4));
        await ReceiveForwardAsync(
            database, estate.CaseId, InstructionsMailboxId, EngineersAddress, heldElsewhere, PreparedAtUtc.AddHours(-3));

        var originals = await Store(database).ListUnretainedForwardOriginalsAsync(
            estate.CaseId, PreparedAtUtc, CancellationToken.None);

        Assert.Empty(originals);
    }

    /// <summary>
    /// The bounds of an unretained original are the retained one's: only a
    /// mailbox that is Approved, identified and activated is written to
    /// (R916), the forwarder must be an approved mailbox, only a forward held
    /// in instructions@ or pegasustest@ names an original, and only mail held
    /// when the send was prepared counts.
    /// </summary>
    [Fact]
    public async Task AnUnretainedOriginalIsNamedOnlyForAWritableForwardingMailboxAndAForwardHeldInInstructions()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        await EnableInstructionsMailboxAsync(database);
        var viaTestInstance = Guid.NewGuid();
        await ReceiveForwardAsync(
            database, estate.CaseId, InstructionsMailboxId, DeactivatedAddress, Guid.NewGuid(), PreparedAtUtc.AddHours(-6));
        await ReceiveForwardAsync(
            database, estate.CaseId, InstructionsMailboxId, "withdrawn@example.invalid", Guid.NewGuid(), PreparedAtUtc.AddHours(-5));
        await ReceiveForwardAsync(
            database, estate.CaseId, InstructionsMailboxId, "someone@elsewhere.invalid", Guid.NewGuid(), PreparedAtUtc.AddHours(-4));
        // Held in engineers@, not instructions@ or pegasustest@: a forward there names no original (R652).
        await ReceiveForwardAsync(
            database, estate.CaseId, estate.MailboxId, EngineersAddress, Guid.NewGuid(), PreparedAtUtc.AddHours(-3));
        await ReceiveForwardAsync(
            database, estate.CaseId, InstructionsMailboxId, EngineersAddress, Guid.NewGuid(), PreparedAtUtc.AddMinutes(1));
        await ReceiveForwardAsync(
            database, estate.CaseId, estate.TestInstanceMailboxId, EngineersAddress, viaTestInstance, PreparedAtUtc.AddHours(-2));

        var originals = await Store(database).ListUnretainedForwardOriginalsAsync(
            estate.CaseId, PreparedAtUtc, CancellationToken.None);

        Assert.Equal(
            new UnretainedForwardOriginal("engineers-mailbox", "sent", ForwardedIdentity(viaTestInstance)),
            Assert.Single(originals));
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
        Assert.Empty(await context.CaseWorkflowEvents.AsNoTracking().ToArrayAsync());
        Assert.Equal(operation.Id, Assert.Single(await store.ListDueAsync(10, CancellationToken.None)).OperationId);
    }

    [Fact]
    public async Task AMovedRunIsDoneWithOneNotesEntryAndNoLongerDue()
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
        // A report send's Case is its generation's. The entry is on the timeline the
        // Notes tab reads (R959), at the Case's version as it stands.
        var entry = Assert.Single(await context.CaseWorkflowEvents.AsNoTracking().ToArrayAsync());
        Assert.Equal(estate.CaseId, entry.CaseId);
        Assert.Equal("correspondence_moved_to_deleted_items", entry.EventType);
        Assert.Equal(nameof(Pegasus.Core.Identity.ActorKind.SystemWorker), entry.ActorKind);
        Assert.Equal(Actor, entry.ActorSubjectId);
        Assert.Equal(WorkflowVersion, entry.BeforeVersion);
        Assert.Equal(WorkflowVersion, entry.AfterVersion);
        Assert.Equal(
            WorkflowVersion,
            (await context.CaseWorkflows.AsNoTracking().SingleAsync(item => item.CaseId == estate.CaseId)).Version);
        Assert.Empty(await context.CaseHistory.AsNoTracking().ToArrayAsync());
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
        var entry = Assert.Single(await context.CaseWorkflowEvents.AsNoTracking().ToArrayAsync());
        Assert.Equal(estate.OtherCaseId, entry.CaseId);
        Assert.Equal("correspondence_moved_to_deleted_items", entry.EventType);
        Assert.Equal(
            "The Case's received e-mails were moved to Deleted Items after the send: 0 moved, 2 already there, 0 no longer in the mailbox.",
            entry.Reason);
    }

    /// <summary>
    /// A Triage Case has no Case workflow and its Notes tab reads the Triage's
    /// own history, so a Reply with finding's run is written there, without
    /// taking a Triage version (R959).
    /// </summary>
    [Fact]
    public async Task ATriageCasesRunIsOnTheTriagesOwnHistoryWithoutAVersion()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();
        var estate = await SeedEstateAsync(database);
        var (triageCaseId, _) = await SeedTriageAsync(database, estate);
        var operation = Operation(
            estate, SentAtUtc, StaffMailPurpose.TriageOutcomeReply, contextId: triageCaseId);
        await AddAsync(database, operation);

        Assert.True(await Store(database).RecordAsync(
            operation.Id, operation.Version, new(1, 0, 0, 0), null, NowUtc, CancellationToken.None));

        await using var context = await database.CreateContextAsync();
        var entry = Assert.Single(await context.TriageHistory.AsNoTracking()
            .Where(item => item.TriageCaseId == triageCaseId)
            .ToArrayAsync());
        Assert.Equal("correspondence_moved_to_deleted_items", entry.EventType);
        Assert.Equal(Actor, entry.Actor);
        Assert.Equal(nameof(Pegasus.Core.Identity.ActorKind.SystemWorker), entry.ActorKind);
        Assert.Equal(
            "The Case's received e-mails were moved to Deleted Items after the send: 1 moved, 0 already there, 0 no longer in the mailbox.",
            entry.Reason);
        Assert.Equal($"correspondence-tidy:{operation.Id:N}:1", entry.OperationKey);
        Assert.Equal(0L, entry.BeforeVersion);
        Assert.Equal(0L, entry.AfterVersion);
        Assert.Equal("open", entry.AfterState);
        Assert.Equal(0L, (await context.Triage.AsNoTracking().SingleAsync(item => item.CaseId == triageCaseId)).Version);
        Assert.Empty(await context.CaseWorkflowEvents.AsNoTracking().ToArrayAsync());
    }

    /// <summary>
    /// Messages all gone from the mailbox, or a Case with no received e-mail,
    /// changed nothing on the Case: the run is recorded on the send row alone.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(0)]
    public async Task ARunThatFoundNothingToMoveIsDoneWithoutANotesEntry(int missing)
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
        Assert.Empty(await context.CaseWorkflowEvents.AsNoTracking().ToArrayAsync());
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
        // Three entries at one Case version: the version index leaves these event types out.
        var history = await context.CaseWorkflowEvents.AsNoTracking()
            .Where(item => item.CaseId == estate.CaseId)
            .OrderBy(item => item.OccurredAtUtc)
            .ToArrayAsync();
        Assert.All(history, entry => Assert.Equal("correspondence_move_failed", entry.EventType));
        Assert.All(history, entry => Assert.Equal(WorkflowVersion, entry.AfterVersion));
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
            (await context.CaseWorkflowEvents.AsNoTracking().OrderBy(item => item.OccurredAtUtc).ToArrayAsync())
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

    /// <summary>The Message-ID a forward retains for the original <see cref="ReceiveForwardAsync"/> names.</summary>
    private static string ForwardedIdentity(Guid original) =>
        MailboxMessageIdentity.CanonicalizeInternetMessageIdentity($"{original:N}@example.invalid");

    /// <summary>The seeded instructions@ row with its Graph identities saved and activated.</summary>
    private static async Task EnableInstructionsMailboxAsync(LocalDbTestDatabase database)
    {
        await using var db = await database.CreateContextAsync();
        var mailbox = await db.ApprovedMailboxes.SingleAsync(item => item.Id == InstructionsMailboxId);
        mailbox.MailboxIdentity = "instructions-mailbox";
        mailbox.InboxFolderIdentity = "inbox";
        mailbox.SentFolderIdentity = "sent";
        mailbox.ActivatedAtUtc = SentAtUtc.AddDays(-1);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// A received e-mail whose Message-ID is retained in its comparison form,
    /// as the Inbox poll retains it, so a forward can name it.
    /// </summary>
    private static Task<Guid> ReceiveOriginalAsync(
        LocalDbTestDatabase database,
        Guid caseId,
        Linking linking,
        Guid mailboxId,
        DateTimeOffset receivedAtUtc) =>
        ReceiveAsync(
            database, caseId, linking, mailboxId, receivedAtUtc: receivedAtUtc,
            shape: message => message.CanonicalInternetMessageIdentity =
                MailboxMessageIdentity.CanonicalizeInternetMessageIdentity(message.InternetMessageIdentity!));

    /// <summary>
    /// A message linked to the Case that <paramref name="forwardedFrom"/> sent
    /// and whose In-Reply-To names <paramref name="original"/>, without angle
    /// brackets, as the MIME reader retains a reply identity.
    /// </summary>
    private static Task<Guid> ReceiveForwardAsync(
        LocalDbTestDatabase database,
        Guid caseId,
        Guid holdingMailboxId,
        string forwardedFrom,
        Guid original,
        DateTimeOffset receivedAtUtc,
        params Guid[] earlierInThread) =>
        ReceiveAsync(
            database, caseId, Linking.AcceptedLink, holdingMailboxId, receivedAtUtc: receivedAtUtc,
            shape: message =>
            {
                message.SenderAddress = forwardedFrom;
                // The message answered first, then the rest of its thread, as the reader retains them.
                message.InReplyToIdentitiesJson = JsonSerializer.Serialize(earlierInThread
                    .Prepend(original)
                    .Select(id => MailboxMessageIdentity.CanonicalizeInternetMessageIdentity($"{id:N}@example.invalid"))
                    .ToArray());
            });

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
        DateTimeOffset? dismissedAtUtc = null,
        Action<RetainedMailboxMessageEntity>? shape = null)
    {
        var receiptId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var token = $"retained:{messageId:N}";
        var receivedAt = receivedAtUtc ?? PreparedAtUtc.AddHours(-2);
        await using var db = await database.CreateContextAsync();
        db.IntakeReceipts.Add(Receipt(receiptId, channel, token, receivedAt));
        var message = Message(
            messageId, mailboxId, folderScope, token, receivedAt, retainedAtUtc ?? receivedAt, dismissedAtUtc);
        shape?.Invoke(message);
        db.RetainedMailboxMessages.Add(message);
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
        MailboxAddress = mailboxId is null ? "upload" : EngineersAddress,
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
        Guid WithdrawnMailboxId,
        Guid DeactivatedMailboxId,
        Guid TestInstanceMailboxId);

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
        var deactivatedMailboxId = Guid.NewGuid();
        var testInstanceMailboxId = Guid.NewGuid();
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
        foreach (var workflowCaseId in new[] { caseId, otherCaseId })
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({workflowCaseId}, {"Review"}, {WorkflowVersion}, {Guid.NewGuid()})");
        }
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
                Address = EngineersAddress,
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
            },
            // Approved and identified, but not enabled: what a production copy
            // leaves on the test instance (R627).
            new ApprovedMailboxEntity
            {
                Id = deactivatedMailboxId,
                Address = DeactivatedAddress,
                AllowIncoming = true,
                AllowOutgoing = true,
                MailboxGeneration = 1,
                State = "Approved",
                MailboxIdentity = "deactivated-mailbox",
                InboxFolderIdentity = "inbox",
                SentFolderIdentity = "sent",
                ActivatedAtUtc = null,
                Version = 1
            },
            // The hosted test instance's own mailbox (R693), which staff forward into there.
            new ApprovedMailboxEntity
            {
                Id = testInstanceMailboxId,
                Address = PrincipalMailRoutePolicy.TestInstanceMailboxAddress,
                AllowIncoming = true,
                AllowOutgoing = true,
                MailboxGeneration = 1,
                State = "Approved",
                MailboxIdentity = "pegasustest-mailbox",
                InboxFolderIdentity = "inbox",
                SentFolderIdentity = "sent",
                ActivatedAtUtc = SentAtUtc.AddDays(-1),
                Version = 1
            });
        await db.SaveChangesAsync();
        return new(
            principalId, lineageId, caseId, otherCaseId, generationId, mailboxId, withdrawnMailboxId,
            deactivatedMailboxId, testInstanceMailboxId);
    }
}
