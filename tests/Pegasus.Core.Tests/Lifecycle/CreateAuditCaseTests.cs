using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Lifecycle;

public sealed class CreateAuditCaseTests
{
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly Guid EngineerId = Guid.NewGuid();
    private static readonly ActionActor Staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
    private static readonly DateTimeOffset Now = new(2026, 9, 13, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("total_loss", AuditAssessment.TotalLoss, "a.QDOS26214")]
    [InlineData("repairable", AuditAssessment.Repairable, "a.QDOS26214")]
    [InlineData("cash_in_lieu", AuditAssessment.Repairable, "a.QDOS26214")]
    [InlineData("contract_repair", AuditAssessment.Repairable, "a.QDOS26214")]
    public async Task TheAuditReferenceUsesOnePrefixAndKeepsTheRecordedOutcome(string outcome, AuditAssessment expected, string reference)
    {
        var store = new RecordingStore();
        var sut = Sut(store, outcome: outcome);

        var result = await sut.ExecuteAsync(Request(), default);

        var command = Assert.Single(store.Commands);
        Assert.Equal(expected, command.Assessment);
        Assert.Equal(reference, command.AuditReference);
        Assert.Equal(EngineerId, command.EngineerId);
        Assert.Equal("QDOS26214", command.Source.Reference);
        Assert.Equal(reference, result.AuditCase.Reference);
        Assert.Equal(AuditCasePolicy.Describe(expected), outcome == "total_loss" ? "total loss" : "repairable");
    }

    [Theory]
    [InlineData(AuditAssessment.Repairable, "repairable")]
    [InlineData(AuditAssessment.TotalLoss, "total_loss")]
    public void AuditAssessmentCodesRoundTrip(AuditAssessment assessment, string code)
    {
        Assert.Equal(code, AuditAssessmentCode.ToCode(assessment));
        Assert.Equal(assessment, AuditAssessmentCode.Parse(code));
    }

    [Fact]
    public void AuditAssessmentCodeRejectsAnUnknownPersistedValue()
    {
        Assert.Throws<InvalidDataException>(() => AuditAssessmentCode.Parse("TotalLoss"));
    }

    [Fact]
    public void AuditIdentityRejectsAnInvalidAssessment()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            AuditIdentity.Create("QDOS26214", (AuditAssessment)int.MaxValue));
    }

    [Fact]
    public async Task EachPreconditionFailsClosedWithItsOwnRefusal()
    {
        await Refuses(AuditCaseRefusal.NotInspectionAndAudit, Sut(new RecordingStore(), caseType: CaseType.Inspection));
        await Refuses(AuditCaseRefusal.AuditCaseAlreadyExists, Sut(new RecordingStore(), existingAudit: true));
        await Refuses(AuditCaseRefusal.SourceCreatedInError, Sut(new RecordingStore(), state: CaseLifecycleState.CreatedInError));
        await Refuses(AuditCaseRefusal.SourceArchived, Sut(new RecordingStore(), archived: true));
        await Refuses(AuditCaseRefusal.NoGeneratedReport, Sut(new RecordingStore(), reportGenerated: false));
        await Refuses(AuditCaseRefusal.NoRecordedOutcome, Sut(new RecordingStore(), outcome: null));
        await Refuses(AuditCaseRefusal.NoRecordedOutcome, Sut(new RecordingStore(), outcome: "pending"));
        await Refuses(AuditCaseRefusal.NotStaff, Sut(new RecordingStore()), Request() with { Actor = ActionActor.Automation("client") });
    }

    [Fact]
    public void TheHistoryLineReadsAsPlanned()
    {
        Assert.Equal(
            "Audit case a.QDOS26214 created by A Mercer from QDOS26214 — total loss",
            AuditCasePolicy.SourceHistoryLine("a.QDOS26214", "A Mercer", "QDOS26214", AuditAssessment.TotalLoss));
    }

    private static async Task Refuses(AuditCaseRefusal refusal, CreateAuditCase sut, CreateAuditCaseRequest? request = null)
    {
        var exception = await Assert.ThrowsAsync<AuditCaseCreationException>(() => sut.ExecuteAsync(request ?? Request(), default));
        Assert.Equal(refusal, exception.Refusal);
    }

    private static CreateAuditCaseRequest Request() => new(CaseId, 4, Staff, "create-audit-1", "lease-token");

    private static CreateAuditCase Sut(
        RecordingStore store,
        CaseType caseType = CaseType.InspectionAndAudit,
        CaseLifecycleState state = CaseLifecycleState.PostReport,
        bool archived = false,
        bool existingAudit = false,
        bool reportGenerated = true,
        string? outcome = "total_loss") => new(
            new FakeHeader(caseType, state, archived),
            new FakeAssessment(outcome),
            new FakeReports(reportGenerated),
            new FakeLinks(existingAudit),
            store);

    private sealed class FakeHeader(CaseType caseType, CaseLifecycleState state, bool archived) : IGetCaseHeader
    {
        public Task<CaseHeader?> ExecuteAsync(GetCaseHeaderQuery query, CancellationToken cancellationToken)
        {
            var identity = new CaseIdentity(CaseId, "QDOS", 2026, 214, "QDOS26214");
            var workflow = new CaseWorkflowRecord(CaseId, identity, state, EngineerId, null, null, null, null, null, null, 4)
            {
                Archive = archived ? new CaseArchive(Now, Staff, "Archived") : null
            };
            var summary = new CaseSearchItem(
                CaseId, "QDOS26214", null, caseType, "QDOS", state, EngineerId, "AB12CDE", "A Claimant", null,
                Now, null, "manual", Now);
            return Task.FromResult<CaseHeader?>(new CaseHeader(summary, workflow, null, 0, 0, 0));
        }
    }

    private sealed class FakeAssessment(string? outcome) : ICaseAssessmentStore
    {
        public Task<CaseAssessmentProjection?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseAssessmentProjection?>(new CaseAssessmentProjection(
                caseId, "QDOS26214", 4, CaseLifecycleState.PostReport, EngineerId,
                outcome is null
                    ? []
                    : [new AssessmentFieldValue(AssessmentVocabulary.Outcome, outcome, ActorKind.Staff, "engineer", Now, "engineer", Now)],
                [],
                new AssessmentCaseOwnedData(null, null, null, null, null, null, "tbc", null, null, null, null)));

        public Task<CaseAssessmentProjection> SaveAsync(SaveAssessmentRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeReports(bool generated) : ICaseReportGeneratedQueries
    {
        public Task<bool> HasGeneratedReportAsync(Guid caseId, CancellationToken cancellationToken) => Task.FromResult(generated);
    }

    private sealed class FakeLinks(bool existing) : ICaseAuditLinkQueries
    {
        public Task<CaseAuditLink?> GetAuditCaseAsync(Guid sourceCaseId, CancellationToken cancellationToken) =>
            Task.FromResult(existing ? new CaseAuditLink(Guid.NewGuid(), "a.QDOS26214") : null);

        public Task<CaseAuditLink?> GetOriginalCaseAsync(Guid auditCaseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseAuditLink?>(null);
    }

    private sealed class RecordingStore : ICreateAuditCaseStore
    {
        public List<CreateAuditCaseCommand> Commands { get; } = [];

        public Task<CreateAuditCaseResult> CreateAsync(CreateAuditCaseCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(new CreateAuditCaseResult(
                command.Source,
                new CaseIdentity(Guid.NewGuid(), "QDOS", 2026, 214, command.AuditReference, command.AuditReference),
                command.Assessment,
                false));
        }
    }
}
