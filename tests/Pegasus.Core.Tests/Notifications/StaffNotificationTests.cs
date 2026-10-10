using Pegasus.Core.AiWork;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Notifications;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Notifications;

public sealed class StaffNotificationTests
{
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly Guid EngineerId = Guid.NewGuid();
    private static readonly Guid StarterId = Guid.NewGuid();
    private static readonly Guid OtherId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(StaffNotificationCause.EditedByOther)]
    [InlineData(StaffNotificationCause.EmailReceived)]
    [InlineData(StaffNotificationCause.QueryReceived)]
    [InlineData(StaffNotificationCause.CancellationReceived)]
    [InlineData(StaffNotificationCause.CaseAssigned)]
    public void TheEngineerIsToldUnlessTheyDidItThemself(StaffNotificationCause cause)
    {
        Assert.Equal(EngineerId, StaffNotificationPolicy.Recipient(cause, EngineerId, null, OtherId));
        Assert.Null(StaffNotificationPolicy.Recipient(cause, EngineerId, null, EngineerId));
        // A Case with no engineer has nobody to tell; a User who created it is never told.
        Assert.Null(StaffNotificationPolicy.Recipient(cause, null, StarterId, OtherId));
    }

    [Fact]
    public void AnAiDraftGoesToTheEngineerOtherwiseToWhoeverStartedTheJob()
    {
        Assert.Equal(EngineerId, StaffNotificationPolicy.Recipient(StaffNotificationCause.AiDraftReady, EngineerId, StarterId, null));
        Assert.Equal(StarterId, StaffNotificationPolicy.Recipient(StaffNotificationCause.AiDraftReady, null, StarterId, null));
        Assert.Null(StaffNotificationPolicy.Recipient(StaffNotificationCause.AiDraftReady, null, null, null));
    }

    [Fact]
    public void EachDraftKindOpensItsOwnPlaceAndMarketResearchOpensNothing()
    {
        var message = Guid.NewGuid();
        var item = Guid.NewGuid();

        Assert.Equal($"/Cases/{CaseId:D}?section=estimate", StaffNotificationPolicy.AiDraftRoute(Job(AiJobKind.Estimate, CaseId)));
        Assert.Equal($"/Inbox/{message:D}", StaffNotificationPolicy.AiDraftRoute(Job(AiJobKind.QueryResponse, CaseId, message.ToString("D"))));
        Assert.Equal($"/Cases/{CaseId:D}?section=correspondence", StaffNotificationPolicy.AiDraftRoute(Job(AiJobKind.QueryResponse, CaseId, "draft text")));
        Assert.Equal($"/Unidentified/{item:D}", StaffNotificationPolicy.AiDraftRoute(Job(AiJobKind.UnidentifiedResolution, item, kind: AiJobSubjectKind.Unidentified)));
        Assert.Null(StaffNotificationPolicy.AiDraftRoute(Job(AiJobKind.MarketResearch, CaseId)));
        Assert.Null(StaffNotificationPolicy.AiDraftRoute(Job(AiJobKind.UnidentifiedQueuePass, null, kind: AiJobSubjectKind.Queue)));
    }

    [Fact]
    public async Task RaisingWritesTheRowForTheRecipientAndNothingForNobody()
    {
        var store = new FakeStore();
        var raise = new RaiseStaffNotification(store);
        var actor = ActionActor.Staff(OtherId, [StaffRole.User]);

        var written = await raise.ExecuteAsync(
            new(StaffNotificationCause.EditedByOther, CaseId, "QDOS260001", "AB12CDE", " /Cases/x?section=notes ", EngineerId, actor),
            default);
        var nobody = await raise.ExecuteAsync(
            new(StaffNotificationCause.EditedByOther, CaseId, "QDOS260001", null, "/Cases/x", null, actor),
            default);

        Assert.NotNull(written);
        Assert.Equal(EngineerId, written.StaffId);
        Assert.Equal("/Cases/x?section=notes", written.Route);
        Assert.Equal(actor.SubjectId, written.ActorSubjectId);
        Assert.True(written.IsUnread);
        Assert.Null(nobody);
        Assert.Single(store.Rows);
        await Assert.ThrowsAsync<ArgumentException>(() => raise.ExecuteAsync(
            new(StaffNotificationCause.CaseAssigned, CaseId, "QDOS260001", null, "https://elsewhere", EngineerId, actor), default));
    }

    [Fact]
    public async Task TheCaseNotifierGivenTheReferenceAndEngineerDoesNotReadTheWorkflowAndStillSkipsTheEngineerThemself()
    {
        var store = new FakeStore();
        var workflows = new FakeWorkflows(Workflow(CaseLifecycleState.Review, EngineerId));
        var notifier = new CaseStaffNotifier(new RaiseStaffNotification(store), workflows, new NoReceipts());

        var told = await notifier.NotifyAsync(
            StaffNotificationCause.EditedByOther, CaseId, "QDOS260009", EngineerId,
            ActionActor.Staff(OtherId, [StaffRole.User]), "notes", "AB12CDE", default);
        var own = await notifier.NotifyAsync(
            StaffNotificationCause.EditedByOther, CaseId, "QDOS260009", EngineerId,
            ActionActor.Staff(EngineerId, [StaffRole.Engineer]), "notes", null, default);

        Assert.Equal("QDOS260009", told!.Reference);
        Assert.Equal(EngineerId, told.StaffId);
        Assert.Null(own);
        Assert.Equal(0, workflows.Reads);
    }

    [Fact]
    public async Task TheCaseNotifierReadsTheEngineerAndTellsQueriesFromEmail()
    {
        var store = new FakeStore();
        var workflows = new FakeWorkflows(Workflow(CaseLifecycleState.Query, EngineerId));
        var notifier = new CaseStaffNotifier(new RaiseStaffNotification(store), workflows, new NoReceipts());

        var query = await notifier.NotifyMailArrivalAsync(CaseId, Guid.NewGuid(), null, default);
        workflows.Current = Workflow(CaseLifecycleState.PostReport, EngineerId);
        var mail = await notifier.NotifyMailArrivalAsync(CaseId, Guid.NewGuid(), ActionActor.Staff(OtherId, [StaffRole.User]), default);
        var own = await notifier.NotifyMailArrivalAsync(CaseId, Guid.NewGuid(), ActionActor.Staff(EngineerId, [StaffRole.Engineer]), default);
        workflows.Current = Workflow(CaseLifecycleState.Review, null);
        var unassigned = await notifier.NotifyMailArrivalAsync(CaseId, Guid.NewGuid(), null, default);

        Assert.Equal(StaffNotificationCause.QueryReceived, query!.Cause);
        Assert.Equal($"/Cases/{CaseId:D}?section=correspondence", query.Route);
        Assert.Equal(StaffNotificationCause.EmailReceived, mail!.Cause);
        Assert.Equal("QDOS260001", mail.Reference);
        Assert.Null(own);
        Assert.Null(unassigned);
    }

    [Fact]
    public async Task ACancellationOnAnOpenCaseIsNamedAsOneAndAClosedCaseHearsOfOrdinaryMail()
    {
        var store = new FakeStore();
        var workflows = new FakeWorkflows(Workflow(CaseLifecycleState.ReportPreparation, EngineerId));
        var receiptId = Guid.NewGuid();
        var receipts = new FakeReceipts(
            receiptId,
            MailCategory.Received(ReceivedMailFamily.InProgressCases, MailCategory.CancellationSubtype));
        var notifier = new CaseStaffNotifier(new RaiseStaffNotification(store), workflows, receipts);

        var open = await notifier.NotifyMailArrivalAsync(CaseId, receiptId, null, default);
        var corrected = await notifier.NotifyCancellationReceivedAsync(CaseId, ActionActor.Staff(OtherId, [StaffRole.User]), default);
        var other = await notifier.NotifyMailArrivalAsync(CaseId, Guid.NewGuid(), null, default);
        workflows.Current = Workflow(CaseLifecycleState.PostReportComplete, EngineerId);
        var closed = await notifier.NotifyMailArrivalAsync(CaseId, receiptId, null, default);
        var closedCorrection = await notifier.NotifyCancellationReceivedAsync(CaseId, null, default);

        Assert.Equal(StaffNotificationCause.CancellationReceived, open!.Cause);
        Assert.Equal($"/Cases/{CaseId:D}?section=correspondence", open.Route);
        Assert.Equal(StaffNotificationCause.CancellationReceived, corrected!.Cause);
        Assert.Equal(StaffNotificationCause.EmailReceived, other!.Cause);
        Assert.Equal(StaffNotificationCause.EmailReceived, closed!.Cause);
        Assert.Null(closedCorrection);
    }

    [Fact]
    public async Task ACancellationOnACaseAlreadyInQueryIsNamedAsACancellation()
    {
        var store = new FakeStore();
        var workflows = new FakeWorkflows(Workflow(CaseLifecycleState.Query, EngineerId));
        var receiptId = Guid.NewGuid();
        var receipts = new FakeReceipts(
            receiptId,
            MailCategory.Received(ReceivedMailFamily.InProgressCases, MailCategory.CancellationSubtype));
        var notifier = new CaseStaffNotifier(new RaiseStaffNotification(store), workflows, receipts);

        var cancellation = await notifier.NotifyMailArrivalAsync(
            CaseId, receiptId, ActionActor.Staff(OtherId, [StaffRole.User]), default);

        Assert.Equal(StaffNotificationCause.CancellationReceived, cancellation!.Cause);
        Assert.Equal(EngineerId, cancellation.StaffId);
        Assert.Equal($"/Cases/{CaseId:D}?section=correspondence", cancellation.Route);
    }

    [Fact]
    public async Task AnAiDraftOnACaseTellsTheEngineerAndAnUnidentifiedDraftTellsTheStarter()
    {
        var store = new FakeStore();
        var workflows = new FakeWorkflows(Workflow(CaseLifecycleState.ReportPreparation, EngineerId));
        var notifier = new CaseStaffNotifier(new RaiseStaffNotification(store), workflows, new NoReceipts());
        var item = Guid.NewGuid();

        var estimate = await notifier.NotifyAiDraftReadyAsync(Job(AiJobKind.Estimate, CaseId) with { State = AiJobState.DraftReady }, default);
        var unidentified = await notifier.NotifyAiDraftReadyAsync(
            Job(AiJobKind.UnidentifiedResolution, item, kind: AiJobSubjectKind.Unidentified) with { State = AiJobState.DraftReady }, default);
        var research = await notifier.NotifyAiDraftReadyAsync(Job(AiJobKind.MarketResearch, CaseId) with { State = AiJobState.DraftReady }, default);
        var notReady = await notifier.NotifyAiDraftReadyAsync(Job(AiJobKind.Estimate, CaseId), default);

        Assert.Equal(EngineerId, estimate!.StaffId);
        Assert.Equal(CaseId, estimate.CaseId);
        Assert.Equal(StarterId, unidentified!.StaffId);
        Assert.Null(unidentified.CaseId);
        Assert.Equal("U-00001", unidentified.Reference);
        Assert.Null(research);
        Assert.Null(notReady);
    }

    [Fact]
    public async Task TheBellIsPersonalAndThirtyDaysDeep()
    {
        var store = new FakeStore();
        var raise = new RaiseStaffNotification(store);
        var engineer = ActionActor.Staff(EngineerId, [StaffRole.Engineer]);
        var bell = new MyStaffNotifications(store, new FixedTime(Now));
        await raise.ExecuteAsync(new(StaffNotificationCause.CaseAssigned, CaseId, "QDOS260001", null, "/Cases/x", EngineerId, null), default);
        await raise.ExecuteAsync(new(StaffNotificationCause.CaseAssigned, CaseId, "QDOS260002", null, "/Cases/y", OtherId, null), default);
        store.Rows[0] = store.Rows[0] with { RaisedAtUtc = Now.AddDays(-31) };

        Assert.Empty(await bell.ListAsync(engineer, default));
        store.Rows[0] = store.Rows[0] with { RaisedAtUtc = Now.AddDays(-29) };
        var mine = Assert.Single(await bell.ListAsync(engineer, default));
        Assert.Equal(1, await bell.CountUnreadAsync(engineer, default));

        var opened = await bell.OpenAsync(engineer, mine.Id, default);
        Assert.Equal(Now, opened!.ReadAtUtc);
        Assert.Equal(0, await bell.CountUnreadAsync(engineer, default));
        Assert.Null(await bell.OpenAsync(ActionActor.Staff(OtherId, [StaffRole.User]), mine.Id, default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => bell.ListAsync(ActionActor.Automation("client"), default));
    }

    private static CaseWorkflowRecord Workflow(CaseLifecycleState state, Guid? engineerId) => new(
        CaseId, new(CaseId, "QDOS", 2026, 1, "QDOS260001"), state, engineerId,
        null, null, null, null, null, null, 1);

    private static AiJobRecord Job(AiJobKind jobKind, Guid? subjectId, string? resultReference = null, AiJobSubjectKind kind = AiJobSubjectKind.Case) => new(
        Guid.NewGuid(), jobKind, kind, subjectId, kind == AiJobSubjectKind.Unidentified ? "U-00001" : "QDOS260001",
        "Do the work.", null, null, AiJobState.Taken, ActorKind.Staff, StarterId.ToString("D"), Now,
        Now + AiJobPolicy.DefaultExpiry, "client", Now, Now + AiJobPolicy.LeaseDuration, null, null, resultReference, null, null, null, 1);

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeWorkflows(CaseWorkflowRecord current) : ICaseWorkflowQueries
    {
        public CaseWorkflowRecord Current { get; set; } = current;

        public int Reads { get; private set; }

        public Task<CaseWorkflowRecord?> GetAsync(Guid caseId, CancellationToken cancellationToken)
        {
            Reads++;
            return Task.FromResult<CaseWorkflowRecord?>(caseId == Current.CaseId ? Current : null);
        }

        public Task<IReadOnlyDictionary<Guid, Guid?>> GetAssignedEngineersAsync(
            IReadOnlyCollection<Guid> caseIds,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> HasOperationAsync(Guid caseId, string operationKey, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class FakeReceipts(Guid receiptId, MailCategory category) : IIntakeReceiptQueries
    {
        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<IntakeReceipt?>(id != receiptId ? null : new(
                receiptId, "message.eml", "message/rfc822", 1, new string('A', 64),
                new(IntakeSourceChannel.Mailbox, "token"), Now, Now, IntakeDecision.NeedsSorting, "Fixture.",
                [], [], null, [], null, null, false, "reader", "1", null, null,
                MailClassificationDecision: MailClassificationResult.Classified(category, [], "Fixture.", "policy", 1)));
    }

    private sealed class NoReceipts : IIntakeReceiptQueries
    {
        public Task<IntakeReceipt?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<IntakeReceipt?>(null);
    }

    private sealed class FakeStore : IStaffNotificationStore
    {
        public List<StaffNotification> Rows { get; } = [];

        public Task<StaffNotification> AddAsync(NewStaffNotification notification, CancellationToken cancellationToken)
        {
            var row = new StaffNotification(
                Guid.NewGuid(), notification.StaffId, notification.CaseId, notification.Reference, notification.Registration,
                notification.Cause, notification.Route, Now, null)
            {
                ActorSubjectId = notification.ActorSubjectId
            };
            Rows.Add(row);
            return Task.FromResult(row);
        }

        public Task<IReadOnlyList<StaffNotification>> ListAsync(Guid staffId, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StaffNotification>>(
                Rows.Where(row => row.StaffId == staffId && row.RaisedAtUtc >= sinceUtc).OrderByDescending(row => row.RaisedAtUtc).ToArray());

        public Task<int> CountUnreadAsync(Guid staffId, DateTimeOffset sinceUtc, CancellationToken cancellationToken) =>
            Task.FromResult(Rows.Count(row => row.StaffId == staffId && row.RaisedAtUtc >= sinceUtc && row.ReadAtUtc is null));

        public Task<StaffNotification?> MarkReadAsync(Guid staffId, Guid notificationId, DateTimeOffset readAtUtc, CancellationToken cancellationToken)
        {
            var index = Rows.FindIndex(row => row.Id == notificationId && row.StaffId == staffId);
            if (index < 0)
            {
                return Task.FromResult<StaffNotification?>(null);
            }

            Rows[index] = Rows[index] with { ReadAtUtc = Rows[index].ReadAtUtc ?? readAtUtc };
            return Task.FromResult<StaffNotification?>(Rows[index]);
        }

        public Task<int> MarkAllReadAsync(Guid staffId, DateTimeOffset readAtUtc, CancellationToken cancellationToken)
        {
            var count = 0;
            for (var index = 0; index < Rows.Count; index++)
            {
                if (Rows[index].StaffId == staffId && Rows[index].ReadAtUtc is null)
                {
                    Rows[index] = Rows[index] with { ReadAtUtc = readAtUtc };
                    count++;
                }
            }

            return Task.FromResult(count);
        }

        public Task<int> PurgeOlderThanAsync(DateTimeOffset cutoffUtc, CancellationToken cancellationToken) =>
            Task.FromResult(Rows.RemoveAll(row => row.RaisedAtUtc < cutoffUtc));
    }
}
