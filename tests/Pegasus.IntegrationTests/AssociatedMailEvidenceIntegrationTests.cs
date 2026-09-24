using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
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
        var email = FollowUp();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var received = await services.GetRequiredService<ReceiveIntake>().ExecuteAsync(
            new(email.FileName, email.MediaType, email.Content, now,
                "system-worker:approved-inbox-poller",
                new(IntakeSourceChannel.Mailbox, Guid.NewGuid().ToString("N"))),
            $"existing-link:{Guid.NewGuid():N}", default);
        // Model the deployed association-only route through the existing promotion port.
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
        await DispatchAsync(services, received.StagedReceiptId);
        Assert.Equal(QueuedIntakeProcessingOutcome.Completed,
            await IntakeWebDriver.CreateProcessor(services).ExecuteAsync(received.StagedReceiptId, default));
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
        var gallery = await scope.ServiceProvider.GetRequiredService<ICaseEvidenceImageQueries>()
            .ListForCaseAsync(caseId, default);
        Assert.Equal(4, gallery.Count);
        Assert.All(gallery, image => Assert.True(image.IsCaseDocument));

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
                CaseId = id, CompletenessPolicyKey = "case_completeness", CompletenessPolicyVersion = 1,
                CompletenessPolicySatisfied = false, AcceptedAtUtc = now
            },
            new CaseMatchIndexEntity
            {
                CaseId = id, WorkProviderCode = "QDOS", DurableClaimToken = "48450/1",
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
}
