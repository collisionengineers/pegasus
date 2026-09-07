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
public sealed class CaseDataCompletenessPersistenceTests
{
    [Fact]
    public async Task AcceptanceSnapshotsTypedSourceProvenanceWithAutoAddedValues()
    {
        await using var harness = await CaseDataHarness.CreateAsync();

        var projection = await harness.DataStore.GetAsync(
            harness.CaseId,
            CancellationToken.None);

        Assert.NotNull(projection);
        Assert.Equal(CaseLifecycleState.Review, projection.State);
        Assert.True(projection.Completeness.Evaluation.SatisfiesPolicy);
        Assert.Equal(harness.ReceiptId, projection.Origin.IntakeReceiptId);
        Assert.Equal("mailbox-item-immutable-1", projection.Origin.ExternalReceiptToken);
        Assert.Equal(harness.SourceHash, projection.Origin.SourceHash);
        Assert.Equal("QDOS", projection.Provider.WorkProviderCode.Fact?.Value);
        Assert.True(projection.Provider.WorkProviderCode.Fact?.IsAccepted);
        Assert.Equal(
            CaseDataSourceKind.MailRoute,
            projection.Provider.WorkProviderCode.Fact?.Source.Kind);
        Assert.Equal(
            "qdos_mail_route",
            projection.Provider.WorkProviderCode.Fact?.Source.PolicyKey);
        Assert.Equal(2, projection.Provider.WorkProviderCode.Fact?.Source.PolicyVersion);
        // INTK-021: an unambiguous extracted value is auto-added (Fact),
        // not parked as a suggestion awaiting confirmation.
        Assert.Null(projection.Claimant.Name.Suggestion);
        Assert.Equal("Jane Example", projection.Claimant.Name.Fact?.Value);
        Assert.Null(projection.Claimant.Name.Confirmed);
        Assert.Equal("qdos_instruction", projection.Claimant.Name.Fact?.Source.PolicyKey);
        Assert.Contains("instructions.pdf", projection.Claimant.Name.Fact?.Source.Label);
        Assert.Equal("1 Test Street, London", projection.Inspection.Address.Fact?.Value);
        Assert.Equal(
            CaseDataSourceKind.IntakeEvidence,
            projection.Inspection.Address.Fact?.Source.Kind);
        Assert.Equal("qdos_instruction", projection.Inspection.Address.Fact?.Source.PolicyKey);
        Assert.Equal(
            Ext18InspectionAddressPolicy.ImageBasedAssessment,
            projection.Inspection.Address.Confirmed?.Value);
        Assert.Equal(
            harness.StaffActor.SubjectId,
            projection.Inspection.Address.Confirmed?.ConfirmedByActor);
        Assert.Equal(
            CaseDataSourceKind.ProviderSetting,
            projection.Inspection.Address.Confirmed?.Source.Kind);
        Assert.Equal(
            ProviderInspectionModePolicy.PolicyKey,
            projection.Inspection.Address.Confirmed?.Source.PolicyKey);
        Assert.Equal(
            CaseInspectionMode.ImageBasedAssessment,
            projection.Inspection.Mode.Confirmed?.Value);
        Assert.Null(projection.Contact.Name.Current);
        Assert.Null(projection.Instruction.VatStatus.Current);

        var currentAddress = await harness.AddressStore.GetAsync(
            harness.ReceiptId,
            CancellationToken.None);
        Assert.NotNull(currentAddress?.Evaluation.Suggestion);
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.AddressStore.ResolveAsync(
            new(
                harness.ReceiptId,
                currentAddress!.ReceiptVersion,
                currentAddress.Evaluation.Suggestion!.Fingerprint,
                InspectionAddressStaffDecision.AcceptSuggestion,
                null,
                harness.StaffActor,
                Guid.NewGuid(),
                "address-after-acceptance"),
            CancellationToken.None));
    }

    [Fact]
    public async Task CorrectedInspectionAddressRetainsExtractedValueAndRecordsStaffCorrectionSource()
    {
        await using var harness = await CaseDataHarness.CreateAsync(
            InspectionAddressStaffDecision.CorrectSuggestion,
            "2 Corrected Street, London");

        var projection = await harness.GetRequiredDataAsync();

        Assert.Equal("1 Test Street, London", projection.Inspection.Address.Fact?.Value);
        Assert.Equal(
            CaseDataSourceKind.IntakeEvidence,
            projection.Inspection.Address.Fact?.Source.Kind);
        Assert.Equal(
            "qdos_instruction",
            projection.Inspection.Address.Fact?.Source.PolicyKey);
        Assert.Equal(
            Ext18InspectionAddressPolicy.ImageBasedAssessment,
            projection.Inspection.Address.Confirmed?.Value);
        Assert.Equal(
            CaseDataSourceKind.ProviderSetting,
            projection.Inspection.Address.Confirmed?.Source.Kind);
        Assert.Equal(
            ProviderInspectionModePolicy.PolicyKey,
            projection.Inspection.Address.Confirmed?.Source.PolicyKey);
        Assert.Equal(
            CaseInspectionMode.ImageBasedAssessment,
            projection.Inspection.Mode.Confirmed?.Value);

        var retainedAddress = await harness.AddressStore.GetAsync(
            harness.ReceiptId,
            CancellationToken.None);
        Assert.NotNull(retainedAddress);
        Assert.Equal(InspectionAddressResolutionState.Corrected, retainedAddress.State);
        Assert.Equal("2 Corrected Street, London", retainedAddress.ResolvedValue);
        Assert.Equal(Guid.Parse(harness.StaffActor.SubjectId), retainedAddress.ResolvedByStaffId);
        Assert.NotNull(retainedAddress.ResolvedAtUtc);
        var extractedAddress = Assert.Single(retainedAddress.Evaluation.Suggestion!.Provenance);
        Assert.Equal("qdos_instruction", extractedAddress.PolicyKey);
        Assert.Equal(1, extractedAddress.PolicyVersion);
    }

    [Fact]
    public async Task ConfirmAndSaveUseSharedVersionLeaseReplayAndImmutableHistory()
    {
        await using var harness = await CaseDataHarness.CreateAsync(
            instructionConfirmedByStaff: true,
            imagesConfirmedByStaff: true);
        var initial = await harness.GetRequiredDataAsync();
        Assert.Equal(0, initial.Version);
        Assert.Equal(41, await harness.HiddenCaseVersionAsync());
        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "lease-confirm");
        var confirmation = new ConfirmCompletenessRequest(
            harness.CaseId,
            initial.Version,
            harness.StaffActor,
            "confirm-completeness-1",
            "Confirmed instruction and image evidence",
            lease.Token,
            new(
                true,
                true,
                initial.Completeness.Values.InstructionConfirmedByStaff,
                initial.Completeness.Values.ImagesConfirmedByStaff));

        var confirmed = await harness.ConfirmCompleteness.ExecuteAsync(
            confirmation,
            CancellationToken.None);
        var replayedConfirmation = await harness.ConfirmCompleteness.ExecuteAsync(
            confirmation,
            CancellationToken.None);

        Assert.Equal(CaseLifecycleState.Review, confirmed.State);
        Assert.Equal(1, confirmed.Version);
        Assert.Equal(
            initial.Completeness.Values.InstructionConfirmedByStaff,
            confirmed.Completeness.Values.InstructionConfirmedByStaff);
        Assert.Equal(
            initial.Completeness.Values.ImagesConfirmedByStaff,
            confirmed.Completeness.Values.ImagesConfirmedByStaff);
        Assert.Equal(confirmed, replayedConfirmation);
        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.ConfirmCompleteness.ExecuteAsync(
                confirmation with { Reason = "Different confirmation material" },
                CancellationToken.None));

        var saveLease = await harness.AcquireLeaseAsync(
            confirmed.Version,
            harness.StaffActor,
            "lease-save");
        var save = new SaveCaseRequest(
            harness.CaseId,
            confirmed.Version,
            harness.StaffActor,
            "save-case-1",
            "Confirmed the reviewed case values",
            saveLease.Token,
            new(
                ClaimantName: "Jane Example",
                ClaimNumber: "QDOS-123",
                VehicleRegistration: "AB12 CDE",
                InspectionDeadline: new DateOnly(2031, 5, 20),
                InspectionAddress: "1 Test Street, London",
                InspectionMode: CaseInspectionMode.PhysicalAddress));

        var saved = await harness.SaveCase.ExecuteAsync(save, CancellationToken.None);
        var replayedSave = await harness.SaveCase.ExecuteAsync(save, CancellationToken.None);

        Assert.Equal(2, saved.Version);
        // The legacy SaveCase demotes the case as a side effect of editing any
        // fact. The Case workspace save does not: it re-evaluates readiness
        // from the row it just wrote
        // (CaseWorkspacePersistenceTests.ASaveDoesNotDemoteCompletenessAsASideEffect).
        // This assertion is retained deliberately, because SaveCase's own
        // behaviour is unchanged by CASE-047.
        Assert.Equal(CaseLifecycleState.NotReady, saved.State);
        Assert.False(saved.Completeness.Values.InstructionComplete);
        Assert.False(saved.Completeness.Values.InstructionConfirmedByStaff);
        Assert.Equal(saved, replayedSave);
        Assert.Equal("Jane Example", saved.Claimant.Name.Fact?.Value);
        Assert.Equal("Jane Example", saved.Claimant.Name.Confirmed?.Value);
        Assert.Equal("AB12CDE", saved.Vehicle.Registration.Confirmed?.Value);
        Assert.Equal(initial.Identity, saved.Identity);
        Assert.Equal(initial.Origin, saved.Origin);
        Assert.Equal(2, await harness.HistoryCountAsync());
        Assert.Equal(41, await harness.HiddenCaseVersionAsync());

        var reconfirmLease = await harness.AcquireLeaseAsync(
            saved.Version,
            harness.StaffActor,
            "lease-reconfirm");
        var reconfirmed = await harness.ConfirmCompleteness.ExecuteAsync(
            new(
                harness.CaseId,
                saved.Version,
                harness.StaffActor,
                "confirm-completeness-2",
                "Reconfirmed after the case-data change",
                reconfirmLease.Token,
                new(true, true, true, true)),
            CancellationToken.None);
        Assert.Equal(3, reconfirmed.Version);
        Assert.Equal(CaseLifecycleState.Review, reconfirmed.State);
        Assert.Equal(3, await harness.HistoryCountAsync());
        Assert.Equal(41, await harness.HiddenCaseVersionAsync());

        await Assert.ThrowsAsync<CaseVersionConflictException>(() => harness.SaveCase.ExecuteAsync(
            save with
            {
                ExpectedVersion = 1,
                OperationKey = "save-stale-version",
                Data = save.Data with { ClaimantName = "Stale overwrite" }
            },
            CancellationToken.None));
    }

    [Fact]
    public async Task MissingWrongHolderWrongTokenAndExpiredLeasesNeverOverwrite()
    {
        await using var harness = await CaseDataHarness.CreateAsync();
        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            harness.StaffActor,
            "lease-denial-matrix");
        var changed = new CaseEditableData(ClaimantName: "Changed claimant");

        await Assert.ThrowsAsync<ArgumentException>(() => harness.SaveCase.ExecuteAsync(
            new(
                harness.CaseId,
                initial.Version,
                harness.StaffActor,
                "save-missing-lease",
                "Missing lease denial",
                " ",
                changed),
            CancellationToken.None));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() => harness.SaveCase.ExecuteAsync(
            new(
                harness.CaseId,
                initial.Version,
                harness.StaffActor,
                "save-wrong-token",
                "Wrong token denial",
                "not-the-issued-token",
                changed),
            CancellationToken.None));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.ConfirmCompleteness.ExecuteAsync(
                new(
                    harness.CaseId,
                    initial.Version,
                    harness.StaffActor,
                    "confirm-wrong-token",
                    "Wrong completeness lease token denial",
                    "not-the-issued-token",
                    new(true, true, true, true)),
                CancellationToken.None));

        var otherStaff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() => harness.SaveCase.ExecuteAsync(
            new(
                harness.CaseId,
                initial.Version,
                otherStaff,
                "save-wrong-holder",
                "Wrong holder denial",
                lease.Token,
                changed),
            CancellationToken.None));

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(5));
        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() => harness.SaveCase.ExecuteAsync(
            new(
                harness.CaseId,
                initial.Version,
                harness.StaffActor,
                "save-expired-lease",
                "Expired lease denial",
                lease.Token,
                changed),
            CancellationToken.None));

        var after = await harness.GetRequiredDataAsync();
        Assert.Equal(initial.Version, after.Version);
        Assert.Equal("Jane Example", after.Claimant.Name.Fact?.Value);
        Assert.Null(after.Claimant.Name.Confirmed);
        Assert.Equal(0, await harness.HistoryCountAsync());
    }

    /// <summary>
    /// Internal, not private, so the Case workspace persistence tests build
    /// their fixture from this one accepted-case recipe instead of a second
    /// copy of it.
    /// </summary>
    internal sealed class CaseDataHarness : IAsyncDisposable
    {
        private static readonly DateTimeOffset StartUtc =
            new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        private readonly LocalDbTestDatabase database;
        private readonly PooledDbContextFactory<PegasusDbContext> factory;
        private readonly AcquireCaseEditLease acquireLease;

        private CaseDataHarness(
            LocalDbTestDatabase database,
            PooledDbContextFactory<PegasusDbContext> factory,
            MutableTimeProvider timeProvider,
            Guid receiptId,
            Guid caseId,
            string sourceHash,
            ActionActor staffActor,
            InspectionAddressResolutionStore addressStore,
            EfCaseDataStore dataStore,
            ConfirmCompleteness confirmCompleteness,
            SaveCase saveCase,
            AcquireCaseEditLease acquireLease,
            EfCaseWorkflowStore workflowStore,
            EfCaseWorkspaceStore workspaceStore)
        {
            Factory = factory;
            WorkflowStore = workflowStore;
            WorkspaceStore = workspaceStore;
            this.database = database;
            this.factory = factory;
            TimeProvider = timeProvider;
            ReceiptId = receiptId;
            CaseId = caseId;
            SourceHash = sourceHash;
            StaffActor = staffActor;
            AddressStore = addressStore;
            DataStore = dataStore;
            ConfirmCompleteness = confirmCompleteness;
            SaveCase = saveCase;
            this.acquireLease = acquireLease;
        }

        public IDbContextFactory<PegasusDbContext> Factory { get; }
        public EfCaseWorkflowStore WorkflowStore { get; }
        public EfCaseWorkspaceStore WorkspaceStore { get; }
        public MutableTimeProvider TimeProvider { get; }
        public Guid ReceiptId { get; }
        public Guid CaseId { get; }
        public string SourceHash { get; }
        public ActionActor StaffActor { get; }
        public InspectionAddressResolutionStore AddressStore { get; }
        public EfCaseDataStore DataStore { get; }
        public ConfirmCompleteness ConfirmCompleteness { get; }
        public SaveCase SaveCase { get; }

        public static async Task<CaseDataHarness> CreateAsync(
            InspectionAddressStaffDecision addressDecision =
                InspectionAddressStaffDecision.AcceptSuggestion,
            string? correctedAddress = null,
            bool instructionConfirmedByStaff = false,
            bool imagesConfirmedByStaff = false)
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            try
            {
                var options = new DbContextOptionsBuilder<PegasusDbContext>()
                    .UseSqlServer(database.ConnectionString)
                    .Options;
                var factory = new PooledDbContextFactory<PegasusDbContext>(options);
                var timeProvider = new MutableTimeProvider(StartUtc);
                var receiptId = Guid.NewGuid();
                var staffId = Guid.NewGuid();
                var staffActor = ActionActor.Staff(staffId, [StaffRole.User]);
                var sourceHash = new string('a', 64);
                await SeedAsync(factory, receiptId, sourceHash);

                var addressStore = new InspectionAddressResolutionStore(factory, timeProvider);
                var address = await addressStore.GetAsync(receiptId, CancellationToken.None);
                var suggestion = address?.Evaluation.Suggestion
                    ?? throw new InvalidOperationException("The address fixture did not produce a suggestion.");
                var resolved = await addressStore.ResolveAsync(
                    new(
                        receiptId,
                        address!.ReceiptVersion,
                        suggestion.Fingerprint,
                        addressDecision,
                        correctedAddress,
                        staffActor,
                        Guid.NewGuid(),
                        "address-before-acceptance"),
                    CancellationToken.None);

                var configuration = new FixedConfiguration();
                var acceptanceStore = new EfCaseAcceptanceStore(factory, timeProvider);
                var accept = new AcceptIntake(
                    acceptanceStore,
                    configuration,
                    new EfProviderInspectionModeStore(factory),
                    new CommittedWorkPublisherDouble());
                var outcome = await accept.ExecuteAsync(
                    new(
                        receiptId,
                        resolved.ReceiptVersion,
                        staffActor,
                        "accept-case-data-fixture",
                        "Accepted reviewed QDOS case data",
                        CaseType.Inspection,
                        "QDOS",
                        new(
                            true,
                            true,
                            instructionConfirmedByStaff,
                            imagesConfirmedByStaff),
                        AcceptedInspectionDeadline: new DateOnly(2031, 5, 20)),
                    CancellationToken.None);
                await using (var divergenceContext = await factory.CreateDbContextAsync())
                {
                    await divergenceContext.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE Cases SET Version = {41L} WHERE Id = {outcome.Identity.CaseId}");
                }


                var workflowStore = new EfCaseWorkflowStore(factory, timeProvider);
                var dataStore = new EfCaseDataStore(factory, timeProvider);
                return new(
                    database,
                    factory,
                    timeProvider,
                    receiptId,
                    outcome.Identity.CaseId,
                    sourceHash,
                    staffActor,
                    addressStore,
                    dataStore,
                    new ConfirmCompleteness(dataStore, configuration),
                    new SaveCase(dataStore),
                    new AcquireCaseEditLease(workflowStore),
                    workflowStore,
                    new EfCaseWorkspaceStore(factory, timeProvider, configuration));
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public Task<CaseEditLease> AcquireLeaseAsync(
            long version,
            ActionActor actor,
            string operationKey) => acquireLease.ExecuteAsync(
            new(CaseId, version, actor, operationKey),
            CancellationToken.None);

        public async Task<CaseDataProjection> GetRequiredDataAsync() =>
            await DataStore.GetAsync(CaseId, CancellationToken.None)
            ?? throw new InvalidOperationException("The case-data fixture was not persisted.");
        public async Task<long> HiddenCaseVersionAsync()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Database.SqlQuery<long>(
                    $"SELECT [Version] AS [Value] FROM [Cases] WHERE [Id] = {CaseId}")
                .SingleAsync();
        }


        public async Task<long> HistoryCountAsync()
        {
            await using var context = await factory.CreateDbContextAsync();
            return await context.Database.SqlQuery<long>(
                    $"SELECT COUNT_BIG(*) AS [Value] FROM [ActionHistory] WHERE [AggregateType] = {"case"} AND [AggregateId] = {CaseId.ToString("D")}")
                .SingleAsync();
        }

        public async ValueTask DisposeAsync() => await database.DisposeAsync();

        private static async Task SeedAsync(
            IDbContextFactory<PegasusDbContext> factory,
            Guid receiptId,
            string sourceHash)
        {
            await using var context = await factory.CreateDbContextAsync();
            var principal = await SeededPrincipals.QdosAsync(context);
            var organizationId = principal.OrganizationId;
            var lineageId = principal.SequenceLineageId;
            var principalId = principal.Id;
            var fieldsJson =
                """{"version":1,"data":[{"name":"Claimant name","suggestedValue":"Jane Example","candidates":[{"value":"Jane Example","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Claim number","suggestedValue":"QDOS-123","candidates":[{"value":"QDOS-123","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Vehicle registration","suggestedValue":"AB12 CDE","candidates":[{"value":"AB12 CDE","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Inspection address","suggestedValue":"1 Test Street, London","candidates":[{"value":"1 Test Street, London","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Inspection date","suggestedValue":"2031-05-20","candidates":[{"value":"2031-05-20","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false}]}""";
            var emptyEnvelope = """{"version":1,"data":[]}""";

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({receiptId}, {"qdos.eml"}, {"message/rfc822"}, {100L}, {sourceHash}, {"mailbox"}, {"mailbox-item-immutable-1"}, {StartUtc}, {StartUtc}, {"fixture-reader"}, {"1"}, {"qdos_instruction"}, {1}, {0L}, {"case_created"}, {"Ready fixture"}, {emptyEnvelope}, {fieldsJson}, {emptyEnvelope})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO InstructionDrafts (IntakeReceiptId, SuggestedPrincipalCode, ClaimantName, ClaimNumber, VehicleRegistration, InspectionAddress, InspectionDate) VALUES ({receiptId}, {"QDOS"}, {"Jane Example"}, {"QDOS-123"}, {"AB12CDE"}, {"1 Test Street, London"}, {new DateOnly(2031, 5, 20)})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeMailRouteDecisions (IntakeReceiptId, Disposition, RouteOwnerCode, RouteKind, WorkProviderCode, PredicatesJson, Reason, PolicyKey, PolicyVersion, TransportIdentitiesJson, OriginalIdentitiesJson) VALUES ({receiptId}, {"accepted"}, {"QDOS"}, {"direct_work_provider"}, {"QDOS"}, {emptyEnvelope}, {"Accepted QDOS route"}, {"qdos_mail_route"}, {2}, {emptyEnvelope}, {emptyEnvelope})");
        }
    }

    internal sealed class FixedConfiguration : ICaseWorkflowConfiguration
    {
        private static readonly CaseWorkflowConfiguration Configuration = new(
            "case-workflow",
            1);

        public Task<CaseWorkflowConfiguration> GetCurrentAsync(
            CancellationToken cancellationToken) => Task.FromResult(Configuration);
    }

    public sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset current = utcNow;

        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan interval)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(interval, TimeSpan.Zero);
            current += interval;
        }
    }
}
