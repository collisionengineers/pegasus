using System.Data.Common;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Pegasus.Core.Address;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Tasks;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Harness = Pegasus.IntegrationTests.CaseDataCompletenessPersistenceTests.CaseDataHarness;

namespace Pegasus.IntegrationTests;

/// <summary>
/// One Case edit is one transaction. These tests prove that it either records
/// the whole authorized snapshot or none of it: a stale version, a lease that
/// is missing, foreign or expired, a discarded estimate, a terminal case and a
/// replayed operation key with a different payload each leave the record
/// exactly as it was.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseWorkspacePersistenceTests(CaseWorkspaceDatabase database)
    : IClassFixture<CaseWorkspaceDatabase>
{
    [Theory]
    [InlineData("a", "Current", "ReportPreparation", true)]
    [InlineData("b", "Primary", "ReportPreparation", true)]
    [InlineData("c", "Primary", "ReportSent", true)]
    [InlineData("d", "Current", "ReportSent", false)]
    public async Task PageFrameReadsUnlinkedSentEvidenceOnlyForPrimaryWorkOrReportPreparation(
        string row, string work, string state, bool expectEvidence)
    {
        Assert.NotEmpty(row);
        await using var harness = await Harness.CreateAsync();
        var evidenceId = Guid.NewGuid();
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseWorkflows SET State = {state} WHERE CaseId = {harness.CaseId}");
            context.Add(new CaseReportSentEvidenceEntity
            {
                Id = evidenceId,
                CaseId = null,
                MailboxIdentity = "test-mailbox",
                SentFolderIdentity = "sent",
                ImmutableItemIdentity = "sent-item",
                InternetMessageIdentity = "sent@test.invalid",
                ConversationIdentity = "conversation",
                ReplyChainIdentity = "reply-chain",
                SourceOccurrenceIdentity = "sent-source",
                SourceSha256 = new string('A', 64),
                MimeSha256 = new string('B', 64),
                SentAtUtc = harness.TimeProvider.GetUtcNow(),
                DiscoveredAtUtc = harness.TimeProvider.GetUtcNow(),
                DiscoveredByKind = nameof(ActorKind.SystemWorker),
                DiscoveredBySubjectId = "test",
                RetentionOperationKey = "retained-sent-gate",
                RetentionRequestHash = new string('C', 64)
            });
            await context.SaveChangesAsync();
        }

        var store = new EfCaseQueryStore(harness.Factory, harness.TimeProvider);
        var selector = work == "Primary" ? CaseWorkSelector.Primary : CaseWorkSelector.Current;
        var frame = Assert.IsType<CasePageFrameData>(
            await store.GetPageFrameAsync(harness.CaseId, selector, CancellationToken.None));

        Assert.Equal(
            expectEvidence ? [evidenceId] : [],
            frame.AvailableReportSentEvidence.Select(item => item.EvidenceId));
        Assert.Equal(
            [evidenceId],
            (await ((IAvailableReportSentEvidenceQueries)store).ListAsync(CancellationToken.None))
                .Select(item => item.EvidenceId));
    }

    [Fact]
    public async Task FocusedPageAndFilesReadsKeepHistoryAndTypedCaseDataOutOfTheirBodies()
    {
        await using var harness = await Harness.CreateAsync(database);
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var workflow = await context.CaseWorkflows.SingleAsync(
                row => row.CaseId == harness.CaseId);
            context.AddRange(
                new CaseWorkflowEventEntity
                {
                    Id = Guid.NewGuid(),
                    CaseId = harness.CaseId,
                    Workflow = workflow,
                    EventType = "operator_note",
                    OperationKey = "focused-page-history",
                    RequestHash = new string('e', 64),
                    ActorKind = nameof(ActorKind.Staff),
                    ActorSubjectId = harness.StaffActor.SubjectId,
                    ActorRolesJson = "[\"User\"]",
                    Reason = "Focused page history fixture.",
                    OccurredAtUtc = harness.TimeProvider.GetUtcNow(),
                    BeforeVersion = workflow.Version,
                    AfterVersion = workflow.Version
                },
                new CaseDocumentEntity
                {
                    Id = documentId,
                    CaseId = harness.CaseId,
                    Ordinal = 1,
                    SourceOccurrenceIdentity = "focused-page-file"
                },
                new DocumentVersionEntity
                {
                    Id = versionId,
                    DocumentId = documentId,
                    Version = 1,
                    FileName = "report.pdf",
                    MediaType = "application/pdf",
                    ContentLength = 1,
                    Sha256 = new string('d', 64),
                    CustodyStatus = DocumentCustodyStatus.Confirmed,
                    CreatedAtUtc = harness.TimeProvider.GetUtcNow(),
                    CreatedBy = "Staff:fixture",
                    IsCurrent = true
                },
                new DocumentOccurrenceEntity
                {
                    Id = occurrenceId,
                    CaseId = harness.CaseId,
                    DocumentId = documentId,
                    VersionId = versionId,
                    SemanticRole = DocumentSemanticRole.EngineerReport,
                    Source = DocumentSource.StaffUpload,
                    SourceOccurrenceIdentity = "focused-page-file",
                    RecordedAtUtc = harness.TimeProvider.GetUtcNow(),
                    OperationKey = "focused-page-file"
                });
            await context.SaveChangesAsync();
        }

        string connectionString;
        await using (var connectionContext = await harness.Factory.CreateDbContextAsync())
        {
            connectionString = connectionContext.Database.GetConnectionString()
                ?? throw new InvalidOperationException("The SQL fixture has no connection string.");
        }

        var pageOptions = new DbContextOptionsBuilder<PegasusDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(new RejectingQueryReadInterceptor("[CaseWorkflowEvents]"))
            .Options;
        var pageFactory = new PooledDbContextFactory<PegasusDbContext>(pageOptions);
        var frame = await new EfCaseQueryStore(pageFactory, harness.TimeProvider)
            .GetPageFrameAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None);
        var pageFrame = Assert.IsType<CasePageFrameData>(frame).Frame;

        var directFilesOptions = new DbContextOptionsBuilder<PegasusDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(new RejectingQueryReadInterceptor("[CaseWorkflows]", "[CaseAssessmentFields]"))
            .Options;
        var directFilesFactory = new PooledDbContextFactory<PegasusDbContext>(directFilesOptions);
        var directFilesStore = new EfCaseQueryStore(directFilesFactory, harness.TimeProvider);
        var directFiles = await directFilesStore.GetFilesSectionAsync(
            harness.CaseId,
            includeDocuments: false,
            frame: pageFrame,
            cancellationToken: CancellationToken.None);
        var fragmentFilesOptions = new DbContextOptionsBuilder<PegasusDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(new RejectingQueryReadInterceptor("[CaseAssessmentFields]"))
            .Options;
        var fragmentFilesFactory = new PooledDbContextFactory<PegasusDbContext>(fragmentFilesOptions);
        var fragmentFiles = await new EfCaseQueryStore(fragmentFilesFactory, harness.TimeProvider).GetFilesSectionAsync(
            harness.CaseId,
            includeDocuments: true,
            frame: null,
            cancellationToken: CancellationToken.None);
        var history = await new EfCaseQueryStore(harness.Factory, harness.TimeProvider)
            .ListHistoryAsync(harness.CaseId, CancellationToken.None);

        Assert.NotNull(frame);
        Assert.Equal(harness.CaseId, frame!.Frame.Workflow.CaseId);
        Assert.Single(frame.Documents);
        Assert.NotNull(directFiles);
        Assert.Empty(directFiles!.Documents);
        Assert.NotNull(fragmentFiles);
        Assert.Single(fragmentFiles!.Documents);
        Assert.NotEmpty(history);
        Assert.Null(typeof(CasePageFrameData).GetProperty("History"));
        Assert.Null(typeof(CaseFilesSectionData).GetProperty("Data"));
        Assert.Null(typeof(CaseFilesSectionData).GetProperty("AvailableReportSentEvidence"));
    }

    [Fact]
    public async Task RenderLeaseValidationUsesTheCurrentPersistedHashForValidWrongAndStaleTokens()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            harness.StaffActor,
            "focused-render-lease");
        var validator = new ValidateCaseRenderLease(
            new EfCaseQueryStore(harness.Factory, harness.TimeProvider),
            harness.TimeProvider);

        Assert.True(await validator.ExecuteAsync(
            new(harness.CaseId, harness.StaffActor, lease.Token),
            CancellationToken.None));
        Assert.False(await validator.ExecuteAsync(
            new(harness.CaseId, harness.StaffActor, new string('b', CaseEditAuthority.LeaseTokenLength)),
            CancellationToken.None));

        // A lapsed staff lease nobody claimed still renders as held; once a
        // colleague claims the Case the lapsed token is stale.
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(6));
        Assert.True(await validator.ExecuteAsync(
            new(harness.CaseId, harness.StaffActor, lease.Token),
            CancellationToken.None));

        await harness.AcquireLeaseAsync(
            initial.Version,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            "focused-render-colleague-lease");
        Assert.False(await validator.ExecuteAsync(
            new(harness.CaseId, harness.StaffActor, lease.Token),
            CancellationToken.None));
    }

    [Fact]
    public async Task ImageCropAndDamageCommitTogetherAndAStaleImageRollsBackBoth()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        var hash = new string('a', 64);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            context.AddRange(
                new CaseDocumentEntity { Id = documentId, CaseId = harness.CaseId, Ordinal = 99, SourceOccurrenceIdentity = "workspace-crop-fixture" },
                new DocumentVersionEntity { Id = versionId, DocumentId = documentId, Version = 1, FileName = "damage.jpg", MediaType = "image/jpeg", ContentLength = 1, Sha256 = hash, CustodyStatus = DocumentCustodyStatus.Confirmed, CreatedAtUtc = harness.TimeProvider.GetUtcNow(), CreatedBy = "Staff:fixture", IsCurrent = true },
                new DocumentOccurrenceEntity { Id = occurrenceId, CaseId = harness.CaseId, DocumentId = documentId, VersionId = versionId, SemanticRole = DocumentSemanticRole.Image, Source = DocumentSource.StaffUpload, SourceOccurrenceIdentity = "workspace-crop-fixture", RecordedAtUtc = harness.TimeProvider.GetUtcNow(), OperationKey = "seed-workspace-crop" });
            await context.SaveChangesAsync();
        }
        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "edit-workspace-crop");
        var crop = new CaseAssetCrop(.1m, .2m, .5m, .6m);
        var request = Request(harness, initial.Version, lease.Token, "save-workspace-crop") with
        {
            Damage = new([new DamageDisc(0.18, 0.2, 0.12)], null),
            ImagePreparation = new([new(occurrenceId, 99, null, CaseAssetRotation.Clockwise90, crop)])
        };
        await Assert.ThrowsAsync<CaseAssetPreparationVersionConflictException>(() => harness.WorkspaceStore.SaveAsync(request, default));
        Assert.Equal(initial.Version, (await harness.GetRequiredDataAsync()).Version);
        Assert.Equal(0, await AssessmentFieldCountAsync(harness));
        var saved = await harness.WorkspaceStore.SaveAsync(request with
        {
            ImagePreparation = new([new(occurrenceId, 0, null, CaseAssetRotation.Clockwise90, crop)])
        }, default);
        Assert.Equal(initial.Version + 1, saved.Version);
        Assert.Equal("[{\"x\":0.18,\"y\":0.2,\"r\":0.12}]", saved.Assessment.Field(AssessmentVocabulary.DamageMarkers)?.Value);
        var preparation = Assert.Single(await new EfCaseAssetPreparationStore(harness.Factory).ListForCaseAsync(harness.CaseId, default));
        Assert.Equal(crop, preparation.Crop);
        Assert.Equal(CaseAssetRotation.Clockwise90, preparation.Rotation);
        await using var check = await harness.Factory.CreateDbContextAsync();
        Assert.Equal(hash, (await check.Set<DocumentVersionEntity>().SingleAsync(item => item.Id == versionId)).Sha256);
        Assert.Equal(1, await WorkflowEventCountAsync(harness, "case_workspace_saved"));
    }

    [Fact]
    public async Task OneWorkspaceSaveWritesOneWorkflowEventAndBumpsTheVersionExactlyOnce()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            harness.StaffActor,
            "lease-workspace-1");
        var historyBefore = await harness.HistoryCountAsync();

        var result = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "workspace-save-1") with
            {
                Overview = Overview("Jane Example"),
                Inspection = Inspection(
                    CaseReportAddressTreatment.PhysicalVehicleLocation,
                    "5 Repairer Way, Leeds") with { StoragePerDay = 20m, RecoveryCharge = 120m },
                Vehicle = new(
                    "AB12 CDE",
                    "Ford",
                    "Focus",
                    new(72_850, CaseOdometerUnit.Miles, CaseOdometerUnit.Kilometres),
                    new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        [AssessmentVocabulary.VehicleCondition] = "good",
                        [AssessmentVocabulary.HistoryCheck] = "History clear"
                    }),
                Damage = new([new DamageDisc(0.18, 0.2, 0.12)], new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [AssessmentVocabulary.DamageUnrelated] = "Old rear bumper scrape",
                }),
                Completeness = new(true, true)
            },
            CancellationToken.None);

        Assert.False(result.WasReplay);
        Assert.Equal(initial.Version + 1, result.Version);
        Assert.Equal(historyBefore + 1, await harness.HistoryCountAsync());
        Assert.Equal(1, await WorkflowEventCountAsync(harness, "case_workspace_saved"));

        Assert.Equal("Jane Example", result.Data.Claimant.Name.Fact?.Value);
        Assert.Equal(initial.Claimant.Name.Fact, result.Data.Claimant.Name.Fact);
        Assert.Null(result.Data.Claimant.Name.Confirmed);
        Assert.Equal("AB12CDE", result.Data.Vehicle.Registration.Fact?.Value);
        Assert.Equal(initial.Vehicle.Registration.Fact, result.Data.Vehicle.Registration.Fact);
        Assert.Null(result.Data.Vehicle.Registration.Confirmed);
        Assert.Equal(72_850, result.Data.Vehicle.Mileage.Confirmed?.Value);
        Assert.Equal("5 Repairer Way, Leeds", result.Data.Inspection.Address.Confirmed?.Value);
        Assert.Equal(
            CaseInspectionMode.PhysicalAddress,
            result.Data.Inspection.Mode.Confirmed?.Value);
        Assert.NotNull(result.Data.Workspace);
        Assert.Equal(
            CaseReportAddressTreatment.PhysicalVehicleLocation,
            result.Data.Workspace!.InspectionAddressTreatment);
        Assert.Equal(CaseOdometerUnit.Kilometres, result.Data.Workspace.VehicleMileageDisplayUnit);
        Assert.Equal(
            "good",
            result.Assessment.Fields.Single(field => field.Path == AssessmentVocabulary.VehicleCondition).Value);
        Assert.Equal("History clear", result.Assessment.Field(AssessmentVocabulary.HistoryCheck)?.Value);
        Assert.Equal("20.00", result.Assessment.Field(AssessmentVocabulary.SettlementStoragePerDay)?.Value);
        Assert.Equal("120.00", result.Assessment.Field(AssessmentVocabulary.CostRecoveryCharge)?.Value);
        // The markers are kept as drawn and nothing is derived from them (v46).
        Assert.Equal(
            "[{\"x\":0.18,\"y\":0.2,\"r\":0.12}]",
            result.Assessment.Fields.Single(field => field.Path == AssessmentVocabulary.DamageMarkers).Value);
        Assert.DoesNotContain(result.Assessment.Fields, field => field.Path.StartsWith("assessment.impact_", StringComparison.Ordinal));
        Assert.Equal("Old rear bumper scrape", result.Assessment.Field(AssessmentVocabulary.DamageUnrelated)?.Value);
    }

    /// <summary>
    /// Two saves under one operation key that arrive together are one save.
    /// The workflow lock comes before the replay probe, so the second waits
    /// for the first and replays it: one workflow event, one version, no SQL
    /// error.
    /// </summary>
    [Fact]
    public async Task TwoSavesUnderOneOperationKeyAtOnceAreOneSaveAndOneReplay()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "lease-concurrent-save");
        var request = Request(harness, initial.Version, lease.Token, "concurrent-workspace-save") with
        {
            Overview = Overview("Jane Example")
        };

        // A held workflow lock stops both saves at their first statement, so
        // they meet the replay probe together once it is released.
        await using var blocker = await harness.Factory.CreateDbContextAsync();
        await using var blocking = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM [CaseWorkflows] WITH (UPDLOCK, HOLDLOCK) WHERE [CaseId] = {harness.CaseId}");
        var gate = new LockStatementGate(expected: 2);
        var store = new EfCaseWorkspaceStore(
            new PooledDbContextFactory<PegasusDbContext>(new DbContextOptionsBuilder<PegasusDbContext>()
                .UseSqlServer(blocker.Database.GetConnectionString())
                .AddInterceptors(gate)
                .Options),
            harness.TimeProvider);
        var saves = new[]
        {
            Task.Run(() => store.SaveAsync(request, CancellationToken.None)),
            Task.Run(() => store.SaveAsync(request, CancellationToken.None))
        };
        var first = await Task.WhenAny(gate.Reached.Task, Task.WhenAny(saves), Task.Delay(TimeSpan.FromMinutes(2)));
        if (first != gate.Reached.Task)
        {
            // A save that ended before it reached the lock reports its own failure.
            await Task.WhenAll(saves);
        }
        Assert.True(gate.Reached.Task.IsCompleted, "The saves never reached the workflow lock.");
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        await blocking.CommitAsync();

        var results = await Task.WhenAll(saves);

        Assert.Single(results, result => !result.WasReplay);
        Assert.Single(results, result => result.WasReplay);
        Assert.All(results, result => Assert.Equal(initial.Version + 1, result.Version));
        Assert.Equal(1, await WorkflowEventCountAsync(harness, "case_workspace_saved"));
    }

    /// <summary>
    /// The guide source cards are recorded by the Case save (23 September
    /// 2026): one workflow event and one version for the whole save, the same
    /// source and guide month replaces the earlier card, and a card whose
    /// figures are already recorded is left untouched, so an Apply pinned to
    /// its last-written stamp still holds. The save's one history line names
    /// the cards it recorded.
    /// </summary>
    [Fact]
    public async Task TheCaseSaveRecordsGuideCardsAndLeavesAnUnchangedCardAlone()
    {
        await using var harness = await Harness.CreateAsync(database);
        var april = new DateOnly(2030, 4, 1);
        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "lease-guide-1");

        var first = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "guide-save-1") with
            {
                Valuation = new([GuideCard(ValuationSource.Glasses, april, 12_500m), GuideCard(ValuationSource.Brego, april, 11_000m)])
            },
            CancellationToken.None);

        Assert.Equal(initial.Version + 1, first.Version);
        Assert.Equal(1, await WorkflowEventCountAsync(harness, "case_workspace_saved"));
        var valuations = new EfValuationStore(harness.Factory);
        var recorded = await valuations.ListForCaseAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.Equal(2, recorded.Count);
        var glasses = recorded.Single(card => card.Details.Source == ValuationSource.Glasses);
        Assert.Null(glasses.LastEditedAtUtc);

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        var again = await harness.AcquireLeaseAsync(first.Version, harness.StaffActor, "lease-guide-2");
        var second = await harness.WorkspaceStore.SaveAsync(
            Request(harness, first.Version, again.Token, "guide-save-2") with
            {
                Valuation = new([GuideCard(ValuationSource.Glasses, april, 12_500m), GuideCard(ValuationSource.Brego, april, 11_750m)])
            },
            CancellationToken.None);

        Assert.Equal(first.Version + 1, second.Version);
        Assert.Equal(2, await WorkflowEventCountAsync(harness, "case_workspace_saved"));
        recorded = await valuations.ListForCaseAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.Equal(2, recorded.Count);
        var unchanged = recorded.Single(card => card.Details.Source == ValuationSource.Glasses);
        Assert.Equal(DetailsModelStamp(glasses), DetailsModelStamp(unchanged));
        var replaced = recorded.Single(card => card.Details.Source == ValuationSource.Brego);
        Assert.Equal(11_750m, replaced.Details.RetailValue);
        Assert.NotNull(replaced.LastEditedAtUtc);

        await using var context = await harness.Factory.CreateDbContextAsync();
        // The save's own account; the Notes line is its session's (R466).
        var line = await context.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateType == "case" && item.AggregateId == harness.CaseId.ToString("D")
                && item.CorrelationId == "guide-save-2" && item.EventKind == "case_workspace_saved")
            .Select(item => item.Reason ?? string.Empty)
            .SingleAsync();
        Assert.Contains("Valuation: Brego Apr 2030", line, StringComparison.Ordinal);
        Assert.DoesNotContain("Glasses", line, StringComparison.Ordinal);
    }

    private static DateTimeOffset DetailsModelStamp(CaseValuation valuation) =>
        valuation.LastEditedAtUtc ?? valuation.RecordedAtUtc;

    private static ValuationDetails GuideCard(ValuationSource source, DateOnly guideMonth, decimal retail) =>
        new(source, new DateOnly(2030, 5, 6), new TimeOnly(10, 30), 42_000, retail, retail - 1_000m, guideMonth);

    [Fact]
    public async Task SavingUnchangedExtractedOverviewFactsAndANotePreservesReportFreshness()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var claimantFact = Assert.IsType<CaseDataValue<string>>(initial.Claimant.Name.Fact);
        Assert.Null(initial.Claimant.Name.Confirmed);
        var generationId = await SeedCurrentGenerationAsync(harness, initial.Version);
        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            harness.StaffActor,
            "unchanged-extracted-overview-lease");

        var saved = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "unchanged-extracted-overview-save") with
            {
                Overview = Overview(claimantFact.Value) with
                {
                    PrincipalNotes = "Unchanged extracted facts reviewed; note added.",
                },
            },
            CancellationToken.None);

        Assert.Equal(initial.Claimant.Name.Fact, saved.Data.Claimant.Name.Fact);
        Assert.Null(saved.Data.Claimant.Name.Confirmed);
        Assert.Equal(
            "Unchanged extracted facts reviewed; note added.",
            saved.Data.Workspace!.PrincipalNotes);
        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.Equal(
            nameof(CaseReportGenerationState.Confirmed),
            (await context.Set<CaseReportGenerationEntity>()
                .SingleAsync(item => item.Id == generationId)).State);
        Assert.Empty(await context.ActionHistory
            .Where(item => item.AggregateId == harness.CaseId.ToString("D")
                && item.EventKind == EfCaseReportGenerationStore.StaleEventKind)
            .ToArrayAsync());
    }

    [Theory]
    [InlineData("heading")]
    [InlineData("text")]
    [InlineData("order")]
    [InlineData("inclusion")]
    [InlineData("manual paragraph")]
    public async Task ReportWordingOnlyChangesPersistAndStaleTheCurrentGeneration(string change)
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var generationId = await SeedCurrentGenerationAsync(harness, initial.Version);
        var wording = change switch
        {
            "heading" => new CaseReportWording(
                ReportWordingComposition.NatureOfIncident,
                "Revised heading",
                null,
                null),
            "text" => new CaseReportWording(
                ReportWordingComposition.NatureOfIncident,
                null,
                "Revised report text.",
                null),
            "order" => new CaseReportWording(
                ReportWordingComposition.NatureOfIncident,
                null,
                null,
                3),
            "inclusion" => new CaseReportWording(
                ReportWordingComposition.NatureOfIncident,
                null,
                null,
                null,
                Included: false),
            "manual paragraph" => new CaseReportWording(
                "manual:additional",
                "Additional paragraph",
                "Additional report text.",
                9,
                Manual: true),
            _ => throw new ArgumentOutOfRangeException(nameof(change), change, null),
        };
        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            harness.StaffActor,
            $"wording-{change.Replace(' ', '-')}-lease");

        var saved = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, $"wording-{change.Replace(' ', '-')}-save") with
            {
                ReportWording = new([wording]),
            },
            CancellationToken.None);

        Assert.Equal(initial.Version + 1, saved.Version);
        await AssertGenerationStateAsync(
            harness,
            generationId,
            CaseReportGenerationState.Stale,
            expectedStaleEvents: 1,
            expectedReason: CaseReportStaleReasons.ReportContentChanged);
        await using var context = await harness.Factory.CreateDbContextAsync();
        var persisted = Assert.Single(await context.Set<CaseReportWordingEntity>()
            .Where(item => item.WorkId == harness.CaseId)
            .ToArrayAsync());
        Assert.Equal(wording.Key, persisted.BlockKey);
        Assert.Equal(wording.Title, persisted.Title);
        Assert.Equal(wording.Text, persisted.Text);
        Assert.Equal(wording.Order, persisted.Order);
        Assert.Equal(wording.Included, persisted.Included);
        Assert.Equal(wording.Manual, persisted.Manual);
    }

    [Fact]
    public async Task DirectAssessmentSaveStalesOnlyForAPrintedFactChange()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var generationId = await SeedCurrentGenerationAsync(harness, initial.Version);
        var actor = Engineer(harness);
        var assessmentStore = new EfCaseAssessmentStore(
            harness.Factory,
            harness.TimeProvider);
        var unprintedLease = await harness.AcquireLeaseAsync(
            initial.Version,
            actor,
            "direct-assessment-unprinted-lease");

        var unprinted = await assessmentStore.SaveAsync(
            new(
                harness.CaseId,
                initial.Version,
                actor,
                "direct-assessment-unprinted-save",
                "Recorded an unprinted rate card.",
                unprintedLease.Token,
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [AssessmentVocabulary.RateCard] = "standard",
                }),
            CancellationToken.None);

        await AssertGenerationStateAsync(
            harness,
            generationId,
            CaseReportGenerationState.Confirmed,
            expectedStaleEvents: 0);

        // The body type is recorded on the Case and the report no longer
        // prints it (operator, 27 September 2026), so it leaves a report current.
        var bodyLease = await harness.AcquireLeaseAsync(
            unprinted.CaseVersion,
            actor,
            "direct-assessment-body-lease");
        var body = await assessmentStore.SaveAsync(
            new(
                harness.CaseId,
                unprinted.CaseVersion,
                actor,
                "direct-assessment-body-save",
                "Recorded the body type.",
                bodyLease.Token,
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [AssessmentVocabulary.VehicleBody] = "Hatchback",
                }),
            CancellationToken.None);

        await AssertGenerationStateAsync(
            harness,
            generationId,
            CaseReportGenerationState.Confirmed,
            expectedStaleEvents: 0);

        var vinLease = await harness.AcquireLeaseAsync(
            body.CaseVersion,
            actor,
            "direct-assessment-vin-lease");
        await assessmentStore.SaveAsync(
            new(
                harness.CaseId,
                body.CaseVersion,
                actor,
                "direct-assessment-vin-save",
                "Corrected the printed VIN.",
                vinLease.Token,
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [AssessmentVocabulary.VehicleVin] = "KNABX312LCT122784",
                }),
            CancellationToken.None);

        await AssertGenerationStateAsync(
            harness,
            generationId,
            CaseReportGenerationState.Stale,
            expectedStaleEvents: 1,
            expectedReason: CaseReportStaleReasons.AssessmentFactsChanged);
    }

    [Fact]
    public async Task ACaseFactSaveStalesOnlyForAPrintedFactChange()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var generationId = await SeedCurrentGenerationAsync(harness, initial.Version);
        var noteLease = await harness.AcquireLeaseAsync(
            initial.Version,
            harness.StaffActor,
            "direct-case-data-note-lease");

        var noted = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, noteLease.Token, "direct-case-data-note-save") with
            {
                Overview = Overview("Jane Example") with { PrincipalNotes = "Photos will follow." },
            },
            CancellationToken.None);

        await AssertGenerationStateAsync(
            harness,
            generationId,
            CaseReportGenerationState.Confirmed,
            expectedStaleEvents: 0);

        var claimantLease = await harness.AcquireLeaseAsync(
            noted.Version,
            harness.StaffActor,
            "direct-case-data-claimant-lease");
        await harness.WorkspaceStore.SaveAsync(
            Request(harness, noted.Version, claimantLease.Token, "direct-case-data-claimant-save") with
            {
                Overview = Overview("Janet Example") with { PrincipalNotes = "Photos will follow." },
            },
            CancellationToken.None);

        await AssertGenerationStateAsync(
            harness,
            generationId,
            CaseReportGenerationState.Stale,
            expectedStaleEvents: 1,
            expectedReason: CaseReportStaleReasons.AssessmentFactsChanged);
    }

    /// <summary>
    /// The default signatory the editor showed comes back with the save. It is
    /// no pick, so the Case records none and the default stays a default
    /// (operator, 12 November 2026, R1019).
    /// </summary>
    [Fact]
    public async Task TheEffectiveDefaultSignatoryComingBackThroughWorkspaceIsNoPickAndDoesNotStaleTheReport()
    {
        await using var harness = await Harness.CreateAsync();
        var signOffEngineerId = await SeedDefaultSignOffEngineerAsync(harness);
        var initial = await harness.GetRequiredDataAsync();
        var generationId = await SeedCurrentGenerationAsync(harness, initial.Version);
        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            harness.StaffActor,
            "workspace-effective-signatory-lease");

        await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "workspace-effective-signatory-save") with
            {
                Report = new(
                    new Dictionary<string, string?>(StringComparer.Ordinal),
                    signOffEngineerId,
                    null),
            },
            CancellationToken.None);

        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.Null(
            (await context.CaseWorkflows.SingleAsync(item => item.CaseId == harness.CaseId))
                .SignOffEngineerId);
        Assert.Equal(
            nameof(CaseReportGenerationState.Confirmed),
            (await context.Set<CaseReportGenerationEntity>()
                .SingleAsync(item => item.Id == generationId)).State);
        Assert.Empty(await context.ActionHistory
            .Where(item => item.AggregateId == harness.CaseId.ToString("D")
                && item.EventKind == EfCaseReportGenerationStore.StaleEventKind)
            .ToArrayAsync());
    }

    [Fact]
    public async Task ReadinessIsReEvaluatedFromPersistedFactsInsideTheTransaction()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        Assert.True(initial.Completeness.Evaluation.SatisfiesPolicy);

        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            harness.StaffActor,
            "lease-readiness");
        var demoted = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "workspace-readiness-1") with
            {
                Completeness = new(false, null)
            },
            CancellationToken.None);

        Assert.False(demoted.Completeness.Values.InstructionComplete);
        Assert.True(demoted.Completeness.Values.ImagesComplete);
        Assert.False(demoted.Completeness.Evaluation.SatisfiesPolicy);
        Assert.Equal(CaseLifecycleState.NotReady, demoted.Data.State);

        var restoreLease = await harness.AcquireLeaseAsync(
            demoted.Version,
            harness.StaffActor,
            "lease-readiness-2");
        var restored = await harness.WorkspaceStore.SaveAsync(
            Request(harness, demoted.Version, restoreLease.Token, "workspace-readiness-2") with
            {
                Completeness = new(true, true)
            },
            CancellationToken.None);

        Assert.True(restored.Completeness.Evaluation.SatisfiesPolicy);
        Assert.Equal(CaseLifecycleState.Review, restored.Data.State);
    }

    [Fact]
    public async Task ASaveDoesNotDemoteCompletenessAsASideEffect()
    {
        // The Case save evaluates readiness from the row it just wrote, so
        // editing an unrelated fact cannot silently take a Review case back to
        // Not ready.
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            harness.StaffActor,
            "lease-no-demotion");

        var result = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "workspace-no-demotion") with
            {
                Overview = Overview("Jane Corrected")
            },
            CancellationToken.None);

        Assert.True(result.Completeness.Values.InstructionComplete);
        Assert.True(result.Completeness.Values.ImagesComplete);
        Assert.True(result.Completeness.Evaluation.SatisfiesPolicy);
        Assert.Equal(CaseLifecycleState.Review, result.Data.State);
    }

    [Fact]
    public async Task EditingMakeAlongsideAnUnchangedAcceptedRegistrationProjectsPartialConfirmedVehicleEvidence()
    {
        // The original instruction's registration remains its accepted Fact.
        // Correcting Make is a separate confirmation and must not make the
        // vehicle-evidence reader reject the Case or invent registration
        // confirmation provenance.
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            harness.StaffActor,
            "lease-confirmed-make");

        var saved = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "workspace-confirmed-make") with
            {
                // The section posts its other boxes as shown, the extracted
                // mileage included (R1077).
                Vehicle = new(
                    "AB12 CDE",
                    "Ford",
                    null,
                    new(Harness.FixtureMileage, CaseOdometerUnit.Miles, null),
                    new Dictionary<string, string?>(StringComparer.Ordinal))
            },
            CancellationToken.None);

        Assert.Equal("AB12CDE", saved.Data.Vehicle.Registration.Fact?.Value);
        Assert.Null(saved.Data.Vehicle.Registration.Confirmed);
        Assert.Equal("Ford", saved.Data.Vehicle.Make.Confirmed?.Value);

        var evidence = await new EfVehicleWorkflowStore(
                harness.Factory,
                harness.TimeProvider)
            .GetAsync(harness.CaseId, CancellationToken.None);

        Assert.NotNull(evidence);
        Assert.NotNull(evidence.Confirmed);
        Assert.Null(evidence.Confirmed!.Registration);
        Assert.Equal("Ford", evidence.Confirmed.Make?.Value);

        var cases = await new EfCaseQueryStore(harness.Factory, harness.TimeProvider)
            .SearchAsync(
                new(harness.StaffActor, new CaseSearchFilters()),
                CancellationToken.None);
        Assert.Contains(cases.Items, item => item.CaseId == harness.CaseId);
    }

    [Theory]
    [InlineData(CaseLifecycleState.ReportPreparation)]
    [InlineData(CaseLifecycleState.ReportSent)]
    public async Task SettlementAndReportSaveTogetherPreservingUnsubmittedFactsAndInvalidatingTheReport(
        CaseLifecycleState state)
    {
        await using var harness = await Harness.CreateAsync(database);
        var engineer = Engineer(harness, StaffRole.User);
        var signOffEngineerId = Guid.Parse(engineer.SubjectId);
        var generationId = Guid.NewGuid();
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseWorkflows SET State = {state.ToString()}, AssignedEngineerId = {signOffEngineerId} WHERE CaseId = {harness.CaseId}");
            context.Add(new CaseReportGenerationEntity
            {
                Id = generationId,
                CaseId = harness.CaseId,
                WorkId = harness.CaseId,
                CaseVersion = 0,
                SnapshotHash = new string('2', 64),
                SnapshotJson = ReportGenerationSnapshotFixture.Json(harness.CaseId, "seed-generation-current"),
                TemplateVersion = "assessment-report/v1",
                RendererVersion = "renderer/v1",
                State = nameof(CaseReportGenerationState.Confirmed),
                GeneratedAtUtc = harness.TimeProvider.GetUtcNow(),
                Version = 1
            });
            await context.SaveChangesAsync();
        }

        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(initial.Version, engineer, "lease-report-editors");
        var historyBefore = await harness.HistoryCountAsync();
        var request = Request(harness, initial.Version, lease.Token, "workspace-report-editors", engineer) with
        {
            Settlement = new(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [AssessmentVocabulary.Outcome] = "repairable",
                [AssessmentVocabulary.SettlementClaimantVatRegistered] = "false",
            }),
            Report = new(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [AssessmentVocabulary.AgreedFee] = "120.00",
                [AssessmentVocabulary.FeeDescriptionLines] = "Assessment report",
                [AssessmentVocabulary.ReportDiscloseGuideSource] = "true",
                [AssessmentVocabulary.ReportValuationCommentary] = "false",
                [AssessmentVocabulary.ReportIncludeUnrelatedDamage] = "false"
            }, signOffEngineerId, new DateOnly(2031, 5, 20))
        };

        var saved = await harness.WorkspaceStore.SaveAsync(request, CancellationToken.None);
        Assert.False(saved.WasReplay);
        Assert.Equal(initial.Version + 1, saved.Version);
        Assert.Equal(state, saved.Data.State);
        Assert.Equal(initial.Origin, saved.Data.Origin);
        Assert.Equal(initial.Claimant.Name.Fact, saved.Data.Claimant.Name.Fact);
        Assert.Null(saved.Data.Claimant.Name.Confirmed);
        Assert.Equal(initial.Inspection.Address.Confirmed, saved.Data.Inspection.Address.Confirmed);
        Assert.Equal(initial.Vehicle.Registration.Fact, saved.Data.Vehicle.Registration.Fact);
        Assert.Equal(initial.Workspace, saved.Data.Workspace);
        Assert.Equal(initial.Completeness, saved.Completeness);
        Assert.Equal(1, await WorkflowEventCountAsync(harness, "case_workspace_saved"));
        // One workspace event and one report-invalidation event, not one save per section.
        Assert.Equal(historyBefore + 2, await harness.HistoryCountAsync());
        Assert.Equal("false", saved.Assessment.Field(AssessmentVocabulary.SettlementClaimantVatRegistered)?.Value);
        Assert.Equal("Assessment report", saved.Assessment.Field(AssessmentVocabulary.FeeDescriptionLines)?.Value);
        Assert.Equal("120.00", saved.Assessment.Field(AssessmentVocabulary.AgreedFee)?.Value);
        Assert.Equal("2031-05-20", saved.Assessment.Field(AssessmentVocabulary.ReportDate)?.Value);
        // Every value this save wrote is staff's; the Good pre-incident
        // condition the Case started with (R598) was not posted.
        Assert.All(saved.Assessment.Fields.Where(field => field.RecordedBy != VehicleConditionDefault.RecorderId),
            field => Assert.Equal(ActorKind.Staff, field.RecordedByKind));
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var workflow = await context.CaseWorkflows.AsNoTracking().SingleAsync(row => row.CaseId == harness.CaseId);
            Assert.Equal(signOffEngineerId, workflow.SignOffEngineerId);
            var generation = await context.Set<CaseReportGenerationEntity>().AsNoTracking().SingleAsync(row => row.Id == generationId);
            Assert.Equal(nameof(CaseReportGenerationState.Stale), generation.State);
        }

        var replay = await harness.WorkspaceStore.SaveAsync(request, CancellationToken.None);
        Assert.True(replay.WasReplay);
        Assert.Equal(saved.Version, replay.Version);
        Assert.Equal(historyBefore + 2, await harness.HistoryCountAsync());
        await Assert.ThrowsAsync<CaseOperationConflictException>(() => harness.WorkspaceStore.SaveAsync(
            request with { Report = request.Report! with { ReportDate = new DateOnly(2031, 5, 21) } },
            CancellationToken.None));
        Assert.Equal(saved.Version, (await harness.GetRequiredDataAsync()).Version);
    }

    [Fact]
    public async Task AStaleExpectedVersionRefusesTheWholeWorkspaceWithoutPartialWrites()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            harness.StaffActor,
            "lease-stale");
        var historyBefore = await harness.HistoryCountAsync();

        await Assert.ThrowsAsync<CaseVersionConflictException>(() =>
            harness.WorkspaceStore.SaveAsync(
                Request(harness, initial.Version + 5, lease.Token, "workspace-stale") with
                {
                    Overview = Overview("Never written"),
                    Damage = new([new DamageDisc(0.5, 0.1, 0.12)], null)
                },
                CancellationToken.None));

        var after = await harness.GetRequiredDataAsync();
        Assert.Equal(initial.Version, after.Version);
        Assert.Equal("Jane Example", after.Claimant.Name.Current?.Value);
        Assert.Equal(historyBefore, await harness.HistoryCountAsync());
        Assert.Equal(0, await AssessmentFieldCountAsync(harness));
    }

    [Fact]
    public async Task SubmittedOverviewPreservesAcceptedProvenanceAndDoesNotConfirmAnUnpostedSuggestion()
    {
        await using var harness = await Harness.CreateAsync(database);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var source = await context.CaseDataFields.AsNoTracking().SingleAsync(row =>
                row.WorkId == harness.CaseId && row.FieldName == CaseDataFieldNames.InspectionAddress
                && row.ValueKind == CaseDataCodes.Fact);
            context.CaseDataFields.Add(new CaseDataFieldEntity
            {
                WorkId = harness.CaseId,
                FieldName = CaseDataFieldNames.ClaimantAddress,
                ValueKind = CaseDataCodes.Suggestion,
                ValueType = source.ValueType,
                Value = source.Value,
                SourceKind = source.SourceKind,
                SourceIdentity = source.SourceIdentity,
                SourceLabel = source.SourceLabel,
                PolicyKey = source.PolicyKey,
                PolicyVersion = source.PolicyVersion
            });
            await context.SaveChangesAsync();
        }
        var initial = await harness.GetRequiredDataAsync();
        Assert.NotNull(initial.Claimant.Name.Fact);
        Assert.Null(initial.Claimant.Name.Confirmed);
        Assert.NotNull(initial.Claimant.Address.Suggestion);
        Assert.Null(initial.Claimant.Address.Fact);
        Assert.NotNull(initial.Inspection.Address.Confirmed);
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        var lease = await harness.AcquireLeaseAsync(initial.Version, actor, "lease-preserve-provenance");
        var overview = Overview(initial.Claimant.Name.Fact.Value) with { VatStatus = "Not VAT registered" };
        var saved = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "workspace-preserve-provenance", actor) with
            {
                Overview = overview
            }, CancellationToken.None);

        Assert.Equal(initial.Claimant.Name.Fact, saved.Data.Claimant.Name.Fact);
        Assert.Null(saved.Data.Claimant.Name.Confirmed);
        Assert.Equal(initial.Claim.Number.Fact, saved.Data.Claim.Number.Fact);
        Assert.Null(saved.Data.Claim.Number.Confirmed);
        Assert.Equal(initial.Claimant.Address.Suggestion, saved.Data.Claimant.Address.Suggestion);
        Assert.Null(saved.Data.Claimant.Address.Confirmed);
        Assert.Equal(initial.Inspection.Address.Confirmed, saved.Data.Inspection.Address.Confirmed);
        Assert.Equal("Not VAT registered", saved.Data.Instruction.VatStatus.Confirmed?.Value);
        Assert.Equal(actor.SubjectId, saved.Data.Instruction.VatStatus.Confirmed?.ConfirmedByActor);

        var correctionLease = await harness.AcquireLeaseAsync(saved.Version, actor, "lease-genuine-correction");
        var corrected = await harness.WorkspaceStore.SaveAsync(
            Request(harness, saved.Version, correctionLease.Token, "workspace-genuine-correction", actor) with
            {
                Overview = overview with
                {
                    ClaimantName = "Jane Corrected",
                    ClaimantAddress = initial.Claimant.Address.Suggestion.Value
                }
            }, CancellationToken.None);
        Assert.Equal("Jane Corrected", corrected.Data.Claimant.Name.Confirmed?.Value);
        Assert.Equal(initial.Claimant.Name.Fact, corrected.Data.Claimant.Name.Fact);
        Assert.Equal(actor.SubjectId, corrected.Data.Claimant.Name.Confirmed?.ConfirmedByActor);
        // An explicitly supplied suggestion is a real confirmation, unlike an
        // unchanged accepted Fact or a suggestion that the form did not submit.
        Assert.Equal(initial.Claimant.Address.Suggestion.Value, corrected.Data.Claimant.Address.Confirmed?.Value);
        Assert.Equal(actor.SubjectId, corrected.Data.Claimant.Address.Confirmed?.ConfirmedByActor);
        Assert.Equal(initial.Claimant.Address.Suggestion, corrected.Data.Claimant.Address.Suggestion);
        Assert.Equal(saved.Data.Instruction.VatStatus.Confirmed, corrected.Data.Instruction.VatStatus.Confirmed);
        Assert.Equal(initial.Inspection.Address.Confirmed, corrected.Data.Inspection.Address.Confirmed);
    }

    /// <summary>
    /// #837: the one Save posts every control, so the values the intake
    /// extracted (Facts with no staff row) come back unchanged. The save reads
    /// both sides of its diff by the accepted-value rule, so its note names no
    /// field, and an untouched Fact is neither rewritten nor confirmed.
    /// </summary>
    [Fact]
    public async Task RepostingTheShownValuesUnchangedWritesANoteThatNamesNoField()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        Assert.NotNull(initial.Claimant.Name.Fact);
        Assert.Null(initial.Claimant.Name.Confirmed);
        CaseWorkspaceOverview overview;
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var snapshot = await EfCaseDataStore.SnapshotQuery(context, tracking: false)
                .SingleAsync(item => item.WorkId == harness.CaseId);
            var shown = CaseDataFieldWriter.ReadEditable(snapshot);
            var dueWork = await context.CaseDueWork.SingleOrDefaultAsync(item => item.CaseId == harness.CaseId);
            overview = new(
                shown.ClaimantName, shown.ClaimantContactNumber, shown.ClaimantAddress, shown.ClaimNumber,
                shown.IncidentDate,
                shown.AccidentCircumstances, shown.VatStatus, shown.RepairerAddress, null,
                PrincipalNotes: shown.PrincipalNotes, ClaimSourceNotes: shown.ClaimSourceNotes,
                DueBy: CaseDuePolicy.Resolve(dueWork?.DueBy, snapshot.Work.Case.AcceptedInspectionDeadline));
        }
        Assert.Equal(initial.Claimant.Name.Fact.Value, overview.ClaimantName);

        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "lease-837");
        var saved = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "workspace-837") with { Overview = overview },
            CancellationToken.None);

        Assert.Equal(initial.Claimant.Name.Fact, saved.Data.Claimant.Name.Fact);
        Assert.Null(saved.Data.Claimant.Name.Confirmed);
        Assert.Equal(initial.Claim.Number.Fact, saved.Data.Claim.Number.Fact);
        Assert.Null(saved.Data.Claim.Number.Confirmed);
        await using var verification = await harness.Factory.CreateDbContextAsync();
        var line = await verification.CaseHistory.AsNoTracking()
            .Where(item => item.CaseId == harness.CaseId && item.OperationKey == "workspace-837")
            .Select(item => item.Reason)
            .SingleAsync();
        Assert.StartsWith("No field changed", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// One note per save session (R466, operator, 28 October 2026). The
    /// session is the lease's lifetime: each session save keeps the lease and
    /// hands back its next token (R770), and saves made one after another with
    /// those tokens show in Notes as one line listing every field the session
    /// changed, once each, while every save keeps its own workflow event and
    /// action history. Done, or a lapse and a claim, starts a new session, and
    /// a Case note written between two saves puts the save after it on a line
    /// of its own.
    /// </summary>
    [Fact]
    public async Task TheSavesOfOneEditSessionShowAsOneNoteListingEveryFieldChanged()
    {
        await using var harness = await Harness.CreateAsync(database);
        var opened = (await harness.GetRequiredDataAsync()).Version;
        var version = opened;
        var token = (await harness.AcquireLeaseAsync(version, harness.StaffActor, "session-claim-1")).Token;

        async Task SaveAsync(string key, CaseWorkspaceOverview overview)
        {
            var saved = await harness.WorkspaceStore.SaveAsync(
                Request(harness, version, token, key) with { Overview = overview, ContinuesSession = true },
                CancellationToken.None);
            version = saved.Version;
            token = saved.SessionLease!.Token;
        }

        async Task<CaseHistoryEntry[]> SavedLinesAsync() =>
            [.. (await new EfCaseQueryStore(harness.Factory, harness.TimeProvider)
                    .ListHistoryAsync(harness.CaseId, CancellationToken.None))
                .Where(entry => entry.EventType == "case_workspace_saved")];

        await SaveAsync("session-save-1", Overview("Jane Example"));
        await SaveAsync("session-save-2", Overview("Jane Example") with { PrincipalNotes = "Photos to follow." });
        await SaveAsync("session-save-3", Overview("John Example") with { PrincipalNotes = "Photos to follow." });

        var session = Assert.Single(await SavedLinesAsync());
        Assert.Equal((opened, version), (session.BeforeVersion, session.AfterVersion));
        var line = session.Reason ?? string.Empty;
        Assert.Single(line.Split(" · ")[0].Split(", "), part => part == "Claimant name");
        Assert.Contains("Principal notes", line, StringComparison.Ordinal);
        Assert.EndsWith(" · Recorded the Case workspace", line, StringComparison.Ordinal);
        Assert.Single(line.Split(" · "), part => part == "Recorded the Case workspace");
        Assert.Equal(3, await WorkflowEventCountAsync(harness, "case_workspace_saved"));
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            Assert.Equal(3, await context.ActionHistory.AsNoTracking().CountAsync(item =>
                item.AggregateType == "case"
                && item.AggregateId == harness.CaseId.ToString("D")
                && item.EventKind == "case_workspace_saved"));
        }

        // Done releases the lease with the token the last save handed back:
        // the next Edit claims and opens a new session.
        await harness.WorkflowStore.ReleaseAsync(
            new(harness.CaseId, harness.StaffActor, "session-release", token),
            CancellationToken.None);
        var reopened = version;
        token = (await harness.AcquireLeaseAsync(version, harness.StaffActor, "session-claim-2")).Token;
        await SaveAsync("session-save-4", Overview("John Example") with { PrincipalNotes = "Parked outside." });

        var lines = await SavedLinesAsync();
        Assert.Equal(2, lines.Length);
        var latest = lines.Single(entry => entry.AfterVersion == version);
        Assert.Equal(reopened, latest.BeforeVersion);
        Assert.DoesNotContain("Claimant name", latest.Reason ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("Principal notes", latest.Reason ?? string.Empty, StringComparison.Ordinal);

        // A Case note between two saves keeps the timeline in order; the save
        // after it still carries the token the previous save handed back.
        await new EfCaseNoteStore(harness.Factory).AddAsync(
            new(harness.CaseId, harness.StaffActor, "session-note", "Called the repairer."),
            harness.TimeProvider.GetUtcNow(),
            CancellationToken.None);
        await SaveAsync("session-save-5", Overview("John Example") with { PrincipalNotes = "Parked on the drive." });

        Assert.Equal(3, (await SavedLinesAsync()).Length);
    }

    [Fact]
    public async Task MissingWrongHolderWrongTokenAndExpiredLeasesNeverWriteTheWorkspace()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            harness.StaffActor,
            "lease-denials");
        var historyBefore = await harness.HistoryCountAsync();
        var other = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

        await Assert.ThrowsAsync<ArgumentException>(() => harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, " ", "workspace-missing-lease") with
            {
                Overview = Overview("Never written")
            },
            CancellationToken.None));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.WorkspaceStore.SaveAsync(
                Request(harness, initial.Version, "not-the-issued-token", "workspace-wrong-token") with
                {
                    Overview = Overview("Never written")
                },
                CancellationToken.None));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.WorkspaceStore.SaveAsync(
                Request(harness, initial.Version, lease.Token, "workspace-wrong-holder", other) with
                {
                    Overview = Overview("Never written")
                },
                CancellationToken.None));

        // A lapsed lease carries on only while nobody claims the Case; once a
        // colleague has it, the lapsed token is no authority.
        harness.TimeProvider.Advance(TimeSpan.FromHours(4));
        await harness.AcquireLeaseAsync(initial.Version, other, "lease-denials-colleague");
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.WorkspaceStore.SaveAsync(
                Request(harness, initial.Version, lease.Token, "workspace-expired") with
                {
                    Overview = Overview("Never written")
                },
                CancellationToken.None));

        var after = await harness.GetRequiredDataAsync();
        Assert.Equal(initial.Version, after.Version);
        Assert.Equal("Jane Example", after.Claimant.Name.Current?.Value);
        Assert.Equal(historyBefore, await harness.HistoryCountAsync());
    }

    /// <summary>
    /// A staff holder's lease that lapsed while nobody claimed the Case carries on (operator,
    /// 6 October 2026): the holder's next save lands without a new claim. Once a colleague has
    /// claimed the Case, the lapsed token is refused and writes nothing.
    /// </summary>
    [Fact]
    public async Task ALapsedLeaseNobodyClaimedStillSavesUntilAColleagueClaimsTheCase()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "lease-lapsed-holder");

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(6));
        var saved = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "workspace-lapsed-holder") with
            {
                Damage = new([new DamageDisc(0.18, 0.2, 0.12)], null)
            },
            CancellationToken.None);
        Assert.Equal(initial.Version + 1, saved.Version);
        Assert.Equal("[{\"x\":0.18,\"y\":0.2,\"r\":0.12}]", saved.Assessment.Field(AssessmentVocabulary.DamageMarkers)?.Value);

        var again = await harness.AcquireLeaseAsync(saved.Version, harness.StaffActor, "lease-lapsed-holder-again");
        harness.TimeProvider.Advance(TimeSpan.FromMinutes(6));
        await harness.AcquireLeaseAsync(
            saved.Version,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            "lease-lapsed-colleague");
        var historyBefore = await harness.HistoryCountAsync();
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.WorkspaceStore.SaveAsync(
                Request(harness, saved.Version, again.Token, "workspace-lapsed-after-colleague") with
                {
                    Damage = new([new DamageDisc(0.5, 0.1, 0.12)], null)
                },
                CancellationToken.None));

        var after = await harness.GetRequiredDataAsync();
        Assert.Equal(saved.Version, after.Version);
        Assert.Equal(historyBefore, await harness.HistoryCountAsync());
        Assert.Equal(1, await WorkflowEventCountAsync(harness, "case_workspace_saved"));
    }

    /// <summary>
    /// Save-time cross-field pairings are gone (operator, 6 October 2026): a total loss with no
    /// salvage category or value saves, and readiness is what names the gap.
    /// </summary>
    [Fact]
    public async Task ATotalLossSavesWithoutItsSalvageCategoryOrValue()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var engineer = Engineer(harness);
        var lease = await harness.AcquireLeaseAsync(initial.Version, engineer, "lease-total-loss");

        var saved = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "workspace-total-loss", engineer) with
            {
                Settlement = new(new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [AssessmentVocabulary.Outcome] = "total_loss"
                })
            },
            CancellationToken.None);

        Assert.Equal(initial.Version + 1, saved.Version);
        Assert.Equal("total_loss", saved.Assessment.Field(AssessmentVocabulary.Outcome)?.Value);
        Assert.Null(saved.Assessment.Field(AssessmentVocabulary.SalvageCategory)?.Value);
        Assert.Null(saved.Assessment.Field(AssessmentVocabulary.SalvageValue)?.Value);
    }

    [Fact]
    public async Task AnExactReplayReturnsTheOriginalOutcomeAndAChangedPayloadConflicts()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            harness.StaffActor,
            "lease-replay");
        var request = Request(harness, initial.Version, lease.Token, "workspace-replay") with
        {
            Overview = Overview("Jane Replay")
        };

        var first = await harness.WorkspaceStore.SaveAsync(request, CancellationToken.None);
        var replayed = await harness.WorkspaceStore.SaveAsync(request, CancellationToken.None);

        Assert.False(first.WasReplay);
        Assert.True(replayed.WasReplay);
        Assert.Equal(first.Version, replayed.Version);
        Assert.Equal(first.Data, replayed.Data);
        Assert.Equal(1, await WorkflowEventCountAsync(harness, "case_workspace_saved"));

        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.WorkspaceStore.SaveAsync(
                request with { Overview = Overview("Different payload") },
                CancellationToken.None));
        Assert.Equal(1, await WorkflowEventCountAsync(harness, "case_workspace_saved"));
    }

    [Fact]
    public async Task AForcedFailureLeavesNeitherCaseNorAssessmentChanged()
    {
        // The estimate section is validated and persisted inside the same
        // transaction as the case facts and the assessment fields, so a refusal
        // there must undo the writes that already happened in this transaction.
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var engineer = Engineer(harness);
        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            engineer,
            "lease-partial");
        var historyBefore = await harness.HistoryCountAsync();
        var discarded = DiscardedEstimateId();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.WorkspaceStore.SaveAsync(
                Request(harness, initial.Version, lease.Token, "workspace-partial", engineer) with
                {
                    Overview = Overview("Never written"),
                    Vehicle = new(
                        null,
                        null,
                        null,
                        null,
                        new Dictionary<string, string?>(StringComparer.Ordinal)
                        {
                            [AssessmentVocabulary.VehicleCondition] = "poor"
                        }),
                    Settlement = new(new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        [AssessmentVocabulary.SettlementClaimantVatRegistered] = "false"
                    }),
                    Report = new(new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        [AssessmentVocabulary.FeeDescriptionLines] = "Never written",
                        [AssessmentVocabulary.AgreedFee] = "120.00"
                    }, null, new DateOnly(2031, 5, 20)),
                    // A discarded spec is gone: the refusal comes after the
                    // Case facts were written.
                    Estimate = new(discarded, new("Discarded estimate", null, null, 20m), [])
                },
                CancellationToken.None));

        var after = await harness.GetRequiredDataAsync();
        Assert.Equal(initial.Version, after.Version);
        Assert.Equal("Jane Example", after.Claimant.Name.Current?.Value);
        Assert.Equal(historyBefore, await harness.HistoryCountAsync());
        Assert.Equal(0, await AssessmentFieldCountAsync(harness));
    }

    [Fact]
    public async Task ADiscardedEstimateAndAClosedCaseRefuseTheWorkspaceSave()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var discarded = DiscardedEstimateId();
        var engineer = Engineer(harness);
        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            engineer,
            "lease-discarded");

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.WorkspaceStore.SaveAsync(
                Request(harness, initial.Version, lease.Token, "workspace-discarded", engineer) with
                {
                    Estimate = new(discarded, new("Discarded estimate", null, null, 20m), [])
                },
                CancellationToken.None));
        Assert.Contains("The estimate was not found on this case", refusal.Message, StringComparison.Ordinal);

        await MarkTerminalAsync(harness);
        await Assert.ThrowsAsync<CaseTerminalMutationException>(() =>
            harness.WorkspaceStore.SaveAsync(
                Request(harness, initial.Version, lease.Token, "workspace-terminal", engineer) with
                {
                    Overview = Overview("Never written")
                },
                CancellationToken.None));
    }

    [Fact]
    public async Task TheDraftEstimateHeaderAndLinesJoinTheSameTransaction()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var engineer = Engineer(harness);
        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            engineer,
            "lease-estimate");

        var result = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "workspace-estimate", engineer) with
            {
                Estimate = new(
                    null,
                    new("Estimate 1", 45m, 0m, 20m),
                    [new(
                        "new_part",
                        null,
                        "Front bumper",
                        1.5m,
                        240m,
                        false,
                        "BP-1",
                        null,
                        null,
                        null,
                        null,
                        1)])
            },
            CancellationToken.None);

        Assert.NotNull(result.Estimate);
        Assert.Equal(RepairSpecificationState.Draft, result.Estimate!.State);
        // A staff member's new spec is the one the Case uses at once.
        Assert.True(result.Estimate.IsCurrent);
        Assert.Equal("Estimate 1", result.Estimate.Details.Name);
        Assert.Equal(45m, result.Estimate.Details.LabourRate);
        var line = Assert.Single(result.Estimate.Lines);
        Assert.Equal("Front bumper", line.Description);
        Assert.Equal(1, line.Position);
        Assert.Equal(engineer.SubjectId, line.RecordedBy);
        Assert.Equal(initial.Version + 1, result.Version);
        Assert.Equal(1, await WorkflowEventCountAsync(harness, "case_workspace_saved"));
    }

    /// <summary>
    /// The one Case Save posts the whole Repair Spec editor every time (23
    /// September 2026): a spec saved exactly as recorded is not rewritten, so
    /// its lines keep their identities and the history line does not name it.
    /// </summary>
    [Fact]
    public async Task AnUnchangedSpecIsNotRewrittenByTheCaseSave()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var engineer = Engineer(harness);
        var lease = await harness.AcquireLeaseAsync(initial.Version, engineer, "lease-spec-1");
        var first = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "spec-save-1", engineer) with
            {
                Estimate = new(
                    null,
                    new("Estimate 1", 45m, 0m, 20m),
                    [new("new_part", null, "Front bumper", 1.5m, 240m, false, "BP-1", null, null, null, null, 1)])
            },
            CancellationToken.None);
        var spec = Assert.IsType<RepairSpecificationVersion>(first.Estimate);

        var again = await harness.AcquireLeaseAsync(first.Version, engineer, "lease-spec-2");
        var second = await harness.WorkspaceStore.SaveAsync(
            Request(harness, first.Version, again.Token, "spec-save-2", engineer) with
            {
                Estimate = new(
                    spec.SpecificationId,
                    spec.Details,
                    [.. spec.Lines.Select(RepairSpecificationScaling.ToInput)],
                    [.. spec.Lines.Select(line => (Guid?)line.Id)])
            },
            CancellationToken.None);

        Assert.Equal(first.Version + 1, second.Version);
        Assert.Equal(spec.Lines[0].Id, Assert.Single(second.Estimate!.Lines).Id);
        await using var context = await harness.Factory.CreateDbContextAsync();
        // The save's own account; the Notes line is its session's (R466).
        var line = await context.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateType == "case" && item.AggregateId == harness.CaseId.ToString("D")
                && item.CorrelationId == "spec-save-2" && item.EventKind == "case_workspace_saved")
            .Select(item => item.Reason ?? string.Empty)
            .SingleAsync();
        Assert.DoesNotContain("Estimate", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// The lines the Case save returns on its assessment are the saved
    /// specification's own, by position, and an exact replay returns the same.
    /// </summary>
    [Fact]
    public async Task AWorkspaceSaveReturnsTheAssessmentLinesInPositionOrder()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var engineer = Engineer(harness);
        var lease = await harness.AcquireLeaseAsync(initial.Version, engineer, "lease-line-order");
        var request = Request(harness, initial.Version, lease.Token, "workspace-line-order", engineer) with
        {
            Estimate = TwoLineEstimate()
        };

        var saved = await harness.WorkspaceStore.SaveAsync(request, CancellationToken.None);
        var replayed = await harness.WorkspaceStore.SaveAsync(request, CancellationToken.None);

        Assert.False(saved.WasReplay);
        Assert.True(replayed.WasReplay);
        var specificationLines = Assert.IsType<RepairSpecificationVersion>(saved.Estimate).Lines;
        Assert.Equal(Enumerable.Range(1, 2), specificationLines.Select(line => line.Position));
        Assert.Equal(Enumerable.Range(1, 2), saved.Assessment.EstimateLines.Select(line => line.Position));
        Assert.Equal(
            specificationLines.Select(line => line.Id).ToArray(),
            saved.Assessment.EstimateLines.Select(line => line.Id).ToArray());
        Assert.Equal(saved.Assessment.EstimateLines, replayed.Assessment.EstimateLines);
    }

    /// <summary>
    /// A committed save answers from the rows its own transaction holds and
    /// wrote (#1951): nothing is read back after the commit. The specification
    /// the save carried is new, so its lines are never read at all, and the
    /// work's snapshot is read once, the tracked read the save writes through.
    /// </summary>
    [Fact]
    public async Task ACommittedSaveReadsNothingBackAfterItCommits()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var engineer = Engineer(harness);
        var lease = await harness.AcquireLeaseAsync(initial.Version, engineer, "lease-no-read-back");
        var counter = new SqlStatementCounter();
        var store = await StoreCountingStatementsAsync(harness, counter);

        var saved = await store.SaveAsync(
            Request(harness, initial.Version, lease.Token, "workspace-no-read-back", engineer) with
            {
                Estimate = TwoLineEstimate()
            },
            CancellationToken.None);

        Assert.Equal(2, Assert.IsType<RepairSpecificationVersion>(saved.Estimate).Lines.Count);
        Assert.True(counter.CountContaining("FROM [CaseEstimateLines]") == 0, counter.Describe());
        Assert.True(counter.CountContaining("FROM [CaseDataSnapshots]") == 1, counter.Describe());
    }

    /// <summary>
    /// The committed save's answer is projected from the tracked rows, the
    /// replay's is read afresh (#1951); the two are the same Case. One save sets
    /// a value and clears another, writes one assessment field and removes
    /// another, and carries a new specification with two lines.
    /// </summary>
    [Fact]
    public async Task ACommittedSaveProjectsExactlyWhatItsReplayReads()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var engineer = Engineer(harness);
        var setupLease = await harness.AcquireLeaseAsync(initial.Version, engineer, "lease-projection-setup");
        var setup = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, setupLease.Token, "workspace-projection-setup", engineer) with
            {
                Overview = Overview("Jane Example") with { ClaimantContactNumber = "07700 900123" },
                Settlement = new(new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [AssessmentVocabulary.Outcome] = "total_loss"
                })
            },
            CancellationToken.None);
        Assert.NotNull(setup.Data.Claimant.ContactNumber.Current);
        Assert.Equal("total_loss", setup.Assessment.Field(AssessmentVocabulary.Outcome)?.Value);

        var lease = await harness.AcquireLeaseAsync(setup.Version, engineer, "lease-projection");
        var request = Request(harness, setup.Version, lease.Token, "workspace-projection", engineer) with
        {
            Overview = Overview("John Example"),
            Damage = new(null, new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [AssessmentVocabulary.DamageUnrelated] = "Old rear bumper scrape"
            }),
            Settlement = new(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [AssessmentVocabulary.Outcome] = null
            }),
            Estimate = TwoLineEstimate()
        };

        var first = await harness.WorkspaceStore.SaveAsync(request, CancellationToken.None);
        var replayed = await harness.WorkspaceStore.SaveAsync(request, CancellationToken.None);

        Assert.False(first.WasReplay);
        Assert.True(replayed.WasReplay);
        Assert.Equal("John Example", first.Data.Claimant.Name.Current?.Value);
        Assert.Null(first.Data.Claimant.ContactNumber.Current);
        Assert.Equal("Old rear bumper scrape", first.Assessment.Field(AssessmentVocabulary.DamageUnrelated)?.Value);
        Assert.Null(first.Assessment.Field(AssessmentVocabulary.Outcome));

        Assert.Equal(first.Version, replayed.Version);
        Assert.Equal(first.Data, replayed.Data);
        // The committed projection orders the fields ordinally and the
        // replay's read by the database collation, so both are put in one order.
        Assert.Equal(
            first.Assessment.Fields.OrderBy(field => field.Path, StringComparer.Ordinal).ToArray(),
            replayed.Assessment.Fields.OrderBy(field => field.Path, StringComparer.Ordinal).ToArray());
        Assert.Equal(first.Assessment.EstimateLines, replayed.Assessment.EstimateLines);
        var committed = Assert.IsType<RepairSpecificationVersion>(first.Estimate);
        var read = Assert.IsType<RepairSpecificationVersion>(replayed.Estimate);
        Assert.Equal(committed.SpecificationId, read.SpecificationId);
        Assert.Equal(committed.Version, read.Version);
        Assert.Equal(committed.State, read.State);
        Assert.Equal(committed.IsCurrent, read.IsCurrent);
        Assert.Equal(2, committed.Lines.Count);
        Assert.Equal(committed.Lines, read.Lines);
    }

    /// <summary>
    /// A session save keeps the lease it was made under (R770): the same
    /// holder, kind, operation key and generation, renewed for the lease's
    /// duration from the save, with its next token issued inside the save and
    /// handed back. The save records no lease operation.
    /// </summary>
    [Fact]
    public async Task ASessionSaveKeepsTheLeaseAndIssuesItsNextToken()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "lease-session-keep");
        var claimed = await WorkflowRowAsync(harness);
        var operationsBefore = await LeaseOperationCountAsync(harness);

        var saved = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "workspace-session-keep") with
            {
                Overview = Overview("Jane Session"),
                ContinuesSession = true
            },
            CancellationToken.None);

        var kept = Assert.IsType<CaseSessionLease>(saved.SessionLease);
        Assert.Equal(CaseEditAuthority.LeaseTokenLength, kept.Token.Length);
        Assert.True(kept.Token.All(char.IsAsciiHexDigit), kept.Token);
        Assert.NotEqual(lease.Token, kept.Token);
        Assert.Equal(harness.TimeProvider.GetUtcNow() + CaseEditAuthority.LeaseDuration, kept.ExpiresAtUtc);
        var row = await WorkflowRowAsync(harness);
        Assert.Equal(harness.StaffActor.SubjectId, row.EditLeaseHolder);
        Assert.Equal(nameof(ActorKind.Staff), row.EditLeaseHolderKind);
        Assert.NotNull(claimed.EditLeaseOperationKey);
        Assert.Equal(claimed.EditLeaseOperationKey, row.EditLeaseOperationKey);
        Assert.Equal(claimed.EditLeaseGeneration, row.EditLeaseGeneration);
        Assert.Equal(kept.ExpiresAtUtc, row.EditLeaseExpiresAtUtc);
        Assert.NotEqual(claimed.EditLeaseTokenHash, row.EditLeaseTokenHash);
        Assert.Equal(operationsBefore, await LeaseOperationCountAsync(harness));
    }

    /// <summary>
    /// The token a session save replaced has no authority left: a save or a
    /// heartbeat presenting it is refused as another window's would be, and
    /// writes nothing. The token the save handed back saves.
    /// </summary>
    [Fact]
    public async Task TheTokenASessionSaveReplacedIsRefusedOnSaveAndHeartbeat()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "lease-replaced-token");
        var saved = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "workspace-replaced-token-1") with
            {
                Overview = Overview("Jane Session"),
                ContinuesSession = true
            },
            CancellationToken.None);
        var historyBefore = await harness.HistoryCountAsync();

        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.WorkspaceStore.SaveAsync(
                Request(harness, saved.Version, lease.Token, "workspace-replaced-token-old") with
                {
                    Overview = Overview("Never written"),
                    ContinuesSession = true
                },
                CancellationToken.None));
        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.WorkflowStore.HeartbeatAsync(
                new(harness.CaseId, harness.StaffActor, lease.Token),
                CancellationToken.None));
        Assert.Equal(saved.Version, (await harness.GetRequiredDataAsync()).Version);
        Assert.Equal(historyBefore, await harness.HistoryCountAsync());

        var next = await harness.WorkspaceStore.SaveAsync(
            Request(harness, saved.Version, saved.SessionLease!.Token, "workspace-replaced-token-2") with
            {
                Overview = Overview("John Session"),
                ContinuesSession = true
            },
            CancellationToken.None);
        Assert.Equal(saved.Version + 1, next.Version);
        Assert.Equal("John Session", next.Data.Claimant.Name.Current?.Value);
    }

    /// <summary>
    /// FRD-14's two windows: the same member of staff holds the lease in two
    /// windows; window one saves and carries on with the next token, and window
    /// two, still holding the token and version it read, is refused rather
    /// than overwriting window one's save.
    /// </summary>
    [Fact]
    public async Task ASecondWindowsStaleSaveIsRefusedAfterASessionSave()
    {
        await using var harness = await Harness.CreateAsync(database);
        var opened = (await harness.GetRequiredDataAsync()).Version;
        var lease = await harness.AcquireLeaseAsync(opened, harness.StaffActor, "lease-two-windows");

        var windowOne = await harness.WorkspaceStore.SaveAsync(
            Request(harness, opened, lease.Token, "workspace-window-one") with
            {
                Overview = Overview("Window One"),
                ContinuesSession = true
            },
            CancellationToken.None);
        Assert.Equal(opened + 1, windowOne.Version);
        Assert.NotNull(windowOne.SessionLease);

        await Assert.ThrowsAsync<CaseEditLeaseConflictException>(() =>
            harness.WorkspaceStore.SaveAsync(
                Request(harness, opened, lease.Token, "workspace-window-two") with
                {
                    Overview = Overview("Window Two"),
                    ContinuesSession = true
                },
                CancellationToken.None));

        var after = await harness.GetRequiredDataAsync();
        Assert.Equal(windowOne.Version, after.Version);
        Assert.Equal("Window One", after.Claimant.Name.Current?.Value);
    }

    /// <summary>
    /// A session save sent again (a retried post) replays: it hands back the
    /// caller's current token, the one the first save issued, so the page
    /// carries on with the lease it holds.
    /// </summary>
    [Fact]
    public async Task AReplayedSessionSaveHandsBackTheCurrentToken()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "lease-session-replay");
        var request = Request(harness, initial.Version, lease.Token, "workspace-session-replay") with
        {
            Overview = Overview("Jane Replay"),
            ContinuesSession = true
        };

        var first = await harness.WorkspaceStore.SaveAsync(request, CancellationToken.None);
        var replayed = await harness.WorkspaceStore.SaveAsync(request, CancellationToken.None);

        Assert.False(first.WasReplay);
        Assert.True(replayed.WasReplay);
        var issued = Assert.IsType<CaseSessionLease>(first.SessionLease);
        var handedBack = Assert.IsType<CaseSessionLease>(replayed.SessionLease);
        Assert.Equal(issued.Token, handedBack.Token);
        Assert.Equal(issued.ExpiresAtUtc, handedBack.ExpiresAtUtc);
        Assert.Equal(1, await WorkflowEventCountAsync(harness, "case_workspace_saved"));
    }

    /// <summary>
    /// The Automation Actor's lease stands across its writes with one token
    /// (operator, 8 October 2026), and its saves are not a staff edit session:
    /// two saves under one Automation lease keep two Notes lines and the token
    /// is never rotated.
    /// </summary>
    [Fact]
    public async Task TwoAutomationSavesKeepTwoNotes()
    {
        await using var harness = await Harness.CreateAsync(database);
        var automation = ActionActor.Automation("pegasus-automation");
        var opened = (await harness.GetRequiredDataAsync()).Version;
        var lease = await harness.AcquireLeaseAsync(opened, automation, "lease-automation-saves");
        var claimed = await WorkflowRowAsync(harness);

        var first = await harness.WorkspaceStore.SaveAsync(
            Request(harness, opened, lease.Token, "workspace-automation-1", automation) with
            {
                Overview = Overview("Jane Automation")
            },
            CancellationToken.None);
        var second = await harness.WorkspaceStore.SaveAsync(
            Request(harness, first.Version, lease.Token, "workspace-automation-2", automation) with
            {
                Overview = Overview("John Automation")
            },
            CancellationToken.None);

        Assert.Null(first.SessionLease);
        Assert.Null(second.SessionLease);
        Assert.Equal(opened + 2, second.Version);
        var row = await WorkflowRowAsync(harness);
        Assert.Equal(claimed.EditLeaseTokenHash, row.EditLeaseTokenHash);
        Assert.Equal(automation.SubjectId, row.EditLeaseHolder);
        var lines = (await new EfCaseQueryStore(harness.Factory, harness.TimeProvider)
                .ListHistoryAsync(harness.CaseId, CancellationToken.None))
            .Where(entry => entry.EventType == "case_workspace_saved")
            .ToArray();
        Assert.Equal(2, lines.Length);
        Assert.Contains(lines, entry => (entry.BeforeVersion, entry.AfterVersion) == (opened, first.Version));
        Assert.Contains(lines, entry => (entry.BeforeVersion, entry.AfterVersion) == (first.Version, second.Version));
    }

    /// <summary>
    /// A save that does not ask to continue the session (a one-command or MCP
    /// write) ends the lease it was made under, as every staff write did
    /// before R770: no lease comes back and the token it presented is refused.
    /// </summary>
    [Fact]
    public async Task ASaveThatDoesNotContinueTheSessionEndsTheLease()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "lease-one-command");

        var saved = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "workspace-one-command") with
            {
                Overview = Overview("Jane Command")
            },
            CancellationToken.None);

        Assert.Null(saved.SessionLease);
        var row = await WorkflowRowAsync(harness);
        Assert.Null(row.EditLeaseHolder);
        Assert.Null(row.EditLeaseTokenHash);
        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() =>
            harness.WorkspaceStore.SaveAsync(
                Request(harness, saved.Version, lease.Token, "workspace-one-command-again") with
                {
                    Overview = Overview("Never written")
                },
                CancellationToken.None));
        Assert.Equal(saved.Version, (await harness.GetRequiredDataAsync()).Version);
    }

    /// <summary>
    /// A line cannot outlive its specification (operator, 11 October 2026: a
    /// discard deletes the specification with its lines), so a replay naming a
    /// specification that is gone has no lines to read and sends no statement
    /// that reads them.
    /// </summary>
    [Fact]
    public async Task AReplayWhoseSpecificationIsGoneReadsNoLines()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var engineer = Engineer(harness);
        var lease = await harness.AcquireLeaseAsync(initial.Version, engineer, "lease-lines-gone");
        var request = Request(harness, initial.Version, lease.Token, "workspace-lines-gone", engineer) with
        {
            Estimate = TwoLineEstimate()
        };
        var saved = await harness.WorkspaceStore.SaveAsync(request, CancellationToken.None);
        var specificationId = Assert.IsType<RepairSpecificationVersion>(saved.Estimate).SpecificationId;
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM CaseEstimateLines WHERE RepairSpecificationId = {specificationId}");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM CaseRepairSpecifications WHERE Id = {specificationId}");
        }
        var counter = new SqlStatementCounter();
        var store = await StoreCountingStatementsAsync(harness, counter);

        var replayed = await store.SaveAsync(request, CancellationToken.None);

        Assert.True(replayed.WasReplay);
        Assert.Null(replayed.Estimate);
        Assert.Empty(replayed.Assessment.EstimateLines);
        Assert.Equal(0, counter.CountContaining("FROM [CaseEstimateLines]"));
    }

    private static CaseWorkspaceEstimate TwoLineEstimate() => new(
        null,
        new("Estimate 1", 45m, 0m, 20m),
        [
            new("new_part", null, "Front bumper", 1.5m, 240m, false, "BP-1", null, null, null, null, 1),
            new("new_part", null, "Rear bumper", 1.5m, 180m, false, "BP-2", null, null, null, null, 1)
        ]);

    /// <summary>
    /// The harness's store, on a second factory over the same database whose
    /// every statement <paramref name="counter"/> keeps.
    /// </summary>
    private static async Task<EfCaseWorkspaceStore> StoreCountingStatementsAsync(
        Harness harness,
        SqlStatementCounter counter)
    {
        await using var connectionContext = await harness.Factory.CreateDbContextAsync();
        var connectionString = connectionContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException("The SQL fixture has no connection string.");
        return new(
            new PooledDbContextFactory<PegasusDbContext>(new DbContextOptionsBuilder<PegasusDbContext>()
                .UseSqlServer(connectionString)
                .AddInterceptors(counter)
                .Options),
            harness.TimeProvider);
    }

    /// <summary>
    /// A calculation in a Save that records a new guide month for the basis
    /// source is recorded against that new card, the one the page shows (one
    /// Save, 23 September 2026), names the Case version the save produced and
    /// carries the Case's mileage. The Engineer's Value is the box the same
    /// save posts; the report's Retail and Trade are that card's figures
    /// (operator, 8 October 2026). All three are the saving Engineer's own.
    /// </summary>
    [Fact]
    public async Task ACalculationIsRecordedAgainstTheBasisCardTheSameSaveRecorded()
    {
        await using var harness = await Harness.CreateAsync(database);
        var april = new DateOnly(2030, 4, 1);
        var may = new DateOnly(2030, 5, 1);
        var initial = await harness.GetRequiredDataAsync();
        var engineer = Engineer(harness);
        var lease = await harness.AcquireLeaseAsync(initial.Version, engineer, "lease-basis-1");
        var first = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "basis-save-1", engineer) with
            {
                Valuation = new([GuideCard(ValuationSource.Glasses, april, 12_500m)])
            },
            CancellationToken.None);
        var valuations = new EfValuationStore(harness.Factory);
        var aprilCard = Assert.Single(await valuations.ListForCaseAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None));

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        var again = await harness.AcquireLeaseAsync(first.Version, engineer, "lease-basis-2");
        var second = await harness.WorkspaceStore.SaveAsync(
            Request(harness, first.Version, again.Token, "basis-save-2", engineer) with
            {
                // The Case's own mileage, in kilometres, recorded by the same
                // save beside its extracted registration as shown (R1077).
                Vehicle = new(
                    "AB12 CDE",
                    null,
                    null,
                    new(100_000, CaseOdometerUnit.Kilometres, null),
                    new Dictionary<string, string?>(StringComparer.Ordinal)),
                Valuation = new(
                    [GuideCard(ValuationSource.Glasses, may, 13_000m)],
                    new ValuationCalculationSelection(aprilCard.ValuationId, false, null, [], 0m),
                    EngineerValue("13000"))
            },
            CancellationToken.None);

        var cards = await valuations.ListForCaseAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None);
        var mayCard = cards.Single(card => card.Details.Source == ValuationSource.Glasses && card.Details.GuideMonth == may);
        var applied = Assert.Single(await valuations.ListAppliedAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None));
        Assert.Equal(mayCard.ValuationId, applied.GuideValuationId);
        Assert.Equal(13_000m, applied.AcceptedEngineerValue);
        Assert.Equal(second.Version, applied.CaseVersion);
        Assert.Equal(2, await WorkflowEventCountAsync(harness, "case_workspace_saved"));
        // The Engineer's Value carries the Case's mileage in miles, never a
        // guide card's (operator, 24 September 2026): 100,000 km is 62,137 miles.
        var engineersValue = Assert.Single(cards, card => card.Details.Source == ValuationSource.EngineersValue);
        Assert.Equal(62_137, engineersValue.Details.Mileage);
        var fields = await AssessmentFieldsAsync(harness);
        foreach (var (path, value) in new[]
        {
            (AssessmentVocabulary.ValueRetail, "13000.00"),
            (AssessmentVocabulary.ValueTrade, "12000.00"),
            (AssessmentVocabulary.ValueEngineer, "13000.00"),
        })
        {
            var field = fields[path];
            Assert.Equal(value, field.Value);
            Assert.Equal(nameof(ActorKind.Staff), field.RecordedByKind);
            Assert.Equal(engineer.SubjectId, field.RecordedBy);
        }
    }

    /// <summary>
    /// The Engineer's Value is a typed box (operator, 26 September 2026): with
    /// no mileage and no guide card it saves, clears its blocker and records
    /// no calculation, and changing it stales the current report. The
    /// report's Retail and Trade are the chosen card's figures (operator,
    /// 8 October 2026): a save that types one is refused, and with no card
    /// chosen both stay blockers.
    /// </summary>
    [Fact]
    public async Task ATypedEngineersValueSavesWithoutAMileageAndRetailAndTradeAreNotTyped()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var engineer = Engineer(harness);
        var lease = await harness.AcquireLeaseAsync(initial.Version, engineer, "lease-typed-1");
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "typed-retail-refused", engineer) with
            {
                Valuation = new([], AssessmentFields: new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [AssessmentVocabulary.ValueRetail] = "12500",
                })
            },
            CancellationToken.None));
        Assert.Contains("chosen guide card", refusal.Message, StringComparison.Ordinal);

        var typed = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "typed-save-1", engineer) with
            {
                Valuation = new([], AssessmentFields: EngineerValue("12750"))
            },
            CancellationToken.None);

        var blockers = AssessmentPolicy.EvaluatePostReviewReadiness(typed.Assessment);
        Assert.DoesNotContain(blockers, item => item.Field == AssessmentVocabulary.ValueEngineer);
        Assert.Contains(blockers, item => item.Field == AssessmentVocabulary.ValueRetail);
        Assert.Contains(blockers, item => item.Field == AssessmentVocabulary.ValueTrade);
        Assert.Equal("12750.00", (await AssessmentFieldsAsync(harness))[AssessmentVocabulary.ValueEngineer].Value);
        var valuations = new EfValuationStore(harness.Factory);
        Assert.Empty(await valuations.ListForCaseAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None));
        Assert.Empty(await valuations.ListAppliedAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None));
        var generationId = await SeedCurrentGenerationAsync(harness, typed.Version);

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        var again = await harness.AcquireLeaseAsync(typed.Version, engineer, "lease-typed-2");
        await harness.WorkspaceStore.SaveAsync(
            Request(harness, typed.Version, again.Token, "typed-save-2", engineer) with
            {
                Valuation = new([], AssessmentFields: EngineerValue("13000"))
            },
            CancellationToken.None);

        await AssertGenerationStateAsync(
            harness,
            generationId,
            CaseReportGenerationState.Stale,
            expectedStaleEvents: 1,
            expectedReason: CaseReportStaleReasons.AssessmentFactsChanged);
    }

    private static Dictionary<string, string?> EngineerValue(string engineer) =>
        new(StringComparer.Ordinal)
        {
            [AssessmentVocabulary.ValueEngineer] = engineer,
        };
    /// <summary>
    /// The Inspection date is the date the report says the damage was
    /// assessed (issue #834), so changing it through the workspace stales the
    /// current report. The Inspection deadline is not printed, so moving it
    /// leaves the report current.
    /// </summary>
    [Fact]
    public async Task InspectionDateSaveThroughTheWorkspaceStalesTheReport()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var inspection = Inspection(CaseReportAddressTreatment.PhysicalVehicleLocation, "1 Depot Road");
        var firstLease = await harness.AcquireLeaseAsync(
            initial.Version,
            harness.StaffActor,
            "inspection-date-first-lease");
        var inspected = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, firstLease.Token, "inspection-date-first-save") with
            {
                Inspection = inspection
            },
            CancellationToken.None);
        var generationId = await SeedCurrentGenerationAsync(harness, inspected.Version);

        var deadlineLease = await harness.AcquireLeaseAsync(
            inspected.Version,
            harness.StaffActor,
            "inspection-deadline-lease");
        var rescheduled = await harness.WorkspaceStore.SaveAsync(
            Request(harness, inspected.Version, deadlineLease.Token, "inspection-deadline-save") with
            {
                Inspection = inspection with { InspectionDeadline = new DateOnly(2031, 5, 25) }
            },
            CancellationToken.None);

        await AssertGenerationStateAsync(
            harness,
            generationId,
            CaseReportGenerationState.Confirmed,
            expectedStaleEvents: 0);

        var dateLease = await harness.AcquireLeaseAsync(
            rescheduled.Version,
            harness.StaffActor,
            "inspection-date-change-lease");
        await harness.WorkspaceStore.SaveAsync(
            Request(harness, rescheduled.Version, dateLease.Token, "inspection-date-change-save") with
            {
                Inspection = inspection with
                {
                    InspectionDate = new DateOnly(2031, 5, 21),
                    InspectionDeadline = new DateOnly(2031, 5, 25)
                }
            },
            CancellationToken.None);

        await AssertGenerationStateAsync(
            harness,
            generationId,
            CaseReportGenerationState.Stale,
            expectedStaleEvents: 1,
            expectedReason: CaseReportStaleReasons.AssessmentFactsChanged);
    }

    /// <summary>
    /// An edit from the Inspection view once the Audit exists writes the
    /// primary work only and moves nothing that is the Case's: its state, due
    /// work, completeness and accepted deadline stay as the Audit has them
    /// (operator, 2 October 2026). It stales the Inspection's report, never
    /// the Audit's.
    /// </summary>
    [Fact]
    public async Task APrimarySaveOnAnAuditedCaseWritesThePrimaryWorkOnly()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var auditWorkId = await Reports.CaseReportGenerationPersistenceTests.GiveAuditAsync(
            harness.Factory, harness.CaseId, harness.TimeProvider.GetUtcNow());
        var inspectionGenerationId = await SeedCurrentGenerationAsync(harness, initial.Version);
        var auditGenerationId = await SeedCurrentGenerationAsync(harness, initial.Version, auditWorkId);
        DateOnly? acceptedDeadline;
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            acceptedDeadline = (await context.Cases.SingleAsync(item => item.Id == harness.CaseId))
                .AcceptedInspectionDeadline;
        }

        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "primary-save-lease");
        var saved = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "primary-save") with
            {
                Work = CaseWorkSelector.Primary,
                Overview = Overview("Jane Example") with { DueBy = new DateOnly(2031, 5, 18) },
                Inspection = Inspection(CaseReportAddressTreatment.PhysicalVehicleLocation, "1 Depot Road") with
                {
                    InspectionDate = new DateOnly(2031, 5, 21),
                    InspectionDeadline = new DateOnly(2031, 5, 25),
                },
                Completeness = new(false, false),
            },
            CancellationToken.None);

        Assert.Equal(CaseLifecycleState.Review, saved.Data.State);
        Assert.Equal(new DateOnly(2031, 5, 21), (await ReadEditableCaseDataAsync(harness)).InspectionDate);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            Assert.False(await context.CaseDataFields.AnyAsync(item => item.WorkId == auditWorkId));
            Assert.False(await context.CaseAssessmentFields.AnyAsync(item => item.WorkId == auditWorkId));
            Assert.False(await context.CaseDueWork.AnyAsync(item => item.CaseId == harness.CaseId));
            var caseRow = await context.Cases.SingleAsync(item => item.Id == harness.CaseId);
            Assert.True(caseRow.InstructionComplete);
            Assert.True(caseRow.ImagesComplete);
            Assert.Equal(acceptedDeadline, caseRow.AcceptedInspectionDeadline);
            var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == harness.CaseId);
            Assert.Equal(nameof(CaseLifecycleState.Review), workflow.State);
            var generations = await context.Set<CaseReportGenerationEntity>()
                .ToDictionaryAsync(item => item.Id, item => item.State);
            Assert.Equal(nameof(CaseReportGenerationState.Stale), generations[inspectionGenerationId]);
            Assert.Equal(nameof(CaseReportGenerationState.Confirmed), generations[auditGenerationId]);
        }
    }

    /// <summary>
    /// A calculation records without the Case's mileage (operator, 26
    /// September 2026): its Engineer's Value row carries none.
    /// </summary>
    [Fact]
    public async Task ACalculationRecordsWithoutTheCaseMileage()
    {
        await using var harness = await Harness.CreateAsync(database, mileage: null);
        var april = new DateOnly(2030, 4, 1);
        var initial = await harness.GetRequiredDataAsync();
        var engineer = Engineer(harness);
        var lease = await harness.AcquireLeaseAsync(initial.Version, engineer, "lease-no-mileage-1");
        var first = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "no-mileage-save-1", engineer) with
            {
                Valuation = new([GuideCard(ValuationSource.Glasses, april, 12_500m)])
            },
            CancellationToken.None);
        var valuations = new EfValuationStore(harness.Factory);
        var card = Assert.Single(await valuations.ListForCaseAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None));

        var again = await harness.AcquireLeaseAsync(first.Version, engineer, "lease-no-mileage-2");
        var second = await harness.WorkspaceStore.SaveAsync(
            Request(harness, first.Version, again.Token, "no-mileage-save-2", engineer) with
            {
                Valuation = new([], new ValuationCalculationSelection(card.ValuationId, false, 0.10m, [], 0m))
            },
            CancellationToken.None);

        var applied = Assert.Single(await valuations.ListAppliedAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None));
        Assert.Equal(second.Version, applied.CaseVersion);
        var engineersValue = Assert.Single(
            await valuations.ListForCaseAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None),
            item => item.Details.Source == ValuationSource.EngineersValue);
        Assert.Null(engineersValue.Details.Mileage);
    }

    /// <summary>
    /// Choosing a guide card typed in the same edit (operator, 28
    /// September 2026): the card has no identity yet, so the Save is told its
    /// source and records the calculation against the card it has just
    /// recorded, in the one Save.
    /// </summary>
    [Fact]
    public async Task UsingACardTypedInTheSameSaveRecordsTheCalculationAgainstThatNewCard()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var engineer = Engineer(harness);
        var lease = await harness.AcquireLeaseAsync(initial.Version, engineer, "lease-use-new-1");

        var saved = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "use-new-save-1", engineer) with
            {
                Valuation = new(
                    [GuideCard(ValuationSource.Glasses, new DateOnly(2030, 4, 1), 12_500m)],
                    new ValuationCalculationSelection(Guid.Empty, false, null, [], 0m)
                    {
                        GuideSource = ValuationSource.Glasses,
                    },
                    EngineerValue("12500"))
            },
            CancellationToken.None);

        var valuations = new EfValuationStore(harness.Factory);
        var card = Assert.Single(
            await valuations.ListForCaseAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None),
            item => item.Details.Source == ValuationSource.Glasses);
        var applied = Assert.Single(
            await valuations.ListAppliedAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None));
        Assert.Equal(card.ValuationId, applied.GuideValuationId);
        Assert.Equal(12_500m, applied.AcceptedEngineerValue);
        Assert.Equal(saved.Version, applied.CaseVersion);
    }

    /// <summary>
    /// An Engineer's Value typed over the calculated figure is the Engineer's
    /// own: it is recorded as the staff member's value and the Save does not
    /// attribute it to the guide card by recording the calculation. The
    /// report's Retail and Trade still follow the chosen card.
    /// </summary>
    [Fact]
    public async Task AnEngineersValueTypedOverTheCalculationIsTheEngineersOwnAndNotRecordedAgainstTheCard()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var engineer = Engineer(harness);
        var lease = await harness.AcquireLeaseAsync(initial.Version, engineer, "lease-own-value-1");

        await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "own-value-save-1", engineer) with
            {
                Valuation = new(
                    [GuideCard(ValuationSource.Glasses, new DateOnly(2030, 4, 1), 12_500m)],
                    new ValuationCalculationSelection(Guid.Empty, false, null, [], 0m)
                    {
                        GuideSource = ValuationSource.Glasses,
                    },
                    EngineerValue("14000"))
            },
            CancellationToken.None);

        var valuations = new EfValuationStore(harness.Factory);
        Assert.Empty(await valuations.ListAppliedAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None));
        Assert.DoesNotContain(
            await valuations.ListForCaseAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None),
            item => item.Details.Source == ValuationSource.EngineersValue);
        var fields = await AssessmentFieldsAsync(harness);
        var field = fields[AssessmentVocabulary.ValueEngineer];
        Assert.Equal("14000.00", field.Value);
        Assert.Equal("12500.00", fields[AssessmentVocabulary.ValueRetail].Value);
        Assert.Equal("11500.00", fields[AssessmentVocabulary.ValueTrade].Value);
        Assert.Equal(nameof(ActorKind.Staff), field.RecordedByKind);
        Assert.Equal(engineer.SubjectId, field.RecordedBy);
    }

    /// <summary>
    /// R311: an AI market research card has no Trade, so choosing it writes its
    /// Retail, no Trade and the basis marker, and the report no longer names
    /// Trade; a guide card chosen after it clears the marker.
    /// </summary>
    [Fact]
    public async Task AnAiMarketResearchBasisWritesItsRetailAndNoTradeAndTradeStopsBlockingTheReport()
    {
        await using var harness = await Harness.CreateAsync();
        var initial = await harness.GetRequiredDataAsync();
        var engineer = Engineer(harness);
        var lease = await harness.AcquireLeaseAsync(initial.Version, engineer, "lease-research-basis-1");
        var researchId = Guid.NewGuid();
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            context.CaseValuations.Add(new CaseValuationEntity
            {
                Id = researchId,
                WorkId = harness.CaseId,
                Source = nameof(ValuationSource.AiMarketResearch),
                Date = new DateOnly(2030, 5, 6),
                Time = new TimeOnly(10, 30),
                GuideMonth = new DateOnly(2030, 4, 1),
                Mileage = 42_000,
                RetailValue = 13_000m,
                TradeValue = null,
                RecordedBy = "automation",
                RecordedAtUtc = DateTimeOffset.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var saved = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "research-basis-save-1", engineer) with
            {
                Valuation = new(
                    [],
                    new ValuationCalculationSelection(researchId, false, null, [], 0m),
                    EngineerValue("13000"))
            },
            CancellationToken.None);

        var fields = await AssessmentFieldsAsync(harness);
        Assert.Equal("13000.00", fields[AssessmentVocabulary.ValueRetail].Value);
        Assert.False(fields.ContainsKey(AssessmentVocabulary.ValueTrade));
        Assert.Equal("true", fields[AssessmentVocabulary.ValueFromMarketResearch].Value);
        var blockers = AssessmentPolicy.EvaluatePostReviewReadiness(saved.Assessment);
        Assert.DoesNotContain(blockers, item => item.Field == AssessmentVocabulary.ValueTrade);

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        var again = await harness.AcquireLeaseAsync(saved.Version, engineer, "lease-research-basis-2");
        var second = await harness.WorkspaceStore.SaveAsync(
            Request(harness, saved.Version, again.Token, "research-basis-save-2", engineer) with
            {
                Valuation = new(
                    [GuideCard(ValuationSource.Glasses, new DateOnly(2030, 4, 1), 13_000m)],
                    new ValuationCalculationSelection(Guid.Empty, false, null, [], 0m)
                    {
                        GuideSource = ValuationSource.Glasses,
                    },
                    EngineerValue("13000"))
            },
            CancellationToken.None);

        var after = await AssessmentFieldsAsync(harness);
        Assert.Equal("12000.00", after[AssessmentVocabulary.ValueTrade].Value);
        Assert.False(after.ContainsKey(AssessmentVocabulary.ValueFromMarketResearch));
        Assert.DoesNotContain(
            AssessmentPolicy.EvaluatePostReviewReadiness(second.Assessment),
            item => item.Field == AssessmentVocabulary.ValueTrade);
    }

    /// <summary>
    /// Choosing a card is a decision, so a Save that carries it while the
    /// Engineer's Value box no longer holds the calculated figure (the preview
    /// had not landed, or the box was typed over) is refused with a message and
    /// writes nothing: it is never dropped silently.
    /// </summary>
    [Fact]
    public async Task ChosenCardWithAnEngineersValueBoxThatDoesNotMatchTheCalculationIsRefused()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var engineer = Engineer(harness);
        var lease = await harness.AcquireLeaseAsync(initial.Version, engineer, "lease-use-stale-1");

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "use-stale-save-1", engineer) with
            {
                Valuation = new(
                    [GuideCard(ValuationSource.Glasses, new DateOnly(2030, 4, 1), 12_500m)],
                    new ValuationCalculationSelection(Guid.Empty, false, null, [], 0m)
                    {
                        GuideSource = ValuationSource.Glasses,
                        Use = true,
                    },
                    // The box still holds an earlier figure, not the calculated 12,500.
                    EngineerValue("9000"))
            },
            CancellationToken.None));
        Assert.Equal(ValuationCalculationPolicy.UseFigureChanged, refusal.Message);

        var valuations = new EfValuationStore(harness.Factory);
        Assert.Empty(await valuations.ListAppliedAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None));
        Assert.Empty(await valuations.ListForCaseAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None));
    }

    /// <summary>
    /// The preview is what the Save uses (operator, 28 September 2026): from
    /// the retail as typed and the claimant's VAT position as the form holds
    /// it, the preview and the calculation the Save records are one figure,
    /// though the recorded card and the recorded VAT position say otherwise.
    /// </summary>
    [Fact]
    public async Task ThePreviewFromUnsavedFiguresIsTheCalculationTheSaveRecords()
    {
        await using var harness = await Harness.CreateAsync(database);
        var april = new DateOnly(2030, 4, 1);
        var initial = await harness.GetRequiredDataAsync();
        var engineer = Engineer(harness);
        var lease = await harness.AcquireLeaseAsync(initial.Version, engineer, "lease-preview-save-1");
        var first = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "preview-save-1", engineer) with
            {
                Valuation = new([GuideCard(ValuationSource.Glasses, april, 12_500m)])
            },
            CancellationToken.None);
        var valuations = new EfValuationStore(harness.Factory);
        var card = Assert.Single(await valuations.ListForCaseAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None));
        var selection = new ValuationCalculationSelection(card.ValuationId, true, null, [], 0m);

        // Recorded: 12,500 retail, claimant not VAT registered, so VAT is added.
        var recorded = await new PreviewValuationCalculation(valuations).ExecuteAsync(
            new(harness.CaseId, engineer, selection),
            CancellationToken.None);
        Assert.Equal(15_000m, recorded.Calculation.Proposal);

        // Typed: 13,000 retail and a VAT registered claimant, neither saved yet.
        var preview = await new PreviewValuationCalculation(valuations).ExecuteAsync(
            new(harness.CaseId, engineer, selection)
            {
                GuideRetailValue = 13_000m,
                ClaimantVatRegistered = true,
            },
            CancellationToken.None);
        Assert.Equal(13_000m, preview.Calculation.Proposal);

        var again = await harness.AcquireLeaseAsync(first.Version, engineer, "lease-preview-save-2");
        await harness.WorkspaceStore.SaveAsync(
            Request(harness, first.Version, again.Token, "preview-save-2", engineer) with
            {
                Settlement = new(new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [AssessmentVocabulary.SettlementClaimantVatRegistered] = "true",
                }),
                Valuation = new(
                    [GuideCard(ValuationSource.Glasses, april, 13_000m)],
                    selection,
                    EngineerValue(preview.Calculation.Proposal.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)))
            },
            CancellationToken.None);

        var applied = Assert.Single(await valuations.ListAppliedAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None));
        Assert.Equal(preview.Calculation.GuideRetailValue, applied.Calculation.GuideRetailValue);
        Assert.Equal(preview.Calculation.Proposal, applied.Calculation.Proposal);
        Assert.False(applied.Calculation.CommercialVatApplied);
        Assert.Equal(13_000m, applied.AcceptedEngineerValue);
    }

    [Fact]
    public async Task ReviewGatedTransitionsReadThePersistedFacts()
    {
        // The case is demoted through the workspace save, and the
        // Review-gated transitions then refuse.
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            harness.StaffActor,
            "lease-gate");
        var demoted = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "workspace-gate") with
            {
                Completeness = new(false, false)
            },
            CancellationToken.None);
        Assert.Equal(CaseLifecycleState.NotReady, demoted.Data.State);

        var transitionLease = await harness.AcquireLeaseAsync(
            demoted.Version,
            harness.StaffActor,
            "lease-return");
        // Return to Review asks its state rule (With Engineer or Report sent)
        // before the readiness gate, so the Case is put in a state that passes it.
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseWorkflows SET State = {nameof(CaseLifecycleState.ReportPreparation)} WHERE CaseId = {harness.CaseId}");
        }

        await Assert.ThrowsAsync<CaseReviewReadinessException>(() =>
            harness.WorkflowStore.ReturnToReviewAsync(
                new(
                    harness.CaseId,
                    demoted.Version,
                    harness.StaffActor,
                    "return-to-review-refused",
                    "Return to Review while the case is demoted",
                    transitionLease.Token),
                CancellationToken.None));
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseWorkflows SET State = {nameof(CaseLifecycleState.Review)} WHERE CaseId = {harness.CaseId}");
        }

        // The store refuses an unknown Engineer before the readiness gate, so the
        // hand-off names an enabled one.
        var engineerId = await SeedDefaultSignOffEngineerAsync(harness);
        // The refused transition never cleared the lease, so the same holder's
        // token is still the live one for the second attempt.
        await Assert.ThrowsAsync<CaseReviewReadinessException>(() =>
            harness.WorkflowStore.AssignEngineerAsync(
                new(
                    harness.CaseId,
                    demoted.Version,
                    harness.StaffActor,
                    "assign-engineer-refused",
                    "Assign Engineer while the case is demoted",
                    transitionLease.Token,
                    engineerId),
                CaseLifecycleState.ReportPreparation,
                CancellationToken.None));

        Assert.Equal(demoted.Version, (await harness.GetRequiredDataAsync()).Version);
    }

    /// <summary>
    /// Phase 5b: a changed claim source must be an active Claim source record
    /// when the save commits; a deactivated one or an organisation without the
    /// role leaves the Case unchanged. This Case's Claim source notes save with the Case.
    /// </summary>
    [Fact]
    public async Task AChangedClaimSourceMustBeAnActiveClaimSourceAndItsNotesSaveWithTheCase()
    {
        await using var harness = await Harness.CreateAsync();
        var inactiveId = Guid.NewGuid();
        var repairerId = Guid.NewGuid();
        var activeId = Guid.NewGuid();
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            context.Organizations.AddRange(
                new() { Id = inactiveId, Name = "Retired source", Version = 1, Active = false, Role = "claim_source" },
                new() { Id = repairerId, Name = "A repairer", Version = 1, Active = true, Role = "repairer" },
                new() { Id = activeId, Name = "Acme Claims", Version = 3, Active = true, Role = "claim_source" });
            await context.SaveChangesAsync();
        }

        // A refused save changes nothing, so the one lease stays live and the
        // version stays put for the next attempt.
        var before = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(before.Version, harness.StaffActor, "claim-source-lease");
        foreach (var (id, name) in new[] { (inactiveId, "Retired source"), (repairerId, "A repairer") })
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.WorkspaceStore.SaveAsync(
                Request(harness, before.Version, lease.Token, $"refused-save-{id:N}") with
                {
                    Overview = Overview("Jane Example") with { ClaimSource = new(id, 1, name, null, null, null) }
                }, default));
            Assert.Equal(before.Version, (await harness.GetRequiredDataAsync()).Version);
        }

        var saved = await harness.WorkspaceStore.SaveAsync(
            Request(harness, before.Version, lease.Token, "accepted-source-save") with
            {
                Overview = Overview("Jane Example") with
                {
                    ClaimSource = new(activeId, 3, "Acme Claims", "A Handler", null, null),
                    ClaimSourceNotes = "The claimant says the car was parked.\n\nPhotos to follow."
                }
            }, default);

        Assert.Equal(activeId, saved.Data.Workspace!.ClaimSource!.ClaimSourceId);
        Assert.Equal("The claimant says the car was parked.\n\nPhotos to follow.", saved.Data.Workspace.ClaimSourceNotes);
        var history = await new EfCaseQueryStore(harness.Factory, harness.TimeProvider)
            .ListHistoryByCursorAsync(harness.CaseId, null, null, 20, default);
        Assert.Contains(history, entry => entry.Reason?.Contains("Claim source: Acme Claims", StringComparison.Ordinal) == true
            && entry.Reason.Contains("Claim source notes", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AStaffDueBySurvivesALaterInspectionDeadlineChange()
    {
        await using var harness = await Harness.CreateAsync(database);
        var manualDueBy = new DateOnly(2031, 5, 18);
        var before = await harness.GetRequiredDataAsync();
        var firstLease = await harness.AcquireLeaseAsync(before.Version, harness.StaffActor, "staff-due-lease");
        var withManualDueBy = await harness.WorkspaceStore.SaveAsync(
            Request(harness, before.Version, firstLease.Token, "staff-due-save") with
            {
                Overview = Overview("Jane Example") with { DueBy = manualDueBy }
            }, default);

        var secondLease = await harness.AcquireLeaseAsync(
            withManualDueBy.Version,
            harness.StaffActor,
            "deadline-change-lease");
        await harness.WorkspaceStore.SaveAsync(
            Request(harness, withManualDueBy.Version, secondLease.Token, "deadline-change-save") with
            {
                Inspection = Inspection(CaseReportAddressTreatment.PhysicalVehicleLocation, "1 Depot Road") with
                {
                    InspectionDeadline = new DateOnly(2031, 5, 25)
                }
            }, default);

        await using var context = await harness.Factory.CreateDbContextAsync();
        var due = await context.CaseDueWork.SingleAsync(item => item.CaseId == harness.CaseId);
        Assert.Equal(manualDueBy, due.DueBy);
        Assert.True(due.DueBySetByStaff);
    }

    [Fact]
    public async Task ClearingAStaffDueByRestoresTheAcceptedInspectionDeadline()
    {
        await using var harness = await Harness.CreateAsync(database);
        var before = await harness.GetRequiredDataAsync();
        var firstLease = await harness.AcquireLeaseAsync(before.Version, harness.StaffActor, "set-due-lease");
        var withManualDueBy = await harness.WorkspaceStore.SaveAsync(
            Request(harness, before.Version, firstLease.Token, "set-due-save") with
            {
                Overview = Overview("Jane Example") with { DueBy = new DateOnly(2031, 5, 18) }
            }, default);

        var clearLease = await harness.AcquireLeaseAsync(
            withManualDueBy.Version,
            harness.StaffActor,
            "clear-due-lease");
        await harness.WorkspaceStore.SaveAsync(
            Request(harness, withManualDueBy.Version, clearLease.Token, "clear-due-save") with
            {
                Overview = Overview("Jane Example") with { DueBy = null }
            }, default);

        await using var context = await harness.Factory.CreateDbContextAsync();
        var due = await context.CaseDueWork.SingleAsync(item => item.CaseId == harness.CaseId);
        Assert.Equal(new DateOnly(2031, 5, 20), due.DueBy);
        Assert.False(due.DueBySetByStaff);
    }

    /// <summary>
    /// Blank means blank (R1077): emptying a box whose value was extracted
    /// records staff's clear, an empty value under their name and time. The
    /// Case shows no value, the extracted one stays as history, and the
    /// history line names the field once. Posting the empty box again names
    /// nothing and leaves who cleared it alone; retyping the value brings it
    /// back.
    /// </summary>
    [Fact]
    public async Task EmptyingAnExtractedClaimNumberRecordsTheClearUntilStaffRetypeIt()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        var extracted = Assert.IsType<CaseDataValue<string>>(initial.Claim.Number.Fact);
        Assert.Null(initial.Claim.Number.Confirmed);
        var clearedAtUtc = harness.TimeProvider.GetUtcNow();
        // The overview as the page shows it: Due by shows the accepted
        // inspection deadline (R1120), so only the claim number changes.
        var shown = Overview("Jane Example") with { DueBy = new DateOnly(2031, 5, 20) };

        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "clear-claim-number-lease");
        var cleared = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "clear-claim-number") with
            {
                Overview = shown with { ClaimNumber = null }
            },
            CancellationToken.None);

        Assert.True(cleared.Data.Claim.Number.Cleared);
        Assert.Null(cleared.Data.Claim.Number.Current);
        Assert.Null(cleared.Data.Claim.Number.Fact);
        Assert.Null(cleared.Assessment.CaseOwned.ClaimNumber);
        Assert.Null((await ReadEditableCaseDataAsync(harness)).ClaimNumber);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var rows = await context.CaseDataFields.AsNoTracking()
                .Where(item => item.WorkId == harness.CaseId && item.FieldName == CaseDataFieldNames.ClaimNumber)
                .ToDictionaryAsync(item => item.ValueKind, StringComparer.Ordinal);
            var staff = rows[CaseDataCodes.Confirmed];
            Assert.Equal(string.Empty, staff.Value);
            Assert.Equal(CaseDataCodes.Text, staff.ValueType);
            Assert.Equal(CaseDataCodes.StaffCorrection, staff.SourceKind);
            Assert.Equal(harness.StaffActor.SubjectId, staff.ConfirmedByActor);
            Assert.Equal(clearedAtUtc, staff.ConfirmedAtUtc!.Value);
            Assert.Equal(extracted.Value, rows[CaseDataCodes.Fact].Value);
            Assert.Equal(CaseDataCodes.IntakeEvidence, rows[CaseDataCodes.Fact].SourceKind);
            var line = await context.CaseHistory.AsNoTracking()
                .Where(item => item.CaseId == harness.CaseId && item.OperationKey == "clear-claim-number")
                .Select(item => item.Reason)
                .SingleAsync();
            Assert.StartsWith("Claim number · ", line, StringComparison.Ordinal);
        }

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(1));
        var againLease = await harness.AcquireLeaseAsync(cleared.Version, harness.StaffActor, "clear-claim-number-again-lease");
        var again = await harness.WorkspaceStore.SaveAsync(
            Request(harness, cleared.Version, againLease.Token, "clear-claim-number-again") with
            {
                Overview = shown with { ClaimNumber = null }
            },
            CancellationToken.None);

        Assert.True(again.Data.Claim.Number.Cleared);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var staff = await context.CaseDataFields.AsNoTracking()
                .SingleAsync(item => item.WorkId == harness.CaseId
                    && item.FieldName == CaseDataFieldNames.ClaimNumber
                    && item.ValueKind == CaseDataCodes.Confirmed);
            Assert.Equal(string.Empty, staff.Value);
            Assert.Equal(clearedAtUtc, staff.ConfirmedAtUtc!.Value);
            var line = await context.CaseHistory.AsNoTracking()
                .Where(item => item.CaseId == harness.CaseId && item.OperationKey == "clear-claim-number-again")
                .Select(item => item.Reason)
                .SingleAsync();
            Assert.StartsWith("No field changed", line, StringComparison.Ordinal);
        }

        var retypeLease = await harness.AcquireLeaseAsync(again.Version, harness.StaffActor, "retype-claim-number-lease");
        var retyped = await harness.WorkspaceStore.SaveAsync(
            Request(harness, again.Version, retypeLease.Token, "retype-claim-number") with
            {
                Overview = shown
            },
            CancellationToken.None);

        Assert.False(retyped.Data.Claim.Number.Cleared);
        Assert.Equal(extracted.Value, retyped.Data.Claim.Number.Current?.Value);
        Assert.Equal(extracted.Value, retyped.Data.Claim.Number.Fact?.Value);
        Assert.Equal(extracted.Value, (await ReadEditableCaseDataAsync(harness)).ClaimNumber);
    }

    /// <summary>
    /// A value only staff had entered has nothing behind it: emptying its box
    /// removes it, as before (R1077).
    /// </summary>
    [Fact]
    public async Task EmptyingAValueOnlyStaffEnteredRemovesIt()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        Assert.Null(initial.Claimant.ContactNumber.Current);
        var shown = Overview("Jane Example") with { DueBy = new DateOnly(2031, 5, 20) };
        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "staff-contact-lease");
        var entered = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "staff-contact") with
            {
                Overview = shown with { ClaimantContactNumber = "0113 496 0000" }
            },
            CancellationToken.None);
        Assert.Equal("0113 496 0000", entered.Data.Claimant.ContactNumber.Confirmed?.Value);

        var clearLease = await harness.AcquireLeaseAsync(entered.Version, harness.StaffActor, "staff-contact-clear-lease");
        var emptied = await harness.WorkspaceStore.SaveAsync(
            Request(harness, entered.Version, clearLease.Token, "staff-contact-clear") with
            {
                Overview = shown
            },
            CancellationToken.None);

        Assert.False(emptied.Data.Claimant.ContactNumber.Cleared);
        Assert.Null(emptied.Data.Claimant.ContactNumber.Current);
        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.False(await context.CaseDataFields.AnyAsync(item =>
            item.WorkId == harness.CaseId && item.FieldName == CaseDataFieldNames.ClaimantContactNumber));
    }

    /// <summary>
    /// Emptying an extracted inspection date leaves the report with no date
    /// and stales the report that printed one (R1077).
    /// </summary>
    [Fact]
    public async Task EmptyingAnExtractedInspectionDateEmptiesTheReportDateAndStalesTheReport()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        Assert.Equal(new DateOnly(2031, 5, 20), initial.Inspection.InspectionDate.Fact?.Value);
        var inspection = Inspection(CaseReportAddressTreatment.PhysicalVehicleLocation, "1 Depot Road");
        var firstLease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "inspection-clear-first-lease");
        var inspected = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, firstLease.Token, "inspection-clear-first") with
            {
                Inspection = inspection
            },
            CancellationToken.None);
        Assert.Equal(new DateOnly(2031, 5, 20), inspected.Assessment.CaseOwned.InspectionDate);
        var generationId = await SeedCurrentGenerationAsync(harness, inspected.Version);

        var clearLease = await harness.AcquireLeaseAsync(inspected.Version, harness.StaffActor, "inspection-clear-lease");
        var cleared = await harness.WorkspaceStore.SaveAsync(
            Request(harness, inspected.Version, clearLease.Token, "inspection-clear") with
            {
                Inspection = inspection with { InspectionDate = null }
            },
            CancellationToken.None);

        Assert.True(cleared.Data.Inspection.InspectionDate.Cleared);
        Assert.Null(cleared.Assessment.CaseOwned.InspectionDate);
        Assert.Null((await ReadEditableCaseDataAsync(harness)).InspectionDate);
        await AssertGenerationStateAsync(
            harness,
            generationId,
            CaseReportGenerationState.Stale,
            expectedStaleEvents: 1,
            expectedReason: CaseReportStaleReasons.AssessmentFactsChanged);
    }

    /// <summary>
    /// Emptying an extracted mileage clears its unit with it (R1080). The
    /// Not ready queue's gate reads no mileage recorded, the vehicle evidence
    /// still loads with nothing confirmed, and a Case already in Review is
    /// never sent back (R631, R759, R1081).
    /// </summary>
    [Fact]
    public async Task EmptyingAnExtractedMileageClearsItsUnitAndTheGateButKeepsAReviewCaseInReview()
    {
        await using var harness = await Harness.CreateAsync(database);
        var initial = await harness.GetRequiredDataAsync();
        Assert.Equal(CaseLifecycleState.Review, initial.State);
        Assert.Equal(Harness.FixtureMileage, initial.Vehicle.Mileage.Fact?.Value);
        Assert.NotNull(initial.Vehicle.MileageUnit.Fact);
        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "clear-mileage-lease");

        var saved = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "clear-mileage") with
            {
                Vehicle = new("AB12 CDE", null, null, null, null)
            },
            CancellationToken.None);

        Assert.Equal(CaseLifecycleState.Review, saved.Data.State);
        Assert.True(saved.Data.Vehicle.Mileage.Cleared);
        Assert.True(saved.Data.Vehicle.MileageUnit.Cleared);
        Assert.Null(saved.Data.Vehicle.Mileage.Current);
        Assert.Null(saved.Assessment.CaseOwned.Mileage);
        var editable = await ReadEditableCaseDataAsync(harness);
        Assert.Null(editable.VehicleMileage);
        Assert.Null(editable.VehicleMileageUnit);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var staff = await context.CaseDataFields.AsNoTracking()
                .Where(item => item.WorkId == harness.CaseId
                    && item.ValueKind == CaseDataCodes.Confirmed
                    && (item.FieldName == CaseDataFieldNames.VehicleMileage
                        || item.FieldName == CaseDataFieldNames.VehicleMileageUnit))
                .ToDictionaryAsync(item => item.FieldName, StringComparer.Ordinal);
            Assert.Equal(string.Empty, staff[CaseDataFieldNames.VehicleMileage].Value);
            Assert.Equal(CaseDataCodes.Integer, staff[CaseDataFieldNames.VehicleMileage].ValueType);
            Assert.Equal(string.Empty, staff[CaseDataFieldNames.VehicleMileageUnit].Value);
            Assert.Equal(2, await context.CaseDataFields.AsNoTracking().CountAsync(item =>
                item.WorkId == harness.CaseId
                && item.ValueKind == CaseDataCodes.Fact
                && (item.FieldName == CaseDataFieldNames.VehicleMileage
                    || item.FieldName == CaseDataFieldNames.VehicleMileageUnit)));
        }

        var blockers = await ((INotReadyCaseQueries)new EfCaseQueryStore(harness.Factory, harness.TimeProvider))
            .GetBlockersAsync([harness.CaseId], CancellationToken.None);
        Assert.Equal(new CaseVehicleGate(true, false), blockers[harness.CaseId].VehicleGate);
        var evidence = await new EfVehicleWorkflowStore(harness.Factory, harness.TimeProvider)
            .GetAsync(harness.CaseId, CancellationToken.None);
        Assert.NotNull(evidence);
        Assert.Null(evidence.Confirmed);
    }

    /// <summary>
    /// The shared readers of a field (R1077): a cleared box has no accepted
    /// and no current value, whatever fact or suggestion stands behind it.
    /// </summary>
    [Fact]
    public void AClearedBoxNeverFallsBackToTheFactOrSuggestionBehindIt()
    {
        CaseDataFieldEntity Row(string kind, string value) => new()
        {
            WorkId = Guid.Empty,
            FieldName = CaseDataFieldNames.VehicleMake,
            ValueKind = kind,
            ValueType = CaseDataCodes.Text,
            Value = value,
            SourceKind = kind == CaseDataCodes.Confirmed ? CaseDataCodes.StaffCorrection : CaseDataCodes.VehicleLookup,
            SourceIdentity = "test",
            SourceLabel = "test",
            PolicyKey = "test",
            PolicyVersion = 1,
            ConfirmedByActor = kind == CaseDataCodes.Confirmed ? "staff" : null,
            ConfirmedAtUtc = kind == CaseDataCodes.Confirmed ? DateTimeOffset.UnixEpoch : null
        };

        List<CaseDataFieldEntity> fields =
        [
            Row(CaseDataCodes.Fact, "FORD"),
            Row(CaseDataCodes.Suggestion, "FIAT"),
            Row(CaseDataCodes.Confirmed, CaseDataFieldValues.ClearedValue)
        ];

        Assert.True(CaseDataFieldValues.IsClearedField(fields, CaseDataFieldNames.VehicleMake));
        Assert.Null(CaseDataFieldValues.AcceptedField(fields, CaseDataFieldNames.VehicleMake));
        Assert.Null(CaseDataFieldValues.CurrentField(fields, CaseDataFieldNames.VehicleMake));
        Assert.Null(CaseDataFieldValues.Accepted(fields, CaseDataFieldNames.VehicleMake));
        Assert.Null(CaseDataFieldValues.Current(fields, CaseDataFieldNames.VehicleMake));

        // Retyped, the value stands again; with no clear the fact stands.
        fields[2].Value = "MAZDA";
        Assert.Equal("MAZDA", CaseDataFieldValues.Current(fields, CaseDataFieldNames.VehicleMake));
        fields.RemoveAt(2);
        Assert.False(CaseDataFieldValues.IsClearedField(fields, CaseDataFieldNames.VehicleMake));
        Assert.Equal("FORD", CaseDataFieldValues.Accepted(fields, CaseDataFieldNames.VehicleMake));
    }

    [Fact]
    public async Task SavingDueByWithoutDueWorkCreatesAStoppedProjectionWithoutAChase()
    {
        await using var harness = await Harness.CreateAsync(database);
        await using (var initialContext = await harness.Factory.CreateDbContextAsync())
        {
            Assert.False(await initialContext.CaseDueWork.AnyAsync(item => item.CaseId == harness.CaseId));
        }

        var before = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(before.Version, harness.StaffActor, "due-without-work-lease");
        var manualDueBy = new DateOnly(2031, 5, 18);
        await harness.WorkspaceStore.SaveAsync(
            Request(harness, before.Version, lease.Token, "due-without-work-save") with
            {
                Overview = Overview("Jane Example") with { DueBy = manualDueBy }
            }, default);

        await using var context = await harness.Factory.CreateDbContextAsync();
        var due = await context.CaseDueWork.SingleAsync(item => item.CaseId == harness.CaseId);
        Assert.Equal(manualDueBy, due.DueBy);
        Assert.True(due.DueBySetByStaff);
        Assert.Equal(nameof(CaseDueWorkState.Stopped), due.State);
        Assert.Null(due.NextChaseAtUtc);
    }

    [Fact]
    public async Task ClaimSourceOverridesStayForTheSameSourceAndClearForAnotherSourceOrNone()
    {
        await using var harness = await Harness.CreateAsync();
        var sourceAId = Guid.NewGuid();
        var sourceBId = Guid.NewGuid();
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            context.Organizations.AddRange(
                new() { Id = sourceAId, Name = "Source A", Version = 1, Active = true, Role = "claim_source" },
                new() { Id = sourceBId, Name = "Source B", Version = 2, Active = true, Role = "claim_source" });
            await context.SaveChangesAsync();
        }

        var before = await harness.GetRequiredDataAsync();
        var firstLease = await harness.AcquireLeaseAsync(before.Version, harness.StaffActor, "source-a-lease");
        var sourceA = await harness.WorkspaceStore.SaveAsync(
            Request(harness, before.Version, firstLease.Token, "source-a-save") with
            {
                Overview = Overview("Jane Example") with
                {
                    ClaimSource = new(
                        sourceAId,
                        1,
                        "Source A",
                        "Directory A",
                        "0113 000 0001",
                        "a@example.test")
                }
            }, default);

        var sourceAOverrideLease = await harness.AcquireLeaseAsync(
            sourceA.Version,
            harness.StaffActor,
            "source-a-again-lease");
        var sourceAOverride = await harness.WorkspaceStore.SaveAsync(
            Request(harness, sourceA.Version, sourceAOverrideLease.Token, "source-a-again-save") with
            {
                Overview = Overview("Jane Example") with
                {
                    ClaimSource = new(
                        sourceAId,
                        1,
                        "Source A",
                        "Directory A",
                        "0113 000 0001",
                        "a@example.test",
                        "Case A",
                        "0113 999 0001",
                        "case-a@example.test")
                }
            }, default);

        var sameSourceLease = await harness.AcquireLeaseAsync(
            sourceAOverride.Version,
            harness.StaffActor,
            "source-a-unchanged-lease");
        var sameSource = await harness.WorkspaceStore.SaveAsync(
            Request(harness, sourceAOverride.Version, sameSourceLease.Token, "source-a-unchanged-save") with
            {
                Overview = Overview("Jane Example") with
                {
                    ClaimSource = new(
                        sourceAId,
                        1,
                        "Source A",
                        "Directory A",
                        "0113 000 0001",
                        "a@example.test")
                }
            }, default);
        Assert.Equal("Case A", sameSource.Data.Workspace!.ClaimSource!.OverrideContactName);
        Assert.Equal("0113 999 0001", sameSource.Data.Workspace.ClaimSource.OverrideContactTelephone);
        Assert.Equal("case-a@example.test", sameSource.Data.Workspace.ClaimSource.OverrideContactEmailAddress);

        var sourceBLease = await harness.AcquireLeaseAsync(
            sameSource.Version,
            harness.StaffActor,
            "source-b-lease");
        var sourceB = await harness.WorkspaceStore.SaveAsync(
            Request(harness, sameSource.Version, sourceBLease.Token, "source-b-save") with
            {
                Overview = Overview("Jane Example") with
                {
                    ClaimSource = new(
                        sourceBId,
                        2,
                        "Source B",
                        "Directory B",
                        "0113 000 0002",
                        "b@example.test",
                        "Old Case A",
                        "0113 999 0001",
                        "old-case-a@example.test")
                }
            }, default);
        Assert.Null(sourceB.Data.Workspace!.ClaimSource!.OverrideContactName);
        Assert.Null(sourceB.Data.Workspace.ClaimSource.OverrideContactTelephone);
        Assert.Null(sourceB.Data.Workspace.ClaimSource.OverrideContactEmailAddress);

        var sourceBOverrideLease = await harness.AcquireLeaseAsync(
            sourceB.Version,
            harness.StaffActor,
            "source-b-override-lease");
        var sourceBOverride = await harness.WorkspaceStore.SaveAsync(
            Request(harness, sourceB.Version, sourceBOverrideLease.Token, "source-b-override-save") with
            {
                Overview = Overview("Jane Example") with
                {
                    ClaimSource = sourceB.Data.Workspace.ClaimSource with
                    {
                        OverrideContactName = "Case B"
                    }
                }
            }, default);
        Assert.Equal("Case B", sourceBOverride.Data.Workspace!.ClaimSource!.OverrideContactName);

        var noneLease = await harness.AcquireLeaseAsync(
            sourceBOverride.Version,
            harness.StaffActor,
            "source-none-lease");
        var none = await harness.WorkspaceStore.SaveAsync(
            Request(harness, sourceBOverride.Version, noneLease.Token, "source-none-save") with
            {
                Overview = Overview("Jane Example") with { ClaimSource = null }
            }, default);
        Assert.Null(none.Data.Workspace!.ClaimSource);
        await using var finalContext = await harness.Factory.CreateDbContextAsync();
        var snapshot = await finalContext.CaseDataSnapshots.SingleAsync(item => item.WorkId == harness.CaseId);
        Assert.Null(snapshot.ClaimSourceOverrideContactName);
        Assert.Null(snapshot.ClaimSourceOverrideContactTelephone);
        Assert.Null(snapshot.ClaimSourceOverrideContactEmailAddress);
    }

    private static ActionActor Engineer(Harness harness, StaffRole role = StaffRole.Engineer) => ActionActor.Staff(
        Guid.Parse(harness.StaffActor.SubjectId),
        [role]);

    /// <summary>
    /// Discard deletes a spec (operator, 11 October 2026), so a discarded one
    /// is an id the Case no longer holds.
    /// </summary>
    private static Guid DiscardedEstimateId() => Guid.NewGuid();

    private static async Task<Guid> SeedCurrentGenerationAsync(Harness harness, long caseVersion, Guid? workId = null)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        var generationId = Guid.NewGuid();
        context.Set<CaseReportGenerationEntity>().Add(new()
        {
            Id = generationId,
            CaseId = harness.CaseId,
            WorkId = workId ?? harness.CaseId,
            CaseVersion = caseVersion,
            SnapshotHash = new string('6', 64),
            SnapshotJson = ReportGenerationSnapshotFixture.Json(harness.CaseId, "workspace-mileage-source-generation"),
            TemplateVersion = "assessment-report/v1",
            RendererVersion = "renderer/v1",
            State = nameof(CaseReportGenerationState.Confirmed),
            GeneratedAtUtc = harness.TimeProvider.GetUtcNow(),
            Version = 1,
        });
        await context.SaveChangesAsync();
        return generationId;
    }

    private static async Task<CaseEditableData> ReadEditableCaseDataAsync(Harness harness)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        var snapshot = await EfCaseDataStore.SnapshotQuery(context, tracking: false)
            .SingleAsync(item => item.WorkId == harness.CaseId);
        return CaseDataFieldWriter.ReadEditable(snapshot);
    }

    private static async Task AssertGenerationStateAsync(
        Harness harness,
        Guid generationId,
        CaseReportGenerationState expectedState,
        int expectedStaleEvents,
        string? expectedReason = null)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.Equal(
            expectedState.ToString(),
            (await context.Set<CaseReportGenerationEntity>()
                .SingleAsync(item => item.Id == generationId)).State);
        var staleEvents = await context.ActionHistory
            .Where(item => item.AggregateId == harness.CaseId.ToString("D")
                && item.EventKind == EfCaseReportGenerationStore.StaleEventKind)
            .ToArrayAsync();
        Assert.Equal(expectedStaleEvents, staleEvents.Length);
        if (expectedReason is not null)
        {
            Assert.Equal(expectedReason, Assert.Single(staleEvents).Reason);
        }
    }

    private static async Task<Guid> SeedDefaultSignOffEngineerAsync(Harness harness)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        var engineerRole = await context.Roles.SingleAsync(
            role => role.NormalizedName == "ENGINEER");
        var staffId = Guid.NewGuid();
        var signature = new byte[] { 1, 2, 3, 4 };
        context.Users.Add(new PegasusIdentityUser
        {
            Id = staffId,
            UserName = $"workspace-signatory-{staffId:N}",
            NormalizedUserName = $"WORKSPACE-SIGNATORY-{staffId:N}",
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            IsEnabled = true,
            IsSignOffEngineer = true,
            IsDefaultSignOffEngineer = true,
            SignOffPrintedName = "Workspace Signatory",
            SignOffQualifications = "ATA VDA",
            SignOffSignature = signature,
            SignOffSignatureDigest = Convert.ToHexStringLower(SHA256.HashData(signature)),
        });
        context.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = staffId,
            RoleId = engineerRole.Id,
        });
        await context.SaveChangesAsync();
        return staffId;
    }

    [Fact]
    public async Task ThePageFrameOverlapsItsReadsAndStillAnswersAMissingCaseWithNull()
    {
        await using var harness = await Harness.CreateAsync(database);
        var store = new EfCaseQueryStore(harness.Factory, harness.TimeProvider);

        var frame = await store.GetPageFrameAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None);
        var missing = await store.GetPageFrameAsync(Guid.NewGuid(), CaseWorkSelector.Current, CancellationToken.None);

        var present = Assert.IsType<CasePageFrameData>(frame);
        Assert.Equal(harness.CaseId, present.Frame.Summary.CaseId);
        Assert.Equal(harness.CaseId, present.Frame.Workflow.CaseId);
        Assert.Null(missing);
    }

    private static async Task MarkTerminalAsync(Harness harness)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE CaseWorkflows SET State = {nameof(CaseLifecycleState.PrincipalCancelled)} WHERE CaseId = {harness.CaseId}");
    }

    private static async Task<CaseWorkflowEntity> WorkflowRowAsync(Harness harness)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        return await context.CaseWorkflows.AsNoTracking().SingleAsync(item => item.CaseId == harness.CaseId);
    }

    private static async Task<int> LeaseOperationCountAsync(Harness harness)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        return await context.CaseEditLeaseOperations.AsNoTracking().CountAsync(item => item.CaseId == harness.CaseId);
    }

    private static async Task<long> WorkflowEventCountAsync(Harness harness, string eventType)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        return await context.CaseWorkflowEvents.AsNoTracking()
            .LongCountAsync(item => item.CaseId == harness.CaseId && item.EventType == eventType);
    }

    // The rows a save wrote: the agreed fee the Case took from its Principal
    // and the Good pre-incident condition it started with (R598) are not.
    private static async Task<long> AssessmentFieldCountAsync(Harness harness)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        return await context.CaseAssessmentFields.AsNoTracking()
            .LongCountAsync(item => item.WorkId == harness.CaseId
                && item.RecordedBy != PrincipalDefaultFeePolicy.RecorderId
                && item.RecordedBy != VehicleConditionDefault.RecorderId);
    }

    private static async Task<Dictionary<string, CaseAssessmentFieldEntity>> AssessmentFieldsAsync(Harness harness)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        return await context.CaseAssessmentFields.AsNoTracking()
            .Where(item => item.WorkId == harness.CaseId)
            .ToDictionaryAsync(item => item.FieldPath, StringComparer.Ordinal);
    }

    private sealed class RejectingQueryReadInterceptor(params string[] forbiddenCommandTexts) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.CommandSource == CommandSource.LinqQuery
                && forbiddenCommandTexts.Any(forbiddenCommandText =>
                    command.CommandText.Contains(forbiddenCommandText, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    "Focused reader queried an excluded body table.");
            }

            return ValueTask.FromResult(result);
        }
    }

    private static CaseWorkspaceOverview Overview(string claimantName) => new(
        claimantName,
        null,
        null,
        "QDOS-123",
        null,
        null,
        null,
        null,
        null);

    // The inspection section posts every box as the page shows it: the
    // harness's extracted inspection date included, since an emptied box
    // records a clear (R1077).
    private static CaseWorkspaceInspection Inspection(
        CaseReportAddressTreatment treatment,
        string? address) => new(
        treatment,
        address,
        null,
        null,
        new DateOnly(2031, 5, 20),
        new DateOnly(2031, 5, 20),
        null,
        null,
        null,
        null,
        null);

    private static SaveCaseWorkspaceRequest Request(
        Harness harness,
        long expectedVersion,
        string leaseToken,
        string operationKey,
        ActionActor? actor = null) => new(
        harness.CaseId,
        expectedVersion,
        actor ?? harness.StaffActor,
        operationKey,
        "Recorded the Case workspace",
        leaseToken);
}

/// <summary>
/// The one database a class of Case workspace tests shares: restored and
/// migrated once. A test accepts an intake into it, which makes a Case of its
/// own, and removes that Case and the intake when it ends, so the next test
/// finds the database as the one before it did. A test that reads or writes
/// across the whole database (the retained Sent evidence of every Case, the
/// default sign-off Engineer, the claim sources) accepts into a database of its
/// own instead.
/// </summary>
public sealed class CaseWorkspaceDatabase : IAsyncLifetime
{
    private LocalDbTestDatabase? database;
    private ForeignKeyRowRemoval? caseRemoval;
    private ForeignKeyRowRemoval? receiptRemoval;

    internal LocalDbTestDatabase Database =>
        database ?? throw new InvalidOperationException("The database has not been created.");

    internal PooledDbContextFactory<PegasusDbContext> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        database = await LocalDbTestDatabase.CreateAsync();
        Factory = new PooledDbContextFactory<PegasusDbContext>(
            new DbContextOptionsBuilder<PegasusDbContext>().UseSqlServer(database.ConnectionString).Options);
    }

    public async Task DisposeAsync()
    {
        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }

    /// <summary>
    /// Removes the intake receipt, the Case it made and everything that hangs
    /// from either. The QDOS Principal and its sequence counter are left as
    /// they are: the next Case takes the next number.
    /// </summary>
    internal async Task RemoveAsync(Guid receiptId)
    {
        caseRemoval ??= await ForeignKeyRowRemoval.CreateAsync(
            Database, ForeignKeyRowRemoval.CasesTable, ForeignKeyRowRemoval.InRoots, []);
        receiptRemoval ??= await ForeignKeyRowRemoval.CreateAsync(
            Database, ForeignKeyRowRemoval.IntakeReceiptsTable, ForeignKeyRowRemoval.CasesTable);

        // One batch: the Cases the receipt made go to @roots, then the walk from
        // each, the walk from the receipt, and the history (it names a Case or
        // a receipt without a key to it).
        var sql = ForeignKeyRowRemoval.RootsDeclaration
            + "INSERT @roots (Id) SELECT Id FROM [dbo].[Cases] WHERE OriginIntakeReceiptId = @id;\n"
            + caseRemoval.Sql + ";\n"
            + receiptRemoval.Sql + ";\n"
            + "DELETE FROM [dbo].[ActionHistory] WHERE AggregateId IN (SELECT LOWER(CONVERT(nvarchar(36), Id)) FROM @roots)"
            + " OR AggregateId = LOWER(CONVERT(nvarchar(36), @id))";
        await ForeignKeyRowRemoval.ExecuteAsync(Database, sql, ("@id", receiptId));
    }
}
