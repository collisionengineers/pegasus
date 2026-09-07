using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class CaseAssetPreparationPersistenceTests
{
    [Fact]
    public async Task SavingAssignsRolesOrderRotationAndCropWithoutTouchingBytes()
    {
        await using var harness = await Harness.CreateAsync();
        var closeUp = await harness.SeedImageAsync(new string('a', 64));
        var overview = await harness.SeedImageAsync(new string('b', 64));
        var supporting = await harness.SeedImageAsync(new string('c', 64));
        var lease = await harness.AcquireLeaseAsync();

        var result = await harness.Store.SaveAsync(
            new(
                harness.CaseId,
                harness.CaseVersion,
                harness.StaffActor,
                "save-1",
                "Prepared images for the report",
                lease.Token,
                [
                    new(closeUp.OccurrenceId, 0, CaseAssetReportRole.CloseUp, null, CaseAssetRotation.None, CaseAssetCrop.Full),
                    new(overview.OccurrenceId, 0, CaseAssetReportRole.Overview, null, CaseAssetRotation.Clockwise90, CaseAssetCrop.Full),
                    new(
                        supporting.OccurrenceId,
                        0,
                        CaseAssetReportRole.Supporting,
                        1,
                        CaseAssetRotation.None,
                        new(0.1m, 0.1m, 0.5m, 0.5m))
                ]),
            CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Equal(CaseAssetReportRole.CloseUp, result.Single(item => item.OccurrenceId == closeUp.OccurrenceId).Role);
        var overviewResult = result.Single(item => item.OccurrenceId == overview.OccurrenceId);
        Assert.Equal(CaseAssetReportRole.Overview, overviewResult.Role);
        Assert.Equal(CaseAssetRotation.Clockwise90, overviewResult.Rotation);
        var supportingResult = result.Single(item => item.OccurrenceId == supporting.OccurrenceId);
        Assert.Equal(CaseAssetReportRole.Supporting, supportingResult.Role);
        Assert.Equal(1, supportingResult.Order);
        Assert.Equal(new CaseAssetCrop(0.1m, 0.1m, 0.5m, 0.5m), supportingResult.Crop);

        Assert.Equal(harness.CaseVersion + 1, await harness.CurrentCaseVersionAsync());
        Assert.Equal(3, (await harness.Store.ListForCaseAsync(harness.CaseId, CancellationToken.None)).Count);

        // Bytes/hash are never touched by preparation.
        Assert.Equal(new string('a', 64), await harness.SourceSha256Async(closeUp.VersionId));
        Assert.Equal(new string('b', 64), await harness.SourceSha256Async(overview.VersionId));
        Assert.Equal(new string('c', 64), await harness.SourceSha256Async(supporting.VersionId));

        Assert.Equal(1, await harness.WorkflowEventCountAsync("case_asset_preparation_saved"));
        Assert.Equal(1, await harness.ActionHistoryCountAsync("case_asset_preparation_saved"));
        Assert.Equal(1, await harness.CaseHistoryCountAsync("case_asset_preparation_saved"));
    }

    [Fact]
    public async Task ReplayingTheSameOperationKeyAndPayloadReturnsTheSameResultWithoutBumpingVersionAgain()
    {
        await using var harness = await Harness.CreateAsync();
        var asset = await harness.SeedImageAsync(new string('d', 64));
        var lease = await harness.AcquireLeaseAsync();
        var request = new SaveCaseAssetPreparationRequest(
            harness.CaseId,
            harness.CaseVersion,
            harness.StaffActor,
            "save-replay",
            "Prepared the overview",
            lease.Token,
            [new(asset.OccurrenceId, 0, CaseAssetReportRole.Overview, null, CaseAssetRotation.None, CaseAssetCrop.Full)]);

        var first = await harness.Store.SaveAsync(request, CancellationToken.None);
        var second = await harness.Store.SaveAsync(request, CancellationToken.None);

        Assert.Equal(
            first.Select(item => (item.OccurrenceId, item.Role, item.PreparationVersion)),
            second.Select(item => (item.OccurrenceId, item.Role, item.PreparationVersion)));
        Assert.Equal(harness.CaseVersion + 1, await harness.CurrentCaseVersionAsync());
        Assert.Equal(1, await harness.WorkflowEventCountAsync("case_asset_preparation_saved"));
    }

    [Fact]
    public async Task ReplayingTheSameOperationKeyWithAChangedPayloadThrowsAConflict()
    {
        await using var harness = await Harness.CreateAsync();
        var asset = await harness.SeedImageAsync(new string('e', 64));
        var lease = await harness.AcquireLeaseAsync();
        var first = new SaveCaseAssetPreparationRequest(
            harness.CaseId,
            harness.CaseVersion,
            harness.StaffActor,
            "save-conflict",
            "Prepared the overview",
            lease.Token,
            [new(asset.OccurrenceId, 0, CaseAssetReportRole.Overview, null, CaseAssetRotation.None, CaseAssetCrop.Full)]);
        await harness.Store.SaveAsync(first, CancellationToken.None);

        var changed = first with
        {
            Edits = [new(asset.OccurrenceId, 0, CaseAssetReportRole.CloseUp, null, CaseAssetRotation.None, CaseAssetCrop.Full)]
        };

        await Assert.ThrowsAsync<CaseOperationConflictException>(
            () => harness.Store.SaveAsync(changed, CancellationToken.None));
    }

    [Fact]
    public async Task AStaleCaseVersionIsRejectedAndNothingIsPersisted()
    {
        await using var harness = await Harness.CreateAsync();
        var asset = await harness.SeedImageAsync(new string('f', 64));
        var lease = await harness.AcquireLeaseAsync();

        await Assert.ThrowsAsync<CaseVersionConflictException>(() => harness.Store.SaveAsync(
            new(
                harness.CaseId,
                harness.CaseVersion + 41,
                harness.StaffActor,
                "save-stale-version",
                "Prepared the close-up",
                lease.Token,
                [new(asset.OccurrenceId, 0, CaseAssetReportRole.CloseUp, null, CaseAssetRotation.None, CaseAssetCrop.Full)]),
            CancellationToken.None));

        Assert.Equal(harness.CaseVersion, await harness.CurrentCaseVersionAsync());
        Assert.Equal(CaseAssetReportRole.NotUsed, (await harness.OccurrenceRowAsync(asset.OccurrenceId)).Role);
    }

    [Fact]
    public async Task AMissingLeaseTokenIsRejected()
    {
        await using var harness = await Harness.CreateAsync();
        var asset = await harness.SeedImageAsync(new string('g', 64));

        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() => harness.Store.SaveAsync(
            new(
                harness.CaseId,
                harness.CaseVersion,
                harness.StaffActor,
                "save-no-lease",
                "Prepared the close-up",
                new string('0', 43),
                [new(asset.OccurrenceId, 0, CaseAssetReportRole.CloseUp, null, CaseAssetRotation.None, CaseAssetCrop.Full)]),
            CancellationToken.None));

        Assert.Equal(harness.CaseVersion, await harness.CurrentCaseVersionAsync());
    }

    [Fact]
    public async Task AnExpiredLeaseIsRejected()
    {
        await using var harness = await Harness.CreateAsync();
        var asset = await harness.SeedImageAsync(new string('h', 64));
        var lease = await harness.AcquireLeaseAsync();
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(10));

        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() => harness.Store.SaveAsync(
            new(
                harness.CaseId,
                harness.CaseVersion,
                harness.StaffActor,
                "save-expired-lease",
                "Prepared the close-up",
                lease.Token,
                [new(asset.OccurrenceId, 0, CaseAssetReportRole.CloseUp, null, CaseAssetRotation.None, CaseAssetCrop.Full)]),
            CancellationToken.None));
    }

    [Fact]
    public async Task AForeignActorsLeaseTokenIsRejected()
    {
        await using var harness = await Harness.CreateAsync();
        var asset = await harness.SeedImageAsync(new string('i', 64));
        var lease = await harness.AcquireLeaseAsync();
        var otherStaff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() => harness.Store.SaveAsync(
            new(
                harness.CaseId,
                harness.CaseVersion,
                otherStaff,
                "save-foreign-lease",
                "Prepared the close-up",
                lease.Token,
                [new(asset.OccurrenceId, 0, CaseAssetReportRole.CloseUp, null, CaseAssetRotation.None, CaseAssetCrop.Full)]),
            CancellationToken.None));
    }

    [Fact]
    public async Task ACrossCaseAssetIsRejected()
    {
        await using var harness = await Harness.CreateAsync();
        var otherCaseId = await harness.SeedAnotherCaseAsync();
        var foreignAsset = await harness.SeedImageAsync(new string('j', 64), caseId: otherCaseId);
        var lease = await harness.AcquireLeaseAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Store.SaveAsync(
            new(
                harness.CaseId,
                harness.CaseVersion,
                harness.StaffActor,
                "save-cross-case",
                "Prepared the close-up",
                lease.Token,
                [new(foreignAsset.OccurrenceId, 0, CaseAssetReportRole.CloseUp, null, CaseAssetRotation.None, CaseAssetCrop.Full)]),
            CancellationToken.None));

        Assert.Equal(harness.CaseVersion, await harness.CurrentCaseVersionAsync());
    }

    [Fact]
    public async Task AStaleSupersededAssetVersionIsRejected()
    {
        await using var harness = await Harness.CreateAsync();
        var asset = await harness.SeedImageAsync(new string('k', 64));
        await harness.SupersedeImageAsync(asset.DocumentId, new string('l', 64));
        var lease = await harness.AcquireLeaseAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Store.SaveAsync(
            new(
                harness.CaseId,
                harness.CaseVersion,
                harness.StaffActor,
                "save-stale-source",
                "Prepared the overview",
                lease.Token,
                [new(asset.OccurrenceId, 0, CaseAssetReportRole.Overview, null, CaseAssetRotation.None, CaseAssetCrop.Full)]),
            CancellationToken.None));

        Assert.Equal(harness.CaseVersion, await harness.CurrentCaseVersionAsync());
        Assert.Equal(CaseAssetReportRole.NotUsed, (await harness.OccurrenceRowAsync(asset.OccurrenceId)).Role);
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
        await harness.Store.SaveAsync(
            new(
                harness.CaseId,
                harness.CaseVersion,
                harness.StaffActor,
                "save-setup",
                "Prepared the first supporting image",
                initialLease.Token,
                [new(first.OccurrenceId, 0, CaseAssetReportRole.Supporting, 1, CaseAssetRotation.None, CaseAssetCrop.Full)]),
            CancellationToken.None);
        var caseVersionAfterSetup = await harness.CurrentCaseVersionAsync();
        var preparedFirstRow = await harness.OccurrenceRowAsync(first.OccurrenceId);
        Assert.Equal(1, preparedFirstRow.PreparationVersion);

        var lease = await harness.AcquireLeaseAsync(caseVersionAfterSetup);

        // One valid edit (the first occurrence, correctly expecting version 1)
        // is batched with one that goes stale (the second occurrence,
        // claiming an expected version it does not have). The whole call
        // must fail atomically: the valid half is not silently applied.
        await Assert.ThrowsAsync<CaseAssetPreparationVersionConflictException>(() => harness.Store.SaveAsync(
            new(
                harness.CaseId,
                caseVersionAfterSetup,
                harness.StaffActor,
                "save-partial-failure",
                "Reorders both images",
                lease.Token,
                [
                    new(first.OccurrenceId, 1, CaseAssetReportRole.Supporting, 2, CaseAssetRotation.None, CaseAssetCrop.Full),
                    new(second.OccurrenceId, 99, CaseAssetReportRole.CloseUp, null, CaseAssetRotation.None, CaseAssetCrop.Full)
                ]),
            CancellationToken.None));

        Assert.Equal(caseVersionAfterSetup, await harness.CurrentCaseVersionAsync());
        var unchangedFirstRow = await harness.OccurrenceRowAsync(first.OccurrenceId);
        Assert.Equal(1, unchangedFirstRow.PreparationVersion);
        Assert.Equal(1, unchangedFirstRow.Order);
        Assert.Equal(CaseAssetReportRole.NotUsed, (await harness.OccurrenceRowAsync(second.OccurrenceId)).Role);
    }

    [Fact]
    public async Task ResetRestoresTheOriginalPresentationAndKeepsBytesUnchanged()
    {
        await using var harness = await Harness.CreateAsync();
        var asset = await harness.SeedImageAsync(new string('o', 64));
        var lease = await harness.AcquireLeaseAsync();
        await harness.Store.SaveAsync(
            new(
                harness.CaseId,
                harness.CaseVersion,
                harness.StaffActor,
                "save-before-reset",
                "Prepared the overview",
                lease.Token,
                [
                    new(
                        asset.OccurrenceId,
                        0,
                        CaseAssetReportRole.Overview,
                        null,
                        CaseAssetRotation.Half,
                        new(0.2m, 0.2m, 0.4m, 0.4m))
                ]),
            CancellationToken.None);
        var caseVersionAfterSave = await harness.CurrentCaseVersionAsync();
        var resetLease = await harness.AcquireLeaseAsync(caseVersionAfterSave);

        var result = await harness.Store.ResetAsync(
            new(
                harness.CaseId,
                caseVersionAfterSave,
                harness.StaffActor,
                "reset-1",
                "Restored the original presentation",
                resetLease.Token,
                [asset.OccurrenceId]),
            CancellationToken.None);

        var restored = Assert.Single(result);
        Assert.Equal(CaseAssetReportRole.NotUsed, restored.Role);
        Assert.Null(restored.Order);
        Assert.Equal(CaseAssetRotation.None, restored.Rotation);
        Assert.True(restored.Crop.IsFull);
        Assert.Equal(new string('o', 64), await harness.SourceSha256Async(asset.VersionId));
        Assert.Equal(caseVersionAfterSave + 1, await harness.CurrentCaseVersionAsync());
    }

    [Fact]
    public async Task ResettingASupportingImageRenormalizesTheRemainingSequence()
    {
        await using var harness = await Harness.CreateAsync();
        var first = await harness.SeedImageAsync(new string('r', 64));
        var second = await harness.SeedImageAsync(new string('s', 64));
        var third = await harness.SeedImageAsync(new string('t', 64));
        var lease = await harness.AcquireLeaseAsync();
        await harness.Store.SaveAsync(
            new(
                harness.CaseId,
                harness.CaseVersion,
                harness.StaffActor,
                "save-three-supporting",
                "Ordered three supporting images",
                lease.Token,
                [
                    new(first.OccurrenceId, 0, CaseAssetReportRole.Supporting, 1, CaseAssetRotation.None, CaseAssetCrop.Full),
                    new(second.OccurrenceId, 0, CaseAssetReportRole.Supporting, 2, CaseAssetRotation.None, CaseAssetCrop.Full),
                    new(third.OccurrenceId, 0, CaseAssetReportRole.Supporting, 3, CaseAssetRotation.None, CaseAssetCrop.Full)
                ]),
            CancellationToken.None);
        var caseVersionAfterSave = await harness.CurrentCaseVersionAsync();
        var resetLease = await harness.AcquireLeaseAsync(caseVersionAfterSave);

        // Removing the middle image (order 2) must not leave a 1, 3 gap in
        // what remains — the same contiguous-from-1 rule Save enforces.
        var result = await harness.Store.ResetAsync(
            new(
                harness.CaseId,
                caseVersionAfterSave,
                harness.StaffActor,
                "reset-middle",
                "Removed the middle supporting image",
                resetLease.Token,
                [second.OccurrenceId]),
            CancellationToken.None);

        var remainingFirst = result.Single(item => item.OccurrenceId == first.OccurrenceId);
        var remainingThird = result.Single(item => item.OccurrenceId == third.OccurrenceId);
        var resetSecond = result.Single(item => item.OccurrenceId == second.OccurrenceId);
        Assert.Equal(1, remainingFirst.Order);
        Assert.Equal(2, remainingThird.Order);
        Assert.Equal(CaseAssetReportRole.NotUsed, resetSecond.Role);
        Assert.Null(resetSecond.Order);
        Assert.Equal(2, (await harness.OccurrenceRowAsync(third.OccurrenceId)).Order);
    }

    [Fact]
    public async Task TheDatabaseRejectsAnOutOfRangeCropIndependentlyOfApplicationValidation()
    {
        await using var harness = await Harness.CreateAsync();
        var asset = await harness.SeedImageAsync(new string('p', 64));

        var exception = await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() =>
            harness.Database.ExecuteAsync(
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
            harness.Database.ExecuteAsync(
                $"""
                UPDATE DocumentOccurrences SET RotationDegrees = 45 WHERE Id = '{asset.OccurrenceId:D}'
                """));
        Assert.Contains("CK_DocumentOccurrences_Rotation", exception.Message);
    }

    private sealed record ImageSeed(Guid OccurrenceId, Guid DocumentId, Guid VersionId);

    private sealed record OccurrenceRow(CaseAssetReportRole Role, int? Order, long PreparationVersion);

    private sealed class Harness : IAsyncDisposable
    {
        private static readonly DateTimeOffset StartUtc = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

        private Harness(
            LocalDbTestDatabase database,
            PooledDbContextFactory<PegasusDbContext> factory,
            MutableTimeProvider timeProvider,
            Guid caseId,
            long caseVersion,
            ActionActor staffActor)
        {
            Database = database;
            Factory = factory;
            TimeProvider = timeProvider;
            CaseId = caseId;
            CaseVersion = caseVersion;
            StaffActor = staffActor;
            Store = new EfCaseAssetPreparationStore(factory, timeProvider);
            WorkflowStore = new EfCaseWorkflowStore(factory, timeProvider);
            AcquireLease = new AcquireCaseEditLease(WorkflowStore);
        }

        public LocalDbTestDatabase Database { get; }
        public PooledDbContextFactory<PegasusDbContext> Factory { get; }
        public MutableTimeProvider TimeProvider { get; }
        public Guid CaseId { get; }
        public long CaseVersion { get; }
        public ActionActor StaffActor { get; }
        public EfCaseAssetPreparationStore Store { get; }
        public EfCaseWorkflowStore WorkflowStore { get; }
        public AcquireCaseEditLease AcquireLease { get; }

        public static async Task<Harness> CreateAsync()
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            try
            {
                var options = new DbContextOptionsBuilder<PegasusDbContext>()
                    .UseSqlServer(database.ConnectionString)
                    .Options;
                var factory = new PooledDbContextFactory<PegasusDbContext>(options);
                var timeProvider = new MutableTimeProvider(StartUtc);
                var staffActor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
                var caseId = await SeedCaseAsync(factory, "PREP31001", 1, StartUtc);
                return new(database, factory, timeProvider, caseId, 1, staffActor);
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public Task<Guid> SeedAnotherCaseAsync() =>
            SeedCaseAsync(Factory, "PREP31002", 2, StartUtc);

        public Task<CaseEditLease> AcquireLeaseAsync(long? version = null) =>
            AcquireLease.ExecuteAsync(
                new(CaseId, version ?? CaseVersion, StaffActor, $"lease-{Guid.NewGuid():N}"),
                CancellationToken.None);

        public async Task<ImageSeed> SeedImageAsync(string sha256, Guid? caseId = null)
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
                    FileName = "asset.jpg",
                    MediaType = "image/jpeg",
                    ContentLength = 1,
                    Sha256 = sha256,
                    CustodyStatus = DocumentCustodyStatus.Confirmed,
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
                    RecordedAtUtc = StartUtc,
                    OperationKey = $"seed-image:{occurrenceId:N}",
                    PreparationRole = nameof(CaseAssetReportRole.NotUsed)
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
            var role = occurrence.PreparationRole is null
                ? CaseAssetReportRole.NotUsed
                : Enum.Parse<CaseAssetReportRole>(occurrence.PreparationRole);
            return new(role, occurrence.SupportingOrder, occurrence.PreparationVersion);
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
            PooledDbContextFactory<PegasusDbContext> factory,
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
                    Type = "Inspection",
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

        public async ValueTask DisposeAsync() => await Database.DisposeAsync();

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
}
