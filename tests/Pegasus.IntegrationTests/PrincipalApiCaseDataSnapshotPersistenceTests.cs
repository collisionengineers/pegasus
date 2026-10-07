using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Core.PrincipalApi;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class PrincipalApiCaseDataSnapshotPersistenceTests
{
    private static readonly DateTimeOffset StartUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// Automatic allocation is the path whose <c>PrincipalCode</c> really is the
    /// credential binding's — <c>AttemptAutomaticAsync</c> derives it from
    /// <c>EstablishedPrincipalCode(receipt, binding)</c> and acts as
    /// <c>ActionActor.SystemWorker</c> (<c>IntakeAllocation.cs:259,283</c>).
    /// Only that path may claim the "authenticated credential binding" label.
    /// </summary>
    [Fact]
    public async Task AcceptanceRecordsPrincipalCodeFromAuthenticatedCredentialBinding()
    {
        await using var harness = await Harness.CreateAsync();

        var outcome = await harness.AcceptIntake.ExecuteAsync(
            new(
                harness.ReceiptId,
                0,
                harness.WorkerActor,
                "accept-principal-api-1",
                CaseType.Inspection,
                "QDOS",
                new(true, true)),
            CancellationToken.None);
        var projection = await harness.DataStore.GetAsync(
            outcome.Identity.CaseId,
            CaseWorkSelector.Current,
            CancellationToken.None);

        Assert.NotNull(projection);
        var workPrincipal = projection.Principal.PrincipalCode.Current;
        Assert.NotNull(workPrincipal);
        Assert.Equal("QDOS", workPrincipal.Value);
        Assert.Equal(CaseDataValueKind.Fact, workPrincipal.Kind);
        Assert.Equal(CaseDataSourceKind.PrincipalApi, workPrincipal.Source.Kind);
        Assert.Equal("authenticated credential binding", workPrincipal.Source.Label);
        Assert.Equal(PrincipalInstructionPolicy.PolicyKey, workPrincipal.Source.PolicyKey);
        Assert.Equal(PrincipalInstructionPolicy.PolicyVersion, workPrincipal.Source.PolicyVersion);
    }

    /// <summary>
    /// The staff create path takes whatever an operator keyed, and staff may
    /// key a different principal entirely to correct a provider that posted
    /// under the wrong account. Labelling that "authenticated credential
    /// binding" would export a provenance to the EVA archive that no credential
    /// supplied — the same discipline <c>AddExtractedValue</c> keeps by mapping
    /// a person-keyed value to <c>StaffCorrection</c>. The case still carries
    /// the Principal the operator allocated it to, because without it the EVA
    /// export sends an empty Work Provider and no case-match index row exists;
    /// it is recorded as the operator's own confirmation at acceptance.
    /// </summary>
    [Fact]
    public async Task AStaffCreatedCaseRecordsItsAllocatedPrincipalNotTheCredentialBinding()
    {
        await using var harness = await Harness.CreateAsync();

        var outcome = await harness.AcceptIntake.ExecuteAsync(
            new(
                harness.ReceiptId,
                0,
                harness.StaffActor,
                "accept-principal-api-staff-1",
                CaseType.Inspection,
                "QDOS",
                new(true, true)),
            CancellationToken.None);
        var projection = await harness.DataStore.GetAsync(
            outcome.Identity.CaseId,
            CaseWorkSelector.Current,
            CancellationToken.None);

        Assert.NotNull(projection);
        var workPrincipal = projection.Principal.PrincipalCode.Current;
        Assert.NotNull(workPrincipal);
        Assert.Equal("QDOS", workPrincipal.Value);
        Assert.Equal(CaseDataValueKind.Confirmed, workPrincipal.Kind);
        Assert.Equal(CaseDataSourceKind.CaseAcceptance, workPrincipal.Source.Kind);
        Assert.Equal("staff-accepted principal allocation", workPrincipal.Source.Label);
        Assert.Equal(harness.StaffActor.SubjectId, workPrincipal.ConfirmedByActor);
        Assert.Null(projection.Principal.PrincipalCode.Fact);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly LocalDbTestDatabase database;

        private Harness(
            LocalDbTestDatabase database,
            Guid receiptId,
            ActionActor staffActor,
            AcceptIntake acceptIntake,
            EfCaseDataStore dataStore)
        {
            this.database = database;
            ReceiptId = receiptId;
            StaffActor = staffActor;
            AcceptIntake = acceptIntake;
            DataStore = dataStore;
        }

        public Guid ReceiptId { get; }
        public ActionActor StaffActor { get; }

        /// <summary>The actor automatic allocation uses.</summary>
        public ActionActor WorkerActor { get; } =
            ActionActor.SystemWorker("intake-processing");
        public AcceptIntake AcceptIntake { get; }
        public EfCaseDataStore DataStore { get; }

        public static async Task<Harness> CreateAsync()
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            try
            {
                var options = new DbContextOptionsBuilder<PegasusDbContext>()
                    .UseSqlServer(database.ConnectionString)
                    .Options;
                var factory = new PooledDbContextFactory<PegasusDbContext>(options);
                var receiptId = Guid.NewGuid();
                var staffActor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
                await SeedAsync(factory, receiptId);

                var acceptanceStore = new EfCaseAcceptanceStore(factory, TimeProvider.System);
                return new(
                    database,
                    receiptId,
                    staffActor,
                    new AcceptIntake(
                        acceptanceStore,
                        new FixedConfiguration(),
                        new EfPrincipalInspectionModeStore(factory),
                        new DiscardingCommittedWorkPublisher(),
                        new TriageCasePairing(new EfTriageStore(factory,
                            [new PrincipalCaseMatchPolicy(new QdosInstructionExtractionPolicy())], TimeProvider.System))),
                    new EfCaseDataStore(factory));
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync() => await database.DisposeAsync();

        private static async Task SeedAsync(
            IDbContextFactory<PegasusDbContext> factory,
            Guid receiptId)
        {
            await using var context = await factory.CreateDbContextAsync();
            var principal = await SeededPrincipals.QdosAsync(context);
            var organizationId = principal.OrganizationId;
            var lineageId = principal.SequenceLineageId;
            var principalId = principal.Id;
            var sourceHash = new string('b', 64);
            var fieldsJson =
                """{"version":1,"data":[{"name":"Claimant name","suggestedValue":"Jane Example","candidates":[{"value":"Jane Example","source":"principal_declaration","sourceLabel":"claimant.name"}],"isDefaulted":false,"hasConflict":false},{"name":"Claim number","suggestedValue":"QDOS-123","candidates":[{"value":"QDOS-123","source":"principal_declaration","sourceLabel":"claimNumber"}],"isDefaulted":false,"hasConflict":false},{"name":"Vehicle registration","suggestedValue":"AB12 CDE","candidates":[{"value":"AB12 CDE","source":"principal_declaration","sourceLabel":"vehicle.registration"}],"isDefaulted":false,"hasConflict":false}]}""";
            var emptyEnvelope = """{"version":1,"data":[]}""";
            var sourceChannel = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.PrincipalApi);

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({receiptId}, {PrincipalInstructionPolicy.SourceFileName}, {PrincipalInstructionPolicy.SourceMediaType}, {100L}, {sourceHash}, {sourceChannel}, {Guid.NewGuid().ToString("N")}, {StartUtc}, {StartUtc}, {PrincipalInstructionPolicy.ReaderKey}, {PrincipalInstructionPolicy.ReaderVersion}, {PrincipalInstructionPolicy.PolicyKey}, {PrincipalInstructionPolicy.PolicyVersion}, {0L}, {"case_created"}, {"Ready fixture"}, {emptyEnvelope}, {fieldsJson}, {emptyEnvelope})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO InstructionDrafts (IntakeReceiptId, SuggestedPrincipalCode, ClaimantName, ClaimNumber, VehicleRegistration) VALUES ({receiptId}, {"QDOS"}, {"Jane Example"}, {"QDOS-123"}, {"AB12CDE"})");
        }
    }

    private sealed class FixedConfiguration : ICaseWorkflowConfiguration
    {
        private static readonly CaseWorkflowConfiguration Configuration = new(
            "case-workflow",
            1);

        public Task<CaseWorkflowConfiguration> GetCurrentAsync(
            CancellationToken cancellationToken) => Task.FromResult(Configuration);
    }
}
