using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class AssessmentPersistenceIntegrationTests
{
    private static readonly DateTimeOffset StartUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task AutomationSaveIsUnconfirmedAttributedAndParityLoggedWithAStaffSave()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("assessment-accept-1");
        var caseId = outcome.Identity.CaseId;

        // The Automation actor writes under the same lease and version
        // guards as a staff save; its values land unconfirmed.
        var automationLease = await harness.AcquireLeaseAsync(
            caseId,
            0,
            harness.AutomationActor,
            "assessment-lease-automation");
        var saved = await harness.SaveAssessment.ExecuteAsync(
            new(
                caseId,
                automationLease.Version,
                harness.AutomationActor,
                "mcp:assessment-save-1",
                "Automation recorded the assessment draft.",
                automationLease.Token,
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["vehicle.condition"] = "good",
                    ["assessment.outcome"] = "total_loss",
                    ["assessment.category"] = "S",
                    ["assessment.salvage_value"] = "1500.00",
                    ["assessment.values.retail"] = "12000",
                    ["assessment.values.trade"] = "10500",
                    ["assessment.values.engineer"] = "12000"
                },
                [
                    new("repair", null, "Repair nearside door", 3.5m, null, false, null, null,
                        "estimated", "judgement", "Visible panel damage"),
                    new("new_part", null, "Door skin", null, 220.40m, false, "P-1234", null,
                        "confirmed", "official", "Distorted beyond repair")
                ]),
            CancellationToken.None);

        Assert.Equal(1, saved.CaseVersion);
        Assert.All(saved.Fields, field =>
        {
            Assert.Equal(ActorKind.Automation, field.RecordedByKind);
            Assert.False(field.IsConfirmed);
        });
        Assert.Equal(2, saved.EstimateLines.Count);
        Assert.All(saved.EstimateLines, line => Assert.False(line.IsConfirmed));
        Assert.Contains(
            saved.Readiness,
            item => item.Requirement == "vehicle.condition awaits review"
                && item.Source.Contains("Automation", StringComparison.Ordinal));
        Assert.Contains(
            saved.Readiness,
            item => item.Requirement == "Estimate line 1 (repair) awaits review");

        // A staff Engineer re-saves one finding with the same value: the
        // value flips to confirmed, and both saves left exactly the same
        // shape of permanent evidence (logging parity, side by side). The
        // clock advances so the two history rows order deterministically.
        harness.Advance(TimeSpan.FromMinutes(1));
        var staffLease = await harness.AcquireLeaseAsync(
            caseId,
            saved.CaseVersion,
            harness.EngineerActor,
            "assessment-lease-staff");
        var confirmed = await harness.SaveAssessment.ExecuteAsync(
            new(
                caseId,
                staffLease.Version,
                harness.EngineerActor,
                "staff-assessment-save-1",
                "Engineer confirmed the recorded outcome.",
                staffLease.Token,
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["assessment.outcome"] = "total_loss"
                }),
            CancellationToken.None);
        var confirmedOutcome = confirmed.Field("assessment.outcome");
        Assert.NotNull(confirmedOutcome);
        Assert.True(confirmedOutcome!.IsConfirmed);
        Assert.Equal(ActorKind.Staff, confirmedOutcome.RecordedByKind);

        await using var context = await harness.Factory.CreateDbContextAsync();
        var history = await context.ActionHistory.AsNoTracking()
            .Where(item => item.EventKind == "case_assessment_saved")
            .OrderBy(item => item.OccurredAtUtc)
            .ToArrayAsync();
        Assert.Equal(2, history.Length);
        Assert.Equal(nameof(ActorKind.Automation), history[0].ActorKind);
        Assert.Equal("pegasus-automation", history[0].ActorSubjectId);
        Assert.Equal(nameof(ActorKind.Staff), history[1].ActorKind);
        Assert.All(history, entry =>
        {
            Assert.Equal("case", entry.AggregateType);
            Assert.Equal(caseId.ToString("D"), entry.AggregateId);
            Assert.Equal("Succeeded", entry.Outcome);
            Assert.False(string.IsNullOrWhiteSpace(entry.BeforeJson));
            Assert.False(string.IsNullOrWhiteSpace(entry.AfterJson));
            Assert.False(string.IsNullOrWhiteSpace(entry.Reason));
            Assert.Equal("case-assessment-edit/v1", entry.PolicyVersion);
        });
        Assert.Equal(2, await context.CaseWorkflowEvents.AsNoTracking()
            .CountAsync(item => item.CaseId == caseId
                && item.EventType == "case_assessment_saved"));
        Assert.Equal(2, await context.CaseHistory.AsNoTracking()
            .CountAsync(item => item.CaseId == caseId
                && item.EventType == "case_assessment_saved"));
    }

    [Fact]
    public async Task OperationKeyReplayReturnsTheOriginalResultAndConflictsOnDifferentMaterial()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("assessment-accept-2");
        var caseId = outcome.Identity.CaseId;
        var lease = await harness.AcquireLeaseAsync(
            caseId,
            0,
            harness.AutomationActor,
            "assessment-lease-replay");
        SaveAssessmentRequest Request(string value) => new(
            caseId,
            lease.Version,
            harness.AutomationActor,
            "mcp:assessment-replay",
            "Automation recorded the assessment draft.",
            lease.Token,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["vehicle.condition"] = value
            });

        var first = await harness.SaveAssessment.ExecuteAsync(
            Request("good"),
            CancellationToken.None);
        Assert.Equal(1, first.CaseVersion);

        var replay = await harness.SaveAssessment.ExecuteAsync(
            Request("good"),
            CancellationToken.None);
        Assert.Equal(1, replay.CaseVersion);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            Assert.Equal(1, await context.CaseWorkflowEvents.AsNoTracking()
                .CountAsync(item => item.CaseId == caseId
                    && item.EventType == "case_assessment_saved"));
        }

        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.SaveAssessment.ExecuteAsync(Request("poor"), CancellationToken.None));
    }

    [Fact]
    public async Task StaleVersionsAndMissingLeasesFailClosed()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("assessment-accept-3");
        var caseId = outcome.Identity.CaseId;

        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() =>
            harness.SaveAssessment.ExecuteAsync(
                new(
                    caseId,
                    0,
                    harness.AutomationActor,
                    "mcp:assessment-noleased",
                    "Automation recorded the assessment draft.",
                    "not-a-lease",
                    new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["vehicle.condition"] = "good"
                    }),
                CancellationToken.None));

        var lease = await harness.AcquireLeaseAsync(
            caseId,
            0,
            harness.AutomationActor,
            "assessment-lease-stale");
        await Assert.ThrowsAsync<CaseVersionConflictException>(() =>
            harness.SaveAssessment.ExecuteAsync(
                new(
                    caseId,
                    lease.Version + 5,
                    harness.AutomationActor,
                    "mcp:assessment-stale",
                    "Automation recorded the assessment draft.",
                    lease.Token,
                    new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["vehicle.condition"] = "good"
                    }),
                CancellationToken.None));
    }

    [Fact]
    public async Task AnUnknownWorkRequestBindingFailsClosed()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("assessment-accept-4");
        var caseId = outcome.Identity.CaseId;
        var lease = await harness.AcquireLeaseAsync(
            caseId,
            0,
            harness.AutomationActor,
            "assessment-lease-binding");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SaveAssessment.ExecuteAsync(
                new(
                    caseId,
                    lease.Version,
                    harness.AutomationActor,
                    "mcp:assessment-binding",
                    "Automation recorded the assessment draft.",
                    lease.Token,
                    new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["vehicle.condition"] = "good"
                    },
                    AiWorkRequestId: Guid.NewGuid()),
                CancellationToken.None));
    }

    [Fact]
    public async Task TheAiWorkRequestLifecyclePersistsWithCorrelatedHistory()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("assessment-accept-5");
        var caseId = outcome.Identity.CaseId;
        var staff = harness.EngineerActor;

        var created = await harness.WorkRequests.CreateAsync(
            new(
                caseId,
                outcome.Identity.Reference,
                0,
                staff,
                "send-op-1",
                "Work the assessment.",
                TimeSpan.FromHours(24)),
            CancellationToken.None);
        Assert.Equal(AiWorkRequestState.Created, created.State);

        // Creation replays idempotently on the same operation key and
        // conflicts on different material.
        var replay = await harness.WorkRequests.CreateAsync(
            new(
                caseId,
                outcome.Identity.Reference,
                0,
                staff,
                "send-op-1",
                "Work the assessment.",
                TimeSpan.FromHours(24)),
            CancellationToken.None);
        Assert.Equal(created.RequestId, replay.RequestId);
        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.WorkRequests.CreateAsync(
                new(
                    caseId,
                    outcome.Identity.Reference,
                    0,
                    staff,
                    "send-op-1",
                    "A different instruction.",
                    TimeSpan.FromHours(24)),
                CancellationToken.None));

        var handedOff = await harness.WorkRequests.TransitionAsync(
            new(created.RequestId, created.Version, AiWorkRequestState.HandedOff, staff, "t-1"),
            CancellationToken.None);
        Assert.Equal(AiWorkRequestState.HandedOff, handedOff.State);
        Assert.NotNull(handedOff.HandedOffAtUtc);

        var completed = await harness.WorkRequests.TransitionAsync(
            new(
                created.RequestId,
                handedOff.Version,
                AiWorkRequestState.Completed,
                staff,
                "t-2",
                ReplyStatus: "done",
                ReplyMessage: "Assessment recorded."),
            CancellationToken.None);
        Assert.Equal(AiWorkRequestState.Completed, completed.State);
        Assert.Equal("Assessment recorded.", completed.ReplyMessage);

        // Completed is terminal: reopening it is an illegal transition, and
        // an exact repeat of the terminal transition replays inertly.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.WorkRequests.TransitionAsync(
                new(
                    created.RequestId,
                    completed.Version,
                    AiWorkRequestState.HandedOff,
                    staff,
                    "t-3"),
                CancellationToken.None));
        var repeat = await harness.WorkRequests.TransitionAsync(
            new(
                created.RequestId,
                completed.Version,
                AiWorkRequestState.Completed,
                staff,
                "t-2"),
            CancellationToken.None);
        Assert.Equal(completed.Version, repeat.Version);

        await using var context = await harness.Factory.CreateDbContextAsync();
        var history = await context.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateType == "ai_work_request")
            .ToArrayAsync();
        Assert.Equal(3, history.Length);
        Assert.All(history, entry =>
            Assert.Equal(created.RequestId.ToString("D"), entry.CorrelationId));
        Assert.Contains(history, entry => entry.EventKind == "ai_work_request_created");
        Assert.Contains(history, entry => entry.EventKind == "ai_work_request_handedoff");
        Assert.Contains(history, entry => entry.EventKind == "ai_work_request_completed");
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly LocalDbTestDatabase database;
        private readonly AcquireCaseEditLease acquireLease;
        private readonly AcceptIntake acceptIntake;
        private readonly CaseDataCompletenessPersistenceTests.MutableTimeProvider timeProvider;

        private Harness(
            LocalDbTestDatabase database,
            PooledDbContextFactory<PegasusDbContext> factory,
            Guid receiptId,
            AcceptIntake acceptIntake,
            AcquireCaseEditLease acquireLease,
            SaveAssessment saveAssessment,
            EfAiWorkRequestStore workRequests,
            CaseDataCompletenessPersistenceTests.MutableTimeProvider timeProvider)
        {
            this.database = database;
            Factory = factory;
            ReceiptId = receiptId;
            this.acceptIntake = acceptIntake;
            this.acquireLease = acquireLease;
            SaveAssessment = saveAssessment;
            WorkRequests = workRequests;
            this.timeProvider = timeProvider;
        }

        public PooledDbContextFactory<PegasusDbContext> Factory { get; }
        public Guid ReceiptId { get; }
        public SaveAssessment SaveAssessment { get; }
        public EfAiWorkRequestStore WorkRequests { get; }
        public ActionActor AutomationActor { get; } = ActionActor.Automation("pegasus-automation");
        public ActionActor EngineerActor { get; } =
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

        public static async Task<Harness> CreateAsync()
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            try
            {
                var options = new DbContextOptionsBuilder<PegasusDbContext>()
                    .UseSqlServer(database.ConnectionString)
                    .Options;
                var factory = new PooledDbContextFactory<PegasusDbContext>(options);
                var timeProvider = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(
                    StartUtc);
                var receiptId = Guid.NewGuid();
                await SeedAsync(factory, receiptId);
                var acceptanceStore = new EfCaseAcceptanceStore(factory, timeProvider, []);
                var workflowStore = new EfCaseWorkflowStore(factory, timeProvider);
                return new(
                    database,
                    factory,
                    receiptId,
                    new AcceptIntake(
                        acceptanceStore,
                        new FixedConfiguration(),
                        new EfProviderInspectionModeStore(factory)),
                    new AcquireCaseEditLease(workflowStore),
                    new SaveAssessment(new EfCaseAssessmentStore(factory, timeProvider)),
                    new EfAiWorkRequestStore(factory, timeProvider),
                    timeProvider);
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public void Advance(TimeSpan interval) => timeProvider.Advance(interval);

        public Task<CaseAcceptanceOutcome> AcceptAsync(string operationKey) =>
            acceptIntake.ExecuteAsync(
                new(
                    ReceiptId,
                    0,
                    ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
                    operationKey,
                    "Accepted assessment fixture case",
                    CaseType.Inspection,
                    "QDOS",
                    new(true, true, false, false),
                    AcceptedInspectionDeadline: new DateOnly(2031, 5, 20)),
                CancellationToken.None);

        public Task<CaseEditLease> AcquireLeaseAsync(
            Guid caseId,
            long version,
            ActionActor actor,
            string operationKey) => acquireLease.ExecuteAsync(
            new(caseId, version, actor, operationKey),
            CancellationToken.None);

        public async ValueTask DisposeAsync() => await database.DisposeAsync();

        private static async Task SeedAsync(
            IDbContextFactory<PegasusDbContext> factory,
            Guid receiptId)
        {
            await using var context = await factory.CreateDbContextAsync();
            var organizationId = Guid.NewGuid();
            var lineageId = Guid.NewGuid();
            var principalId = Guid.NewGuid();
            var sourceHash = new string('d', 64);
            var fieldsJson =
                """{"version":1,"data":[{"name":"Claimant name","suggestedValue":"Mrs Jane Example","candidates":[{"value":"Mrs Jane Example","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Claim number","suggestedValue":"ABC/DEF/12345/1","candidates":[{"value":"ABC/DEF/12345/1","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Vehicle registration","suggestedValue":"AB12 CDE","candidates":[{"value":"AB12 CDE","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Date of incident","suggestedValue":"2031-04-01","candidates":[{"value":"2031-04-01","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Inspection address","suggestedValue":"1 Test Street, London","candidates":[{"value":"1 Test Street, London","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Inspection date","suggestedValue":"2031-05-20","candidates":[{"value":"2031-05-20","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false}]}""";
            var emptyEnvelope = """{"version":1,"data":[]}""";

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {"Assessment fixture provider"}, {0L})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {StartUtc})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, InspectionMode, Version) VALUES ({principalId}, {organizationId}, {"QDOS"}, {lineageId}, {true}, {"image_based_assessment"}, {0L})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({receiptId}, {"assessment.eml"}, {"message/rfc822"}, {100L}, {sourceHash}, {"mailbox"}, {"assessment-item-1"}, {StartUtc}, {StartUtc}, {"fixture-reader"}, {"1"}, {"qdos_instruction"}, {1}, {0L}, {"draft_ready"}, {"Ready fixture"}, {emptyEnvelope}, {fieldsJson}, {emptyEnvelope})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO InstructionDrafts (IntakeReceiptId, SuggestedPrincipalCode, ClaimantName, ClaimNumber, VehicleRegistration, DateOfIncident, InspectionAddress, InspectionDate) VALUES ({receiptId}, {"QDOS"}, {"Mrs Jane Example"}, {"ABC/DEF/12345/1"}, {"AB12CDE"}, {new DateOnly(2031, 4, 1)}, {"1 Test Street, London"}, {new DateOnly(2031, 5, 20)})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeMailRouteDecisions (IntakeReceiptId, Disposition, RouteOwnerCode, RouteKind, WorkProviderCode, PredicatesJson, Reason, PolicyKey, PolicyVersion, TransportIdentitiesJson, OriginalIdentitiesJson) VALUES ({receiptId}, {"accepted"}, {"QDOS"}, {"direct_work_provider"}, {"QDOS"}, {emptyEnvelope}, {"Accepted QDOS route"}, {"qdos_mail_route"}, {3}, {emptyEnvelope}, {emptyEnvelope})");
        }
    }

    private sealed class FixedConfiguration : ICaseWorkflowConfiguration
    {
        private static readonly CaseWorkflowConfiguration Configuration = new(
            true,
            true,
            true,
            true,
            "case-workflow",
            1);

        public Task<CaseWorkflowConfiguration> GetCurrentAsync(
            CancellationToken cancellationToken) => Task.FromResult(Configuration);
    }
}
