using Pegasus.Core.Cases;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Custody;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Operations;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Infrastructure.Reports;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// The durable half of B05: the freeze/confirm two-transaction shape, the
/// retained Pending/Failed/Unknown custody outcomes and their restart-safe
/// retry, the stale rule, and the A06 report-ready reader contract — all
/// against a real database, with fakes only at the custody and rendering
/// boundaries Stream A owns. The class shares one database
/// (<see cref="ReportGenerationDatabase"/>) and every test makes a Case of its
/// own in it; the tests that change the signatory or add a database user make a
/// database of their own, because both reach every Case.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseReportGenerationPersistenceTests(ReportGenerationDatabase database)
    : IClassFixture<ReportGenerationDatabase>
{
    [Theory]
    [InlineData(CaseReportArtifactKind.AssessmentReport)]
    [InlineData(CaseReportArtifactKind.FeeNote)]
    public async Task UserCanFreezeEachReportArtifactWithTheExistingImmutableGenerationBoundary(
        CaseReportArtifactKind kind)
    {
        await using var harness = await Harness.CreateAsync(database, StaffRole.User);
        var generator = harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness));
        var report = await generator.ExecuteAsync(harness.Request(), default);

        var result = kind == CaseReportArtifactKind.AssessmentReport
            ? report
            : await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
                .ExecuteAsync(harness.Request(
                    CaseReportArtifactKind.FeeNote,
                    operationKey: harness.Key("user-fee-note"),
                    targetGenerationId: report.Generation!.Id), default);

        var generation = Assert.IsType<CaseReportGenerationRecord>(result.Generation);
        Assert.Contains(StaffRole.User.ToString(), generation.Snapshot.GeneratedBy.Roles);
        Assert.Contains(generation.Artifacts, artifact => artifact.Kind == kind);
    }

    [Fact]
    public async Task SourceReaderRejectsMutationDuringWorkspaceReadInsteadOfLabellingOldFieldsAsCurrent()
    {
        await using var harness = await Harness.CreateAsync(database);

        await Assert.ThrowsAsync<CaseVersionConflictException>(() =>
            harness.ReadSourceAsync(new MutatingWorkspaceSource(harness)));
    }

    [Fact]
    public async Task SourceReaderExcludesRetainedReportOutputButKeepsGeneratedInputDocuments()
    {
        await using var harness = await Harness.CreateAsync(database);
        var result = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), default);
        var artifact = Assert.Single(result.Generation!.Artifacts);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var input = await context.Set<DocumentOccurrenceEntity>().SingleAsync(
                item => item.Id == harness.Source.OccurrenceId);
            input.Source = DocumentSource.Generated;
            context.Add(new DocumentOccurrenceEntity
            {
                Id = Guid.NewGuid(), CaseId = harness.CaseId,
                DocumentId = artifact.DocumentId!.Value, VersionId = artifact.VersionId!.Value,
                Ordinal = 4, SemanticRole = DocumentSemanticRole.OriginalSource,
                Source = DocumentSource.Generated, SourceOccurrenceIdentity = "retained-report",
                RecordedAtUtc = Harness.StartUtc, OperationKey = artifact.OperationKey,
            });
            await context.SaveChangesAsync();
        }

        var inputs = await harness.ReadSourceAsync();

        Assert.Contains(inputs!.Projection.Sources, source => source.DocumentId == harness.Source.DocumentId);
        Assert.DoesNotContain(inputs.Projection.Sources, source => source.DocumentId == artifact.DocumentId);
        Assert.Equal(1, inputs.CaseVersion);
    }

    [Fact]
    public async Task FreezeRejectsSignatoryChangedAfterItsInputsWereRead()
    {
        await using var harness = await Harness.CreateIsolatedAsync();
        await harness.ChangeSignatoryAsync("name");

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Store.FreezeAsync(
            new(harness.StaffActor, harness.CaseId, 1, harness.Lease.Token, harness.OperationKey,
                CaseReportArtifactKind.AssessmentReport, "Generate the report",
                AssessmentReportContract.TemplateVersion, "fake"), default));

        Assert.Contains("sign-off Engineer changed", refused.Message, StringComparison.Ordinal);
        Assert.Empty(await harness.GenerationRowsAsync());
    }

    [Fact]
    public async Task FreezeRejectsOlderInputsEvenWhenTheCallerHoldsTheCurrentCaseVersion()
    {
        await using var harness = await Harness.CreateAsync(database);
        // The source is deliberately held at version 1 while a genuine source
        // mutation advances the Case and the caller reloads/reacquires at 2.
        await harness.AddSourceAsync();
        var lease = await new CaseEditLeases(
                new EfCaseWorkflowStore(harness.Factory, Harness.Clock), new InProcessCaseEditSignal())
            .ClaimAsync(new(harness.CaseId, 2, harness.StaffActor, harness.Key("lease-report-2")), default);

        await Assert.ThrowsAsync<CaseVersionConflictException>(() => harness.Store.FreezeAsync(
            new(harness.StaffActor, harness.CaseId, 2, lease.Token, harness.OperationKey,
                CaseReportArtifactKind.AssessmentReport, "Generate the report",
                AssessmentReportContract.TemplateVersion, "fake"), default));

        Assert.Empty(await harness.GenerationRowsAsync());
        Assert.Empty(await harness.ArtifactRowsAsync());
    }

    [Fact]
    public async Task FreezeRefusesTheBrowserExpectedVersionInsteadOfAdoptingTheCurrentVersion()
    {
        await using var harness = await Harness.CreateAsync(database);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == harness.CaseId);
            workflow.Version = 2;
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<CaseVersionConflictException>(() => harness.Store.FreezeAsync(
            new(harness.StaffActor, harness.CaseId, 1, harness.Lease.Token, harness.OperationKey,
                CaseReportArtifactKind.AssessmentReport, "Generate the report",
                AssessmentReportContract.TemplateVersion, "fake"), default));

        Assert.Empty(await harness.GenerationRowsAsync());
        Assert.Empty(await harness.ArtifactRowsAsync());
    }

    [Fact]
    public async Task DefaultReportDateUsesLondonAtBstMidnight()
    {
        await using var harness = await Harness.CreateAsync(database);
        var result = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), default);

        Assert.Equal(new DateOnly(2026, 9, 7), result.Generation!.Snapshot.ReportDate);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Generation.Snapshot.Report.ReportDate);
    }

    [Fact]
    public async Task TypedValuesGenerateWithoutAnAppliedValuationAndTheReportPrintsThem()
    {
        await using var harness = await Harness.CreateAsync(database);
        harness.TypeValues(retail: 6_100m, trade: 4_900m, engineer: 5_700m);
        var renderer = new RecordingRenderer(harness);

        var result = await harness.Generate(new RecordingCustody(harness), renderer)
            .ExecuteAsync(harness.Request(), default);

        Assert.Equal(CaseReportGenerationOutcome.Generated, result.Outcome);
        Assert.Empty(result.Reasons);
        var rendered = Assert.IsType<AssessmentReportSnapshot>(renderer.Rendered);
        Assert.Equal(6_100m, rendered.RetailValue);
        Assert.Equal(4_900m, rendered.TradeValue);
        Assert.Equal(5_700m, rendered.EngineerValue);
        var frozen = result.Generation!.Snapshot;
        Assert.Equal(5_700m, frozen.EngineerValue);
        Assert.Null(frozen.AppliedValuationId);
    }

    [Theory]
    [InlineData("add")]
    [InlineData("remove")]
    [InlineData("occurrence")]
    [InlineData("version")]
    [InlineData("hash")]
    [InlineData("name")]
    [InlineData("media")]
    [InlineData("length")]
    [InlineData("box-file")]
    [InlineData("box-version")]
    [InlineData("current")]
    [InlineData("custody")]
    public async Task FreezeRefusesChangedConfirmedSourceCensusBeforeWriting(string change)
    {
        await using var harness = await Harness.CreateAsync(database);
        // The fixture captured the complete source census at construction.
        // Change persisted evidence independently, without a staff Case edit.
        await harness.ChangeConfirmedSourceAsync(change);
        await using var context = await harness.Factory.CreateDbContextAsync();
        var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == harness.CaseId);
        CaseMutationGuard.Require(workflow, harness.StaffActor, 1, harness.Lease.Token, Harness.StartUtc);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Store.FreezeAsync(
            new(harness.StaffActor, harness.CaseId, 1, harness.Lease.Token, harness.OperationKey,
                CaseReportArtifactKind.AssessmentReport, "Generate the report",
                AssessmentReportContract.TemplateVersion, "fake"), default));

        Assert.Contains("source evidence changed", refused.Message, StringComparison.Ordinal);
        Assert.Empty(await harness.GenerationRowsAsync());
        Assert.Empty(await harness.ArtifactRowsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceAdditionOrRemovalInvalidatesTheReportAndRefusesItsDelivery(bool remove)
    {
        await using var harness = await Harness.CreateAsync(database);
        var generated = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), default);
        var generation = generated.Generation!;
        var delivery = await harness.SendReportAsync(generation);

        if (remove)
        {
            await harness.RemoveSourceAsync();
        }
        else
        {
            await harness.AddSourceAsync();
        }

        var stale = Assert.Single(await harness.GenerationRowsAsync());
        Assert.Equal(CaseReportGenerationState.Stale, stale.State);
        Assert.Equal(CaseReportStaleReasons.SourceDocumentsChanged, await harness.StaleReasonAsync());
        Assert.Equal(generation.SnapshotHash, stale.SnapshotHash);
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.SendReportAsync(generation));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => harness.RequireDeliveryReadyAsync(delivery));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("qualifications")]
    [InlineData("signature")]
    [InlineData("eligibility")]
    [InlineData("disabled")]
    [InlineData("role")]
    [InlineData("unchanged")]
    public async Task EffectiveSignatoryEditsInvalidateOnlyChangedFrozenIdentity(string change)
    {
        await using var harness = await Harness.CreateIsolatedAsync();
        var generated = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), default);
        var generation = generated.Generation!;
        var delivery = await harness.SendReportAsync(generation);

        await harness.ChangeSignatoryAsync(change);

        var current = Assert.Single(await harness.GenerationRowsAsync());
        Assert.Equal(generation.SnapshotHash, current.SnapshotHash);
        Assert.Equal("Ed Mawdsley", current.Snapshot.Report.Signatory.PrintedName);
        if (change is "unchanged" or "role")
        {
            Assert.Equal(CaseReportGenerationState.Confirmed, current.State);
            await harness.RequireDeliveryReadyAsync(delivery);
        }
        else
        {
            Assert.Equal(CaseReportGenerationState.Stale, current.State);
            Assert.Equal(CaseReportStaleReasons.SignatoryChanged, await harness.StaleReasonAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.SendReportAsync(generation));
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RequireDeliveryReadyAsync(delivery));
        }
    }

    [Fact]
    public async Task AssigningTheEngineerStalesTheReportAndRefusesItsDelivery()
    {
        await using var harness = await Harness.CreateIsolatedAsync();
        var generated = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), default);
        var generation = generated.Generation!;
        var replacementId = await harness.AddEligibleSignatoryAsync();
        var workflowStore = new EfCaseWorkflowStore(harness.Factory, Harness.Clock);
        await harness.MarkReviewReadyAsync();
        var changed = await workflowStore.AssignEngineerAsync(
            new(
                harness.CaseId,
                1,
                harness.StaffActor,
                harness.Key("assign-report-signatory"),
                "Assign the replacement Engineer.",
                harness.Lease.Token,
                replacementId),
            CaseLifecycleState.ReportPreparation,
            default);

        var stale = Assert.Single(await harness.GenerationRowsAsync());
        Assert.Equal(CaseReportGenerationState.Stale, stale.State);
        Assert.Equal(CaseReportStaleReasons.SignatoryChanged, await harness.StaleReasonAsync());
        var lease = await new CaseEditLeases(workflowStore, new InProcessCaseEditSignal()).ClaimAsync(
            new(
                harness.CaseId,
                changed.Workflow.Version,
                harness.StaffActor,
                harness.Key("lease-after-report-signatory-change")),
            default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SendReportAsync(generation, changed.Workflow.Version, lease.Token));
    }

    [Theory]
    [InlineData("replacement")]
    [InlineData("effective")]
    public async Task SavingTheWorkspaceSignOffEngineerStalesTheReportOnlyWhenTheEffectiveSignatoryChanges(
        string selection)
    {
        await using var harness = await Harness.CreateIsolatedAsync();
        await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), default);
        var selectedId = selection == "replacement"
            ? await harness.AddEligibleSignatoryAsync()
            : harness.SignatoryId;
        await using (var seed = await harness.Factory.CreateDbContextAsync())
        {
            seed.CaseDataSnapshots.Add(new CaseDataSnapshotEntity
            {
                WorkId = harness.CaseId,
                OriginReceivedAtUtc = Harness.StartUtc,
                CompletenessPolicyKey = "test",
                CompletenessPolicyVersion = 1,
                AcceptedAtUtc = Harness.StartUtc
            });
            await seed.SaveChangesAsync();
        }

        await new EfCaseWorkspaceStore(harness.Factory, Harness.Clock).SaveAsync(
            new SaveCaseWorkspaceRequest(
                harness.CaseId,
                1,
                harness.StaffActor,
                harness.Key($"save-report-signatory-{selection}"),
                null,
                harness.Lease.Token)
            {
                Report = new(null, selectedId, null)
            },
            default);

        var current = Assert.Single(await harness.GenerationRowsAsync());
        if (selection == "replacement")
        {
            Assert.Equal(CaseReportGenerationState.Stale, current.State);
            Assert.Equal(CaseReportStaleReasons.SignatoryChanged, await harness.StaleReasonAsync());
        }
        else
        {
            Assert.Equal(CaseReportGenerationState.Confirmed, current.State);
            Assert.Equal(
                0,
                await harness.ActionHistoryCountAsync(EfCaseReportGenerationStore.StaleEventKind));
        }
    }

    [Theory]
    [InlineData("pegasus_web_runtime_role")]
    [InlineData("pegasus_worker_runtime_role")]
    public async Task RuntimeCustodyCallerInvalidatesSourceButNotItsOwnReportOutput(string role)
    {
        await using var harness = await Harness.CreateIsolatedAsync();
        await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), default);
        await using var context = await harness.Factory.CreateDbContextAsync();
        await context.Database.ExecuteSqlRawAsync("CREATE USER [report_custody_caller] WITHOUT LOGIN;");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC sys.sp_addrolemember @rolename = {role}, @membername = N'report_custody_caller';");
        await harness.Database.AsDatabaseUserAsync("report_custody_caller", async connection =>
        {
            await using var caller = new ConnectedContextFactory(connection).CreateDbContext();
            await using var transaction = await caller.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await EfCaseArtifactCustody.RecordConfirmedSourceChangeAsync(
                caller, harness.CaseId, harness.OperationKey, Harness.StartUtc, default);
            Assert.Equal(1, (await caller.CaseWorkflows.SingleAsync(item => item.CaseId == harness.CaseId)).Version);
            Assert.Equal(nameof(CaseReportGenerationState.Confirmed),
                (await caller.Set<CaseReportGenerationEntity>().SingleAsync(item => item.CaseId == harness.CaseId)).State);

            await EfCaseArtifactCustody.RecordConfirmedSourceChangeAsync(
                caller, harness.CaseId, harness.Key("glass-estimate-source"), Harness.StartUtc, default);
            await transaction.CommitAsync();
        });

        Assert.Equal(CaseReportGenerationState.Stale, Assert.Single(await harness.GenerationRowsAsync()).State);
        var workflow = await context.CaseWorkflows.AsNoTracking().SingleAsync(item => item.CaseId == harness.CaseId);
        Assert.Equal(1, workflow.Version);
        CaseMutationGuard.Require(workflow, harness.StaffActor, 1, harness.Lease.Token, Harness.StartUtc);
        Assert.Equal(harness.Lease.ExpiresAtUtc, workflow.EditLeaseExpiresAtUtc);
        Assert.Equal(1, await harness.ActionHistoryCountAsync("case_report_generation_stale"));
    }

    [Fact]
    public async Task WebRuntimeRoleCanFreezeAndConfirmThroughTheActualReportStore()
    {
        await using var harness = await Harness.CreateIsolatedAsync();
        await harness.Database.ExecuteAsync(
            "CREATE USER [report_generation_caller] WITHOUT LOGIN; ALTER ROLE [pegasus_web_runtime_role] ADD MEMBER [report_generation_caller];");
        await harness.Database.AsDatabaseUserAsync("report_generation_caller", async connection =>
        {
            // Both store transactions run under the same real runtime impersonation.
            var result = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness),
                    store: harness.StoreUsing(new ConnectedContextFactory(connection)))
                .ExecuteAsync(harness.Request(), default);

            Assert.Equal(CaseReportGenerationState.Confirmed, result.Generation!.State);
            Assert.Equal(CaseReportArtifactStatus.Confirmed, Assert.Single(result.Generation.Artifacts).Status);
        });
    }

    [Fact]
    public async Task AReportWithoutARecordedDateIsDatedTodayAndWritesNothingBack()
    {
        await using var harness = await Harness.CreateAsync(database);

        var result = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        var generation = Assert.IsType<CaseReportGenerationRecord>(result.Generation);
        Assert.Equal(Pegasus.Core.LondonCalendar.DateAt(Harness.StartUtc), generation.Snapshot.ReportDate);
        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.False(await context.CaseAssessmentFields.AnyAsync(field =>
            field.WorkId == generation.WorkId && field.FieldPath == AssessmentVocabulary.ReportDate));
    }

    [Fact]
    public async Task FreezeCommitsBeforeRenderingAndConfirmationIsASecondTransaction()
    {
        await using var harness = await Harness.CreateAsync(database);
        var custody = new RecordingCustody(harness);
        var renderer = new RecordingRenderer(harness);
        // The freeze must be committed and lock-free by the time the renderer
        // and Box would run: the renderer reads the artifact row on its own
        // connection, which would block if the freeze still held its lock.
        renderer.Before = async () =>
        {
            var frozen = Assert.Single(await harness.ArtifactRowsAsync());
            Assert.Equal(nameof(CaseReportArtifactStatus.Pending), frozen.State);
            Assert.Equal(1, await harness.ActionHistoryCountAsync("case_report_generation_frozen"));
            Assert.Equal(0, await harness.ActionHistoryCountAsync("case_report_generation_ready"));
        };

        var result = await harness.Generate(custody, renderer)
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, result.Outcome);
        var generation = Assert.IsType<CaseReportGenerationRecord>(result.Generation);
        Assert.Equal(CaseReportGenerationState.Confirmed, generation.State);
        var artifact = Assert.Single(generation.Artifacts);
        Assert.Equal(CaseReportArtifactStatus.Confirmed, artifact.Status);
        Assert.Equal(renderer.Sha256, artifact.Sha256);
        Assert.Equal(harness.OperationKey, artifact.OperationKey);
        Assert.Equal($"case-report:{generation.Id:D}:AssessmentReport", Assert.Single(custody.OccurrenceIdentities));
        Assert.Equal(harness.OperationKey, Assert.Single(custody.OperationKeys));

        // The frozen snapshot pins Box identity alongside document identity
        // and hash for every prepared image and accepted source.
        var image = Assert.Single(
            generation.Snapshot.Images, item => item.Role == CaseAssetReportRole.CloseUp);
        Assert.Equal(harness.CloseUp.DocumentId, image.DocumentId);
        Assert.Equal(harness.CloseUp.VersionId, image.VersionId);
        Assert.Equal($"box-file-{harness.CloseUp.VersionId:N}", image.BoxFileId);
        Assert.Equal($"box-version-{harness.CloseUp.VersionId:N}", image.BoxVersionId);
        Assert.True(image.FullPage);
        Assert.True(Assert.Single(harness.RehydratedPhotos(generation.Snapshot),
            photo => photo.OccurrenceId == harness.CloseUp.OccurrenceId).FullPage);
        Assert.Equal(3, generation.Snapshot.Sources.Count);
        Assert.Contains(generation.Snapshot.Sources, item => item.DocumentId == harness.CloseUp.DocumentId);
        Assert.Contains(generation.Snapshot.Sources, item => item.DocumentId == harness.Overview.DocumentId);
        var source = Assert.Single(generation.Snapshot.Sources,
            item => item.DocumentId == harness.Source.DocumentId);
        Assert.Equal(harness.Source.DocumentId, source.DocumentId);
        Assert.Equal($"box-file-{harness.Source.VersionId:N}", source.BoxFileId);
        Assert.Equal($"box-version-{harness.Source.VersionId:N}", source.BoxVersionId);

        // The confirmed artifact carries the custody object A actually wrote.
        Assert.Equal(custody.VersionId, artifact.VersionId);
        Assert.Equal(custody.DocumentId, artifact.DocumentId);
        Assert.Equal($"box-file-{custody.VersionId:N}", artifact.BoxFileId);
        Assert.Null(artifact.PendingContentStorageKey);

        Assert.Equal(["freeze", "render", "retain", "confirm"], harness.Sequence);
        Assert.Equal(1, await harness.ActionHistoryCountAsync("case_report_artifact_confirmed"));
    }

    /// <summary>
    /// Generate reopens the pinned images through the production reader's
    /// batch lookup: the lookups for every image together, then each image's
    /// own bytes as the renderer prints it. The report is given the images the
    /// generation froze, with the same identities and in the same order, and
    /// each prints its own bytes. Every image was cached when it was filed, so
    /// none is read from Box. The reader's own SQL proves the lookups were made
    /// together: two commands for all four images, where a single read of
    /// each would cost two apiece.
    /// </summary>
    [Fact]
    public async Task GenerateReadsThePinnedImagesThroughTheBatchLookupInTheOrderTheyWereFrozen()
    {
        await using var harness = await Harness.CreateAsync(database, supportingImages: 2);
        // The production reader checks the folder the Case's files are filed in.
        await harness.RecordCaseFolderAsync("report-case-folder");
        Harness.SeededDocument[] pinned = [harness.Overview, harness.CloseUp, .. harness.Supporting];
        Assert.Equal(pinned.Length, pinned.Select(image => image.Sha256).Distinct().Count());

        var box = new UnreachableBox();
        using var boxHttp = new HttpClient(box);
        var metrics = new DocumentContentCacheMetrics();
        // Only the reader's commands are counted; the generation store has its own factory.
        var readerCommands = new SqlStatementCounter();
        var reader = new CachedDocumentContentStore(
            new PooledDbContextFactory<PegasusDbContext>(
                new DbContextOptionsBuilder<PegasusDbContext>()
                    .UseSqlServer(harness.ConnectionString)
                    .AddInterceptors(readerCommands)
                    .Options),
            new InMemoryCacheContainer(),
            new BoxContentClient(BoxOptions(), boxHttp, box, Harness.Clock, TimeProviderMemoryCache.Create(Harness.Clock)),
            Harness.Clock,
            metrics);
        foreach (var image in pinned)
        {
            // Filing caches the copy of each image it files.
            await reader.PublishAsync(
                DocumentContentCacheKey.ForVersion(image.VersionId),
                image.Content,
                image.Sha256,
                CancellationToken.None);
        }
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var versionIds = pinned.Select(image => (Guid?)image.VersionId).ToArray();
            Assert.Equal(pinned.Length, await context.Set<DocumentContentCacheEntryEntity>()
                .CountAsync(entry => versionIds.Contains(entry.DocumentVersionId)));
        }

        await using var scope = harness.CreateScope();
        var content = new EfCaseReportContentSource(
            reader, scope.ServiceProvider.GetRequiredService<IStaffAccountQueries>());
        readerCommands.Reset();
        var renderer = new RecordingRenderer(harness);
        IReadOnlyList<(Guid? VersionId, byte[] Bytes)> printed = [];
        // As the renderer does, at most two at a time, each into its own
        // place in the order the report prints them.
        renderer.Before = async () =>
        {
            var ordered = renderer.Rendered!.OrderedPhotos;
            var opened = new (Guid? VersionId, byte[] Bytes)[ordered.Count];
            await Parallel.ForEachAsync(
                Enumerable.Range(0, ordered.Count),
                new ParallelOptions { MaxDegreeOfParallelism = QuestPdfAssessmentReportRenderer.ImagesPreparedAtOnce },
                async (index, token) =>
                    opened[index] = (ordered[index].VersionId, await ordered[index].OpenAsync(token)));
            printed = opened;
        };

        var result = await harness.Generate(new RecordingCustody(harness), renderer, content: content)
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, result.Outcome);
        var frozen = result.Generation!.Snapshot.Images;
        Assert.Equal(pinned.Select(image => image.VersionId), frozen.Select(image => image.VersionId));
        Assert.Equal(
            [
                CaseAssetReportRole.Overview, CaseAssetReportRole.CloseUp,
                CaseAssetReportRole.Supporting, CaseAssetReportRole.Supporting
            ],
            frozen.Select(image => image.Role));
        Assert.Equal(
            frozen.Select(image => (image.OccurrenceId, image.VersionId, image.Sha256, image.Role, image.Order)),
            renderer.Rendered!.Photos.Select(photo => (
                photo.OccurrenceId!.Value, photo.VersionId!.Value, photo.Sha256, photo.Role, photo.Order)));
        Assert.Equal(pinned.Select(image => (Guid?)image.VersionId), printed.Select(item => item.VersionId));
        Assert.Equal(pinned.Select(image => image.Content), printed.Select(item => item.Bytes));
        Assert.Equal(new DocumentContentCacheMetricSnapshot(pinned.Length, 0), metrics.Snapshot());
        // The authorised versions and their cache entries, once for every
        // image. The entries were written just now, so no hit extends one.
        Assert.True(readerCommands.Count <= 2, readerCommands.Describe());
    }

    /// <summary>
    /// The statements a report and its fee note send together through the
    /// real projection source on a warm staff cache: a ceiling, not a total
    /// (engineering, Simplicity). By the reads they make, about 52: the
    /// report's projection 5 (the workflow, its works, the confirmed
    /// documents, the guides, the wording; its staff reads are kept and its
    /// closing version read is skipped), its freeze 17 (two replay reads, the
    /// lock, the workflow, the work, the source census, the file name, the
    /// sent count, the sign-off list and signature compared under the lock,
    /// the hash lookup, the supersede, up to three saves and the record), each
    /// confirmation 9 (the lock, the generation, the artifact, its siblings,
    /// the ready check, up to two saves and the record) and the fee note's
    /// freeze 12; both composes read the kept signature. Set at 56 until the
    /// first CI run's <see cref="SqlStatementCounter.Describe"/> states the
    /// measured count.
    /// </summary>
    private const int GenerateWithFeeNoteCommands = 56;

    /// <summary>
    /// A report and its fee note generated in one custody batch, as the Case
    /// page and the MCP tool make them (#1952), stay under their ceiling. The
    /// staff reads are kept, so the only signature read is the one the freeze
    /// compares under its lock, the only sign-off list is the one it reads
    /// there, and the confirmed documents are read by the projection and the
    /// freeze's census alone.
    /// </summary>
    [Fact]
    public async Task GenerateWithItsFeeNoteStaysUnderItsStatementCeiling()
    {
        await using var harness = await Harness.CreateAsync(database);
        var counter = new SqlStatementCounter();
        var real = await harness.StoreOverRealSourceAsync(counter);
        await real.WarmStaffReadsAsync(harness.SignatoryId);
        var generate = harness.Generate(
            new RecordingCustody(harness), new RecordingRenderer(harness), store: real.Store, content: real.Content);
        counter.Reset();

        CaseReportGenerationResult report;
        CaseReportGenerationResult feeNote;
        await using (generate.BeginBatch())
        {
            report = await generate.ExecuteAsync(harness.Request(), CancellationToken.None);
            // The fee note is made for the report's generation, as both callers pair them.
            var target = Assert.NotNull(
                GenerateCaseReport.FeeNoteTargetOf(CaseReportArtifactKind.AssessmentReport, report.Generation));
            feeNote = await generate.ExecuteAsync(
                harness.Request(CaseReportArtifactKind.FeeNote, harness.Key("fee-note"), target),
                CancellationToken.None);
        }

        Assert.Equal(CaseReportGenerationOutcome.Generated, report.Outcome);
        Assert.Equal(CaseReportGenerationOutcome.Generated, feeNote.Outcome);
        Assert.Contains(feeNote.Generation!.Artifacts, artifact => artifact is
        {
            Kind: CaseReportArtifactKind.FeeNote,
            Status: CaseReportArtifactStatus.Confirmed,
        });
        Assert.True(counter.Count <= GenerateWithFeeNoteCommands, counter.Describe());
        // The sign-off list names the printed name; the signature read alone does not.
        Assert.True(
            counter.CountMentioning("[SignOffSignature]", without: "[SignOffPrintedName]") <= 1,
            counter.Describe());
        Assert.True(counter.CountContaining("[SignOffPrintedName]") <= 1, counter.Describe());
        Assert.True(
            counter.CountContaining("[DocumentOccurrences]", "[CaseDocuments]", "[GeneratedCaseArtifacts]") <= 2,
            counter.Describe());
    }

    /// <summary>
    /// The projection no longer re-reads the Case's version for the freeze,
    /// which re-reads it under its own lock instead (#1952). A Case moved by
    /// system work while the real source read its workspace, which leaves the
    /// caller's lease and expected version standing, is still refused by that
    /// re-read, and nothing is written.
    /// </summary>
    [Fact]
    public async Task FreezeStillRefusesACaseChangedWhileItsInputsWereRead()
    {
        await using var harness = await Harness.CreateAsync(database);
        var real = await harness.StoreOverRealSourceAsync(
            new SqlStatementCounter(),
            new MutatingWorkspaceSource(harness, harness.MoveVersionAsSystemWorkAsync));
        var generate = harness.Generate(
            new RecordingCustody(harness), new RecordingRenderer(harness), store: real.Store, content: real.Content);

        await Assert.ThrowsAsync<CaseVersionConflictException>(() =>
            generate.ExecuteAsync(harness.Request(), CancellationToken.None));

        Assert.Empty(await harness.GenerationRowsAsync());
        Assert.Empty(await harness.ArtifactRowsAsync());
    }

    /// <summary>
    /// The freeze re-reads the Case's version under its lock, so the
    /// projection's closing read of it is not sent (#1952). The page read,
    /// which holds no lock, still sends it, which shows the statement counted
    /// is that read.
    /// </summary>
    [Fact]
    public async Task TheFreezeSkipsTheProjectionsClosingVersionRead()
    {
        await using var harness = await Harness.CreateAsync(database);
        var counter = new SqlStatementCounter();
        var real = await harness.StoreOverRealSourceAsync(counter);
        await real.WarmStaffReadsAsync(harness.SignatoryId);
        counter.Reset();
        await real.Source.GetAsync(
            harness.CaseId, harness.StaffActor, CaseWorkSelector.Current, reuse: null, CancellationToken.None);
        Assert.True(counter.CountMatching(SelectsOnlyTheCaseVersion) == 1, counter.Describe());
        counter.Reset();

        var result = await harness.Generate(
                new RecordingCustody(harness), new RecordingRenderer(harness), store: real.Store, content: real.Content)
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, result.Outcome);
        Assert.True(counter.CountMatching(SelectsOnlyTheCaseVersion) == 0, counter.Describe());
    }

    /// <summary>
    /// Whether a statement selects nothing but the version from a Case's
    /// workflow row, as the projection's closing re-read does. Every other
    /// read of the row a generation makes selects more, and the lock reads it
    /// with a hint rather than an alias.
    /// </summary>
    private static bool SelectsOnlyTheCaseVersion(string statement)
    {
        var flat = string.Join(' ', statement.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var from = flat.IndexOf(" FROM [CaseWorkflows] AS ", StringComparison.Ordinal);
        return from > 0
            && flat.StartsWith("SELECT ", StringComparison.Ordinal)
            && flat[..from].EndsWith(".[Version]", StringComparison.Ordinal)
            && !flat[..from].Contains(',', StringComparison.Ordinal);
    }

    /// <summary>
    /// B09 review (lifecycle): a stale current generation is history, not a
    /// deliverable snapshot. Regenerating the same material after a stale
    /// must create a NEW current generation - never reuse the stale row and
    /// report it as already generated while it stays undeliverable.
    /// </summary>
    [Fact]
    public async Task RegeneratingAfterAStaleCreatesANewCurrentGeneration()
    {
        await using var harness = await Harness.CreateAsync(database);
        var custody = new RecordingCustody(harness);
        var first = await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var firstGeneration = Assert.IsType<CaseReportGenerationRecord>(first.Generation);
        Assert.Equal(CaseReportGenerationState.Confirmed, firstGeneration.State);

        await harness.Store.MarkStaleAsync(harness.CaseId, "test-material-change", CancellationToken.None);

        // A new operation key is a new generation request; the material is
        // unchanged, so the hash matches the stale generation anyway.
        var second = await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(
                harness.Request(operationKey: harness.Key("case-report-regenerate")), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, second.Outcome);
        var secondGeneration = Assert.IsType<CaseReportGenerationRecord>(second.Generation);
        Assert.Equal(CaseReportGenerationState.Confirmed, secondGeneration.State);
        // The claim is unchanged material: the same hash, a different row.
        Assert.Equal(firstGeneration.SnapshotHash, secondGeneration.SnapshotHash);
        Assert.NotEqual(firstGeneration.Id, secondGeneration.Id);

        var rows = await harness.GenerationRowsAsync();
        var stale = Assert.Single(rows, row => row.Id == firstGeneration.Id);
        Assert.Equal(CaseReportGenerationState.Stale, stale.State);
        Assert.Equal(secondGeneration.Id, stale.SupersededById);
    }

    /// <summary>
    /// The uniqueness the schema keeps after G16 relaxes it for Stale rows:
    /// one Case never holds two live generations of the same material. The
    /// second row is written past the store on purpose, because the store's
    /// own lookup would have reused the first.
    /// </summary>
    [Fact]
    public async Task TwoLiveGenerationsOfTheSameMaterialAreStillRejected()
    {
        await using var harness = await Harness.CreateAsync(database);
        var first = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var live = Assert.IsType<CaseReportGenerationRecord>(first.Generation);
        Assert.Equal(CaseReportGenerationState.Confirmed, live.State);

        await using var context = await harness.Factory.CreateDbContextAsync();
        context.Set<CaseReportGenerationEntity>().Add(new CaseReportGenerationEntity
        {
            Id = Guid.NewGuid(),
            CaseId = harness.CaseId,
            WorkId = harness.CaseId,
            CaseVersion = live.CaseVersion,
            SnapshotHash = live.SnapshotHash,
            SnapshotJson = "{}",
            TemplateVersion = live.TemplateVersion,
            RendererVersion = live.RendererVersion,
            State = nameof(CaseReportGenerationState.Pending),
            GeneratedAtUtc = Harness.StartUtc,
            Version = 1,
        });

        var refused = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Contains(
            "IX_CaseReportGenerations_WorkId_SnapshotHash",
            refused.InnerException?.Message,
            StringComparison.Ordinal);
        Assert.Single(await harness.GenerationRowsAsync());
    }

    [Fact]
    public async Task APendingArtifactIsConfirmedFromTheCustodyStatusQueryAfterARestart()
    {
        await using var harness = await Harness.CreateAsync(database);
        var custody = new RecordingCustody(harness)
        {
            Disposition = CaseArtifactCustodyDisposition.Pending
        };
        var pending = await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Pending, pending.Outcome);
        var pendingArtifact = Assert.Single(pending.Generation!.Artifacts);
        Assert.Equal(CaseReportArtifactStatus.Pending, pendingArtifact.Status);
        // G7 relaxed the custody check constraint so a Pending row keeps the
        // logical identities a restart-safe retry needs.
        Assert.Equal(custody.VersionId, pendingArtifact.VersionId);
        Assert.Equal(custody.DocumentId, pendingArtifact.DocumentId);
        Assert.Equal($"pending/{custody.VersionId:N}", pendingArtifact.PendingContentStorageKey);
        Assert.Equal(0, await harness.ActionHistoryCountAsync("case_report_generation_ready"));

        // Restart: custody settled the object out of process, so the retry
        // asks it what happened instead of rendering the same bytes again.
        await harness.ConfirmCustodyObjectAsync(custody.VersionId!.Value);
        var status = new RecordingCustodyStatus
        {
            Result = new(
                CaseArtifactCustodyDisposition.Confirmed, custody.DocumentId, custody.VersionId, custody.OccurrenceId,
                $"box-file-{custody.VersionId:N}", $"box-version-{custody.VersionId:N}",
                custody.Sha256, custody.ContentLength, "application/pdf", null, null)
        };
        var refusing = new RefusingRenderer();
        harness.Sequence.Clear();

        var confirmed = await harness.Generate(new RecordingCustody(harness), refusing, status)
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, confirmed.Outcome);
        Assert.Equal(pending.Generation.Id, confirmed.Generation!.Id);
        var settled = Assert.Single(confirmed.Generation.Artifacts);
        Assert.Equal(pendingArtifact.Id, settled.Id);
        Assert.Equal(CaseReportArtifactStatus.Confirmed, settled.Status);
        Assert.Equal(custody.Sha256, settled.Sha256);
        Assert.Equal(pendingArtifact.OperationKey, status.LastOperationKey);
        Assert.Null(status.LastQuery);
        Assert.Equal(["freeze", "confirm"], harness.Sequence);
        Assert.Equal(1, await harness.ActionHistoryCountAsync("case_report_generation_ready"));
    }

    /// <summary>
    /// A request can end while its file is still on its way to Box. Custody
    /// files it afterwards, and the Worker's pass then records the report as
    /// stored, so nobody has to press Generate report again.
    /// </summary>
    [Fact]
    public async Task APendingReportWhoseFileIsFiledLaterIsSettledWithoutAnotherGenerate()
    {
        await using var harness = await Harness.CreateAsync(database);
        var custody = new RecordingCustody(harness)
        {
            Disposition = CaseArtifactCustodyDisposition.Pending
        };
        var pending = await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        Assert.Equal(CaseReportGenerationOutcome.Pending, pending.Outcome);
        await harness.RecordCustodyOccurrenceAsync(custody, harness.OperationKey);
        var later = Harness.StartUtc.AddMinutes(4);

        // Nothing is filed yet, so there is nothing to settle however old it is.
        Assert.Equal(0, await harness.SettleAt(later).ExecuteAsync(50, CancellationToken.None));
        Assert.Equal(
            CaseReportArtifactStatus.Pending,
            Assert.Single(Assert.Single(await harness.GenerationRowsAsync()).Artifacts).Status);

        // Custody files the version out of process, as its reconciliation does.
        await harness.ConfirmCustodyObjectAsync(custody.VersionId!.Value);

        Assert.Equal(1, await harness.SettleAt(later).ExecuteAsync(50, CancellationToken.None));

        var generation = Assert.Single(await harness.GenerationRowsAsync());
        Assert.Equal(CaseReportGenerationState.Confirmed, generation.State);
        var artifact = Assert.Single(generation.Artifacts);
        Assert.Equal(CaseReportArtifactStatus.Confirmed, artifact.Status);
        Assert.Equal(custody.VersionId, artifact.VersionId);
        Assert.Equal(custody.DocumentId, artifact.DocumentId);
        Assert.Equal(custody.Sha256, artifact.Sha256);
        Assert.Equal($"box-file-{custody.VersionId:N}", artifact.BoxFileId);
        Assert.Null(artifact.FailureCode);

        // The history says the Worker confirmed it, and names the file as custody holds it.
        var confirmed = Assert.Single(await harness.ArtifactConfirmedEventsAsync());
        Assert.Equal(nameof(ActorKind.SystemWorker), confirmed.ActorKind);
        Assert.Equal(EfSettleFiledCaseReportArtifacts.ActorId, confirmed.ActorSubjectId);
        Assert.Equal(later, confirmed.OccurredAtUtc);
        Assert.Equal(harness.OperationKey, confirmed.CorrelationId);
        using var after = JsonDocument.Parse(confirmed.AfterJson!);
        Assert.Equal(generation.Snapshot.ReportFileName, after.RootElement.GetProperty("fileName").GetString());
        Assert.Equal(
            custody.VersionId,
            Guid.Parse(after.RootElement.GetProperty("versionId").GetString()!));
        var ready = Assert.Single(await harness.ReadyEventsAsync());
        Assert.Equal(EfSettleFiledCaseReportArtifacts.ActorId, ready.ActorSubjectId);

        // A second pass finds nothing left to settle and writes nothing more.
        Assert.Equal(0, await harness.SettleAt(later).ExecuteAsync(50, CancellationToken.None));
        Assert.Single(await harness.ArtifactConfirmedEventsAsync());
        Assert.Single(await harness.ReadyEventsAsync());

        // Generate report pressed afterwards answers from the settled row and draws nothing.
        harness.Sequence.Clear();
        var replayed = await harness.Generate(new RecordingCustody(harness), new RefusingRenderer())
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        Assert.Equal(CaseReportGenerationOutcome.Generated, replayed.Outcome);
        Assert.Equal(["freeze"], harness.Sequence);
    }

    /// <summary>
    /// The production case: the file was filed while the request still ran,
    /// and the request recorded Pending. The pass leaves the report to its
    /// request until it was frozen more than three minutes ago.
    /// </summary>
    [Fact]
    public async Task AFiledReportFrozenWithinThreeMinutesIsLeftToItsRequest()
    {
        await using var harness = await Harness.CreateAsync(database);
        var custody = new RecordingCustody(harness)
        {
            Disposition = CaseArtifactCustodyDisposition.Pending,
            ConfirmedInCustody = true
        };
        var pending = await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        Assert.Equal(CaseReportGenerationOutcome.Pending, pending.Outcome);
        Assert.Equal(
            DocumentCustodyStatus.Confirmed,
            await harness.CustodyStatusAsync(custody.VersionId!.Value));
        await harness.RecordCustodyOccurrenceAsync(custody, harness.OperationKey);

        Assert.Equal(
            0,
            await harness.SettleAt(Harness.StartUtc.AddMinutes(3)).ExecuteAsync(50, CancellationToken.None));

        var untouched = Assert.Single(await harness.GenerationRowsAsync());
        Assert.Equal(CaseReportGenerationState.Pending, untouched.State);
        Assert.Equal(CaseReportArtifactStatus.Pending, Assert.Single(untouched.Artifacts).Status);
        Assert.Empty(await harness.ArtifactConfirmedEventsAsync());

        Assert.Equal(
            1,
            await harness.SettleAt(Harness.StartUtc.AddMinutes(3).AddSeconds(1))
                .ExecuteAsync(50, CancellationToken.None));
        Assert.Equal(
            CaseReportGenerationState.Confirmed,
            Assert.Single(await harness.GenerationRowsAsync()).State);
    }

    /// <summary>
    /// A fee note joins a report frozen long before, so the report's age
    /// says nothing about the request producing the fee note. The pass goes
    /// by when custody recorded the file as well.
    /// </summary>
    [Fact]
    public async Task ACompanionDocumentIsLeftToItsRequestHoweverOldItsReportIs()
    {
        await using var harness = await Harness.CreateAsync(database);
        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var feeOperationKey = harness.Key("case-report-fee-later");
        var custody = new RecordingCustody(harness)
        {
            Disposition = CaseArtifactCustodyDisposition.Pending,
            ConfirmedInCustody = true
        };
        var feeNote = await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(
                harness.Request(
                    CaseReportArtifactKind.FeeNote,
                    feeOperationKey,
                    targetGenerationId: report.Generation!.Id),
                CancellationToken.None);
        Assert.Equal(CaseReportGenerationOutcome.Pending, feeNote.Outcome);
        var asked = Harness.StartUtc.AddHours(1);
        await harness.RecordCustodyOccurrenceAsync(custody, feeOperationKey, asked);

        Assert.Equal(
            0,
            await harness.SettleAt(asked.AddMinutes(2)).ExecuteAsync(50, CancellationToken.None));
        Assert.Equal(
            1,
            await harness.SettleAt(asked.AddMinutes(4)).ExecuteAsync(50, CancellationToken.None));

        var generation = Assert.Single(await harness.GenerationRowsAsync());
        Assert.Equal(CaseReportGenerationState.Confirmed, generation.State);
        Assert.Equal(2, generation.Artifacts.Count);
        Assert.All(
            generation.Artifacts,
            artifact => Assert.Equal(CaseReportArtifactStatus.Confirmed, artifact.Status));
        // The report was ready before the fee note was asked for; it is not announced twice.
        Assert.Single(await harness.ReadyEventsAsync());
    }

    /// <summary>
    /// Catalogue checks run as the database owner. This runs the actual pass
    /// as a member of the Worker's role, which must be able to update the
    /// artifact row it could only read before.
    /// </summary>
    [Fact]
    public async Task WorkerRuntimeRoleCanSettleAFiledReportThroughTheActualPass()
    {
        await using var harness = await Harness.CreateIsolatedAsync();
        var custody = new RecordingCustody(harness)
        {
            Disposition = CaseArtifactCustodyDisposition.Pending,
            ConfirmedInCustody = true
        };
        await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        await harness.RecordCustodyOccurrenceAsync(custody, harness.OperationKey);
        await harness.Database.ExecuteAsync(
            "CREATE USER [report_settlement_caller] WITHOUT LOGIN; ALTER ROLE [pegasus_worker_runtime_role] ADD MEMBER [report_settlement_caller];");
        await harness.Database.AsDatabaseUserAsync("report_settlement_caller", async connection =>
        {
            // The pass's read and its confirmation both run under the real runtime role.
            var settled = await new EfSettleFiledCaseReportArtifacts(
                    new ConnectedContextFactory(connection), Harness.ClockAt(Harness.StartUtc.AddMinutes(4)))
                .ExecuteAsync(50, CancellationToken.None);

            Assert.Equal(1, settled);
        });

        var generation = Assert.Single(await harness.GenerationRowsAsync());
        Assert.Equal(CaseReportGenerationState.Confirmed, generation.State);
        Assert.Equal(CaseReportArtifactStatus.Confirmed, Assert.Single(generation.Artifacts).Status);
    }

    /// <summary>
    /// The whole chain over custody itself, with no fake between the report
    /// and its file: the report waits for its Box folder, a second press draws
    /// nothing, reconciliation files the version once the folder exists, and
    /// the pass then records the report as stored.
    /// </summary>
    [Fact]
    public async Task AReportWaitingForItsFolderIsFiledByReconciliationAndThenSettled()
    {
        await using var harness = await Harness.CreateAsync(database);
        await using var scope = harness.CreateScope();
        var content = scope.ServiceProvider.GetRequiredService<IDocumentContentStore>();
        var custody = new EfCaseArtifactCustody(
            harness.Factory,
            content,
            scope.ServiceProvider.GetRequiredService<Pegasus.Core.Intake.IIntakeQuarantineArtifactStore>(),
            Harness.Clock);

        var pending = await harness.GenerateThroughCustody(custody, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Pending, pending.Outcome);
        var waiting = Assert.Single(pending.Generation!.Artifacts);
        Assert.Equal(CaseReportArtifactStatus.Pending, waiting.Status);
        Assert.Equal("case_custody_pending", waiting.FailureCode);

        // A second press asks custody first. Drawing again could never match
        // the bytes custody holds, so the renderer here refuses every call.
        harness.Sequence.Clear();
        var pressedAgain = await harness.GenerateThroughCustody(custody, new RefusingRenderer())
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        Assert.Equal(CaseReportGenerationOutcome.Pending, pressedAgain.Outcome);
        Assert.Equal(["freeze", "record"], harness.Sequence);

        await harness.RecordCaseFolderAsync("case-root");
        // The sweep leaves a newly recorded version to its request for the
        // inline-filing grace, so it runs once that has passed.
        var reconciled = await new ReconcilePendingArtifactCustody(
                harness.Factory,
                content,
                scope.ServiceProvider.GetRequiredService<Pegasus.Core.Intake.IIntakeArtifactStore>(),
                Harness.ClockAt(Harness.StartUtc + PendingCustodyRetryPolicy.InlineFilingGrace))
            .ExecuteAsync(10, CancellationToken.None);
        Assert.Equal(1, reconciled.Confirmed);
        Assert.Equal(
            DocumentCustodyStatus.Confirmed,
            await harness.CustodyStatusAsync(waiting.VersionId!.Value));
        // Filing the version leaves the report's own record as it was.
        Assert.Equal(
            CaseReportArtifactStatus.Pending,
            Assert.Single(Assert.Single(await harness.GenerationRowsAsync()).Artifacts).Status);

        Assert.Equal(
            1,
            await harness.SettleAt(Harness.StartUtc.AddMinutes(4)).ExecuteAsync(50, CancellationToken.None));

        var generation = Assert.Single(await harness.GenerationRowsAsync());
        Assert.Equal(CaseReportGenerationState.Confirmed, generation.State);
        var settled = Assert.Single(generation.Artifacts);
        Assert.Equal(waiting.Id, settled.Id);
        Assert.Equal(CaseReportArtifactStatus.Confirmed, settled.Status);
        Assert.Equal(waiting.VersionId, settled.VersionId);
        Assert.Equal(new RecordingRenderer(harness).Sha256, settled.Sha256);
        Assert.Null(settled.FailureCode);
        Assert.Null(settled.PendingContentStorageKey);
        Assert.Single(await harness.ReadyEventsAsync());
    }

    [Fact]
    public async Task AFailedArtifactIsRetriedWithTheSameSnapshotOperationKeyAndArtifactRow()
    {
        await using var harness = await Harness.CreateAsync(database);
        var failing = new RecordingCustody(harness)
        {
            Disposition = CaseArtifactCustodyDisposition.Failed,
            FailureCode = "box_upload_rejected"
        };

        var failed = await harness.Generate(failing, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Failed, failed.Outcome);
        var failedArtifact = Assert.Single(failed.Generation!.Artifacts);
        Assert.Equal(CaseReportArtifactStatus.Failed, failedArtifact.Status);
        Assert.Equal("box_upload_rejected", failedArtifact.FailureCode);
        Assert.Null(failedArtifact.Sha256);
        Assert.Equal(0, await harness.ActionHistoryCountAsync("case_report_generation_ready"));

        var custody = new RecordingCustody(harness);
        harness.Sequence.Clear();
        var retried = await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, retried.Outcome);
        // Same generation, same frozen snapshot, same artifact row and the
        // same operation key: a retry is never a second identity.
        Assert.Equal(failed.Generation.Id, retried.Generation!.Id);
        Assert.Equal(failed.Generation.SnapshotHash, retried.Generation.SnapshotHash);
        var retriedArtifact = Assert.Single(retried.Generation.Artifacts);
        Assert.Equal(failedArtifact.Id, retriedArtifact.Id);
        Assert.Equal(harness.OperationKey, Assert.Single(custody.OperationKeys));
        Assert.Equal(CaseReportArtifactStatus.Confirmed, retriedArtifact.Status);
        Assert.Null(retriedArtifact.FailureCode);
        Assert.Equal(["freeze", "render", "retain", "confirm"], harness.Sequence);
        Assert.Single(await harness.GenerationRowsAsync());
    }

    [Fact]
    public async Task AFailedSeparateFeeNoteRecoversWithItsRetainedOperationIdentity()
    {
        await using var harness = await Harness.CreateAsync(database);
        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var feeOperationKey = harness.Key("case-report-fee-retry");
        var request = harness.Request(
            CaseReportArtifactKind.FeeNote,
            feeOperationKey,
            targetGenerationId: report.Generation!.Id);
        var failing = new RecordingCustody(harness)
        {
            Disposition = CaseArtifactCustodyDisposition.Failed,
            FailureCode = "transient_fee_note_failure",
        };

        var failed = await harness.Generate(failing, new RecordingRenderer(harness))
            .ExecuteAsync(request, CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Failed, failed.Outcome);
        var failedArtifact = Assert.Single(
            failed.Generation!.Artifacts,
            artifact => artifact.Kind == CaseReportArtifactKind.FeeNote);
        Assert.Equal(CaseReportArtifactStatus.Failed, failedArtifact.Status);
        Assert.Equal(feeOperationKey, failedArtifact.OperationKey);

        var custody = new RecordingCustody(harness);
        var recovered = await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(request, CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, recovered.Outcome);
        Assert.Equal(report.Generation.Id, recovered.Generation!.Id);
        var recoveredArtifact = Assert.Single(
            recovered.Generation.Artifacts,
            artifact => artifact.Kind == CaseReportArtifactKind.FeeNote);
        Assert.Equal(failedArtifact.Id, recoveredArtifact.Id);
        Assert.Equal(feeOperationKey, recoveredArtifact.OperationKey);
        Assert.Equal(feeOperationKey, Assert.Single(custody.OperationKeys));
        Assert.Equal(CaseReportArtifactStatus.Confirmed, recoveredArtifact.Status);
        Assert.Equal(2, recovered.Generation.Artifacts.Count);
        Assert.All(
            recovered.Generation.Artifacts,
            artifact => Assert.Equal(CaseReportArtifactStatus.Confirmed, artifact.Status));
    }

    [Fact]
    public async Task AnUnknownCustodyOutcomeIsRetainedRatherThanRetriedAway()
    {
        await using var harness = await Harness.CreateAsync(database);
        var custody = new RecordingCustody(harness)
        {
            Disposition = CaseArtifactCustodyDisposition.Unknown
        };

        var unknown = await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Pending, unknown.Outcome);
        var artifact = Assert.Single(unknown.Generation!.Artifacts);
        Assert.Equal(CaseReportArtifactStatus.Unknown, artifact.Status);
        Assert.Equal(custody.VersionId, artifact.VersionId);
        Assert.Equal($"pending/{custody.VersionId:N}", artifact.PendingContentStorageKey);
        Assert.Equal(CaseReportGenerationState.Pending, unknown.Generation.State);
        Assert.Equal(1, custody.Calls);

        // The unresolved outcome stays exactly as recorded until something
        // asks again; nothing retries it in the background.
        var current = await harness.Store.GetCurrentAsync(
            harness.StaffActor, harness.CaseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.Equal(
            CaseReportArtifactStatus.Unknown, Assert.Single(current!.Artifacts).Status);
        Assert.Equal(0, await harness.ActionHistoryCountAsync("case_report_generation_ready"));
        Assert.Equal(1, await harness.ActionHistoryCountAsync("case_report_artifact_outcome_recorded"));
    }

    [Fact]
    public async Task TheReadyEventIsWrittenExactlyOnceAndCarriesTheA06ReaderContract()
    {
        await using var harness = await Harness.CreateAsync(database);

        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        Assert.Equal(CaseReportGenerationOutcome.Generated, report.Outcome);

        var ready = Assert.Single(await harness.ReadyEventsAsync());
        Assert.Equal("case_report_generation_ready", ready.EventKind);
        Assert.Equal("Succeeded", ready.Outcome);
        Assert.Equal("case", ready.AggregateType);
        Assert.Equal(harness.CaseId.ToString("D"), ready.AggregateId);
        Assert.Equal(report.Generation!.Snapshot.OperationKey, ready.CorrelationId);
        Assert.Equal(harness.OperationKey, ready.CorrelationId);
        Assert.Equal(nameof(ActorKind.Staff), ready.ActorKind);
        Assert.Equal(harness.StaffActor.SubjectId, ready.ActorSubjectId);
        Assert.Equal(Harness.StartUtc, ready.OccurredAtUtc);

        using var after = JsonDocument.Parse(ready.AfterJson!);
        Assert.Equal(JsonValueKind.Object, after.RootElement.ValueKind);
        var generationId = after.RootElement.GetProperty("generationId");
        Assert.Equal(JsonValueKind.String, generationId.ValueKind);
        Assert.Equal(report.Generation.Id, Guid.Parse(generationId.GetString()!));

        // Asking the same snapshot for its fee note adds a second artifact and
        // confirms it; the report-ready transition is not announced twice.
        var feeNote = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(
                harness.Request(
                    CaseReportArtifactKind.FeeNote,
                    harness.Key("case-report-fee-1"),
                    targetGenerationId: report.Generation.Id),
                CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, feeNote.Outcome);
        Assert.Equal(report.Generation.Id, feeNote.Generation!.Id);
        Assert.Equal(2, feeNote.Generation.Artifacts.Count);
        Assert.All(
            feeNote.Generation.Artifacts,
            item => Assert.Equal(CaseReportArtifactStatus.Confirmed, item.Status));
        Assert.Single(await harness.ReadyEventsAsync());
    }

    [Fact]
    public async Task SameOperationKeyWithDifferentArtifactKindIsAConflict()
    {
        await using var harness = await Harness.CreateAsync(database);
        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
                .ExecuteAsync(
                    harness.Request(
                        CaseReportArtifactKind.FeeNote,
                        harness.OperationKey,
                        targetGenerationId: report.Generation!.Id),
                    CancellationToken.None));

        Assert.Single((await harness.GenerationRowsAsync()).Single().Artifacts);
    }

    [Fact]
    public async Task SameOperationKeyWithDifferentArtifactKindConflictsBeforeCustody()
    {
        await using var harness = await Harness.CreateAsync(database);
        var frozen = await harness.Store.FreezeAsync(
            new(harness.StaffActor, harness.CaseId, 1, harness.Lease.Token, harness.OperationKey,
                CaseReportArtifactKind.AssessmentReport, "Generate the report",
                AssessmentReportContract.TemplateVersion, "fake"), default);

        await Assert.ThrowsAsync<CaseOperationConflictException>(() => harness.Store.FreezeAsync(
            new(harness.StaffActor, harness.CaseId, 1, harness.Lease.Token, harness.OperationKey,
                CaseReportArtifactKind.FeeNote, "Generate the fee note",
                AssessmentReportContract.TemplateVersion, "fake",
                TargetGenerationId: frozen.Generation!.Id), default));

        var artifact = Assert.Single(await harness.ArtifactRowsAsync());
        Assert.Equal(nameof(CaseReportArtifactKind.AssessmentReport), artifact.Kind);
        Assert.Equal(nameof(CaseReportArtifactStatus.Pending), artifact.State);
    }

    [Fact]
    public async Task SameOperationKeyWithChangedTargetGenerationIsAConflict()
    {
        await using var harness = await Harness.CreateAsync(database);
        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var feeOperationKey = harness.Key("case-report-fee-target");
        await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(
                harness.Request(
                    CaseReportArtifactKind.FeeNote,
                    feeOperationKey,
                    targetGenerationId: report.Generation!.Id),
                CancellationToken.None);

        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
                .ExecuteAsync(
                    harness.Request(
                        CaseReportArtifactKind.FeeNote,
                        feeOperationKey,
                        targetGenerationId: Guid.NewGuid()),
                    CancellationToken.None));

        Assert.Equal(2, (await harness.GenerationRowsAsync()).Single().Artifacts.Count);
    }

    /// <summary>
    /// Whether the report carries vehicle images is the Principal's report
    /// sending rule (operator, 6 October 2026), read with the Principal's
    /// name and address: a Principal without the rule has them.
    /// </summary>
    [Fact]
    public async Task TheSourceReaderReadsWhetherTheReportHasVehicleImagesFromThePrincipal()
    {
        await using var harness = await Harness.CreateAsync(database);

        Assert.True((await harness.ReadSourceAsync())!.Projection.IncludeVehicleImages);

        await harness.SetReportImagesRuleAsync(false);

        Assert.False((await harness.ReadSourceAsync())!.Projection.IncludeVehicleImages);
    }

    /// <summary>
    /// The fee note's VAT number is the Principal's (R1006), read with the
    /// Principal's name and address: a Principal without one has the standard
    /// number, which the source reads as none.
    /// </summary>
    [Fact]
    public async Task TheSourceReaderReadsTheFeeNoteVatNumberFromThePrincipal()
    {
        await using var harness = await Harness.CreateAsync(database);

        Assert.Null((await harness.ReadSourceAsync())!.Projection.VatNumber);

        await harness.SetFeeNoteVatNumberAsync("GB 123 4567 89");

        Assert.Equal("GB 123 4567 89", (await harness.ReadSourceAsync())!.Projection.VatNumber);
    }

    /// <summary>
    /// A generation freezes its Principal's fee note VAT number (R1006): a
    /// later change to the Principal does not alter a fee note already
    /// generated. A generation of the standard number stores no VAT number at
    /// all, so its snapshot is what it was before a Principal could have one.
    /// </summary>
    [Fact]
    public async Task AGenerationFreezesItsPrincipalsFeeNoteVatNumber()
    {
        await using var harness = await Harness.CreateAsync(database);
        var standard = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        Assert.Null(standard.Generation!.Snapshot.Report.VatNumber);
        Assert.False(await StoresAVatNumberAsync(standard.Generation.Id));

        await harness.SetFeeNoteVatNumberAsync("GB 123 4567 89");
        await harness.Store.MarkStaleAsync(harness.CaseId, "test-material-change", CancellationToken.None);
        var own = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(operationKey: harness.Key("report-own-vat")), CancellationToken.None);
        Assert.Equal("GB 123 4567 89", own.Generation!.Snapshot.Report.VatNumber);
        Assert.True(await StoresAVatNumberAsync(own.Generation.Id));

        await harness.SetFeeNoteVatNumberAsync(null);
        var reloaded = await harness.Store.GetAsync(
            harness.StaffActor, harness.CaseId, own.Generation.Id, CancellationToken.None);
        Assert.Equal("GB 123 4567 89", reloaded!.Snapshot.Report.VatNumber);

        async Task<bool> StoresAVatNumberAsync(Guid generationId)
        {
            await using var context = await harness.Factory.CreateDbContextAsync();
            var json = await context.Set<CaseReportGenerationEntity>()
                .Where(item => item.Id == generationId)
                .Select(item => item.SnapshotJson)
                .SingleAsync();
            return JsonNode.Parse(json)!["report"]!.AsObject().ContainsKey("vatNumber");
        }
    }

    /// <summary>The issuer the Principal's own invoice prints in these tests: the draft's.</summary>
    private static readonly string[] InvoiceIssuer =
        ["United Legal Assistance Limited", "130c Lord Street", "Southport PR9 0AE"];

    /// <summary>The fixture's claimant, whose address is not recorded: the name alone (R1149).</summary>
    private static readonly string[] InvoiceAddressee = ["Alex Example"];

    /// <summary>
    /// The Principal's own invoice's facts are read with the Principal, only
    /// for a Principal whose setting asks for the ULA Invoice (R1147 to
    /// R1149, R1162): its name and address lines with the postcode after the
    /// last, the claimant's name alone when no address is recorded, the
    /// Case's make and model in capitals, and its VAT number and terms.
    /// </summary>
    [Fact]
    public async Task TheSourceReaderReadsThePrincipalsInvoiceFactsFromThePrincipal()
    {
        await using var harness = await Harness.CreateAsync(database);

        Assert.Null((await harness.ReadSourceAsync())!.Projection.PrincipalInvoice);

        var name = await harness.SetPrincipalAddressAsync("130c Lord Street\nSouthport", "PR9 0AE");
        await harness.SetPrincipalInvoiceAsync(PrincipalInvoicePolicy.UlaInvoiceCode, "GB 987 6543 21", "30 days");

        var invoice = Assert.IsType<UlaInvoiceSnapshot>((await harness.ReadSourceAsync())!.Projection.PrincipalInvoice);
        string[] issuer = [name, "130c Lord Street", "Southport PR9 0AE"];
        Assert.Equal(issuer, invoice.Issuer);
        Assert.Equal(InvoiceAddressee, invoice.Addressee);
        Assert.Equal("FORD FOCUS", invoice.SubjectVehicle);
        Assert.Equal("GB 987 6543 21", invoice.VatNumber);
        Assert.Equal("30 days", invoice.PaymentTerms);

        await harness.SetPrincipalInvoiceAsync(PrincipalInvoicePolicy.NoneCode, "GB 987 6543 21", "30 days");

        Assert.Null((await harness.ReadSourceAsync())!.Projection.PrincipalInvoice);
    }

    /// <summary>
    /// A generation for a Principal with the ULA Invoice freezes the
    /// invoice's facts with its report, and a later change to the Principal
    /// does not alter them. A Principal without it freezes none at all, so
    /// its snapshot is the bytes it was before the invoice existed (R1147).
    /// </summary>
    [Fact]
    public async Task AGenerationFreezesItsPrincipalsInvoiceFactsOnlyForAPrincipalThatHasOne()
    {
        await using var harness = await Harness.CreateAsync(database);
        var none = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        Assert.Null(none.Generation!.Snapshot.Report.PrincipalInvoice);
        Assert.False(await StoresAnInvoiceAsync(none.Generation.Id));

        await harness.SetPrincipalInvoiceAsync(
            PrincipalInvoicePolicy.UlaInvoiceCode,
            "GB 987 6543 21",
            "30 days",
            new UlaInvoiceSnapshot(InvoiceIssuer, InvoiceAddressee, "FORD FOCUS", "GB 987 6543 21", "30 days"));
        await harness.Store.MarkStaleAsync(harness.CaseId, "test-material-change", CancellationToken.None);
        var own = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(operationKey: harness.Key("report-invoice")), CancellationToken.None);

        var frozen = Assert.IsType<UlaInvoiceSnapshot>(own.Generation!.Snapshot.Report.PrincipalInvoice);
        Assert.Equal(InvoiceIssuer, frozen.Issuer);
        Assert.Equal(InvoiceAddressee, frozen.Addressee);
        Assert.Equal("FORD FOCUS", frozen.SubjectVehicle);
        Assert.Equal("GB 987 6543 21", frozen.VatNumber);
        Assert.Equal("30 days", frozen.PaymentTerms);
        Assert.True(await StoresAnInvoiceAsync(own.Generation.Id));

        await harness.SetPrincipalInvoiceAsync(PrincipalInvoicePolicy.NoneCode, null, null);
        var reloaded = await harness.Store.GetAsync(
            harness.StaffActor, harness.CaseId, own.Generation.Id, CancellationToken.None);
        Assert.Equal("GB 987 6543 21", reloaded!.Snapshot.Report.PrincipalInvoice!.VatNumber);

        async Task<bool> StoresAnInvoiceAsync(Guid generationId)
        {
            await using var context = await harness.Factory.CreateDbContextAsync();
            var json = await context.Set<CaseReportGenerationEntity>()
                .Where(item => item.Id == generationId)
                .Select(item => item.SnapshotJson)
                .SingleAsync();
            return JsonNode.Parse(json)!["report"]!.AsObject().ContainsKey("principalInvoice");
        }
    }

    /// <summary>
    /// The Principal's own invoice is made after its report, from the same
    /// confirmed generation, by the integrated renderer, and filed as
    /// <c>ULA_Invoice_{claim reference}.pdf</c> (R1147, R1148). Once it is
    /// confirmed the generation pairs no other.
    /// </summary>
    [Fact]
    public async Task ThePrincipalsInvoiceIsMadeAfterItsReportAndFiledUnderTheClaimReference()
    {
        await using var harness = await Harness.CreateAsync(database);
        await harness.SetPrincipalInvoiceAsync(
            PrincipalInvoicePolicy.UlaInvoiceCode,
            null,
            null,
            new UlaInvoiceSnapshot(InvoiceIssuer, InvoiceAddressee, "FORD FOCUS"));
        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        Assert.Equal(CaseReportGenerationOutcome.Generated, report.Outcome);
        var target = Assert.NotNull(
            GenerateCaseReport.PrincipalInvoiceTargetOf(CaseReportArtifactKind.AssessmentReport, report.Generation));
        Assert.Equal(report.Generation!.Id, target);

        var services = new ServiceCollection();
        services.AddLogging();
        Pegasus.Infrastructure.DependencyInjection.AddPegasusReportRendering(services);
        await using var provider = services.BuildServiceProvider();
        var invoice = await harness.Generate(
                new RecordingCustody(harness), provider.GetRequiredService<IAssessmentReportRenderer>())
            .ExecuteAsync(
                harness.Request(CaseReportArtifactKind.PrincipalInvoice, harness.Key("principal-invoice"), target),
                CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, invoice.Outcome);
        var filed = Assert.Single(
            invoice.Generation!.Artifacts, artifact => artifact.Kind == CaseReportArtifactKind.PrincipalInvoice);
        Assert.Equal(CaseReportArtifactStatus.Confirmed, filed.Status);
        // The fixture's claim reference is P-100, made safe as the report's own names are.
        Assert.Equal("ULA_Invoice_P_100.pdf", filed.FileName);
        Assert.Null(
            GenerateCaseReport.PrincipalInvoiceTargetOf(CaseReportArtifactKind.AssessmentReport, invoice.Generation));
    }

    /// <summary>
    /// The report is filed under the name frozen with it (R740): the
    /// Principal's Inspection name on a first send when it sets one, else the
    /// standard name, and a later change to the name does not rename it.
    /// </summary>
    [Fact]
    public async Task TheReportIsFiledUnderTheNameFrozenWithIt()
    {
        await using var harness = await Harness.CreateAsync(database);
        var standard = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var snapshot = standard.Generation!.Snapshot;
        var standardName = CaseReportDeliveryNaming.ReportName(
            snapshot.CaseReference,
            snapshot.Report.Vehicle.Registration,
            CaseReportDeliveryNaming.OutcomeWords(snapshot.Report.Outcome),
            0) + ".pdf";
        Assert.Equal(standardName, snapshot.ReportFileName);
        Assert.Equal(
            standardName,
            standard.Generation.Artifacts.Single(artifact => artifact.Kind == CaseReportArtifactKind.AssessmentReport).FileName);

        await harness.SetReportNamesAsync(new("{REG}_{case ref}_INSPECTION", null, "{REG}_AUDIT", null));
        await harness.Store.MarkStaleAsync(harness.CaseId, "test-material-change", CancellationToken.None);
        var named = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(operationKey: harness.Key("report-named")), CancellationToken.None);

        var expected = $"{snapshot.Report.Vehicle.Registration}_{snapshot.CaseReference}_INSPECTION.pdf";
        Assert.Equal(expected, named.Generation!.Snapshot.ReportFileName);
        Assert.Equal(
            expected,
            named.Generation.Artifacts.Single(artifact => artifact.Kind == CaseReportArtifactKind.AssessmentReport).FileName);

        await harness.SetReportNamesAsync(PrincipalReportNames.None);
        var reloaded = await harness.Store.GetAsync(
            harness.StaffActor, harness.CaseId, named.Generation.Id, CancellationToken.None);
        Assert.Equal(expected, reloaded!.Snapshot.ReportFileName);
    }

    /// <summary>
    /// A report generated without vehicle images freezes that with its
    /// generation and still pins every image, which its images document
    /// prints. A later change to the rules does not alter it.
    /// </summary>
    [Fact]
    public async Task AReportFrozenWithoutVehicleImagesKeepsThatAndStillPinsItsImages()
    {
        await using var harness = await Harness.CreateAsync(database);
        await harness.SetReportImagesRuleAsync(false);

        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, report.Outcome);
        Assert.False(report.Generation!.Snapshot.Report.IncludeVehicleImages);
        Assert.Equal(
            new[] { harness.CloseUp.VersionId, harness.Overview.VersionId }.Order(),
            report.Generation.Snapshot.Images.Select(image => image.VersionId).Order());

        await harness.SetReportImagesRuleAsync(true);

        var reloaded = await harness.Store.GetAsync(
            harness.StaffActor, harness.CaseId, report.Generation.Id, CancellationToken.None);
        Assert.False(reloaded!.Snapshot.Report.IncludeVehicleImages);
    }

    /// <summary>
    /// The same Generate replayed after the Principal's image rule changed
    /// would be a different report, so it is refused like any other change
    /// to the command, and the frozen generation is left as it was.
    /// </summary>
    [Fact]
    public async Task SameOperationKeyAfterThePrincipalsImageRuleChangedIsAConflict()
    {
        await using var harness = await Harness.CreateAsync(database);
        await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        await harness.SetReportImagesRuleAsync(false);

        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
                .ExecuteAsync(harness.Request(), CancellationToken.None));
        Assert.True((await harness.GenerationRowsAsync()).Single().Snapshot.Report.IncludeVehicleImages);
    }

    /// <summary>
    /// A generation frozen before the rule existed holds no member for it:
    /// it reads as a report with vehicle images, and replays as one.
    /// </summary>
    [Fact]
    public async Task ASnapshotFrozenBeforeTheImageRuleReadsAsIncludingVehicleImages()
    {
        await using var harness = await Harness.CreateAsync(database);
        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var generationId = report.Generation!.Id;
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var generation = await context.Set<CaseReportGenerationEntity>()
                .SingleAsync(item => item.Id == generationId);
            var frozen = JsonNode.Parse(generation.SnapshotJson)!.AsObject();
            Assert.True(frozen["report"]!.AsObject().Remove("includeVehicleImages"));
            generation.SnapshotJson = frozen.ToJsonString();
            await context.SaveChangesAsync();
        }

        var reloaded = await harness.Store.GetAsync(
            harness.StaffActor, harness.CaseId, generationId, CancellationToken.None);
        var replay = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.True(reloaded!.Snapshot.Report.IncludeVehicleImages);
        Assert.Equal(generationId, replay.Generation!.Id);
    }

    [Theory]
    [InlineData(CaseReportArtifactKind.FeeNote, CaseArtifactCustodyDisposition.Confirmed)]
    [InlineData(CaseReportArtifactKind.FeeNote, CaseArtifactCustodyDisposition.Pending)]
    [InlineData(CaseReportArtifactKind.RepairSpecification, CaseArtifactCustodyDisposition.Confirmed)]
    [InlineData(CaseReportArtifactKind.RepairSpecification, CaseArtifactCustodyDisposition.Pending)]
    [InlineData(CaseReportArtifactKind.ImagePack, CaseArtifactCustodyDisposition.Confirmed)]
    [InlineData(CaseReportArtifactKind.ImagePack, CaseArtifactCustodyDisposition.Pending)]
    public async Task EveryCompanionKindReplaysWithItsTargetGeneration(
        CaseReportArtifactKind kind, CaseArtifactCustodyDisposition disposition)
    {
        await using var harness = await Harness.CreateAsync(database);
        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var targetGenerationId = report.Generation!.Id;
        var operationKey = harness.Key($"case-report-{kind}-replay");
        var documents = kind == CaseReportArtifactKind.RepairSpecification
            ? Harness.RenderedRepairSpecificationDocumentsForTest()
            : null;

        var first = await harness.Generate(
                new RecordingCustody(harness) { Disposition = disposition }, new RecordingRenderer(harness),
                repairSpecificationDocuments: documents)
            .ExecuteAsync(
                harness.Request(kind, operationKey, targetGenerationId: targetGenerationId),
                CancellationToken.None);
        var replay = await harness.Generate(
                new RecordingCustody(harness), new RecordingRenderer(harness),
                repairSpecificationDocuments: documents)
            .ExecuteAsync(
                harness.Request(kind, operationKey, targetGenerationId: targetGenerationId),
                CancellationToken.None);

        var expectedOutcome = disposition == CaseArtifactCustodyDisposition.Confirmed
            ? CaseReportGenerationOutcome.Generated
            : CaseReportGenerationOutcome.Pending;
        Assert.Equal(expectedOutcome, first.Outcome);
        Assert.NotEqual(CaseReportGenerationOutcome.NotFound, replay.Outcome);
        Assert.Equal(targetGenerationId, replay.Generation!.Id);
        Assert.Equal(
            operationKey,
            Assert.Single(replay.Generation.Artifacts, artifact => artifact.Kind == kind).OperationKey);
    }

    [Fact]
    public async Task ASecondOperationKeyCannotClaimAnExistingFeeNoteArtifact()
    {
        await using var harness = await Harness.CreateAsync(database);
        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var originalFeeOperationKey = harness.Key("case-report-fee-original");
        await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(
                harness.Request(
                    CaseReportArtifactKind.FeeNote,
                    originalFeeOperationKey,
                    targetGenerationId: report.Generation!.Id),
                CancellationToken.None);

        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.Store.FreezeAsync(
                new(
                    harness.StaffActor,
                    harness.CaseId,
                    1,
                    harness.Lease.Token,
                    harness.Key("case-report-fee-unbound"),
                    CaseReportArtifactKind.FeeNote,
                    "Generate the fee note",
                    AssessmentReportContract.TemplateVersion,
                    "fake",
                    TargetGenerationId: report.Generation.Id),
                CancellationToken.None));

        var artifacts = await harness.ArtifactRowsAsync();
        Assert.Equal(2, artifacts.Count);
        Assert.Equal(
            originalFeeOperationKey,
            Assert.Single(
                artifacts,
                artifact => artifact.Kind == nameof(CaseReportArtifactKind.FeeNote)).OperationKey);
        Assert.DoesNotContain(
            artifacts, artifact => artifact.OperationKey == harness.Key("case-report-fee-unbound"));
    }

    [Fact]
    public async Task SeparateFeeNoteAttachesToTheNamedCurrentGenerationAndItsFrozenSnapshot()
    {
        await using var harness = await Harness.CreateAsync(database);
        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var reportDate = report.Generation!.Snapshot.ReportDate;

        var nextDay = Harness.StartUtc.AddDays(1);
        var nextDayStore = harness.StoreAt(nextDay);
        var nextDayLease = await new CaseEditLeases(
                new EfCaseWorkflowStore(harness.Factory, Harness.ClockAt(nextDay)), new InProcessCaseEditSignal())
            .ClaimAsync(
                new(harness.CaseId, 1, harness.StaffActor, harness.Key("lease-report-next-day")),
                CancellationToken.None);
        var feeNote = await harness.Generate(
                new RecordingCustody(harness), new RecordingRenderer(harness), store: nextDayStore)
            .ExecuteAsync(
                harness.Request(
                    CaseReportArtifactKind.FeeNote,
                    harness.Key("case-report-fee-next-day"),
                    targetGenerationId: report.Generation.Id) with
                {
                    LeaseToken = nextDayLease.Token,
                },
                CancellationToken.None);

        Assert.Equal(report.Generation.Id, feeNote.Generation!.Id);
        Assert.Equal(reportDate, feeNote.Generation.Snapshot.ReportDate);
        Assert.Equal(report.Generation.Snapshot.Report.AgreedFee, feeNote.Generation.Snapshot.Report.AgreedFee);
        Assert.Equal(2, feeNote.Generation.Artifacts.Count);
    }

    [Fact]
    public async Task SeparateFeeNoteIsRefusedWithoutACurrentGeneration()
    {
        await using var harness = await Harness.CreateAsync(database);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
                .ExecuteAsync(
                    harness.Request(
                        CaseReportArtifactKind.FeeNote,
                        harness.Key("case-report-fee-none"),
                        targetGenerationId: Guid.NewGuid()),
                    CancellationToken.None));

        Assert.Contains("unavailable", refusal.Message, StringComparison.Ordinal);
        Assert.Empty(await harness.GenerationRowsAsync());
    }

    [Fact]
    public async Task SeparateFeeNoteIsRefusedForAStaleCurrentGeneration()
    {
        await using var harness = await Harness.CreateAsync(database);
        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        await harness.Store.MarkStaleAsync(
            harness.CaseId, CaseReportStaleReasons.AssessmentFactsChanged, CancellationToken.None);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
                .ExecuteAsync(
                    harness.Request(
                        CaseReportArtifactKind.FeeNote,
                        harness.Key("case-report-fee-stale"),
                        targetGenerationId: report.Generation!.Id),
                    CancellationToken.None));

        Assert.Contains("unavailable", refusal.Message, StringComparison.Ordinal);
        Assert.Single((await harness.GenerationRowsAsync()).Single().Artifacts);
    }

    /// <summary>
    /// Once the Audit exists, the Inspection report already sent is generated
    /// again on its own work (operator, 2 October 2026): the new generation
    /// supersedes only the Inspection's earlier one, the Audit's stays
    /// current, and a companion addressed to the Audit's work never attaches
    /// to the Inspection's generation.
    /// </summary>
    [Fact]
    public async Task TheInspectionReportSentAfterTheAuditIsGeneratedAgainOnItsOwnWork()
    {
        await using var harness = await Harness.CreateAsync(database);
        var inspection = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var inspectionGenerationId = inspection.Generation!.Id;
        var auditWorkId = await GiveAuditAsync(harness.Factory, harness.CaseId, Harness.StartUtc);
        await LinkInspectionSentEvidenceAsync(harness.Factory, harness.CaseId, Harness.StartUtc);
        harness.UseAuditWork(auditWorkId);
        var audit = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(
                harness.Request(operationKey: harness.Key("case-report-audit")), CancellationToken.None);
        Assert.Equal(auditWorkId, audit.Generation!.WorkId);

        // The current work is the Audit's, so the Inspection's generation is not its own.
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
                .ExecuteAsync(
                    harness.Request(
                        CaseReportArtifactKind.FeeNote,
                        harness.Key("case-report-audit-fee"),
                        targetGenerationId: inspectionGenerationId),
                    CancellationToken.None));
        Assert.Equal("The case report generation is unavailable.", refusal.Message);

        // The next day's report is dated afresh, so it is new material.
        var nextDay = Harness.StartUtc.AddDays(1);
        var nextDayLease = await new CaseEditLeases(
                new EfCaseWorkflowStore(harness.Factory, Harness.ClockAt(nextDay)), new InProcessCaseEditSignal())
            .ClaimAsync(
                new(harness.CaseId, 1, harness.StaffActor, harness.Key("lease-inspection-again")),
                CancellationToken.None);
        var again = await harness.Generate(
                new RecordingCustody(harness), new RecordingRenderer(harness), store: harness.StoreAt(nextDay))
            .ExecuteAsync(
                harness.Request(operationKey: harness.Key("case-report-inspection-again")) with
                {
                    LeaseToken = nextDayLease.Token,
                    Work = CaseWorkSelector.Primary,
                },
                CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, again.Outcome);
        Assert.Equal(harness.CaseId, again.Generation!.WorkId);
        // Each work's rows are its own: the Inspection's earlier generation is
        // superseded within the primary work, the Audit's untouched.
        var inspectionRows = await harness.Store.ListAsync(
            harness.StaffActor, harness.CaseId, CaseWorkSelector.Primary, CancellationToken.None);
        Assert.Equal(again.Generation.Id, Assert.Single(inspectionRows, row => row.Id == inspectionGenerationId).SupersededById);
        var auditRow = Assert.Single(await harness.GenerationRowsAsync(), row => row.Id == audit.Generation.Id);
        Assert.Null(auditRow.SupersededById);
        Assert.Equal(CaseReportGenerationState.Confirmed, auditRow.State);
    }

    /// <summary>
    /// The Case as Create audit leaves it: an Audit work beside the primary
    /// one, which keeps the Inspection report's own facts. Returns the Audit
    /// work's id.
    /// </summary>
    internal static async Task<Guid> GiveAuditAsync(
        IDbContextFactory<PegasusDbContext> factory, Guid caseId, DateTimeOffset createdAtUtc)
    {
        await using var context = await factory.CreateDbContextAsync();
        var auditWorkId = Guid.NewGuid();
        context.CaseWorks.Add(new CaseWorkEntity
        {
            Id = auditWorkId,
            CaseId = caseId,
            Kind = CaseWorkKinds.Audit,
            CreatedAtUtc = createdAtUtc,
        });
        await context.SaveChangesAsync();
        return auditWorkId;
    }

    /// <summary>The Inspection report's Sent evidence, linked to the primary work once the Audit exists.</summary>
    internal static async Task LinkInspectionSentEvidenceAsync(
        IDbContextFactory<PegasusDbContext> factory, Guid caseId, DateTimeOffset sentAtUtc)
    {
        await using var context = await factory.CreateDbContextAsync();
        var evidenceId = Guid.NewGuid();
        var evidence = new CaseReportSentEvidenceEntity
        {
            Id = evidenceId,
            CaseId = caseId,
            MailboxIdentity = "reports@collision.example",
            SentFolderIdentity = "sent-items",
            ImmutableItemIdentity = $"inspection-item-{evidenceId:N}",
            InternetMessageIdentity = $"inspection-message-{evidenceId:N}",
            ConversationIdentity = $"inspection-conversation-{evidenceId:N}",
            ReplyChainIdentity = $"inspection-reply-chain-{evidenceId:N}",
            SourceOccurrenceIdentity = $"inspection-occurrence-{evidenceId:N}",
            SourceSha256 = new string('d', 64),
            MimeSha256 = new string('e', 64),
            SentAtUtc = sentAtUtc,
            DiscoveredAtUtc = sentAtUtc,
            DiscoveredByKind = nameof(ActorKind.SystemWorker),
            DiscoveredBySubjectId = "approved-mailbox-evidence-ingestion",
            RetentionOperationKey = $"retain-inspection-{evidenceId:N}",
            RetentionRequestHash = new string('f', 64),
            LinkedAtUtc = sentAtUtc,
            LinkedByKind = nameof(ActorKind.SystemWorker),
            LinkedBySubjectId = "approved-mailbox-sent-poll",
            LinkedByRolesJson = "[]",
        };
        context.CaseReportSentEvidence.Add(evidence);
        var primary = await context.CaseWorks.SingleAsync(work => work.Id == caseId);
        primary.ReportSentEvidenceId = evidenceId;
        primary.ReportSentEvidence = evidence;
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task CustodyConfirmationAloneIsNeverTheReadyTransition()
    {
        await using var harness = await Harness.CreateAsync(database);
        var custody = new RecordingCustody(harness)
        {
            // The object lands in custody as Confirmed, but the second
            // transaction never runs: the generation is not ready.
            Disposition = CaseArtifactCustodyDisposition.Pending,
            ConfirmedInCustody = true
        };

        await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(
            DocumentCustodyStatus.Confirmed,
            await harness.CustodyStatusAsync(custody.VersionId!.Value));
        Assert.Empty(await harness.ReadyEventsAsync());
        var artifact = Assert.Single((await harness.GenerationRowsAsync()).Single().Artifacts);
        Assert.Equal(CaseReportArtifactStatus.Pending, artifact.Status);
    }

    [Fact]
    public async Task AMaterialChangeBetweenFreezeAndConfirmLeavesTheGenerationConfirmedButStale()
    {
        await using var harness = await Harness.CreateAsync(database);
        var custody = new RecordingCustody(harness);
        var renderer = new RecordingRenderer(harness);
        renderer.Before = async () => await harness.Store.MarkStaleAsync(
            harness.CaseId, CaseReportStaleReasons.EstimateChanged, CancellationToken.None);

        var result = await harness.Generate(custody, renderer)
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, result.Outcome);
        var artifact = Assert.Single(result.Generation!.Artifacts);
        Assert.Equal(CaseReportArtifactStatus.Confirmed, artifact.Status);
        // Rendering finishing later never makes a staled generation current.
        Assert.Equal(CaseReportGenerationState.Stale, result.Generation.State);
        Assert.Empty(await harness.ReadyEventsAsync());
        Assert.Equal(1, await harness.ActionHistoryCountAsync("case_report_generation_stale"));

        // A second stale marking of an already stale generation is a no-op.
        Assert.Equal(
            0,
            await harness.Store.MarkStaleAsync(
                harness.CaseId, CaseReportStaleReasons.EstimateChanged, CancellationToken.None));
    }

    [Fact]
    public async Task RegeneratingAfterAMaterialChangeNeverRewritesThePriorGeneration()
    {
        await using var harness = await Harness.CreateAsync(database);
        var first = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var firstArtifact = Assert.Single(first.Generation!.Artifacts);

        await harness.Store.MarkStaleAsync(
            harness.CaseId, CaseReportStaleReasons.ValuationChanged, CancellationToken.None);
        harness.AcceptEngineerValue(5_250m);

        var second = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(
                harness.Request(CaseReportArtifactKind.AssessmentReport, harness.Key("case-report-2")),
                CancellationToken.None);

        Assert.NotEqual(first.Generation.Id, second.Generation!.Id);
        Assert.NotEqual(first.Generation.SnapshotHash, second.Generation.SnapshotHash);
        Assert.Equal(5_250m, second.Generation.Snapshot.EngineerValue);
        Assert.Equal(CaseReportGenerationState.Confirmed, second.Generation.State);

        // The prior generation keeps its bytes, its confirmed artifact and its
        // history exactly as issued; only its currency changed.
        var prior = await harness.Store.GetAsync(
            harness.StaffActor, harness.CaseId, first.Generation.Id, CancellationToken.None);
        Assert.Equal(CaseReportGenerationState.Stale, prior!.State);
        Assert.Equal(second.Generation.Id, prior.SupersededById);
        Assert.Equal(5_000m, prior.Snapshot.EngineerValue);
        var priorArtifact = Assert.Single(prior.Artifacts);
        Assert.Equal(firstArtifact.Id, priorArtifact.Id);
        Assert.Equal(firstArtifact.VersionId, priorArtifact.VersionId);
        Assert.Equal(firstArtifact.Sha256, priorArtifact.Sha256);
        Assert.Equal(CaseReportArtifactStatus.Confirmed, priorArtifact.Status);

        var current = await harness.Store.GetCurrentAsync(
            harness.StaffActor, harness.CaseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.Equal(second.Generation.Id, current!.Id);
        Assert.Equal(2, (await harness.Store.ListAsync(
            harness.StaffActor, harness.CaseId, CaseWorkSelector.Current, CancellationToken.None)).Count);
        Assert.Equal(2, (await harness.ReadyEventsAsync()).Count);
    }

    /// <summary>
    /// Issue #834: a missing printed fact, here a total loss with no salvage
    /// category, is named before the freeze writes anything. The confirmed
    /// current generation stays current and nothing supersedes it.
    /// </summary>
    [Fact]
    public async Task AMissingSalvageCategoryIsNamedBeforeAnyGenerationIsWritten()
    {
        await using var harness = await Harness.CreateAsync(database);
        var first = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var current = Assert.IsType<CaseReportGenerationRecord>(first.Generation);
        Assert.Equal(CaseReportGenerationState.Confirmed, current.State);
        var artifactsBefore = await harness.ArtifactRowsAsync();

        var recordedAt = new DateTimeOffset(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);
        AssessmentFieldValue Recorded(string path, string value) => new(
            path, value, ActorKind.Staff, "engineer-1", recordedAt);
        harness.ReviseAssessment(assessment => assessment with
        {
            Fields =
            [
                .. assessment.Fields.Where(field =>
                    field.Path != AssessmentVocabulary.Outcome
                    && field.Path != AssessmentVocabulary.SalvageCategory),
                Recorded(AssessmentVocabulary.Outcome, "total_loss"),
                Recorded(AssessmentVocabulary.SalvageValue, "500.00"),
            ],
        });
        var renderer = new RecordingRenderer(harness);
        var refused = await harness.Generate(new RecordingCustody(harness), renderer)
            .ExecuteAsync(
                harness.Request(operationKey: harness.Key("case-report-no-category")), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.NotReady, refused.Outcome);
        Assert.Null(refused.Generation);
        var reason = Assert.Single(refused.Reasons);
        Assert.Equal("Salvage category", reason.Requirement);
        Assert.Equal(AssessmentVocabulary.SalvageCategory, reason.Field);
        Assert.Empty(renderer.Kinds);
        var generation = Assert.Single(await harness.GenerationRowsAsync());
        Assert.Equal(current.Id, generation.Id);
        Assert.Equal(CaseReportGenerationState.Confirmed, generation.State);
        Assert.Null(generation.SupersededById);
        Assert.Equal(
            artifactsBefore.Select(row => row.Id),
            (await harness.ArtifactRowsAsync()).Select(row => row.Id));
    }

    [Fact]
    public async Task ReopeningAGeneratedArtifactReturnsTheConfirmedImmutableBytes()
    {
        await using var harness = await Harness.CreateAsync(database);
        var renderer = new RecordingRenderer(harness);
        var generated = await harness.Generate(new RecordingCustody(harness), renderer)
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var artifact = Assert.Single(generated.Generation!.Artifacts);

        await using var content = await harness.Store.OpenAsync(
            harness.StaffActor, harness.CaseId, generated.Generation.Id, artifact.Id,
            CancellationToken.None);
        using var buffer = new MemoryStream();
        await content.Content.CopyToAsync(buffer);

        Assert.Equal(renderer.Pdf, buffer.ToArray());
        Assert.Equal(renderer.Sha256, content.Sha256);
        Assert.Equal(artifact.VersionId, content.VersionId);

        // A generation the operator never generated has nothing to reopen.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.Store.OpenAsync(
                harness.StaffActor, harness.CaseId, generated.Generation.Id, Guid.NewGuid(),
                CancellationToken.None));
    }

    /// <summary>The report is filed and sent under one standard name (R740).</summary>
    [Fact]
    public async Task GeneratedReportIsFiledAndSentUnderOneName()
    {
        await using var harness = await Harness.CreateAsync(database);
        var generated = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var generation = generated.Generation!;

        var command = await harness.SendReportAsync(generation);

        var custodyAttachment = Assert.Single(command.Report.Artifacts);
        var expectedReportName = CaseReportDeliveryNaming.ReportName(
            generation.Snapshot.CaseReference,
            generation.Snapshot.Report.Vehicle.Registration,
            CaseReportDeliveryNaming.OutcomeWords(generation.Snapshot.Report.Outcome),
            0) + ".pdf";
        Assert.Equal(expectedReportName, custodyAttachment.FileName);
        Assert.Equal(expectedReportName, Assert.Single(command.Mail.Attachments).FileName);
    }

    /// <summary>
    /// A preview is a Case-history "viewed" event, never a
    /// "downloaded" one, and repeat previews the same staff day never grow
    /// the Case's history — the simplest idempotent rule the existing
    /// per-Case operation-key pattern already supports.
    /// </summary>
    [Fact]
    public async Task PreviewingTheDraftRecordsAViewedCaseHistoryEventOncePerStaffDay()
    {
        await using var harness = await Harness.CreateAsync(database);

        await harness.Store.RecordDraftPreviewedAsync(
            new(harness.StaffActor, harness.CaseId, CaseReportArtifactKind.AssessmentReport, Harness.StartUtc),
            CancellationToken.None);
        // A second preview a couple of hours later, same London day, same
        // staff member: a silent no-op, never a second row.
        await harness.Store.RecordDraftPreviewedAsync(
            new(harness.StaffActor, harness.CaseId, CaseReportArtifactKind.AssessmentReport,
                Harness.StartUtc.AddHours(2)),
            CancellationToken.None);

        var viewed = Assert.Single(await harness.CaseHistoryEventsAsync("case_report_draft_previewed"));
        Assert.Equal("Report draft previewed", viewed.Reason);
        Assert.Equal(nameof(ActorKind.Staff), viewed.ActorKind);
        Assert.Equal(harness.StaffActor.SubjectId, viewed.ActorSubjectId);
        // A view is never a Case mutation.
        Assert.Equal(viewed.BeforeVersion, viewed.AfterVersion);
        Assert.Empty(await harness.CaseHistoryEventsAsync("case_report_artifact_downloaded"));

        // A genuinely later London day is a genuinely new view.
        await harness.Store.RecordDraftPreviewedAsync(
            new(harness.StaffActor, harness.CaseId, CaseReportArtifactKind.AssessmentReport,
                Harness.StartUtc.AddDays(1)),
            CancellationToken.None);
        Assert.Equal(2, (await harness.CaseHistoryEventsAsync("case_report_draft_previewed")).Count);
    }

    [Fact]
    public async Task PreviewingAnEstimateDocumentRecordsOneEventPerVersionStaffAndDay()
    {
        await using var harness = await Harness.CreateAsync(database);
        var estimateId = Guid.NewGuid();

        await harness.Store.RecordPreviewedAsync(
            new(harness.StaffActor, harness.CaseId, estimateId, 3, Harness.StartUtc),
            CancellationToken.None);
        await harness.Store.RecordPreviewedAsync(
            new(harness.StaffActor, harness.CaseId, estimateId, 3, Harness.StartUtc.AddHours(2)),
            CancellationToken.None);

        var viewed = Assert.Single(await harness.CaseHistoryEventsAsync(
            CaseReportPresentationEvents.EstimateDocumentPreviewed));
        Assert.Equal("Estimate document previewed", viewed.Reason);
        Assert.Equal(viewed.BeforeVersion, viewed.AfterVersion);

        await harness.Store.RecordPreviewedAsync(
            new(harness.StaffActor, harness.CaseId, estimateId, 4, Harness.StartUtc.AddHours(3)),
            CancellationToken.None);
        Assert.Equal(2, (await harness.CaseHistoryEventsAsync(
            CaseReportPresentationEvents.EstimateDocumentPreviewed)).Count);
    }

    /// <summary>
    /// The presentation events' other half: reopening a confirmed generation artifact is a
    /// completed download, recorded distinctly from a preview view, and only
    /// once the bytes are actually reopened — never on a failed or refused
    /// attempt.
    /// </summary>
    [Fact]
    public async Task ReopeningAConfirmedArtifactRecordsADownloadedCaseHistoryEventOncePerStaffDay()
    {
        await using var harness = await Harness.CreateAsync(database);
        var generated = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var artifact = Assert.Single(generated.Generation!.Artifacts);

        var sameDayStore = harness.StoreAt(Harness.StartUtc.AddHours(1));
        await using (await sameDayStore.OpenAsync(
            harness.StaffActor, harness.CaseId, generated.Generation.Id, artifact.Id, CancellationToken.None))
        {
        }
        // A second download the same London day is a silent no-op.
        await using (await sameDayStore.OpenAsync(
            harness.StaffActor, harness.CaseId, generated.Generation.Id, artifact.Id, CancellationToken.None))
        {
        }

        var downloaded = Assert.Single(await harness.CaseHistoryEventsAsync("case_report_artifact_downloaded"));
        Assert.Equal("Report downloaded", downloaded.Reason);
        Assert.Equal(downloaded.BeforeVersion, downloaded.AfterVersion);
        Assert.Empty(await harness.CaseHistoryEventsAsync("case_report_draft_previewed"));

        // A genuinely later London day is a genuinely new download.
        var nextDayStore = harness.StoreAt(Harness.StartUtc.AddDays(1));
        await using (await nextDayStore.OpenAsync(
            harness.StaffActor, harness.CaseId, generated.Generation.Id, artifact.Id, CancellationToken.None))
        {
        }
        Assert.Equal(2, (await harness.CaseHistoryEventsAsync("case_report_artifact_downloaded")).Count);
    }

    /// <summary>
    /// The supersede of the prior generation is written before the new one.
    /// A freeze that fails when the new generation is saved rolls the
    /// supersede back with its transaction, so the prior generation is still
    /// the work's current one.
    /// </summary>
    [Fact]
    public async Task AFreezeThatFailsAtSaveLeavesThePriorGenerationCurrent()
    {
        await using var harness = await Harness.CreateAsync(database);
        var first = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        // New material without a stale marking: the second freeze supersedes.
        harness.AcceptEngineerValue(5_250m);
        var failing = harness.StoreUsing(new PooledDbContextFactory<PegasusDbContext>(
            new DbContextOptionsBuilder<PegasusDbContext>()
                .UseSqlServer(harness.ConnectionString)
                .AddInterceptors(new GenerationSaveFailureInterceptor())
                .Options));

        var thrown = await Assert.ThrowsAsync<DbUpdateException>(() => failing.FreezeAsync(
            new(harness.StaffActor, harness.CaseId, 1, harness.Lease.Token, harness.Key("case-report-2"),
                CaseReportArtifactKind.AssessmentReport, "Generate the report",
                AssessmentReportContract.TemplateVersion, "fake"), CancellationToken.None));

        Assert.IsType<InvalidOperationException>(thrown.InnerException);
        var generation = Assert.Single(await harness.GenerationRowsAsync());
        Assert.Equal(first.Generation!.Id, generation.Id);
        Assert.Equal(CaseReportGenerationState.Confirmed, generation.State);
        Assert.Null(generation.SupersededById);
    }

    private sealed class Harness : IAsyncDisposable
    {
        internal static readonly DateTimeOffset StartUtc = new(2026, 9, 6, 23, 30, 0, TimeSpan.Zero);
        internal static TimeProvider Clock => ClockAt(StartUtc);
        internal static TimeProvider ClockAt(DateTimeOffset now) => new FixedTimeProvider(now);

        private readonly ReportGenerationDatabase database;
        private readonly bool ownsDatabase;
        private readonly ReportGenerationDatabase.SeededCase seeded;
        private readonly FakeSnapshotSource snapshotSource;

        private Harness(
            ReportGenerationDatabase database,
            bool ownsDatabase,
            ReportGenerationDatabase.SeededCase seeded,
            ActionActor staffActor,
            CaseEditLease lease,
            FakeSnapshotSource snapshotSource,
            SeededDocument closeUp,
            SeededDocument overview,
            SeededDocument source,
            IReadOnlyList<SeededDocument> supporting)
        {
            this.database = database;
            this.ownsDatabase = ownsDatabase;
            this.seeded = seeded;
            this.snapshotSource = snapshotSource;
            Factory = database.Factory;
            CaseId = seeded.CaseId;
            StaffActor = staffActor;
            Lease = lease;
            CloseUp = closeUp;
            Overview = overview;
            Source = source;
            Supporting = supporting;
            var store = new EfCaseReportGenerationStore(
                Factory, snapshotSource, new FakeDocumentReader(this), new FixedTimeProvider(StartUtc));
            Store = store;
        }

        public PooledDbContextFactory<PegasusDbContext> Factory { get; }

        /// <summary>The database this harness works in, for a reader that needs its own context options.</summary>
        public string ConnectionString => database.ConnectionString;

        /// <summary>The database this harness works in, for a caller that runs as a database user.</summary>
        public LocalDbTestDatabase Database => database.Database;

        public Guid CaseId { get; }

        /// <summary>The sign-off Engineer the Case is signed off by.</summary>
        public Guid SignatoryId => database.SignatoryId;

        /// <summary>The operation key most tests generate with.</summary>
        public string OperationKey => Key("case-report-1");

        /// <summary>
        /// An operation key only this Case uses: generated artifacts keep their
        /// operation keys unique across the database, which the class shares.
        /// </summary>
        public string Key(string name) => KeyOf(CaseId, name);

        private static string KeyOf(Guid caseId, string name) => $"{name}:{caseId:N}";

        public ActionActor StaffActor { get; }

        public CaseEditLease Lease { get; }

        public SeededDocument CloseUp { get; }

        public SeededDocument Overview { get; }

        public SeededDocument Source { get; }

        /// <summary>The Supporting images the report prints after the Overview, in their order.</summary>
        public IReadOnlyList<SeededDocument> Supporting { get; }

        public EfCaseReportGenerationStore Store { get; }

        public List<string> Sequence { get; } = [];

        /// <summary>The bytes each fake custody object retained, by version.</summary>
        public Dictionary<Guid, byte[]> RetainedContent { get; } = [];

        /// <summary>The seeded evidence bytes a frozen image hash pins.</summary>
        public Dictionary<string, byte[]> EvidenceContent { get; } =
            new(StringComparer.Ordinal);

        /// <summary>A Case of its own in the class's database, removed when the harness is disposed.</summary>
        public static Task<Harness> CreateAsync(
            ReportGenerationDatabase database, StaffRole staffRole = StaffRole.Engineer, int supportingImages = 0) =>
            OpenAsync(database, ownsDatabase: false, staffRole, supportingImages);

        /// <summary>
        /// A Case in a database of its own, dropped when the harness is disposed,
        /// for a test that changes the signatory or adds a database user: both
        /// reach every Case in a database.
        /// </summary>
        public static async Task<Harness> CreateIsolatedAsync() => await OpenAsync(
            await ReportGenerationDatabase.CreateAsync(), ownsDatabase: true, StaffRole.Engineer, supportingImages: 0);

        private static async Task<Harness> OpenAsync(
            ReportGenerationDatabase database, bool ownsDatabase, StaffRole staffRole, int supportingImages)
        {
            var factory = database.Factory;
            ReportGenerationDatabase.SeededCase? seeded = null;
            try
            {
                seeded = await SeedCaseAsync(database);
                var caseId = seeded.CaseId;
                var staffActor = ActionActor.Staff(Guid.NewGuid(), [staffRole]);
                var closeUp = await SeedDocumentAsync(factory, caseId, "close-up.png", "image/png", 1);
                var overview = await SeedDocumentAsync(factory, caseId, "overview.png", "image/png", 2);
                var source = await SeedDocumentAsync(
                    factory, caseId, "instruction.pdf", "application/pdf", 3);
                var supporting = new List<SeededDocument>();
                for (var index = 0; index < supportingImages; index++)
                {
                    supporting.Add(await SeedDocumentAsync(
                        factory, caseId, $"supporting-{index + 1}.png", "image/png", (byte)(4 + index)));
                }
                var lease = await new CaseEditLeases(
                        new EfCaseWorkflowStore(factory, new FixedTimeProvider(StartUtc)), new InProcessCaseEditSignal())
                    .ClaimAsync(new(caseId, 1, staffActor, KeyOf(caseId, "lease-report")), CancellationToken.None);
                await using var sourceContext = await factory.CreateDbContextAsync();
                var confirmed = await EfAssessmentReportProjectionSource.ConfirmedDocumentsAsync(
                    sourceContext, caseId, default);
                var snapshotSource = new FakeSnapshotSource(
                    caseId, seeded.Reference, database.SignatoryId, closeUp, overview, supporting, confirmed);
                var harness = new Harness(
                    database, ownsDatabase, seeded, staffActor, lease, snapshotSource,
                    closeUp, overview, source, supporting);
                foreach (var document in new[] { closeUp, overview, source }.Concat(supporting))
                {
                    harness.EvidenceContent[document.Sha256] = document.Content;
                }

                return harness;
            }
            catch
            {
                await ReleaseAsync(database, ownsDatabase, seeded);
                throw;
            }
        }

        /// <summary>
        /// Ends a harness: drops the database it made, or removes its Case from
        /// the shared one. A Case that was never seeded left no rows to remove.
        /// </summary>
        private static Task ReleaseAsync(
            ReportGenerationDatabase database, bool ownsDatabase, ReportGenerationDatabase.SeededCase? seeded) =>
            ownsDatabase ? database.DisposeAsync()
            : seeded is null ? Task.CompletedTask
            : database.RemoveCaseAsync(seeded);

        public GenerateCaseReport Generate(
            RecordingCustody custody,
            IAssessmentReportRenderer renderer,
            RecordingCustodyStatus? custodyStatus = null,
            EfCaseReportGenerationStore? store = null,
            IRenderCaseEstimateDocument? repairSpecificationDocuments = null,
            ICaseReportContentSource? content = null) => new(
            new RecordingStore(store ?? Store, Sequence),
            content ?? new FakeContentSource(this),
            renderer,
            repairSpecificationDocuments ?? new RefusingRepairSpecificationDocuments(),
                custody,
                custodyStatus ?? new RecordingCustodyStatus(),
                new FixedTimeProvider(StartUtc));

        /// <summary>
        /// The generator over custody itself, which both retains the file and
        /// answers what it holds, clocked as the store is.
        /// </summary>
        public GenerateCaseReport GenerateThroughCustody(
            EfCaseArtifactCustody custody, IAssessmentReportRenderer renderer) => new(
            new RecordingStore(Store, Sequence),
            new FakeContentSource(this),
            renderer,
            new RefusingRepairSpecificationDocuments(),
            custody,
            custody,
            new FixedTimeProvider(StartUtc));

        /// <summary>The harness's own services, for the local stores custody writes to.</summary>
        public AsyncServiceScope CreateScope() => database.CreateAsyncScope();

        /// <summary>Records the Case's Box folder, as creating it does.</summary>
        public async Task RecordCaseFolderAsync(string rootRemoteId)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var @case = await context.Cases.SingleAsync(item => item.Id == CaseId);
            @case.CustodyRootRemoteId = rootRemoteId;
            await context.SaveChangesAsync();
        }

        public static IRenderCaseEstimateDocument RenderedRepairSpecificationDocumentsForTest() =>
            new RenderedRepairSpecificationDocuments();

        public EfCaseReportGenerationStore StoreUsing(IDbContextFactory<PegasusDbContext> factory) =>
            new(factory, snapshotSource, new FakeDocumentReader(this), Clock);

        /// <summary>The same store, clocked to a different instant — used to
        /// prove the view/download day boundary genuinely depends on the
        /// London calendar day, not call count.</summary>
        public EfCaseReportGenerationStore StoreAt(DateTimeOffset now) =>
            new(Factory, snapshotSource, new FakeDocumentReader(this), new FixedTimeProvider(now));

        public GenerateCaseReportRequest Request(
            CaseReportArtifactKind kind = CaseReportArtifactKind.AssessmentReport,
            string? operationKey = null,
            Guid? targetGenerationId = null) => new(
                StaffActor, CaseId, 1, Lease.Token, operationKey ?? OperationKey, kind,
                "Generate the immutable case report", targetGenerationId);

        public async Task<CaseReportFreezeInputs?> ReadSourceAsync(IGetAssessmentWorkspace? workspace = null)
        {
            await using var scope = database.CreateAsyncScope();
            var services = scope.ServiceProvider;
            ICaseReportSnapshotSource source = new EfAssessmentReportProjectionSource(Factory,
                workspace ?? new FakeGetAssessmentWorkspace(AssessmentWorkspaceTestData.Create(
                    AssessmentReportDraftWebTests.FullAssessmentProjection(CaseId) with { CaseVersion = 1 })),
                services.GetRequiredService<IDocumentContentStore>(),
                services.GetRequiredService<IStaffAccountQueries>(),
                services.GetRequiredService<ICaseAssetPreparationQueries>(),
                services.GetRequiredService<IListAppliedValuations>());
            return await source.GetAsync(CaseId, StaffActor, CaseWorkSelector.Current, reuse: null, default);
        }

        /// <summary>
        /// The store over the real projection source and the real staff
        /// queries, both on a factory of their own that
        /// <paramref name="counter"/> counts, with the real content source a
        /// generation composes through. Only the workspace, the prepared
        /// images and the applied valuation are fixtures, the last two the
        /// fake source's. The Case is first assigned to the signatory, which a
        /// ready report needs (#1213 ruling 3).
        /// </summary>
        public async Task<RealSource> StoreOverRealSourceAsync(
            SqlStatementCounter counter, IGetAssessmentWorkspace? workspace = null)
        {
            await using (var context = await Factory.CreateDbContextAsync())
            {
                var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == CaseId);
                workflow.AssignedEngineerId = SignatoryId;
                await context.SaveChangesAsync();
            }

            var factory = new PooledDbContextFactory<PegasusDbContext>(
                new DbContextOptionsBuilder<PegasusDbContext>()
                    .UseSqlServer(ConnectionString)
                    .AddInterceptors(counter)
                    .Options);
            var staff = new EfStaffAccountQueries(
                factory, new StaffAccountReads(new MemoryCache(new MemoryCacheOptions())));
            // The content store is a singleton, so it outlives the scope.
            await using var scope = database.CreateAsyncScope();
            var source = new EfAssessmentReportProjectionSource(
                factory,
                workspace ?? new FakeGetAssessmentWorkspace(AssessmentWorkspaceTestData.Create(
                        AssessmentReportDraftWebTests.FullAssessmentProjection(CaseId) with { CaseVersion = 1 })
                    with { CurrentSpecification = AssessmentReportDraftWebTests.CurrentEstimate() }),
                scope.ServiceProvider.GetRequiredService<IDocumentContentStore>(),
                staff,
                snapshotSource,
                snapshotSource);
            return new(
                new EfCaseReportGenerationStore(factory, source, new FakeDocumentReader(this), Clock),
                source,
                staff,
                new EfCaseReportContentSource(new FakeDocumentReader(this), staff));
        }

        /// <summary>
        /// Moves the Case's version as system work does: the version advances
        /// and the staff edit lease stands (operator, 6 October 2026).
        /// </summary>
        public async Task MoveVersionAsSystemWorkAsync()
        {
            await using var context = await Factory.CreateDbContextAsync();
            var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == CaseId);
            workflow.Version++;
            await context.SaveChangesAsync();
        }

        public async Task AddSourceAsync()
        {
            await using var scope = database.CreateAsyncScope();
            var store = new EfDocumentCustodyStore(Factory,
                scope.ServiceProvider.GetRequiredService<IDocumentContentStore>(), Clock,
                scope.ServiceProvider.GetRequiredService<IMemoryCache>());
            await store.ExecuteAsync(new(CaseId, "instruction.pdf", "application/pdf", Source.Content,
                DocumentSemanticRole.Instruction, DocumentSource.Generated, "source-change",
                StaffActor, Key("add-source"), 1, Lease.Token), default);
        }

        public async Task RemoveSourceAsync()
        {
            await using var scope = database.CreateAsyncScope();
            ILogicallyRemoveDocument store = new EfDocumentCustodyStore(Factory,
                scope.ServiceProvider.GetRequiredService<IDocumentContentStore>(), Clock,
                scope.ServiceProvider.GetRequiredService<IMemoryCache>());
            await store.ExecuteAsync(new(CaseId, Source.OccurrenceId, StaffActor,
                "Incorrect source document", Key("remove-source"), 1, Lease.Token), default);
        }

        public async Task ChangeConfirmedSourceAsync(string change)
        {
            if (change == "add")
            {
                await SeedDocumentAsync(Factory, CaseId, "instruction.pdf", "application/pdf", 3);
                return;
            }
            await using var context = await Factory.CreateDbContextAsync();
            if (change == "occurrence")
            {
                // No source facts change: an equal-sized census with a new
                // occurrence must still differ from the captured membership.
                await context.Set<DocumentOccurrenceEntity>()
                    .Where(item => item.Id == Source.OccurrenceId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Id, Guid.NewGuid()));
                return;
            }
            var version = await context.Set<DocumentVersionEntity>()
                .SingleAsync(item => item.Id == Source.VersionId);
            switch (change)
            {
                case "remove": version.IsLogicallyRemoved = true; break;
                case "version": version.Version++; break;
                case "hash": version.Sha256 = CloseUp.Sha256; break;
                case "name": version.FileName = "close-up.png"; break;
                case "media": version.MediaType = "image/png"; break;
                case "length": version.ContentLength++; break;
                case "box-file": version.BoxFileId = $"box-file-{CloseUp.VersionId:N}"; break;
                case "box-version": version.BoxVersionId = $"box-version-{CloseUp.VersionId:N}"; break;
                case "current": version.IsCurrent = false; break;
                case "custody": version.CustodyStatus = DocumentCustodyStatus.Pending; break;
                default: throw new ArgumentOutOfRangeException(nameof(change));
            }
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// One send of the generation's report through the real use case over
        /// this database; the transport records the command it was handed.
        /// </summary>
        public async Task<StaffReportSendCommand> SendReportAsync(
            CaseReportGenerationRecord generation,
            long expectedCaseVersion = 1,
            string? leaseToken = null)
        {
            var send = new RecordingReportSend();
            // A Principal whose rules send every report to one fixed address;
            // the form is drawn from them.
            var facts = new ReportDispatchFacts(
                generation.Snapshot.CaseReference,
                null,
                null,
                PrincipalReportSendingRules.Default with
                {
                    EveryCase =
                    [
                        .. PrincipalReportSendingRules.Default.EveryCase,
                        new(ReportSendingActionKind.SendTo, Addresses: ["digital@collisionengineers.co.uk"])
                    ]
                },
                null);
            await new SendCaseReport(
                    Store,
                    new EfCaseReportSendHistoryQueries(Factory),
                    new FixedDispatchFacts(facts),
                    new ResolveCaseMailSender(
                        new EfCaseInstructionEmailQueries(Factory),
                        new FixedApprovedMailboxes(TestMailbox)),
                    send,
                    Clock)
                .ExecuteAsync(
                    new(
                        StaffActor,
                        CaseId,
                        expectedCaseVersion,
                        leaseToken ?? Lease.Token,
                        generation.Id,
                        generation.Version,
                        Key("send-report"),
                        "Please find attached our report.",
                        // An uploaded Case has no instruction email to reply
                        // to: the report is new from the chosen mailbox.
                        FromMailboxId: TestMailbox.Id,
                        DispatchFingerprint: facts.Fingerprint),
                    default);
            return Assert.Single(send.Commands);
        }

        /// <summary>
        /// These generations never ask for the repair specification document,
        /// so a call here would mean a companion took the snapshot route.
        /// </summary>
        private sealed class RefusingRepairSpecificationDocuments : IRenderCaseEstimateDocument
        {
            public Task<RenderCaseEstimateDocumentResult> ExecuteAsync(
                Guid caseId, Guid estimateId, ActionActor actor, CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("No repair specification document was expected.");
        }

        private sealed class RenderedRepairSpecificationDocuments : IRenderCaseEstimateDocument
        {
            public Task<RenderCaseEstimateDocumentResult> ExecuteAsync(
                Guid caseId, Guid estimateId, ActionActor actor, CancellationToken cancellationToken = default) =>
                Task.FromResult(new RenderCaseEstimateDocumentResult(
                    RenderCaseEstimateDocumentOutcome.Rendered,
                    new RenderedReportArtifact(
                        "RPT31001_estimate.pdf", [1, 2, 3], 1,
                        Convert.ToHexStringLower(SHA256.HashData([1, 2, 3])),
                        AssessmentReportContract.TemplateVersion, "fake"),
                    [], 2));
        }

        public Task RequireDeliveryReadyAsync(StaffReportSendCommand command) =>
            new ReportSendReadiness(Store, new EfFiledEstimateAttachmentQueries(Factory))
                .RequireReadyAsync(command.Report, default);

        public async Task ChangeSignatoryAsync(string change)
        {
            await using var scope = database.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
            var store = new EfStaffAccountAdministration(context,
                scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>(),
                scope.ServiceProvider.GetRequiredService<StaffAccountReads>(), Clock);
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
            var account = await context.Users.SingleAsync(
                item => item.Id == SignatoryId);
            if (change == "disabled")
            {
                await store.DisableAsync(new(
                    actor, account.Id, "Unavailable", Key("change-signatory"), account.Version), default);
            }
            else if (change == "role")
            {
                await store.UpdateAsync(new(
                    actor, account.Id, StaffRole.User, true, "Ed Mawdsley", "ATA VDA AQP", SignatureBytes, true,
                    Key("change-signatory"), account.Version), default);
            }
            else
            {
                await store.UpdateAsync(new(
                    actor, account.Id, StaffRole.Engineer, change != "eligibility",
                    change == "name" ? "Ed M" : "Ed Mawdsley",
                    change == "qualifications" ? "ATA VDA" : "ATA VDA AQP",
                    change == "signature" ? EvidenceBytesOf(7) : SignatureBytes,
                    change != "eligibility", Key("change-signatory"),
                    account.Version), default);
            }
        }

        public async Task<Guid> AddEligibleSignatoryAsync()
        {
            await using var context = await Factory.CreateDbContextAsync();
            var engineerRole = await context.Roles.SingleAsync(role => role.NormalizedName == "ENGINEER");
            var staffId = Guid.NewGuid();
            context.Users.Add(new PegasusIdentityUser
            {
                Id = staffId,
                UserName = $"report-signatory-{staffId:N}",
                NormalizedUserName = $"REPORT-SIGNATORY-{staffId:N}",
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                IsEnabled = true,
                IsSignOffEngineer = true,
                IsDefaultSignOffEngineer = false,
                SignOffPrintedName = "Jo Engineer",
                SignOffQualifications = "ATA VDA",
                SignOffSignature = SignatureBytes,
                SignOffSignatureDigest = Convert.ToHexStringLower(SHA256.HashData(SignatureBytes)),
            });
            context.UserRoles.Add(new IdentityUserRole<Guid>
            {
                UserId = staffId,
                RoleId = engineerRole.Id,
            });
            await context.SaveChangesAsync();
            return staffId;
        }

        public async Task MarkReviewReadyAsync()
        {
            await using var context = await Factory.CreateDbContextAsync();
            var @case = await context.Cases.SingleAsync(item => item.Id == CaseId);
            @case.InstructionComplete = true;
            @case.ImagesComplete = true;
            await context.SaveChangesAsync();
        }

        /// <summary>Accepts a different Engineer's Value, a material change.</summary>
        public void AcceptEngineerValue(decimal value) => snapshotSource.AcceptEngineerValue(value);

        /// <summary>The current work's inputs are read from the Audit's work from now on.</summary>
        public void UseAuditWork(Guid auditWorkId) => snapshotSource.AuditWorkId = auditWorkId;

        /// <summary>Types the three boxes with no basis card, so no applied valuation row exists.</summary>
        public void TypeValues(decimal retail, decimal trade, decimal engineer) =>
            snapshotSource.TypeValues(retail, trade, engineer);

        /// <summary>Records a revised assessment, which every later freeze reads.</summary>
        public void ReviseAssessment(Func<CaseAssessmentProjection, CaseAssessmentProjection> revise) =>
            snapshotSource.TransformAssessment(revise);

        /// <summary>
        /// Stores the Principal's report sending rules with the given images
        /// rule, and has every later freeze read it, as the real source does.
        /// </summary>
        /// <summary>Stores the Principal's report names, which every later freeze reads.</summary>
        public async Task SetReportNamesAsync(PrincipalReportNames names)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var principalId = await context.Cases
                .Where(item => item.Id == CaseId)
                .Select(item => item.PrincipalId)
                .SingleAsync();
            await context.Set<PrincipalEntity>()
                .Where(item => item.Id == principalId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.InspectionReportNameFirst, names.InspectionFirst)
                    .SetProperty(item => item.InspectionReportNameResend, names.InspectionResend)
                    .SetProperty(item => item.AuditReportNameFirst, names.AuditFirst)
                    .SetProperty(item => item.AuditReportNameResend, names.AuditResend));
        }

        /// <summary>Sets the Principal's fee note VAT number; null is the standard number.</summary>
        public async Task SetFeeNoteVatNumberAsync(string? vatNumber)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var principalId = await context.Cases
                .Where(item => item.Id == CaseId)
                .Select(item => item.PrincipalId)
                .SingleAsync();
            await context.Set<PrincipalEntity>()
                .Where(item => item.Id == principalId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.FeeNoteVatNumber, vatNumber));
            snapshotSource.VatNumber = vatNumber;
        }

        /// <summary>
        /// Sets whether Generate report also makes the Principal's own invoice
        /// and the two values it prints (R1148, R1162). Every later freeze
        /// through the fake source carries <paramref name="frozen"/>, as the
        /// real source builds it for a Principal that has the invoice.
        /// </summary>
        public async Task SetPrincipalInvoiceAsync(
            string code, string? vatNumber, string? paymentTerms, UlaInvoiceSnapshot? frozen = null)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var principalId = await context.Cases
                .Where(item => item.Id == CaseId)
                .Select(item => item.PrincipalId)
                .SingleAsync();
            await context.Set<PrincipalEntity>()
                .Where(item => item.Id == principalId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.PrincipalInvoice, code)
                    .SetProperty(item => item.InvoiceVatNumber, vatNumber)
                    .SetProperty(item => item.InvoicePaymentTerms, paymentTerms));
            snapshotSource.PrincipalInvoice = frozen;
        }

        /// <summary>
        /// Stores the Principal organisation's address and postcode, which the
        /// Principal's own invoice prints under its name; returns the name.
        /// </summary>
        public async Task<string> SetPrincipalAddressAsync(string? address, string? postcode)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var organization = await context.Cases
                .Where(item => item.Id == CaseId)
                .Select(item => new { item.Principal.OrganizationId, item.Principal.Organization.Name })
                .SingleAsync();
            await context.Set<OrganizationEntity>()
                .Where(item => item.Id == organization.OrganizationId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Address, address)
                    .SetProperty(item => item.Postcode, postcode));
            return organization.Name;
        }

        public async Task SetReportImagesRuleAsync(bool reportImages)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var principalId = await context.Cases
                .Where(item => item.Id == CaseId)
                .Select(item => item.PrincipalId)
                .SingleAsync();
            var principal = await context.Set<PrincipalEntity>().SingleAsync(item => item.Id == principalId);
            principal.ReportImages = reportImages;
            await context.SaveChangesAsync();
            snapshotSource.IncludeVehicleImages = reportImages;
        }

        public static byte[] SignatureBytes => ReportGenerationDatabase.SignatureBytes;

        public ReportImageEvidence[] RehydratedPhotos(
            CaseReportGenerationSnapshot snapshot) => snapshot.Images
                .Select(image => new ReportImageEvidence(
                    $"{image.OccurrenceId:D}.png", image.ContentType,
                    ReportImageContent.Opened(_ => Task.FromResult(EvidenceContent[image.Sha256])),
                    image.Sha256, image.Role, image.Order, image.Rotation, image.Crop,
                    image.OccurrenceId, image.VersionId, image.BoxFileId, image.BoxVersionId,
                    image.FullPage))
                .ToArray();

        private readonly Dictionary<Guid, Guid> occurrences = [];

        public async Task<SeededDocument> RetainArtifactAsync(
            CaseArtifactCustodyRequest request, byte[] content, bool confirmed)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var document = await context.Set<CaseDocumentEntity>().SingleOrDefaultAsync(
                item => item.CaseId == CaseId
                    && item.SourceOccurrenceIdentity == request.OccurrenceIdentity);
            if (document is null)
            {
                document = new CaseDocumentEntity
                {
                    Id = Guid.NewGuid(),
                    CaseId = CaseId,
                    Ordinal = 1 + await context.Set<CaseDocumentEntity>()
                        .Where(item => item.CaseId == CaseId)
                        .Select(item => (int?)item.Ordinal)
                        .MaxAsync() ?? 1,
                    SourceOccurrenceIdentity = request.OccurrenceIdentity
                };
                context.Add(document);
            }

            var version = await context.Set<DocumentVersionEntity>().SingleOrDefaultAsync(
                item => item.DocumentId == document.Id && item.Sha256 == request.Sha256);
            if (version is null)
            {
                version = new DocumentVersionEntity
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    Version = 1,
                    FileName = request.FileName,
                    MediaType = request.MediaType,
                    ContentLength = request.ContentLength,
                    Sha256 = request.Sha256,
                    CreatedAtUtc = StartUtc,
                    CreatedBy = $"Staff:{StaffActor.SubjectId}",
                    IsCurrent = true
                };
                context.Add(version);
            }

            Apply(version, confirmed);
            await context.SaveChangesAsync();
            RetainedContent[version.Id] = content;
            // One occurrence per retained document, stable across retries, the
            // way the real custody adapter reuses the occurrence it minted.
            if (!occurrences.TryGetValue(document.Id, out var occurrenceId))
            {
                occurrences[document.Id] = occurrenceId = Guid.NewGuid();
            }
            return new(document.Id, version.Id, request.Sha256, content) { OccurrenceId = occurrenceId };
        }

        /// <summary>
        /// Writes the occurrence custody keeps for a retained file, under the
        /// operation key the report's row carries, recorded at the given time.
        /// The fake custody keeps its occurrences in memory; the Worker's
        /// settle pass finds a filed report through this row.
        /// </summary>
        public async Task RecordCustodyOccurrenceAsync(
            RecordingCustody custody, string operationKey, DateTimeOffset? recordedAtUtc = null)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var version = await context.Set<DocumentVersionEntity>().SingleAsync(
                item => item.Id == custody.VersionId);
            var document = await context.Set<CaseDocumentEntity>().SingleAsync(
                item => item.Id == version.DocumentId);
            version.CreatedAtUtc = recordedAtUtc ?? StartUtc;
            context.Add(new DocumentOccurrenceEntity
            {
                Id = custody.OccurrenceId!.Value,
                CaseId = CaseId,
                DocumentId = document.Id,
                VersionId = version.Id,
                Ordinal = document.Ordinal,
                SemanticRole = DocumentSemanticRole.OriginalSource,
                Source = DocumentSource.Generated,
                SourceOccurrenceIdentity = document.SourceOccurrenceIdentity,
                RecordedAtUtc = version.CreatedAtUtc,
                OperationKey = operationKey,
                InReport = false
            });
            await context.SaveChangesAsync();
        }

        /// <summary>The Worker's settle pass, clocked to the given instant.</summary>
        public EfSettleFiledCaseReportArtifacts SettleAt(DateTimeOffset now) =>
            new(Factory, new FixedTimeProvider(now));

        public async Task<IReadOnlyList<ActionHistoryEntity>> ArtifactConfirmedEventsAsync()
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await context.ActionHistory.AsNoTracking()
                .Where(item => item.AggregateType == "case"
                    && item.AggregateId == CaseId.ToString("D")
                    && item.EventKind == EfCaseReportGenerationStore.ArtifactConfirmedEventKind)
                .ToArrayAsync();
        }

        public async Task ConfirmCustodyObjectAsync(Guid versionId)
        {
            await using var context = await Factory.CreateDbContextAsync();
            var version = await context.Set<DocumentVersionEntity>().SingleAsync(
                item => item.Id == versionId);
            Apply(version, confirmed: true);
            await context.SaveChangesAsync();
        }

        public async Task<DocumentCustodyStatus> CustodyStatusAsync(Guid versionId)
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await context.Set<DocumentVersionEntity>().AsNoTracking()
                .Where(item => item.Id == versionId)
                .Select(item => item.CustodyStatus)
                .SingleAsync();
        }

        public async Task<IReadOnlyList<ArtifactRow>> ArtifactRowsAsync()
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await (
                from artifact in context.Set<GeneratedCaseArtifactEntity>().AsNoTracking()
                join generation in context.Set<CaseReportGenerationEntity>().AsNoTracking()
                    on artifact.GenerationId equals generation.Id
                where generation.CaseId == CaseId
                select new ArtifactRow(artifact.Id, artifact.Kind, artifact.State, artifact.OperationKey))
                .ToArrayAsync();
        }

        public async Task<IReadOnlyList<CaseReportGenerationRecord>> GenerationRowsAsync() =>
            await Store.ListAsync(StaffActor, CaseId, CaseWorkSelector.Current, CancellationToken.None);

        public async Task<IReadOnlyList<ActionHistoryEntity>> ReadyEventsAsync()
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await context.ActionHistory.AsNoTracking()
                .Where(item => item.AggregateType == "case"
                    && item.AggregateId == CaseId.ToString("D")
                    && item.EventKind == "case_report_generation_ready")
                .ToArrayAsync();
        }

        public async Task<int> ActionHistoryCountAsync(string eventKind)
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await context.ActionHistory.AsNoTracking()
                .CountAsync(item => item.AggregateType == "case"
                    && item.AggregateId == CaseId.ToString("D")
                    && item.EventKind == eventKind);
        }

        /// <summary>
        /// The Case-history events a preview view or artifact download
        /// records — <c>CaseWorkflowEvents</c>, the table the
        /// Case's own Notes/history panel reads, not <c>ActionHistory</c>.
        /// </summary>
        public async Task<IReadOnlyList<CaseWorkflowEventEntity>> CaseHistoryEventsAsync(string eventType)
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await context.Set<CaseWorkflowEventEntity>().AsNoTracking()
                .Where(item => item.CaseId == CaseId && item.EventType == eventType)
                .ToArrayAsync();
        }

        public async Task<string?> StaleReasonAsync()
        {
            await using var context = await Factory.CreateDbContextAsync();
            return await context.ActionHistory
                .Where(item => item.AggregateId == CaseId.ToString("D")
                    && item.EventKind == EfCaseReportGenerationStore.StaleEventKind)
                .Select(item => item.Reason).SingleAsync();
        }

        public async ValueTask DisposeAsync() => await ReleaseAsync(database, ownsDatabase, seeded);

        internal static byte[] EvidenceBytesOf(byte seed) => [137, 80, 78, 71, seed, 1, 2, 3];

        private static void Apply(DocumentVersionEntity version, bool confirmed)
        {
            version.CustodyStatus = confirmed
                ? DocumentCustodyStatus.Confirmed
                : DocumentCustodyStatus.Pending;
            version.BoxFileId = confirmed ? $"box-file-{version.Id:N}" : null;
            version.BoxVersionId = confirmed ? $"box-version-{version.Id:N}" : null;
            version.PendingContentStorageKey = confirmed ? null : $"pending/{version.Id:N}";
        }

        private static async Task<SeededDocument> SeedDocumentAsync(
            PooledDbContextFactory<PegasusDbContext> factory,
            Guid caseId,
            string fileName,
            string mediaType,
            byte seed)
        {
            var content = EvidenceBytesOf(seed);
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(content));
            await using var context = await factory.CreateDbContextAsync();
            var documentId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var occurrenceId = Guid.NewGuid();
            var ordinal = 1 + await context.Set<CaseDocumentEntity>()
                .Where(item => item.CaseId == caseId)
                .Select(item => (int?)item.Ordinal)
                .MaxAsync() ?? 1;
            var version = new DocumentVersionEntity
            {
                Id = versionId,
                DocumentId = documentId,
                Version = 1,
                FileName = fileName,
                MediaType = mediaType,
                ContentLength = content.LongLength,
                Sha256 = sha256,
                CreatedAtUtc = StartUtc,
                CreatedBy = "Staff:test",
                IsCurrent = true
            };
            Apply(version, confirmed: true);
            context.AddRange(
                new CaseDocumentEntity
                {
                    Id = documentId,
                    CaseId = caseId,
                    Ordinal = ordinal,
                    SourceOccurrenceIdentity = $"report-fixture:{occurrenceId:N}"
                },
                version,
                new DocumentOccurrenceEntity
                {
                    Id = occurrenceId,
                    CaseId = caseId,
                    DocumentId = documentId,
                    VersionId = versionId,
                    SemanticRole = mediaType == "application/pdf"
                        ? DocumentSemanticRole.Instruction
                        : DocumentSemanticRole.Image,
                    Source = DocumentSource.StaffUpload,
                    SourceOccurrenceIdentity = $"report-fixture:{occurrenceId:N}",
                    RecordedAtUtc = StartUtc,
                    OperationKey = $"seed:{occurrenceId:N}",
                    InReport = false
                });
            await context.SaveChangesAsync();
            return new(documentId, versionId, sha256, content) { OccurrenceId = occurrenceId };
        }

        /// <summary>
        /// A Case of its own. The Principal's code and name, the Case's reference
        /// and its sequence number are each unique across the database, so they
        /// come from the database's counter.
        /// </summary>
        private static async Task<ReportGenerationDatabase.SeededCase> SeedCaseAsync(
            ReportGenerationDatabase database)
        {
            await using var context = await database.Factory.CreateDbContextAsync();
            var number = database.NextCaseNumber();
            var reference = $"RPT31{number:000}";
            var organizationId = Guid.NewGuid();
            var lineageId = Guid.NewGuid();
            var principalId = Guid.NewGuid();
            var receiptId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            context.AddRange(
                new OrganizationEntity { Role = "principal", Id = organizationId, Name = $"Report generation test {reference}", Version = 0 },
                new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = StartUtc },
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
                    SourceFileName = "report-origin.pdf",
                    MediaType = "application/pdf",
                    SourceLength = 1,
                    SourceHash = new string('0', 64),
                    SourceChannel = "manual_upload",
                    ExternalReceiptToken = $"report:{receiptId:N}",
                    ReceivedAtUtc = StartUtc,
                    ProcessedAtUtc = StartUtc,
                    SourceReaderKey = "report-test",
                    SourceReaderVersion = "1",
                    Version = 0,
                    Decision = "case_created",
                    DecisionReason = "Report generation test",
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
                    Sequence = number,
                    Reference = reference,
                    Type = "inspection",
                    InitialState = "NotReady",
                    CustodyState = "confirmed",
                    OriginIntakeReceiptId = receiptId,
                    CreatedAtUtc = StartUtc,
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                },
                new CaseWorkflowEntity
                {
                    CaseId = caseId,
                    State = "ReportPreparation",
                    Version = 1,
                    ConcurrencyToken = Guid.NewGuid()
                });
            await context.SaveChangesAsync();
            return new(caseId, reference, organizationId, lineageId, principalId, receiptId);
        }

        internal sealed record ArtifactRow(Guid Id, string Kind, string State, string OperationKey);

        /// <summary>
        /// The store over the real projection source, the source itself, the
        /// staff queries both read through, and the content source over them.
        /// </summary>
        internal sealed record RealSource(
            EfCaseReportGenerationStore Store,
            ICaseReportSnapshotSource Source,
            IStaffAccountQueries Staff,
            ICaseReportContentSource Content)
        {
            /// <summary>
            /// Reads the sign-off list and <paramref name="signatoryId"/>'s
            /// signature once, as any earlier page did, so later reads of them
            /// are the kept ones.
            /// </summary>
            public async Task WarmStaffReadsAsync(Guid signatoryId)
            {
                await Staff.ListSignOffEngineersAsync(default);
                await Staff.GetSignOffSignatureAsync(signatoryId, default);
            }
        }

        internal sealed record SeededDocument(
            Guid DocumentId, Guid VersionId, string Sha256, byte[] Content)
        {
            public Guid OccurrenceId { get; init; }
        }

        private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => utcNow;
        }
    }

    /// <summary>
    /// A workspace read at version 1 while <paramref name="mutate"/> moves the
    /// Case on: by default a source is added, a staff write.
    /// </summary>
    private sealed class MutatingWorkspaceSource(Harness harness, Func<Task>? mutate = null)
        : IGetAssessmentWorkspace
    {
        public async Task<AssessmentWorkspace?> ExecuteAsync(
            GetAssessmentWorkspaceQuery query, CancellationToken cancellationToken = default)
        {
            var workspace = AssessmentWorkspaceTestData.Create(
                AssessmentReportDraftWebTests.FullAssessmentProjection(harness.CaseId) with { CaseVersion = 1 });
            await (mutate ?? harness.AddSourceAsync)();
            return workspace;
        }
    }

    /// <summary>
    /// The one read model a freeze loads, built from the same accepted
    /// fixtures the routed report tests use so readiness is genuinely met. Its
    /// prepared images and applied valuation also answer the preparation and
    /// valuation ports, for the real source to read in their place
    /// (<see cref="Harness.StoreOverRealSourceAsync"/>).
    /// </summary>
    private sealed class FakeSnapshotSource
        : ICaseReportSnapshotSource, ICaseAssetPreparationQueries, IListAppliedValuations
    {
        private static readonly DateTimeOffset RecordedAtUtc = new(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);

        private readonly Guid caseId;
        private readonly string reference;
        private readonly Guid signatoryId;
        private readonly CaseAssessmentProjection assessment;
        private readonly AssessmentReportProjectionInput projection;
        private readonly Harness.SeededDocument closeUp;
        private readonly Harness.SeededDocument overview;
        private readonly IReadOnlyList<Harness.SeededDocument> supporting;
        private readonly IReadOnlyDictionary<Guid, DocumentVersion> confirmedSources;
        private readonly RepairSpecificationVersion estimate =
            AssessmentReportDraftWebTests.CurrentEstimate();
        private readonly Guid valuationId = Guid.NewGuid();
        private readonly Guid guideValuationId = Guid.NewGuid();
        private decimal engineerValue = 5_000m;
        private bool fromCard = true;
        private Func<CaseAssessmentProjection, CaseAssessmentProjection>? transform;

        public FakeSnapshotSource(
            Guid caseId,
            string reference,
            Guid signatoryId,
            Harness.SeededDocument closeUp,
            Harness.SeededDocument overview,
            IReadOnlyList<Harness.SeededDocument> supporting,
            IReadOnlyList<EfAssessmentReportProjectionSource.ConfirmedDocumentRow> confirmed)
        {
            this.caseId = caseId;
            this.reference = reference;
            this.signatoryId = signatoryId;
            this.closeUp = closeUp;
            this.overview = overview;
            this.supporting = supporting;
            confirmedSources = EfAssessmentReportProjectionSource.ConfirmedImageSources(confirmed);
            assessment = AssessmentReportDraftWebTests.FullAssessmentProjection(caseId);
            projection = AssessmentReportDraftWebTests.ReadyInput(caseId) with
            {
                Assessment = assessment,
                ReportDate = null,
                CurrentEstimate = estimate,
                Signatory = new ReportSignatory("Ed Mawdsley", "ATA VDA AQP", Harness.SignatureBytes, "image/png"),
                Photos =
                [
                    Photo(overview, CaseAssetReportRole.Overview, 1),
                    Photo(closeUp, CaseAssetReportRole.CloseUp, 2),
                    .. supporting.Select((document, index) =>
                        Photo(document, CaseAssetReportRole.Supporting, index + 3)),
                ],
                Sources = EfAssessmentReportProjectionSource.ReportSources(confirmed),
            };
        }

        /// <summary>
        /// A calculation fills the Engineer's Value box with <paramref name="value"/>
        /// and the Save records it against its basis card: the field the report
        /// prints and the applied row move together.
        /// </summary>
        public void AcceptEngineerValue(decimal value)
        {
            engineerValue = value;
            transform = current => WithValues(current, engineer: value);
        }

        /// <summary>
        /// The three boxes typed by hand: the fields carry the figures and no
        /// applied valuation row exists (operator, 26 September 2026).
        /// </summary>
        public void TypeValues(decimal retail, decimal trade, decimal engineer)
        {
            fromCard = false;
            transform = current => WithValues(current, retail, trade, engineer);
        }

        /// <summary>
        /// Applies <paramref name="revise"/> to the accepted assessment every
        /// later read returns, both to what the report prints and to what
        /// readiness is decided from.
        /// </summary>
        public void TransformAssessment(Func<CaseAssessmentProjection, CaseAssessmentProjection> revise) =>
            transform = revise;

        /// <summary>The Audit work, once the test gives the Case one; the current work is then the Audit's.</summary>
        public Guid? AuditWorkId { get; set; }

        /// <summary>The Principal's images rule, as the real source reads it with the Principal.</summary>
        public bool IncludeVehicleImages { get; set; } = true;
        public string? VatNumber { get; set; }

        /// <summary>The Principal's own invoice's facts, as the real source builds them for a Principal that has one.</summary>
        public UlaInvoiceSnapshot? PrincipalInvoice { get; set; }

        public Task<CaseReportFreezeInputs?> GetAsync(
            Guid requestedCaseId, ActionActor actor, CaseWorkSelector work, ReportProjectionReuse? reuse, CancellationToken cancellationToken)
        {
            var current = transform?.Invoke(assessment) ?? assessment;
            // The primary work's id is the Case's.
            var workId = work == CaseWorkSelector.Current && AuditWorkId is { } auditWorkId ? auditWorkId : caseId;
            return Task.FromResult<CaseReportFreezeInputs?>(
                requestedCaseId == caseId
                    ? new(
                        projection with
                        {
                            Assessment = current,
                            IncludeVehicleImages = IncludeVehicleImages,
                            VatNumber = VatNumber,
                            PrincipalInvoice = PrincipalInvoice,
                        },
                        Readiness(current), reference, 1) { WorkId = workId }
                    : null);
        }

        public Task<CaseAssetPreparation?> GetForOccurrenceAsync(
            Guid requestedCaseId, Guid occurrenceId, CancellationToken cancellationToken) =>
            Task.FromResult(requestedCaseId == caseId
                ? Preparations.SingleOrDefault(item => item.OccurrenceId == occurrenceId)
                : null);

        public Task<IReadOnlyList<CaseAssetPreparation>> ListForCaseAsync(
            Guid requestedCaseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CaseAssetPreparation>>(requestedCaseId == caseId ? Preparations : []);

        public Task<IReadOnlyList<AppliedValuation>> ExecuteAsync(
            Guid requestedCaseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AppliedValuation>>(
                requestedCaseId == caseId && fromCard ? [Valuation()] : []);

        // The signatory is also the Case's assigned Engineer, which a ready
        // report needs (#1213 ruling 3).
        private CaseReportReadinessInput Readiness(CaseAssessmentProjection current) => new(
            current,
            signatoryId,
            signatoryId,
            [new SignOffEngineerProfile(
                signatoryId, "Ed Mawdsley", "ATA VDA AQP", IsDefault: true)],
            estimate,
            fromCard ? Valuation() : null,
            Preparations,
            confirmedSources);

        /// <summary>The operator's prepared images, which readiness and the real source both read.</summary>
        private IReadOnlyList<CaseAssetPreparation> Preparations =>
        [
            Preparation(closeUp, CaseAssetReportRole.CloseUp),
            Preparation(overview, CaseAssetReportRole.Overview),
            .. supporting.Select(document => Preparation(document, CaseAssetReportRole.Supporting)),
        ];

        private static CaseAssessmentProjection WithValues(
            CaseAssessmentProjection current,
            decimal? retail = null,
            decimal? trade = null,
            decimal? engineer = null) => current with
            {
                Fields = current.Fields
                    .Select(field => field.Path switch
                    {
                        AssessmentVocabulary.ValueRetail when retail is { } value => field with { Value = Money(value) },
                        AssessmentVocabulary.ValueTrade when trade is { } value => field with { Value = Money(value) },
                        AssessmentVocabulary.ValueEngineer when engineer is { } value => field with { Value = Money(value) },
                        _ => field,
                    })
                    .ToArray(),
            };

        private static string Money(decimal value) =>
            value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        private AppliedValuation Valuation() => new(
            valuationId, caseId, 1, guideValuationId, RecordedAtUtc,
            new ValuationCalculation(
                engineerValue, false, 0m, engineerValue, null, 0m, [], 0m, 0m, engineerValue),
            engineerValue, "engineer-1", RecordedAtUtc, "Accepted the guide value",
            "case-valuation-calculation/v1");

        private static ReportImageEvidence Photo(
            Harness.SeededDocument document, CaseAssetReportRole role, int? order = null) => new(
                $"{document.OccurrenceId:D}.png", "image/png", ReportImageContent.Opened(_ => Task.FromResult(document.Content)),
                document.Sha256, role, order, CaseAssetRotation.None, CaseAssetCrop.Full,
                document.OccurrenceId, document.VersionId,
                $"box-file-{document.VersionId:N}", $"box-version-{document.VersionId:N}",
                role == CaseAssetReportRole.CloseUp);

        private CaseAssetPreparation Preparation(
            Harness.SeededDocument document, CaseAssetReportRole role) => new(
                caseId, document.OccurrenceId, document.DocumentId, document.VersionId, 1,
                document.Sha256, "image/png", true, null, CaseAssetRotation.None, CaseAssetCrop.Full,
                1, "engineer-1", RecordedAtUtc, role == CaseAssetReportRole.CloseUp)
            {
                TagIds = role switch
                {
                    CaseAssetReportRole.CloseUp => [ImageTagVocabulary.CloseUpId],
                    CaseAssetReportRole.Overview => [ImageTagVocabulary.OverviewId],
                    _ => [],
                },
                SourceFileName = $"{document.OccurrenceId:D}.png",
                RecordedAtUtc = RecordedAtUtc,
                CanPrint = true
            };

    }

    /// <summary>
    /// Rehydrates the frozen snapshot's pinned bytes exactly as
    /// <c>EfCaseReportContentSource</c> does, without a staff-account store.
    /// </summary>
    private sealed class FakeContentSource(CaseReportGenerationPersistenceTests.Harness harness)
        : ICaseReportContentSource
    {
        public Task<AssessmentReportSnapshot> ComposeAsync(
            CaseReportGenerationSnapshot snapshot, ActionActor actor, CancellationToken cancellationToken) =>
            Task.FromResult(snapshot.Report with
            {
                Photos = harness.RehydratedPhotos(snapshot),
                Signatory = snapshot.Report.Signatory with
                {
                    SignatureContent = Harness.SignatureBytes,
                    SignatureContentType = snapshot.SignatureContentType,
                },
            });
    }

    /// <summary>Records the store call order without changing any behaviour.</summary>
    private sealed class RecordingStore(EfCaseReportGenerationStore inner, List<string> sequence)
        : ICaseReportGenerationStore
    {
        public Task<CaseReportFreezeResult> FreezeAsync(
            FreezeCaseReportGenerationRequest request, CancellationToken cancellationToken)
        {
            sequence.Add("freeze");
            return inner.FreezeAsync(request, cancellationToken);
        }

        public Task<CaseReportGenerationRecord> ConfirmArtifactAsync(
            ConfirmCaseReportArtifactRequest request, CancellationToken cancellationToken)
        {
            sequence.Add("confirm");
            return inner.ConfirmArtifactAsync(request, cancellationToken);
        }

        public Task<CaseReportGenerationRecord> RecordArtifactOutcomeAsync(
            RecordCaseReportArtifactOutcomeRequest request, CancellationToken cancellationToken)
        {
            sequence.Add("record");
            return inner.RecordArtifactOutcomeAsync(request, cancellationToken);
        }

        public Task<CaseReportGenerationRecord> GetForDeliveryAsync(
            SendCaseReportRequest request, CancellationToken cancellationToken) =>
            inner.GetForDeliveryAsync(request, cancellationToken);

        public Task<CaseReportGenerationRecord?> GetAsync(
            ActionActor actor, Guid caseId, Guid generationId, CancellationToken cancellationToken) =>
            inner.GetAsync(actor, caseId, generationId, cancellationToken);

        public Task<CaseReportGenerationRecord?> GetCurrentAsync(
            ActionActor actor, Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            inner.GetCurrentAsync(actor, caseId, CaseWorkSelector.Current, cancellationToken);

        public Task<IReadOnlyList<CaseReportGenerationRecord>> ListAsync(
            ActionActor actor, Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            inner.ListAsync(actor, caseId, CaseWorkSelector.Current, cancellationToken);

        public Task<int> MarkStaleAsync(
            Guid caseId, string reasonCode, CancellationToken cancellationToken) =>
            inner.MarkStaleAsync(caseId, reasonCode, cancellationToken);

        public Task RecordDraftPreviewedAsync(
            RecordCaseReportDraftPreviewedRequest request, CancellationToken cancellationToken)
        {
            sequence.Add("preview");
            return inner.RecordDraftPreviewedAsync(request, cancellationToken);
        }
    }

    private sealed class RecordingRenderer(CaseReportGenerationPersistenceTests.Harness harness)
        : IAssessmentReportRenderer
    {
        public byte[] Pdf { get; } = [0x25, 0x50, 0x44, 0x46, 1, 2, 3];

        public string Sha256 => Convert.ToHexStringLower(SHA256.HashData(Pdf));

        public string EngineVersion => "Fake/1.0";

        public List<CaseReportArtifactKind> Kinds { get; } = [];

        /// <summary>Runs after the freeze committed and before custody.</summary>
        public Func<Task>? Before { get; set; }

        /// <summary>The facts the last render was given.</summary>
        public AssessmentReportSnapshot? Rendered { get; private set; }

        public async Task<RenderedReportArtifact> RenderAsync(
            AssessmentReportSnapshot snapshot,
            CaseReportArtifactKind kind,
            CancellationToken cancellationToken = default)
        {
            Kinds.Add(kind);
            Rendered = snapshot;
            harness.Sequence.Add("render");
            if (Before is not null)
            {
                await Before();
            }

            return new($"{kind}.pdf", Pdf, 1, Sha256, AssessmentReportContract.TemplateVersion, EngineVersion);
        }
    }

    /// <summary>A renderer a restart-safe retry must never reach.</summary>
    private sealed class RefusingRenderer : IAssessmentReportRenderer
    {
        public string EngineVersion => "Fake/1.0";

        public Task<RenderedReportArtifact> RenderAsync(
            AssessmentReportSnapshot snapshot,
            CaseReportArtifactKind kind,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "A retry that custody already settled must not render the same bytes again.");
    }

    private sealed class RecordingCustody(CaseReportGenerationPersistenceTests.Harness harness)
        : ICaseArtifactCustody
    {
        public CaseArtifactCustodyDisposition Disposition { get; init; } =
            CaseArtifactCustodyDisposition.Confirmed;

        public string? FailureCode { get; init; }

        /// <summary>
        /// Whether the durable object landed Confirmed even though the
        /// generation's second transaction has not recorded it.
        /// </summary>
        public bool ConfirmedInCustody { get; init; }

        public int Calls { get; private set; }

        public Guid? DocumentId { get; private set; }

        public Guid? VersionId { get; private set; }

        public Guid? OccurrenceId { get; private set; }

        public string? Sha256 { get; private set; }

        public long ContentLength { get; private set; }

        public List<string> OperationKeys { get; } = [];

        public List<string> OccurrenceIdentities { get; } = [];

        public async Task<CaseArtifactCustodyResult> RetainAsync(
            CaseArtifactCustodyRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            Calls++;
            OperationKeys.Add(request.OperationKey);
            OccurrenceIdentities.Add(request.OccurrenceIdentity);
            harness.Sequence.Add("retain");
            using var buffer = new MemoryStream();
            await request.Content.CopyToAsync(buffer, cancellationToken);
            if (Disposition == CaseArtifactCustodyDisposition.Failed)
            {
                return new(
                    CaseArtifactCustodyDisposition.Failed, null, null, null, null, null, null, null, null,
                    FailureCode, null);
            }

            var confirmed = ConfirmedInCustody || Disposition == CaseArtifactCustodyDisposition.Confirmed;
            var retained = await harness.RetainArtifactAsync(request, buffer.ToArray(), confirmed);
            DocumentId = retained.DocumentId;
            VersionId = retained.VersionId;
            OccurrenceId = retained.OccurrenceId;
            Sha256 = request.Sha256;
            ContentLength = request.ContentLength;
            return new(
                Disposition,
                retained.DocumentId,
                retained.VersionId,
                retained.OccurrenceId,
                confirmed ? $"box-file-{retained.VersionId:N}" : null,
                confirmed ? $"box-version-{retained.VersionId:N}" : null,
                request.Sha256,
                request.ContentLength,
                request.MediaType,
                null,
                confirmed ? null : $"pending/{retained.VersionId:N}");
        }
    }

    private sealed class RecordingCustodyStatus : ICaseArtifactCustodyStatus
    {
        public (Guid CaseId, Guid DocumentId, Guid VersionId, Guid OccurrenceId)? LastQuery { get; private set; }

        public string? LastOperationKey { get; private set; }

        /// <summary>What custody holds under the operation key; nothing on a first request.</summary>
        public CaseArtifactCustodyResult? Result { get; init; }

        public Task<CaseArtifactCustodyResult> GetAsync(
            ActionActor actor, Guid caseId, Guid documentId, Guid versionId, Guid occurrenceId,
            CancellationToken cancellationToken)
        {
            LastQuery = (caseId, documentId, versionId, occurrenceId);
            return Task.FromResult(Result
                ?? throw new FileNotFoundException("Custody holds nothing under these identities."));
        }

        /// <summary>
        /// The generated artifact's recovery identity is its retain operation
        /// key (G15), so a restart-safe retry reads by it.
        /// </summary>
        public Task<CaseArtifactCustodyResult?> FindByOperationKeyAsync(
            ActionActor actor, Guid caseId, string operationKey,
            CancellationToken cancellationToken)
        {
            LastOperationKey = operationKey;
            return Task.FromResult(Result);
        }
    }

    private static readonly ApprovedMailbox TestMailbox = new(
        Guid.NewGuid(),
        "reports@collisionengineers.example",
        [ApprovedMailboxRouteScope.Outgoing],
        ApprovedMailboxState.Approved,
        "identity",
        "inbox",
        "sent",
        IdentityIsBound: true,
        ActivatedAtUtc: new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero),
        Version: 1,
        Generation: 3);

    private sealed class FixedDispatchFacts(ReportDispatchFacts facts)
        : IReportRecipientSuggestionQueries
    {
        public Task<ReportDispatchFacts?> GetAsync(
            Guid caseId, CaseWorkSelector work, CancellationToken cancellationToken) =>
            Task.FromResult<ReportDispatchFacts?>(facts);
    }

    private sealed class FixedApprovedMailboxes(params ApprovedMailbox[] mailboxes)
        : IApprovedMailboxStore
    {
        public Task<IReadOnlyList<ApprovedMailbox>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ApprovedMailbox>>(mailboxes);

        public Task<ApprovedMailbox> UpdateAsync(
            UpdateApprovedMailboxRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> IsApprovedAsync(
            string mailboxAddress,
            ApprovedMailboxRouteScope routeScope,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class RecordingReportSend : IStaffReportSend
    {
        public List<StaffReportSendCommand> Commands { get; } = [];

        public Task<StaffMailOperation> SendAsync(
            StaffReportSendCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(new StaffMailOperation(
                Guid.NewGuid(),
                StaffMailState.Unknown,
                null,
                1,
                new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero),
                null,
                null,
                null,
                command.Mail.ApprovedMailboxId,
                command.Mail.ExpectedMailboxGeneration,
                new string('d', 64),
                null,
                null,
                command.Mail.Purpose,
                command.Mail.ContextId,
                command.Mail.ExpectedContextVersion,
                null));
        }
    }

    /// <summary>
    /// Fails the SaveChanges command that writes a generation. Reads, the
    /// set-based supersede and every other command go through.
    /// </summary>
    private sealed class GenerationSaveFailureInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.CommandSource == CommandSource.SaveChanges
                && command.CommandText.Contains("[CaseReportGenerations]", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Simulated database failure at save.");
            }

            return ValueTask.FromResult(result);
        }
    }

    private static BoxCustodyOptions BoxOptions() => BoxCustodyOptions.Create(
        "https://api.box.com/2.0/", "https://upload.box.com/api/2.0/", "405543781910",
        """{"boxAppSettings":{"clientID":"x","appAuth":{"publicKeyID":"x","privateKey":"x","passphrase":"x"}},"enterpriseID":"x"}""",
        "x", "holding");

    /// <summary>
    /// Box, for a report whose images were all cached when they were filed:
    /// any request to it fails the render.
    /// </summary>
    private sealed class UnreachableBox : HttpMessageHandler, IBoxAuthorizationHeaderProvider
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "A pinned image was read from Box, not from the copy cached when it was filed.");

        public Task<string> GetAuthorizationHeaderAsync(CancellationToken cancellationToken) =>
            Task.FromResult("Bearer unreachable");

        public Task<bool> RenewIfDueAsync(CancellationToken cancellationToken) => Task.FromResult(false);
    }

    /// <summary>The read cache's blob container, in memory: one blob for each name asked for.</summary>
    private sealed class InMemoryCacheContainer : BlobContainerClient
    {
        private readonly Dictionary<string, InMemoryCacheBlob> blobs = new(StringComparer.Ordinal);

        public override BlobClient GetBlobClient(string blobName)
        {
            lock (blobs)
            {
                if (!blobs.TryGetValue(blobName, out var blob))
                {
                    blobs[blobName] = blob = new InMemoryCacheBlob();
                }
                return blob;
            }
        }
    }

    /// <summary>One cache blob: the bytes and metadata its upload wrote, read back as they were written.</summary>
    private sealed class InMemoryCacheBlob : BlobClient
    {
        private static readonly ETag Written = new("\"1\"");
        private byte[]? stored;
        private IDictionary<string, string> metadata = new Dictionary<string, string>();

        public override async Task<Response<BlobContentInfo>> UploadAsync(
            Stream content, BlobUploadOptions options, CancellationToken cancellationToken = default)
        {
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            stored = copy.ToArray();
            metadata = options.Metadata;
            return Response.FromValue(
                BlobsModelFactory.BlobContentInfo(Written, DateTimeOffset.UtcNow, null, null, 0),
                new CacheResponse());
        }

        public override Task<Response<BlobProperties>> GetPropertiesAsync(
            BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Response.FromValue(
                BlobsModelFactory.BlobProperties(
                    contentLength: stored?.LongLength ?? 0, eTag: Written, metadata: metadata),
                (Response)new CacheResponse()));

        public override Task<Response<BlobDownloadStreamingResult>> DownloadStreamingAsync(
            BlobDownloadOptions? options = null, CancellationToken cancellationToken = default) =>
            stored is null
                ? Task.FromException<Response<BlobDownloadStreamingResult>>(
                    new RequestFailedException(404, "The cache blob does not exist."))
                : Task.FromResult(Response.FromValue(
                    BlobsModelFactory.BlobDownloadStreamingResult(new MemoryStream(stored, writable: false)),
                    (Response)new CacheResponse()));
    }

    private sealed class CacheResponse : Response
    {
        public override int Status => 200;

        public override string ReasonPhrase => "OK";

        public override Stream? ContentStream { get; set; }

        public override string ClientRequestId { get; set; } = "";

        public override void Dispose()
        {
        }

        protected override bool ContainsHeader(string name) => false;

        protected override IEnumerable<HttpHeader> EnumerateHeaders() => [];

        protected override bool TryGetHeader(string name, out string value)
        {
            value = "";
            return false;
        }

        protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values)
        {
            values = [];
            return false;
        }
    }

    /// <summary>Serves the bytes the fake custody retained, by exact hash.</summary>
    private sealed class FakeDocumentReader(CaseReportGenerationPersistenceTests.Harness harness)
        : IReadLogicalDocumentVersion
    {
        public Task<LogicalDocumentContent> OpenAsync(
            ReadLogicalDocumentVersionRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            var versionId = request.VersionId
                ?? throw new InvalidOperationException("A logical version identity is required.");
            if (!harness.RetainedContent.TryGetValue(versionId, out var content))
            {
                throw new InvalidOperationException($"Version '{versionId}' was never retained.");
            }

            if (!string.Equals(
                Convert.ToHexStringLower(SHA256.HashData(content)),
                request.ExpectedSha256,
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The retained bytes do not match the expected hash.");
            }

            return Task.FromResult(new LogicalDocumentContent(
                new MemoryStream(content, writable: false), request.DocumentId, versionId, null,
                request.ExpectedSha256, content.LongLength, "artifact.pdf", "application/pdf"));
        }
    }
}

/// <summary>
/// The one database a class of report generation tests shares: restored and
/// migrated once, with the default sign-off Engineer seeded once. A test makes
/// a Case of its own in it and removes that Case's rows when it ends, so the
/// next test finds the database as the one before it did. A test that changes
/// the signatory or adds a database user reaches every Case, so it makes a
/// database of its own with <see cref="CreateAsync"/> instead.
/// </summary>
public sealed class ReportGenerationDatabase : IAsyncLifetime
{
    /// <summary>The default sign-off Engineer's signature image.</summary>
    internal static readonly byte[] SignatureBytes = [137, 80, 78, 71, 9, 9, 9, 9];

    private readonly string contentRoot =
        Path.Combine(Path.GetTempPath(), "Pegasus.ReportTests", Guid.NewGuid().ToString("N"));

    private LocalDbTestDatabase? database;
    private ForeignKeyRowRemoval? caseRemoval;
    private int caseNumber;

    /// <summary>The default sign-off Engineer, who signs off every Case in this database.</summary>
    internal Guid SignatoryId { get; } = Guid.NewGuid();

    internal PooledDbContextFactory<PegasusDbContext> Factory { get; private set; } = null!;

    internal string ConnectionString => Database.ConnectionString;

    internal LocalDbTestDatabase Database =>
        database ?? throw new InvalidOperationException("The database has not been created.");

    /// <summary>A database of its own, for a test that cannot share the class's.</summary>
    internal static async Task<ReportGenerationDatabase> CreateAsync()
    {
        var created = new ReportGenerationDatabase();
        try
        {
            await created.InitializeAsync();
        }
        catch
        {
            await created.DisposeAsync();
            throw;
        }

        return created;
    }

    public async Task InitializeAsync()
    {
        database = await LocalDbTestDatabase.CreateAsync(
            localArtifactRootFactory: _ => contentRoot,
            configureServices: IdentityPersistenceTestServices.Configure);
        Factory = new PooledDbContextFactory<PegasusDbContext>(
            new DbContextOptionsBuilder<PegasusDbContext>().UseSqlServer(database.ConnectionString).Options);
        await using var context = await Factory.CreateDbContextAsync();
        var engineerRole = await context.Roles.SingleAsync(role => role.NormalizedName == "ENGINEER");
        context.Users.Add(new PegasusIdentityUser
        {
            Id = SignatoryId,
            UserName = "ed-mawdsley", NormalizedUserName = "ED-MAWDSLEY",
            SecurityStamp = Guid.NewGuid().ToString(), ConcurrencyStamp = Guid.NewGuid().ToString(),
            IsEnabled = true, IsSignOffEngineer = true, IsDefaultSignOffEngineer = true,
            SignOffPrintedName = "Ed Mawdsley", SignOffQualifications = "ATA VDA AQP",
            SignOffSignature = SignatureBytes,
            SignOffSignatureDigest = Convert.ToHexStringLower(SHA256.HashData(SignatureBytes)),
        });
        context.UserRoles.Add(new IdentityUserRole<Guid> { UserId = SignatoryId, RoleId = engineerRole.Id });
        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (database is not null)
        {
            await database.DisposeAsync();
        }

        if (Directory.Exists(contentRoot))
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    /// <summary>The harness's own services, for the local stores custody writes to.</summary>
    internal AsyncServiceScope CreateAsyncScope() => Database.CreateAsyncScope();

    /// <summary>The next number for a Case's Principal code, reference and sequence, each unique in the database.</summary>
    internal int NextCaseNumber() => Interlocked.Increment(ref caseNumber);

    /// <summary>
    /// Removes the Case and everything that hangs from it, and the Principal,
    /// Organization, sequence lineage and intake receipt it was seeded with.
    /// Nothing else in the database is touched.
    /// </summary>
    internal async Task RemoveCaseAsync(SeededCase seeded)
    {
        caseRemoval ??= await ForeignKeyRowRemoval.CreateAsync(Database, ForeignKeyRowRemoval.CasesTable);
        await caseRemoval.RemoveAsync(seeded.CaseId);

        // The history names the Case without a key to it; the rest are what the Case hung from.
        await using var context = await Factory.CreateDbContextAsync();
        var aggregateId = seeded.CaseId.ToString("D");
        await context.ActionHistory.Where(item => item.AggregateId == aggregateId).ExecuteDeleteAsync();
        await context.Principals.Where(item => item.Id == seeded.PrincipalId).ExecuteDeleteAsync();
        await context.PrincipalSequenceLineages.Where(item => item.Id == seeded.LineageId).ExecuteDeleteAsync();
        await context.Organizations.Where(item => item.Id == seeded.OrganizationId).ExecuteDeleteAsync();
        await context.IntakeReceipts.Where(item => item.Id == seeded.ReceiptId).ExecuteDeleteAsync();
    }

    /// <summary>A Case seeded for one test, with the rows it was seeded from.</summary>
    internal sealed record SeededCase(
        Guid CaseId, string Reference, Guid OrganizationId, Guid LineageId, Guid PrincipalId, Guid ReceiptId);
}
