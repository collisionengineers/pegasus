using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Persistence;
using SkiaSharp;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class AssociatedMailEvidenceIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReasonedReevaluationRepairsAnExistingFollowUpLinkButDoesNotRefileTheCaseOrigin(bool isCaseOrigin)
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await SeedCaseAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var (receipt, stagedReceiptId) = await LinkWithoutFilingAsync(services, caseId);
        await using (var before = await factory.Database.CreateContextAsync())
        {
            Assert.False(await before.Set<CaseDocumentEntity>().AnyAsync(value => value.CaseId == caseId));
            Assert.False(await before.Cases.Where(value => value.Id == caseId).Select(value => value.ImagesComplete).SingleAsync());
            Assert.True(await before.Set<IntakeSearchDocumentEntity>().AnyAsync(value =>
                value.IntakeReceiptId == receipt.Id && value.AttachmentFileName == "1_Images-V1.pdf"));
            if (isCaseOrigin)
            {
                await before.Cases.Where(value => value.Id == caseId).ExecuteUpdateAsync(update => update
                    .SetProperty(value => value.OriginIntakeReceiptId, receipt.Id));
            }
            else
            {
                // Re-evaluation can discover a photograph missing from the earlier extraction.
                var rediscovered = InstructionEvidenceImages.Select(receipt.AssetRecords)[0].Id;
                await before.IntakeAssets.Where(value => value.Id == rediscovered).ExecuteDeleteAsync();
            }
        }
        await services.GetRequiredService<IReevaluateIntake>().ExecuteAsync(new(
            receipt.Id, receipt.Version, ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
            $"repair-linked-evidence:{Guid.NewGuid():N}", "Recover retained photographs from the association-only route."), default);
        await DispatchAsync(services, stagedReceiptId);
        Assert.Equal(QueuedIntakeProcessingOutcome.Completed,
            await IntakeWebDriver.CreateProcessor(services).ExecuteAsync(stagedReceiptId, default));
        await using var after = await factory.Database.CreateContextAsync();
        Assert.True(await after.Set<IntakeSearchDocumentEntity>().AnyAsync(value =>
            value.IntakeReceiptId == receipt.Id && value.AttachmentFileName == "1_Images-V1.pdf"));
        if (isCaseOrigin)
        {
            Assert.False(await after.Set<CaseDocumentEntity>().AnyAsync(value => value.CaseId == caseId));
            Assert.False(await after.Cases.Where(value => value.Id == caseId).Select(value => value.ImagesComplete).SingleAsync());
        }
        else
        {
            await AssertFiledAsync(factory, caseId, DocumentCustodyStatus.Confirmed);
            Assert.True(await after.Cases.Where(value => value.Id == caseId).Select(value => value.ImagesComplete).SingleAsync());
        }
        Assert.Equal(1, await after.Cases.CountAsync());
    }

    /// <summary>
    /// Filing a linked message's photographs carries what staff made of each
    /// before the Case had it: its crop, rotation and tags (FRD-19). One
    /// tagged Third party arrives out of the report.
    /// </summary>
    [Fact]
    public async Task FilingALinkedMessageCarriesAPhotographsCropRotationAndTags()
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await SeedCaseAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var (receipt, stagedReceiptId) = await LinkWithoutFilingAsync(services, caseId);
        var photographId = InstructionEvidenceImages.Select(receipt.AssetRecords)[0].Id;
        var staff = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        await services.GetRequiredService<ISavePreCaseImageCrop>().ExecuteAsync(
            new(photographId, 0, CaseAssetRotation.Clockwise90, new CaseAssetCrop(0.1m, 0.2m, 0.5m, 0.6m),
                staff, $"linked-crop:{Guid.NewGuid():N}"), default);
        await services.GetRequiredService<ITagPreCaseImage>().ExecuteAsync(
            new(photographId, ImageTagVocabulary.ThirdPartyId, true, staff, $"linked-tag:{Guid.NewGuid():N}"), default);

        await services.GetRequiredService<IReevaluateIntake>().ExecuteAsync(new(
            receipt.Id, receipt.Version, staff,
            $"file-linked-evidence:{Guid.NewGuid():N}", "File the linked message's evidence on its Case."), default);
        await DispatchAsync(services, stagedReceiptId);
        Assert.Equal(QueuedIntakeProcessingOutcome.Completed,
            await IntakeWebDriver.CreateProcessor(services).ExecuteAsync(stagedReceiptId, default));

        await using var db = await factory.Database.CreateContextAsync();
        var images = await db.Set<DocumentOccurrenceEntity>().AsNoTracking()
            .Where(value => value.CaseId == caseId && value.SemanticRole == DocumentSemanticRole.Image)
            .ToListAsync();
        Assert.Equal(4, images.Count);
        var prepared = Assert.Single(images, image => image.SourceOccurrenceIdentity == photographId.ToString("N"));
        Assert.False(prepared.InReport);
        Assert.Equal(90, prepared.RotationDegrees);
        Assert.Equal(0.1m, prepared.CropLeft);
        Assert.Equal(0.6m, prepared.CropHeight);
        var imageIds = images.Select(image => image.Id).ToArray();
        var tag = Assert.Single(await db.Set<DocumentOccurrenceTagEntity>().AsNoTracking()
            .Where(value => imageIds.Contains(value.OccurrenceId)).ToListAsync());
        Assert.Equal(prepared.Id, tag.OccurrenceId);
        Assert.Equal(ImageTagVocabulary.ThirdPartyId, tag.TagId);
        Assert.All(images.Where(image => image.Id != prepared.Id), image =>
        {
            Assert.True(image.InReport);
            Assert.Null(image.CropLeft);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MatchedAuditFollowUpFilesPdfAndFourPhotosAndPreservesOtherReadinessGates(
        bool instructionsComplete)
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await SeedCaseAsync(factory, instructionsComplete: instructionsComplete);
        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, FollowUp());
        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = Assert.IsType<IntakeReceipt>(await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>().GetAsync(receiptId, default));

        Assert.Equal(caseId, receipt.CurrentCaseId);
        Assert.Equal(MailClassificationOutcome.Unclassified, receipt.MailClassificationDecision!.Outcome);
        Assert.Equal(CaseMatchOutcome.UniqueMatch, receipt.CaseMatchDecision!.Outcome);
        Assert.Equal(4, InstructionEvidenceImages.Select(receipt.AssetRecords).Count);
        await AssertFiledAsync(factory, caseId, DocumentCustodyStatus.Confirmed);
        await using var db = await factory.Database.CreateContextAsync();
        var stored = await db.Cases.SingleAsync(value => value.Id == caseId);
        Assert.True(stored.ImagesComplete);
        Assert.Equal(instructionsComplete, stored.InstructionComplete);
        var workflow = await db.CaseWorkflows.SingleAsync(value => value.CaseId == caseId);
        Assert.Equal(instructionsComplete ? nameof(CaseLifecycleState.Review) : nameof(CaseLifecycleState.NotReady), workflow.State);
        Assert.Null(workflow.AssignedEngineerId);
        Assert.Single(await db.Cases.Where(value => value.Id == caseId).ToListAsync());
        Assert.False(await db.ImageIntakes.AnyAsync(value => value.OriginReceiptId == receiptId));
        Assert.Equal(4, await db.Set<DocumentOccurrenceEntity>().CountAsync(value => value.CaseId == caseId && value.SemanticRole == DocumentSemanticRole.Image));

        var identities = await db.Set<DocumentOccurrenceEntity>()
            .Where(value => value.CaseId == caseId).Select(value => value.Id).ToArrayAsync();
        var stagedId = await db.IntakeWorkItems.Where(value => value.ProcessedReceiptId == receiptId)
            .Select(value => value.StagedReceiptId).SingleAsync();
        var historyCount = await db.Set<CaseHistoryEntity>().CountAsync(value => value.CaseId == caseId);
        Assert.Equal(QueuedIntakeProcessingOutcome.NoOp,
            await IntakeWebDriver.CreateProcessor(scope.ServiceProvider).ExecuteAsync(stagedId, default));
        Assert.Equal(identities.Order(), (await db.Set<DocumentOccurrenceEntity>()
            .Where(value => value.CaseId == caseId).Select(value => value.Id).ToArrayAsync()).Order());
        Assert.Equal(historyCount, await db.Set<CaseHistoryEntity>().CountAsync(value => value.CaseId == caseId));
    }

    [Fact]
    public async Task PendingCaseCustodyResumesWithoutDuplicatesAndOnlyThenClearsImages()
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await SeedCaseAsync(factory, hasCustodyRoot: false);
        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, FollowUp());
        await AssertFiledAsync(factory, caseId, DocumentCustodyStatus.Pending);
        Guid[] originalIds;
        await using (var db = await factory.Database.CreateContextAsync())
        {
            Assert.False((await db.Cases.SingleAsync(value => value.Id == caseId)).ImagesComplete);
            originalIds = await db.Set<DocumentVersionEntity>()
                .Where(value => db.Set<CaseDocumentEntity>().Any(document => document.Id == value.DocumentId && document.CaseId == caseId))
                .Select(value => value.Id).ToArrayAsync();
            await db.Cases.Where(value => value.Id == caseId).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.CustodyRootRemoteId, "case-root"));
            // A completed intervening edit must trigger a fresh guard, not strand the old plan.
            await db.CaseWorkflows.Where(value => value.CaseId == caseId).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.Version, value => value.Version + 1));
            await AgePastInlineFilingGraceAsync(db);
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var reconciliation = await scope.ServiceProvider.GetRequiredService<ReconcilePendingArtifactCustody>()
            .ExecuteAsync(20, default);
        Assert.Equal(6, reconciliation.Confirmed);
        Assert.Equal(0, reconciliation.Failures);
        await AssertFiledAsync(factory, caseId, DocumentCustodyStatus.Confirmed);
        await using var verify = await factory.Database.CreateContextAsync();
        Assert.True((await verify.Cases.SingleAsync(value => value.Id == caseId)).ImagesComplete);
        Assert.Equal(originalIds.Order(), (await verify.Set<DocumentVersionEntity>()
            .Where(value => originalIds.Contains(value.Id)).Select(value => value.Id).ToArrayAsync()).Order());

        // A later staff decision must not be overwritten by an old receipt replay.
        await verify.Cases.Where(value => value.Id == caseId)
            .ExecuteUpdateAsync(update => update.SetProperty(value => value.ImagesComplete, false));
        var stagedId = await verify.IntakeWorkItems.Where(value => value.ProcessedReceiptId == receiptId)
            .Select(value => value.StagedReceiptId).SingleAsync();
        await IntakeWebDriver.CreateProcessor(scope.ServiceProvider).ExecuteAsync(stagedId, default);
        Assert.False(await verify.Cases.Where(value => value.Id == caseId).Select(value => value.ImagesComplete).SingleAsync());
        Assert.Equal(6, await verify.Set<CaseDocumentEntity>().CountAsync(value => value.CaseId == caseId));
    }

    [Theory]
    [InlineData("unlink")]
    [InlineData("archive")]
    [InlineData("post-report")]
    [InlineData("report-sent")]
    [InlineData("editor")]
    public async Task DelayedCustodyRechecksAssociationCaseEligibilityAndActiveEditor(string change)
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await SeedCaseAsync(factory, hasCustodyRoot: false);
        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, FollowUp());
        await AssertFiledAsync(factory, caseId, DocumentCustodyStatus.Pending);
        await using (var db = await factory.Database.CreateContextAsync())
        {
            var storedCase = await db.Cases.SingleAsync(value => value.Id == caseId);
            storedCase.CustodyRootRemoteId = "case-root";
            var workflow = await db.CaseWorkflows.SingleAsync(value => value.CaseId == caseId);
            switch (change)
            {
                case "unlink":
                    (await db.IntakeManualAssociations.SingleAsync(value => value.IntakeReceiptId == receiptId)).IsActive = false;
                    break;
                case "archive":
                    workflow.ArchivedAtUtc = DateTimeOffset.UtcNow;
                    workflow.ArchivedByKind = nameof(ActorKind.Staff);
                    workflow.ArchivedBySubjectId = Guid.NewGuid().ToString();
                    workflow.ArchivedByRolesJson = "[\"Administrator\"]";
                    workflow.ArchiveReason = "Archived during pending custody.";
                    break;
                case "post-report":
                    workflow.State = nameof(CaseLifecycleState.PostReportComplete);
                    break;
                case "report-sent":
                    workflow.ReportSentEvidence = new CaseReportSentEvidenceEntity
                    {
                        Id = Guid.NewGuid(), CaseId = caseId, MailboxIdentity = "test-mailbox",
                        SentFolderIdentity = "sent", ImmutableItemIdentity = "sent-item",
                        InternetMessageIdentity = "sent@test.invalid", ConversationIdentity = "conversation",
                        ReplyChainIdentity = "reply-chain", SourceOccurrenceIdentity = "sent-source",
                        SourceSha256 = new string('A', 64), MimeSha256 = new string('B', 64),
                        SentAtUtc = DateTimeOffset.UtcNow, DiscoveredAtUtc = DateTimeOffset.UtcNow,
                        DiscoveredByKind = nameof(ActorKind.SystemWorker), DiscoveredBySubjectId = "test",
                        RetentionOperationKey = "retained-sent-test", RetentionRequestHash = new string('C', 64)
                    };
                    db.Add(workflow.ReportSentEvidence);
                    break;
                case "editor":
                    workflow.EditLeaseHolder = Guid.NewGuid().ToString();
                    workflow.EditLeaseHolderKind = nameof(ActorKind.Staff);
                    workflow.EditLeaseTokenHash = new string('A', 64);
                    workflow.EditLeaseExpiresAtUtc = DateTimeOffset.MaxValue;
                    break;
            }
            await db.SaveChangesAsync();
            await AgePastInlineFilingGraceAsync(db);
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ReconcilePendingArtifactCustody>()
            .ExecuteAsync(20, default);
        Assert.Equal(0, result.Confirmed);
        await AssertFiledAsync(factory, caseId, DocumentCustodyStatus.Pending);
        await using var verify = await factory.Database.CreateContextAsync();
        Assert.False((await verify.Cases.SingleAsync(value => value.Id == caseId)).ImagesComplete);
        if (change == "editor")
        {
            await verify.CaseWorkflows.Where(value => value.CaseId == caseId).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.EditLeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(value => value.EditLeaseTokenHash, (string?)null)
                .SetProperty(value => value.EditLeaseHolder, (string?)null)
                .SetProperty(value => value.EditLeaseHolderKind, (string?)null));
            var resumed = await scope.ServiceProvider.GetRequiredService<ReconcilePendingArtifactCustody>()
                .ExecuteAsync(20, default);
            Assert.Equal(6, resumed.Confirmed);
            Assert.True(await verify.Cases.Where(value => value.Id == caseId).Select(value => value.ImagesComplete).SingleAsync());
        }
    }

    [Theory]
    [InlineData(CaseLifecycleState.Held, false)]
    [InlineData(CaseLifecycleState.ReportPreparation, true)]
    [InlineData(CaseLifecycleState.Review, true)]
    public async Task FilingPreReportEvidencePreservesTheExistingHoldAndEngineerWorkflow(
        CaseLifecycleState state, bool assigned)
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await SeedCaseAsync(factory, instructionsComplete: false);
        Guid? engineerId = assigned ? Guid.NewGuid() : null;
        await using (var db = await factory.Database.CreateContextAsync())
        {
            await db.CaseWorkflows.Where(value => value.CaseId == caseId).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.State, state.ToString())
                .SetProperty(value => value.AssignedEngineerId, engineerId));
        }
        await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, FollowUp());
        await AssertFiledAsync(factory, caseId, DocumentCustodyStatus.Confirmed);
        await using var verify = await factory.Database.CreateContextAsync();
        var workflow = await verify.CaseWorkflows.SingleAsync(value => value.CaseId == caseId);
        Assert.Equal(state.ToString(), workflow.State);
        Assert.Equal(engineerId, workflow.AssignedEngineerId);
        Assert.True(await verify.Cases.Where(value => value.Id == caseId).Select(value => value.ImagesComplete).SingleAsync());
        Assert.False(await verify.Cases.Where(value => value.Id == caseId).Select(value => value.InstructionComplete).SingleAsync());
    }

    [Fact]
    public async Task CorruptParentPdfWithholdsReadinessEvenWhenEveryPhotographHasConfirmed()
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await SeedCaseAsync(factory, hasCustodyRoot: false);
        await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, FollowUp());
        string storageKey;
        await using (var db = await factory.Database.CreateContextAsync())
        {
            storageKey = await db.Set<DocumentVersionEntity>()
                .Where(value => value.FileName == "1_Images-V1.pdf")
                .Select(value => value.PendingContentStorageKey!).SingleAsync();
            await db.Cases.Where(value => value.Id == caseId).ExecuteUpdateAsync(update => update
                .SetProperty(value => value.CustodyRootRemoteId, "case-root"));
            await AgePastInlineFilingGraceAsync(db);
        }
        var path = Path.Combine(factory.ArtifactDirectory, storageKey.Replace('/', Path.DirectorySeparatorChar));
        var original = await File.ReadAllBytesAsync(path);
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        await using var scope = factory.Services.CreateAsyncScope();
        var reconciler = scope.ServiceProvider.GetRequiredService<ReconcilePendingArtifactCustody>();
        var partial = await reconciler.ExecuteAsync(20, default);
        Assert.Equal(5, partial.Confirmed);
        Assert.Equal(1, partial.Failures);
        await using var verify = await factory.Database.CreateContextAsync();
        Assert.Equal(4, await (from occurrence in verify.Set<DocumentOccurrenceEntity>()
                              join version in verify.Set<DocumentVersionEntity>() on occurrence.VersionId equals version.Id
                              where occurrence.CaseId == caseId && occurrence.SemanticRole == DocumentSemanticRole.Image
                                  && version.CustodyStatus == DocumentCustodyStatus.Confirmed
                              select version.Id).CountAsync());
        Assert.False(await verify.Cases.Where(value => value.Id == caseId).Select(value => value.ImagesComplete).SingleAsync());
        await File.WriteAllBytesAsync(path, original);
        // A failed attempt waits before the sweep offers the version again, so the
        // recorded failure is aged past its longest wait first.
        await verify.ActionHistory
            .Where(value => value.EventKind == "ArtifactCustodyReconciliationAttempt"
                && value.Outcome == "Failed")
            .ExecuteUpdateAsync(update => update.SetProperty(
                value => value.OccurredAtUtc, DateTimeOffset.UtcNow.AddHours(-1)));
        var recovered = await reconciler.ExecuteAsync(20, default);
        Assert.Equal(1, recovered.Confirmed);
        Assert.True(await verify.Cases.Where(value => value.Id == caseId).Select(value => value.ImagesComplete).SingleAsync());
        await AssertFiledAsync(factory, caseId, DocumentCustodyStatus.Confirmed);
    }

    [Fact]
    public async Task AnAmbiguousClaimMatchDoesNotFilePhotographsToEitherCase()
    {
        using var factory = new IntakeWebApplicationFactory();
        var first = await SeedCaseAsync(factory);
        var second = await SeedCaseAsync(factory, sequence: 10);
        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, FollowUp());
        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = Assert.IsType<IntakeReceipt>(await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>().GetAsync(receiptId, default));
        Assert.Null(receipt.CurrentCaseId);
        Assert.Equal(CaseMatchOutcome.Ambiguous, receipt.CaseMatchDecision!.Outcome);
        await using var db = await factory.Database.CreateContextAsync();
        Assert.False(await db.Set<CaseDocumentEntity>().AnyAsync(value => value.CaseId == first || value.CaseId == second));
        Assert.False(await db.Cases.AnyAsync(value => (value.Id == first || value.Id == second) && value.ImagesComplete));
    }

    [Fact]
    public async Task ALinkedPdfContainingOnlyABannerCannotSatisfyImages()
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await SeedCaseAsync(factory);
        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, FollowUp(includePhotos: false));
        await using var scope = factory.Services.CreateAsyncScope();
        var receipt = Assert.IsType<IntakeReceipt>(await scope.ServiceProvider
            .GetRequiredService<IIntakeReceiptQueries>().GetAsync(receiptId, default));
        Assert.Equal(caseId, receipt.CurrentCaseId);
        Assert.Empty(InstructionEvidenceImages.Select(receipt.AssetRecords));
        await using var db = await factory.Database.CreateContextAsync();
        Assert.False((await db.Cases.SingleAsync(value => value.Id == caseId)).ImagesComplete);
        Assert.False(await db.Set<DocumentOccurrenceEntity>().AnyAsync(value => value.CaseId == caseId && value.SemanticRole == DocumentSemanticRole.Image));
    }

    [Fact]
    public async Task AMatchedFollowUpWithoutPhotographsIsFiledOnTheCaseAndNotHeld()
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await SeedCaseAsync(factory);
        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(factory.Services, FollowUp(includePhotos: false));
        await using var db = await factory.Database.CreateContextAsync();
        var files = await (from occurrence in db.Set<DocumentOccurrenceEntity>()
                           join version in db.Set<DocumentVersionEntity>() on occurrence.VersionId equals version.Id
                           where occurrence.CaseId == caseId
                           select new
                           {
                               occurrence.SemanticRole,
                               occurrence.SourceOccurrenceIdentity,
                               version.FileName,
                               version.CustodyStatus
                           }).ToArrayAsync();
        Assert.Equal(2, files.Length);
        Assert.All(files, file => Assert.Equal(DocumentCustodyStatus.Confirmed, file.CustodyStatus));
        Assert.Single(files, file => file.FileName == "follow-up.eml" && file.SemanticRole == DocumentSemanticRole.OriginalSource);
        Assert.Single(files, file => file.FileName == "1_Images-V1.pdf" && file.SemanticRole == DocumentSemanticRole.Correspondence);
        Assert.False((await db.Cases.SingleAsync(value => value.Id == caseId)).ImagesComplete);

        // Filed on the Case, the receipt's files are read from the Case folder:
        // none of them is copied to the holding folder.
        var filedAssetIds = files.Select(file => file.SourceOccurrenceIdentity).ToHashSet(StringComparer.Ordinal);
        var filedAssets = (await db.IntakeAssets.Where(asset => asset.IntakeReceiptId == receiptId).ToListAsync())
            .Where(asset => filedAssetIds.Contains(asset.Id.ToString("N")))
            .ToArray();
        Assert.Equal(2, filedAssets.Length);
        Assert.All(filedAssets, asset =>
        {
            Assert.Equal("confirmed", asset.CustodyStatus);
            Assert.Equal("case-root", asset.BoxParentFolderId);
        });
    }

    /// <summary>
    /// A matched follow-up's message and its one attachment are filed on the
    /// Case by the promotion the intake processor runs, into Box through the
    /// store the Worker composes. Beside each upload, the only Box call is one
    /// read that proves the Case folder: the name is not looked up first,
    /// nothing is walked afterwards, and no folder is listed.
    /// </summary>
    [Fact]
    public async Task FilingAMailAttachmentReadsTheCaseFolderOnceBesideEachUpload()
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await SeedCaseAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var box = new CaseFolderBox();
        var client = new BoxContentClient(
            new(
                new Uri("https://api.box.com/2.0/"),
                new Uri("https://upload.box.com/api/2.0/"),
                CaseFolderBox.RootId,
                "test", "test", "test", "test", "test", "test", "holding-folder"),
            new HttpClient(box),
            new StaticBoxAuthorizationHeaderProvider(),
            TimeProvider.System);
        var promotion = new PromoteAssociatedIntakeCaseEvidence(
            services.GetRequiredService<IIntakeArtifactStore>(),
            new EfCaseArtifactCustody(
                services.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
                new BoxDocumentContentStore(client),
                services.GetRequiredService<IIntakeQuarantineArtifactStore>(),
                services.GetRequiredService<TimeProvider>()),
            services.GetRequiredService<IAutomaticCaseEvidencePromotionStore>());
        var email = FollowUp(includePhotos: false);
        var received = await services.GetRequiredService<ReceiveIntake>().ExecuteAsync(
            new(email.FileName, email.MediaType, email.Content,
                services.GetRequiredService<TimeProvider>().GetUtcNow(),
                "system-worker:approved-inbox-poller",
                new(IntakeSourceChannel.Mailbox, Guid.NewGuid().ToString("N"))),
            $"box-filing:{Guid.NewGuid():N}", default);
        await DispatchAsync(services, received.StagedReceiptId);

        Assert.Equal(QueuedIntakeProcessingOutcome.Completed,
            await ActivatorUtilities.CreateInstance<ProcessQueuedIntake>(services, promotion)
                .ExecuteAsync(received.StagedReceiptId, default));

        await using var db = await factory.Database.CreateContextAsync();
        var filed = (await (from occurrence in db.Set<DocumentOccurrenceEntity>()
                            join version in db.Set<DocumentVersionEntity>() on occurrence.VersionId equals version.Id
                            where occurrence.CaseId == caseId
                            select new { version.FileName, version.CustodyStatus, version.BoxFileId })
                .ToArrayAsync())
            .OrderBy(file => file.FileName, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(["1_Images-V1.pdf", "follow-up.eml"], filed.Select(file => file.FileName).ToArray());
        Assert.All(filed, file =>
        {
            Assert.Equal(DocumentCustodyStatus.Confirmed, file.CustodyStatus);
            Assert.StartsWith("file-", file.BoxFileId, StringComparison.Ordinal);
        });
        Assert.Equal(2, box.Requests.Count(request => request == CaseFolderBox.Upload));
        Assert.Equal(
            ["GET /2.0/folders/case-root", "GET /2.0/folders/case-root"],
            box.Requests.Where(request => request != CaseFolderBox.Upload).ToArray());
    }

    /// <summary>
    /// An original report e-mailed separately to an Audit created without it
    /// (#901): the matched follow-up files the report and Pegasus recognises
    /// it, so the Audit has its original report and its cells, in the
    /// system's name, and a replay records nothing more.
    /// </summary>
    [Fact]
    public async Task AMatchedFollowUpCarryingOneRecognisedReportGivesTheAuditItsOriginalReport()
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await SeedCaseAsync(factory);
        var receiptId = await MailboxIntakeTestData.SubmitAndProcessAsync(
            factory.Services, ReportFollowUp(("connexus-report.pdf", ConnexusReport)));

        await using var db = await factory.Database.CreateContextAsync();
        var files = await FiledRolesAsync(db, caseId);
        Assert.Equal(DocumentSemanticRole.AuditReport, files["connexus-report.pdf"]);
        Assert.Equal(DocumentSemanticRole.OriginalSource, files["report-follow-up.eml"]);
        var assessor = await db.CaseAssessmentFields.SingleAsync(item =>
            item.WorkId == caseId && item.FieldPath == AssessmentVocabulary.OriginalReportAssessor);
        Assert.Equal("Connexus Vehicle Assessors", assessor.Value);
        var recorded = await db.CaseWorkflowEvents.SingleAsync(item =>
            item.CaseId == caseId && item.EventType == "original_report_recorded");
        Assert.Equal(nameof(ActorKind.SystemWorker), recorded.ActorKind);
        Assert.Equal("Original report: connexus-report.pdf", recorded.Reason);

        var stagedId = await db.IntakeWorkItems.Where(value => value.ProcessedReceiptId == receiptId)
            .Select(value => value.StagedReceiptId).SingleAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        await IntakeWebDriver.CreateProcessor(scope.ServiceProvider).ExecuteAsync(stagedId, default);
        Assert.Equal(1, await db.CaseWorkflowEvents.CountAsync(item =>
            item.CaseId == caseId && item.EventType == "original_report_recorded"));
    }

    /// <summary>
    /// Two recognised reports in one e-mail mark neither (operator, 28
    /// September 2026): both are filed and staff choose with Mark as original
    /// report.
    /// </summary>
    [Fact]
    public async Task AMatchedFollowUpCarryingTwoRecognisedReportsMarksNeither()
    {
        using var factory = new IntakeWebApplicationFactory();
        var caseId = await SeedCaseAsync(factory);
        await MailboxIntakeTestData.SubmitAndProcessAsync(
            factory.Services,
            ReportFollowUp(
                ("connexus-report.pdf", ConnexusReport),
                ("connexus-supplementary.pdf", ConnexusReport + " Supplementary")));

        await using var db = await factory.Database.CreateContextAsync();
        var files = await FiledRolesAsync(db, caseId);
        Assert.Equal(DocumentSemanticRole.Correspondence, files["connexus-report.pdf"]);
        Assert.Equal(DocumentSemanticRole.Correspondence, files["connexus-supplementary.pdf"]);
        Assert.False(await db.CaseWorkflowEvents.AnyAsync(item =>
            item.CaseId == caseId && item.EventType == "original_report_recorded"));
    }

    private const string ConnexusReport =
        "Engineer Repairable Report Our Ref: 48450/1 Roadworthy: No Connexus Vehicle Assessors";

    private static TestEmail ReportFollowUp(params (string FileName, string Text)[] reports) =>
        IntakeTestEvidence.CreateEmail("report-follow-up.eml", "Please find the engineer's report attached.",
            subject: "Fw: (EREF10) RTA on 09/09/2031 : Mr Test Person (Our Ref: SCL/ND/48450/1)",
            attachments: [.. reports.Select(report =>
                (report.FileName, "application/pdf", IntakeTestEvidence.CreatePdf(report.Text)))]);

    private static async Task<Dictionary<string, DocumentSemanticRole>> FiledRolesAsync(
        PegasusDbContext db, Guid caseId) =>
        await (from occurrence in db.Set<DocumentOccurrenceEntity>()
               join version in db.Set<DocumentVersionEntity>() on occurrence.VersionId equals version.Id
               where occurrence.CaseId == caseId
               select new { version.FileName, occurrence.SemanticRole })
            .ToDictionaryAsync(file => file.FileName, file => file.SemanticRole);

    /// <summary>
    /// The sweep leaves a newly recorded version to the request that recorded
    /// it for the inline-filing grace; ages every version past it so the sweep
    /// offers what intake left pending.
    /// </summary>
    private static Task AgePastInlineFilingGraceAsync(PegasusDbContext db) =>
        db.Set<DocumentVersionEntity>().ExecuteUpdateAsync(update => update.SetProperty(
            value => value.CreatedAtUtc, DateTimeOffset.UtcNow.AddHours(-1)));

    private static async Task AssertFiledAsync(IntakeWebApplicationFactory factory, Guid caseId, DocumentCustodyStatus custody)
    {
        await using var db = await factory.Database.CreateContextAsync();
        var files = await (from occurrence in db.Set<DocumentOccurrenceEntity>()
                           join version in db.Set<DocumentVersionEntity>() on occurrence.VersionId equals version.Id
                           where occurrence.CaseId == caseId
                           select new { occurrence.SemanticRole, occurrence.Source, version.FileName, version.MediaType, version.CustodyStatus }).ToArrayAsync();
        Assert.Equal(6, files.Length);
        Assert.All(files, file =>
        {
            Assert.Equal(DocumentSource.Intake, file.Source);
            Assert.Equal(custody, file.CustodyStatus);
        });
        Assert.Single(files, file => file.FileName == "follow-up.eml" && file.SemanticRole == DocumentSemanticRole.OriginalSource);
        Assert.Single(files, file => file.FileName == "1_Images-V1.pdf" && file.SemanticRole == DocumentSemanticRole.Correspondence);
        Assert.Equal(4, files.Count(file => file.SemanticRole == DocumentSemanticRole.Image && file.MediaType == "image/jpeg"));
        Assert.DoesNotContain(files, file => file.MediaType == "image/png");
    }

    /// <summary>
    /// A follow-up the association-only route linked to its Case and did not
    /// file there, modelled through the existing promotion port.
    /// </summary>
    private static async Task<(IntakeReceipt Receipt, Guid StagedReceiptId)> LinkWithoutFilingAsync(
        IServiceProvider services, Guid caseId)
    {
        var email = FollowUp();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var received = await services.GetRequiredService<ReceiveIntake>().ExecuteAsync(
            new(email.FileName, email.MediaType, email.Content, now,
                "system-worker:approved-inbox-poller",
                new(IntakeSourceChannel.Mailbox, Guid.NewGuid().ToString("N"))),
            $"existing-link:{Guid.NewGuid():N}", default);
        var oldRoute = new PromoteAssociatedIntakeCaseEvidence(
            services.GetRequiredService<IIntakeArtifactStore>(),
            services.GetRequiredService<ICaseArtifactCustody>(), new AssociationOnlyPromotionStore());
        var oldProcessor = ActivatorUtilities.CreateInstance<ProcessQueuedIntake>(services, oldRoute);
        await DispatchAsync(services, received.StagedReceiptId);
        Assert.Equal(QueuedIntakeProcessingOutcome.Completed,
            await oldProcessor.ExecuteAsync(received.StagedReceiptId, default));
        var evaluation = Assert.IsType<IntakeEvaluationRevision>(await services.GetRequiredService<IIntakeWorkStore>()
            .GetCompletedEvaluationAsync(received.StagedReceiptId, default));
        var receipt = Assert.IsType<IntakeReceipt>(await services.GetRequiredService<IIntakeReceiptQueries>()
            .GetAsync(evaluation.ProcessedReceiptId, default));
        Assert.Equal(caseId, receipt.CurrentCaseId);
        return (receipt, received.StagedReceiptId);
    }

    private static async Task DispatchAsync(IServiceProvider services, Guid stagedId)
    {
        var store = services.GetRequiredService<IIntakeWorkStore>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var dispatch = Assert.IsType<IntakeWorkItem>(await store.ClaimDispatchAsync(stagedId, now, TimeSpan.FromMinutes(1), default));
        await store.MarkDispatchedAsync(dispatch.Id, Assert.IsType<string>(dispatch.LeaseToken), now, default);
    }

    private sealed class AssociationOnlyPromotionStore : IAutomaticCaseEvidencePromotionStore
    {
        public Task<AutomaticCaseEvidencePromotionPreparation> PrepareAsync(
            AutomaticCaseEvidencePromotionRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new AutomaticCaseEvidencePromotionPreparation(AutomaticCaseEvidencePromotionPreparationDisposition.NotApplicable));
    }

    private static async Task<Guid> SeedCaseAsync(IntakeWebApplicationFactory factory,
        bool instructionsComplete = true, bool hasCustodyRoot = true, int sequence = 9)
    {
        _ = factory.Services;
        await using var db = await factory.Database.CreateContextAsync();
        var principal = await db.Principals.SingleAsync(value => value.Code == "QDOS");
        var id = Guid.NewGuid();
        var now = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        db.AddRange(
            new CaseEntity
            {
                Id = id, PrincipalId = principal.Id, SequenceLineageId = principal.SequenceLineageId,
                Year = 2031, Sequence = sequence, Reference = $"a.QDOS31{sequence:000}", Type = "audit",
                InitialState = "NotReady", CustodyState = "confirmed", CreatedAtUtc = now,
                InstructionComplete = instructionsComplete, ImagesComplete = false,
                CustodyRootRemoteId = hasCustodyRoot ? "case-root" : null, ConcurrencyToken = Guid.NewGuid()
            },
            new CaseWorkflowEntity { CaseId = id, State = "NotReady", Version = 0, ConcurrencyToken = Guid.NewGuid() },
            new CaseDataSnapshotEntity
            {
                WorkId = id, CompletenessPolicyKey = "case_completeness", CompletenessPolicyVersion = 1,
                CompletenessPolicySatisfied = false, AcceptedAtUtc = now
            },
            new CaseMatchIndexEntity
            {
                CaseId = id, PrincipalCode = "QDOS", DurableClaimToken = "48450/1",
                MatchPolicyKey = "principal_case_match", MatchPolicyVersion = 1, UpdatedAtUtc = now
            });
        await db.SaveChangesAsync();
        return id;
    }

    private static TestEmail FollowUp(bool includePhotos = true)
    {
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        page.AddText("Your Ref: 48450/1", 10, new PdfPoint(36, 780), font);
        page.AddPng(ImageBytes(1990, 437, 31, SKEncodedImageFormat.Png), new PdfRectangle(36, 695, 556, 765));
        if (includePhotos)
        {
            for (var index = 0; index < 4; index++)
            {
                var jpeg = ImageBytes(584, 650, index + 1, SKEncodedImageFormat.Jpeg);
                Assert.True(jpeg.Length >= InstructionEvidenceImages.EmbeddedPhotographMinimumBytes);
                var left = 36 + index % 2 * 260;
                var bottom = 390 - index / 2 * 300;
                page.AddJpeg(jpeg, new PdfRectangle(left, bottom, left + 240, bottom + 270));
            }
        }
        return IntakeTestEvidence.CreateEmail("follow-up.eml", "Please find the attached photographs.",
            subject: "Fw: (EREF10) RTA on 09/09/2031 : Mr Test Person (Our Ref: SCL/ND/48450/1)",
            attachments: [("1_Images-V1.pdf", "application/pdf", builder.Build())]);
    }

    private static byte[] ImageBytes(int width, int height, int seed, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        var random = new Random(seed);
        bitmap.Pixels = Enumerable.Range(0, width * height)
            .Select(_ => new SKColor((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256)))
            .ToArray();
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 90);
        return encoded.ToArray();
    }

    private sealed class StaticBoxAuthorizationHeaderProvider : IBoxAuthorizationHeaderProvider
    {
        public Task<string> GetAuthorizationHeaderAsync(CancellationToken cancellationToken) =>
            Task.FromResult("Bearer test-token");

        public Task<bool> RenewIfDueAsync(CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    /// <summary>
    /// The Box a seeded Case files into: the approved root, the Case folder
    /// <c>case-root</c> under it, and the files uploaded there. It answers
    /// every read the store can make of that folder and its files, and records
    /// each request as its method and path.
    /// </summary>
    private sealed class CaseFolderBox : HttpMessageHandler
    {
        public const string RootId = "405543781910";
        public const string Upload = "POST /api/2.0/files/content";
        private const string CaseFolderId = "case-root";
        private readonly Lock gate = new();
        private readonly List<BoxFile> files = [];

        public ConcurrentQueue<string> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Enqueue($"{request.Method} {path}");
            if (request.Method == HttpMethod.Post && path == "/api/2.0/files/content")
            {
                var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
                using var attributes = JsonDocument.Parse(
                    await Part(multipart, "attributes").ReadAsStringAsync(cancellationToken));
                Assert.Equal(
                    CaseFolderId,
                    attributes.RootElement.GetProperty("parent").GetProperty("id").GetString());
                var name = attributes.RootElement.GetProperty("name").GetString()!;
                var content = await Part(multipart, "file").ReadAsByteArrayAsync(cancellationToken);
                lock (gate)
                {
                    if (files.Any(file => file.Name == name))
                    {
                        return new HttpResponseMessage(HttpStatusCode.Conflict)
                        {
                            Content = new StringContent(
                                """{"code":"item_name_in_use"}""", Encoding.UTF8, "application/json")
                        };
                    }
                    var id = $"file-{files.Count + 1}";
                    var created = new BoxFile(id, name, content);
                    files.Add(created);
                    return Json(new { entries = new[] { FileJson(created) } });
                }
            }
            if (request.Method == HttpMethod.Get && path == $"/2.0/folders/{CaseFolderId}")
            {
                return Json(new
                {
                    id = CaseFolderId,
                    name = "a.QDOS31009",
                    type = "folder",
                    parent = new { id = RootId },
                    trashed_at = (string?)null
                });
            }
            lock (gate)
            {
                if (request.Method == HttpMethod.Get && path == $"/2.0/folders/{CaseFolderId}/items")
                {
                    return Json(new { entries = files.Select(FileJson) });
                }
                if (request.Method == HttpMethod.Get
                    && files.SingleOrDefault(file => path == $"/2.0/files/{file.Id}") is { } found)
                {
                    return Json(FileJson(found));
                }
            }
            throw new InvalidOperationException($"Unexpected Box request: {request.Method} {request.RequestUri}");
        }

        private static HttpContent Part(MultipartFormDataContent multipart, string name) =>
            multipart.Single(part => part.Headers.ContentDisposition?.Name?.Trim('"') == name);

        private static object FileJson(BoxFile file) => new
        {
            id = file.Id,
            name = file.Name,
            type = "file",
            etag = "1",
            file_version = new { id = $"{file.Id}-version" },
            size = file.Content.LongLength,
            parent = new { id = CaseFolderId },
            trashed_at = (string?)null
        };

        private sealed record BoxFile(string Id, string Name, byte[] Content);

        private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
        };
    }
}
