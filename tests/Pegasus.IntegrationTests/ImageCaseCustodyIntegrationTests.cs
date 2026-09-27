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
using Pegasus.Infrastructure.Persistence;
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
        var banner = new byte[110_783];
        photographOne[0] = 1;
        photographTwo[0] = 2;
        banner[0] = 3;
        var first = await StageAsync(photographOne);
        var second = await StageAsync(photographTwo);
        var duplicate = await StageAsync(photographOne);
        var excludedBanner = await StageAsync(banner);
        var contextFactory = services.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        Guid sourceAssetId;
        Guid firstAssetId;
        Guid secondAssetId;
        Guid duplicateAssetId;
        Guid bannerAssetId;
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
            // The U50-sized letterhead banner is deliberately retained in this
            // fixture to prove downstream custody also excludes historic bad
            // candidates when a receipt is replayed.
            bannerAssetId = AddEmbeddedImage(
                context, receiptId, "page-1-image-1.png", excludedBanner, 1990, 437);
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
        Assert.DoesNotContain(retainedDirectories, path => path.Contains($"{bannerAssetId:N}", StringComparison.Ordinal));
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
        var workflows = services.GetRequiredService<ICaseWorkflowQueries>();
        var mergeWorkId = await LinkAndMergeAsync(services, record, memberReceiptIds, caseId);

        await processor.ExecuteAsync(mergeWorkId, CancellationToken.None);
        await processor.ExecuteAsync(mergeWorkId, CancellationToken.None);

        // Each photograph's filing has its own key under the fold's, so filing
        // the same photographs again records nothing twice.
        await using (var refiling = await contextFactory.CreateDbContextAsync())
        {
            Assert.Equal(
                2,
                await EfQueuedCustodyProcessor.RecordFoldedPhotographsAsync(
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

        // The Overview came from the record, so only the Close-up is asked
        // for. Tagging the second photograph clears it.
        var preparations = services.GetRequiredService<ICaseAssetPreparationQueries>();
        Assert.Equal(
            [CaseReportReadiness.CloseUpImageRequirement],
            DocumentCustodyDurabilityTests.ImageBlockers(
                await preparations.ListForCaseAsync(caseId, CancellationToken.None)));
        var tagWorkflow = await workflows.GetAsync(caseId, CancellationToken.None);
        var tagLease = await services.GetRequiredService<ILeaseCaseForEdit>().ClaimAsync(
            new(caseId, tagWorkflow!.Version, StaffActor(), $"close-up-lease:{Guid.NewGuid():N}"),
            CancellationToken.None);
        await services.GetRequiredService<ITagCaseImage>().ExecuteAsync(
            new(
                caseId,
                occurrenceIds[1],
                ImageTagVocabulary.CloseUpId,
                StaffActor(),
                $"close-up-tag:{Guid.NewGuid():N}",
                tagLease.Version,
                tagLease.Token),
            CancellationToken.None);
        Assert.Empty(DocumentCustodyDurabilityTests.ImageBlockers(
            await preparations.ListForCaseAsync(caseId, CancellationToken.None)));
    }

    /// <summary>
    /// The fold holds no staff lease and yields to a member of staff editing
    /// the Case (FRD-14): it writes nothing, leaves their lease, and files the
    /// photographs on the retry after they finish. An editor already there
    /// stops the fold before anything moves; one arriving while the files
    /// move stops its completion, and the retry files from where they are.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AFoldYieldsToAStaffEditorAndFilesThePhotographsAfterTheyFinish(
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
        var (caseId, _) = await SeedCaseWithFolderAsync(services, memberReceiptIds[0], "IMG26003");
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

        IProcessQueuedCustody firstAttempt = processor;
        if (editorArrivesWhileTheFilesMove)
        {
            firstAttempt = new EfQueuedCustodyProcessor(
                contextFactory,
                services.GetRequiredService<IExternalWorkStore>(),
                new CustodyWithAnArrivingEditor(services.GetRequiredService<ICaseCustody>(), EditorArrivesAsync),
                services.GetRequiredService<TimeProvider>());
        }
        else
        {
            await EditorArrivesAsync();
        }

        await Assert.ThrowsAsync<IOException>(() =>
            firstAttempt.ExecuteAsync(mergeWorkId, CancellationToken.None));

        // An editor already there stopped the fold before anything moved.
        var imageFolder = Path.Combine(
            factory.ArtifactDirectory, "custody", "cases", record.Id.ToString("N"));
        Assert.Equal(!editorArrivesWhileTheFilesMove, Directory.Exists(imageFolder));
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var work = await context.ExternalWorkItems.AsNoTracking().SingleAsync(item => item.Id == mergeWorkId);
            Assert.Equal("pending", work.State);
            Assert.Equal("custody_dependency_failure", work.FailureCode);
            Assert.Equal("confirmed", await ReadImageCustodyStateAsync(context, record.Id));
            Assert.False(await context.Set<CaseDocumentEntity>().AnyAsync(item => item.CaseId == caseId));
            Assert.False(await context.CaseHistory.AnyAsync(item =>
                item.CaseId == caseId && item.EventType == "image_custody_merged"));
            var workflow = await context.CaseWorkflows.AsNoTracking().SingleAsync(item => item.CaseId == caseId);
            Assert.Equal(editor!.Version, workflow.Version);
            Assert.Equal(editor.ExpiresAtUtc, workflow.EditLeaseExpiresAtUtc);
        }
        // The editor still holds the lease they claimed.
        await leases.HeartbeatAsync(new(caseId, StaffActor(), editor!.Token), CancellationToken.None);

        await leases.ReleaseAsync(
            new(caseId, StaffActor(), $"editor-release:{Guid.NewGuid():N}", editor.Token),
            CancellationToken.None);
        await processor.ExecuteAsync(mergeWorkId, CancellationToken.None);

        Assert.False(Directory.Exists(imageFolder));
        await using var filed = await contextFactory.CreateDbContextAsync();
        Assert.Equal(
            "completed",
            (await filed.ExternalWorkItems.AsNoTracking().SingleAsync(item => item.Id == mergeWorkId)).State);
        Assert.Equal("merged", await ReadImageCustodyStateAsync(filed, record.Id));
        var images = await (
                from occurrence in filed.Set<DocumentOccurrenceEntity>().AsNoTracking()
                join version in filed.Set<DocumentVersionEntity>().AsNoTracking()
                    on occurrence.VersionId equals version.Id
                where occurrence.CaseId == caseId
                orderby occurrence.Ordinal
                select new { OccurrenceId = occurrence.Id, VersionId = version.Id })
            .ToListAsync();
        Assert.Equal(2, images.Count);
        foreach (var image in images)
        {
            await using var download = Assert.IsType<DocumentDownload>(
                await services.GetRequiredService<IDownloadCaseDocument>().ExecuteAsync(
                    new(caseId, image.OccurrenceId, image.VersionId, StaffActor(), $"read-folded:{Guid.NewGuid():N}"),
                    CancellationToken.None));
            using var buffer = new MemoryStream();
            await download.Content.CopyToAsync(buffer);
            Assert.Equal(PngBytes, buffer.ToArray());
        }
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
        Assert.Equal(PngBytes, photograph.Content);
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
    private static async Task<(ImageIntakeRecord Record, Guid[] MemberReceiptIds)> RegisterTwoPhotographsAsync(
        IntakeWebApplicationFactory factory,
        HttpClient client,
        IServiceProvider services)
    {
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);
        var upload = await IntakeWebDriver.PostUploadManyAsync(
            client,
            form.AntiforgeryToken,
            form.ExternalReceiptToken,
            [
                ("overview.png", "image/png", PngBytes),
                ("close-up.png", "image/png", PngBytes)
            ]);
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
    /// merge enqueues the fold, whose work item is returned.
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
        await store.MergeAsync(
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
            $"INSERT INTO Organizations (Id, Name, Version) VALUES ({organizationId}, {$"Image case custody provider {reference}"}, {0L})");
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
