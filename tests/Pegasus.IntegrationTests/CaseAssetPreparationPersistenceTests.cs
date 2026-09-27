using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using CaseDataHarness = Pegasus.IntegrationTests.CaseDataCompletenessPersistenceTests.CaseDataHarness;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Report image preparation as the Case's one Save records it: the workspace
/// save applies the preparation edit inside its own transaction, and the
/// preparation store reads it back.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseAssetPreparationPersistenceTests
{
    /// <summary>
    /// A new image is in the report (operator, 26 September 2026); the Case
    /// save records its order, rotation and crop without touching its bytes.
    /// </summary>
    [Fact]
    public async Task SavingRecordsOrderRotationAndCropWithoutTouchingBytes()
    {
        await using var harness = await Harness.CreateAsync();
        var first = await harness.SeedImageAsync(new string('a', 64));
        var second = await harness.SeedImageAsync(new string('b', 64));
        var third = await harness.SeedImageAsync(new string('c', 64));
        var lease = await harness.AcquireLeaseAsync();

        await harness.SavePreparationAsync(
            lease,
            "save-1",
            [
                new(first.OccurrenceId, 0, 1, CaseAssetRotation.None, CaseAssetCrop.Full),
                new(second.OccurrenceId, 0, 2, CaseAssetRotation.Clockwise90, CaseAssetCrop.Full),
                new(third.OccurrenceId, 0, 3, CaseAssetRotation.None, new(0.1m, 0.1m, 0.5m, 0.5m))
            ]);

        var result = await harness.Store.ListForCaseAsync(harness.CaseId, CancellationToken.None);
        Assert.Equal(3, result.Count);
        Assert.All(result, item => Assert.True(item.InReport));
        Assert.Equal(
            [first.OccurrenceId, second.OccurrenceId, third.OccurrenceId],
            result.OrderBy(item => item.Order).Select(item => item.OccurrenceId).ToArray());
        Assert.Equal(CaseAssetRotation.Clockwise90, result.Single(item => item.OccurrenceId == second.OccurrenceId).Rotation);
        Assert.Equal(new CaseAssetCrop(0.1m, 0.1m, 0.5m, 0.5m), result.Single(item => item.OccurrenceId == third.OccurrenceId).Crop);

        Assert.Equal(harness.CaseVersion + 1, await harness.CurrentCaseVersionAsync());

        // Bytes/hash are never touched by preparation.
        Assert.Equal(new string('a', 64), await harness.SourceSha256Async(first.VersionId));
        Assert.Equal(new string('b', 64), await harness.SourceSha256Async(second.VersionId));
        Assert.Equal(new string('c', 64), await harness.SourceSha256Async(third.VersionId));

        Assert.Equal(1, await harness.WorkflowEventCountAsync("case_workspace_saved"));
        Assert.Equal(1, await harness.ActionHistoryCountAsync("case_workspace_saved"));
        Assert.Equal(1, await harness.CaseHistoryCountAsync("case_workspace_saved"));
    }

    [Fact]
    public async Task GettingOnePreparationUsesTheCaseAndOccurrenceAndReturnsOneCurrentSnapshot()
    {
        await using var harness = await Harness.CreateAsync();
        var wanted = await harness.SeedImageAsync(new string('a', 64));
        await harness.SeedImageAsync(new string('b', 64));
        var otherCaseId = await harness.SeedAnotherCaseAsync();
        var otherCaseImage = await harness.SeedImageAsync(new string('c', 64), otherCaseId);
        var crop = new CaseAssetCrop(0.1m, 0.2m, 0.7m, 0.6m);
        var lease = await harness.AcquireLeaseAsync();
        await harness.SavePreparationAsync(
            lease,
            "save-single-snapshot",
            [new(
                wanted.OccurrenceId,
                0,
                null,
                CaseAssetRotation.Clockwise90,
                crop)]);

        var snapshot = await harness.Store.GetForOccurrenceAsync(
            harness.CaseId, wanted.OccurrenceId, CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(harness.CaseId, snapshot.CaseId);
        Assert.Equal(wanted.OccurrenceId, snapshot.OccurrenceId);
        Assert.Equal(wanted.VersionId, snapshot.VersionId);
        Assert.Equal(new string('a', 64), snapshot.SourceSha256);
        Assert.Equal(CaseAssetRotation.Clockwise90, snapshot.Rotation);
        Assert.Equal(crop, snapshot.Crop);
        Assert.Equal(1, snapshot.PreparationVersion);
        Assert.Null(await harness.Store.GetForOccurrenceAsync(
            harness.CaseId, otherCaseImage.OccurrenceId, CancellationToken.None));
        Assert.Null(await harness.Store.GetForOccurrenceAsync(
            otherCaseId, wanted.OccurrenceId, CancellationToken.None));
    }

    [Fact]
    public async Task ACrossCaseAssetIsRejected()
    {
        await using var harness = await Harness.CreateAsync();
        var otherCaseId = await harness.SeedAnotherCaseAsync();
        var foreignAsset = await harness.SeedImageAsync(new string('j', 64), caseId: otherCaseId);
        var lease = await harness.AcquireLeaseAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.SavePreparationAsync(
            lease,
            "save-cross-case",
            [new(foreignAsset.OccurrenceId, 0, null, CaseAssetRotation.None, CaseAssetCrop.Full)]));

        Assert.Equal(harness.CaseVersion, await harness.CurrentCaseVersionAsync());
    }

    [Fact]
    public async Task AStaleSupersededAssetVersionIsRejected()
    {
        await using var harness = await Harness.CreateAsync();
        var asset = await harness.SeedImageAsync(new string('k', 64));
        await harness.SupersedeImageAsync(asset.DocumentId, new string('l', 64));
        var lease = await harness.AcquireLeaseAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.SavePreparationAsync(
            lease,
            "save-stale-source",
            [new(asset.OccurrenceId, 0, null, CaseAssetRotation.Clockwise90, CaseAssetCrop.Full)]));

        Assert.Equal(harness.CaseVersion, await harness.CurrentCaseVersionAsync());
        Assert.Equal(0, (await harness.OccurrenceRowAsync(asset.OccurrenceId)).PreparationVersion);
        // The superseded version's own bytes/hash are untouched by the rejection.
        Assert.Equal(new string('k', 64), await harness.SourceSha256Async(asset.VersionId));
    }

    [Fact]
    public async Task APartialFailureAcrossMultipleEditedRowsCommitsNothing()
    {
        await using var harness = await Harness.CreateAsync();
        var first = await harness.SeedImageAsync(new string('m', 64));
        var second = await harness.SeedImageAsync(new string('n', 64));
        var initialLease = await harness.AcquireLeaseAsync();

        // Prepare the first occurrence so it carries a real, non-zero
        // PreparationVersion the second (failing) request can go stale against.
        await harness.SavePreparationAsync(
            initialLease,
            "save-setup",
            [new(first.OccurrenceId, 0, 1, CaseAssetRotation.None, CaseAssetCrop.Full)]);
        var caseVersionAfterSetup = await harness.CurrentCaseVersionAsync();
        var preparedFirstRow = await harness.OccurrenceRowAsync(first.OccurrenceId);
        Assert.Equal(1, preparedFirstRow.PreparationVersion);

        var lease = await harness.AcquireLeaseAsync(caseVersionAfterSetup);

        // One valid edit (the first occurrence, correctly expecting version 1)
        // is batched with one that goes stale (the second occurrence,
        // claiming an expected version it does not have). The whole call
        // must fail atomically: the valid half is not silently applied.
        await Assert.ThrowsAsync<CaseAssetPreparationVersionConflictException>(() => harness.SavePreparationAsync(
            lease,
            "save-partial-failure",
            [
                new(first.OccurrenceId, 1, 2, CaseAssetRotation.None, CaseAssetCrop.Full),
                new(second.OccurrenceId, 99, 1, CaseAssetRotation.Half, CaseAssetCrop.Full)
            ]));

        Assert.Equal(caseVersionAfterSetup, await harness.CurrentCaseVersionAsync());
        var unchangedFirstRow = await harness.OccurrenceRowAsync(first.OccurrenceId);
        Assert.Equal(1, unchangedFirstRow.PreparationVersion);
        Assert.Equal(1, unchangedFirstRow.Order);
        Assert.Equal(0, (await harness.OccurrenceRowAsync(second.OccurrenceId)).PreparationVersion);
    }

    [Theory]
    [InlineData(CaseLifecycleState.PostReportComplete)]
    [InlineData(CaseLifecycleState.Query)]
    public async Task CompletedAndQueryStatesRefuseAssetPreparationSave(
        CaseLifecycleState state)
    {
        await using var harness = await Harness.CreateAsync();
        var asset = await harness.SeedImageAsync(new string('z', 64));
        var preparedCrop = new CaseAssetCrop(0.1m, 0.1m, 0.5m, 0.5m);
        var preparationLease = await harness.AcquireLeaseAsync();
        await harness.SavePreparationAsync(
            preparationLease,
            "prepare-before-readonly",
            [new(
                asset.OccurrenceId,
                0,
                null,
                CaseAssetRotation.Half,
                preparedCrop)]);
        var caseVersionAfterPreparation = await harness.CurrentCaseVersionAsync();
        var initiallyPrepared = Assert.Single(
            await harness.Store.ListForCaseAsync(harness.CaseId, CancellationToken.None));
        Assert.True(initiallyPrepared.InReport);
        Assert.Equal(CaseAssetRotation.Half, initiallyPrepared.Rotation);
        Assert.Equal(preparedCrop, initiallyPrepared.Crop);

        await harness.SetWorkflowStateAsync(state);
        // Query and completed Cases remain leaseable until archived. This is a
        // valid lease for the exact current version, so the asserted error can
        // only be the Case save's state guard.
        var lease = await harness.AcquireLeaseAsync(caseVersionAfterPreparation);
        Assert.Equal(harness.CaseId, lease.CaseId);
        Assert.Equal(harness.StaffActor.SubjectId, lease.Holder);
        Assert.Equal(caseVersionAfterPreparation, lease.Version);

        var save = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.SavePreparationAsync(
            lease,
            $"save-readonly-{state}",
            [new(
                asset.OccurrenceId,
                initiallyPrepared.PreparationVersion,
                null,
                CaseAssetRotation.Clockwise90,
                CaseAssetCrop.Full)]));
        Assert.Equal("The Case cannot be saved in its current state.", save.Message);

        Assert.Equal(caseVersionAfterPreparation, await harness.CurrentCaseVersionAsync());
        var stillPrepared = Assert.Single(
            await harness.Store.ListForCaseAsync(harness.CaseId, CancellationToken.None));
        Assert.Equal(CaseAssetRotation.Half, stillPrepared.Rotation);
        Assert.Equal(preparedCrop, stillPrepared.Crop);
        Assert.Equal(initiallyPrepared.PreparationVersion, stillPrepared.PreparationVersion);
    }

    /// <summary>
    /// Only an image that can print counts (operator, 26 September 2026). A
    /// new image is in the report whatever its storage has reached, so the
    /// store says which can print: one still being stored, one whose storage
    /// failed, a superseded one and a removed one are not the report's images.
    /// </summary>
    [Fact]
    public async Task OnlyAConfirmedCurrentUnremovedImageCanPrint()
    {
        await using var harness = await Harness.CreateAsync();
        var printable = await harness.SeedImageAsync(new string('a', 64));
        var arriving = await harness.SeedImageAsync(new string('b', 64), custody: DocumentCustodyStatus.Pending);
        var failed = await harness.SeedImageAsync(new string('c', 64), custody: DocumentCustodyStatus.Failed);
        var superseded = await harness.SeedImageAsync(new string('d', 64));
        await harness.SupersedeImageAsync(superseded.DocumentId, new string('e', 64));
        var removed = await harness.SeedImageAsync(new string('f', 64));
        await harness.ExecuteSqlAsync(
            $"UPDATE DocumentVersions SET IsLogicallyRemoved = 1 WHERE Id = '{removed.VersionId:D}'");

        var preparations = await harness.Store.ListForCaseAsync(harness.CaseId, CancellationToken.None);

        Assert.All(preparations, item => Assert.True(item.InReport));
        Assert.Equal(
            [printable.OccurrenceId],
            preparations.Where(item => item.CanPrint).Select(item => item.OccurrenceId).ToArray());
        Assert.Equal(
            [printable.OccurrenceId],
            CaseAssetPreparationPolicy.ForReport(preparations).Select(item => item.OccurrenceId).ToArray());
        Assert.False((await harness.Store.GetForOccurrenceAsync(
            harness.CaseId, arriving.OccurrenceId, CancellationToken.None))!.CanPrint);
        Assert.Contains(preparations, item => item.OccurrenceId == failed.OccurrenceId && !item.CanPrint);
    }

    /// <summary>
    /// Images nobody has ordered print in the order they arrived, then by
    /// file name: the store hands the policy each image's recorded time and
    /// file name, and the Case save numbers them in that order.
    /// </summary>
    [Fact]
    public async Task ImagesNobodyOrderedFollowArrivalThenFileName()
    {
        await using var harness = await Harness.CreateAsync();
        var last = await harness.SeedImageAsync(
            new string('a', 64), fileName: "a.jpg", recordedAtUtc: Harness.StartUtc.AddMinutes(2));
        var laterName = await harness.SeedImageAsync(
            new string('b', 64), fileName: "IMG_0002.jpg", recordedAtUtc: Harness.StartUtc.AddMinutes(1));
        var earlierName = await harness.SeedImageAsync(
            new string('c', 64), fileName: "img_0001.jpg", recordedAtUtc: Harness.StartUtc.AddMinutes(1));
        var first = await harness.SeedImageAsync(
            new string('d', 64), fileName: "z.jpg", recordedAtUtc: Harness.StartUtc);
        Guid[] arrival = [first.OccurrenceId, earlierName.OccurrenceId, laterName.OccurrenceId, last.OccurrenceId];

        var preparations = await harness.Store.ListForCaseAsync(harness.CaseId, CancellationToken.None);

        Assert.Equal("z.jpg", preparations.Single(item => item.OccurrenceId == first.OccurrenceId).SourceFileName);
        Assert.Equal(
            Harness.StartUtc.AddMinutes(2),
            preparations.Single(item => item.OccurrenceId == last.OccurrenceId).RecordedAtUtc);
        Assert.Equal(
            arrival,
            CaseAssetPreparationPolicy.ForReport(preparations).Select(item => item.OccurrenceId).ToArray());

        var lease = await harness.AcquireLeaseAsync();
        await harness.SavePreparationAsync(
            lease,
            "save-arrival-order",
            [new(last.OccurrenceId, 0, null, CaseAssetRotation.Clockwise90, CaseAssetCrop.Full)]);

        var saved = await harness.Store.ListForCaseAsync(harness.CaseId, CancellationToken.None);
        Assert.Equal(arrival, saved.OrderBy(item => item.Order).Select(item => item.OccurrenceId).ToArray());
        Assert.Equal([1, 2, 3, 4], saved.Select(item => item.Order!.Value).Order().ToArray());
    }

    [Fact]
    public async Task TheDatabaseRejectsAnOutOfRangeCropIndependentlyOfApplicationValidation()
    {
        await using var harness = await Harness.CreateAsync();
        var asset = await harness.SeedImageAsync(new string('p', 64));

        var exception = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            harness.ExecuteSqlAsync(
                $"""
                UPDATE DocumentOccurrences
                SET CropLeft = 0.9, CropTop = 0, CropWidth = 0.5, CropHeight = 0.5
                WHERE Id = '{asset.OccurrenceId:D}'
                """));
        Assert.Contains("CK_DocumentOccurrences_Crop", exception.Message);
    }

    [Fact]
    public async Task TheDatabaseRejectsANonQuarterRotationIndependentlyOfApplicationValidation()
    {
        await using var harness = await Harness.CreateAsync();
        var asset = await harness.SeedImageAsync(new string('q', 64));

        var exception = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            harness.ExecuteSqlAsync(
                $"""
                UPDATE DocumentOccurrences SET RotationDegrees = 45 WHERE Id = '{asset.OccurrenceId:D}'
                """));
        Assert.Contains("CK_DocumentOccurrences_Rotation", exception.Message);
    }

    private sealed record ImageSeed(Guid OccurrenceId, Guid DocumentId, Guid VersionId);

    private sealed record OccurrenceRow(bool InReport, int? Order, long PreparationVersion);

    /// <summary>
    /// An accepted Case with its typed data snapshot, which the workspace save
    /// requires, plus the image rows and read-backs these tests need.
    /// </summary>
    private sealed class Harness : IAsyncDisposable
    {
        public static readonly DateTimeOffset StartUtc = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        private readonly CaseDataHarness caseData;

        private Harness(CaseDataHarness caseData, long caseVersion)
        {
            this.caseData = caseData;
            CaseVersion = caseVersion;
            Store = new EfCaseAssetPreparationStore(caseData.Factory);
        }

        public IDbContextFactory<PegasusDbContext> Factory => caseData.Factory;
        public Guid CaseId => caseData.CaseId;
        public long CaseVersion { get; }
        public ActionActor StaffActor => caseData.StaffActor;
        public EfCaseAssetPreparationStore Store { get; }
        public EfCaseWorkspaceStore WorkspaceStore => caseData.WorkspaceStore;

        public static async Task<Harness> CreateAsync()
        {
            var caseData = await CaseDataHarness.CreateAsync();
            try
            {
                await using var context = await caseData.Factory.CreateDbContextAsync();
                var caseVersion = await context.CaseWorkflows.AsNoTracking()
                    .Where(item => item.CaseId == caseData.CaseId)
                    .Select(item => item.Version)
                    .SingleAsync();
                return new(caseData, caseVersion);
            }
            catch
            {
                await caseData.DisposeAsync();
                throw;
            }
        }

        public Task<Guid> SeedAnotherCaseAsync() =>
            SeedCaseAsync(Factory, "PREP31002", 2, StartUtc);

        public Task<CaseEditLease> AcquireLeaseAsync(long? version = null) =>
            caseData.AcquireLeaseAsync(version ?? CaseVersion, StaffActor, $"lease-{Guid.NewGuid():N}");

        public Task<SaveCaseWorkspaceResult> SavePreparationAsync(
            CaseEditLease lease,
            string operationKey,
            IReadOnlyList<CaseAssetPreparationEdit> edits) =>
            WorkspaceStore.SaveAsync(
                new SaveCaseWorkspaceRequest(CaseId, lease.Version, StaffActor, operationKey, null, lease.Token)
                {
                    ImagePreparation = new(edits)
                },
                CancellationToken.None);

        public async Task ExecuteSqlAsync(string sql)
        {
            await using var context = await Factory.CreateDbContextAsync();
            await context.Database.ExecuteSqlRawAsync(sql);
        }

        public async Task SetWorkflowStateAsync(CaseLifecycleState state)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == CaseId);
            workflow.State = state.ToString();
            workflow.ClosureOutcome = null;
            await context.SaveChangesAsync();
        }

        public async Task<ImageSeed> SeedImageAsync(
            string sha256,
            Guid? caseId = null,
            DocumentCustodyStatus custody = DocumentCustodyStatus.Confirmed,
            string fileName = "asset.jpg",
            DateTimeOffset? recordedAtUtc = null)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var owningCaseId = caseId ?? CaseId;
            var documentId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var occurrenceId = Guid.NewGuid();
            var nextOrdinal = 1 + await context.Set<CaseDocumentEntity>()
                .Where(item => item.CaseId == owningCaseId)
                .Select(item => (int?)item.Ordinal)
                .MaxAsync() ?? 1;
            context.AddRange(
                new CaseDocumentEntity
                {
                    Id = documentId,
                    CaseId = owningCaseId,
                    Ordinal = nextOrdinal,
                    SourceOccurrenceIdentity = $"test-image:{occurrenceId:N}"
                },
                new DocumentVersionEntity
                {
                    Id = versionId,
                    DocumentId = documentId,
                    Version = 1,
                    FileName = fileName,
                    MediaType = "image/jpeg",
                    ContentLength = 1,
                    Sha256 = sha256,
                    CustodyStatus = custody,
                    CreatedAtUtc = StartUtc,
                    CreatedBy = "Staff:test",
                    IsCurrent = true
                },
                new DocumentOccurrenceEntity
                {
                    Id = occurrenceId,
                    CaseId = owningCaseId,
                    DocumentId = documentId,
                    VersionId = versionId,
                    SemanticRole = DocumentSemanticRole.Image,
                    Source = DocumentSource.StaffUpload,
                    SourceOccurrenceIdentity = $"test-image:{occurrenceId:N}",
                    RecordedAtUtc = recordedAtUtc ?? StartUtc,
                    OperationKey = $"seed-image:{occurrenceId:N}"
                });
            await context.SaveChangesAsync();
            return new(occurrenceId, documentId, versionId);
        }

        public async Task SupersedeImageAsync(Guid documentId, string newSha256)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var previous = await context.Set<DocumentVersionEntity>()
                .Where(version => version.DocumentId == documentId && version.IsCurrent)
                .ToListAsync();
            foreach (var version in previous)
            {
                version.IsCurrent = false;
            }
            context.Add(new DocumentVersionEntity
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                Version = previous.Count == 0 ? 1 : previous.Max(version => version.Version) + 1,
                FileName = "asset.jpg",
                MediaType = "image/jpeg",
                ContentLength = 1,
                Sha256 = newSha256,
                CustodyStatus = DocumentCustodyStatus.Confirmed,
                CreatedAtUtc = StartUtc,
                CreatedBy = "Staff:test",
                IsCurrent = true
            });
            await context.SaveChangesAsync();
        }

        public async Task<long> CurrentCaseVersionAsync()
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await context.CaseWorkflows.AsNoTracking()
                .Where(item => item.CaseId == CaseId)
                .Select(item => item.Version)
                .SingleAsync();
        }

        public async Task<string> SourceSha256Async(Guid versionId)
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await context.Set<DocumentVersionEntity>().AsNoTracking()
                .Where(version => version.Id == versionId)
                .Select(version => version.Sha256)
                .SingleAsync();
        }

        public async Task<OccurrenceRow> OccurrenceRowAsync(Guid occurrenceId)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var occurrence = await context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                .SingleAsync(item => item.Id == occurrenceId);
            return new(occurrence.InReport, occurrence.SupportingOrder, occurrence.PreparationVersion);
        }

        public async Task<int> WorkflowEventCountAsync(string eventType)
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await context.CaseWorkflowEvents.AsNoTracking()
                .CountAsync(item => item.CaseId == CaseId && item.EventType == eventType);
        }

        public async Task<int> ActionHistoryCountAsync(string eventKind)
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await context.ActionHistory.AsNoTracking()
                .CountAsync(item =>
                    item.AggregateType == "case"
                    && item.AggregateId == CaseId.ToString("D")
                    && item.EventKind == eventKind);
        }

        public async Task<int> CaseHistoryCountAsync(string eventType)
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await context.CaseHistory.AsNoTracking()
                .CountAsync(item => item.CaseId == CaseId && item.EventType == eventType);
        }

        private static async Task<Guid> SeedCaseAsync(
            IDbContextFactory<PegasusDbContext> factory,
            string reference,
            int sequence,
            DateTimeOffset occurredAtUtc)
        {
            await using var context = await factory.CreateDbContextAsync();
            var organizationId = Guid.NewGuid();
            var lineageId = Guid.NewGuid();
            var principalId = Guid.NewGuid();
            var receiptId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            context.AddRange(
                new OrganizationEntity { Id = organizationId, Name = $"Asset preparation test {reference}", Version = 0 },
                new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = occurredAtUtc },
                new PrincipalEntity
                {
                    Id = principalId,
                    OrganizationId = organizationId,
                    SequenceLineageId = lineageId,
                    Code = reference,
                    IsActive = true,
                    Version = 0
                },
                new IntakeReceiptEntity
                {
                    Id = receiptId,
                    SourceFileName = "prep-origin.pdf",
                    MediaType = "application/pdf",
                    SourceLength = 1,
                    SourceHash = new string('0', 64),
                    SourceChannel = "manual_upload",
                    ExternalReceiptToken = $"prep:{receiptId:N}",
                    ReceivedAtUtc = occurredAtUtc,
                    ProcessedAtUtc = occurredAtUtc,
                    SourceReaderKey = "prep-test",
                    SourceReaderVersion = "1",
                    Version = 0,
                    Decision = "case_created",
                    DecisionReason = "Asset preparation test",
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
                    Sequence = sequence,
                    Reference = reference,
                    Type = "inspection",
                    InitialState = "NotReady",
                    CustodyState = "confirmed",
                    OriginIntakeReceiptId = receiptId,
                    CreatedAtUtc = occurredAtUtc,
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                },
                new CaseWorkflowEntity
                {
                    CaseId = caseId,
                    State = "Review",
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                });
            await context.SaveChangesAsync();
            return caseId;
        }

        public async ValueTask DisposeAsync() => await caseData.DisposeAsync();
    }
}
