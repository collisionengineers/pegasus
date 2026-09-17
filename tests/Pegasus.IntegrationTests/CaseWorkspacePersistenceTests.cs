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
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Harness = Pegasus.IntegrationTests.CaseDataCompletenessPersistenceTests.CaseDataHarness;

namespace Pegasus.IntegrationTests;

/// <summary>
/// One Case edit is one transaction. These tests prove that it either records
/// the whole authorized snapshot or none of it: a stale version, a lease that
/// is missing, foreign or expired, an accepted estimate, a terminal case and a
/// replayed operation key with a different payload each leave the record
/// exactly as it was.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseWorkspacePersistenceTests
{
    [Fact]
    public async Task FocusedPageAndFilesReadsKeepHistoryAndTypedCaseDataOutOfTheirBodies()
    {
        await using var harness = await Harness.CreateAsync();
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
                    OperationKey = "focused-page-file",
                    PreparationRole = nameof(CaseAssetReportRole.NotUsed)
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
            .GetPageFrameAsync(harness.CaseId, CancellationToken.None);
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
        await using var harness = await Harness.CreateAsync();
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

        harness.TimeProvider.Advance(TimeSpan.FromMinutes(6));

        Assert.False(await validator.ExecuteAsync(
            new(harness.CaseId, harness.StaffActor, lease.Token),
            CancellationToken.None));
    }

    [Fact]
    public async Task ClaimSourceGuidanceIsAnImmutableTimelineSnapshotAppliedOncePerTemplate()
    {
        await using var harness = await Harness.CreateAsync();
        var sourceId = Guid.NewGuid();
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            context.Organizations.Add(new()
            {
                Id = sourceId, Name = "Guidance source", Version = 1, Active = true,
                GuidanceTemplate = "Contact the repairer before finalising.", GuidanceTemplateVersion = 1,
                ContactRoles = [new() { Role = "claim_source" }]
            });
            await context.SaveChangesAsync();
        }
        var overview = Overview("Jane Example") with { ClaimSource = new(sourceId, 1, "Guidance source", null, null, null) };
        for (var index = 0; index < 3; index++)
        {
            var current = await harness.GetRequiredDataAsync();
            var lease = await harness.AcquireLeaseAsync(current.Version, harness.StaffActor, $"guidance-edit-{index}");
            await harness.WorkspaceStore.SaveAsync(Request(harness, current.Version, lease.Token, $"guidance-save-{index}") with
            {
                Overview = index == 1 ? overview with { ClaimSource = null } : overview
            }, default);
        }
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var source = await context.Organizations.SingleAsync(item => item.Id == sourceId);
            source.GuidanceTemplate = "Replacement template must not rewrite history.";
            source.GuidanceTemplateVersion = 2;
            await context.SaveChangesAsync();
        }
        var history = await new EfCaseQueryStore(harness.Factory, harness.TimeProvider)
            .ListHistoryByCursorAsync(harness.CaseId, null, null, 20, default);
        var guidance = Assert.Single(history.SelectMany(entry => entry.Guidance));
        Assert.Equal("Contact the repairer before finalising.", guidance.Text);
        Assert.Equal("claim_source_guidance_applied", guidance.EventType);
        Assert.Equal(1, guidance.TemplateVersion);
        Assert.Equal(history.Count, history.Select(entry => entry.EntryId).Distinct().Count());
    }

    [Fact]
    public async Task ImageCropAndDamageCommitTogetherAndAStaleImageRollsBackBoth()
    {
        await using var harness = await Harness.CreateAsync();
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
                new DocumentOccurrenceEntity { Id = occurrenceId, CaseId = harness.CaseId, DocumentId = documentId, VersionId = versionId, SemanticRole = DocumentSemanticRole.Image, Source = DocumentSource.StaffUpload, SourceOccurrenceIdentity = "workspace-crop-fixture", RecordedAtUtc = harness.TimeProvider.GetUtcNow(), OperationKey = "seed-workspace-crop", PreparationRole = nameof(CaseAssetReportRole.NotUsed) });
            await context.SaveChangesAsync();
        }
        var lease = await harness.AcquireLeaseAsync(initial.Version, harness.StaffActor, "edit-workspace-crop");
        var crop = new CaseAssetCrop(.1m, .2m, .5m, .6m);
        var request = Request(harness, initial.Version, lease.Token, "save-workspace-crop") with
        {
            Damage = new([new("left_front_wing", "light", "Scuffed")], null),
            ImagePreparation = new([new(occurrenceId, 99, CaseAssetReportRole.Overview, null, CaseAssetRotation.Clockwise90, crop)])
        };
        await Assert.ThrowsAsync<CaseAssetPreparationVersionConflictException>(() => harness.WorkspaceStore.SaveAsync(request, default));
        Assert.Equal(initial.Version, (await harness.GetRequiredDataAsync()).Version);
        Assert.Equal(0, await AssessmentFieldCountAsync(harness));
        var saved = await harness.WorkspaceStore.SaveAsync(request with
        {
            ImagePreparation = new([new(occurrenceId, 0, CaseAssetReportRole.Overview, null, CaseAssetRotation.Clockwise90, crop)])
        }, default);
        Assert.Equal(initial.Version + 1, saved.Version);
        Assert.Equal("left_front", saved.Assessment.Field(AssessmentVocabulary.ImpactLocation)?.Value);
        var preparation = Assert.Single(await new EfCaseAssetPreparationStore(harness.Factory, harness.TimeProvider).ListForCaseAsync(harness.CaseId, default));
        Assert.Equal(crop, preparation.Crop);
        Assert.Equal(CaseAssetRotation.Clockwise90, preparation.Rotation);
        await using var check = await harness.Factory.CreateDbContextAsync();
        Assert.Equal(hash, (await check.Set<DocumentVersionEntity>().SingleAsync(item => item.Id == versionId)).Sha256);
        Assert.Equal(1, await WorkflowEventCountAsync(harness, "case_workspace_saved"));
    }

    [Fact]
    public async Task OneWorkspaceSaveWritesOneWorkflowEventAndBumpsTheVersionExactlyOnce()
    {
        await using var harness = await Harness.CreateAsync();
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
                    new(72_850, CaseOdometerUnit.Miles, "repairer", CaseOdometerUnit.Kilometres),
                    new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        [AssessmentVocabulary.VehicleCondition] = "good",
                        [AssessmentVocabulary.HistoryCheck] = "History clear"
                    }),
                Damage = new([new("left_front_wing", "light", "Scuffed")], new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [AssessmentVocabulary.DamageTyreRightFront] = "damaged",
                    [AssessmentVocabulary.DamageBeltLeftRear] = "deployed",
                    [AssessmentVocabulary.DamageUnrelated] = "Old rear bumper scrape",
                    [AssessmentVocabulary.DamageUnrelatedDeduction] = "125.50",
                    [AssessmentVocabulary.DamageMaterialTransfer] = "White paint transfer"
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
        // The headline impact location is derived from the impacts, never
        // written directly, and a detailed region rolls up to its parent.
        Assert.Equal(
            "left_front",
            result.Assessment.Fields.Single(field => field.Path == AssessmentVocabulary.ImpactLocation).Value);
        Assert.Equal("damaged", result.Assessment.Field(AssessmentVocabulary.DamageTyreRightFront)?.Value);
        Assert.Equal("deployed", result.Assessment.Field(AssessmentVocabulary.DamageBeltLeftRear)?.Value);
        Assert.Equal("Old rear bumper scrape", result.Assessment.Field(AssessmentVocabulary.DamageUnrelated)?.Value);
        Assert.Equal("125.50", result.Assessment.Field(AssessmentVocabulary.DamageUnrelatedDeduction)?.Value);
        Assert.Equal("White paint transfer", result.Assessment.Field(AssessmentVocabulary.DamageMaterialTransfer)?.Value);
    }

    [Fact]
    public async Task SavingUnchangedExtractedOverviewFactsAndANotePreservesReportFreshness()
    {
        await using var harness = await Harness.CreateAsync();
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
                    ClientNotes = "Unchanged extracted facts reviewed; note added.",
                },
            },
            CancellationToken.None);

        Assert.Equal(initial.Claimant.Name.Fact, saved.Data.Claimant.Name.Fact);
        Assert.Null(saved.Data.Claimant.Name.Confirmed);
        Assert.Equal(
            "Unchanged extracted facts reviewed; note added.",
            saved.Data.Workspace!.ClientNotes);
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

    [Fact]
    public async Task ChangingOnlyTheEffectiveMileageSourceStalesTheCurrentGeneration()
    {
        await using var harness = await Harness.CreateAsync();
        var initial = await harness.GetRequiredDataAsync();
        var firstLease = await harness.AcquireLeaseAsync(
            initial.Version,
            harness.StaffActor,
            "mileage-source-first-lease");
        var withMileage = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, firstLease.Token, "mileage-source-first-save") with
            {
                Vehicle = new(
                    null,
                    null,
                    null,
                    new(72_850, CaseOdometerUnit.Miles, CaseVehicleMileageSourcePolicy.Owner, null),
                    new Dictionary<string, string?>(StringComparer.Ordinal)),
            },
            CancellationToken.None);
        var generationId = await SeedCurrentGenerationAsync(harness, withMileage.Version);

        var sourceLease = await harness.AcquireLeaseAsync(
            withMileage.Version,
            harness.StaffActor,
            "mileage-source-change-lease");
        await harness.WorkspaceStore.SaveAsync(
            Request(harness, withMileage.Version, sourceLease.Token, "mileage-source-change-save") with
            {
                Vehicle = new(
                    null,
                    null,
                    null,
                    new(72_850, CaseOdometerUnit.Miles, CaseVehicleMileageSourcePolicy.Repairer, null),
                    new Dictionary<string, string?>(StringComparer.Ordinal)),
            },
            CancellationToken.None);

        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.Equal(
            nameof(CaseReportGenerationState.Stale),
            (await context.Set<CaseReportGenerationEntity>()
                .SingleAsync(item => item.Id == generationId)).State);
        Assert.Equal(
            CaseReportStaleReasons.AssessmentFactsChanged,
            (await context.Set<ActionHistoryEntity>()
                .SingleAsync(item => item.AggregateId == harness.CaseId.ToString("D")
                    && item.EventKind == "case_report_generation_stale"))
                .Reason);
    }

    [Fact]
    public async Task DirectAssessmentSaveStalesOnlyForAPrintedFactChange()
    {
        await using var harness = await Harness.CreateAsync();
        var initial = await harness.GetRequiredDataAsync();
        var generationId = await SeedCurrentGenerationAsync(harness, initial.Version);
        var actor = Engineer(harness);
        var assessmentStore = new EfCaseAssessmentStore(
            harness.Factory,
            harness.TimeProvider,
            new EfRepairSpecificationStore(harness.Factory, harness.TimeProvider));
        var noteLease = await harness.AcquireLeaseAsync(
            initial.Version,
            actor,
            "direct-assessment-note-lease");

        var noted = await assessmentStore.SaveAsync(
            new(
                harness.CaseId,
                initial.Version,
                actor,
                "direct-assessment-note-save",
                "Recorded an unprinted Engineer note.",
                noteLease.Token,
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [AssessmentVocabulary.VehicleEngineerNotes] = "Check the trim on return.",
                }),
            CancellationToken.None);

        await AssertGenerationStateAsync(
            harness,
            generationId,
            CaseReportGenerationState.Confirmed,
            expectedStaleEvents: 0);

        var colourLease = await harness.AcquireLeaseAsync(
            noted.CaseVersion,
            actor,
            "direct-assessment-colour-lease");
        await assessmentStore.SaveAsync(
            new(
                harness.CaseId,
                noted.CaseVersion,
                actor,
                "direct-assessment-colour-save",
                "Corrected the printed vehicle colour.",
                colourLease.Token,
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [AssessmentVocabulary.VehicleColour] = "Blue",
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
    public async Task DirectCaseDataSaveStalesOnlyForAPrintedFactChange()
    {
        await using var harness = await Harness.CreateAsync();
        var initial = await harness.GetRequiredDataAsync();
        var generationId = await SeedCurrentGenerationAsync(harness, initial.Version);
        var current = await ReadEditableCaseDataAsync(harness);
        var noteLease = await harness.AcquireLeaseAsync(
            initial.Version,
            harness.StaffActor,
            "direct-case-data-note-lease");

        var noted = await harness.DataStore.SaveAsync(
            new(
                harness.CaseId,
                initial.Version,
                harness.StaffActor,
                "direct-case-data-note-save",
                "Recorded an unprinted client note.",
                noteLease.Token,
                current with { ClientNotes = "Photos will follow." }),
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
        await harness.DataStore.SaveAsync(
            new(
                harness.CaseId,
                noted.Version,
                harness.StaffActor,
                "direct-case-data-claimant-save",
                "Corrected the printed claimant name.",
                claimantLease.Token,
                current with
                {
                    ClaimantName = "Janet Example",
                    ClientNotes = "Photos will follow.",
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
    public async Task SelectingTheEffectiveDefaultSignatoryThroughWorkspaceDoesNotStaleTheReport()
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
        Assert.Equal(
            signOffEngineerId,
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
        await using var harness = await Harness.CreateAsync();
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
        // The legacy SaveCase forces Instruction complete to false whenever any
        // case fact changes. The Case workspace save evaluates readiness from
        // the row it just wrote instead, so editing an unrelated fact cannot
        // silently take a Review case back to Not ready.
        await using var harness = await Harness.CreateAsync();
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
        await using var harness = await Harness.CreateAsync();
        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            harness.StaffActor,
            "lease-confirmed-make");

        var saved = await harness.WorkspaceStore.SaveAsync(
            Request(harness, initial.Version, lease.Token, "workspace-confirmed-make") with
            {
                Vehicle = new(
                    "AB12 CDE",
                    "Ford",
                    null,
                    null,
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
    [InlineData(CaseLifecycleState.PostReport)]
    public async Task SettlementAndReportSaveTogetherPreservingUnsubmittedFactsAndInvalidatingTheReport(
        CaseLifecycleState state)
    {
        await using var harness = await Harness.CreateAsync();
        var engineer = Engineer(harness);
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
                CaseVersion = 0,
                SnapshotHash = new string('2', 64),
                SnapshotJson = "{\"operationKey\":\"seed-generation-current\"}",
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
                [AssessmentVocabulary.SettlementExcess] = "250.00",
                [AssessmentVocabulary.SettlementBetterment] = "0.00",
                [AssessmentVocabulary.SettlementClaimantVatRegistered] = "false",
                [AssessmentVocabulary.SettlementRepairDelays] = "",
                [AssessmentVocabulary.SettlementHireStart] = "2031-05-20"
            }),
            Report = new(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [AssessmentVocabulary.EngineersComments] = "Scuffed",
                [AssessmentVocabulary.AgreedFee] = "120.00",
                [AssessmentVocabulary.FeeDescriptionLines] = "Assessment report",
                [AssessmentVocabulary.ReportDiscloseGuideSource] = "true",
                [AssessmentVocabulary.ReportValuationCommentary] = "false",
                [AssessmentVocabulary.ReportIncludeUnrelatedDamage] = "false",
                [AssessmentVocabulary.ReportDateOverride] = "false"
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
        Assert.Equal("250.00", saved.Assessment.Field(AssessmentVocabulary.SettlementExcess)?.Value);
        Assert.Equal("false", saved.Assessment.Field(AssessmentVocabulary.SettlementClaimantVatRegistered)?.Value);
        Assert.Null(saved.Assessment.Field(AssessmentVocabulary.SettlementRepairDelays)?.Value);
        Assert.Equal("Scuffed", saved.Assessment.Field(AssessmentVocabulary.EngineersComments)?.Value);
        Assert.Equal("120.00", saved.Assessment.Field(AssessmentVocabulary.AgreedFee)?.Value);
        Assert.Equal("2031-05-20", saved.Assessment.Field(AssessmentVocabulary.ReportDate)?.Value);
        Assert.Equal("false", saved.Assessment.Field(AssessmentVocabulary.ReportDateOverride)?.Value);
        Assert.All(saved.Assessment.Fields, field => Assert.True(field.IsConfirmed));
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
        await using var harness = await Harness.CreateAsync();
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
                    Damage = new([new("front", "heavy", "Never written")], null)
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
        await using var harness = await Harness.CreateAsync();
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var source = await context.CaseDataFields.AsNoTracking().SingleAsync(row =>
                row.CaseId == harness.CaseId && row.FieldName == CaseDataFieldNames.InspectionAddress
                && row.ValueKind == CaseDataCodes.Fact);
            context.CaseDataFields.Add(new CaseDataFieldEntity
            {
                CaseId = harness.CaseId,
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
        var overview = Overview(initial.Claimant.Name.Fact.Value) with { ContactName = "Jane Corrected" };
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
        Assert.Equal("Jane Corrected", saved.Data.Contact.Name.Confirmed?.Value);
        Assert.Equal(actor.SubjectId, saved.Data.Contact.Name.Confirmed?.ConfirmedByActor);

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
        Assert.Equal(saved.Data.Contact.Name.Confirmed, corrected.Data.Contact.Name.Confirmed);
        Assert.Equal(initial.Inspection.Address.Confirmed, corrected.Data.Inspection.Address.Confirmed);
    }

    [Fact]
    public async Task MissingWrongHolderWrongTokenAndExpiredLeasesNeverWriteTheWorkspace()
    {
        await using var harness = await Harness.CreateAsync();
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

        harness.TimeProvider.Advance(TimeSpan.FromHours(4));
        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() =>
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

    [Fact]
    public async Task AnExactReplayReturnsTheOriginalOutcomeAndAChangedPayloadConflicts()
    {
        await using var harness = await Harness.CreateAsync();
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
        await using var harness = await Harness.CreateAsync();
        var initial = await harness.GetRequiredDataAsync();
        var engineer = Engineer(harness);
        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            engineer,
            "lease-partial");
        var historyBefore = await harness.HistoryCountAsync();
        await AcceptAnEstimateAsync(harness);

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
                        [AssessmentVocabulary.SettlementExcess] = "250.00"
                    }),
                    Report = new(new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        [AssessmentVocabulary.EngineersComments] = "Never written",
                        [AssessmentVocabulary.AgreedFee] = "120.00"
                    }, null, new DateOnly(2031, 5, 20)),
                    Estimate = new(null, null, [])
                },
                CancellationToken.None));

        var after = await harness.GetRequiredDataAsync();
        Assert.Equal(initial.Version, after.Version);
        Assert.Equal("Jane Example", after.Claimant.Name.Current?.Value);
        Assert.Equal(historyBefore, await harness.HistoryCountAsync());
        Assert.Equal(0, await AssessmentFieldCountAsync(harness));
    }

    [Fact]
    public async Task AnAcceptedEstimateAndAClosedCaseRefuseTheWorkspaceSave()
    {
        await using var harness = await Harness.CreateAsync();
        var initial = await harness.GetRequiredDataAsync();
        await AcceptAnEstimateAsync(harness);
        var engineer = Engineer(harness);
        var lease = await harness.AcquireLeaseAsync(
            initial.Version,
            engineer,
            "lease-accepted");

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.WorkspaceStore.SaveAsync(
                Request(harness, initial.Version, lease.Token, "workspace-accepted", engineer) with
                {
                    Estimate = new(null, null, [])
                },
                CancellationToken.None));
        Assert.Contains("immutable", refusal.Message, StringComparison.Ordinal);

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
        await using var harness = await Harness.CreateAsync();
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
                    new("Estimate 1", 3, 45m, 120m, 0m, 20m, null),
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
                        null,
                        1)])
            },
            CancellationToken.None);

        Assert.NotNull(result.Estimate);
        Assert.Equal(RepairSpecificationState.Draft, result.Estimate!.State);
        Assert.Equal("Estimate 1", result.Estimate.Details.Name);
        Assert.Equal(45m, result.Estimate.Details.LabourRate);
        var line = Assert.Single(result.Estimate.Lines);
        Assert.Equal("Front bumper", line.Description);
        Assert.Equal(1, line.Position);
        Assert.Equal(engineer.SubjectId, line.ConfirmedBy);
        Assert.Equal(initial.Version + 1, result.Version);
        Assert.Equal(1, await WorkflowEventCountAsync(harness, "case_workspace_saved"));
    }

    [Fact]
    public async Task ReviewGatedTransitionsReadThePersistedFactsNotThePostedOnes()
    {
        // CASE-046: the case is demoted through the workspace save, and the
        // Review-gated transitions then refuse even when the caller sends a
        // readiness envelope claiming the opposite.
        await using var harness = await Harness.CreateAsync();
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
        await Assert.ThrowsAsync<CaseReviewReadinessException>(() =>
            harness.WorkflowStore.ReturnToReviewAsync(
                new(
                    harness.CaseId,
                    demoted.Version,
                    harness.StaffActor,
                    "return-to-review-forged",
                    "Forged readiness claim",
                    transitionLease.Token,
                    new(true, true, "case-completeness-projection")),
                CancellationToken.None));

        // The refused transition never cleared the lease, so the same holder's
        // token is still the live one for the second attempt.
        await Assert.ThrowsAsync<CaseReviewReadinessException>(() =>
            harness.WorkflowStore.AssignEngineerAsync(
                new(
                    harness.CaseId,
                    demoted.Version,
                    harness.StaffActor,
                    "assign-engineer-forged",
                    "Forged readiness claim",
                    transitionLease.Token,
                    Guid.NewGuid(),
                    new(true, true, "case-completeness-projection")),
                null,
                CaseLifecycleState.ReportPreparation,
                CancellationToken.None));

        Assert.Equal(demoted.Version, (await harness.GetRequiredDataAsync()).Version);
    }

    /// <summary>
    /// Phase 5b: an Automation value on a decision field is recorded as an
    /// Awaiting proposal in the same save; the staff save that records the
    /// field resolves it — the same value Accepted, another value Corrected —
    /// and a field nobody proposed carries no proposal.
    /// </summary>
    [Fact]
    public async Task AnAutomationProposalIsAcceptedOrCorrectedByTheStaffSaveThatRecordsTheField()
    {
        await using var harness = await Harness.CreateAsync();
        var engineer = Engineer(harness);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseWorkflows SET State = {nameof(CaseLifecycleState.ReportPreparation)}, AssignedEngineerId = {Guid.Parse(engineer.SubjectId)} WHERE CaseId = {harness.CaseId}");
        }
        var automation = ActionActor.Automation("pegasus-automation");
        var initial = await harness.GetRequiredDataAsync();
        var aiLease = await harness.AcquireLeaseAsync(initial.Version, automation, "proposal-ai-lease");
        await harness.WorkspaceStore.SaveAsync(Request(harness, initial.Version, aiLease.Token, "proposal-ai-save", automation) with
        {
            Settlement = new(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [AssessmentVocabulary.Outcome] = "repairable",
                [AssessmentVocabulary.LegalStatus] = "roadworthy"
            })
        }, default);

        var queries = new EfCaseFieldProposalQueries(harness.Factory);
        Assert.All(
            await queries.ListForCaseAsync(harness.CaseId, default),
            proposal => Assert.Equal(CaseFieldProposalStatus.Awaiting, proposal.Status));

        var afterAi = await harness.GetRequiredDataAsync();
        var staffLease = await harness.AcquireLeaseAsync(afterAi.Version, engineer, "proposal-staff-lease");
        await harness.WorkspaceStore.SaveAsync(Request(harness, afterAi.Version, staffLease.Token, "proposal-staff-save", engineer) with
        {
            Settlement = new(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [AssessmentVocabulary.Outcome] = "repairable",
                [AssessmentVocabulary.LegalStatus] = "unroadworthy",
                [AssessmentVocabulary.UnroadworthyReason] = "Brake line severed",
                [AssessmentVocabulary.SettlementExcess] = "250.00"
            })
        }, default);

        var proposals = (await queries.ListForCaseAsync(harness.CaseId, default)).ToDictionary(item => item.FieldPath);
        Assert.Equal(2, proposals.Count);
        Assert.Equal(CaseFieldProposalStatus.Accepted, proposals[AssessmentVocabulary.Outcome].Status);
        Assert.Equal(CaseFieldProposalStatus.Corrected, proposals[AssessmentVocabulary.LegalStatus].Status);
        Assert.Equal("roadworthy", proposals[AssessmentVocabulary.LegalStatus].ProposedValue);
        Assert.Equal(engineer.SubjectId, proposals[AssessmentVocabulary.LegalStatus].ResolvedBy);
    }

    /// <summary>
    /// Phase 5b: a changed claim source must be an active Claim source record
    /// when the save commits; a deactivated one or an organisation without the
    /// role leaves the Case unchanged. Notes from client save with the Case.
    /// </summary>
    [Fact]
    public async Task AChangedClaimSourceMustBeAnActiveClaimSourceAndNotesFromClientSaveWithTheCase()
    {
        await using var harness = await Harness.CreateAsync();
        var inactiveId = Guid.NewGuid();
        var repairerId = Guid.NewGuid();
        var activeId = Guid.NewGuid();
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            context.Organizations.AddRange(
                new() { Id = inactiveId, Name = "Retired source", Version = 1, Active = false, ContactRoles = [new() { Role = "claim_source" }] },
                new() { Id = repairerId, Name = "A repairer", Version = 1, Active = true, ContactRoles = [new() { Role = "repairer" }] },
                new() { Id = activeId, Name = "Acme Claims", Version = 3, Active = true, ContactRoles = [new() { Role = "claim_source" }] });
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
                    ClientNotes = "The claimant says the car was parked.\n\nPhotos to follow."
                }
            }, default);

        Assert.Equal(activeId, saved.Data.Workspace!.ClaimSource!.ClaimSourceId);
        Assert.Equal("The claimant says the car was parked.\n\nPhotos to follow.", saved.Data.Workspace.ClientNotes);
        var history = await new EfCaseQueryStore(harness.Factory, harness.TimeProvider)
            .ListHistoryByCursorAsync(harness.CaseId, null, null, 20, default);
        Assert.Contains(history, entry => entry.Reason?.Contains("Claim source: Acme Claims", StringComparison.Ordinal) == true
            && entry.Reason.Contains("Client notes", StringComparison.Ordinal));
    }

    private static ActionActor Engineer(Harness harness) => ActionActor.Staff(
        Guid.Parse(harness.StaffActor.SubjectId),
        [StaffRole.Engineer]);

    private static async Task AcceptAnEstimateAsync(Harness harness)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO CaseRepairSpecifications
                (Id, CaseId, Version, State, SourceRoute, CreatedBy, CreationOperationKey,
                 CreatedAtUtc, Name, VatPercent, IsCurrent, AcceptedBy, AcceptedAtUtc)
            VALUES
                ({Guid.NewGuid()}, {harness.CaseId}, {1}, {"Accepted"}, {"LegacyUnresolved"},
                 {harness.StaffActor.SubjectId}, {"accepted-estimate"},
                 {DateTimeOffset.UtcNow}, {"Accepted estimate"}, {20m}, {true},
                 {harness.StaffActor.SubjectId}, {DateTimeOffset.UtcNow})
            """);
    }

    private static async Task<Guid> SeedCurrentGenerationAsync(Harness harness, long caseVersion)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        var generationId = Guid.NewGuid();
        context.Set<CaseReportGenerationEntity>().Add(new()
        {
            Id = generationId,
            CaseId = harness.CaseId,
            CaseVersion = caseVersion,
            SnapshotHash = new string('6', 64),
            SnapshotJson = "{\"operationKey\":\"workspace-mileage-source-generation\"}",
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
            .SingleAsync(item => item.CaseId == harness.CaseId);
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

    private static async Task MarkTerminalAsync(Harness harness)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE CaseWorkflows SET State = {nameof(CaseLifecycleState.ProviderCancelled)} WHERE CaseId = {harness.CaseId}");
    }

    private static async Task<long> WorkflowEventCountAsync(Harness harness, string eventType)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        return await context.CaseWorkflowEvents.AsNoTracking()
            .LongCountAsync(item => item.CaseId == harness.CaseId && item.EventType == eventType);
    }

    private static async Task<long> AssessmentFieldCountAsync(Harness harness)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        return await context.CaseAssessmentFields.AsNoTracking()
            .LongCountAsync(item => item.CaseId == harness.CaseId);
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
        null,
        null,
        null,
        null,
        null);

    private static CaseWorkspaceInspection Inspection(
        CaseReportAddressTreatment treatment,
        string? address) => new(
        treatment,
        address,
        null,
        null,
        null,
        null,
        new DateOnly(2031, 5, 20),
        null,
        null,
        null,
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
