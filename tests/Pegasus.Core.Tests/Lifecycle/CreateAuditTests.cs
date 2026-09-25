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

    [Theory]
    [InlineData(CaseLifecycleState.PostReport)]
    [InlineData(CaseLifecycleState.PostReportComplete)]
    [InlineData(CaseLifecycleState.Query)]
    public void CreateAuditIsAllowedOnceTheReportIsSent(CaseLifecycleState state)
    {
        Assert.Null(AuditPolicy.Refusal(CaseType.InspectionAndAudit, Workflow(state), PrimaryOnly()));
        Assert.Null(AuditPolicy.Refusal(CaseType.InspectionAndAudit, Workflow(state), works: null));
    }

    [Theory]
    [InlineData(CaseLifecycleState.Held)]
    [InlineData(CaseLifecycleState.ReportPreparation)]
    [InlineData(CaseLifecycleState.Review)]
    [InlineData(CaseLifecycleState.NotReady)]
    [InlineData(CaseLifecycleState.ProviderCancelled)]
    [InlineData(CaseLifecycleState.CollisionEngineersRejected)]
    [InlineData(CaseLifecycleState.SourceEmailUnlinked)]
    public void CreateAuditIsRefusedBeforeTheReportIsSentOrAfterAClosedDisposition(CaseLifecycleState state)
    {
        Assert.Equal(
            AuditRefusal.ReportNotSent,
            AuditPolicy.Refusal(CaseType.InspectionAndAudit, Workflow(state), PrimaryOnly()));
    }

    [Fact]
    public void APostReportCaseWithoutSentEvidenceHasNotSentItsReport()
    {
        Assert.Equal(
            AuditRefusal.ReportNotSent,
            AuditPolicy.Refusal(
                CaseType.InspectionAndAudit,
                Workflow(CaseLifecycleState.PostReport, sent: false),
                PrimaryOnly()));
    }

    [Fact]
    public void EachOtherPreconditionHasItsOwnRefusal()
    {
        Assert.Equal(
            AuditRefusal.NotInspectionAndAudit,
            AuditPolicy.Refusal(CaseType.Inspection, Workflow(CaseLifecycleState.PostReport), PrimaryOnly()));
        Assert.Equal(
            AuditRefusal.NotInspectionAndAudit,
            AuditPolicy.Refusal(CaseType.Audit, Workflow(CaseLifecycleState.PostReport), PrimaryOnly()));
        Assert.Equal(
            AuditRefusal.CreatedInError,
            AuditPolicy.Refusal(
                CaseType.InspectionAndAudit, Workflow(CaseLifecycleState.CreatedInError), PrimaryOnly()));
        Assert.Equal(
            AuditRefusal.Archived,
            AuditPolicy.Refusal(
                CaseType.InspectionAndAudit,
                Workflow(CaseLifecycleState.PostReportComplete, archived: true),
                PrimaryOnly()));
        Assert.Equal(
            AuditRefusal.AuditAlreadyExists,
            AuditPolicy.Refusal(
                CaseType.InspectionAndAudit, Workflow(CaseLifecycleState.ReportPreparation), WithAudit()));
        Assert.Equal(
            AuditRefusal.NoAssignedEngineer,
            AuditPolicy.Refusal(
                CaseType.InspectionAndAudit,
                Workflow(CaseLifecycleState.PostReport, assigned: false),
                PrimaryOnly()));
    }

    [Theory]
    [InlineData(AuditRefusal.NotInspectionAndAudit, "Only an Inspection + Audit case can have an audit created from it.")]
    [InlineData(AuditRefusal.CreatedInError, "A case recorded as created in error cannot have an audit created from it.")]
    [InlineData(AuditRefusal.Archived, "An archived case cannot have an audit created from it.")]
    [InlineData(AuditRefusal.AuditAlreadyExists, "This case already has its audit.")]
    [InlineData(AuditRefusal.ReportNotSent, "Create audit is available once the report is sent.")]
    [InlineData(AuditRefusal.NoAssignedEngineer, "Report preparation requires an assigned Engineer.")]
    public void EachRefusalReadsItsApprovedMessage(AuditRefusal refusal, string message)
    {
        Assert.Equal(message, AuditPolicy.Message(refusal));
        var exception = new AuditCreationException(CaseId, refusal);
        Assert.Equal(message, exception.Message);
        Assert.Equal(CaseId, exception.CaseId);
        Assert.Equal(refusal, exception.Refusal);
    }

    [Fact]
    public void TheHistoryLineNamesTheAuditReferenceAndTheActor()
    {
        Assert.Equal("Audit a.QDOS26214 created by A Mercer", AuditPolicy.HistoryLine("a.QDOS26214", "A Mercer"));
    }

    [Fact]
    public async Task CreateAuditCarriesTheAuditReferenceAndEngineerAndPublishesTheFolderWork()
    {
        var store = new RecordingStore();
        var publisher = new RecordingPublisher();
        var sut = Sut(Workflow(CaseLifecycleState.PostReport), store, publisher);

        var result = await sut.ExecuteAsync(Request() with { OperationKey = "  create-audit-1  " }, default);

        var command = Assert.Single(store.Commands);
        Assert.Equal("a.QDOS26214", command.AuditReference);
        Assert.Equal("QDOS26214", command.Case.Reference);
        Assert.Equal(EngineerId, command.EngineerId);
        Assert.Equal("create-audit-1", command.Request.OperationKey);
        Assert.Equal("a.QDOS26214", result.AuditReference);
        Assert.False(result.IsReplay);
        Assert.Equal(CustodyWorkId, Assert.Single(publisher.Published));
    }

    [Fact]
    public async Task ARefusalNamesTheCaseAndReachesNoStore()
    {
        var store = new RecordingStore();
        var sut = Sut(Workflow(CaseLifecycleState.Held), store, new RecordingPublisher());

        var exception = await Assert.ThrowsAsync<AuditCreationException>(() => sut.ExecuteAsync(Request(), default));

        Assert.Equal(AuditRefusal.ReportNotSent, exception.Refusal);
        Assert.Equal(CaseId, exception.CaseId);
        Assert.Empty(store.Commands);
    }

    [Fact]
    public async Task AnExistingAuditIsRefused()
    {
        var store = new RecordingStore();
        var sut = Sut(Workflow(CaseLifecycleState.ReportPreparation), store, new RecordingPublisher(), WithAudit());

        var exception = await Assert.ThrowsAsync<AuditCreationException>(() => sut.ExecuteAsync(Request(), default));

        Assert.Equal(AuditRefusal.AuditAlreadyExists, exception.Refusal);
        Assert.Empty(store.Commands);
    }

    [Fact]
    public async Task NoAssignedEngineerIsRefused()
    {
        var store = new RecordingStore();
        var sut = Sut(Workflow(CaseLifecycleState.PostReport, assigned: false), store, new RecordingPublisher());

        var exception = await Assert.ThrowsAsync<AuditCreationException>(() => sut.ExecuteAsync(Request(), default));

        Assert.Equal(AuditRefusal.NoAssignedEngineer, exception.Refusal);
        Assert.Empty(store.Commands);
    }

    [Theory]
    [InlineData(false, true, "The assigned staff account does not exist.")]
    [InlineData(true, false, "The assigned staff account is disabled.")]
    public async Task AnIneligibleEngineerIsRefusedAsReturnToEngineerRefusesThem(
        bool accountExists,
        bool isEnabled,
        string message)
    {
        var store = new RecordingStore();
        var sut = Sut(
            Workflow(CaseLifecycleState.PostReportComplete),
            store,
            new RecordingPublisher(),
            eligibility: new CaseEngineerEligibility(accountExists, isEnabled));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ExecuteAsync(Request(), default));

        Assert.Equal(message, exception.Message);
        Assert.Empty(store.Commands);
    }

    [Fact]
    public async Task AReplayIsAnsweredByTheStoreWithoutAskingTheRuleAgainOrPublishing()
    {
        var store = new RecordingStore { Replay = true };
        var publisher = new RecordingPublisher();
        var sut = Sut(
            Workflow(CaseLifecycleState.ReportPreparation),
            store,
            publisher,
            WithAudit(),
            hasOperation: true);

        var result = await sut.ExecuteAsync(Request(), default);

        Assert.True(result.IsReplay);
        Assert.Single(store.Commands);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task OnlyAnActorAllowedCaseworkMayCreateAnAudit()
    {
        var store = new RecordingStore();
        var sut = Sut(Workflow(CaseLifecycleState.PostReport), store, new RecordingPublisher());

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => sut.ExecuteAsync(
            Request() with { Actor = ActionActor.SystemWorker("worker") },
            default));
        Assert.Empty(store.Commands);
    }

    [Fact]
    public async Task AnOperationKeyAndAnEditLeaseAreRequired()
    {
        var sut = Sut(Workflow(CaseLifecycleState.PostReport), new RecordingStore(), new RecordingPublisher());

        await Assert.ThrowsAsync<ArgumentException>(() => sut.ExecuteAsync(Request() with { OperationKey = " " }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => sut.ExecuteAsync(Request() with { EditLeaseToken = "" }, default));
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

    private static CreateAudit Sut(
        CaseWorkflowRecord workflow,
        RecordingStore store,
        RecordingPublisher publisher,
        CaseWorkSet? works = null,
        bool hasOperation = false,
        CaseEngineerEligibility? eligibility = null) => new(
            new FakeHeader(workflow, works ?? PrimaryOnly()),
            new FakeWorkflowQueries(workflow, hasOperation),
            new FakeEligibility(eligibility ?? new CaseEngineerEligibility(true, true)),
            store,
            publisher);

    private sealed class FakeHeader(CaseWorkflowRecord workflow, CaseWorkSet works) : IGetCaseHeader
    {
        public Task<CaseHeader?> ExecuteAsync(GetCaseHeaderQuery query, CancellationToken cancellationToken)
        {
            var summary = new CaseSearchItem(
                CaseId, "QDOS26214", null, CaseType.InspectionAndAudit, "QDOS", workflow.State, workflow.AssignedEngineerId,
                "AB12CDE", "A Claimant", null, Now, "manual", Now);
            return Task.FromResult<CaseHeader?>(new CaseHeader(summary, workflow, null, 0, 0, 0, works));
        }
    }

    private sealed class FakeWorkflowQueries(CaseWorkflowRecord workflow, bool hasOperation) : ICaseWorkflowQueries
    {
        public Task<CaseWorkflowRecord?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseWorkflowRecord?>(workflow);

        public Task<bool> HasOperationAsync(Guid caseId, string operationKey, CancellationToken cancellationToken) =>
            Task.FromResult(hasOperation);
    }

    private sealed class FakeEligibility(CaseEngineerEligibility eligibility) : ICaseEngineerEligibility
    {
        public Task<CaseEngineerEligibility> GetAsync(Guid staffId, CancellationToken cancellationToken) =>
            Task.FromResult(eligibility);
    }

    private sealed class RecordingStore : ICreateAuditStore
    {
        public List<CreateAuditCommand> Commands { get; } = [];

        public bool Replay { get; init; }

        public Task<CreateAuditOutcome> CreateAsync(CreateAuditCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(new CreateAuditOutcome(
                new CreateAuditResult(
                    command.Case with { AuditReference = command.AuditReference },
                    Guid.NewGuid(),
                    command.AuditReference,
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
