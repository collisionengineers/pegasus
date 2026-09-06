using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// The durable half of B07: one preparation is one serializable transaction
/// over the Foundation <c>CaseReportDeliveryIntents</c> table that re-reads
/// the Case, its lease and the generation's rows, pins every confirmed
/// artifact by exact identity/hash/length, replays by operation key, and
/// conflicts when the same key carries a different payload. Nothing here
/// sends or records a Sent state.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseReportDeliveryPreparationPersistenceTests
{
    private static readonly DateTimeOffset StartUtc = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PreparePinsConfirmedArtifactsAndWritesThePreparedEventOnce()
    {
        await using var harness = await Harness.CreateAsync();
        var command = harness.PrepareCommand();

        var record = await harness.Store.PrepareAsync(command, CancellationToken.None);

        Assert.Equal(harness.GenerationId, record.Preparation.GenerationId);
        Assert.Equal(1, record.Preparation.Version);
        Assert.Equal(harness.Artifact.DocumentId, Assert.Single(record.Preparation.Artifacts).DocumentId);
        Assert.Equal(
            ("handler@principal.example", "DVR-31001"),
            (Assert.Single(record.Addressing.To).Address, record.Addressing.Subject));
        Assert.Equal(CaseReportGenerationState.Confirmed, record.GenerationState);
        Assert.True(record.GenerationIsCurrent);
        Assert.Equal(1, record.FrozenCaseVersion);
        Assert.Equal(record.FrozenCaseVersion, record.CurrentCaseVersion);
        Assert.Equal(1, await harness.IntentCountAsync());
        Assert.Equal(1, await harness.ActionHistoryCountAsync());
    }

    [Fact]
    public async Task TheSameOperationKeyReplaysAndADifferentPayloadConflicts()
    {
        await using var harness = await Harness.CreateAsync();
        var command = harness.PrepareCommand();
        var first = await harness.Store.PrepareAsync(command, CancellationToken.None);

        var replay = await harness.Store.PrepareAsync(command, CancellationToken.None);
        Assert.Equal(first.Preparation.Id, replay.Preparation.Id);
        Assert.Equal(1, await harness.IntentCountAsync());
        Assert.Equal(1, await harness.ActionHistoryCountAsync());

        // The same key is the same operation only with the same inputs.
        var changed = harness.PrepareCommand() with
        {
            Addressing = new(
                [new("other@principal.example", null)], [], "DVR-31001"),
        };
        await Assert.ThrowsAsync<CaseOperationConflictException>(
            () => harness.Store.PrepareAsync(changed, CancellationToken.None));
    }

    [Fact]
    public async Task PrepareRefusesAPendingSupersededOrStaleVersionGeneration()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SetGenerationStateAsync(nameof(CaseReportGenerationState.Pending));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Store.PrepareAsync(harness.PrepareCommand(), CancellationToken.None));

        await harness.SetGenerationStateAsync(nameof(CaseReportGenerationState.Confirmed));
        await harness.SupersedeGenerationAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Store.PrepareAsync(harness.PrepareCommand(), CancellationToken.None));

        await using var fresh = await Harness.CreateAsync();
        await fresh.SetGenerationVersionAsync(7);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fresh.Store.PrepareAsync(fresh.PrepareCommand(expectedGenerationVersion: 1), CancellationToken.None));
    }

    [Fact]
    public async Task PrepareRefusesAPartlyConfirmedGeneration()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.SetArtifactStateAsync(nameof(CaseReportArtifactStatus.Pending));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Store.PrepareAsync(harness.PrepareCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task PrepareRequiresTheCurrentCaseVersionAndTheHeldLease()
    {
        await using var harness = await Harness.CreateAsync();

        await Assert.ThrowsAsync<CaseVersionConflictException>(
            () => harness.Store.PrepareAsync(
                harness.PrepareCommand(expectedCaseVersion: 2), CancellationToken.None));
        await AssertThrowsAsyncAny<CaseEditLeaseExpiredException, CaseEditLeaseConflictException>(
            () => harness.Store.PrepareAsync(harness.PrepareCommand(leaseToken: "foreign-lease"), CancellationToken.None));
        Assert.Equal(0, await harness.IntentCountAsync());
    }

    [Fact]
    public async Task ReadinessRefusesWhenAConfirmedArtifactChangedUnderneath()
    {
        await using var harness = await Harness.CreateAsync();
        var record = await harness.Store.PrepareAsync(harness.PrepareCommand(), CancellationToken.None);
        var readiness = new ReportSendReadiness(harness.Store);
        var request = harness.ReadyRequest(record);

        // The pinned payload still matches its confirmed rows: ready.
        await readiness.RequireReadyAsync(request, CancellationToken.None);

        await harness.TamperArtifactVersionAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => readiness.RequireReadyAsync(request, CancellationToken.None));
    }

    /// <summary>
    /// Stream A review: the frozen Case version is the one the preparation
    /// pinned. A Case mutation after preparation — with the addressing and
    /// artifacts untouched — refuses the send boundary.
    /// </summary>
    [Fact]
    public async Task ReadinessRefusesWhenTheCaseMovedAfterPreparation()
    {
        await using var harness = await Harness.CreateAsync();
        var record = await harness.Store.PrepareAsync(harness.PrepareCommand(), CancellationToken.None);
        var readiness = new ReportSendReadiness(harness.Store);
        var request = harness.ReadyRequest(record);
        await readiness.RequireReadyAsync(request, CancellationToken.None);

        await harness.MoveCaseVersionAsync(2);
        var moved = await harness.Store.GetAsync(harness.Staff, harness.CaseId,
            record.Preparation.Id, CancellationToken.None);
        Assert.NotNull(moved);
        Assert.Equal(1, moved!.FrozenCaseVersion);
        Assert.Equal(2, moved.CurrentCaseVersion);

        await Assert.ThrowsAsync<CaseVersionConflictException>(
            () => readiness.RequireReadyAsync(harness.ReadyRequest(moved), CancellationToken.None));
    }

    [Fact]
    public async Task GetCurrentReturnsTheLatestPreparationOfTheCurrentGenerationOnly()
    {
        await using var harness = await Harness.CreateAsync();
        var first = await harness.Store.PrepareAsync(harness.PrepareCommand(), CancellationToken.None);

        var current = await harness.Store.GetCurrentAsync(harness.Staff, harness.CaseId, CancellationToken.None);
        Assert.Equal(first.Preparation.Id, current!.Preparation.Id);

        await harness.SupersedeGenerationAsync();
        Assert.Null(await harness.Store.GetCurrentAsync(harness.Staff, harness.CaseId, CancellationToken.None));
    }

    private static async Task AssertThrowsAsyncAny<T1, T2>(Func<Task> action)
        where T1 : Exception
        where T2 : Exception
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is T1 or T2)
        {
            return;
        }

        Assert.Fail($"Expected {typeof(T1).Name} or {typeof(T2).Name}.");
    }

    /// <summary>
    /// Seeds one case at version 1 with an active staff edit lease, one
    /// Confirmed generation at version 1, and one Confirmed artifact joined
    /// to a real custody version row — the minimum the preparation store
    /// re-reads inside its transaction.
    /// </summary>
    private sealed class Harness : IAsyncDisposable
    {
        internal const string OperationKey = "prepare-delivery-1";

        private readonly LocalDbTestDatabase database;

        private Harness(
            LocalDbTestDatabase database,
            PooledDbContextFactory<PegasusDbContext> factory,
            Guid caseId,
            ActionActor staff,
            CaseEditLease lease,
            Guid generationId,
            Guid artifactVersionId,
            SeededArtifact artifact)
        {
            this.database = database;
            Factory = factory;
            CaseId = caseId;
            Staff = staff;
            Lease = lease;
            GenerationId = generationId;
            ArtifactVersionId = artifactVersionId;
            Artifact = artifact;
            Store = new EfCaseReportDeliveryPreparationStore(
                factory, new FixedTimeProvider(StartUtc));
        }

        public PooledDbContextFactory<PegasusDbContext> Factory { get; }

        public Guid CaseId { get; }

        public ActionActor Staff { get; }

        public CaseEditLease Lease { get; }

        public Guid GenerationId { get; }

        public Guid ArtifactVersionId { get; }

        public SeededArtifact Artifact { get; }

        public EfCaseReportDeliveryPreparationStore Store { get; }

        public static async Task<Harness> CreateAsync()
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            try
            {
                var options = new DbContextOptionsBuilder<PegasusDbContext>()
                    .UseSqlServer(database.ConnectionString)
                    .Options;
                var factory = new PooledDbContextFactory<PegasusDbContext>(options);
                var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
                var caseId = await SeedCaseAsync(factory);
                var artifact = await SeedConfirmedGenerationAsync(factory, caseId);
                var lease = await new AcquireCaseEditLease(
                        new EfCaseWorkflowStore(factory, new FixedTimeProvider(StartUtc)))
                    .ExecuteAsync(new(caseId, 1, staff, "lease-prepare-delivery"), CancellationToken.None);
                return new(
                    database, factory, caseId, staff, lease, artifact.GenerationId, artifact.VersionId, artifact);
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public PrepareCaseReportDeliveryCommand PrepareCommand(
            long expectedCaseVersion = 1,
            long expectedGenerationVersion = 1,
            string leaseToken = "lease") => new(
            new(
                Staff,
                CaseId,
                expectedCaseVersion,
                leaseToken == "lease" ? Lease.Token : leaseToken,
                GenerationId,
                expectedGenerationVersion,
                OperationKey),
            new([new("handler@principal.example", "Principal Handler")], [], "DVR-31001"));

        public ReportSendReadinessRequest ReadyRequest(CaseReportDeliveryPreparationRecord record) => new(
            Staff,
            CaseId,
            record.FrozenCaseVersion,
            record.Preparation.GenerationId,
            record.Preparation.GenerationVersion,
            record.Preparation.Id,
            record.Preparation.Version,
            record.Preparation.Artifacts);

        public Task<long> IntentCountAsync() => database.ScalarAsync<long>(
            $"SELECT COUNT_BIG(*) FROM CaseReportDeliveryIntents");

        public Task<long> ActionHistoryCountAsync() => database.ScalarAsync<long>(
            $"SELECT COUNT(*) FROM ActionHistory WHERE EventKind = 'case_report_delivery_prepared' AND CorrelationId = '{OperationKey}'");

        public async Task SetGenerationStateAsync(string state)
        {
            await database.ExecuteAsync(
                $"UPDATE CaseReportGenerations SET State = '{state}' WHERE Id = '{GenerationId:D}'");
        }

        public async Task SetGenerationVersionAsync(long version)
        {
            await database.ExecuteAsync(
                $"UPDATE CaseReportGenerations SET [Version] = {version} WHERE Id = '{GenerationId:D}'");
        }

        public async Task SetArtifactStateAsync(string state)
        {
            await database.ExecuteAsync(
                $"UPDATE GeneratedCaseArtifacts SET State = '{state}'");
        }

        public async Task SupersedeGenerationAsync()
        {
            await database.ExecuteAsync(
                $"UPDATE CaseReportGenerations SET SupersededById = '{Guid.NewGuid():D}' WHERE Id = '{GenerationId:D}'");
        }

        /// <summary>
        /// Changes the confirmed version row's content length underneath the
        /// pinned attachment: the send boundary must refuse bytes that no
        /// longer match what was prepared.
        /// </summary>
        public Task TamperArtifactVersionAsync() => database.ExecuteAsync(
            $"UPDATE DocumentVersions SET ContentLength = 999 WHERE Id = '{ArtifactVersionId:D}'");

        /// <summary>A Case mutation after preparation: the live version moves.</summary>
        public Task MoveCaseVersionAsync(long version) => database.ExecuteAsync(
            $"UPDATE CaseWorkflows SET [Version] = {version} WHERE CaseId = '{CaseId:D}'");

        public async ValueTask DisposeAsync() => await database.DisposeAsync();

        private static async Task<Guid> SeedCaseAsync(
            PooledDbContextFactory<PegasusDbContext> factory)
        {
            await using var context = await factory.CreateDbContextAsync();
            var organizationId = Guid.NewGuid();
            var lineageId = Guid.NewGuid();
            var principalId = Guid.NewGuid();
            var receiptId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            context.AddRange(
                new OrganizationEntity { Id = organizationId, Name = "Delivery preparation test", Version = 0 },
                new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = StartUtc },
                new PrincipalEntity
                {
                    Id = principalId,
                    OrganizationId = organizationId,
                    SequenceLineageId = lineageId,
                    Code = "DVRP",
                    IsActive = true,
                    Version = 0
                },
                new IntakeReceiptEntity
                {
                    Id = receiptId,
                    SourceFileName = "delivery-origin.pdf",
                    MediaType = "application/pdf",
                    SourceLength = 1,
                    SourceHash = new string('0', 64),
                    SourceChannel = "manual_upload",
                    ExternalReceiptToken = $"prepare:{receiptId:N}",
                    ReceivedAtUtc = StartUtc,
                    ProcessedAtUtc = StartUtc,
                    SourceReaderKey = "prepare-test",
                    SourceReaderVersion = "1",
                    Version = 0,
                    Decision = "case_created",
                    DecisionReason = "Delivery preparation test",
                    EvidenceJson = "[]",
                    FieldsJson = "[]",
                    OcrCandidatesJson = "[]"
                },
                new CaseEntity
                {
                    Id = caseId,
                    PrincipalId = principalId,
                    SequenceLineageId = lineageId,
                    Year = 2031,
                    Sequence = 1,
                    Reference = "DVR-31001",
                    Type = "Inspection",
                    InitialState = "NotReady",
                    CustodyState = "confirmed",
                    OriginIntakeReceiptId = receiptId,
                    CreatedAtUtc = StartUtc,
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                },
                new CaseWorkflowEntity
                {
                    CaseId = caseId,
                    State = "ReportPreparation",
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                });
            await context.SaveChangesAsync();
            return caseId;
        }

        private static async Task<SeededArtifact> SeedConfirmedGenerationAsync(
            PooledDbContextFactory<PegasusDbContext> factory,
            Guid caseId)
        {
            await using var context = await factory.CreateDbContextAsync();
            var generationId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var occurrenceId = Guid.NewGuid();
            var content = "delivery-preparation"u8.ToArray();
            var sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content));
            context.AddRange(
                new CaseDocumentEntity
                {
                    Id = documentId,
                    CaseId = caseId,
                    Ordinal = 1,
                    SourceOccurrenceIdentity = $"prepare:{documentId:N}"
                },
                new DocumentVersionEntity
                {
                    Id = versionId,
                    DocumentId = documentId,
                    Version = 1,
                    FileName = "report.pdf",
                    MediaType = "application/pdf",
                    ContentLength = content.LongLength,
                    Sha256 = sha256,
                    CustodyStatus = DocumentCustodyStatus.Confirmed,
                    CreatedAtUtc = StartUtc,
                    CreatedBy = "Staff:test",
                    IsCurrent = true
                },
                new DocumentOccurrenceEntity
                {
                    Id = occurrenceId,
                    CaseId = caseId,
                    DocumentId = documentId,
                    VersionId = versionId,
                    SemanticRole = DocumentSemanticRole.EngineerReport,
                    Source = DocumentSource.Generated,
                    SourceOccurrenceIdentity = $"prepare:{occurrenceId:N}",
                    RecordedAtUtc = StartUtc,
                    OperationKey = $"seed:{occurrenceId:N}"
                },
                new CaseReportGenerationEntity
                {
                    Id = generationId,
                    CaseId = caseId,
                    CaseVersion = 1,
                    SnapshotHash = new string('1', 64),
                    SnapshotJson = "{}",
                    TemplateVersion = "assessment-report/v1",
                    RendererVersion = "playwright/v1",
                    State = nameof(CaseReportGenerationState.Confirmed),
                    GeneratedAtUtc = StartUtc,
                    Version = 1
                },
                new GeneratedCaseArtifactEntity
                {
                    Id = Guid.NewGuid(),
                    GenerationId = generationId,
                    VersionId = versionId,
                    Kind = nameof(CaseReportArtifactKind.AssessmentReport),
                    Sha256 = sha256,
                    State = nameof(CaseReportArtifactStatus.Confirmed),
                    OperationKey = "artifact-1"
                });
            await context.SaveChangesAsync();
            return new(generationId, versionId, documentId, sha256, content.LongLength);
        }

        internal sealed record SeededArtifact(
            Guid GenerationId, Guid VersionId, Guid DocumentId, string Sha256, long ContentLength);

        private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => utcNow;
        }
    }
}
