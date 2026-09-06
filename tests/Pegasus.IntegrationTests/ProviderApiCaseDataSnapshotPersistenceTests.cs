using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.ProviderApi;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class ProviderApiCaseDataSnapshotPersistenceTests
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
    public async Task AcceptanceRecordsWorkProviderFromAuthenticatedCredentialBinding()
    {
        await using var harness = await Harness.CreateAsync();

        var outcome = await harness.AcceptIntake.ExecuteAsync(
            new(
                harness.ReceiptId,
                0,
                harness.WorkerActor,
                "accept-provider-api-1",
                "Accepted provider API instruction",
                CaseType.Inspection,
                "QDOS",
                new(true, true, false, false)),
            CancellationToken.None);
        var projection = await harness.DataStore.GetAsync(
            outcome.Identity.CaseId,
            CancellationToken.None);

        Assert.NotNull(projection);
        var workProvider = projection.Provider.WorkProviderCode.Current;
        Assert.NotNull(workProvider);
        Assert.Equal("QDOS", workProvider.Value);
        Assert.Equal(CaseDataValueKind.Fact, workProvider.Kind);
        Assert.Equal(CaseDataSourceKind.ProviderApi, workProvider.Source.Kind);
        Assert.Equal("authenticated credential binding", workProvider.Source.Label);
        Assert.Equal(ProviderInstructionPolicy.PolicyKey, workProvider.Source.PolicyKey);
        Assert.Equal(ProviderInstructionPolicy.PolicyVersion, workProvider.Source.PolicyVersion);
    }

    /// <summary>
    /// The staff create path takes whatever an operator keyed, and staff may
    /// key a different principal entirely to correct a provider that posted
    /// under the wrong account. Labelling that "authenticated credential
    /// binding" would export a provenance to the EVA archive that no credential
    /// supplied, so this path records no work provider fact at all — the same
    /// discipline <c>AddExtractedValue</c> keeps by mapping a person-keyed
    /// value to <c>StaffCorrection</c>.
    /// </summary>
    [Fact]
    public async Task AStaffCreatedCaseDoesNotClaimTheCredentialBindingAsItsWorkProvider()
    {
        await using var harness = await Harness.CreateAsync();

        var outcome = await harness.AcceptIntake.ExecuteAsync(
            new(
                harness.ReceiptId,
                0,
                harness.StaffActor,
                "accept-provider-api-staff-1",
                "Accepted provider API instruction",
                CaseType.Inspection,
                "QDOS",
                new(true, true, false, false)),
            CancellationToken.None);
        var projection = await harness.DataStore.GetAsync(
            outcome.Identity.CaseId,
            CancellationToken.None);

        Assert.NotNull(projection);
        Assert.Null(projection.Provider.WorkProviderCode.Current);
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
                        new EfProviderInspectionModeStore(factory),
                        new CommittedWorkPublisherDouble()),
                    new EfCaseDataStore(factory, TimeProvider.System));
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
                """{"version":1,"data":[{"name":"Claimant name","suggestedValue":"Jane Example","candidates":[{"value":"Jane Example","source":"provider_declaration","sourceLabel":"claimant.name"}],"isDefaulted":false,"hasConflict":false},{"name":"Claim number","suggestedValue":"QDOS-123","candidates":[{"value":"QDOS-123","source":"provider_declaration","sourceLabel":"claimNumber"}],"isDefaulted":false,"hasConflict":false},{"name":"Vehicle registration","suggestedValue":"AB12 CDE","candidates":[{"value":"AB12 CDE","source":"provider_declaration","sourceLabel":"vehicle.registration"}],"isDefaulted":false,"hasConflict":false}]}""";
            var emptyEnvelope = """{"version":1,"data":[]}""";
            var sourceChannel = EfIntakeReceiptStore.ToCode(IntakeSourceChannel.ProviderApi);

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({receiptId}, {ProviderInstructionPolicy.SourceFileName}, {ProviderInstructionPolicy.SourceMediaType}, {100L}, {sourceHash}, {sourceChannel}, {Guid.NewGuid().ToString("N")}, {StartUtc}, {StartUtc}, {ProviderInstructionPolicy.ReaderKey}, {ProviderInstructionPolicy.ReaderVersion}, {ProviderInstructionPolicy.PolicyKey}, {ProviderInstructionPolicy.PolicyVersion}, {0L}, {"case_created"}, {"Ready fixture"}, {emptyEnvelope}, {fieldsJson}, {emptyEnvelope})");
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
