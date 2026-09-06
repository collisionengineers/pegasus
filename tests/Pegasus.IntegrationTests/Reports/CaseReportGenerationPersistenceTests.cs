using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Assessment;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// The durable half of B05: the freeze/confirm two-transaction shape, the
/// retained Pending/Failed/Unknown custody outcomes and their restart-safe
/// retry, the stale rule, and the A06 report-ready reader contract — all
/// against a real database, with fakes only at the custody and rendering
/// boundaries Stream A owns.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseReportGenerationPersistenceTests
{
    [Fact]
    public async Task FreezeCommitsBeforeRenderingAndConfirmationIsASecondTransaction()
    {
        await using var harness = await Harness.CreateAsync();
        var custody = new RecordingCustody(harness);
        var renderer = new RecordingRenderer(harness);
        // The freeze must be committed and lock-free by the time Chromium and
        // Box would run: the renderer reads the artifact row on its own
        // connection, which would block if the freeze still held its lock.
        renderer.Before = async () =>
        {
            var frozen = Assert.Single(await harness.ArtifactRowsAsync());
            Assert.Equal(nameof(CaseReportArtifactStatus.Pending), frozen.State);
            Assert.Equal(1, await harness.ActionHistoryCountAsync("case_report_generation_frozen"));
            Assert.Equal(0, await harness.ActionHistoryCountAsync("case_report_generation_ready"));
        };

        var result = await harness.Generate(custody, renderer)
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, result.Outcome);
        var generation = Assert.IsType<CaseReportGenerationRecord>(result.Generation);
        Assert.Equal(CaseReportGenerationState.Confirmed, generation.State);
        var artifact = Assert.Single(generation.Artifacts);
        Assert.Equal(CaseReportArtifactStatus.Confirmed, artifact.Status);
        Assert.Equal(renderer.Sha256, artifact.Sha256);
        Assert.Equal(Harness.OperationKey, artifact.OperationKey);
        Assert.Equal($"case-report:{generation.Id:D}:AssessmentReport", Assert.Single(custody.OccurrenceIdentities));
        Assert.Equal(Harness.OperationKey, Assert.Single(custody.OperationKeys));

        // The frozen snapshot pins Box identity alongside document identity
        // and hash for every prepared image and accepted source.
        var image = Assert.Single(
            generation.Snapshot.Images, item => item.Role == CaseAssetReportRole.CloseUp);
        Assert.Equal(harness.CloseUp.DocumentId, image.DocumentId);
        Assert.Equal(harness.CloseUp.VersionId, image.VersionId);
        Assert.Equal($"box-file-{harness.CloseUp.VersionId:N}", image.BoxFileId);
        Assert.Equal($"box-version-{harness.CloseUp.VersionId:N}", image.BoxVersionId);
        var source = Assert.Single(generation.Snapshot.Sources);
        Assert.Equal(harness.Source.DocumentId, source.DocumentId);
        Assert.Equal($"box-file-{harness.Source.VersionId:N}", source.BoxFileId);
        Assert.Equal($"box-version-{harness.Source.VersionId:N}", source.BoxVersionId);

        // The confirmed artifact carries the custody object A actually wrote.
        Assert.Equal(custody.VersionId, artifact.VersionId);
        Assert.Equal(custody.DocumentId, artifact.DocumentId);
        Assert.Equal($"box-file-{custody.VersionId:N}", artifact.BoxFileId);
        Assert.Null(artifact.PendingContentStorageKey);

        Assert.Equal(["freeze", "render", "retain", "confirm"], harness.Sequence);
        Assert.Equal(1, await harness.ActionHistoryCountAsync("case_report_artifact_confirmed"));
    }

    /// <summary>
    /// B09 review (lifecycle): a stale current generation is history, not a
    /// deliverable snapshot. Regenerating the same material after a stale
    /// must create a NEW current generation - never reuse the stale row and
    /// report it as already generated while it stays undeliverable.
    /// </summary>
    [Fact]
    public async Task RegeneratingAfterAStaleCreatesANewCurrentGeneration()
    {
        await using var harness = await Harness.CreateAsync();
        var custody = new RecordingCustody(harness);
        var first = await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var firstGeneration = Assert.IsType<CaseReportGenerationRecord>(first.Generation);
        Assert.Equal(CaseReportGenerationState.Confirmed, firstGeneration.State);

        await harness.Store.MarkStaleAsync(harness.CaseId, "test-material-change", CancellationToken.None);

        // A new operation key is a new generation request; the material is
        // unchanged, so the hash matches the stale generation anyway.
        var second = await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(operationKey: "case-report-regenerate"), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, second.Outcome);
        var secondGeneration = Assert.IsType<CaseReportGenerationRecord>(second.Generation);
        Assert.Equal(CaseReportGenerationState.Confirmed, secondGeneration.State);
        // The claim is unchanged material: the same hash, a different row.
        Assert.Equal(firstGeneration.SnapshotHash, secondGeneration.SnapshotHash);
        Assert.NotEqual(firstGeneration.Id, secondGeneration.Id);

        var rows = await harness.GenerationRowsAsync();
        var stale = Assert.Single(rows, row => row.Id == firstGeneration.Id);
        Assert.Equal(CaseReportGenerationState.Stale, stale.State);
        Assert.Equal(secondGeneration.Id, stale.SupersededById);
    }

    /// <summary>
    /// The uniqueness the schema keeps after G16 relaxes it for Stale rows:
    /// one Case never holds two live generations of the same material. The
    /// second row is written past the store on purpose, because the store's
    /// own lookup would have reused the first.
    /// </summary>
    [Fact]
    public async Task TwoLiveGenerationsOfTheSameMaterialAreStillRejected()
    {
        await using var harness = await Harness.CreateAsync();
        var first = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var live = Assert.IsType<CaseReportGenerationRecord>(first.Generation);
        Assert.Equal(CaseReportGenerationState.Confirmed, live.State);

        await using var context = await harness.Factory.CreateDbContextAsync();
        context.Set<CaseReportGenerationEntity>().Add(new CaseReportGenerationEntity
        {
            Id = Guid.NewGuid(),
            CaseId = harness.CaseId,
            CaseVersion = live.CaseVersion,
            SnapshotHash = live.SnapshotHash,
            SnapshotJson = "{}",
            TemplateVersion = live.TemplateVersion,
            RendererVersion = live.RendererVersion,
            State = nameof(CaseReportGenerationState.Pending),
            GeneratedAtUtc = Harness.StartUtc,
            Version = 1,
        });

        var refused = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Contains(
            "IX_CaseReportGenerations_CaseId_SnapshotHash",
            refused.InnerException?.Message,
            StringComparison.Ordinal);
        Assert.Single(await harness.GenerationRowsAsync());
    }

    [Fact]
    public async Task APendingArtifactIsConfirmedFromTheCustodyStatusQueryAfterARestart()
    {
        await using var harness = await Harness.CreateAsync();
        var custody = new RecordingCustody(harness)
        {
            Disposition = CaseArtifactCustodyDisposition.Pending
        };
        var pending = await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Pending, pending.Outcome);
        var pendingArtifact = Assert.Single(pending.Generation!.Artifacts);
        Assert.Equal(CaseReportArtifactStatus.Pending, pendingArtifact.Status);
        // G7 relaxed the custody check constraint so a Pending row keeps the
        // logical identities a restart-safe retry needs.
        Assert.Equal(custody.VersionId, pendingArtifact.VersionId);
        Assert.Equal(custody.DocumentId, pendingArtifact.DocumentId);
        Assert.Equal($"pending/{custody.VersionId:N}", pendingArtifact.PendingContentStorageKey);
        Assert.Equal(0, await harness.ActionHistoryCountAsync("case_report_generation_ready"));

        // Restart: custody settled the object out of process, so the retry
        // asks it what happened instead of rendering the same bytes again.
        await harness.ConfirmCustodyObjectAsync(custody.VersionId!.Value);
        var status = new RecordingCustodyStatus
        {
            Result = new(
                CaseArtifactCustodyDisposition.Confirmed, custody.DocumentId, custody.VersionId,
                $"box-file-{custody.VersionId:N}", $"box-version-{custody.VersionId:N}",
                custody.Sha256, custody.ContentLength, "application/pdf", null, null)
        };
        var refusing = new RefusingRenderer();
        harness.Sequence.Clear();

        var confirmed = await harness.Generate(new RecordingCustody(harness), refusing, status)
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, confirmed.Outcome);
        Assert.Equal(pending.Generation.Id, confirmed.Generation!.Id);
        var settled = Assert.Single(confirmed.Generation.Artifacts);
        Assert.Equal(pendingArtifact.Id, settled.Id);
        Assert.Equal(CaseReportArtifactStatus.Confirmed, settled.Status);
        Assert.Equal(custody.Sha256, settled.Sha256);
        Assert.Equal(
            (harness.CaseId, custody.DocumentId!.Value, custody.VersionId!.Value), status.LastQuery);
        Assert.Equal(["freeze", "confirm"], harness.Sequence);
        Assert.Equal(1, await harness.ActionHistoryCountAsync("case_report_generation_ready"));
    }

    [Fact]
    public async Task AFailedArtifactIsRetriedWithTheSameSnapshotOperationKeyAndArtifactRow()
    {
        await using var harness = await Harness.CreateAsync();
        var failing = new RecordingCustody(harness)
        {
            Disposition = CaseArtifactCustodyDisposition.Failed,
            FailureCode = "box_upload_rejected"
        };

        var failed = await harness.Generate(failing, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Failed, failed.Outcome);
        var failedArtifact = Assert.Single(failed.Generation!.Artifacts);
        Assert.Equal(CaseReportArtifactStatus.Failed, failedArtifact.Status);
        Assert.Equal("box_upload_rejected", failedArtifact.FailureCode);
        Assert.Null(failedArtifact.Sha256);
        Assert.Equal(0, await harness.ActionHistoryCountAsync("case_report_generation_ready"));

        var custody = new RecordingCustody(harness);
        harness.Sequence.Clear();
        var retried = await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, retried.Outcome);
        // Same generation, same frozen snapshot, same artifact row and the
        // same operation key: a retry is never a second identity.
        Assert.Equal(failed.Generation.Id, retried.Generation!.Id);
        Assert.Equal(failed.Generation.SnapshotHash, retried.Generation.SnapshotHash);
        var retriedArtifact = Assert.Single(retried.Generation.Artifacts);
        Assert.Equal(failedArtifact.Id, retriedArtifact.Id);
        Assert.Equal(Harness.OperationKey, Assert.Single(custody.OperationKeys));
        Assert.Equal(CaseReportArtifactStatus.Confirmed, retriedArtifact.Status);
        Assert.Null(retriedArtifact.FailureCode);
        Assert.Equal(["freeze", "render", "retain", "confirm"], harness.Sequence);
        Assert.Single(await harness.GenerationRowsAsync());
    }

    [Fact]
    public async Task AnUnknownCustodyOutcomeIsRetainedRatherThanRetriedAway()
    {
        await using var harness = await Harness.CreateAsync();
        var custody = new RecordingCustody(harness)
        {
            Disposition = CaseArtifactCustodyDisposition.Unknown
        };

        var unknown = await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Pending, unknown.Outcome);
        var artifact = Assert.Single(unknown.Generation!.Artifacts);
        Assert.Equal(CaseReportArtifactStatus.Unknown, artifact.Status);
        Assert.Equal(custody.VersionId, artifact.VersionId);
        Assert.Equal($"pending/{custody.VersionId:N}", artifact.PendingContentStorageKey);
        Assert.Equal(CaseReportGenerationState.Pending, unknown.Generation.State);
        Assert.Equal(1, custody.Calls);

        // The unresolved outcome stays exactly as recorded until something
        // asks again; nothing retries it in the background.
        var current = await harness.Store.GetCurrentAsync(
            harness.StaffActor, harness.CaseId, CancellationToken.None);
        Assert.Equal(
            CaseReportArtifactStatus.Unknown, Assert.Single(current!.Artifacts).Status);
        Assert.Equal(0, await harness.ActionHistoryCountAsync("case_report_generation_ready"));
        Assert.Equal(1, await harness.ActionHistoryCountAsync("case_report_artifact_outcome_recorded"));
    }

    [Fact]
    public async Task TheReadyEventIsWrittenExactlyOnceAndCarriesTheA06ReaderContract()
    {
        await using var harness = await Harness.CreateAsync();

        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        Assert.Equal(CaseReportGenerationOutcome.Generated, report.Outcome);

        var ready = Assert.Single(await harness.ReadyEventsAsync());
        Assert.Equal("case_report_generation_ready", ready.EventKind);
        Assert.Equal("Succeeded", ready.Outcome);
        Assert.Equal("case", ready.AggregateType);
        Assert.Equal(harness.CaseId.ToString("D"), ready.AggregateId);
        Assert.Equal(report.Generation!.Snapshot.OperationKey, ready.CorrelationId);
        Assert.Equal(Harness.OperationKey, ready.CorrelationId);
        Assert.Equal(nameof(ActorKind.Staff), ready.ActorKind);
        Assert.Equal(harness.StaffActor.SubjectId, ready.ActorSubjectId);
        Assert.Equal(Harness.StartUtc, ready.OccurredAtUtc);

        using var after = JsonDocument.Parse(ready.AfterJson!);
        Assert.Equal(JsonValueKind.Object, after.RootElement.ValueKind);
        var generationId = after.RootElement.GetProperty("generationId");
        Assert.Equal(JsonValueKind.String, generationId.ValueKind);
        Assert.Equal(report.Generation.Id, Guid.Parse(generationId.GetString()!));

        // Asking the same snapshot for its fee note adds a second artifact and
        // confirms it; the report-ready transition is not announced twice.
        var feeNote = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(
                harness.Request(CaseReportArtifactKind.FeeNote, "case-report-fee-1"),
                CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, feeNote.Outcome);
        Assert.Equal(report.Generation.Id, feeNote.Generation!.Id);
        Assert.Equal(2, feeNote.Generation.Artifacts.Count);
        Assert.All(
            feeNote.Generation.Artifacts,
            item => Assert.Equal(CaseReportArtifactStatus.Confirmed, item.Status));
        Assert.True(feeNote.Generation.IsFullyConfirmed);
        Assert.Single(await harness.ReadyEventsAsync());
    }

    [Fact]
    public async Task CustodyConfirmationAloneIsNeverTheReadyTransition()
    {
        await using var harness = await Harness.CreateAsync();
        var custody = new RecordingCustody(harness)
        {
            // The object lands in custody as Confirmed, but the second
            // transaction never runs: the generation is not ready.
            Disposition = CaseArtifactCustodyDisposition.Pending,
            ConfirmedInCustody = true
        };

        await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(
            DocumentCustodyStatus.Confirmed,
            await harness.CustodyStatusAsync(custody.VersionId!.Value));
        Assert.Empty(await harness.ReadyEventsAsync());
        var artifact = Assert.Single((await harness.GenerationRowsAsync()).Single().Artifacts);
        Assert.Equal(CaseReportArtifactStatus.Pending, artifact.Status);
    }

    [Fact]
    public async Task AMaterialChangeBetweenFreezeAndConfirmLeavesTheGenerationConfirmedButStale()
    {
        await using var harness = await Harness.CreateAsync();
        var custody = new RecordingCustody(harness);
        var renderer = new RecordingRenderer(harness);
        renderer.Before = async () => await harness.Store.MarkStaleAsync(
            harness.CaseId, CaseReportStaleReasons.EstimateChanged, CancellationToken.None);

        var result = await harness.Generate(custody, renderer)
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, result.Outcome);
        var artifact = Assert.Single(result.Generation!.Artifacts);
        Assert.Equal(CaseReportArtifactStatus.Confirmed, artifact.Status);
        // Rendering finishing later never makes a staled generation current.
        Assert.Equal(CaseReportGenerationState.Stale, result.Generation.State);
        Assert.Empty(await harness.ReadyEventsAsync());
        Assert.Equal(1, await harness.ActionHistoryCountAsync("case_report_generation_stale"));

        // A second stale marking of an already stale generation is a no-op.
        Assert.Equal(
            0,
            await harness.Store.MarkStaleAsync(
                harness.CaseId, CaseReportStaleReasons.EstimateChanged, CancellationToken.None));
    }

    [Fact]
    public async Task RegeneratingAfterAMaterialChangeNeverRewritesThePriorGeneration()
    {
        await using var harness = await Harness.CreateAsync();
        var first = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var firstArtifact = Assert.Single(first.Generation!.Artifacts);

        await harness.Store.MarkStaleAsync(
            harness.CaseId, CaseReportStaleReasons.ValuationChanged, CancellationToken.None);
        harness.AcceptEngineerValue(5_250m);

        var second = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(
                harness.Request(CaseReportArtifactKind.AssessmentReport, "case-report-2"),
                CancellationToken.None);

        Assert.NotEqual(first.Generation.Id, second.Generation!.Id);
        Assert.NotEqual(first.Generation.SnapshotHash, second.Generation.SnapshotHash);
        Assert.Equal(5_250m, second.Generation.Snapshot.AcceptedEngineerValue);
        Assert.Equal(CaseReportGenerationState.Confirmed, second.Generation.State);

        // The prior generation keeps its bytes, its confirmed artifact and its
        // history exactly as issued; only its currency changed.
        var prior = await harness.Store.GetAsync(
            harness.StaffActor, harness.CaseId, first.Generation.Id, CancellationToken.None);
        Assert.Equal(CaseReportGenerationState.Stale, prior!.State);
        Assert.Equal(second.Generation.Id, prior.SupersededById);
        Assert.Equal(5_000m, prior.Snapshot.AcceptedEngineerValue);
        var priorArtifact = Assert.Single(prior.Artifacts);
        Assert.Equal(firstArtifact.Id, priorArtifact.Id);
        Assert.Equal(firstArtifact.VersionId, priorArtifact.VersionId);
        Assert.Equal(firstArtifact.Sha256, priorArtifact.Sha256);
        Assert.Equal(CaseReportArtifactStatus.Confirmed, priorArtifact.Status);

        var current = await harness.Store.GetCurrentAsync(
            harness.StaffActor, harness.CaseId, CancellationToken.None);
        Assert.Equal(second.Generation.Id, current!.Id);
        Assert.Equal(2, (await harness.Store.ListAsync(
            harness.StaffActor, harness.CaseId, CancellationToken.None)).Count);
        Assert.Equal(2, (await harness.ReadyEventsAsync()).Count);
    }

    [Fact]
    public async Task ReopeningAGeneratedArtifactReturnsTheConfirmedImmutableBytes()
    {
        await using var harness = await Harness.CreateAsync();
        var renderer = new RecordingRenderer(harness);
        var generated = await harness.Generate(new RecordingCustody(harness), renderer)
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var artifact = Assert.Single(generated.Generation!.Artifacts);

        await using var content = await harness.Store.OpenAsync(
            harness.StaffActor, harness.CaseId, generated.Generation.Id, artifact.Id,
            CancellationToken.None);
        using var buffer = new MemoryStream();
        await content.Content.CopyToAsync(buffer);

        Assert.Equal(renderer.Pdf, buffer.ToArray());
        Assert.Equal(renderer.Sha256, content.Sha256);
        Assert.Equal(artifact.VersionId, content.VersionId);

        // A generation the operator never generated has nothing to reopen.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Store.OpenAsync(
                harness.StaffActor, harness.CaseId, generated.Generation.Id, Guid.NewGuid(),
                CancellationToken.None));
    }

    private sealed class Harness : IAsyncDisposable
    {
        internal const string OperationKey = "case-report-1";
        internal static readonly DateTimeOffset StartUtc = new(2026, 9, 6, 10, 0, 0, TimeSpan.Zero);

        private readonly LocalDbTestDatabase database;
        private readonly FakeSnapshotSource snapshotSource;

        private Harness(
            LocalDbTestDatabase database,
            PooledDbContextFactory<PegasusDbContext> factory,
            Guid caseId,
            ActionActor staffActor,
            CaseEditLease lease,
            FakeSnapshotSource snapshotSource,
            SeededDocument closeUp,
            SeededDocument overview,
            SeededDocument source)
        {
            this.database = database;
            this.snapshotSource = snapshotSource;
            Factory = factory;
            CaseId = caseId;
            StaffActor = staffActor;
            Lease = lease;
            CloseUp = closeUp;
            Overview = overview;
            Source = source;
            var store = new EfCaseReportGenerationStore(
                factory, snapshotSource, new FakeDocumentReader(this), new FixedTimeProvider(StartUtc));
            Store = store;
        }

        public PooledDbContextFactory<PegasusDbContext> Factory { get; }

        public Guid CaseId { get; }

        public ActionActor StaffActor { get; }

        public CaseEditLease Lease { get; }

        public SeededDocument CloseUp { get; }

        public SeededDocument Overview { get; }

        public SeededDocument Source { get; }

        public EfCaseReportGenerationStore Store { get; }

        public List<string> Sequence { get; } = [];

        /// <summary>The bytes each fake custody object retained, by version.</summary>
        public Dictionary<Guid, byte[]> RetainedContent { get; } = [];

        /// <summary>The seeded evidence bytes a frozen image hash pins.</summary>
        public Dictionary<string, byte[]> EvidenceContent { get; } =
            new(StringComparer.Ordinal);

        public static async Task<Harness> CreateAsync()
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            try
            {
                var options = new DbContextOptionsBuilder<PegasusDbContext>()
                    .UseSqlServer(database.ConnectionString)
                    .Options;
                var factory = new PooledDbContextFactory<PegasusDbContext>(options);
                var staffActor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
                var caseId = await SeedCaseAsync(factory);
                var closeUp = await SeedDocumentAsync(factory, caseId, "close-up.png", "image/png", 1);
                var overview = await SeedDocumentAsync(factory, caseId, "overview.png", "image/png", 2);
                var source = await SeedDocumentAsync(
                    factory, caseId, "instruction.pdf", "application/pdf", 3);
                var lease = await new AcquireCaseEditLease(
                        new EfCaseWorkflowStore(factory, new FixedTimeProvider(StartUtc)))
                    .ExecuteAsync(new(caseId, 1, staffActor, "lease-report"), CancellationToken.None);
                var snapshotSource = new FakeSnapshotSource(caseId, closeUp, overview, source);
                var harness = new Harness(
                    database, factory, caseId, staffActor, lease, snapshotSource,
                    closeUp, overview, source);
                foreach (var document in new[] { closeUp, overview, source })
                {
                    harness.EvidenceContent[document.Sha256] = document.Content;
                }

                return harness;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public GenerateCaseReport Generate(
            RecordingCustody custody,
            IAssessmentReportRenderer renderer,
            RecordingCustodyStatus? custodyStatus = null) => new(
                new RecordingStore(Store, Sequence),
                new FakeContentSource(this),
                renderer,
                custody,
                custodyStatus ?? new RecordingCustodyStatus(),
                new FixedTimeProvider(StartUtc));

        public GenerateCaseReportRequest Request(
            CaseReportArtifactKind kind = CaseReportArtifactKind.AssessmentReport,
            string operationKey = OperationKey) => new(
                StaffActor, CaseId, 1, Lease.Token, operationKey, kind,
                "Generate the immutable case report");

        /// <summary>Accepts a different Engineer's Value, a material change.</summary>
        public void AcceptEngineerValue(decimal value) => snapshotSource.AcceptEngineerValue(value);

        public static byte[] SignatureBytes => FakeSnapshotSource.SignatureBytes;

        public ReportImageEvidence[] RehydratedPhotos(
            CaseReportGenerationSnapshot snapshot) => snapshot.Images
                .Select(image => new ReportImageEvidence(
                    $"{image.OccurrenceId:D}.png", image.ContentType, EvidenceContent[image.Sha256],
                    image.Sha256, image.Role, image.Order, image.Rotation, image.Crop,
                    image.OccurrenceId, image.VersionId, image.BoxFileId, image.BoxVersionId))
                .ToArray();

        public async Task<SeededDocument> RetainArtifactAsync(
            CaseArtifactCustodyRequest request, byte[] content, bool confirmed)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var document = await context.Set<CaseDocumentEntity>().SingleOrDefaultAsync(
                item => item.CaseId == CaseId
                    && item.SourceOccurrenceIdentity == request.OccurrenceIdentity);
            if (document is null)
            {
                document = new CaseDocumentEntity
                {
                    Id = Guid.NewGuid(),
                    CaseId = CaseId,
                    Ordinal = 1 + await context.Set<CaseDocumentEntity>()
                        .Where(item => item.CaseId == CaseId)
                        .Select(item => (int?)item.Ordinal)
                        .MaxAsync() ?? 1,
                    SourceOccurrenceIdentity = request.OccurrenceIdentity
                };
                context.Add(document);
            }

            var version = await context.Set<DocumentVersionEntity>().SingleOrDefaultAsync(
                item => item.DocumentId == document.Id && item.Sha256 == request.Sha256);
            if (version is null)
            {
                version = new DocumentVersionEntity
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    Version = 1,
                    FileName = request.FileName,
                    MediaType = request.MediaType,
                    ContentLength = request.ContentLength,
                    Sha256 = request.Sha256,
                    CreatedAtUtc = StartUtc,
                    CreatedBy = $"Staff:{StaffActor.SubjectId}",
                    IsCurrent = true
                };
                context.Add(version);
            }

            Apply(version, confirmed);
            await context.SaveChangesAsync();
            RetainedContent[version.Id] = content;
            return new(document.Id, version.Id, request.Sha256, content);
        }

        public async Task ConfirmCustodyObjectAsync(Guid versionId)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var version = await context.Set<DocumentVersionEntity>().SingleAsync(
                item => item.Id == versionId);
            Apply(version, confirmed: true);
            await context.SaveChangesAsync();
        }

        public async Task<DocumentCustodyStatus> CustodyStatusAsync(Guid versionId)
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await context.Set<DocumentVersionEntity>().AsNoTracking()
                .Where(item => item.Id == versionId)
                .Select(item => item.CustodyStatus)
                .SingleAsync();
        }

        public async Task<IReadOnlyList<ArtifactRow>> ArtifactRowsAsync()
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await (
                from artifact in context.Set<GeneratedCaseArtifactEntity>().AsNoTracking()
                join generation in context.Set<CaseReportGenerationEntity>().AsNoTracking()
                    on artifact.GenerationId equals generation.Id
                where generation.CaseId == CaseId
                select new ArtifactRow(artifact.Id, artifact.Kind, artifact.State, artifact.OperationKey))
                .ToArrayAsync();
        }

        public async Task<IReadOnlyList<CaseReportGenerationRecord>> GenerationRowsAsync() =>
            await Store.ListAsync(StaffActor, CaseId, CancellationToken.None);

        public async Task<IReadOnlyList<ActionHistoryEntity>> ReadyEventsAsync()
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await context.ActionHistory.AsNoTracking()
                .Where(item => item.AggregateType == "case"
                    && item.AggregateId == CaseId.ToString("D")
                    && item.EventKind == "case_report_generation_ready")
                .ToArrayAsync();
        }

        public async Task<int> ActionHistoryCountAsync(string eventKind)
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await context.ActionHistory.AsNoTracking()
                .CountAsync(item => item.AggregateType == "case"
                    && item.AggregateId == CaseId.ToString("D")
                    && item.EventKind == eventKind);
        }

        public async ValueTask DisposeAsync() => await database.DisposeAsync();

        internal static byte[] EvidenceBytesOf(byte seed) => [137, 80, 78, 71, seed, 1, 2, 3];

        private static void Apply(DocumentVersionEntity version, bool confirmed)
        {
            version.CustodyStatus = confirmed
                ? DocumentCustodyStatus.Confirmed
                : DocumentCustodyStatus.Pending;
            version.BoxFileId = confirmed ? $"box-file-{version.Id:N}" : null;
            version.BoxVersionId = confirmed ? $"box-version-{version.Id:N}" : null;
            version.PendingContentStorageKey = confirmed ? null : $"pending/{version.Id:N}";
        }

        private static async Task<SeededDocument> SeedDocumentAsync(
            PooledDbContextFactory<PegasusDbContext> factory,
            Guid caseId,
            string fileName,
            string mediaType,
            byte seed)
        {
            var content = EvidenceBytesOf(seed);
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(content));
            await using var context = await factory.CreateDbContextAsync();
            var documentId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var occurrenceId = Guid.NewGuid();
            var ordinal = 1 + await context.Set<CaseDocumentEntity>()
                .Where(item => item.CaseId == caseId)
                .Select(item => (int?)item.Ordinal)
                .MaxAsync() ?? 1;
            var version = new DocumentVersionEntity
            {
                Id = versionId,
                DocumentId = documentId,
                Version = 1,
                FileName = fileName,
                MediaType = mediaType,
                ContentLength = content.LongLength,
                Sha256 = sha256,
                CreatedAtUtc = StartUtc,
                CreatedBy = "Staff:test",
                IsCurrent = true
            };
            Apply(version, confirmed: true);
            context.AddRange(
                new CaseDocumentEntity
                {
                    Id = documentId,
                    CaseId = caseId,
                    Ordinal = ordinal,
                    SourceOccurrenceIdentity = $"report-fixture:{occurrenceId:N}"
                },
                version,
                new DocumentOccurrenceEntity
                {
                    Id = occurrenceId,
                    CaseId = caseId,
                    DocumentId = documentId,
                    VersionId = versionId,
                    SemanticRole = mediaType == "application/pdf"
                        ? DocumentSemanticRole.Instruction
                        : DocumentSemanticRole.Image,
                    Source = DocumentSource.StaffUpload,
                    SourceOccurrenceIdentity = $"report-fixture:{occurrenceId:N}",
                    RecordedAtUtc = StartUtc,
                    OperationKey = $"seed:{occurrenceId:N}",
                    PreparationRole = nameof(CaseAssetReportRole.NotUsed)
                });
            await context.SaveChangesAsync();
            return new(documentId, versionId, sha256, content) { OccurrenceId = occurrenceId };
        }

        private static async Task<Guid> SeedCaseAsync(PooledDbContextFactory<PegasusDbContext> factory)
        {
            await using var context = await factory.CreateDbContextAsync();
            var organizationId = Guid.NewGuid();
            var lineageId = Guid.NewGuid();
            var principalId = Guid.NewGuid();
            var receiptId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            context.AddRange(
                new OrganizationEntity { Id = organizationId, Name = "Report generation test", Version = 0 },
                new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = StartUtc },
                new PrincipalEntity
                {
                    Id = principalId,
                    OrganizationId = organizationId,
                    SequenceLineageId = lineageId,
                    Code = "RPT31001",
                    IsActive = true,
                    Version = 0
                },
                new IntakeReceiptEntity
                {
                    Id = receiptId,
                    SourceFileName = "report-origin.pdf",
                    MediaType = "application/pdf",
                    SourceLength = 1,
                    SourceHash = new string('0', 64),
                    SourceChannel = "manual_upload",
                    ExternalReceiptToken = $"report:{receiptId:N}",
                    ReceivedAtUtc = StartUtc,
                    ProcessedAtUtc = StartUtc,
                    SourceReaderKey = "report-test",
                    SourceReaderVersion = "1",
                    Version = 0,
                    Decision = "case_created",
                    DecisionReason = "Report generation test",
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
                    Reference = "RPT31001",
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

        internal sealed record ArtifactRow(Guid Id, string Kind, string State, string OperationKey);

        internal sealed record SeededDocument(
            Guid DocumentId, Guid VersionId, string Sha256, byte[] Content)
        {
            public Guid OccurrenceId { get; init; }
        }

        private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => utcNow;
        }
    }

    /// <summary>
    /// The one read model a freeze loads, built from the same accepted
    /// fixtures the routed report tests use so readiness is genuinely met.
    /// </summary>
    private sealed class FakeSnapshotSource : ICaseReportSnapshotSource
    {
        internal static readonly byte[] SignatureBytes = [137, 80, 78, 71, 9, 9, 9, 9];

        private static readonly DateTimeOffset RecordedAtUtc = new(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);
        private static readonly Guid SignatoryId = Guid.NewGuid();

        private readonly Guid caseId;
        private readonly CaseAssessmentProjection assessment;
        private readonly AssessmentReportProjectionInput projection;
        private readonly Harness.SeededDocument closeUp;
        private readonly Harness.SeededDocument overview;
        private readonly RepairSpecificationVersion estimate =
            AssessmentReportDraftWebTests.CurrentEstimate();
        private readonly Guid valuationId = Guid.NewGuid();
        private readonly Guid guideValuationId = Guid.NewGuid();
        private decimal engineerValue = 5_000m;

        public FakeSnapshotSource(
            Guid caseId,
            Harness.SeededDocument closeUp,
            Harness.SeededDocument overview,
            Harness.SeededDocument source)
        {
            this.caseId = caseId;
            this.closeUp = closeUp;
            this.overview = overview;
            assessment = AssessmentReportDraftWebTests.FullAssessmentProjection(caseId);
            projection = AssessmentReportDraftWebTests.ReadyInput(caseId) with
            {
                Assessment = assessment,
                ReportDate = null,
                CurrentEstimate = estimate,
                Signatory = new ReportSignatory("Ed Mawdsley", "ATA VDA AQP", SignatureBytes, "image/png"),
                Photos =
                [
                    Photo(closeUp, CaseAssetReportRole.CloseUp),
                    Photo(overview, CaseAssetReportRole.Overview),
                ],
                Sources =
                [
                    new AcceptedReportSource(
                        "instruction.pdf", "1", source.Sha256, source.DocumentId, source.VersionId,
                        $"box-file-{source.VersionId:N}", $"box-version-{source.VersionId:N}"),
                ],
            };
        }

        public void AcceptEngineerValue(decimal value) => engineerValue = value;

        public Task<CaseReportFreezeInputs?> GetAsync(
            Guid requestedCaseId, ActionActor actor, CancellationToken cancellationToken) =>
            Task.FromResult<CaseReportFreezeInputs?>(
                requestedCaseId == caseId
                    ? new(projection, Readiness(), "RPT31001", 1)
                    : null);

        private CaseReportReadinessInput Readiness() => new(
            assessment,
            SignatoryId,
            null,
            [new SignOffEngineerProfile(
                SignatoryId, "Ed Mawdsley", "ATA VDA AQP", SignatureBytes, "image/png", IsDefault: true)],
            estimate,
            Valuation(),
            [Preparation(closeUp, CaseAssetReportRole.CloseUp), Preparation(overview, CaseAssetReportRole.Overview)],
            new Dictionary<Guid, DocumentVersion>
            {
                [closeUp.OccurrenceId] = Version(closeUp),
                [overview.OccurrenceId] = Version(overview),
            });

        private AppliedValuation Valuation() => new(
            valuationId, caseId, 1, guideValuationId, RecordedAtUtc,
            new ValuationCalculation(
                engineerValue, false, 0m, engineerValue, null, 0m, [], 0m, 0m, engineerValue),
            engineerValue, "engineer-1", RecordedAtUtc, "Accepted the guide value",
            "case-valuation-calculation/v1");

        private static ReportImageEvidence Photo(
            Harness.SeededDocument document, CaseAssetReportRole role) => new(
                $"{document.OccurrenceId:D}.png", "image/png", document.Content,
                document.Sha256, role, null, CaseAssetRotation.None, CaseAssetCrop.Full,
                document.OccurrenceId, document.VersionId,
                $"box-file-{document.VersionId:N}", $"box-version-{document.VersionId:N}");

        private CaseAssetPreparation Preparation(
            Harness.SeededDocument document, CaseAssetReportRole role) => new(
                caseId, document.OccurrenceId, document.DocumentId, document.VersionId, 1,
                document.Sha256, "image/png", role, null, CaseAssetRotation.None, CaseAssetCrop.Full,
                1, "engineer-1", RecordedAtUtc);

        private static DocumentVersion Version(Harness.SeededDocument document) => new(
            document.VersionId, document.DocumentId, 1, "photo.png", "image/png",
            document.Content.LongLength, document.Sha256,
            DocumentCustodyStatus.Confirmed, RecordedAtUtc, "engineer-1", true, false, null);
    }

    /// <summary>
    /// Rehydrates the frozen snapshot's pinned bytes exactly as
    /// <c>EfCaseReportContentSource</c> does, without a staff-account store.
    /// </summary>
    private sealed class FakeContentSource(CaseReportGenerationPersistenceTests.Harness harness)
        : ICaseReportContentSource
    {
        public Task<AssessmentReportSnapshot> ComposeAsync(
            CaseReportGenerationSnapshot snapshot, ActionActor actor, CancellationToken cancellationToken) =>
            Task.FromResult(snapshot.Report with
            {
                Photos = harness.RehydratedPhotos(snapshot),
                Signatory = snapshot.Report.Signatory with
                {
                    SignatureContent = Harness.SignatureBytes,
                    SignatureContentType = snapshot.SignatureContentType,
                },
            });
    }

    /// <summary>Records the store call order without changing any behaviour.</summary>
    private sealed class RecordingStore(EfCaseReportGenerationStore inner, List<string> sequence)
        : ICaseReportGenerationStore
    {
        public Task<CaseReportFreezeResult> FreezeAsync(
            FreezeCaseReportGenerationRequest request, CancellationToken cancellationToken)
        {
            sequence.Add("freeze");
            return inner.FreezeAsync(request, cancellationToken);
        }

        public Task<CaseReportGenerationRecord> ConfirmArtifactAsync(
            ConfirmCaseReportArtifactRequest request, CancellationToken cancellationToken)
        {
            sequence.Add("confirm");
            return inner.ConfirmArtifactAsync(request, cancellationToken);
        }

        public Task<CaseReportGenerationRecord> RecordArtifactOutcomeAsync(
            RecordCaseReportArtifactOutcomeRequest request, CancellationToken cancellationToken)
        {
            sequence.Add("record");
            return inner.RecordArtifactOutcomeAsync(request, cancellationToken);
        }

        public Task<CaseReportGenerationRecord?> GetAsync(
            ActionActor actor, Guid caseId, Guid generationId, CancellationToken cancellationToken) =>
            inner.GetAsync(actor, caseId, generationId, cancellationToken);

        public Task<CaseReportGenerationRecord?> GetCurrentAsync(
            ActionActor actor, Guid caseId, CancellationToken cancellationToken) =>
            inner.GetCurrentAsync(actor, caseId, cancellationToken);

        public Task<IReadOnlyList<CaseReportGenerationRecord>> ListAsync(
            ActionActor actor, Guid caseId, CancellationToken cancellationToken) =>
            inner.ListAsync(actor, caseId, cancellationToken);

        public Task<int> MarkStaleAsync(
            Guid caseId, string reasonCode, CancellationToken cancellationToken) =>
            inner.MarkStaleAsync(caseId, reasonCode, cancellationToken);
    }

    private sealed class RecordingRenderer(CaseReportGenerationPersistenceTests.Harness harness)
        : IAssessmentReportRenderer
    {
        public byte[] Pdf { get; } = [0x25, 0x50, 0x44, 0x46, 1, 2, 3];

        public string Sha256 => Convert.ToHexStringLower(SHA256.HashData(Pdf));

        public string EngineVersion => "Fake/1.0; Chromium";

        public List<CaseReportArtifactKind> Kinds { get; } = [];

        /// <summary>Runs after the freeze committed and before custody.</summary>
        public Func<Task>? Before { get; set; }

        public async Task<RenderedReportArtifact> RenderAsync(
            AssessmentReportSnapshot snapshot,
            CaseReportArtifactKind kind,
            CancellationToken cancellationToken = default)
        {
            Kinds.Add(kind);
            harness.Sequence.Add("render");
            if (Before is not null)
            {
                await Before();
            }

            return new($"{kind}.pdf", Pdf, 1, Sha256, AssessmentReportContract.TemplateVersion, EngineVersion);
        }
    }

    /// <summary>A renderer a restart-safe retry must never reach.</summary>
    private sealed class RefusingRenderer : IAssessmentReportRenderer
    {
        public string EngineVersion => "Fake/1.0; Chromium";

        public Task<RenderedReportArtifact> RenderAsync(
            AssessmentReportSnapshot snapshot,
            CaseReportArtifactKind kind,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "A retry that custody already settled must not render the same bytes again.");
    }

    private sealed class RecordingCustody(CaseReportGenerationPersistenceTests.Harness harness)
        : ICaseArtifactCustody
    {
        public CaseArtifactCustodyDisposition Disposition { get; init; } =
            CaseArtifactCustodyDisposition.Confirmed;

        public string? FailureCode { get; init; }

        /// <summary>
        /// Whether the durable object landed Confirmed even though the
        /// generation's second transaction has not recorded it.
        /// </summary>
        public bool ConfirmedInCustody { get; init; }

        public int Calls { get; private set; }

        public Guid? DocumentId { get; private set; }

        public Guid? VersionId { get; private set; }

        public string? Sha256 { get; private set; }

        public long ContentLength { get; private set; }

        public List<string> OperationKeys { get; } = [];

        public List<string> OccurrenceIdentities { get; } = [];

        public async Task<CaseArtifactCustodyResult> RetainAsync(
            CaseArtifactCustodyRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            Calls++;
            OperationKeys.Add(request.OperationKey);
            OccurrenceIdentities.Add(request.OccurrenceIdentity);
            harness.Sequence.Add("retain");
            using var buffer = new MemoryStream();
            await request.Content.CopyToAsync(buffer, cancellationToken);
            if (Disposition == CaseArtifactCustodyDisposition.Failed)
            {
                return new(
                    CaseArtifactCustodyDisposition.Failed, null, null, null, null, null, null, null,
                    FailureCode, null);
            }

            var confirmed = ConfirmedInCustody || Disposition == CaseArtifactCustodyDisposition.Confirmed;
            var retained = await harness.RetainArtifactAsync(request, buffer.ToArray(), confirmed);
            DocumentId = retained.DocumentId;
            VersionId = retained.VersionId;
            Sha256 = request.Sha256;
            ContentLength = request.ContentLength;
            return new(
                Disposition,
                retained.DocumentId,
                retained.VersionId,
                confirmed ? $"box-file-{retained.VersionId:N}" : null,
                confirmed ? $"box-version-{retained.VersionId:N}" : null,
                request.Sha256,
                request.ContentLength,
                request.MediaType,
                null,
                confirmed ? null : $"pending/{retained.VersionId:N}");
        }
    }

    private sealed class RecordingCustodyStatus : ICaseArtifactCustodyStatus
    {
        public (Guid CaseId, Guid DocumentId, Guid VersionId)? LastQuery { get; private set; }

        public CaseArtifactCustodyResult Result { get; init; } = new(
            CaseArtifactCustodyDisposition.Unknown, null, null, null, null, null, null, null, null, null);

        public Task<CaseArtifactCustodyResult> GetAsync(
            ActionActor actor, Guid caseId, Guid documentId, Guid versionId,
            CancellationToken cancellationToken)
        {
            LastQuery = (caseId, documentId, versionId);
            return Task.FromResult(Result);
        }

        /// <summary>
        /// G15: this fixture's scenarios never lose a retention response, so
        /// an operation-key lookup would be unexpected - fail loudly rather
        /// than invent a committed intent.
        /// </summary>
        public Task<CaseArtifactCustodyResult?> FindByOperationKeyAsync(
            ActionActor actor, Guid caseId, string operationKey,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "This fixture never loses a custody response; no operation-key lookup was modelled.");
    }

    /// <summary>Serves the bytes the fake custody retained, by exact hash.</summary>
    private sealed class FakeDocumentReader(CaseReportGenerationPersistenceTests.Harness harness)
        : IReadLogicalDocumentVersion
    {
        public Task<LogicalDocumentContent> OpenAsync(
            ReadLogicalDocumentVersionRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            var versionId = request.VersionId
                ?? throw new InvalidOperationException("A logical version identity is required.");
            if (!harness.RetainedContent.TryGetValue(versionId, out var content))
            {
                throw new InvalidOperationException($"Version '{versionId}' was never retained.");
            }

            if (!string.Equals(
                Convert.ToHexStringLower(SHA256.HashData(content)),
                request.ExpectedSha256,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The retained bytes do not match the expected hash.");
            }

            return Task.FromResult(new LogicalDocumentContent(
                new MemoryStream(content, writable: false), request.DocumentId, versionId, null,
                request.ExpectedSha256, content.LongLength, "artifact.pdf", "application/pdf"));
        }
    }
}
