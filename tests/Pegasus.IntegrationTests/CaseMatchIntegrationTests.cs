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
        Assert.Equal("12345/1", row.DurableClaimToken);
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
            new("12345/1", null, null, null, null),
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
            new("12345/1", null, null, null, null),
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
        Assert.Equal("12345/1", row.DurableClaimToken);
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
                && item.EventType == "intake_case_linked_automatic"));
    }

    /// <summary>
    /// CASE-024: automatic association does not wait for an editor to finish. It writes receipt
    /// rows only — no case row, no case version — so there is nothing for a staff edit to lose,
    /// and the yield it used to perform was one-shot, silently costing the association for the
    /// length of any editing session now that a lease is held for as long as the editor is open.
    /// </summary>
    [Fact]
    public async Task AutomaticAssociationWritesWhileAStaffEditLeaseIsLiveAndLeavesTheCaseUntouched()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("case-match-accept-lease");
        var caseId = outcome.Identity.CaseId;
        var projection = await harness.GetRequiredDataAsync(caseId);
        var chaserReceiptId = await harness.SeedAdditionalReceiptAsync("chaser-token-lease");
        var lease = await harness.AcquireLeaseAsync(
            caseId,
            projection.Version,
            "case-match-lease-live");

        var store = new EfIntakeMutationStore(harness.Factory);
        var associated = await store.AssociateFromMatchAsync(
            new(
                chaserReceiptId,
                caseId,
                "qdos_case_match",
                1,
                "system-worker:intake-processing",
                "case-match-association:while-leased",
                "Automatic association while a staff editor holds the case."),
            StartUtc,
            CancellationToken.None);

        Assert.Equal(AutomaticCaseAssociationOutcome.Associated, associated);

        await using var context = await harness.Factory.CreateDbContextAsync();
        var workflow = await context.CaseWorkflows.AsNoTracking()
            .SingleAsync(item => item.CaseId == caseId);
        // The editor keeps the case exactly as they loaded it: same version, same lease.
        Assert.Equal(projection.Version, workflow.Version);
        Assert.Equal(lease.Holder, workflow.EditLeaseHolder);
        Assert.Equal(lease.ExpiresAtUtc, workflow.EditLeaseExpiresAtUtc);
        var history = Assert.Single(await context.IntakeMutationHistory
            .AsNoTracking()
            .Where(item => item.IntakeReceiptId == chaserReceiptId
                && item.EventType == "intake_case_linked_automatic")
            .ToListAsync());
        Assert.Null(history.ExpectedCaseVersion);
        Assert.Null(history.BeforeCaseVersion);
        Assert.Null(history.AfterCaseVersion);
    }

    [Fact]
    public async Task StaffReversedAssociationIsNeverSilentlyRelinkedByAutomaticMatching()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("case-match-accept-5");
        var caseId = outcome.Identity.CaseId;
        var chaserReceiptId = await harness.SeedAdditionalReceiptAsync("chaser-token-2");

        await using (var seed = await harness.Factory.CreateDbContextAsync())
        {
            await seed.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeManualAssociations (IntakeReceiptId, CaseId, IsActive, Version, LinkedAtUtc, UnlinkedAtUtc, ActorKind, ActorSubjectId, ActorRolesJson, Reason, LastOperationKey, MatchPolicyKey, MatchPolicyVersion) VALUES ({chaserReceiptId}, {caseId}, {false}, {1L}, {StartUtc}, {StartUtc.AddMinutes(5)}, {"Staff"}, {Guid.NewGuid().ToString()}, {"[]"}, {"Staff reversed a mistaken automatic match"}, {"case-match-association:reversed-op"}, {"qdos_case_match"}, {1})");
        }

        var store = new EfIntakeMutationStore(harness.Factory);
        var outcomeAfterReversal = await store.AssociateFromMatchAsync(
            new(
                chaserReceiptId,
                caseId,
                "qdos_case_match",
                1,
                "system-worker:intake-processing",
                "case-match-association:new-evaluation-op",
                "Automatic association from a later evaluation."),
            StartUtc.AddMinutes(10),
            CancellationToken.None);

        Assert.Equal(AutomaticCaseAssociationOutcome.AlreadyAssociated, outcomeAfterReversal);
        await using var context = await harness.Factory.CreateDbContextAsync();
        var association = Assert.Single(
            await context.IntakeManualAssociations
                .AsNoTracking()
                .Where(item => item.IntakeReceiptId == chaserReceiptId)
                .ToListAsync());
        Assert.False(association.IsActive);
        Assert.Equal("case-match-association:reversed-op", association.LastOperationKey);
    }

    [Fact]
    public async Task RetainedMailAssociationRejectsStaleEvidenceThenWritesAndReplaysOnce()
    {
        await using var harness = await Harness.CreateAsync();
        var accepted = await harness.AcceptAsync("mail-case-association-accept");
        var receiptId = await harness.SeedRetainedMailReceiptAsync(
            "mail-case-association-token",
            "AB12 CDE",
            "thread-1");
        var store = new EfIntakeMutationStore(harness.Factory);
        var evidence = await store.GetAsync(receiptId, CancellationToken.None);
        Assert.Equal([accepted.Identity.CaseId], Assert.IsType<AutomaticMailCaseAssociationEvidence>(evidence).RegistrationCaseIds);

        await harness.SetReceiptRegistrationAsync(receiptId, "XY99 ZZZ");
        var staleRequest = new AutomaticCaseAssociationRequest(
            receiptId,
            accepted.Identity.CaseId,
            AssociateRetainedMailWithCase.PolicyKey,
            AssociateRetainedMailWithCase.PolicyVersion,
            "system-worker:intake-processing",
            $"mail-case-association:{receiptId:N}",
            "Automatic association from retained mail evidence.",
            evidence.Fingerprint);
        await Assert.ThrowsAsync<IntakeAssociationConflictException>(() =>
            store.AssociateFromMatchAsync(staleRequest, StartUtc, CancellationToken.None));

        await harness.SetReceiptRegistrationAsync(receiptId, "AB12 CDE");
        var useCase = new AssociateRetainedMailWithCase(store, store, TimeProvider.System);
        Assert.Equal(
            AutomaticCaseAssociationOutcome.Associated,
            await useCase.ExecuteAsync(receiptId));
        Assert.Equal(
            AutomaticCaseAssociationOutcome.AlreadyAssociated,
            await useCase.ExecuteAsync(receiptId));

        await using var context = await harness.Factory.CreateDbContextAsync();
        var association = Assert.Single(await context.IntakeManualAssociations
            .AsNoTracking()
            .Where(item => item.IntakeReceiptId == receiptId)
            .ToListAsync());
        Assert.Equal(accepted.Identity.CaseId, association.CaseId);
        Assert.Equal(AssociateRetainedMailWithCase.PolicyKey, association.MatchPolicyKey);
        Assert.Equal(1, await context.IntakeMutationHistory.CountAsync(item =>
            item.IntakeReceiptId == receiptId
            && item.EventType == "intake_case_linked_automatic"));
        var retainedMessageId = await context.RetainedMailboxMessages
            .Where(item => item.ExternalReceiptToken == "mail-case-association-token")
            .Select(item => item.Id)
            .SingleAsync();
        var retainedDetail = await new EfRetainedMailboxMessageStore(harness.Factory)
            .GetAsync(retainedMessageId, CancellationToken.None);
        Assert.Equal(accepted.Identity.CaseId, retainedDetail?.Summary.CaseId);

        var sameThreadId = await harness.SeedRetainedMailReceiptAsync(
            "same-thread-token",
            null,
            "thread-1");
        var otherMailboxId = await harness.SeedRetainedMailReceiptAsync(
            "other-mailbox-token",
            null,
            "thread-1",
            "mailbox-2");
        Assert.Equal(
            [accepted.Identity.CaseId],
            (await store.GetAsync(sameThreadId, CancellationToken.None))!.ThreadCaseIds);
        Assert.Empty((await store.GetAsync(otherMailboxId, CancellationToken.None))!.ThreadCaseIds);
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
            new("12345/1", "AB12CDE", "EXAMPLE", "J", new DateOnly(2026, 6, 18)),
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
        Assert.Equal("12345/1", audit.Keys.DurableClaimToken);
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
                        new EfProviderInspectionModeStore(factory),
                        new CommittedWorkPublisherDouble()),
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

        public async Task<Guid> SeedRetainedMailReceiptAsync(
            string externalToken,
            string? registration,
            string conversationIdentity,
            string mailboxId = "mailbox-1")
        {
            var id = await SeedAdditionalReceiptAsync(externalToken);
            var messageId = Guid.NewGuid();
            var sourceHash = new string('d', 64);
            await using var context = await Factory.CreateDbContextAsync();
            if (registration is not null)
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO InstructionDrafts (IntakeReceiptId, VehicleRegistration) VALUES ({id}, {registration})");
            }
            var approvedMailboxId = await TestMailboxId.EnsureApprovedAsync(
                context,
                mailboxId,
                $"{mailboxId}@example.test",
                StartUtc);
            await context.SaveChangesAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"IF NOT EXISTS (SELECT 1 FROM ApprovedInboxPollStates WHERE ApprovedMailboxId = {approvedMailboxId}) INSERT INTO ApprovedInboxPollStates (ApprovedMailboxId, MailboxAddress, ScopeFingerprint, ActivatedAtUtc, DueAtUtc) VALUES ({approvedMailboxId}, {$"{mailboxId}@example.test"}, {new string('0', 64)}, {StartUtc}, {StartUtc})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO RetainedMailboxMessages (Id, MailboxId, MailboxAddress, FolderScope, FolderIdentity, ImmutableMessageId, ConversationIdentity, ExternalReceiptToken, ToAddressesJson, CcAddressesJson, SourceLength, SourceSha256, ReceivedAtUtc, RetainedAtUtc, IsRead) VALUES ({messageId}, {approvedMailboxId}, {"intake@example.test"}, {"inbox"}, {"inbox-folder"}, {$"immutable-{messageId:N}"}, {conversationIdentity}, {externalToken}, {"[]"}, {"[]"}, {50L}, {sourceHash}, {StartUtc}, {StartUtc}, {false})");
            return id;
        }

        public async Task SetReceiptRegistrationAsync(Guid receiptId, string registration)
        {
            await using var context = await Factory.CreateDbContextAsync();
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE InstructionDrafts SET VehicleRegistration = {registration} WHERE IntakeReceiptId = {receiptId}");
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
                """{"version":1,"data":[{"name":"Claimant name","suggestedValue":"Mrs Jane Example","candidates":[{"value":"Mrs Jane Example","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Claim number","suggestedValue":"ABC/DEF/12345/1","candidates":[{"value":"ABC/DEF/12345/1","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Vehicle registration","suggestedValue":"AB12 CDE","candidates":[{"value":"AB12 CDE","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Date of incident","suggestedValue":"2031-04-01","candidates":[{"value":"2031-04-01","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Inspection address","suggestedValue":"1 Test Street, London","candidates":[{"value":"1 Test Street, London","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Inspection date","suggestedValue":"2031-05-20","candidates":[{"value":"2031-05-20","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false}]}""";
            var emptyEnvelope = """{"version":1,"data":[]}""";

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({receiptId}, {"qdos.eml"}, {"message/rfc822"}, {100L}, {sourceHash}, {"mailbox"}, {"case-match-item-1"}, {StartUtc}, {StartUtc}, {"fixture-reader"}, {"1"}, {"qdos_instruction"}, {1}, {0L}, {"case_created"}, {"Ready fixture"}, {emptyEnvelope}, {fieldsJson}, {emptyEnvelope})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO InstructionDrafts (IntakeReceiptId, SuggestedPrincipalCode, ClaimantName, ClaimNumber, VehicleRegistration, DateOfIncident, InspectionAddress, InspectionDate) VALUES ({receiptId}, {"QDOS"}, {"Mrs Jane Example"}, {"ABC/DEF/12345/1"}, {"AB12CDE"}, {new DateOnly(2031, 4, 1)}, {"1 Test Street, London"}, {FixtureInspectionDate})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeMailRouteDecisions (IntakeReceiptId, Disposition, RouteOwnerCode, RouteKind, WorkProviderCode, PredicatesJson, Reason, PolicyKey, PolicyVersion, TransportIdentitiesJson, OriginalIdentitiesJson) VALUES ({receiptId}, {"accepted"}, {"QDOS"}, {"direct_work_provider"}, {"QDOS"}, {emptyEnvelope}, {"Accepted QDOS route"}, {"qdos_mail_route"}, {3}, {emptyEnvelope}, {emptyEnvelope})");
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
