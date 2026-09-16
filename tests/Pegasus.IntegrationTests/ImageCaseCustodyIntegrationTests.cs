using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Authentication;

namespace Pegasus.IntegrationTests;

/// <summary>
/// INTK-014: registering an Image-initiated Case durably enqueues external
/// custody work that stores every group image under the registration
/// reference, and the merge into an instruction case enqueues a fold that
/// moves the contents into the case's evidence location and removes the
/// emptied folder. Runs against LocalDB with the local custody adapter — the
/// same processor path production drives against Box.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class ImageCaseCustodyIntegrationTests
{
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
            receipt.FieldsJson = "[]";
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
            "Register only the image-evidence member.", SubmissionGroupId: groupId));
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
        var pngBytes = Convert.FromBase64String(MultiFormatFixture.TinyPngBase64);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);
        var upload = await IntakeWebDriver.PostUploadManyAsync(
            client,
            form.AntiforgeryToken,
            form.ExternalReceiptToken,
            [
                ("overview.png", "image/png", pngBytes),
                ("close-up.png", "image/png", pngBytes)
            ]);
        var groupId = Guid.Parse(upload.Location!.OriginalString.Split('/').Last());

        Guid[] stagedReceiptIds;
        await using (var lookupScope = factory.Services.CreateAsyncScope())
        {
            var groups = lookupScope.ServiceProvider.GetRequiredService<IIntakeSubmissionGroupStore>();
            var group = await groups.GetAsync(groupId);
            stagedReceiptIds = group!.Members
                .OrderBy(member => member.Ordinal)
                .Select(member => member.StagedReceiptId)
                .ToArray();
        }
        var memberReceiptIds = new Guid[stagedReceiptIds.Length];
        for (var index = 0; index < stagedReceiptIds.Length; index++)
        {
            await using var drainScope = factory.Services.CreateAsyncScope();
            var evaluation = await IntakeWebDriver.DrainStagedAsync(
                drainScope.ServiceProvider,
                stagedReceiptIds[index]);
            memberReceiptIds[index] = evaluation.ProcessedReceiptId;
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var resolver = services.GetRequiredService<IImageIntakeOriginResolver>();
        var register = services.GetRequiredService<IRegisterImageIntake>();
        var origin = await resolver.ResolveOriginAsync(memberReceiptIds[0], CancellationToken.None);
        var record = await register.ExecuteAsync(
            new(
                origin!,
                "AB12CDE",
                StaffActor(),
                $"image-intake-register:group:{groupId:N}",
                "Staff registered the whole submission group.",
                SubmissionGroupId: groupId),
            CancellationToken.None);
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

        Guid[] sourceAssetIds;
        await using (var sourceContext = await contextFactory.CreateDbContextAsync())
        {
            var sources = await sourceContext.IntakeAssets.AsNoTracking()
                .Where(asset => memberReceiptIds.Contains(asset.IntakeReceiptId)
                    && asset.Kind == "source" && asset.Disposition == "source")
                .Select(asset => new { asset.IntakeReceiptId, asset.Id })
                .ToDictionaryAsync(asset => asset.IntakeReceiptId, asset => asset.Id);
            sourceAssetIds = memberReceiptIds.Select(receiptId => sources[receiptId]).ToArray();
        }
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
        }
        // Every group image is retained under the registration, in ordinal
        // order, byte-exact.
        var firstImagePath = Path.Combine(
            custodyRootDirectory, "images", $"001-{sourceAssetIds[0]:N}", "content");
        var secondImagePath = Path.Combine(
            custodyRootDirectory, "images", $"002-{sourceAssetIds[1]:N}", "content");
        Assert.Equal(pngBytes, await File.ReadAllBytesAsync(firstImagePath));
        Assert.Equal(pngBytes, await File.ReadAllBytesAsync(secondImagePath));

        // Merge into a formal case: the transition enqueues the fold and
        // commits regardless of external storage availability.
        var originalCaseId = await SeedCaseAsync(services, memberReceiptIds[0], "IMG26001");
        var caseId = await SeedCaseAsync(
            services, memberReceiptIds[0], "a.IMG26001", originalCaseId);
        var caseCustody = services.GetRequiredService<ICaseCustody>();
        await caseCustody.CreateCaseRootAsync(
            originalCaseId, "IMG26001", $"img-case-root:{originalCaseId:N}", CancellationToken.None);
        var caseRoot = await caseCustody.CreateLinkedAuditCaseRootAsync(
            caseId,
            "a.IMG26001",
            originalCaseId,
            "IMG26001",
            "0123456789ABCDEFGHJKMNPQRS",
            $"img-case-root:{caseId:N}",
            null,
            CancellationToken.None);
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Cases SET CustodyRootRemoteId = {caseRoot.RemoteId}, CustodyState = {"confirmed"} WHERE Id = {caseId}");
        }

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

        Guid mergeWorkId;
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var work = await context.ExternalWorkItems
                .AsNoTracking()
                .SingleAsync(item => item.ImageIntakeId == record.Id
                    && item.Kind == ExternalWorkKinds.MergeImageCaseCustody);
            Assert.Equal("pending", work.State);
            Assert.Equal(caseId, work.CaseId);
            mergeWorkId = work.Id;
        }

        await processor.ExecuteAsync(mergeWorkId, CancellationToken.None);
        await processor.ExecuteAsync(mergeWorkId, CancellationToken.None);

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
        }
        // The contents moved into the case's location and the emptied
        // image-case folder is gone.
        Assert.False(Directory.Exists(custodyRootDirectory));
        var caseImagesDirectory = Path.Combine(
            factory.ArtifactDirectory,
            "custody",
            "cases",
            originalCaseId.ToString("N"),
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

    private static async Task<Guid> SeedCaseAsync(
        IServiceProvider services,
        Guid originReceiptId,
        string reference,
        Guid? auditOfCaseId = null)
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
        var caseType = auditOfCaseId is null ? "inspection" : "audit";
        var auditReference = auditOfCaseId is null ? null : reference;
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, AuditReference, Type, InitialState, CustodyState, OriginIntakeReceiptId, AuditOfCaseId, InstructionComplete, ImagesComplete, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2031}, {1}, {reference}, {auditReference}, {caseType}, {"not_ready"}, {"pending"}, {originReceiptId}, {auditOfCaseId}, {true}, {true}, {now}, {0L}, {Guid.NewGuid()})");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO CaseWorkflows (CaseId, State, Version, ConcurrencyToken) VALUES ({caseId}, {nameof(CaseLifecycleState.NotReady)}, {0L}, {Guid.NewGuid()})");
        return caseId;
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
            CancellationToken cancellationToken,
            Guid? parentCaseId = null,
            string? parentCaseReference = null) =>
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
