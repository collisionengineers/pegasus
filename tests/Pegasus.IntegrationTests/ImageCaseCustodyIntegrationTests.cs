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
            custodyRootDirectory, "images", $"001-{memberReceiptIds[0]:N}", "content");
        var secondImagePath = Path.Combine(
            custodyRootDirectory, "images", $"002-{memberReceiptIds[1]:N}", "content");
        Assert.Equal(pngBytes, await File.ReadAllBytesAsync(firstImagePath));
        Assert.Equal(pngBytes, await File.ReadAllBytesAsync(secondImagePath));

        // Merge into a formal case: the transition enqueues the fold and
        // commits regardless of external storage availability.
        var caseId = await SeedCaseAsync(services, memberReceiptIds[0], "IMG26001");
        var caseCustody = services.GetRequiredService<ICaseCustody>();
        var caseRoot = await caseCustody.CreateCaseRootAsync(
            caseId, "IMG26001", $"img-case-root:{caseId:N}", CancellationToken.None);
        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE Cases SET CustodyRootRemoteId = {caseRoot.RemoteId}, CustodyState = {"confirmed"} WHERE Id = {caseId}");
        }

        var store = services.GetRequiredService<IImageIntakeStore>();
        var detail = await store.GetAsync(record.Id, CancellationToken.None);
        await store.MergeAsync(
            new(
                record.Id,
                caseId,
                StaffActor(),
                $"image-intake-merge:{record.Origin.ReceiptId:N}",
                "The Image-initiated case was merged into the linked formal Case.",
                detail!.LifecycleVersion),
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
            factory.ArtifactDirectory, "custody", "cases", caseId.ToString("N"), "images");
        Assert.Equal(
            pngBytes,
            await File.ReadAllBytesAsync(Path.Combine(
                caseImagesDirectory, $"001-{memberReceiptIds[0]:N}", "content")));
        Assert.Equal(
            pngBytes,
            await File.ReadAllBytesAsync(Path.Combine(
                caseImagesDirectory, $"002-{memberReceiptIds[1]:N}", "content")));
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

    private static async Task<Guid> SeedCaseAsync(
        IServiceProvider services,
        Guid originReceiptId,
        string reference)
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
            $"INSERT INTO Cases (Id, PrincipalId, SequenceLineageId, Year, Sequence, Reference, Type, InitialState, CustodyState, OriginIntakeReceiptId, InstructionComplete, ImagesComplete, InstructionConfirmedByStaff, ImagesConfirmedByStaff, CreatedAtUtc, Version, ConcurrencyToken) VALUES ({caseId}, {principalId}, {lineageId}, {2031}, {1}, {reference}, {"inspection"}, {"not_ready"}, {"pending"}, {originReceiptId}, {true}, {true}, {true}, {true}, {now}, {0L}, {Guid.NewGuid()})");
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
