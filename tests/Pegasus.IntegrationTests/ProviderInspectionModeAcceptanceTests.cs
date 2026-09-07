using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class ProviderInspectionModeAcceptanceTests
{
    private static readonly DateTimeOffset StartUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly FixtureInspectionDate = new(2031, 5, 20);

    [Fact]
    public async Task ImageBasedPrincipalAutofillsExactValueWithProviderSettingProvenance()
    {
        await using var harness = await Harness.CreateAsync();

        var outcome = await harness.AcceptAsync("accept-image-based-1");
        var projection = await harness.GetRequiredDataAsync(outcome.Identity.CaseId);

        Assert.Equal(
            Ext18InspectionAddressPolicy.ImageBasedAssessment,
            projection.Inspection.Address.Confirmed?.Value);
        Assert.Equal(
            CaseInspectionMode.ImageBasedAssessment,
            projection.Inspection.Mode.Confirmed?.Value);
        Assert.Equal(
            CaseDataSourceKind.ProviderSetting,
            projection.Inspection.Address.Confirmed?.Source.Kind);
        Assert.Equal(
            ProviderInspectionModePolicy.PolicyKey,
            projection.Inspection.Address.Confirmed?.Source.PolicyKey);
        Assert.Equal(
            ProviderInspectionModePolicy.PolicyVersion,
            projection.Inspection.Address.Confirmed?.Source.PolicyVersion);
        Assert.Equal(
            harness.StaffActor.SubjectId,
            projection.Inspection.Address.Confirmed?.ConfirmedByActor);
        Assert.Equal(
            CaseDataSourceKind.ProviderSetting,
            projection.Inspection.Mode.Confirmed?.Source.Kind);

        Assert.Equal(
            "1 Test Street, London",
            projection.Inspection.Address.Fact?.Value);

        Assert.Equal(
            1,
            await harness.CaseHistoryCountAsync(
                outcome.Identity.CaseId,
                "provider_inspection_mode_applied"));
    }

    [Fact]
    public async Task AutofillWinsOverIntakeAddressResolution()
    {
        await using var harness = await Harness.CreateAsync();

        var address = await harness.AddressStore.GetAsync(
            harness.ReceiptId,
            CancellationToken.None);
        var suggestion = address?.Evaluation.Suggestion
            ?? throw new InvalidOperationException("The address fixture did not produce a suggestion.");
        var resolved = await harness.AddressStore.ResolveAsync(
            new(
                harness.ReceiptId,
                address!.ReceiptVersion,
                suggestion.Fingerprint,
                InspectionAddressStaffDecision.AcceptSuggestion,
                null,
                harness.StaffActor,
                Guid.NewGuid(),
                "resolved-before-image-based-acceptance"),
            CancellationToken.None);

        var outcome = await harness.AcceptAsync(
            "accept-image-based-2",
            expectedVersion: resolved.ReceiptVersion);
        var projection = await harness.GetRequiredDataAsync(outcome.Identity.CaseId);

        Assert.Equal(
            Ext18InspectionAddressPolicy.ImageBasedAssessment,
            projection.Inspection.Address.Confirmed?.Value);
        Assert.Equal(
            CaseDataSourceKind.ProviderSetting,
            projection.Inspection.Address.Confirmed?.Source.Kind);
        Assert.Equal(
            CaseInspectionMode.ImageBasedAssessment,
            projection.Inspection.Mode.Confirmed?.Value);
    }

    [Fact]
    public async Task StaffOverrideToPhysicalAndBackUsesReasonedSaveCase()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("accept-image-based-3");
        var caseId = outcome.Identity.CaseId;
        var projection = await harness.GetRequiredDataAsync(caseId);

        var lease = await harness.AcquireLeaseAsync(caseId, projection.Version, "lease-override-1");
        var overridden = await harness.SaveCase.ExecuteAsync(
            new SaveCaseRequest(
                caseId,
                projection.Version,
                harness.StaffActor,
                "override-to-physical-1",
                "Client confirmed the vehicle is held at the repairer",
                lease.Token,
                new CaseEditableData(
                    InspectionAddress: "5 Repairer Way, Leeds",
                    InspectionMode: CaseInspectionMode.PhysicalAddress)),
            CancellationToken.None);
        var afterOverride = await harness.GetRequiredDataAsync(caseId);

        Assert.Equal("5 Repairer Way, Leeds", afterOverride.Inspection.Address.Confirmed?.Value);
        Assert.Equal(
            CaseInspectionMode.PhysicalAddress,
            afterOverride.Inspection.Mode.Confirmed?.Value);
        Assert.Equal(
            CaseDataSourceKind.StaffCorrection,
            afterOverride.Inspection.Address.Confirmed?.Source.Kind);

        var secondLease = await harness.AcquireLeaseAsync(
            caseId,
            overridden.Version,
            "lease-override-2");
        await harness.SaveCase.ExecuteAsync(
            new SaveCaseRequest(
                caseId,
                overridden.Version,
                harness.StaffActor,
                "override-to-image-based-1",
                "Provider works image-based; restoring the provider default",
                secondLease.Token,
                new CaseEditableData(
                    InspectionAddress: Ext18InspectionAddressPolicy.ImageBasedAssessment,
                    InspectionMode: CaseInspectionMode.ImageBasedAssessment)),
            CancellationToken.None);
        var restored = await harness.GetRequiredDataAsync(caseId);

        Assert.Equal(
            Ext18InspectionAddressPolicy.ImageBasedAssessment,
            restored.Inspection.Address.Confirmed?.Value);
        Assert.Equal(
            CaseInspectionMode.ImageBasedAssessment,
            restored.Inspection.Mode.Confirmed?.Value);
    }

    [Fact]
    public async Task ReplayAfterSettingFlipConflictsInsteadOfDeduplicating()
    {
        await using var harness = await Harness.CreateAsync();
        var request = harness.CreateAcceptRequest("accept-image-based-4");

        var first = await harness.AcceptIntake.ExecuteAsync(request, CancellationToken.None);
        Assert.False(first.IsDuplicate);

        var replayBeforeFlip = await harness.AcceptIntake.ExecuteAsync(
            request,
            CancellationToken.None);
        Assert.True(replayBeforeFlip.IsDuplicate);

        await harness.SetPrincipalModeAsync("physical_address");
        await Assert.ThrowsAsync<CaseAcceptanceOperationConflictException>(
            () => harness.AcceptIntake.ExecuteAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task MidFlightSettingFlipIsRejectedInsideTheAcceptanceTransaction()
    {
        await using var harness = await Harness.CreateAsync();

        var staleModeAcceptance = new CaseAcceptanceRequest(
            harness.ReceiptId,
            0,
            harness.StaffActor,
            "accept-image-based-5",
            "Accepted image-based provider case",
            CaseType.Inspection,
            "QDOS",
            new(true, true, false, false),
            CaseCompletenessPolicy.Evaluate(
                new(true, true, false, false),
                await new FixedConfiguration().GetCurrentAsync(CancellationToken.None)),
            CaseInspectionMode.PhysicalAddress,
            AcceptedInspectionDeadline: FixtureInspectionDate);

        var exception = await Assert.ThrowsAsync<IntakeVersionConflictException>(
            () => harness.AcceptanceStore.AcceptAsync(
                staleModeAcceptance,
                CancellationToken.None));
        Assert.Contains("intake or case changed", exception.Message);
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly LocalDbTestDatabase database;
        private readonly PooledDbContextFactory<PegasusDbContext> factory;
        private readonly AcquireCaseEditLease acquireLease;

        private Harness(
            LocalDbTestDatabase database,
            PooledDbContextFactory<PegasusDbContext> factory,
            Guid receiptId,
            ActionActor staffActor,
            InspectionAddressResolutionStore addressStore,
            EfCaseDataStore dataStore,
            EfCaseAcceptanceStore acceptanceStore,
            AcceptIntake acceptIntake,
            SaveCase saveCase,
            AcquireCaseEditLease acquireLease)
        {
            this.database = database;
            this.factory = factory;
            ReceiptId = receiptId;
            StaffActor = staffActor;
            AddressStore = addressStore;
            DataStore = dataStore;
            AcceptanceStore = acceptanceStore;
            AcceptIntake = acceptIntake;
            SaveCase = saveCase;
            this.acquireLease = acquireLease;
        }

        public Guid ReceiptId { get; }
        public ActionActor StaffActor { get; }
        public InspectionAddressResolutionStore AddressStore { get; }
        public EfCaseDataStore DataStore { get; }
        public EfCaseAcceptanceStore AcceptanceStore { get; }
        public AcceptIntake AcceptIntake { get; }
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

                var acceptanceStore = new EfCaseAcceptanceStore(factory, timeProvider);
                var acceptIntake = new AcceptIntake(
                    acceptanceStore,
                    new FixedConfiguration(),
                    new EfProviderInspectionModeStore(factory),
                    new CommittedWorkPublisherDouble());
                var dataStore = new EfCaseDataStore(factory, timeProvider);
                var workflowStore = new EfCaseWorkflowStore(factory, timeProvider);
                return new(
                    database,
                    factory,
                    receiptId,
                    staffActor,
                    new InspectionAddressResolutionStore(factory, timeProvider),
                    dataStore,
                    acceptanceStore,
                    acceptIntake,
                    new SaveCase(dataStore),
                    new AcquireCaseEditLease(workflowStore));
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public AcceptIntakeRequest CreateAcceptRequest(
            string operationKey,
            long expectedVersion = 0) => new(
            ReceiptId,
            expectedVersion,
            StaffActor,
            operationKey,
            "Accepted image-based provider case",
            CaseType.Inspection,
            "QDOS",
            new(true, true, false, false),
            AcceptedInspectionDeadline: FixtureInspectionDate);

        public Task<CaseAcceptanceOutcome> AcceptAsync(
            string operationKey,
            long expectedVersion = 0) =>
            AcceptIntake.ExecuteAsync(
                CreateAcceptRequest(operationKey, expectedVersion),
                CancellationToken.None);

        public Task<CaseEditLease> AcquireLeaseAsync(
            Guid caseId,
            long version,
            string operationKey) => acquireLease.ExecuteAsync(
            new(caseId, version, StaffActor, operationKey),
            CancellationToken.None);

        public async Task<CaseDataProjection> GetRequiredDataAsync(Guid caseId) =>
            await DataStore.GetAsync(caseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The case-data fixture was not persisted.");

        public async Task<long> CaseHistoryCountAsync(Guid caseId, string eventType)
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Database.SqlQuery<long>(
                    $"SELECT COUNT_BIG(*) AS [Value] FROM [CaseHistory] WHERE [CaseId] = {caseId} AND [EventType] = {eventType}")
                .SingleAsync();
        }

        public async Task SetPrincipalModeAsync(string modeCode)
        {
            await using var context = await factory.CreateDbContextAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Principals SET InspectionMode = {modeCode} WHERE Code = {"QDOS"}");
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
                """{"version":1,"data":[{"name":"Claimant name","suggestedValue":"Jane Example","candidates":[{"value":"Jane Example","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Claim number","suggestedValue":"QDOS-123","candidates":[{"value":"QDOS-123","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Vehicle registration","suggestedValue":"AB12 CDE","candidates":[{"value":"AB12 CDE","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Inspection address","suggestedValue":"1 Test Street, London","candidates":[{"value":"1 Test Street, London","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Inspection date","suggestedValue":"2031-05-20","candidates":[{"value":"2031-05-20","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false}]}""";
            var emptyEnvelope = """{"version":1,"data":[]}""";

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({receiptId}, {"qdos.eml"}, {"message/rfc822"}, {100L}, {sourceHash}, {"mailbox"}, {"mailbox-item-image-based-1"}, {StartUtc}, {StartUtc}, {"fixture-reader"}, {"1"}, {"qdos_instruction"}, {1}, {0L}, {"case_created"}, {"Ready fixture"}, {emptyEnvelope}, {fieldsJson}, {emptyEnvelope})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO InstructionDrafts (IntakeReceiptId, SuggestedPrincipalCode, ClaimantName, ClaimNumber, VehicleRegistration, InspectionAddress, InspectionDate) VALUES ({receiptId}, {"QDOS"}, {"Jane Example"}, {"QDOS-123"}, {"AB12CDE"}, {"1 Test Street, London"}, {FixtureInspectionDate})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeMailRouteDecisions (IntakeReceiptId, Disposition, RouteOwnerCode, RouteKind, WorkProviderCode, PredicatesJson, Reason, PolicyKey, PolicyVersion, TransportIdentitiesJson, OriginalIdentitiesJson) VALUES ({receiptId}, {"accepted"}, {"QDOS"}, {"direct_work_provider"}, {"QDOS"}, {emptyEnvelope}, {"Accepted QDOS route"}, {"qdos_mail_route"}, {2}, {emptyEnvelope}, {emptyEnvelope})");
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
