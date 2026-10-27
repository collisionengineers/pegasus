using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Reports;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Persistence;
using Pegasus.IntegrationTests.Support;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Registering an Image-initiated Case durably enqueues external
/// custody work that stores every group image under the registration
/// reference, and the merge into an instruction case enqueues a fold that
/// moves the contents into the case's evidence location and removes the
/// emptied folder. Runs against LocalDB with the local custody adapter — the
/// same processor path production drives against Box.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ImageCaseCustodyIntegrationTests
{
    private static readonly byte[] PngBytes = Convert.FromBase64String(MultiFormatFixture.TinyPngBase64);

    private static ActionActor StaffActor() => ActionActor.Staff(
        DevelopmentOfflineIdentity.AdministratorId,
        [StaffRole.Administrator]);

    [Fact]
    public async Task AMailboxImageMatchingSeveralCasesRegistersAndQueuesItsFolder()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine("AB12CDE"));
        using var client = IntakeWebDriver.CreateClient(factory);
        _ = await ImageIntakeTestData.SeedInstructionCaseAsync(factory, client, "AB12 CDE", "MULTI-MATCH-01");
        _ = await ImageIntakeTestData.SeedInstructionCaseAsync(factory, client, "AB12 CDE", "MULTI-MATCH-02");

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var receiptId = await AllocationTestData.SubmitAndProcessAsync(
            services,
            new IntakeSource(
                "vehicle.png",
                "image/png",
                PngBytes,
                services.GetRequiredService<TimeProvider>().GetUtcNow(),
                "system-worker:approved-inbox-poller",
                new(IntakeSourceChannel.Mailbox, $"image-multi:{Guid.NewGuid():N}")),
            $"mailbox-image-multi:{Guid.NewGuid():N}");

        // Several Cases match, so nothing is filed automatically: the images
        // register as before and their folder is queued.
        var detail = await services.GetRequiredService<IImageIntakeQueries>()
            .GetByOriginReceiptAsync(receiptId, CancellationToken.None);
        Assert.NotNull(detail);
        Assert.Null(detail!.AssociatedCaseId);
        await using var context = await services.GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var work = await context.ExternalWorkItems.AsNoTracking()
            .SingleAsync(item => item.ImageIntakeId == detail.Record.Id
                && item.Kind == ExternalWorkKinds.CreateImageCaseCustody);
        Assert.Equal("pending", work.State);
    }

    [Fact]
    public async Task PdfParentRetainsItsSourceAndEachSelectedEmbeddedPhotographExactlyOnce()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var sourceBytes = Convert.FromBase64String(MultiFormatFixture.TinyPngBase64);
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "seed.png",
            "image/png",
            sourceBytes,
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var artifacts = services.GetRequiredService<IIntakeArtifactStore>();
        var photographOne = new byte[50_000];
        var photographTwo = new byte[51_000];
        photographOne[0] = 1;
        photographTwo[0] = 2;
        var first = await StageAsync(photographOne);
        var second = await StageAsync(photographTwo);
        var duplicate = await StageAsync(photographOne);
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        Guid sourceAssetId;
        Guid firstAssetId;
        Guid secondAssetId;
        Guid duplicateAssetId;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var receipt = await context.IntakeReceipts
                .Include(item => item.Assets)
                .SingleAsync(item => item.Id == receiptId);
            receipt.SourceFileName = "images.pdf";
            receipt.MediaType = "application/pdf";
            receipt.Decision = "needs_sorting";
            receipt.DecisionReason = "The PDF contains photographs for image intake.";
            // Keep the versioned fields envelope written by StoreAsync; this
            // fixture changes the retained material, not its JSON contract.
            receipt.FailureCode = null;
            receipt.FailureReason = null;
            sourceAssetId = receipt.Assets.Single(item => item.Kind == "source").Id;
            var source = receipt.Assets.Single(item => item.Id == sourceAssetId);
            source.FileName = "images.pdf";
            source.MediaType = "application/pdf";

            firstAssetId = AddEmbeddedImage(
                context, receiptId, "page-1-image-2.jpg", first, 547, 650);
            secondAssetId = AddEmbeddedImage(
                context, receiptId, "page-1-image-3.jpg", second, 552, 650);
            // Same bytes as the first photograph: selection deduplicates by
            // hash, so this asset must not become another custody file.
            duplicateAssetId = AddEmbeddedImage(
                context, receiptId, "page-1-image-4.jpg", duplicate, 547, 650);
            await context.SaveChangesAsync();
        }

        var resolver = services.GetRequiredService<IImageIntakeOriginResolver>();
        var origin = await resolver.ResolveOriginAsync(receiptId, CancellationToken.None);
        var registered = await services.GetRequiredService<IRegisterImageIntake>().ExecuteAsync(
            new(
                origin!,
                "AB12CDE",
                StaffActor(),
                $"pdf-image-custody-register:{receiptId:N}",
                "Staff registered the PDF's retained photographs."),
            CancellationToken.None);
        Guid workId;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            workId = await context.ExternalWorkItems
                .Where(item => item.ImageIntakeId == registered.Id
                    && item.Kind == ExternalWorkKinds.CreateImageCaseCustody)
                .Select(item => item.Id)
                .SingleAsync();
        }

        var processor = services.GetRequiredService<IProcessQueuedCustody>();
        await processor.ExecuteAsync(workId, CancellationToken.None);
        await processor.ExecuteAsync(workId, CancellationToken.None);

        var imagesDirectory = Path.Combine(
            factory.ArtifactDirectory,
            "custody",
            "cases",
            registered.Id.ToString("N"),
            "images");
        var retainedDirectories = Directory.GetDirectories(imagesDirectory)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            [
                $"001-{sourceAssetId:N}",
                $"002-{firstAssetId:N}",
                $"003-{secondAssetId:N}"
            ],
            retainedDirectories.Select(Path.GetFileName));
        Assert.DoesNotContain(retainedDirectories, path => path.Contains($"{duplicateAssetId:N}", StringComparison.Ordinal));
        Assert.Equal(sourceBytes, await File.ReadAllBytesAsync(Path.Combine(retainedDirectories[0], "content")));
        Assert.Equal(photographOne, await File.ReadAllBytesAsync(Path.Combine(retainedDirectories[1], "content")));
        Assert.Equal(photographTwo, await File.ReadAllBytesAsync(Path.Combine(retainedDirectories[2], "content")));

        var metadata = new List<JsonDocument>();
        foreach (var directory in retainedDirectories)
        {
            metadata.Add(JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(directory, "metadata.json"))));
        }
        try
        {
            Assert.Equal(
                [
                    $"image-case-custody:{registered.Id:N}:asset:{sourceAssetId:N}",
                    $"image-case-custody:{registered.Id:N}:asset:{firstAssetId:N}",
                    $"image-case-custody:{registered.Id:N}:asset:{secondAssetId:N}"
                ],
                metadata.Select(document => document.RootElement.GetProperty("OperationKey").GetString()));
            Assert.Equal(
                ["images.pdf", "page-1-image-2.jpg", "page-1-image-3.jpg"],
                metadata.Select(document => document.RootElement.GetProperty("FileName").GetString()));
        }
        finally
        {
            foreach (var document in metadata)
            {
                document.Dispose();
            }
        }

        await using var verified = await contextFactory.CreateDbContextAsync();
        var work = await verified.ExternalWorkItems.SingleAsync(item => item.Id == workId);
        Assert.Equal("completed", work.State);
        Assert.Equal(1, work.AttemptCount);

        async Task<(string Hash, string StorageKey, int Length)> StageAsync(byte[] bytes)
        {
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            return (hash, await artifacts.StoreAsync(hash, bytes, CancellationToken.None), bytes.Length);
        }
    }

    [Fact]
    public async Task MixedGroupExcludesUnregisteredDocumentSiblingFromImagesCountsAndCustody()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var bytes = Convert.FromBase64String(MultiFormatFixture.TinyPngBase64);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);
        var upload = await IntakeWebDriver.PostUploadManyAsync(client,
            form.AntiforgeryToken, form.ExternalReceiptToken,
            [("vehicle.png", "image/png", bytes), ("document-photo.png", "image/png", bytes)]);
        var groupId = Guid.Parse(upload.Location!.OriginalString.Split('/').Last());
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var group = (await services.GetRequiredService<IIntakeSubmissionGroupStore>().GetAsync(groupId))!;
        var receiptIds = new List<Guid>();
        foreach (var member in group.Members.OrderBy(member => member.Ordinal))
        {
            receiptIds.Add((await IntakeWebDriver.DrainStagedAsync(services, member.StagedReceiptId)).ProcessedReceiptId);
        }
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            // Seed a processed office document with a retained photograph. Its
            // material role is outside the image/PDF/mail automation route.
            var sibling = await context.IntakeReceipts.SingleAsync(item => item.Id == receiptIds[1]);
            sibling.MediaType = "application/msword";
            sibling.SourceFileName = "report.doc";
            await context.SaveChangesAsync();
        }
        var origin = (await services.GetRequiredService<IImageIntakeOriginResolver>()
            .ResolveOriginAsync(receiptIds[0], CancellationToken.None))!;
        var record = await services.GetRequiredService<IRegisterImageIntake>().ExecuteAsync(new(
            origin, "AB12CDE", StaffActor(), $"mixed-image-group:{groupId:N}",
            "Register only the image-evidence member.", SubmissionGroupId: groupId), CancellationToken.None);
        var queries = services.GetRequiredService<IImageIntakeQueries>();
        Assert.Equal(receiptIds[0], Assert.Single(await queries.ListImagesAsync(record.Id, CancellationToken.None)).ReceiptId);
        var batched = await queries.ListImagesAsync([record.Id], CancellationToken.None);
        Assert.Equal(receiptIds[0], Assert.Single(batched[record.Id]).ReceiptId);
        Assert.Equal(1, Assert.Single(await queries.SearchByRegistrationAsync("AB12CDE", CancellationToken.None)).ImageCount);
        Assert.Null(await queries.GetByOriginReceiptAsync(receiptIds[1], CancellationToken.None));
        var siblingReceipt = (await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(receiptIds[1], CancellationToken.None))!;
        Assert.Equal(IntakeDecision.NeedsSorting, siblingReceipt.Decision);
        Guid workId;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            workId = await context.ExternalWorkItems.Where(item => item.ImageIntakeId == record.Id
                && item.Kind == ExternalWorkKinds.CreateImageCaseCustody).Select(item => item.Id).SingleAsync();
        }
        await services.GetRequiredService<IProcessQueuedCustody>().ExecuteAsync(workId, CancellationToken.None);
        var retained = Assert.Single(Directory.GetDirectories(Path.Combine(
            factory.ArtifactDirectory, "custody", "cases", record.Id.ToString("N"), "images")));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(retained, "content")));
    }

    [Fact]
    public async Task RegistrationStoresEveryGroupImageAndMergeFoldsThemIntoTheCase()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var pngBytes = PngBytes;
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var (record, memberReceiptIds) = await RegisterTwoPhotographsAsync(factory, client, services);
        Assert.Equal("AB12CDE-01", record.ImageIntakeReference);

        // Registration itself enqueued the custody work in-transaction:
        // Box being unreachable can never block a registration.
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        Guid createWorkId;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var work = await context.ExternalWorkItems
                .AsNoTracking()
                .SingleAsync(item => item.ImageIntakeId == record.Id
                    && item.Kind == ExternalWorkKinds.CreateImageCaseCustody);
            Assert.Equal("pending", work.State);
            Assert.Null(work.CaseId);
            Assert.NotNull(work.CaseRootCreationToken);
            createWorkId = work.Id;
            Assert.Equal(
                "pending",
                await ReadImageCustodyStateAsync(context, record.Id));
        }

        var processor = services.GetRequiredService<IProcessQueuedCustody>();
        await processor.ExecuteAsync(createWorkId, CancellationToken.None);
        // A redelivered queue message is a no-op replay.
        await processor.ExecuteAsync(createWorkId, CancellationToken.None);

        var sourceAssetIds = await PhotographIdsAsync(services, memberReceiptIds);
        var custodyRootDirectory = Path.Combine(
            factory.ArtifactDirectory, "custody", "cases", record.Id.ToString("N"));
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var intake = await context.ImageIntakes
                .AsNoTracking()
                .SingleAsync(item => item.Id == record.Id);
            Assert.Equal("confirmed", intake.CustodyState);
            Assert.Equal($"cases/{record.Id:N}", intake.CustodyRootRemoteId);
            Assert.NotNull(intake.CustodyConfirmedAtUtc);
            var work = await context.ExternalWorkItems
                .AsNoTracking()
                .SingleAsync(item => item.Id == createWorkId);
            Assert.Equal("completed", work.State);
            // The Vehicle images folder holds each registered file, so each is
            // read from there rather than from a holding copy.
            Assert.All(
                await context.IntakeAssets.AsNoTracking()
                    .Where(asset => sourceAssetIds.Contains(asset.Id)).ToListAsync(),
                asset =>
                {
                    Assert.Equal("confirmed", asset.CustodyStatus);
                    Assert.Equal(intake.CustodyRootRemoteId, asset.BoxParentFolderId);
                });
        }
        // Every group image is retained under the registration, in ordinal
        // order, byte-exact.
        var firstImagePath = Path.Combine(
            custodyRootDirectory, "images", $"001-{sourceAssetIds[0]:N}", "content");
        var secondImagePath = Path.Combine(
            custodyRootDirectory, "images", $"002-{sourceAssetIds[1]:N}", "content");
        Assert.Equal(pngBytes, await File.ReadAllBytesAsync(firstImagePath));
        Assert.Equal(pngBytes, await File.ReadAllBytesAsync(secondImagePath));

        // Staff crop and tag the first photograph on the record, before any
        // Case has it.
        await services.GetRequiredService<ISavePreCaseImageCrop>().ExecuteAsync(
            new(
                sourceAssetIds[0],
                0,
                CaseAssetRotation.Clockwise90,
                new CaseAssetCrop(0.1m, 0.2m, 0.5m, 0.6m),
                StaffActor(),
                $"record-crop:{Guid.NewGuid():N}"),
            CancellationToken.None);
        await services.GetRequiredService<ITagPreCaseImage>().ExecuteAsync(
            new(
                sourceAssetIds[0],
                ImageTagVocabulary.OverviewId,
                true,
                StaffActor(),
                $"record-tag:{Guid.NewGuid():N}"),
            CancellationToken.None);

        // Merge into a formal case: the transition enqueues the fold and
        // commits regardless of external storage availability. The Case is a
        // standalone Audit, whose folder is named by its own a. Case/PO.
        var (caseId, caseRootRemoteId) = await SeedCaseWithFolderAsync(
            services, memberReceiptIds[0], "a.IMG26001", "audit");
        var mergeWorkId = await LinkAndMergeAsync(services, record, memberReceiptIds, caseId);

        // The fold uploads nothing, so nobody holds the bytes: once it commits,
        // each photograph it recorded is published to the read cache from the
        // copy intake retained.
        var foldPublisher = new RecordingCachePublisher();
        var mergeProcessor = new EfQueuedCustodyProcessor(
            contextFactory,
            services.GetRequiredService<IExternalWorkStore>(),
            services.GetRequiredService<ICaseCustody>(),
            services.GetRequiredService<TimeProvider>(),
            foldPublisher);
        await mergeProcessor.ExecuteAsync(mergeWorkId, CancellationToken.None);
        await mergeProcessor.ExecuteAsync(mergeWorkId, CancellationToken.None);

        // Each photograph's filing has its own key under the fold's, so filing
        // the same photographs again records nothing twice.
        await using (var refiling = await contextFactory.CreateDbContextAsync())
        {
            Assert.Equal(
                2,
                await EfQueuedCustodyProcessor.RecordFoldedFilesAsync(
                    refiling,
                    await refiling.ImageIntakes.SingleAsync(item => item.Id == record.Id),
                    await refiling.Cases.SingleAsync(item => item.Id == caseId),
                    $"image-case-custody-merge:{record.Id:N}",
                    await refiling.IntakeAssets.Where(asset => sourceAssetIds.Contains(asset.Id)).ToListAsync(),
                    services.GetRequiredService<TimeProvider>().GetUtcNow(),
                    CancellationToken.None));
            await refiling.SaveChangesAsync();
        }

        await using (var assertContext = await contextFactory.CreateDbContextAsync())
        {
            var intake = await assertContext.ImageIntakes
                .AsNoTracking()
                .SingleAsync(item => item.Id == record.Id);
            Assert.Equal("merged", intake.CustodyState);
            Assert.NotNull(intake.CustodyMergedAtUtc);
            var work = await assertContext.ExternalWorkItems
                .AsNoTracking()
                .SingleAsync(item => item.Id == mergeWorkId);
            Assert.Equal("completed", work.State);
            Assert.Equal(
                1,
                await assertContext.CaseHistory
                    .AsNoTracking()
                    .CountAsync(item => item.CaseId == caseId
                        && item.EventType == "image_custody_merged"));
            // The fold moved the files into the Case folder: they are read from there now.
            Assert.All(
                await assertContext.IntakeAssets.AsNoTracking()
                    .Where(asset => sourceAssetIds.Contains(asset.Id)).ToListAsync(),
                asset => Assert.Equal(caseRootRemoteId, asset.BoxParentFolderId));
        }
        // The contents moved into the case's location and the emptied
        // image-case folder is gone.
        Assert.False(Directory.Exists(custodyRootDirectory));
        var caseImagesDirectory = Path.Combine(
            factory.ArtifactDirectory,
            "custody",
            "cases",
            caseId.ToString("N"),
            "images");
        Assert.Equal(
            pngBytes,
            await File.ReadAllBytesAsync(Path.Combine(
                caseImagesDirectory, $"001-{sourceAssetIds[0]:N}", "content")));
        Assert.Equal(
            pngBytes,
            await File.ReadAllBytesAsync(Path.Combine(
                caseImagesDirectory, $"002-{sourceAssetIds[1]:N}", "content")));

        // The photographs are Case images (operator, 27 September 2026): one
        // image document each, under the next Case document numbers, stored
        // and in the report. The replayed fold added nothing.
        Guid[] occurrenceIds;
        Guid[] versionIds;
        await using (var filedContext = await contextFactory.CreateDbContextAsync())
        {
            var filed = await (
                    from occurrence in filedContext.Set<DocumentOccurrenceEntity>().AsNoTracking()
                    join version in filedContext.Set<DocumentVersionEntity>().AsNoTracking()
                        on occurrence.VersionId equals version.Id
                    where occurrence.CaseId == caseId
                    orderby occurrence.Ordinal
                    select new { Occurrence = occurrence, Version = version })
                .ToListAsync();
            Assert.Equal([2, 3], filed.Select(file => file.Occurrence.Ordinal));
            Assert.Equal(["overview.png", "close-up.png"], filed.Select(file => file.Version.FileName));
            Assert.Equal(
                sourceAssetIds.Select(assetId =>
                    $"image-case-custody-merge:{record.Id:N}:photograph:{assetId:N}"),
                filed.Select(file => file.Occurrence.OperationKey));
            Assert.All(filed, file =>
            {
                Assert.Equal(DocumentSemanticRole.Image, file.Occurrence.SemanticRole);
                Assert.Equal(DocumentSource.Intake, file.Occurrence.Source);
                Assert.True(file.Occurrence.InReport);
                Assert.Equal(DocumentCustodyStatus.Confirmed, file.Version.CustodyStatus);
                Assert.False(string.IsNullOrWhiteSpace(file.Version.BoxFileId));
                Assert.False(string.IsNullOrWhiteSpace(file.Version.BoxVersionId));
            });

            // The crop, the rotation and the tag made on the record came with
            // the first photograph; the second arrived as it was.
            Assert.Equal(90, filed[0].Occurrence.RotationDegrees);
            Assert.Equal(0.1m, filed[0].Occurrence.CropLeft);
            Assert.Equal(0.2m, filed[0].Occurrence.CropTop);
            Assert.Equal(0.5m, filed[0].Occurrence.CropWidth);
            Assert.Equal(0.6m, filed[0].Occurrence.CropHeight);
            Assert.Null(filed[1].Occurrence.CropLeft);
            occurrenceIds = [.. filed.Select(file => file.Occurrence.Id)];
            versionIds = [.. filed.Select(file => file.Version.Id)];

            // Each version the fold recorded was published once, under its own
            // identity, from its asset's retained copy. The replayed fold recorded
            // nothing and published nothing.
            var published = foldPublisher.Published;
            Assert.Equal(2, published.Count);
            var assets = await filedContext.IntakeAssets.AsNoTracking()
                .Where(asset => sourceAssetIds.Contains(asset.Id)).ToListAsync();
            for (var index = 0; index < filed.Count; index++)
            {
                var asset = assets.Single(item => item.Id == sourceAssetIds[index]);
                var copy = Assert.Single(published, item =>
                    item.Key == DocumentContentCacheKey.ForVersion(filed[index].Version.Id));
                Assert.Equal(asset.StorageKey, copy.StorageKey);
                Assert.Equal(filed[index].Version.Sha256, copy.Sha256);
                Assert.Equal(filed[index].Version.ContentLength, copy.ContentLength);
            }
            var tag = Assert.Single(await filedContext.Set<DocumentOccurrenceTagEntity>().AsNoTracking()
                .Where(item => occurrenceIds.Contains(item.OccurrenceId))
                .ToListAsync());
            Assert.Equal(occurrenceIds[0], tag.OccurrenceId);
            Assert.Equal(ImageTagVocabulary.OverviewId, tag.TagId);
        }

        // The Case reads each image from where the fold left it.
        for (var index = 0; index < occurrenceIds.Length; index++)
        {
            await using var download = Assert.IsType<DocumentDownload>(
                await services.GetRequiredService<IDownloadCaseDocument>().ExecuteAsync(
                    new(caseId, occurrenceIds[index], versionIds[index], StaffActor(), $"read-folded:{Guid.NewGuid():N}"),
                    CancellationToken.None));
            using var buffer = new MemoryStream();
            await download.Content.CopyToAsync(buffer);
            Assert.Equal(pngBytes, buffer.ToArray());
        }

        // The Overview came from the record, and it is the one image the
        // report needs (operator, 7 October 2026), so no image is asked for.
        var preparations = services.GetRequiredService<ICaseAssetPreparationQueries>();
        Assert.Empty(DocumentCustodyDurabilityTests.ImageBlockers(
            await preparations.ListForCaseAsync(caseId, CancellationToken.None)));
    }

    /// <summary>
    /// The fold is system work, which keeps the session (FRD-14): under a
    /// member of staff's edit lease it files the photographs on its first
    /// attempt and completes the Case's images, the editor keeps the lease,
    /// their heartbeat answers the version the fold moved the Case to, and
    /// their save at the version they loaded lands. The same holds for an
    /// editor already there and for one arriving while the files move.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AFoldCompletesUnderAStaffEditLeaseAndTheEditorsSaveAtTheReadVersionLands(
        bool editorArrivesWhileTheFilesMove)
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var processor = services.GetRequiredService<IProcessQueuedCustody>();
        var (record, memberReceiptIds) = await RegisterTwoPhotographsAsync(factory, client, services);
        await processor.ExecuteAsync(
            await WorkIdAsync(services, record.Id, ExternalWorkKinds.CreateImageCaseCustody),
            CancellationToken.None);
        var (caseId, _) = await SeedCaseWithFolderAsync(
            services, memberReceiptIds[0], "IMG26003", imagesComplete: false);
        var mergeWorkId = await LinkAndMergeAsync(services, record, memberReceiptIds, caseId);

        var leases = services.GetRequiredService<ILeaseCaseForEdit>();
        CaseEditLease? editor = null;
        async Task EditorArrivesAsync()
        {
            var workflow = await services.GetRequiredService<ICaseWorkflowQueries>()
                .GetAsync(caseId, CancellationToken.None);
            editor = await leases.ClaimAsync(
                new(caseId, workflow!.Version, StaffActor(), $"editor-lease:{Guid.NewGuid():N}"),
                CancellationToken.None);
        }

        IProcessQueuedCustody fold = processor;
        if (editorArrivesWhileTheFilesMove)
        {
            fold = new EfQueuedCustodyProcessor(
                contextFactory,
                services.GetRequiredService<IExternalWorkStore>(),
                new CustodyWithAnArrivingEditor(services.GetRequiredService<ICaseCustody>(), EditorArrivesAsync),
                services.GetRequiredService<TimeProvider>());
        }
        else
        {
            await EditorArrivesAsync();
        }

        await fold.ExecuteAsync(mergeWorkId, CancellationToken.None);

        Assert.False(Directory.Exists(Path.Combine(
            factory.ArtifactDirectory, "custody", "cases", record.Id.ToString("N"))));
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var work = await context.ExternalWorkItems.AsNoTracking().SingleAsync(item => item.Id == mergeWorkId);
            Assert.Equal("completed", work.State);
            Assert.Equal(1, work.AttemptCount);
            Assert.Equal("merged", await ReadImageCustodyStateAsync(context, record.Id));
            // The fold moved the Case one version on and left the editor's lease standing.
            var workflow = await context.CaseWorkflows.AsNoTracking()
                .Include(item => item.Case)
                .SingleAsync(item => item.CaseId == caseId);
            Assert.True(workflow.Case.ImagesComplete);
            Assert.Equal(editor!.Version + 1, workflow.Version);
            Assert.Equal(StaffActor().SubjectId, workflow.EditLeaseHolder);
            Assert.False(string.IsNullOrWhiteSpace(workflow.EditLeaseTokenHash));
            Assert.Equal(editor.ExpiresAtUtc, workflow.EditLeaseExpiresAtUtc);
            var images = await context.Set<DocumentOccurrenceEntity>().AsNoTracking()
                .Where(item => item.CaseId == caseId && item.SemanticRole == DocumentSemanticRole.Image)
                .ToListAsync();
            Assert.Equal(2, images.Count);
            foreach (var image in images)
            {
                await using var download = Assert.IsType<DocumentDownload>(
                    await services.GetRequiredService<IDownloadCaseDocument>().ExecuteAsync(
                        new(caseId, image.Id, image.VersionId, StaffActor(), $"read-folded:{Guid.NewGuid():N}"),
                        CancellationToken.None));
                using var buffer = new MemoryStream();
                await download.Content.CopyToAsync(buffer);
                Assert.Equal(PngBytes, buffer.ToArray());
            }
        }

        var beat = await leases.HeartbeatAsync(new(caseId, StaffActor(), editor!.Token), CancellationToken.None);
        Assert.Equal(editor.Token, beat.Token);
        Assert.Equal(editor.Version + 1, beat.Version);
        var saved = await services.GetRequiredService<ICaseWorkspaceStore>().SaveAsync(
            new SaveCaseWorkspaceRequest(
                caseId,
                editor.Version,
                StaffActor(),
                $"fold-editor-save:{Guid.NewGuid():N}",
                "Recorded the damage",
                editor.Token)
            {
                Damage = new([new(["front"], "light", "Scuffed")], null)
            },
            CancellationToken.None);
        Assert.False(saved.WasReplay);
        Assert.Equal(editor.Version + 2, saved.Version);
    }

    /// <summary>
    /// A record merged into an accepted Case before the Case's folder is
    /// stored (a.QDOS26108): the merge creates the fold already waiting, and
    /// a run before the folder lands spends no attempt and records no
    /// failure. With the Case's create not yet running the fold waits
    /// FolderWait; with it running the fold waits out that create's lease.
    /// </summary>
    [Fact]
    public async Task AFoldBeforeTheCaseFolderDefersWithoutSpendingAnAttempt()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "FOLD-DEFER-01");
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var processor = services.GetRequiredService<IProcessQueuedCustody>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var (record, memberReceiptIds) = await RegisterTwoPhotographsAsync(factory, client, services);
        await processor.ExecuteAsync(
            await WorkIdAsync(services, record.Id, ExternalWorkKinds.CreateImageCaseCustody),
            CancellationToken.None);
        var mergeWorkId = await LinkAndMergeAsync(services, record, memberReceiptIds, caseId);
        await AssertDueAsync(contextFactory, mergeWorkId, now + ImageCustodyRetryPolicy.FolderWait);

        await processor.ExecuteAsync(mergeWorkId, CancellationToken.None);
        await AssertTheFoldWaitedAsync(
            contextFactory, mergeWorkId, record.Id, "confirmed", now + ImageCustodyRetryPolicy.FolderWait);

        var createLeaseExpiresAtUtc = now.AddMinutes(4);
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.ExternalWorkItems
                .Where(item => item.CaseId == caseId && item.Kind == ExternalWorkKinds.CreateCaseCustody)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.State, "processing")
                    .SetProperty(item => item.LeaseToken, "a-worker-is-storing-the-case-folder")
                    .SetProperty(item => item.LeaseExpiresAtUtc, createLeaseExpiresAtUtc));
        }
        await processor.ExecuteAsync(mergeWorkId, CancellationToken.None);
        await AssertTheFoldWaitedAsync(contextFactory, mergeWorkId, record.Id, "confirmed", createLeaseExpiresAtUtc);
    }

    /// <summary>
    /// The Case custody that stores the Case's folder makes the waiting fold
    /// due and publishes it, and the fold then files the photographs on its
    /// first attempt.
    /// </summary>
    [Fact]
    public async Task CaseCustodyConfirmationWakesAPendingFold()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", "FOLD-WAKE-01");
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var processor = services.GetRequiredService<IProcessQueuedCustody>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var (record, memberReceiptIds) = await RegisterTwoPhotographsAsync(factory, client, services);
        await processor.ExecuteAsync(
            await WorkIdAsync(services, record.Id, ExternalWorkKinds.CreateImageCaseCustody),
            CancellationToken.None);
        var mergeWorkId = await LinkAndMergeAsync(services, record, memberReceiptIds, caseId);
        await AssertDueAsync(contextFactory, mergeWorkId, now + ImageCustodyRetryPolicy.FolderWait);
        Guid caseCustodyWorkId;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            caseCustodyWorkId = await context.ExternalWorkItems.AsNoTracking()
                .Where(item => item.CaseId == caseId && item.Kind == ExternalWorkKinds.CreateCaseCustody)
                .Select(item => item.Id)
                .SingleAsync();
        }

        var publisher = new RecordingWorkPublisher();
        await ProcessorWith(services, services.GetRequiredService<ICaseCustody>(), publisher)
            .ExecuteAsync(caseCustodyWorkId, CancellationToken.None);
        Assert.Equal(mergeWorkId, Assert.Single(publisher.Published));
        await AssertDueAsync(contextFactory, mergeWorkId, now);

        await processor.ExecuteAsync(mergeWorkId, CancellationToken.None);
        await AssertTheFoldFiledOnItsFirstAttemptAsync(contextFactory, mergeWorkId, record.Id, caseId);
    }

    /// <summary>
    /// A record merged before its own folder is stored, whose create a Box
    /// failure put off for an hour: the fold waits until that create's next
    /// attempt and spends no attempt, and the record's custody makes the fold
    /// due and publishes it when it stores the folder.
    /// </summary>
    [Fact]
    public async Task TheRecordsCustodyWakesAFoldThatWaitedForTheRecordsFolder()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var processor = services.GetRequiredService<IProcessQueuedCustody>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var (record, memberReceiptIds) = await RegisterTwoPhotographsAsync(factory, client, services);
        var createWorkId = await WorkIdAsync(services, record.Id, ExternalWorkKinds.CreateImageCaseCustody);
        var createDueAtUtc = now.AddHours(1);
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.ExternalWorkItems
                .Where(item => item.Id == createWorkId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.DueAtUtc, createDueAtUtc));
        }
        var (caseId, _) = await SeedCaseWithFolderAsync(services, memberReceiptIds[0], "IMG26010");
        var mergeWorkId = await LinkAndMergeAsync(services, record, memberReceiptIds, caseId);
        await AssertDueAsync(contextFactory, mergeWorkId, now + ImageCustodyRetryPolicy.FolderWait);

        await processor.ExecuteAsync(mergeWorkId, CancellationToken.None);
        await AssertTheFoldWaitedAsync(contextFactory, mergeWorkId, record.Id, "pending", createDueAtUtc);

        var publisher = new RecordingWorkPublisher();
        await ProcessorWith(services, services.GetRequiredService<ICaseCustody>(), publisher)
            .ExecuteAsync(createWorkId, CancellationToken.None);
        Assert.Equal(mergeWorkId, Assert.Single(publisher.Published));
        await AssertDueAsync(contextFactory, mergeWorkId, now);

        await processor.ExecuteAsync(mergeWorkId, CancellationToken.None);
        await AssertTheFoldFiledOnItsFirstAttemptAsync(contextFactory, mergeWorkId, record.Id, caseId);
    }

    /// <summary>
    /// A record merged before either folder is stored: the custody that
    /// stores the first folder leaves the fold waiting, and the custody that
    /// stores the second makes it due and publishes it, so the fold files on
    /// its first run.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheSecondFolderStoredWakesAFoldThatWaitedForBoth(bool caseFolderFirst)
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var caseId = await ImageIntakeTestData.SeedInstructionCaseAsync(
            factory, client, "AB12 CDE", caseFolderFirst ? "FOLD-BOTH-01" : "FOLD-BOTH-02");
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var (record, memberReceiptIds) = await RegisterTwoPhotographsAsync(factory, client, services);
        var recordCreateWorkId = await WorkIdAsync(services, record.Id, ExternalWorkKinds.CreateImageCaseCustody);
        var mergeWorkId = await LinkAndMergeAsync(services, record, memberReceiptIds, caseId);
        Guid caseCreateWorkId;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            caseCreateWorkId = await context.ExternalWorkItems.AsNoTracking()
                .Where(item => item.CaseId == caseId && item.Kind == ExternalWorkKinds.CreateCaseCustody)
                .Select(item => item.Id)
                .SingleAsync();
        }
        var (firstCreate, secondCreate) = caseFolderFirst
            ? (caseCreateWorkId, recordCreateWorkId)
            : (recordCreateWorkId, caseCreateWorkId);
        var publisher = new RecordingWorkPublisher();
        var custody = ProcessorWith(services, services.GetRequiredService<ICaseCustody>(), publisher);

        await custody.ExecuteAsync(firstCreate, CancellationToken.None);
        await AssertDueAsync(contextFactory, mergeWorkId, now + ImageCustodyRetryPolicy.FolderWait);

        await custody.ExecuteAsync(secondCreate, CancellationToken.None);
        Assert.Equal(mergeWorkId, Assert.Single(publisher.Published));
        await AssertDueAsync(contextFactory, mergeWorkId, now);

        await services.GetRequiredService<IProcessQueuedCustody>()
            .ExecuteAsync(mergeWorkId, CancellationToken.None);
        await AssertTheFoldFiledOnItsFirstAttemptAsync(contextFactory, mergeWorkId, record.Id, caseId);
    }

    /// <summary>
    /// A record whose own custody failed for good has no folder coming, so
    /// its fold keeps the failure path: the attempt counts and the fold
    /// re-arms with the image custody backoff.
    /// </summary>
    [Fact]
    public async Task AFoldWhoseImageCustodyFailedKeepsItsFailurePath()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var (record, memberReceiptIds) = await RegisterTwoPhotographsAsync(factory, client, services);
        var createWorkId = await WorkIdAsync(services, record.Id, ExternalWorkKinds.CreateImageCaseCustody);
        await Assert.ThrowsAsync<InvalidDataException>(() => ProcessorWith(
                services,
                new FailingCustody(() => new InvalidDataException("The retained source failed its integrity check.")))
            .ExecuteAsync(createWorkId, CancellationToken.None));
        var (caseId, _) = await SeedCaseWithFolderAsync(services, memberReceiptIds[0], "IMG26011");
        var mergeWorkId = await LinkAndMergeAsync(services, record, memberReceiptIds, caseId);

        await Assert.ThrowsAsync<IOException>(() => services.GetRequiredService<IProcessQueuedCustody>()
            .ExecuteAsync(mergeWorkId, CancellationToken.None));

        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Equal("failed", await ReadImageCustodyStateAsync(context, record.Id));
        var fold = await context.ExternalWorkItems.AsNoTracking().SingleAsync(item => item.Id == mergeWorkId);
        Assert.Equal("pending", fold.State);
        Assert.Equal(1, fold.AttemptCount);
        Assert.Equal("custody_dependency_failure", fold.FailureCode);
        Assert.Equal(
            now + ImageCustodyRetryPolicy.NextAttemptDelay(1, "custody_dependency_failure")!.Value,
            fold.DueAtUtc);
    }

    /// <summary>
    /// A member of staff's save that commits between the fold's read and its
    /// save wins the version race. The fold reads the Case again and files on
    /// the same attempt, one version after the editor's save.
    /// </summary>
    [Fact]
    public async Task AFoldThatLosesTheVersionRaceToAStaffSaveRetriesAndFiles()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var (record, memberReceiptIds) = await RegisterTwoPhotographsAsync(factory, client, services);
        await services.GetRequiredService<IProcessQueuedCustody>().ExecuteAsync(
            await WorkIdAsync(services, record.Id, ExternalWorkKinds.CreateImageCaseCustody),
            CancellationToken.None);
        var (caseId, _) = await SeedCaseWithFolderAsync(services, memberReceiptIds[0], "IMG26012");
        var mergeWorkId = await LinkAndMergeAsync(services, record, memberReceiptIds, caseId);
        long versionBeforeTheFold;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            versionBeforeTheFold = await context.CaseWorkflows.AsNoTracking()
                .Where(item => item.CaseId == caseId)
                .Select(item => item.Version)
                .SingleAsync();
        }

        var raced = new StaffSaveDuringTheFoldsCompletion(contextFactory, caseId);
        await new EfQueuedCustodyProcessor(
                raced,
                services.GetRequiredService<IExternalWorkStore>(),
                services.GetRequiredService<ICaseCustody>(),
                services.GetRequiredService<TimeProvider>())
            .ExecuteAsync(mergeWorkId, CancellationToken.None);

        Assert.Equal(1, raced.Saves);
        await AssertTheFoldFiledOnItsFirstAttemptAsync(contextFactory, mergeWorkId, record.Id, caseId);
        await using var after = await contextFactory.CreateDbContextAsync();
        Assert.Equal(
            versionBeforeTheFold + 2,
            await after.CaseWorkflows.AsNoTracking()
                .Where(item => item.CaseId == caseId)
                .Select(item => item.Version)
                .SingleAsync());
        Assert.Equal(1, await after.CaseHistory.CountAsync(item =>
            item.CaseId == caseId && item.EventType == "image_custody_merged"));
    }

    /// <summary>
    /// Photographs that arrive by merge complete the Case's images when the
    /// fold files them (FRD-13). A Not ready Case with nothing else missing
    /// moves to Review, as it does when a linked message's photographs are
    /// filed; one still missing instructions stays Not ready and chased.
    /// </summary>
    [Theory]
    [InlineData(true, nameof(CaseLifecycleState.Review), nameof(CaseDueWorkState.Stopped))]
    [InlineData(false, nameof(CaseLifecycleState.NotReady), nameof(CaseDueWorkState.Scheduled))]
    public async Task TheFoldCompletesTheCasesImagesAndItsReadinessDecidesReview(
        bool instructionComplete,
        string expectedState,
        string expectedChase)
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var processor = services.GetRequiredService<IProcessQueuedCustody>();
        var (record, memberReceiptIds) = await RegisterTwoPhotographsAsync(factory, client, services);
        await processor.ExecuteAsync(
            await WorkIdAsync(services, record.Id, ExternalWorkKinds.CreateImageCaseCustody),
            CancellationToken.None);
        var (caseId, _) = await SeedCaseWithFolderAsync(
            services, memberReceiptIds[0], "IMG26004", instructionComplete: instructionComplete, imagesComplete: false);
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            // A Not ready Case is being chased for what it is missing.
            context.CaseDueWork.Add(new()
            {
                CaseId = caseId,
                MissingMaterialReason = "Case completeness is not confirmed",
                State = nameof(CaseDueWorkState.Scheduled),
                NextChaseAtUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().AddDays(7)
            });
            await context.SaveChangesAsync();
        }
        var mergeWorkId = await LinkAndMergeAsync(services, record, memberReceiptIds, caseId);
        long versionBeforeTheFold;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var linked = await context.CaseWorkflows.AsNoTracking()
                .Include(item => item.Case)
                .SingleAsync(item => item.CaseId == caseId);
            // Neither the link nor the merge completed the images.
            Assert.False(linked.Case.ImagesComplete);
            Assert.Equal(nameof(CaseLifecycleState.NotReady), linked.State);
            versionBeforeTheFold = linked.Version;
        }

        await processor.ExecuteAsync(mergeWorkId, CancellationToken.None);
        await processor.ExecuteAsync(mergeWorkId, CancellationToken.None);

        await using var after = await contextFactory.CreateDbContextAsync();
        var workflow = await after.CaseWorkflows.AsNoTracking()
            .Include(item => item.Case)
            .Include(item => item.DueWork)
            .SingleAsync(item => item.CaseId == caseId);
        Assert.True(workflow.Case.ImagesComplete);
        Assert.Equal(expectedState, workflow.State);
        Assert.Equal(versionBeforeTheFold + 1, workflow.Version);
        Assert.Equal(expectedChase, workflow.DueWork!.State);
        Assert.Equal(
            instructionComplete,
            (await after.CaseDataSnapshots.AsNoTracking().SingleAsync(item => item.WorkId == caseId))
                .CompletenessPolicySatisfied);
        // Recorded once, under the fold's version, with the policy it used.
        var completion = Assert.Single(await after.CaseWorkflowEvents.AsNoTracking()
            .Where(item => item.CaseId == caseId
                && item.EventType == "case_images_completed_from_merged_photographs")
            .ToListAsync());
        Assert.Equal(workflow.Version, completion.AfterVersion);
        Assert.Equal(nameof(ActorKind.SystemWorker), completion.ActorKind);
        Assert.False(string.IsNullOrWhiteSpace(
            (await after.ActionHistory.AsNoTracking()
                .SingleAsync(item => item.CorrelationId == completion.OperationKey)).PolicyVersion));
    }

    /// <summary>
    /// The fold files exactly the photographs it moved. A photograph the
    /// record shows that its folder never held, here one kept in holding,
    /// has no file in the Case folder and is not filed.
    /// </summary>
    [Fact]
    public async Task TheFoldFilesOnlyThePhotographsTheRecordsFolderHeld()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var processor = services.GetRequiredService<IProcessQueuedCustody>();
        var (record, memberReceiptIds) = await RegisterTwoPhotographsAsync(factory, client, services);
        var photographIds = await PhotographIdsAsync(services, memberReceiptIds);
        // The second member was routed elsewhere when the folder was stored,
        // and its photograph is held in the holding folder.
        await SetDecisionAsync(contextFactory, memberReceiptIds[1], IntakeDecision.NeedsSorting);
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            (await context.IntakeAssets.SingleAsync(asset => asset.Id == photographIds[1]))
                .ConfirmCustody("holding-file", "holding-version", "holding-folder");
            await context.SaveChangesAsync();
        }
        await processor.ExecuteAsync(
            await WorkIdAsync(services, record.Id, ExternalWorkKinds.CreateImageCaseCustody),
            CancellationToken.None);
        await SetDecisionAsync(contextFactory, memberReceiptIds[1], IntakeDecision.ImageIntakeRegistered);
        Assert.Equal(
            2,
            (await services.GetRequiredService<IImageIntakeQueries>()
                .ListImagesAsync(record.Id, CancellationToken.None)).Count);

        var (caseId, caseRootRemoteId) = await SeedCaseWithFolderAsync(services, memberReceiptIds[0], "IMG26005");
        var mergeWorkId = await LinkAndMergeAsync(services, record, memberReceiptIds, caseId);
        await processor.ExecuteAsync(mergeWorkId, CancellationToken.None);

        await using var after = await contextFactory.CreateDbContextAsync();
        var filed = Assert.Single(await after.Set<DocumentOccurrenceEntity>().AsNoTracking()
            .Where(item => item.CaseId == caseId)
            .ToListAsync());
        Assert.Equal(
            $"image-case-custody-merge:{record.Id:N}:photograph:{photographIds[0]:N}",
            filed.OperationKey);
        var assets = await after.IntakeAssets.AsNoTracking()
            .Where(asset => photographIds.Contains(asset.Id))
            .ToDictionaryAsync(asset => asset.Id);
        Assert.Equal(caseRootRemoteId, assets[photographIds[0]].BoxParentFolderId);
        Assert.Equal("holding-folder", assets[photographIds[1]].BoxParentFolderId);
        Assert.Equal("holding-file", assets[photographIds[1]].BoxFileId);
    }

    /// <summary>
    /// A report PDF registered as a Vehicle images record with the photographs
    /// that came with it (#901): the fold files the PDF itself on the Audit as
    /// a document beside the photographs, under the file the fold moved, so
    /// staff can mark it as the original report. A replay files nothing twice.
    /// </summary>
    [Fact]
    public async Task TheFoldFilesTheRecordsSourcePdfAsADocumentBesideItsPhotographs()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var processor = services.GetRequiredService<IProcessQueuedCustody>();
        var (record, memberReceiptIds) = await RegisterTwoPhotographsAsync(factory, client, services);
        var sourceIds = await PhotographIdsAsync(services, memberReceiptIds);
        // The first member is the report the photographs came with.
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var receipt = await context.IntakeReceipts.SingleAsync(item => item.Id == memberReceiptIds[0]);
            receipt.SourceFileName = "report.pdf";
            receipt.MediaType = "application/pdf";
            var source = await context.IntakeAssets.SingleAsync(asset => asset.Id == sourceIds[0]);
            source.FileName = "report.pdf";
            source.MediaType = "application/pdf";
            await context.SaveChangesAsync();
        }
        await processor.ExecuteAsync(
            await WorkIdAsync(services, record.Id, ExternalWorkKinds.CreateImageCaseCustody),
            CancellationToken.None);

        var (caseId, caseRootRemoteId) = await SeedCaseWithFolderAsync(
            services, memberReceiptIds[0], "a.IMG26009", "audit");
        var mergeWorkId = await LinkAndMergeAsync(services, record, memberReceiptIds, caseId);
        await processor.ExecuteAsync(mergeWorkId, CancellationToken.None);
        await using (var refiling = await contextFactory.CreateDbContextAsync())
        {
            Assert.Equal(
                1,
                await EfQueuedCustodyProcessor.RecordFoldedFilesAsync(
                    refiling,
                    await refiling.ImageIntakes.SingleAsync(item => item.Id == record.Id),
                    await refiling.Cases.SingleAsync(item => item.Id == caseId),
                    $"image-case-custody-merge:{record.Id:N}",
                    await refiling.IntakeAssets.Where(asset => sourceIds.Contains(asset.Id)).ToListAsync(),
                    services.GetRequiredService<TimeProvider>().GetUtcNow(),
                    CancellationToken.None));
            await refiling.SaveChangesAsync();
        }

        await using var after = await contextFactory.CreateDbContextAsync();
        var filed = await (from occurrence in after.Set<DocumentOccurrenceEntity>().AsNoTracking()
                           join version in after.Set<DocumentVersionEntity>().AsNoTracking()
                               on occurrence.VersionId equals version.Id
                           where occurrence.CaseId == caseId
                           orderby occurrence.Ordinal
                           select new { occurrence.OperationKey, occurrence.SemanticRole, version.FileName, version.BoxFileId })
            .ToListAsync();
        Assert.Equal(
            [
                $"image-case-custody-merge:{record.Id:N}:document:{sourceIds[0]:N}",
                $"image-case-custody-merge:{record.Id:N}:photograph:{sourceIds[1]:N}"
            ],
            filed.Select(item => item.OperationKey));
        Assert.Equal(DocumentSemanticRole.OriginalSource, filed[0].SemanticRole);
        Assert.Equal("report.pdf", filed[0].FileName);
        Assert.Equal(DocumentSemanticRole.Image, filed[1].SemanticRole);
        var report = await after.IntakeAssets.AsNoTracking().SingleAsync(asset => asset.Id == sourceIds[0]);
        Assert.Equal(caseRootRemoteId, report.BoxParentFolderId);
        Assert.Equal(report.BoxFileId, filed[0].BoxFileId);
    }

    /// <summary>
    /// On the fold path a photograph tagged Third party on the record arrives
    /// out of the report, the Case's current report is marked stale, and the
    /// report preview reads the photograph that is in the report from where
    /// the fold left it.
    /// </summary>
    [Fact]
    public async Task AFoldedPhotographKeepsItsTagStalesTheReportAndIsReadByThePreview()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var processor = services.GetRequiredService<IProcessQueuedCustody>();
        var (record, memberReceiptIds) = await RegisterTwoPhotographsAsync(factory, client, services);
        var photographIds = await PhotographIdsAsync(services, memberReceiptIds);
        await processor.ExecuteAsync(
            await WorkIdAsync(services, record.Id, ExternalWorkKinds.CreateImageCaseCustody),
            CancellationToken.None);
        await services.GetRequiredService<ITagPreCaseImage>().ExecuteAsync(
            new(
                photographIds[0],
                ImageTagVocabulary.ThirdPartyId,
                true,
                StaffActor(),
                $"record-third-party:{Guid.NewGuid():N}"),
            CancellationToken.None);

        var (caseId, _) = await SeedCaseWithFolderAsync(services, memberReceiptIds[0], "IMG26006");
        var generationId = Guid.NewGuid();
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            context.Set<CaseReportGenerationEntity>().Add(new()
            {
                Id = generationId,
                CaseId = caseId,
                WorkId = caseId,
                CaseVersion = 0,
                SnapshotHash = new string('6', 64),
                SnapshotJson = ReportGenerationSnapshotFixture.Json(caseId, "fold-stales-the-report"),
                TemplateVersion = "assessment-report/v1",
                RendererVersion = "renderer/v1",
                State = nameof(CaseReportGenerationState.Confirmed),
                GeneratedAtUtc = services.GetRequiredService<TimeProvider>().GetUtcNow(),
                Version = 1,
            });
            await context.SaveChangesAsync();
        }
        var mergeWorkId = await LinkAndMergeAsync(services, record, memberReceiptIds, caseId);
        await processor.ExecuteAsync(mergeWorkId, CancellationToken.None);

        Guid inReportOccurrenceId;
        await using (var after = await contextFactory.CreateDbContextAsync())
        {
            var images = await after.Set<DocumentOccurrenceEntity>().AsNoTracking()
                .Where(item => item.CaseId == caseId)
                .OrderBy(item => item.Ordinal)
                .ToListAsync();
            Assert.Equal(2, images.Count);
            Assert.False(images[0].InReport);
            Assert.True(images[1].InReport);
            inReportOccurrenceId = images[1].Id;
            var imageIds = images.Select(image => image.Id).ToArray();
            var tag = Assert.Single(await after.Set<DocumentOccurrenceTagEntity>().AsNoTracking()
                .Where(item => imageIds.Contains(item.OccurrenceId))
                .ToListAsync());
            Assert.Equal(images[0].Id, tag.OccurrenceId);
            Assert.Equal(ImageTagVocabulary.ThirdPartyId, tag.TagId);
            Assert.Equal(
                nameof(CaseReportGenerationState.Stale),
                (await after.Set<CaseReportGenerationEntity>().AsNoTracking()
                    .SingleAsync(item => item.Id == generationId)).State);
        }

        var preview = await services.GetRequiredService<IAssessmentReportProjectionSource>()
            .GetAsync(caseId, StaffActor(), CaseWorkSelector.Current);
        var photograph = Assert.Single(preview!.Photos);
        Assert.Equal(inReportOccurrenceId, photograph.OccurrenceId);
        Assert.Equal(PngBytes, await photograph.OpenAsync());
    }

    /// <summary>
    /// Two filings can take the same next Case document number. The one that
    /// loses has already moved its files, so its failure is one the image
    /// custody retry policy re-arms. Any other duplicate stays unclassified.
    /// </summary>
    [Fact]
    public async Task ADocumentNumberTakenByAnotherFilingIsRetried()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development", true, recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var receiptId = IntakeWebDriver.ReceiptId(await IntakeWebDriver.UploadAndProcessAsync(
            factory, client, "vehicle.png", "image/png", PngBytes, Guid.NewGuid().ToString("N")));
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var caseId = await SeedCaseAsync(services, receiptId, "IMG26007");

        var numberTaken = await Assert.ThrowsAsync<DbUpdateException>(
            () => AddDocumentsAsync(("first", 2), ("second", 2)));
        var code = EfQueuedCustodyProcessor.GetFailureCode(numberTaken);
        Assert.Equal("custody_dependency_failure", code);
        Assert.NotNull(ImageCustodyRetryPolicy.NextAttemptDelay(1, code));

        var otherDuplicate = await Assert.ThrowsAsync<DbUpdateException>(
            () => AddDocumentsAsync(("same", 3), ("same", 4)));
        Assert.Equal(
            "custody_unexpected_failure:DbUpdateException",
            EfQueuedCustodyProcessor.GetFailureCode(otherDuplicate));

        async Task AddDocumentsAsync(params (string Identity, int Ordinal)[] documents)
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            foreach (var (identity, ordinal) in documents)
            {
                context.Add(new CaseDocumentEntity
                {
                    Id = Guid.NewGuid(),
                    CaseId = caseId,
                    Ordinal = ordinal,
                    SourceOccurrenceIdentity = identity
                });
            }
            await context.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task StorageFailuresRearmOrRecordHonestlyAndNeverTouchTheRetainedImages()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        var upload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "vehicle.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var receiptId = IntakeWebDriver.ReceiptId(upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var resolver = services.GetRequiredService<IImageIntakeOriginResolver>();
        var register = services.GetRequiredService<IRegisterImageIntake>();
        var origin = await resolver.ResolveOriginAsync(receiptId, CancellationToken.None);
        var record = await register.ExecuteAsync(
            new(
                origin!,
                "AB12CDE",
                StaffActor(),
                "image-custody-failure-register",
                "Staff confirmed the registration from the retained image."),
            CancellationToken.None);

        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        Guid workId;
        int retainedAssetCount;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            workId = (await context.ExternalWorkItems
                .AsNoTracking()
                .SingleAsync(item => item.ImageIntakeId == record.Id)).Id;
            retainedAssetCount = await context.IntakeAssets
                .AsNoTracking()
                .CountAsync(item => item.IntakeReceiptId == receiptId);
        }
        Assert.True(retainedAssetCount > 0);

        // A dependency-shaped outage re-arms the work with backoff instead of
        // requiring staff recovery for a transient Box failure.
        var workStore = services.GetRequiredService<IExternalWorkStore>();
        var outageProcessor = new EfQueuedCustodyProcessor(
            contextFactory,
            workStore,
            new FailingCustody(() => new IOException("The storage dependency is unavailable.")),
            services.GetRequiredService<TimeProvider>());
        await Assert.ThrowsAsync<IOException>(() =>
            outageProcessor.ExecuteAsync(workId, CancellationToken.None));
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var work = await context.ExternalWorkItems
                .AsNoTracking()
                .SingleAsync(item => item.Id == workId);
            Assert.Equal("pending", work.State);
            Assert.Equal("custody_dependency_failure", work.FailureCode);
            Assert.Equal(
                "pending",
                await ReadImageCustodyStateAsync(context, record.Id));
        }

        // Once the dependency is healthy again the same pending work
        // completes; the images were never at risk because blob custody is
        // authoritative throughout.
        await services.GetRequiredService<IProcessQueuedCustody>()
            .ExecuteAsync(workId, CancellationToken.None);
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            Assert.Equal(
                "confirmed",
                await ReadImageCustodyStateAsync(context, record.Id));
            Assert.Equal(
                retainedAssetCount,
                await context.IntakeAssets
                    .AsNoTracking()
                    .CountAsync(item => item.IntakeReceiptId == receiptId));
        }

        // A non-retryable integrity failure is terminal and recorded honestly
        // on the Image intake; nothing external is invented.
        var secondUpload = await IntakeWebDriver.UploadAndProcessAsync(
            factory,
            client,
            "second-vehicle.png",
            "image/png",
            Convert.FromBase64String(MultiFormatFixture.TinyPngBase64),
            Guid.NewGuid().ToString("N"));
        var secondReceiptId = IntakeWebDriver.ReceiptId(secondUpload);
        var secondOrigin = await resolver.ResolveOriginAsync(secondReceiptId, CancellationToken.None);
        var secondRecord = await register.ExecuteAsync(
            new(
                secondOrigin!,
                "AB12CDE",
                StaffActor(),
                "image-custody-terminal-register",
                "A second arrival for the same vehicle registration."),
            CancellationToken.None);
        Guid secondWorkId;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            secondWorkId = (await context.ExternalWorkItems
                .AsNoTracking()
                .SingleAsync(item => item.ImageIntakeId == secondRecord.Id)).Id;
        }
        var integrityProcessor = new EfQueuedCustodyProcessor(
            contextFactory,
            workStore,
            new FailingCustody(() => new InvalidDataException("The retained source failed its integrity check.")),
            services.GetRequiredService<TimeProvider>());
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            integrityProcessor.ExecuteAsync(secondWorkId, CancellationToken.None));
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var work = await context.ExternalWorkItems
                .AsNoTracking()
                .SingleAsync(item => item.Id == secondWorkId);
            Assert.Equal("failed", work.State);
            Assert.Equal("source_integrity_conflict", work.FailureCode);
            Assert.Equal(
                "failed",
                await ReadImageCustodyStateAsync(context, secondRecord.Id));
        }
    }

    /// <summary>
    /// The files of one Vehicle images record are filed three at a time, never
    /// more, and each asset keeps its own ordinal-numbered file whichever
    /// finishes first. The probe holds the first three uploads until all three
    /// are in flight together, so only a concurrent loop gets past it.
    /// </summary>
    [Fact]
    public async Task TheFilesOfOneRecordAreFiledThreeAtATimeAndEachKeepsItsOrdinal()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var (record, memberReceiptIds) = await RegisterPhotographsAsync(
            factory, client, services, ["one.png", "two.png", "three.png", "four.png"]);
        var workId = await WorkIdAsync(services, record.Id, ExternalWorkKinds.CreateImageCaseCustody);
        var probe = new CustodyBatchProbe(services.GetRequiredService<ICaseCustody>(), holdUntilConcurrent: 3);

        await ProcessorWith(services, probe).ExecuteAsync(workId, CancellationToken.None);

        Assert.Equal(3, probe.MaxInFlight);
        Assert.Equal([1, 2, 3, 4], probe.Started.Order().ToArray());
        var assetIds = await PhotographIdsAsync(services, memberReceiptIds);
        await using var context = await services.GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var intake = await context.ImageIntakes.AsNoTracking().SingleAsync(item => item.Id == record.Id);
        Assert.Equal("confirmed", intake.CustodyState);
        for (var index = 0; index < assetIds.Length; index++)
        {
            var asset = await context.IntakeAssets.AsNoTracking()
                .SingleAsync(item => item.Id == assetIds[index]);
            Assert.Equal("confirmed", asset.CustodyStatus);
            Assert.Equal(
                $"{intake.CustodyRootRemoteId}/images/{index + 1:000}-{assetIds[index]:N}",
                asset.BoxFileId);
        }
    }

    /// <summary>
    /// A Vehicle images record's files are never held, so nothing else
    /// publishes their read-cache copies: once the record's filing commits,
    /// each file is published under its intake asset, which is what the record
    /// reads it by before any Case has it, from the copy intake retained. A
    /// redelivered work item publishes nothing again.
    /// </summary>
    [Fact]
    public async Task EachFileTheRecordFiledIsPublishedUnderItsAssetAfterTheFilingCommits()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var (record, memberReceiptIds) = await RegisterTwoPhotographsAsync(factory, client, services);
        var workId = await WorkIdAsync(services, record.Id, ExternalWorkKinds.CreateImageCaseCustody);
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var foldersWhenPublished = new ConcurrentQueue<string?>();
        var publisher = new RecordingCachePublisher(async key =>
        {
            await using var db = await contextFactory.CreateDbContextAsync();
            foldersWhenPublished.Enqueue(await db.IntakeAssets.AsNoTracking()
                .Where(asset => asset.Id == key.IntakeAssetId && asset.CustodyStatus == "confirmed")
                .Select(asset => asset.BoxParentFolderId)
                .SingleOrDefaultAsync());
        });
        var processor = new EfQueuedCustodyProcessor(
            contextFactory,
            services.GetRequiredService<IExternalWorkStore>(),
            services.GetRequiredService<ICaseCustody>(),
            services.GetRequiredService<TimeProvider>(),
            publisher);

        await processor.ExecuteAsync(workId, CancellationToken.None);
        await processor.ExecuteAsync(workId, CancellationToken.None);

        var photographIds = await PhotographIdsAsync(services, memberReceiptIds);
        await using var context = await contextFactory.CreateDbContextAsync();
        var intake = await context.ImageIntakes.AsNoTracking().SingleAsync(item => item.Id == record.Id);
        var filed = await context.IntakeAssets.AsNoTracking()
            .Where(asset => asset.BoxParentFolderId == intake.CustodyRootRemoteId)
            .ToListAsync();
        Assert.Subset(filed.Select(asset => asset.Id).ToHashSet(), photographIds.ToHashSet());
        var published = publisher.Published;
        Assert.Equal(filed.Count, published.Count);
        foreach (var asset in filed)
        {
            var copy = Assert.Single(published, item =>
                item.Key == DocumentContentCacheKey.ForIntakeAsset(asset.Id));
            Assert.Null(copy.Bytes);
            Assert.Equal(asset.StorageKey, copy.StorageKey);
            Assert.Equal(asset.ContentHash, copy.Sha256);
            Assert.Equal(asset.ContentLength, copy.ContentLength);
        }
        // Each was published once its asset was confirmed in the record's
        // folder, so a read at that moment already finds it there.
        Assert.All(foldersWhenPublished, folder => Assert.Equal(intake.CustodyRootRemoteId, folder));
    }

    /// <summary>
    /// A Box 429 on the second file fails the work item as it always did: the
    /// throttled exception is what surfaces, not a sibling's cancellation, and
    /// the item is re-armed for the queue to retry, with nothing confirmed.
    /// </summary>
    [Fact]
    public async Task AThrottledSecondFileFailsTheWorkItemForTheQueueToRetry()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var (record, _) = await RegisterPhotographsAsync(
            factory, client, services, ["one.png", "two.png", "three.png"]);
        var workId = await WorkIdAsync(services, record.Id, ExternalWorkKinds.CreateImageCaseCustody);
        var probe = new CustodyBatchProbe(services.GetRequiredService<ICaseCustody>(), holdUntilConcurrent: 3)
        {
            Failure = ordinal => ordinal == 2 ? new BoxThrottledException(TimeSpan.FromSeconds(1)) : null
        };

        await Assert.ThrowsAsync<BoxThrottledException>(() =>
            ProcessorWith(services, probe).ExecuteAsync(workId, CancellationToken.None));

        await using var context = await services.GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var work = await context.ExternalWorkItems.AsNoTracking().SingleAsync(item => item.Id == workId);
        Assert.Equal("pending", work.State);
        Assert.Equal("custody_dependency_failure", work.FailureCode);
        Assert.Equal("pending", await ReadImageCustodyStateAsync(context, record.Id));
        Assert.Null((await context.ImageIntakes.AsNoTracking().SingleAsync(item => item.Id == record.Id))
            .CustodyRootRemoteId);
    }

    /// <summary>
    /// The lease is five minutes and is not renewed, so a batch that outlives
    /// it must not be recorded. The lease is taken while all three files are in
    /// flight, so only the check after the batch can see it gone.
    /// </summary>
    [Fact]
    public async Task ALeaseLostWhileTheFilesAreInFlightIsDetectedAfterTheBatch()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var (record, _) = await RegisterPhotographsAsync(
            factory, client, services, ["one.png", "two.png", "three.png"]);
        var workId = await WorkIdAsync(services, record.Id, ExternalWorkKinds.CreateImageCaseCustody);
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var probe = new CustodyBatchProbe(services.GetRequiredService<ICaseCustody>(), holdUntilConcurrent: 3)
        {
            WhenAllInFlight = async () =>
            {
                await using var context = await contextFactory.CreateDbContextAsync();
                await context.ExternalWorkItems.Where(item => item.Id == workId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.State, "processing")
                        .SetProperty(item => item.LeaseToken, "newer-holder")
                        .SetProperty(item => item.LeaseExpiresAtUtc, DateTimeOffset.UtcNow.AddMinutes(5)));
            }
        };

        await Assert.ThrowsAsync<CustodyProcessingLeaseLostException>(() =>
            ProcessorWith(services, probe).ExecuteAsync(workId, CancellationToken.None));

        Assert.Equal(3, probe.MaxInFlight);
        await using var verify = await contextFactory.CreateDbContextAsync();
        var work = await verify.ExternalWorkItems.AsNoTracking().SingleAsync(item => item.Id == workId);
        Assert.Equal(("processing", "newer-holder"), (work.State, work.LeaseToken));
        Assert.NotEqual("confirmed", await ReadImageCustodyStateAsync(verify, record.Id));
    }

    /// <summary>
    /// The lease is lost once the last file is in Box, after every file's own
    /// lease check has passed. Only the check after the batch can see that,
    /// and it does: nothing is recorded, and the newer holder keeps the item.
    /// The work store writes each lease check's answer into the same timeline
    /// as the filings, so the test shows which check it was.
    /// </summary>
    [Fact]
    public async Task ALeaseLostAfterTheLastFileIsFiledIsCaughtByTheCheckAfterTheBatch()
    {
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            recognitionEngine: new FakeVrmRecognitionEngine());
        using var client = IntakeWebDriver.CreateClient(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var (record, memberReceiptIds) = await RegisterPhotographsAsync(
            factory, client, services, ["one.png", "two.png", "three.png"]);
        var workId = await WorkIdAsync(services, record.Id, ExternalWorkKinds.CreateImageCaseCustody);
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        var timeline = new ConcurrentQueue<string>();
        var filed = 0;
        var probe = new CustodyBatchProbe(services.GetRequiredService<ICaseCustody>())
        {
            WhenFiled = async _ =>
            {
                timeline.Enqueue("filed");
                if (Interlocked.Increment(ref filed) < 3)
                {
                    return;
                }
                await using var context = await contextFactory.CreateDbContextAsync();
                await context.ExternalWorkItems.Where(item => item.Id == workId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.State, "processing")
                        .SetProperty(item => item.LeaseToken, "newer-holder")
                        .SetProperty(
                            item => item.LeaseExpiresAtUtc,
                            services.GetRequiredService<TimeProvider>().GetUtcNow().AddMinutes(5)));
            }
        };
        var workStore = new LeaseCheckTimeline(services.GetRequiredService<IExternalWorkStore>(), timeline);
        var assetIds = await PhotographIdsAsync(services, memberReceiptIds);
        var assetsBefore = await AssetCustodyAsync();

        await Assert.ThrowsAsync<CustodyProcessingLeaseLostException>(() =>
            ProcessorWith(services, workStore, probe).ExecuteAsync(workId, CancellationToken.None));

        var entries = timeline.ToArray();
        Assert.Equal(3, entries.Count(entry => entry == "filed"));
        Assert.Equal("lease lost", entries[^1]);
        Assert.DoesNotContain("lease lost", entries[..^1]);
        await using var verify = await contextFactory.CreateDbContextAsync();
        var work = await verify.ExternalWorkItems.AsNoTracking().SingleAsync(item => item.Id == workId);
        Assert.Equal(("processing", "newer-holder"), (work.State, work.LeaseToken));
        var intake = await verify.ImageIntakes.AsNoTracking().SingleAsync(item => item.Id == record.Id);
        Assert.NotEqual("confirmed", intake.CustodyState);
        Assert.Null(intake.CustodyRootRemoteId);
        Assert.Equal(assetsBefore, await AssetCustodyAsync());

        async Task<string[]> AssetCustodyAsync()
        {
            await using var context = await contextFactory.CreateDbContextAsync();
            var assets = await context.IntakeAssets.AsNoTracking()
                .Where(item => assetIds.Contains(item.Id))
                .OrderBy(item => item.Id)
                .Select(item => new { item.CustodyStatus, item.BoxFileId, item.BoxVersionId })
                .ToListAsync();
            return [.. assets.Select(item => $"{item.CustodyStatus} {item.BoxFileId} {item.BoxVersionId}")];
        }
    }

    private static EfQueuedCustodyProcessor ProcessorWith(
        IServiceProvider services,
        ICaseCustody custody,
        ICommittedExternalWorkPublisher? committedPublisher = null) =>
        ProcessorWith(services, services.GetRequiredService<IExternalWorkStore>(), custody, committedPublisher);

    private static EfQueuedCustodyProcessor ProcessorWith(
        IServiceProvider services,
        IExternalWorkStore workStore,
        ICaseCustody custody,
        ICommittedExternalWorkPublisher? committedPublisher = null) => new(
        services.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
        workStore,
        custody,
        services.GetRequiredService<TimeProvider>(),
        committedPublisher: committedPublisher);

    private static async Task AssertDueAsync(
        IDbContextFactory<PegasusDbContext> contextFactory,
        Guid workId,
        DateTimeOffset dueAtUtc)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var work = await context.ExternalWorkItems.AsNoTracking().SingleAsync(item => item.Id == workId);
        Assert.Equal("pending", work.State);
        Assert.Equal(dueAtUtc, work.DueAtUtc);
    }

    /// <summary>
    /// The fold ran and went back to waiting for a folder: pending, due at
    /// <paramref name="dueAtUtc"/>, with no attempt spent and no failure
    /// recorded, and the record's custody left as it was.
    /// </summary>
    private static async Task AssertTheFoldWaitedAsync(
        IDbContextFactory<PegasusDbContext> contextFactory,
        Guid mergeWorkId,
        Guid imageIntakeId,
        string imageCustodyState,
        DateTimeOffset dueAtUtc)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var fold = await context.ExternalWorkItems.AsNoTracking().SingleAsync(item => item.Id == mergeWorkId);
        Assert.Equal("pending", fold.State);
        Assert.Equal(0, fold.AttemptCount);
        Assert.Null(fold.FailureCode);
        Assert.Equal(dueAtUtc, fold.DueAtUtc);
        Assert.Equal(imageCustodyState, await ReadImageCustodyStateAsync(context, imageIntakeId));
    }

    /// <summary>
    /// The fold completed on its first counted attempt: the record is merged
    /// and its two photographs are Case images.
    /// </summary>
    private static async Task AssertTheFoldFiledOnItsFirstAttemptAsync(
        IDbContextFactory<PegasusDbContext> contextFactory,
        Guid mergeWorkId,
        Guid imageIntakeId,
        Guid caseId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        var fold = await context.ExternalWorkItems.AsNoTracking().SingleAsync(item => item.Id == mergeWorkId);
        Assert.Equal("completed", fold.State);
        Assert.Equal(1, fold.AttemptCount);
        Assert.Equal("merged", await ReadImageCustodyStateAsync(context, imageIntakeId));
        var photographKeys = $"image-case-custody-merge:{imageIntakeId:N}:photograph:";
        Assert.Equal(2, await context.Set<DocumentOccurrenceEntity>().CountAsync(item =>
            item.CaseId == caseId
            && item.SemanticRole == DocumentSemanticRole.Image
            && item.OperationKey.StartsWith(photographKeys)));
    }

    /// <summary>The committed-work publisher, recording each work item it is given.</summary>
    private sealed class RecordingWorkPublisher : ICommittedExternalWorkPublisher
    {
        public List<Guid> Published { get; } = [];

        public Task PublishAsync(Guid workItemId, CancellationToken cancellationToken)
        {
            Published.Add(workItemId);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// The database, with a member of staff whose save of the Case commits,
    /// once, after the fold's completion has read the Case and before it saves.
    /// </summary>
    private sealed class StaffSaveDuringTheFoldsCompletion(
        IDbContextFactory<PegasusDbContext> inner,
        Guid caseId) : IDbContextFactory<PegasusDbContext>
    {
        private int saves;

        public int Saves => saves;

        public PegasusDbContext CreateDbContext()
        {
            var context = inner.CreateDbContext();
            context.SavingChanges += (_, _) =>
            {
                if (context.ChangeTracker.Entries<ExternalWorkItemEntity>().Any(entry =>
                        entry.State == EntityState.Modified
                        && entry.Entity.Kind == ExternalWorkKinds.MergeImageCaseCustody
                        && entry.Entity.State == "completed")
                    && Interlocked.Exchange(ref saves, 1) == 0)
                {
                    using var staff = inner.CreateDbContext();
                    staff.Database.ExecuteSqlInterpolated(
                        $"UPDATE CaseWorkflows SET Version = Version + 1 WHERE CaseId = {caseId}");
                }
            };
            return context;
        }
    }

    /// <summary>
    /// The work store, writing each lease check's answer into a timeline in the
    /// order the checks finish.
    /// </summary>
    private sealed class LeaseCheckTimeline(
        IExternalWorkStore inner,
        ConcurrentQueue<string> timeline) : IExternalWorkStore
    {
        public async Task<bool> HoldsProcessingLeaseAsync(
            Guid workItemId,
            string leaseToken,
            CancellationToken cancellationToken)
        {
            var holds = await inner.HoldsProcessingLeaseAsync(workItemId, leaseToken, cancellationToken);
            timeline.Enqueue(holds ? "lease held" : "lease lost");
            return holds;
        }

        public Task<ExternalWorkDispatchClaim?> ClaimDispatchAsync(
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) =>
            inner.ClaimDispatchAsync(nowUtc, leaseDuration, cancellationToken);

        public Task<ExternalWorkDispatchClaim?> ClaimDispatchAsync(
            Guid workItemId,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) =>
            inner.ClaimDispatchAsync(workItemId, nowUtc, leaseDuration, cancellationToken);

        public Task MarkDispatchedAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset dispatchedAtUtc,
            CancellationToken cancellationToken) =>
            inner.MarkDispatchedAsync(workItemId, leaseToken, dispatchedAtUtc, cancellationToken);

        public Task ReleaseDispatchAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset dueAtUtc,
            CancellationToken cancellationToken) =>
            inner.ReleaseDispatchAsync(workItemId, leaseToken, dueAtUtc, cancellationToken);

        public Task MarkPoisonedAsync(
            Guid workItemId,
            DateTimeOffset failedAtUtc,
            CancellationToken cancellationToken) =>
            inner.MarkPoisonedAsync(workItemId, failedAtUtc, cancellationToken);

        public Task FailProcessingAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset failedAtUtc,
            string failureCode,
            string failureReason,
            CancellationToken cancellationToken) =>
            inner.FailProcessingAsync(
                workItemId, leaseToken, failedAtUtc, failureCode, failureReason, cancellationToken);

        public Task DeferProcessingAsync(
            Guid workItemId,
            string leaseToken,
            DateTimeOffset dueAtUtc,
            CancellationToken cancellationToken) =>
            inner.DeferProcessingAsync(workItemId, leaseToken, dueAtUtc, cancellationToken);
    }

    private static async Task<string?> ReadImageCustodyStateAsync(
        PegasusDbContext context,
        Guid imageIntakeId)
    {
        var intake = await context.ImageIntakes
            .AsNoTracking()
            .SingleAsync(item => item.Id == imageIntakeId);
        return intake.CustodyState;
    }

    private static Guid AddEmbeddedImage(
        PegasusDbContext context,
        Guid receiptId,
        string fileName,
        (string Hash, string StorageKey, int Length) staged,
        int widthPixels,
        int heightPixels)
    {
        var id = Guid.NewGuid();
        context.IntakeAssets.Add(new IntakeAssetEntity
        {
            Id = id,
            IntakeReceiptId = receiptId,
            SourceLabel = $"uploaded images.pdf, page 1, {fileName}",
            FileName = fileName,
            MediaType = fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? "image/png"
                : "image/jpeg",
            Kind = "embedded_image",
            Disposition = "embedded",
            ContentLength = staged.Length,
            ContentHash = staged.Hash,
            StorageKey = staged.StorageKey,
            PageNumber = 1,
            WidthPixels = widthPixels,
            HeightPixels = heightPixels
        });
        return id;
    }

    /// <summary>
    /// Two photographs uploaded together and registered as one Vehicle images
    /// record, through the upload and registration staff use.
    /// </summary>
    private static Task<(ImageIntakeRecord Record, Guid[] MemberReceiptIds)> RegisterTwoPhotographsAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        IServiceProvider services) =>
        RegisterPhotographsAsync(factory, client, services, ["overview.png", "close-up.png"]);

    /// <summary>
    /// Photographs uploaded together, one file each, and registered as one
    /// Vehicle images record, through the upload and registration staff use.
    /// </summary>
    private static async Task<(ImageIntakeRecord Record, Guid[] MemberReceiptIds)> RegisterPhotographsAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        IServiceProvider services,
        IReadOnlyList<string> fileNames)
    {
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);
        var upload = await IntakeWebDriver.PostUploadManyAsync(
            client,
            form.AntiforgeryToken,
            form.ExternalReceiptToken,
            [.. fileNames.Select(name => (name, "image/png", PngBytes))]);
        var groupId = Guid.Parse(upload.Location!.OriginalString.Split('/').Last());
        var group = await services.GetRequiredService<IIntakeSubmissionGroupStore>().GetAsync(groupId);
        var memberReceiptIds = new List<Guid>();
        foreach (var member in group!.Members.OrderBy(member => member.Ordinal))
        {
            await using var drainScope = factory.Services.CreateAsyncScope();
            memberReceiptIds.Add((await IntakeWebDriver.DrainStagedAsync(
                drainScope.ServiceProvider, member.StagedReceiptId)).ProcessedReceiptId);
        }

        var origin = await services.GetRequiredService<IImageIntakeOriginResolver>()
            .ResolveOriginAsync(memberReceiptIds[0], CancellationToken.None);
        var record = await services.GetRequiredService<IRegisterImageIntake>().ExecuteAsync(
            new(
                origin!,
                "AB12CDE",
                StaffActor(),
                $"image-intake-register:group:{groupId:N}",
                "Staff registered the whole submission group.",
                SubmissionGroupId: groupId),
            CancellationToken.None);
        return (record, [.. memberReceiptIds]);
    }

    /// <summary>Each member's photograph, in member order.</summary>
    private static async Task<Guid[]> PhotographIdsAsync(IServiceProvider services, Guid[] memberReceiptIds)
    {
        await using var context = await services.GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var sources = await context.IntakeAssets.AsNoTracking()
            .Where(asset => memberReceiptIds.Contains(asset.IntakeReceiptId)
                && asset.Kind == "source" && asset.Disposition == "source")
            .Select(asset => new { asset.IntakeReceiptId, asset.Id })
            .ToDictionaryAsync(asset => asset.IntakeReceiptId, asset => asset.Id);
        return [.. memberReceiptIds.Select(receiptId => sources[receiptId])];
    }

    private static async Task<Guid> WorkIdAsync(IServiceProvider services, Guid imageIntakeId, string kind)
    {
        await using var context = await services.GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        return await context.ExternalWorkItems.AsNoTracking()
            .Where(item => item.ImageIntakeId == imageIntakeId && item.Kind == kind)
            .Select(item => item.Id)
            .SingleAsync();
    }

    private static async Task SetDecisionAsync(
        IDbContextFactory<PegasusDbContext> contextFactory,
        Guid receiptId,
        IntakeDecision decision)
    {
        await using var context = await contextFactory.CreateDbContextAsync();
        (await context.IntakeReceipts.SingleAsync(item => item.Id == receiptId)).Decision =
            EfIntakeReceiptStore.ToCode(decision);
        await context.SaveChangesAsync();
    }

    /// <summary>A seeded Case whose evidence folder is stored.</summary>
    private static async Task<(Guid CaseId, string CaseRootRemoteId)> SeedCaseWithFolderAsync(
        IServiceProvider services,
        Guid originReceiptId,
        string reference,
        string caseType = "inspection",
        bool instructionComplete = true,
        bool imagesComplete = true)
    {
        var caseId = await SeedCaseAsync(
            services, originReceiptId, reference, caseType, instructionComplete, imagesComplete);
        var caseRoot = await services.GetRequiredService<ICaseCustody>().CreateCaseRootAsync(
            caseId, reference, $"img-case-root:{caseId:N}", CancellationToken.None);
        await using var context = await services.GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE Cases SET CustodyRootRemoteId = {caseRoot.RemoteId}, CustodyState = {"confirmed"} WHERE Id = {caseId}");
        return (caseId, caseRoot.RemoteId);
    }

    /// <summary>
    /// Staff link every member to the Case and merge the record into it. The
    /// merge enqueues the fold, whose work item is returned. It hands the
    /// fold back to publish only when the fold is due, not when it was
    /// created waiting for a folder.
    /// </summary>
    private static async Task<Guid> LinkAndMergeAsync(
        IServiceProvider services,
        ImageIntakeRecord record,
        Guid[] memberReceiptIds,
        Guid caseId)
    {
        var store = services.GetRequiredService<IImageIntakeStore>();
        var mutations = services.GetRequiredService<IIntakeMutationStore>();
        var receipts = services.GetRequiredService<IIntakeReceiptQueries>();
        var workflows = services.GetRequiredService<ICaseWorkflowQueries>();
        foreach (var receiptId in memberReceiptIds)
        {
            var workflow = await workflows.GetAsync(caseId, CancellationToken.None);
            var lease = await services.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
                new(caseId, workflow!.Version, StaffActor(), $"image-custody-link-lease:{receiptId:N}"),
                CancellationToken.None);
            var receipt = await receipts.GetAsync(receiptId, CancellationToken.None);
            await mutations.LinkAsync(new(receiptId, caseId, receipt!.Version, workflow.Version,
                lease.Token, StaffActor(), $"image-custody-link:{receiptId:N}",
                "Staff confirmed this image belongs to the instruction Case."),
                DateTimeOffset.UtcNow, CancellationToken.None);
        }
        var detail = await store.GetAsync(record.Id, CancellationToken.None);
        var imageIntakeLease = await services.GetRequiredService<IEditScopeLeases>().ClaimAsync(
            new(
                EditScopeKind.ImageIntake,
                record.Id,
                detail!.LifecycleVersion,
                StaffActor(),
                $"image-intake-merge-lease:{record.Origin.ReceiptId:N}"),
            CancellationToken.None);
        var merged = await store.MergeAsync(
            new(
                record.Id,
                caseId,
                StaffActor(),
                $"image-intake-merge:{record.Origin.ReceiptId:N}",
                "The Image-initiated case was merged into the linked formal Case.",
                detail!.LifecycleVersion,
                ExpectedStaffOriginAssociationVersion: 0)
            {
                EditLeaseToken = imageIntakeLease.Token
            },
            CancellationToken.None);

        await using var context = await services.GetRequiredService<IDbContextFactory<PegasusDbContext>>()
            .CreateDbContextAsync();
        var work = await context.ExternalWorkItems
            .AsNoTracking()
            .SingleAsync(item => item.ImageIntakeId == record.Id
                && item.Kind == ExternalWorkKinds.MergeImageCaseCustody);
        Assert.Equal("pending", work.State);
        Assert.Equal(caseId, work.CaseId);
        Assert.Equal(
            work.DueAtUtc > services.GetRequiredService<TimeProvider>().GetUtcNow() ? (Guid?)null : work.Id,
            merged.PendingExternalWorkId);
        return work.Id;
    }

    private static async Task<Guid> SeedCaseAsync(
        IServiceProvider services,
        Guid originReceiptId,
        string reference,
        string caseType = "inspection",
        bool instructionComplete = true,
        bool imagesComplete = true)
    {
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var organizationId = Guid.NewGuid();
        var lineageId = Guid.NewGuid();
        var principalId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var now = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Organizations (Role, Id, Name, Version) VALUES (N'principal', {organizationId}, {$"Image case custody provider {reference}"}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO PrincipalSequenceLineages (Id, CreatedAtUtc) VALUES ({lineageId}, {now})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Principals (Id, OrganizationId, Code, SequenceLineageId, IsActive, Version) VALUES ({principalId}, {organizationId}, {reference}, {lineageId}, {true}, {0L})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2031}, {1}, {reference}, {caseType}, {"not_ready"}, {"pending"}, {originReceiptId}, {instructionComplete}, {imagesComplete}, {now}, {0L}, {Guid.NewGuid()})");
        await CaseWorkFixture.InsertPrimaryWorksAsync(context);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {nameof(CaseLifecycleState.NotReady)}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseDataSnapshots (WorkId, OriginIntakeReceiptId, OriginSourceChannel, OriginExternalReceiptToken, OriginSourceHash, OriginReceivedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, CompletenessPolicyKey, CompletenessPolicyVersion, CompletenessPolicySatisfied, AcceptedAtUtc) VALUES ({caseId}, {originReceiptId}, {"manual_upload"}, {reference}, {1.ToString("X64", CultureInfo.InvariantCulture)}, {now}, {"image-case-custody-test-reader"}, {"1"}, {"image-case-custody-fixture"}, {1}, {reference}, {1}, {instructionComplete && imagesComplete}, {now})");
        return caseId;
    }

    /// <summary>
    /// The configured custody, with a member of staff who starts editing the
    /// Case while the fold's files move.
    /// </summary>
    private sealed class CustodyWithAnArrivingEditor(ICaseCustody inner, Func<Task> editorArrives) : ICaseCustody
    {
        public Task<CaseCustodyRoot> CreateCaseRootAsync(
            Guid caseId,
            string caseReference,
            string creationOwnerToken,
            string operationKey,
            CancellationToken cancellationToken) =>
            inner.CreateCaseRootAsync(caseId, caseReference, creationOwnerToken, operationKey, cancellationToken);

        public Task<CaseCustodyRoot> GetExistingCaseRootAsync(
            Guid caseId,
            string caseReference,
            CancellationToken cancellationToken) =>
            inner.GetExistingCaseRootAsync(caseId, caseReference, cancellationToken);

        public Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceAsync(
            CaseCustodyRoot root,
            IntakeSourceCustodyReference source,
            string operationKey,
            CancellationToken cancellationToken) =>
            inner.RetainAcceptedIntakeSourceAsync(root, source, operationKey, cancellationToken);

        public Task<string> CreateAuditReferenceFolderAsync(
            CaseCustodyRoot root,
            string auditReference,
            string creationOwnerToken,
            string operationKey,
            CancellationToken cancellationToken) =>
            inner.CreateAuditReferenceFolderAsync(
                root, auditReference, creationOwnerToken, operationKey, cancellationToken);

        public async Task MergeImageCaseContentsAsync(
            CaseCustodyRoot imageRoot,
            CaseCustodyRoot caseRoot,
            string operationKey,
            CancellationToken cancellationToken)
        {
            await inner.MergeImageCaseContentsAsync(imageRoot, caseRoot, operationKey, cancellationToken);
            await editorArrives();
        }
    }

    /// <summary>
    /// One failing ICaseCustody fake; the constructed exception decides the
    /// persisted failure taxonomy under test.
    /// </summary>
    private sealed class FailingCustody(Func<Exception> failure) : ICaseCustody
    {
        public Task<CaseCustodyRoot> CreateCaseRootAsync(
            Guid caseId,
            string caseReference,
            string creationOwnerToken,
            string operationKey,
            CancellationToken cancellationToken) =>
            Task.FromException<CaseCustodyRoot>(failure());

        public Task<CaseCustodyRoot> GetExistingCaseRootAsync(
            Guid caseId,
            string caseReference,
            CancellationToken cancellationToken) =>
            Task.FromException<CaseCustodyRoot>(failure());

        public Task<CustodyDocumentVersion> RetainAcceptedIntakeSourceAsync(
            CaseCustodyRoot root,
            IntakeSourceCustodyReference source,
            string operationKey,
            CancellationToken cancellationToken) =>
            Task.FromException<CustodyDocumentVersion>(failure());

        public Task<string> CreateAuditReferenceFolderAsync(
            CaseCustodyRoot root,
            string auditReference,
            string creationOwnerToken,
            string operationKey,
            CancellationToken cancellationToken) =>
            Task.FromException<string>(failure());
    }
}
