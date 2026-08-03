using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Collection(LocalDbFixtureDefinition.Name)]
[Trait("Category", "SqlServer")]
public sealed class CaseMatchIntegrationTests
{
    private static readonly DateTimeOffset StartUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly FixtureInspectionDate = new(2031, 5, 20);

    [Fact]
    public async Task AcceptanceWritesTheCaseMatchIndexInTheSameTransaction()
    {
        await using var harness = await Harness.CreateAsync();

        var outcome = await harness.AcceptAsync("case-match-accept-1");

        var row = await harness.SingleIndexRowAsync(outcome.Identity.CaseId);
        Assert.Equal("QDOS", row.WorkProviderCode);
        Assert.Equal("46553/1", row.DurableClaimToken);
        Assert.Equal("AB12CDE", row.NormalizedVrm);
        Assert.Equal("EXAMPLE", row.NormalizedSurname);
        Assert.Equal("J", row.NormalizedFirstInitial);
        Assert.Equal(new DateOnly(2031, 4, 1), row.IncidentDate);
        Assert.Equal("qdos_case_match", row.MatchPolicyKey);
        Assert.Equal(1, row.MatchPolicyVersion);
    }

    [Fact]
    public async Task CandidateQueryFindsTheCaseByEachKeyAndCarriesLifecycleState()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("case-match-accept-2");
        var queries = new EfCaseMatchIndex(harness.Factory);

        foreach (var keys in new CaseMatchKeys[]
        {
            new("46553/1", null, null, null, null),
            new(null, "AB12CDE", null, null, null),
            new(null, null, "EXAMPLE", "J", null)
        })
        {
            var candidates = await queries.FindByAnyKeyAsync("QDOS", keys, CancellationToken.None);
            var candidate = Assert.Single(candidates);
            Assert.Equal(outcome.Identity.CaseId, candidate.CaseId);
            Assert.True(Enum.IsDefined(candidate.State));
            Assert.Null(candidate.ReplacementCaseId);
        }

        Assert.Empty(await queries.FindByAnyKeyAsync(
            "QDOS",
            new("99999/9", null, null, null, null),
            CancellationToken.None));
        Assert.Empty(await queries.FindByAnyKeyAsync(
            "PCH",
            new("46553/1", null, null, null, null),
            CancellationToken.None));
    }

    [Fact]
    public async Task StaffSaveUpdatesTheCaseMatchIndexThroughTheSameGrammar()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("case-match-accept-3");
        var caseId = outcome.Identity.CaseId;
        var projection = await harness.GetRequiredDataAsync(caseId);

        var lease = await harness.AcquireLeaseAsync(caseId, projection.Version, "case-match-lease-1");
        await harness.SaveCase.ExecuteAsync(
            new SaveCaseRequest(
                caseId,
                projection.Version,
                harness.StaffActor,
                "case-match-save-1",
                "Registration corrected from the client's V5C",
                lease.Token,
                new CaseEditableData(VehicleRegistration: "XY65 ZZZ")),
            CancellationToken.None);

        var row = await harness.SingleIndexRowAsync(caseId);
        Assert.Equal("XY65ZZZ", row.NormalizedVrm);
        Assert.Equal("46553/1", row.DurableClaimToken);
    }

    [Fact]
    public async Task AutomaticAssociationWritesOnceAndReplaysAsNoOp()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("case-match-accept-4");
        var caseId = outcome.Identity.CaseId;
        var chaserReceiptId = await harness.SeedAdditionalReceiptAsync("chaser-token-1");
        var store = new EfIntakeMutationStore(harness.Factory);
        var request = new AutomaticCaseAssociationRequest(
            chaserReceiptId,
            caseId,
            "qdos_case_match",
            1,
            "system-worker:intake-processing",
            "case-match-association:test-op-1",
            "Automatic association from the recorded case-match decision.");

        var first = await store.AssociateFromMatchAsync(request, StartUtc, CancellationToken.None);
        var replay = await store.AssociateFromMatchAsync(request, StartUtc, CancellationToken.None);
        var alreadyActive = await store.AssociateFromMatchAsync(
            request with { OperationKey = "case-match-association:test-op-2" },
            StartUtc,
            CancellationToken.None);

        Assert.Equal(AutomaticCaseAssociationOutcome.Associated, first);
        Assert.Equal(AutomaticCaseAssociationOutcome.AlreadyAssociated, replay);
        Assert.Equal(AutomaticCaseAssociationOutcome.AlreadyAssociated, alreadyActive);

        await using var context = await harness.Factory.CreateDbContextAsync();
        var association = Assert.Single(
            await context.IntakeManualAssociations
                .AsNoTracking()
                .Where(item => item.IntakeReceiptId == chaserReceiptId)
                .ToListAsync());
        Assert.True(association.IsActive);
        Assert.Equal(caseId, association.CaseId);
        Assert.Equal(nameof(ActorKind.SystemWorker), association.ActorKind);
        Assert.Equal("system-worker:intake-processing", association.ActorSubjectId);
        Assert.Equal("qdos_case_match", association.MatchPolicyKey);
        Assert.Equal(1, association.MatchPolicyVersion);
        Assert.Equal(1, await context.IntakeMutationHistory
            .CountAsync(item => item.IntakeReceiptId == chaserReceiptId
                && item.EventType == "intake_case_linked"));
    }

    [Fact]
    public async Task CaseMatchDecisionReloadsWithoutLosingAuditEvidence()
    {
        await using var database = await LocalDbTestDatabase.CreateAsync();

        var matchedCaseId = Guid.NewGuid();
        var decision = new CaseMatchEvaluationResult(
            CaseMatchOutcome.UniqueMatch,
            matchedCaseId,
            null,
            new("46553/1", "AB12CDE", "EXAMPLE", "J", new DateOnly(2026, 6, 18)),
            [
                new(
                    matchedCaseId,
                    ["claim-reference", "vehicle-registration"],
                    [])
            ],
            "Exactly one candidate case survived with no contradictory identity evidence.",
            "qdos_case_match",
            1);

        await using var scope = database.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IIntakeReceiptStore>();
        var stored = await store.StoreAsync(
            new(
                SourceFileName: "case-match-audit.eml",
                MediaType: "message/rfc822",
                SourceLength: 1,
                SourceHash: new string('D', 64),
                SourceIdentity: new(IntakeSourceChannel.Mailbox, "case-match-audit-token"),
                ReceivedAtUtc: StartUtc,
                ProcessedAtUtc: StartUtc,
                Actor: "system-worker:approved-inbox-poller",
                Decision: IntakeDecision.NeedsSorting,
                DecisionReason: "The accepted route did not contain a reviewable instruction.",
                Evidence: [],
                Fields: [],
                InstructionDraft: null,
                MissingFields: [],
                FailureCode: null,
                FailureReason: null,
                SourceReaderKey: "protocol_reader",
                SourceReaderVersion: "1",
                ExtractionPolicyKey: "protocol_policy",
                ExtractionPolicyVersion: 1,
                CaseMatchDecision: decision),
            CancellationToken.None);

        var reloaded = await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(stored.Id, CancellationToken.None);
        var audit = Assert.IsType<CaseMatchEvaluationResult>(reloaded?.CaseMatchDecision);
        Assert.Equal(CaseMatchOutcome.UniqueMatch, audit.Outcome);
        Assert.Equal(matchedCaseId, audit.MatchedCaseId);
        Assert.Null(audit.RedirectedFromCaseId);
        Assert.Equal("46553/1", audit.Keys.DurableClaimToken);
        Assert.Equal(new DateOnly(2026, 6, 18), audit.Keys.IncidentDate);
        var candidate = Assert.Single(audit.Candidates);
        Assert.Equal(["claim-reference", "vehicle-registration"], candidate.HitKeys);
        Assert.Equal("qdos_case_match", audit.PolicyKey);
        Assert.Equal(1, audit.PolicyVersion);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly LocalDbTestDatabase database;
        private readonly AcquireCaseEditLease acquireLease;
        private readonly AcceptIntake acceptIntake;
        private readonly EfCaseDataStore dataStore;

        private Harness(
            LocalDbTestDatabase database,
            PooledDbContextFactory<PegasusDbContext> factory,
            Guid receiptId,
            ActionActor staffActor,
            EfCaseDataStore dataStore,
            AcceptIntake acceptIntake,
            SaveCase saveCase,
            AcquireCaseEditLease acquireLease)
        {
            this.database = database;
            Factory = factory;
            ReceiptId = receiptId;
            StaffActor = staffActor;
            this.dataStore = dataStore;
            this.acceptIntake = acceptIntake;
            SaveCase = saveCase;
            this.acquireLease = acquireLease;
        }

        public PooledDbContextFactory<PegasusDbContext> Factory { get; }
        public Guid ReceiptId { get; }
        public ActionActor StaffActor { get; }
        public SaveCase SaveCase { get; }

        public static async Task<Harness> CreateAsync()
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            try
            {
                var options = new DbContextOptionsBuilder<PegasusDbContext>()
                    .UseSqlServer(database.ConnectionString)
                    .Options;
                var factory = new PooledDbContextFactory<PegasusDbContext>(options);
                var timeProvider = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(StartUtc);
                var receiptId = Guid.NewGuid();
                var staffActor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
                await SeedAsync(factory, receiptId);

                IProviderCaseMatchPolicy[] matchPolicies = [new QdosCaseMatchPolicy()];
                var acceptanceStore = new EfCaseAcceptanceStore(factory, timeProvider, matchPolicies);
                var dataStore = new EfCaseDataStore(factory, timeProvider, matchPolicies);
                var workflowStore = new EfCaseWorkflowStore(factory, timeProvider);
                return new(
                    database,
                    factory,
                    receiptId,
                    staffActor,
                    dataStore,
                    new AcceptIntake(
                        acceptanceStore,
                        new FixedConfiguration(),
                        new EfProviderInspectionModeStore(factory)),
                    new SaveCase(dataStore),
                    new AcquireCaseEditLease(workflowStore));
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public Task<CaseAcceptanceOutcome> AcceptAsync(string operationKey) =>
            acceptIntake.ExecuteAsync(
                new(
                    ReceiptId,
                    0,
                    StaffActor,
                    operationKey,
                    "Accepted case-match fixture case",
                    CaseType.Inspection,
                    "QDOS",
                    new(true, true, false, false),
                    AcceptedInspectionDeadline: FixtureInspectionDate),
                CancellationToken.None);

        public Task<CaseEditLease> AcquireLeaseAsync(
            Guid caseId,
            long version,
            string operationKey) => acquireLease.ExecuteAsync(
            new(caseId, version, StaffActor, operationKey),
            CancellationToken.None);

        public async Task<CaseDataProjection> GetRequiredDataAsync(Guid caseId) =>
            await dataStore.GetAsync(caseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The case-data fixture was not persisted.");

        public async Task<CaseMatchIndexEntity> SingleIndexRowAsync(Guid caseId)
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await context.CaseMatchIndex
                .AsNoTracking()
                .SingleAsync(item => item.CaseId == caseId);
        }

        public async Task<Guid> SeedAdditionalReceiptAsync(string externalToken)
        {
            var id = Guid.NewGuid();
            var sourceHash = new string('c', 64);
            var emptyEnvelope = """{"version":1,"data":[]}""";
            await using var context = await Factory.CreateDbContextAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({id}, {"chaser.eml"}, {"message/rfc822"}, {50L}, {sourceHash}, {"mailbox"}, {externalToken}, {StartUtc}, {StartUtc}, {"fixture-reader"}, {"1"}, {0L}, {"needs_sorting"}, {"Chaser fixture"}, {emptyEnvelope}, {emptyEnvelope}, {emptyEnvelope})");
            return id;
        }

        public async ValueTask DisposeAsync() => await database.DisposeAsync();

        private static async Task SeedAsync(
            IDbContextFactory<PegasusDbContext> factory,
            Guid receiptId)
        {
            await using var context = await factory.CreateDbContextAsync();
            var organizationId = Guid.NewGuid();
            var lineageId = Guid.NewGuid();
            var principalId = Guid.NewGuid();
            var sourceHash = new string('b', 64);
            var fieldsJson =
                """{"version":1,"data":[{"name":"Claimant name","suggestedValue":"Mrs Jane Example","candidates":[{"value":"Mrs Jane Example","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Claim number","suggestedValue":"MFI/AKH/46553/1","candidates":[{"value":"MFI/AKH/46553/1","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Vehicle registration","suggestedValue":"AB12 CDE","candidates":[{"value":"AB12 CDE","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Date of incident","suggestedValue":"2031-04-01","candidates":[{"value":"2031-04-01","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Inspection address","suggestedValue":"1 Test Street, London","candidates":[{"value":"1 Test Street, London","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Inspection date","suggestedValue":"2031-05-20","candidates":[{"value":"2031-05-20","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false}]}""";
            var emptyEnvelope = """{"version":1,"data":[]}""";

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {"QDOS case-match provider"}, {0L})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {StartUtc})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, InspectionMode, Version) VALUES ({principalId}, {organizationId}, {"QDOS"}, {lineageId}, {true}, {"image_based_assessment"}, {0L})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({receiptId}, {"qdos.eml"}, {"message/rfc822"}, {100L}, {sourceHash}, {"mailbox"}, {"case-match-item-1"}, {StartUtc}, {StartUtc}, {"fixture-reader"}, {"1"}, {"qdos_instruction"}, {1}, {0L}, {"draft_ready"}, {"Ready fixture"}, {emptyEnvelope}, {fieldsJson}, {emptyEnvelope})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO InstructionDrafts (IntakeReceiptId, SuggestedPrincipalCode, ClaimantName, ClaimNumber, VehicleRegistration, DateOfIncident, InspectionAddress, InspectionDate) VALUES ({receiptId}, {"QDOS"}, {"Mrs Jane Example"}, {"MFI/AKH/46553/1"}, {"AB12CDE"}, {new DateOnly(2031, 4, 1)}, {"1 Test Street, London"}, {FixtureInspectionDate})");
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
