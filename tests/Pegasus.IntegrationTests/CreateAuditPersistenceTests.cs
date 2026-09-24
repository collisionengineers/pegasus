using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Create audit (Stage 2 slice 6): an Inspection + Audit Case whose report is
/// sent gains its Audit work under the same Case — one Cases row, one Case/PO —
/// with a full copy of the Inspection's data, and goes back to its Engineer.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CreateAuditPersistenceTests
{
    private const string Reference = "QDOS31007";
    private const string AuditReference = "a.QDOS31007";
    private static readonly int[] CopiedSpecificationVersions = [1, 2, 4];
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    /// <summary>The seeded Inspection's frozen calculation: the guide retail less a condition deduction.</summary>
    private static readonly ValuationCalculation SeededCalculation = new(
        GuideRetailValue: 12500m,
        CommercialVatApplied: false,
        CommercialVatAmount: 0m,
        ValueIncludingVat: 12500m,
        PriorTotalLossPercentage: null,
        PriorTotalLossAmount: 0m,
        Additions: [],
        AdditionsTotal: 0m,
        ConditionDeduction: 500m,
        Proposal: 12000m);

    [Fact]
    public async Task CreateAuditKeepsOneCaseAndCopiesTheInspectionIntoItsAuditWork()
    {
        await using var harness = await Harness.CreateAsync();
        var request = await harness.RequestAsync("create-audit");
        var before = await harness.Workflows.GetAsync(harness.CaseId, default);

        var result = await harness.CreateAudit.ExecuteAsync(request, default);

        Assert.False(result.IsReplay);
        Assert.Equal(AuditReference, result.AuditReference);
        Assert.Equal(harness.CaseId, result.Case.CaseId);
        Assert.Equal(Reference, result.Case.Reference);
        Assert.Equal(AuditReference, result.Case.AuditReference);
        Assert.NotNull(harness.Services.GetRequiredService<ICreateAudit>());

        await using var context = await harness.ContextAsync();
        Assert.Equal(1, await context.Cases.CountAsync(
            item => item.Reference == Reference || item.Reference == AuditReference));
        var caseEntity = await context.Cases.AsNoTracking().SingleAsync(item => item.Id == harness.CaseId);
        Assert.Equal(Reference, caseEntity.Reference);
        Assert.Equal(AuditReference, caseEntity.AuditReference);

        var works = await context.CaseWorks.AsNoTracking()
            .Where(item => item.CaseId == harness.CaseId)
            .ToListAsync();
        Assert.Equal(2, works.Count);
        var primary = Assert.Single(works, item => item.Kind == CaseWorkKinds.Primary);
        var audit = Assert.Single(works, item => item.Kind == CaseWorkKinds.Audit);
        Assert.Equal(harness.CaseId, primary.Id);
        Assert.Equal(result.AuditWorkId, audit.Id);
        Assert.Equal(harness.ApprovalId, primary.ReportApprovalId);
        Assert.Equal(harness.EvidenceId, primary.ReportSentEvidenceId);
        Assert.Null(audit.ReportApprovalId);
        Assert.Null(audit.ReportSentEvidenceId);

        var workflow = await context.CaseWorkflows.AsNoTracking().SingleAsync(item => item.CaseId == harness.CaseId);
        Assert.Equal(nameof(CaseLifecycleState.ReportPreparation), workflow.State);
        Assert.Null(workflow.ClosureOutcome);
        Assert.Equal(harness.EngineerId, workflow.AssignedEngineerId);
        Assert.Equal(harness.EngineerId, workflow.SignOffEngineerId);
        Assert.Null(workflow.ReportApprovalId);
        Assert.Null(workflow.ReportSentEvidenceId);
        Assert.Equal(before!.Version + 1, workflow.Version);
        Assert.Null(workflow.EditLeaseTokenHash);

        var custodyWork = await context.ExternalWorkItems.AsNoTracking()
            .SingleAsync(item => item.CaseId == harness.CaseId
                && item.Kind == ExternalWorkKinds.CreateAuditReferenceCustody);
        Assert.Equal($"audit-reference-custody:{harness.CaseId:N}", custodyWork.OperationKey);
        Assert.Equal("pending", custodyWork.State);
        Assert.False(string.IsNullOrWhiteSpace(custodyWork.AuditFolderCreationToken));
        Assert.Equal(custodyWork.Id, Assert.Single(harness.Publisher.Published));

        var history = await context.CaseWorkflowEvents.AsNoTracking()
            .SingleAsync(item => item.CaseId == harness.CaseId && item.OperationKey == "create-audit");
        Assert.Equal("audit_created", history.EventType);
        Assert.Equal($"Audit {AuditReference} created by {harness.EngineerUserName}", history.Reason);

        await AssertTheAuditIsAFullCopyAsync(context, harness, audit.Id);
    }

    [Fact]
    public async Task AReplayReturnsTheCommittedAuditAndASecondCreateIsRefused()
    {
        await using var harness = await Harness.CreateAsync();
        var request = await harness.RequestAsync("create-audit-once");

        var first = await harness.CreateAudit.ExecuteAsync(request, default);
        var replay = await harness.CreateAudit.ExecuteAsync(request, default);

        Assert.True(replay.IsReplay);
        Assert.Equal(first.AuditWorkId, replay.AuditWorkId);
        Assert.Equal(first.AuditReference, replay.AuditReference);
        Assert.Single(harness.Publisher.Published);

        var second = await harness.RequestAsync("create-audit-twice");
        var refused = await Assert.ThrowsAsync<AuditCreationException>(
            () => harness.CreateAudit.ExecuteAsync(second, default));
        Assert.Equal(AuditRefusal.AuditAlreadyExists, refused.Refusal);

        await using var context = await harness.ContextAsync();
        Assert.Equal(2, await context.CaseWorks.CountAsync(item => item.CaseId == harness.CaseId));
        Assert.Equal(1, await context.ExternalWorkItems.CountAsync(item => item.CaseId == harness.CaseId
            && item.Kind == ExternalWorkKinds.CreateAuditReferenceCustody));
    }

    [Theory]
    [InlineData(CaseLifecycleState.Held)]
    [InlineData(CaseLifecycleState.ReportPreparation)]
    [InlineData(CaseLifecycleState.NotReady)]
    [InlineData(CaseLifecycleState.ProviderCancelled)]
    public async Task CreateAuditIsRefusedUntilTheReportIsSentAndLeavesTheCaseUntouched(CaseLifecycleState state)
    {
        await using var harness = await Harness.CreateAsync();
        await harness.ExecuteSqlAsync(
            $"UPDATE CaseWorkflows SET State = '{state}' WHERE CaseId = '{harness.CaseId:D}'");
        var request = await harness.RequestAsync($"create-audit-{state}");

        var refused = await Assert.ThrowsAsync<AuditCreationException>(
            () => harness.CreateAudit.ExecuteAsync(request, default));

        Assert.Equal(AuditRefusal.ReportNotSent, refused.Refusal);
        await harness.AssertNoAuditAsync();
    }

    [Fact]
    public async Task APostReportCaseWithoutSentEvidenceIsRefused()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.ExecuteSqlAsync(
            $"UPDATE CaseWorkflows SET ReportSentEvidenceId = NULL WHERE CaseId = '{harness.CaseId:D}'");
        var request = await harness.RequestAsync("create-audit-unsent");

        var refused = await Assert.ThrowsAsync<AuditCreationException>(
            () => harness.CreateAudit.ExecuteAsync(request, default));

        Assert.Equal(AuditRefusal.ReportNotSent, refused.Refusal);
        await harness.AssertNoAuditAsync();
    }

    /// <summary>The store guards its own transaction, whatever Core decided before it.</summary>
    [Fact]
    public async Task TheStoreRefusesAHeldCaseByItself()
    {
        await using var harness = await Harness.CreateAsync();
        var request = await harness.RequestAsync("create-audit-held-store");
        await harness.ExecuteSqlAsync(
            $"UPDATE CaseWorkflows SET State = 'Held' WHERE CaseId = '{harness.CaseId:D}'");
        var store = harness.Services.GetRequiredService<ICreateAuditStore>();

        var refused = await Assert.ThrowsAsync<AuditCreationException>(() => store.CreateAsync(
            new CreateAuditCommand(
                request,
                new CaseIdentity(harness.CaseId, "QDOS", 2031, 7, Reference),
                AuditReference,
                harness.EngineerId),
            default));

        Assert.Equal(AuditRefusal.ReportNotSent, refused.Refusal);
        await harness.AssertNoAuditAsync();
    }

    /// <summary>
    /// The Audit's report is sent after the Audit exists: evidence sent before
    /// it belongs to the Inspection and is refused; evidence sent after it links.
    /// </summary>
    [Fact]
    public async Task AuditSentEvidenceMustFollowTheCreationOfTheAudit()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.CreateAudit.ExecuteAsync(await harness.RequestAsync("create-audit-evidence"), default);
        var retain = harness.Services.GetRequiredService<IRetainApprovedMailboxReportSentEvidence>();
        var now = DateTimeOffset.UtcNow;
        var older = await retain.ExecuteAsync(Evidence("older", now.AddHours(-2), now.AddHours(-1)), default);

        var olderLink = await harness.LinkRequestAsync("link-older", older.EvidenceId);
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Workflows.LinkReportEvidenceAsync(olderLink, default));
        Assert.Equal("Retained Sent evidence must follow the creation of the Audit.", refused.Message);

        var later = DateTimeOffset.UtcNow;
        var newer = await retain.ExecuteAsync(Evidence("newer", later, later), default);
        // The refusal left the lease held at the same version, so the second
        // link reuses it: a fresh take-over writes its event at the version
        // Create audit already occupies (#826).
        var linked = await harness.Workflows.LinkReportEvidenceAsync(
            olderLink with { OperationKey = "link-newer", EvidenceId = newer.EvidenceId },
            default);

        Assert.Equal(CaseLifecycleState.PostReport, linked.State);
        Assert.Equal(newer.EvidenceId, linked.ReportSentEvidence?.EvidenceId);
        await using var context = await harness.ContextAsync();
        var primary = await context.CaseWorks.AsNoTracking().SingleAsync(item => item.Id == harness.CaseId);
        Assert.Equal(harness.EvidenceId, primary.ReportSentEvidenceId);
    }

    /// <summary>
    /// Correct principal after Create audit starts the replacement from the
    /// Inspection's own values, never the Audit's, and the replacement carries
    /// no Audit report reference.
    /// </summary>
    [Fact]
    public async Task AReplacementAfterCreateAuditStartsFromTheInspectionValues()
    {
        await using var harness = await Harness.CreateAsync();
        var result = await harness.CreateAudit.ExecuteAsync(await harness.RequestAsync("create-audit-replace"), default);
        await harness.ExecuteSqlAsync(
            $"UPDATE CaseDataFields SET Value = 'Audit Claimant' WHERE WorkId = '{result.AuditWorkId:D}' AND FieldName = '{CaseDataFieldNames.ClaimantName}'");
        await harness.SeedPrincipalAsync("AUDR");
        var (version, token) = await harness.ClaimAsync("replace-after-audit");
        var replace = new CreateLinkedReplacement(
            harness.Services.GetRequiredService<ILinkedCaseReplacementStore>(),
            new DiscardingCommittedWorkPublisher());

        var replacement = await replace.ExecuteAsync(
            new CreateLinkedReplacementRequest(
                harness.CaseId,
                version,
                harness.Actor,
                "replace-after-audit",
                "The case was allocated to the wrong principal",
                token,
                "AUDR"),
            default);

        await using var context = await harness.ContextAsync();
        var replacementCase = await context.Cases.AsNoTracking()
            .SingleAsync(item => item.Id == replacement.Identity.CaseId);
        Assert.Null(replacementCase.AuditReference);
        Assert.Equal(1, await context.CaseWorks.CountAsync(item => item.CaseId == replacementCase.Id));
        var claimant = await context.Set<CaseDataFieldEntity>().AsNoTracking()
            .Where(item => item.WorkId == replacementCase.Id
                && item.FieldName == CaseDataFieldNames.ClaimantName
                && item.ValueKind == "confirmed")
            .Select(item => item.Value)
            .SingleAsync();
        Assert.Equal("Jane Inspection", claimant);
    }

    /// <summary>
    /// The Audit's a. folder is not a workflow fact: confirming it records the
    /// folder and leaves whoever is editing the Audit their lease and version.
    /// </summary>
    [Fact]
    public async Task ConfirmingTheAuditFolderLeavesTheEditLeaseAndVersionAlone()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.CreateAudit.ExecuteAsync(await harness.RequestAsync("create-audit-folder"), default);
        await harness.ClaimAsync("edit-while-the-folder-is-made");
        var editing = await harness.ReadEditAsync();
        var workId = await harness.AuditCustodyWorkIdAsync();

        await harness.CustodyProcessor(new AuditFolderCustody()).ExecuteAsync(workId, default);

        Assert.NotNull(editing.LeaseTokenHash);
        Assert.Equal(editing, await harness.ReadEditAsync());
        await using var context = await harness.ContextAsync();
        var caseEntity = await context.Cases.AsNoTracking().SingleAsync(item => item.Id == harness.CaseId);
        Assert.Equal($"case-{Reference}/{AuditReference}", caseEntity.AuditCustodyRemoteId);
        Assert.NotNull(caseEntity.AuditCustodyConfirmedAtUtc);
        Assert.Equal("completed", await context.ExternalWorkItems
            .Where(item => item.Id == workId)
            .Select(item => item.State)
            .SingleAsync());
        var confirmed = await context.CaseHistory.AsNoTracking()
            .SingleAsync(item => item.CaseId == harness.CaseId && item.EventType == "audit_custody_confirmed");
        Assert.Equal(editing.Version, confirmed.BeforeVersion);
        Assert.Equal(editing.Version, confirmed.AfterVersion);
    }

    /// <summary>A failed a. folder likewise leaves the editor's lease and version alone.</summary>
    [Fact]
    public async Task AFailedAuditFolderLeavesTheEditLeaseAndVersionAlone()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.CreateAudit.ExecuteAsync(await harness.RequestAsync("create-audit-folder-fails"), default);
        await harness.ClaimAsync("edit-while-the-folder-fails");
        var editing = await harness.ReadEditAsync();
        var workId = await harness.AuditCustodyWorkIdAsync();

        await Assert.ThrowsAsync<HttpRequestException>(
            () => harness.CustodyProcessor(new AuditFolderCustody(fails: true)).ExecuteAsync(workId, default));

        Assert.NotNull(editing.LeaseTokenHash);
        Assert.Equal(editing, await harness.ReadEditAsync());
        await using var context = await harness.ContextAsync();
        Assert.Equal("failed", await context.ExternalWorkItems
            .Where(item => item.Id == workId)
            .Select(item => item.State)
            .SingleAsync());
        var failed = await context.CaseHistory.AsNoTracking()
            .SingleAsync(item => item.CaseId == harness.CaseId && item.EventType == "audit_custody_failed");
        Assert.Equal(editing.Version, failed.BeforeVersion);
        Assert.Equal(editing.Version, failed.AfterVersion);
    }

    /// <summary>
    /// Once the Audit exists its confirmed vehicle facts are the ones read,
    /// as the Case data read beside them is; the Inspection's stay its own.
    /// </summary>
    [Fact]
    public async Task VehicleEvidenceReadsTheAuditsConfirmedVehicleFacts()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.ExecuteSqlAsync(
            $"UPDATE CaseDataFields SET ValueKind = 'confirmed', ConfirmedByActor = 'engineer', ConfirmedAtUtc = SYSDATETIMEOFFSET() WHERE WorkId = '{harness.CaseId:D}' AND FieldName = '{CaseDataFieldNames.VehicleRegistration}'");
        var result = await harness.CreateAudit.ExecuteAsync(await harness.RequestAsync("create-audit-vehicle"), default);
        await harness.ExecuteSqlAsync(
            $"UPDATE CaseDataFields SET Value = 'AU12DIT' WHERE WorkId = '{result.AuditWorkId:D}' AND FieldName = '{CaseDataFieldNames.VehicleRegistration}'");

        var evidence = await harness.Services.GetRequiredService<Pegasus.Core.Vehicle.IVehicleEvidenceQueries>()
            .GetAsync(harness.CaseId, default);

        Assert.Equal("AU12DIT", evidence?.Confirmed?.Registration?.Value);
    }

    private static async Task AssertTheAuditIsAFullCopyAsync(PegasusDbContext context, Harness harness, Guid auditWorkId)
    {
        var sourceSnapshot = await context.CaseDataSnapshots.AsNoTracking()
            .Include(item => item.Fields)
            .SingleAsync(item => item.WorkId == harness.CaseId);
        var auditSnapshot = await context.CaseDataSnapshots.AsNoTracking()
            .Include(item => item.Fields)
            .SingleAsync(item => item.WorkId == auditWorkId);
        Assert.Equal("Audit contact", auditSnapshot.ClaimSourceOverrideContactName);
        Assert.Equal("0113 999 0014", auditSnapshot.ClaimSourceOverrideContactTelephone);
        Assert.Equal("audit-contact@example.test", auditSnapshot.ClaimSourceOverrideContactEmailAddress);
        Assert.Equal(sourceSnapshot.CompletenessPolicyKey, auditSnapshot.CompletenessPolicyKey);
        Assert.Equal(sourceSnapshot.AcceptedAtUtc, auditSnapshot.AcceptedAtUtc);
        Assert.Equal(
            sourceSnapshot.Fields.OrderBy(item => item.FieldName).ThenBy(item => item.ValueKind)
                .Select(item => (item.FieldName, item.ValueKind, item.Value, item.ConfirmedByActor, item.ConfirmedAtUtc)),
            auditSnapshot.Fields.OrderBy(item => item.FieldName).ThenBy(item => item.ValueKind)
                .Select(item => (item.FieldName, item.ValueKind, item.Value, item.ConfirmedByActor, item.ConfirmedAtUtc)));

        var auditFields = await context.CaseAssessmentFields.AsNoTracking()
            .Where(item => item.WorkId == auditWorkId)
            .ToListAsync();
        var fee = Assert.Single(auditFields);
        Assert.Equal("assessment.fee", fee.FieldPath);
        Assert.Equal("150.00", fee.Value);
        Assert.NotNull(fee.ConfirmedAtUtc);

        var sourceSpecifications = await context.CaseRepairSpecifications.AsNoTracking()
            .Include(item => item.Lines)
            .Where(item => item.WorkId == harness.CaseId)
            .ToListAsync();
        var auditSpecifications = await context.CaseRepairSpecifications.AsNoTracking()
            .Include(item => item.Lines)
            .Where(item => item.WorkId == auditWorkId)
            .ToListAsync();
        Assert.Equal(CopiedSpecificationVersions, auditSpecifications.Select(item => item.Version).Order());
        Assert.DoesNotContain(auditSpecifications, item => item.State == nameof(RepairSpecificationState.Discarded));
        Assert.Empty(auditSpecifications.Select(item => item.Id).Intersect(sourceSpecifications.Select(item => item.Id)));
        var superseded = Assert.Single(auditSpecifications, item => item.Version == 1);
        var current = Assert.Single(auditSpecifications, item => item.Version == 2);
        var draft = Assert.Single(auditSpecifications, item => item.Version == 4);
        Assert.Equal(superseded.Id, current.SupersedesSpecificationId);
        Assert.Null(draft.SupplementaryOfSpecificationId);
        Assert.Equal("additional", draft.SupplementaryReason);
        Assert.True(current.IsCurrent);
        Assert.Equal(nameof(RepairSpecificationState.Accepted), current.State);
        Assert.Equal(harness.AiJobId, current.AiJobId);
        Assert.Equal("specification-2", current.CreationOperationKey);
        var sourceLine = Assert.Single(sourceSpecifications.Single(item => item.Version == 2).Lines);
        var line = Assert.Single(current.Lines);
        Assert.NotEqual(sourceLine.Id, line.Id);
        Assert.Equal(auditWorkId, line.WorkId);
        Assert.Equal(current.Id, line.RepairSpecificationId);
        Assert.Equal(sourceLine.Materials, line.Materials);
        Assert.Equal(sourceLine.OriginalValuesJson, line.OriginalValuesJson);
        Assert.Equal(sourceLine.CurrentValuesJson, line.CurrentValuesJson);
        Assert.Equal(sourceLine.SourceDocumentIdentity, line.SourceDocumentIdentity);
        Assert.Equal(sourceLine.SourceRowIdentity, line.SourceRowIdentity);
        Assert.Equal(sourceLine.AmendedBy, line.AmendedBy);
        Assert.Equal(sourceLine.AmendedAtUtc, line.AmendedAtUtc);
        Assert.Equal(sourceLine.Price, line.Price);

        Assert.False(await context.CaseRepairSpecificationSnapshots.AnyAsync(item => item.WorkId == auditWorkId));
        Assert.False(await context.CaseFieldProposals.AnyAsync(item => item.WorkId == auditWorkId));

        var guide = await context.CaseValuations.AsNoTracking().SingleAsync(item => item.WorkId == auditWorkId);
        Assert.NotEqual(harness.GuideValuationId, guide.Id);
        Assert.Equal(12500m, guide.RetailValue);
        Assert.Equal(harness.GuideStampUtc, guide.RecordedAtUtc);

        var applied = await context.Set<AppliedValuationSnapshotEntity>().AsNoTracking()
            .SingleAsync(item => item.WorkId == auditWorkId);
        Assert.NotEqual(harness.AppliedValuationId, applied.Id);
        Assert.Contains(guide.Id.ToString("D"), applied.SnapshotJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(harness.GuideValuationId.ToString("D"), applied.SnapshotJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            EfValuationStore.AppliedSnapshotHash(
                harness.CaseId, 3L, guide.Id, harness.GuideStampUtc, SeededCalculation, 12000m, "Adopted the guide figures"),
            applied.SnapshotHash);
        Assert.Equal(12000m, applied.AcceptedEngineerValue);

        var wording = await context.CaseReportWordings.AsNoTracking().SingleAsync(item => item.WorkId == auditWorkId);
        Assert.Equal("summary", wording.BlockKey);
        Assert.Equal("The Engineer's own summary.", wording.Text);
    }

    private static RetainApprovedMailboxReportSentEvidenceRequest Evidence(
        string suffix,
        DateTimeOffset sentAtUtc,
        DateTimeOffset discoveredAtUtc) => new(
            Guid.NewGuid(),
            Harness.ApprovedMailboxAddress,
            $"sent-folder-{suffix}",
            $"immutable-item-{suffix}",
            $"internet-message-{suffix}",
            $"conversation-{suffix}",
            $"reply-chain-{suffix}",
            $"source-occurrence-{suffix}",
            new string('a', 64),
            new string('b', 64),
            sentAtUtc,
            discoveredAtUtc,
            ActionActor.SystemWorker("approved-mailbox-evidence-ingestion"),
            $"retain-{suffix}");

    private sealed class RecordingPublisher : ICommittedExternalWorkPublisher
    {
        public List<Guid> Published { get; } = [];

        public Task PublishAsync(Guid workItemId, CancellationToken cancellationToken)
        {
            Published.Add(workItemId);
            return Task.CompletedTask;
        }
    }

    /// <summary>The Case folder already exists; the Audit's a. folder is made inside it, or the adapter fails.</summary>
    private sealed class AuditFolderCustody(bool fails = false) : ICaseCustody
    {
        public Task<CaseCustodyRoot> CreateCaseRootAsync(
            Guid caseId, string caseReference, string creationOwnerToken, string operationKey,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("The Case folder already exists.");

        public Task<CaseCustodyRoot> GetExistingCaseRootAsync(
            Guid caseId,
            string caseReference,
            CancellationToken cancellationToken) => Task.FromResult(
                new CaseCustodyRoot(caseId, $"case-{caseReference}", caseReference));

        public Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceAsync(
            CaseCustodyRoot root, IntakeSourceCustodyReference source, string operationKey,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("An Audit folder retains no intake source.");

        public Task<string> CreateAuditReferenceFolderAsync(
            CaseCustodyRoot root, string auditReference, string creationOwnerToken, string operationKey,
            CancellationToken cancellationToken) => fails
                ? throw new HttpRequestException("Fixture adapter failure.")
                : Task.FromResult($"{root.RemoteId}/{auditReference}");
    }

    private sealed class Harness : IAsyncDisposable
    {
        public const string ApprovedMailboxIdentity = "instructions";
        public const string ApprovedMailboxAddress = "instructions@collisionengineers.co.uk";

        private readonly LocalDbTestDatabase database;
        private readonly AsyncServiceScope scope;

        private Harness(LocalDbTestDatabase database, AsyncServiceScope scope, Seed seed)
        {
            this.database = database;
            this.scope = scope;
            CaseId = seed.CaseId;
            EngineerId = seed.EngineerId;
            EngineerUserName = seed.EngineerUserName;
            ApprovalId = seed.ApprovalId;
            EvidenceId = seed.EvidenceId;
            GuideValuationId = seed.GuideValuationId;
            AppliedValuationId = seed.AppliedValuationId;
            GuideStampUtc = seed.GuideStampUtc;
            AiJobId = seed.AiJobId;
            Actor = ActionActor.Staff(seed.EngineerId, [StaffRole.Engineer]);
            var services = scope.ServiceProvider;
            Workflows = services.GetRequiredService<ICaseWorkflowStore>();
            Leases = services.GetRequiredService<ILeaseCaseForEdit>();
            CreateAudit = new Pegasus.Core.Lifecycle.CreateAudit(
                services.GetRequiredService<IGetCaseHeader>(),
                services.GetRequiredService<ICaseWorkflowQueries>(),
                services.GetRequiredService<ICaseEngineerEligibility>(),
                services.GetRequiredService<ICreateAuditStore>(),
                Publisher);
        }

        public Guid CaseId { get; }
        public Guid EngineerId { get; }
        public string EngineerUserName { get; }
        public Guid ApprovalId { get; }
        public Guid EvidenceId { get; }
        public Guid GuideValuationId { get; }
        public Guid AppliedValuationId { get; }
        public DateTimeOffset GuideStampUtc { get; }
        public Guid AiJobId { get; }
        public ActionActor Actor { get; }
        public IServiceProvider Services => scope.ServiceProvider;
        public ICaseWorkflowStore Workflows { get; }
        public ILeaseCaseForEdit Leases { get; }
        public RecordingPublisher Publisher { get; } = new();
        public CreateAudit CreateAudit { get; }

        public Task<PegasusDbContext> ContextAsync() => database.CreateContextAsync();

        public Task ExecuteSqlAsync(string sql) => database.ExecuteAsync(sql);

        /// <summary>A second active Principal, so Correct principal has somewhere to go.</summary>
        public async Task SeedPrincipalAsync(string code)
        {
            var organizationId = Guid.NewGuid();
            var lineageId = Guid.NewGuid();
            await ExecuteSqlAsync(
                $"INSERT INTO Organizations (Id, Name, Version) VALUES ('{organizationId:D}', N'{code} provider', 0)");
            await ExecuteSqlAsync(
                $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ('{lineageId:D}', '2026-01-01 00:00:00 +00:00')");
            await ExecuteSqlAsync(
                $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, PredecessorId, SuccessorId, IsActive, Version) VALUES ('{Guid.NewGuid():D}', '{organizationId:D}', N'{code}', '{lineageId:D}', NULL, NULL, 1, 0)");
        }

        public async Task<CreateAuditRequest> RequestAsync(string operationKey)
        {
            var (version, token) = await ClaimAsync(operationKey);
            return new(CaseId, version, Actor, operationKey, token);
        }

        public async Task<LinkReportEvidenceRequest> LinkRequestAsync(string operationKey, Guid evidenceId)
        {
            var (version, token) = await ClaimAsync(operationKey);
            return new(CaseId, version, Actor, operationKey, "The Audit report was sent", token, evidenceId);
        }

        /// <summary>A fresh lease at the current version; a refused command leaves the last one held.</summary>
        public async Task<(long Version, string Token)> ClaimAsync(string operationKey)
        {
            var workflow = await Workflows.GetAsync(CaseId, default)
                ?? throw new InvalidOperationException("The seeded Case has no workflow.");
            var lease = await Leases.ClaimAsync(
                new ClaimCaseEditLeaseRequest(CaseId, workflow.Version, Actor, $"claim-{operationKey}") { TakeOver = true },
                default);
            return (workflow.Version, lease.Token);
        }

        /// <summary>The workflow version and its edit lease, read as one value to compare.</summary>
        public async Task<(long Version, string? LeaseTokenHash, string? LeaseHolder, DateTimeOffset? LeaseExpiresAtUtc)> ReadEditAsync()
        {
            await using var context = await ContextAsync();
            var workflow = await context.CaseWorkflows.AsNoTracking().SingleAsync(item => item.CaseId == CaseId);
            return (workflow.Version, workflow.EditLeaseTokenHash, workflow.EditLeaseHolder, workflow.EditLeaseExpiresAtUtc);
        }

        public async Task<Guid> AuditCustodyWorkIdAsync()
        {
            await using var context = await ContextAsync();
            return await context.ExternalWorkItems.AsNoTracking()
                .Where(item => item.CaseId == CaseId && item.Kind == ExternalWorkKinds.CreateAuditReferenceCustody)
                .Select(item => item.Id)
                .SingleAsync();
        }

        /// <summary>The real custody processor over this database, behind the given adapter.</summary>
        public EfQueuedCustodyProcessor CustodyProcessor(ICaseCustody custody) => new(
            Services.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
            Services.GetRequiredService<IExternalWorkStore>(),
            custody,
            Services.GetRequiredService<TimeProvider>());

        public async Task AssertNoAuditAsync()
        {
            await using var context = await ContextAsync();
            Assert.Equal(1, await context.CaseWorks.CountAsync(item => item.CaseId == CaseId));
            Assert.False(await context.ExternalWorkItems.AnyAsync(item => item.CaseId == CaseId
                && item.Kind == ExternalWorkKinds.CreateAuditReferenceCustody));
            Assert.Null(await context.Cases.Where(item => item.Id == CaseId)
                .Select(item => item.AuditReference)
                .SingleAsync());
        }

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await database.DisposeAsync();
        }

        public static async Task<Harness> CreateAsync()
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            try
            {
                var seed = await SeedAsync(database);
                return new(database, database.CreateAsyncScope(), seed);
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        private static async Task<Seed> SeedAsync(LocalDbTestDatabase database)
        {
            var now = DateTimeOffset.UtcNow;
            var caseId = Guid.NewGuid();
            var engineerId = Guid.NewGuid();
            var engineerUserName = $"create-audit-{engineerId:N}";
            var approvalId = Guid.NewGuid();
            var evidenceId = Guid.NewGuid();
            var guideValuationId = Guid.NewGuid();
            var appliedValuationId = Guid.NewGuid();
            var guideStampUtc = new DateTimeOffset(2031, 5, 1, 9, 0, 0, TimeSpan.Zero);
            var aiJobId = Guid.NewGuid();

            await using (var context = await database.CreateContextAsync())
            {
                var principal = await SeededPrincipals.QdosAsync(context);
                var engineerRoleId = await context.Roles
                    .Where(role => role.NormalizedName == "ENGINEER")
                    .Select(role => role.Id)
                    .SingleAsync();
                context.Users.Add(new PegasusIdentityUser
                {
                    Id = engineerId,
                    UserName = engineerUserName,
                    NormalizedUserName = engineerUserName.ToUpperInvariant(),
                    IsEnabled = true,
                    MustChangePassword = false,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString("N")
                });
                context.UserRoles.Add(new IdentityUserRole<Guid> { UserId = engineerId, RoleId = engineerRoleId });
                var mailbox = (await context.ApprovedMailboxes.FindAsync(
                    await TestMailboxId.EnsureApprovedAsync(
                        context, ApprovedMailboxIdentity, ApprovedMailboxAddress, now.AddDays(-30))))!;
                mailbox.AllowSentEvidence = true;
                mailbox.SentFolderIdentity = "sent-items";
                mailbox.Version = 2;

                var caseEntity = new CaseEntity
                {
                    Id = caseId,
                    PrincipalId = principal.Id,
                    SequenceLineageId = principal.SequenceLineageId,
                    Year = 2031,
                    Sequence = 7,
                    Reference = Reference,
                    Type = "inspection_and_audit",
                    InitialState = "review",
                    CustodyState = "confirmed",
                    CustodyRootRemoteId = "case-root",
                    CustodyConfirmedAtUtc = now.AddDays(-9),
                    InstructionComplete = true,
                    ImagesComplete = true,
                    CreatedAtUtc = now.AddDays(-10),
                    ConcurrencyToken = Guid.NewGuid()
                };
                context.Cases.Add(caseEntity);
                context.CaseReportApprovals.Add(new CaseReportApprovalEntity
                {
                    Id = approvalId,
                    CaseId = caseId,
                    ArtifactIdentity = $"artifact-{approvalId:N}",
                    ArtifactSha256 = new string('c', 64),
                    ApprovedByKind = nameof(ActorKind.Staff),
                    ApprovedBySubjectId = engineerId.ToString("D"),
                    ApprovedByRolesJson = JsonSerializer.Serialize(new[] { StaffRole.Engineer }),
                    ApprovedAtUtc = now.AddDays(-3)
                });
                context.CaseReportSentEvidence.Add(new CaseReportSentEvidenceEntity
                {
                    Id = evidenceId,
                    CaseId = caseId,
                    MailboxIdentity = ApprovedMailboxAddress,
                    SentFolderIdentity = "sent-items",
                    ImmutableItemIdentity = $"inspection-item-{evidenceId:N}",
                    InternetMessageIdentity = $"inspection-message-{evidenceId:N}",
                    ConversationIdentity = $"inspection-conversation-{evidenceId:N}",
                    ReplyChainIdentity = $"inspection-reply-chain-{evidenceId:N}",
                    SourceOccurrenceIdentity = $"inspection-occurrence-{evidenceId:N}",
                    SourceSha256 = new string('d', 64),
                    MimeSha256 = new string('e', 64),
                    SentAtUtc = now.AddDays(-2),
                    DiscoveredAtUtc = now.AddDays(-2),
                    DiscoveredByKind = nameof(ActorKind.SystemWorker),
                    DiscoveredBySubjectId = "approved-mailbox-evidence-ingestion",
                    RetentionOperationKey = $"retain-inspection-{evidenceId:N}",
                    RetentionRequestHash = new string('f', 64),
                    LinkedAtUtc = now.AddDays(-2),
                    LinkedByKind = nameof(ActorKind.SystemWorker),
                    LinkedBySubjectId = "approved-mailbox-sent-poll",
                    LinkedByRolesJson = "[]"
                });
                context.CaseWorkflows.Add(new CaseWorkflowEntity
                {
                    CaseId = caseId,
                    Case = caseEntity,
                    State = nameof(CaseLifecycleState.PostReport),
                    StateEnteredAtUtc = now.AddDays(-2),
                    AssignedEngineerId = engineerId,
                    SignOffEngineerId = engineerId,
                    ReportApprovalId = approvalId,
                    ReportSentEvidenceId = evidenceId,
                    Version = 5,
                    ConcurrencyToken = Guid.NewGuid()
                });
                await context.SaveChangesAsync();
            }

            await using (var context = await database.CreateContextAsync())
            {
                SeedInspectionData(context, caseId, engineerId.ToString("D"), now);
                SeedSpecifications(context, caseId, aiJobId, now);
                context.CaseValuations.Add(new CaseValuationEntity
                {
                    Id = guideValuationId,
                    WorkId = caseId,
                    Source = nameof(ValuationSource.Glasses),
                    Date = new DateOnly(2031, 5, 1),
                    Time = new TimeOnly(9, 0),
                    GuideMonth = new DateOnly(2031, 5, 1),
                    Mileage = 42000,
                    RetailValue = 12500m,
                    TradeValue = 11000m,
                    RecordedBy = engineerId.ToString("D"),
                    RecordedAtUtc = guideStampUtc
                });
                // A frozen calculation adopted from the guide card; the copy
                // re-points it and rehashes it from its own proposal.
                var snapshotJson = JsonSerializer.Serialize(
                    new { caseVersion = 3L, guideValuationId, guideValuationStampUtc = guideStampUtc, calculation = SeededCalculation },
                    WebJson);
                context.Set<AppliedValuationSnapshotEntity>().Add(new AppliedValuationSnapshotEntity
                {
                    Id = appliedValuationId,
                    WorkId = caseId,
                    SnapshotJson = snapshotJson,
                    CalculationPolicyVersion = "valuation-calculation/v1",
                    GeneratedByKind = nameof(ActorKind.Staff),
                    GeneratedBySubjectId = engineerId.ToString("D"),
                    SnapshotHash = EfValuationStore.AppliedSnapshotHash(
                        caseId, 3L, guideValuationId, guideStampUtc, SeededCalculation, 12000m, "Adopted the guide figures"),
                    AcceptedEngineerValue = 12000m,
                    AcceptedBy = engineerId.ToString("D"),
                    AcceptedAtUtc = now.AddDays(-4),
                    Reason = "Adopted the guide figures",
                    PolicyVersion = "valuation/v1"
                });
                context.CaseReportWordings.Add(new CaseReportWordingEntity
                {
                    Id = Guid.NewGuid(),
                    WorkId = caseId,
                    BlockKey = "summary",
                    Title = "Summary",
                    Text = "The Engineer's own summary.",
                    Order = 1,
                    Included = true,
                    Manual = false,
                    UpdatedBy = engineerId.ToString("D"),
                    UpdatedAtUtc = now.AddDays(-4)
                });
                context.CaseFieldProposals.Add(new CaseFieldProposalEntity
                {
                    WorkId = caseId,
                    FieldPath = "assessment.outcome",
                    ProposedValue = "repairable",
                    ProposedBy = "automation",
                    ProposedAtUtc = now.AddDays(-4)
                });
                await context.SaveChangesAsync();
            }

            return new(
                caseId,
                engineerId,
                engineerUserName,
                approvalId,
                evidenceId,
                guideValuationId,
                appliedValuationId,
                guideStampUtc,
                aiJobId);
        }

        private static void SeedInspectionData(PegasusDbContext context, Guid caseId, string staff, DateTimeOffset now)
        {
            var snapshot = new CaseDataSnapshotEntity
            {
                WorkId = caseId,
                CompletenessPolicyKey = "case-completeness",
                CompletenessPolicyVersion = 1,
                CompletenessPolicySatisfied = true,
                AcceptedAtUtc = now.AddDays(-10),
                ClaimSourceOverrideContactName = "Audit contact",
                ClaimSourceOverrideContactTelephone = "0113 999 0014",
                ClaimSourceOverrideContactEmailAddress = "audit-contact@example.test"
            };
            snapshot.Fields.Add(Field(snapshot, caseId, CaseDataFieldNames.ClaimantName, "confirmed", "Jane Inspection", staff, now));
            snapshot.Fields.Add(Field(snapshot, caseId, CaseDataFieldNames.VehicleRegistration, "fact", "AB12CDE", null, now));
            context.CaseDataSnapshots.Add(snapshot);
            context.CaseAssessmentFields.Add(new CaseAssessmentFieldEntity
            {
                WorkId = caseId,
                FieldPath = "assessment.fee",
                Value = "150.00",
                RecordedByKind = "Staff",
                RecordedBy = staff,
                RecordedAtUtc = now.AddDays(-4),
                ConfirmedBy = staff,
                ConfirmedAtUtc = now.AddDays(-4)
            });
        }

        private static CaseDataFieldEntity Field(
            CaseDataSnapshotEntity snapshot,
            Guid caseId,
            string name,
            string valueKind,
            string value,
            string? confirmedBy,
            DateTimeOffset now) => new()
        {
            WorkId = caseId,
            Snapshot = snapshot,
            FieldName = name,
            ValueKind = valueKind,
            ValueType = "text",
            Value = value,
            SourceKind = "staff_correction",
            SourceIdentity = "create-audit-test",
            SourceLabel = "Staff",
            PolicyKey = "case-data",
            PolicyVersion = 1,
            ConfirmedByActor = confirmedBy,
            ConfirmedAtUtc = confirmedBy is null ? null : (DateTimeOffset?)now.AddDays(-5)
        };

        /// <summary>
        /// Four specifications: v1 superseded by the current v2, a discarded v3,
        /// and a draft v4 supplementing the discarded one.
        /// </summary>
        private static void SeedSpecifications(PegasusDbContext context, Guid caseId, Guid aiJobId, DateTimeOffset now)
        {
            var superseded = Specification(caseId, 1, nameof(RepairSpecificationState.Superseded), now);
            superseded.AcceptedBy = "engineer";
            superseded.AcceptedAtUtc = now.AddDays(-6);
            superseded.Lines.Add(Line(caseId, superseded.Id, now));
            var current = Specification(caseId, 2, nameof(RepairSpecificationState.Accepted), now);
            current.AcceptedBy = "engineer";
            current.AcceptedAtUtc = now.AddDays(-5);
            current.IsCurrent = true;
            current.AiJobId = aiJobId;
            current.SupersedesSpecificationId = superseded.Id;
            current.SupersessionReason = "Revised after inspection";
            current.Lines.Add(Line(caseId, current.Id, now));
            var discarded = Specification(caseId, 3, nameof(RepairSpecificationState.Discarded), now);
            discarded.DiscardedBy = "engineer";
            discarded.DiscardedAtUtc = now.AddDays(-5);
            discarded.DiscardReason = "Entered in error";
            var draft = Specification(caseId, 4, nameof(RepairSpecificationState.Draft), now);
            draft.SupplementaryOfSpecificationId = discarded.Id;
            draft.SupplementaryReason = "additional";
            draft.SupplementaryStatement = "Further damage found.";
            context.CaseRepairSpecifications.AddRange(superseded, current, discarded, draft);
            context.CaseRepairSpecificationSnapshots.Add(new CaseRepairSpecificationSnapshotEntity
            {
                Id = Guid.NewGuid(),
                WorkId = caseId,
                SpecificationId = current.Id,
                Number = 1,
                Kind = nameof(RepairSpecificationSnapshotKind.Imported),
                Origin = "import",
                CreatedBy = "engineer",
                CreatedAtUtc = now.AddDays(-5),
                DetailsJson = "{}",
                LinesJson = "[]",
                SupplementaryJson = "{}",
                ContentHash = new string('9', 64),
                Gross = 480m,
                SentOnReport = false
            });
        }

        private static CaseRepairSpecificationEntity Specification(
            Guid caseId,
            int version,
            string state,
            DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            WorkId = caseId,
            Version = version,
            State = state,
            SourceRoute = nameof(RepairSpecificationSourceRoute.Manual),
            CreatedBy = "engineer",
            CreationOperationKey = $"specification-{version}",
            CreatedAtUtc = now.AddDays(-7 + version),
            Name = $"Estimate {version}",
            VatPercent = 20m,
            LastOperationKey = $"specification-{version}"
        };

        private static CaseEstimateLineEntity Line(Guid caseId, Guid specificationId, DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            WorkId = caseId,
            RepairSpecificationId = specificationId,
            Position = 1,
            LineType = "repair",
            Description = "Front bumper",
            WorkUnits = 10m,
            Quantity = 1,
            Price = 400m,
            Status = "confirmed",
            RecordedByKind = "Staff",
            RecordedBy = "engineer",
            RecordedAtUtc = now.AddDays(-6),
            Materials = 80m,
            OriginalValuesJson = "{\"price\":380}",
            CurrentValuesJson = "{\"price\":400}",
            SourceDocumentIdentity = "estimate.pdf",
            SourceRowIdentity = "row-1",
            AmendedBy = "engineer",
            AmendedAtUtc = now.AddDays(-5)
        };

        private sealed record Seed(
            Guid CaseId,
            Guid EngineerId,
            string EngineerUserName,
            Guid ApprovalId,
            Guid EvidenceId,
            Guid GuideValuationId,
            Guid AppliedValuationId,
            DateTimeOffset GuideStampUtc,
            Guid AiJobId);
    }
}
