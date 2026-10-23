using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

public sealed class CreateAuditTests
{
    private static readonly Guid CaseId = Guid.Parse("0f3c5a2e-8d71-4b6a-9e04-6a1d2c3b4e51");
    private static readonly Guid EngineerId = Guid.Parse("7a4b1c9d-2e3f-4a5b-8c6d-9e0f1a2b3c4d");
    private static readonly Guid CustodyWorkId = Guid.Parse("c2d3e4f5-a6b7-4c8d-9e0f-1a2b3c4d5e6f");
    private static readonly ActionActor Staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Create audit is allowed in every working state, whether or not the
    /// Inspection report has been sent (operator, 1 October 2026).
    /// </summary>
    [Theory]
    [InlineData(CaseLifecycleState.NotReady)]
    [InlineData(CaseLifecycleState.Review)]
    [InlineData(CaseLifecycleState.ReportPreparation)]
    [InlineData(CaseLifecycleState.ReportSent)]
    public void CreateAuditIsAllowedInEveryWorkingState(CaseLifecycleState state)
    {
        Assert.Null(AuditPolicy.Refusal(CaseType.InspectionAndAudit, Workflow(state), PrimaryOnly()));
        Assert.Null(AuditPolicy.Refusal(CaseType.InspectionAndAudit, Workflow(state), works: null));
        Assert.Null(AuditPolicy.Refusal(CaseType.InspectionAndAudit, Workflow(state, sent: false), PrimaryOnly()));
    }

    [Fact]
    public void AHeldCaseIsRefused()
    {
        Assert.Equal(
            AuditRefusal.Held,
            AuditPolicy.Refusal(CaseType.InspectionAndAudit, Workflow(CaseLifecycleState.Held), PrimaryOnly()));
    }

    [Theory]
    [InlineData(CaseLifecycleState.PrincipalCancelled)]
    [InlineData(CaseLifecycleState.CollisionEngineersRejected)]
    [InlineData(CaseLifecycleState.SourceEmailUnlinked)]
    public void AClosedDispositionIsRefused(CaseLifecycleState state)
    {
        Assert.Equal(
            AuditRefusal.Closed,
            AuditPolicy.Refusal(CaseType.InspectionAndAudit, Workflow(state), PrimaryOnly()));
    }

    [Fact]
    public void EachOtherPreconditionHasItsOwnRefusal()
    {
        Assert.Equal(
            AuditRefusal.NotInspectionAndAudit,
            AuditPolicy.Refusal(CaseType.Inspection, Workflow(CaseLifecycleState.ReportSent), PrimaryOnly()));
        Assert.Equal(
            AuditRefusal.NotInspectionAndAudit,
            AuditPolicy.Refusal(CaseType.Audit, Workflow(CaseLifecycleState.ReportSent), PrimaryOnly()));
        Assert.Equal(
            AuditRefusal.CreatedInError,
            AuditPolicy.Refusal(
                CaseType.InspectionAndAudit, Workflow(CaseLifecycleState.CreatedInError), PrimaryOnly()));
        Assert.Equal(
            AuditRefusal.Archived,
            AuditPolicy.Refusal(
                CaseType.InspectionAndAudit,
                Workflow(CaseLifecycleState.ReportSent, archived: true),
                PrimaryOnly()));
        Assert.Equal(
            AuditRefusal.AuditAlreadyExists,
            AuditPolicy.Refusal(
                CaseType.InspectionAndAudit, Workflow(CaseLifecycleState.ReportPreparation), WithAudit()));
        Assert.Equal(
            AuditRefusal.NoAssignedEngineer,
            AuditPolicy.Refusal(
                CaseType.InspectionAndAudit,
                Workflow(CaseLifecycleState.ReportSent, assigned: false),
                PrimaryOnly()));
    }

    [Theory]
    [InlineData(AuditRefusal.NotInspectionAndAudit, "Only an Inspection + Audit case can have an audit created from it.")]
    [InlineData(AuditRefusal.CreatedInError, "A case recorded as created in error cannot have an audit created from it.")]
    [InlineData(AuditRefusal.Archived, "An archived case cannot have an audit created from it.")]
    [InlineData(AuditRefusal.AuditAlreadyExists, "This case already has its audit.")]
    [InlineData(AuditRefusal.Held, "A held case cannot have an audit created from it.")]
    [InlineData(AuditRefusal.Closed, "A closed case cannot have an audit created from it.")]
    [InlineData(AuditRefusal.NoAssignedEngineer, "Report preparation requires an assigned Engineer.")]
    public void EachRefusalReadsItsApprovedMessage(AuditRefusal refusal, string message)
    {
        Assert.Equal(message, AuditPolicy.Message(refusal));
        var exception = new AuditCreationException(CaseId, refusal);
        Assert.Equal(message, exception.Message);
        Assert.Equal(CaseId, exception.CaseId);
        Assert.Equal(refusal, exception.Refusal);
    }

    /// <summary>
    /// An Unassigned Case is refused its Audit and its report in one sentence
    /// (operator, 15 October 2026, #1213 ruling 3: an Engineer must take it first).
    /// </summary>
    [Fact]
    public void TheNoEngineerRefusalIsTheReportBlockersSentence()
    {
        Assert.Equal(
            CaseLifecycleRules.AssignedEngineerRequired,
            AuditPolicy.Message(AuditRefusal.NoAssignedEngineer));
        Assert.Equal(
            CaseLifecycleRules.AssignedEngineerRequired,
            Pegasus.Core.Reports.CaseReportReadiness.AssignedEngineerMissing.WhyOutstanding);
    }

    [Fact]
    public void TheHistoryLineNamesTheAuditReferenceAndTheActor()
    {
        Assert.Equal("Audit a.QDOS26214 created by A Mercer", AuditPolicy.HistoryLine("a.QDOS26214", "A Mercer"));
    }

    /// <summary>
    /// The Case is read once, by the store under its lock, which applies
    /// <see cref="AuditPolicy"/> and the Engineer's eligibility to it and answers
    /// a replay from its record: the use case reads nothing of its own
    /// (eff-07-l, register A3-06).
    /// </summary>
    [Fact]
    public async Task CreateAuditPassesTheTrimmedRequestToTheStoreAndPublishesTheFolderWork()
    {
        var store = new RecordingStore();
        var publisher = new RecordingPublisher();
        var sut = new CreateAudit(store, publisher);

        var result = await sut.ExecuteAsync(Request() with { OperationKey = "  create-audit-1  " }, default);

        var request = Assert.Single(store.Requests);
        Assert.Equal("create-audit-1", request.OperationKey);
        Assert.Equal("lease-token", request.EditLeaseToken);
        Assert.Equal("a.QDOS26214", result.AuditReference);
        Assert.False(result.IsReplay);
        Assert.Equal(CustodyWorkId, Assert.Single(publisher.Published));
    }

    [Fact]
    public async Task AReplayIsAnsweredByTheStoreWithoutPublishing()
    {
        var store = new RecordingStore { Replay = true };
        var publisher = new RecordingPublisher();
        var sut = new CreateAudit(store, publisher);

        var result = await sut.ExecuteAsync(Request(), default);

        Assert.True(result.IsReplay);
        Assert.Single(store.Requests);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task OnlyAnActorAllowedCaseworkMayCreateAnAudit()
    {
        var store = new RecordingStore();
        var sut = new CreateAudit(store, new RecordingPublisher());

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => sut.ExecuteAsync(
            Request() with { Actor = ActionActor.SystemWorker("worker") },
            default));
        Assert.Empty(store.Requests);
    }

    [Fact]
    public async Task AnOperationKeyAndAnEditLeaseAreRequired()
    {
        var sut = new CreateAudit(new RecordingStore(), new RecordingPublisher());

        await Assert.ThrowsAsync<ArgumentException>(() => sut.ExecuteAsync(Request() with { OperationKey = " " }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.ExecuteAsync(Request() with { EditLeaseToken = "" }, default));
    }

    /// <summary>The rule the store asks of the facts it read is the one the Case page asks of the Case.</summary>
    [Fact]
    public void TheFactsFormOfTheRuleKeepsTheOrderOfTheRecordForm()
    {
        Assert.Equal(
            AuditRefusal.CreatedInError,
            AuditPolicy.Refusal(CaseType.InspectionAndAudit, CaseLifecycleState.CreatedInError, true, true, false));
        Assert.Equal(
            AuditRefusal.Archived,
            AuditPolicy.Refusal(CaseType.InspectionAndAudit, CaseLifecycleState.ReportSent, true, true, false));
        Assert.Equal(
            AuditRefusal.AuditAlreadyExists,
            AuditPolicy.Refusal(CaseType.InspectionAndAudit, CaseLifecycleState.Held, false, true, false));
        Assert.Equal(
            AuditRefusal.Held,
            AuditPolicy.Refusal(CaseType.InspectionAndAudit, CaseLifecycleState.Held, false, false, false));
        Assert.Equal(
            AuditRefusal.NoAssignedEngineer,
            AuditPolicy.Refusal(CaseType.InspectionAndAudit, CaseLifecycleState.ReportSent, false, false, false));
        Assert.Null(
            AuditPolicy.Refusal(CaseType.InspectionAndAudit, CaseLifecycleState.ReportSent, false, false, true));
    }

    /// <summary>
    /// The one eligibility refusal Create audit, Start, Assign and Return to Engineer
    /// ask of the Engineer account the store read: both branches, in their exact words.
    /// </summary>
    [Theory]
    [InlineData(false, false, "The assigned staff account does not exist.")]
    [InlineData(true, false, "The assigned staff account is disabled.")]
    public void AnIneligibleEngineerIsRefusedInTheWordsReturnToEngineerAlwaysUsed(
        bool accountExists,
        bool isEnabled,
        string message)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => CaseEngineerEligibilityPolicy.Require(new CaseEngineerEligibility(accountExists, isEnabled)));

        Assert.Equal(message, exception.Message);
        CaseEngineerEligibilityPolicy.Require(new CaseEngineerEligibility(true, true));
    }

    private static CreateAuditRequest Request() => new(CaseId, 4, Staff, "create-audit-1", "lease-token");

    private static CaseWorkSet PrimaryOnly() => new(new CaseWork(CaseId, CaseId, CaseWorkKind.Primary, Now), null);

    private static CaseWorkSet WithAudit() => new(
        new CaseWork(CaseId, CaseId, CaseWorkKind.Primary, Now),
        new CaseWork(Guid.NewGuid(), CaseId, CaseWorkKind.Audit, Now.AddDays(1)));

    private static CaseWorkflowRecord Workflow(
        CaseLifecycleState state,
        bool sent = true,
        bool archived = false,
        bool assigned = true)
    {
        var identity = new CaseIdentity(CaseId, "QDOS", 2026, 214, "QDOS26214");
        return new CaseWorkflowRecord(
            CaseId,
            identity,
            state,
            assigned ? EngineerId : null,
            null,
            sent ? SentEvidence() : null,
            null,
            null,
            null,
            null,
            4)
        {
            Archive = archived ? new CaseArchive(Now, Staff, "Archived") : null
        };
    }

    private static ApprovedMailboxReportSentEvidence SentEvidence() => new(
        Guid.Parse("3e4f5a6b-7c8d-4e9f-a0b1-c2d3e4f5a6b7"),
        "reports@example.test",
        "sent-items",
        "item-1",
        "<message-1@example.test>",
        "conversation-1",
        "chain-1",
        "occurrence-1",
        new string('a', 64),
        new string('b', 64),
        Now.AddDays(-1),
        Now.AddDays(-1),
        ActionActor.SystemWorker("poller"),
        Now.AddDays(-1),
        ActionActor.SystemWorker("poller"));

    private sealed class RecordingStore : ICreateAuditStore
    {
        public List<CreateAuditRequest> Requests { get; } = [];

        public bool Replay { get; init; }

        public Task<CreateAuditOutcome> CreateAsync(CreateAuditRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new CreateAuditOutcome(
                new CreateAuditResult(
                    new CaseIdentity(CaseId, "QDOS", 2026, 214, "QDOS26214") with { AuditReference = "a.QDOS26214" },
                    Guid.NewGuid(),
                    "a.QDOS26214",
                    Replay),
                CustodyWorkId));
        }
    }

    private sealed class RecordingPublisher : ICommittedExternalWorkPublisher
    {
        public List<Guid> Published { get; } = [];

        public Task PublishAsync(Guid workItemId, CancellationToken cancellationToken)
        {
            Published.Add(workItemId);
            return Task.CompletedTask;
        }
    }
}
