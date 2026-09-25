using Pegasus.Core.Cases;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
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

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// The durable half of B05: the freeze/confirm two-transaction shape, the
/// retained Pending/Failed/Unknown custody outcomes and their restart-safe
/// retry, the stale rule, and the A06 report-ready reader contract — all
/// against a real database, with fakes only at the custody and rendering
/// boundaries Stream A owns.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed class CaseReportGenerationPersistenceTests
{
    [Theory]
    [InlineData(CaseReportArtifactKind.AssessmentReport)]
    [InlineData(CaseReportArtifactKind.FeeNote)]
    public async Task UserCanFreezeEachReportArtifactWithTheExistingImmutableGenerationBoundary(
        CaseReportArtifactKind kind)
    {
        await using var harness = await Harness.CreateAsync(StaffRole.User);
        var generator = harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness));
        var report = await generator.ExecuteAsync(harness.Request(), default);

        var result = kind == CaseReportArtifactKind.AssessmentReport
            ? report
            : await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
                .ExecuteAsync(harness.Request(
                    CaseReportArtifactKind.FeeNote,
                    operationKey: "user-fee-note",
                    targetGenerationId: report.Generation!.Id), default);

        var generation = Assert.IsType<CaseReportGenerationRecord>(result.Generation);
        Assert.Contains(StaffRole.User, generation.Snapshot.GeneratedBy.ToActor().Roles);
        Assert.Contains(generation.Artifacts, artifact => artifact.Kind == kind);
    }

    [Fact]
    public async Task SourceReaderRejectsMutationDuringWorkspaceReadInsteadOfLabellingOldFieldsAsCurrent()
    {
        await using var harness = await Harness.CreateAsync();

        await Assert.ThrowsAsync<CaseVersionConflictException>(() =>
            harness.ReadSourceAsync(new MutatingWorkspaceSource(harness)));
    }

    [Fact]
    public async Task SourceReaderExcludesRetainedReportOutputButKeepsGeneratedInputDocuments()
    {
        await using var harness = await Harness.CreateAsync();
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
        await using var harness = await Harness.CreateAsync();
        await harness.ChangeSignatoryAsync("name");

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Store.FreezeAsync(
            new(harness.StaffActor, harness.CaseId, 1, harness.Lease.Token, Harness.OperationKey,
                CaseReportArtifactKind.AssessmentReport, "Generate the report",
                AssessmentReportContract.TemplateVersion, "fake"), default));

        Assert.Contains("sign-off Engineer changed", refused.Message, StringComparison.Ordinal);
        Assert.Empty(await harness.GenerationRowsAsync());
    }

    [Fact]
    public async Task FreezeRejectsOlderInputsEvenWhenTheCallerHoldsTheCurrentCaseVersion()
    {
        await using var harness = await Harness.CreateAsync();
        // The source is deliberately held at version 1 while a genuine source
        // mutation advances the Case and the caller reloads/reacquires at 2.
        await harness.AddSourceAsync();
        var lease = await new AcquireCaseEditLease(
                new EfCaseWorkflowStore(harness.Factory, Harness.Clock))
            .ExecuteAsync(new(harness.CaseId, 2, harness.StaffActor, "lease-report-2"), default);

        await Assert.ThrowsAsync<CaseVersionConflictException>(() => harness.Store.FreezeAsync(
            new(harness.StaffActor, harness.CaseId, 2, lease.Token, Harness.OperationKey,
                CaseReportArtifactKind.AssessmentReport, "Generate the report",
                AssessmentReportContract.TemplateVersion, "fake"), default));

        Assert.Empty(await harness.GenerationRowsAsync());
        Assert.Empty(await harness.ArtifactRowsAsync());
    }

    [Fact]
    public async Task FreezeRefusesTheBrowserExpectedVersionInsteadOfAdoptingTheCurrentVersion()
    {
        await using var harness = await Harness.CreateAsync();
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == harness.CaseId);
            workflow.Version = 2;
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<CaseVersionConflictException>(() => harness.Store.FreezeAsync(
            new(harness.StaffActor, harness.CaseId, 1, harness.Lease.Token, Harness.OperationKey,
                CaseReportArtifactKind.AssessmentReport, "Generate the report",
                AssessmentReportContract.TemplateVersion, "fake"), default));

        Assert.Empty(await harness.GenerationRowsAsync());
        Assert.Empty(await harness.ArtifactRowsAsync());
    }

    [Fact]
    public async Task DefaultReportDateUsesLondonAtBstMidnight()
    {
        await using var harness = await Harness.CreateAsync();
        var result = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), default);

        Assert.Equal(new DateOnly(2026, 9, 7), result.Generation!.Snapshot.ReportDate);
        Assert.Equal(new DateOnly(2026, 9, 7), result.Generation.Snapshot.Report.ReportDate);
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
        await using var harness = await Harness.CreateAsync();
        // The fixture captured the complete source census at construction.
        // Change persisted evidence independently, without a staff Case edit.
        await harness.ChangeConfirmedSourceAsync(change);
        await using var context = await harness.Factory.CreateDbContextAsync();
        var workflow = await context.CaseWorkflows.SingleAsync();
        CaseMutationGuard.Require(workflow, harness.StaffActor, 1, harness.Lease.Token, Harness.StartUtc);

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => harness.Store.FreezeAsync(
            new(harness.StaffActor, harness.CaseId, 1, harness.Lease.Token, Harness.OperationKey,
                CaseReportArtifactKind.AssessmentReport, "Generate the report",
                AssessmentReportContract.TemplateVersion, "fake"), default));

        Assert.Contains("source evidence changed", refused.Message, StringComparison.Ordinal);
        Assert.Empty(await harness.GenerationRowsAsync());
        Assert.Empty(await harness.ArtifactRowsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceAdditionOrRemovalInvalidatesTheReportAndRefusesPreparedDelivery(bool remove)
    {
        await using var harness = await Harness.CreateAsync();
        var generated = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), default);
        var generation = generated.Generation!;
        var delivery = await harness.PrepareDeliveryAsync(generation);

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
        await Assert.ThrowsAsync<InvalidOperationException>(() => harness.PrepareDeliveryAsync(generation));
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
        await using var harness = await Harness.CreateAsync();
        var generated = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), default);
        var generation = generated.Generation!;
        var delivery = await harness.PrepareDeliveryAsync(generation);

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
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.PrepareDeliveryAsync(generation));
            await Assert.ThrowsAsync<InvalidOperationException>(() => harness.RequireDeliveryReadyAsync(delivery));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkflowSignatoryChangesStaleTheReportAndRefusePreparation(
        bool throughEngineerAssignment)
    {
        await using var harness = await Harness.CreateAsync();
        var generated = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), default);
        var generation = generated.Generation!;
        var replacementId = await harness.AddEligibleSignatoryAsync();
        var workflowStore = new EfCaseWorkflowStore(harness.Factory, Harness.Clock);
        CaseWorkflowRecord changed;
        if (throughEngineerAssignment)
        {
            await harness.MarkReviewReadyAsync();
            changed = await workflowStore.AssignEngineerAsync(
                new(
                    harness.CaseId,
                    1,
                    harness.StaffActor,
                    "assign-report-signatory",
                    "Assign the replacement Engineer.",
                    harness.Lease.Token,
                    replacementId,
                    new(true, true, "persisted-case-readiness")),
                replacementId,
                CaseLifecycleState.ReportPreparation,
                default);
        }
        else
        {
            changed = await workflowStore.SetSignOffEngineerAsync(
                new(
                    harness.CaseId,
                    1,
                    harness.StaffActor,
                    "select-report-signatory",
                    "Select the replacement sign-off Engineer.",
                    harness.Lease.Token,
                    replacementId),
                default);
        }

        var stale = Assert.Single(await harness.GenerationRowsAsync());
        Assert.Equal(CaseReportGenerationState.Stale, stale.State);
        Assert.Equal(CaseReportStaleReasons.SignatoryChanged, await harness.StaleReasonAsync());
        var lease = await new AcquireCaseEditLease(workflowStore).ExecuteAsync(
            new(
                harness.CaseId,
                changed.Version,
                harness.StaffActor,
                "lease-after-report-signatory-change"),
            default);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.PrepareDeliveryAsync(generation, changed.Version, lease.Token));
    }

    [Fact]
    public async Task SelectingTheEffectiveDefaultSignatoryDoesNotStaleTheReport()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), default);
        var workflowStore = new EfCaseWorkflowStore(harness.Factory, Harness.Clock);

        await workflowStore.SetSignOffEngineerAsync(
            new(
                harness.CaseId,
                1,
                harness.StaffActor,
                "select-effective-report-signatory",
                "Select the already effective sign-off Engineer.",
                harness.Lease.Token,
                FakeSnapshotSource.SignatoryId),
            default);

        var current = Assert.Single(await harness.GenerationRowsAsync());
        Assert.Equal(CaseReportGenerationState.Confirmed, current.State);
        Assert.Equal(
            0,
            await harness.ActionHistoryCountAsync(EfCaseReportGenerationStore.StaleEventKind));
    }

    [Theory]
    [InlineData("pegasus_web_runtime_role")]
    [InlineData("pegasus_worker_runtime_role")]
    public async Task RuntimeCustodyCallerInvalidatesSourceButNotItsOwnReportOutput(string role)
    {
        await using var harness = await Harness.CreateAsync();
        await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), default);
        await using var context = await harness.Factory.CreateDbContextAsync();
        await context.Database.OpenConnectionAsync();
        await context.Database.ExecuteSqlRawAsync("CREATE USER [report_custody_caller] WITHOUT LOGIN;");
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC sys.sp_addrolemember @rolename = {role}, @membername = N'report_custody_caller';");
        await context.Database.ExecuteSqlRawAsync("EXECUTE AS USER = 'report_custody_caller';");
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            await EfCaseArtifactCustody.RecordConfirmedSourceChangeAsync(
                context, harness.CaseId, Harness.OperationKey, Harness.StartUtc, default);
            Assert.Equal(1, (await context.CaseWorkflows.SingleAsync()).Version);
            Assert.Equal(nameof(CaseReportGenerationState.Confirmed),
                (await context.Set<CaseReportGenerationEntity>().SingleAsync()).State);

            await EfCaseArtifactCustody.RecordConfirmedSourceChangeAsync(
                context, harness.CaseId, "glass-estimate-source", Harness.StartUtc, default);
            await transaction.CommitAsync();
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("REVERT;");
        }

        Assert.Equal(CaseReportGenerationState.Stale, Assert.Single(await harness.GenerationRowsAsync()).State);
        var workflow = await context.CaseWorkflows.AsNoTracking().SingleAsync();
        Assert.Equal(1, workflow.Version);
        CaseMutationGuard.Require(workflow, harness.StaffActor, 1, harness.Lease.Token, Harness.StartUtc);
        Assert.Equal(harness.Lease.ExpiresAtUtc, workflow.EditLeaseExpiresAtUtc);
        Assert.Equal(1, await harness.ActionHistoryCountAsync("case_report_generation_stale"));
    }

    [Fact]
    public async Task WebRuntimeRoleCanFreezeAndConfirmThroughTheActualReportStore()
    {
        await using var harness = await Harness.CreateAsync();
        await using var connectionOwner = await harness.Factory.CreateDbContextAsync();
        await connectionOwner.Database.OpenConnectionAsync();
        await connectionOwner.Database.ExecuteSqlRawAsync(
            "CREATE USER [report_generation_caller] WITHOUT LOGIN; ALTER ROLE [pegasus_web_runtime_role] ADD MEMBER [report_generation_caller];");
        await connectionOwner.Database.ExecuteSqlRawAsync("EXECUTE AS USER = 'report_generation_caller';");
        try
        {
            // EF does not own this already-open connection. Both store
            // transactions run under the same real runtime impersonation.
            var factory = new PooledDbContextFactory<PegasusDbContext>(
                new DbContextOptionsBuilder<PegasusDbContext>()
                    .UseSqlServer(connectionOwner.Database.GetDbConnection()).Options);
            var result = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness),
                    store: harness.StoreUsing(factory))
                .ExecuteAsync(harness.Request(), default);

            Assert.Equal(CaseReportGenerationState.Confirmed, result.Generation!.State);
            Assert.Equal(CaseReportArtifactStatus.Confirmed, Assert.Single(result.Generation.Artifacts).Status);
        }
        finally
        {
            await connectionOwner.Database.ExecuteSqlRawAsync("REVERT;");
        }
    }

    [Fact]
    public async Task FreezeCommitsBeforeRenderingAndConfirmationIsASecondTransaction()
    {
        await using var harness = await Harness.CreateAsync();
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
        Assert.Equal(Harness.OperationKey, artifact.OperationKey);
        Assert.Equal($"case-report:{generation.Id:D}:AssessmentReport", Assert.Single(custody.OccurrenceIdentities));
        Assert.Equal(Harness.OperationKey, Assert.Single(custody.OperationKeys));

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
    /// B09 review (lifecycle): a stale current generation is history, not a
    /// deliverable snapshot. Regenerating the same material after a stale
    /// must create a NEW current generation - never reuse the stale row and
    /// report it as already generated while it stays undeliverable.
    /// </summary>
    [Fact]
    public async Task RegeneratingAfterAStaleCreatesANewCurrentGeneration()
    {
        await using var harness = await Harness.CreateAsync();
        var custody = new RecordingCustody(harness);
        var first = await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var firstGeneration = Assert.IsType<CaseReportGenerationRecord>(first.Generation);
        Assert.Equal(CaseReportGenerationState.Confirmed, firstGeneration.State);

        await harness.Store.MarkStaleAsync(harness.CaseId, "test-material-change", CancellationToken.None);

        // A new operation key is a new generation request; the material is
        // unchanged, so the hash matches the stale generation anyway.
        var second = await harness.Generate(custody, new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(operationKey: "case-report-regenerate"), CancellationToken.None);

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
        await using var harness = await Harness.CreateAsync();
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
        await using var harness = await Harness.CreateAsync();
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

    [Fact]
    public async Task AFailedArtifactIsRetriedWithTheSameSnapshotOperationKeyAndArtifactRow()
    {
        await using var harness = await Harness.CreateAsync();
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
        Assert.Equal(Harness.OperationKey, Assert.Single(custody.OperationKeys));
        Assert.Equal(CaseReportArtifactStatus.Confirmed, retriedArtifact.Status);
        Assert.Null(retriedArtifact.FailureCode);
        Assert.Equal(["freeze", "render", "retain", "confirm"], harness.Sequence);
        Assert.Single(await harness.GenerationRowsAsync());
    }

    [Fact]
    public async Task AFailedSeparateFeeNoteRecoversWithItsRetainedOperationIdentity()
    {
        await using var harness = await Harness.CreateAsync();
        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        const string feeOperationKey = "case-report-fee-retry";
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
        await using var harness = await Harness.CreateAsync();
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
        await using var harness = await Harness.CreateAsync();

        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        Assert.Equal(CaseReportGenerationOutcome.Generated, report.Outcome);

        var ready = Assert.Single(await harness.ReadyEventsAsync());
        Assert.Equal("case_report_generation_ready", ready.EventKind);
        Assert.Equal("Succeeded", ready.Outcome);
        Assert.Equal("case", ready.AggregateType);
        Assert.Equal(harness.CaseId.ToString("D"), ready.AggregateId);
        Assert.Equal(report.Generation!.Snapshot.OperationKey, ready.CorrelationId);
        Assert.Equal(Harness.OperationKey, ready.CorrelationId);
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
                    "case-report-fee-1",
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
        await using var harness = await Harness.CreateAsync();
        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
                .ExecuteAsync(
                    harness.Request(
                        CaseReportArtifactKind.FeeNote,
                        Harness.OperationKey,
                        targetGenerationId: report.Generation!.Id),
                    CancellationToken.None));

        Assert.Single((await harness.GenerationRowsAsync()).Single().Artifacts);
    }

    [Fact]
    public async Task SameOperationKeyWithDifferentArtifactKindConflictsBeforeCustody()
    {
        await using var harness = await Harness.CreateAsync();
        var frozen = await harness.Store.FreezeAsync(
            new(harness.StaffActor, harness.CaseId, 1, harness.Lease.Token, Harness.OperationKey,
                CaseReportArtifactKind.AssessmentReport, "Generate the report",
                AssessmentReportContract.TemplateVersion, "fake"), default);

        await Assert.ThrowsAsync<CaseOperationConflictException>(() => harness.Store.FreezeAsync(
            new(harness.StaffActor, harness.CaseId, 1, harness.Lease.Token, Harness.OperationKey,
                CaseReportArtifactKind.FeeNote, "Generate the fee note",
                AssessmentReportContract.TemplateVersion, "fake",
                TargetGenerationId: frozen.Generation!.Id), default));

        var artifact = Assert.Single(await harness.ArtifactRowsAsync());
        Assert.Equal(nameof(CaseReportArtifactKind.AssessmentReport), artifact.Kind);
        Assert.Equal(nameof(CaseReportArtifactStatus.Pending), artifact.State);
    }

    [Fact]
    public async Task SameOperationKeyWithChangedReportPackagingIsAConflict()
    {
        await using var harness = await Harness.CreateAsync();
        await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);

        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
                .ExecuteAsync(harness.Request(includeFeeNote: true), CancellationToken.None));

        Assert.False((await harness.GenerationRowsAsync()).Single().Snapshot.Report.IncludeFeeNote);
    }

    [Fact]
    public async Task SameOperationKeyWithChangedTargetGenerationIsAConflict()
    {
        await using var harness = await Harness.CreateAsync();
        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        const string feeOperationKey = "case-report-fee-target";
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
        await using var harness = await Harness.CreateAsync();
        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var targetGenerationId = report.Generation!.Id;
        var operationKey = $"case-report-{kind}-replay";
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
        await using var harness = await Harness.CreateAsync();
        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        const string originalFeeOperationKey = "case-report-fee-original";
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
                    "case-report-fee-unbound",
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
        Assert.DoesNotContain(artifacts, artifact => artifact.OperationKey == "case-report-fee-unbound");
    }

    [Fact]
    public async Task SeparateFeeNoteAttachesToTheNamedCurrentGenerationAndItsFrozenSnapshot()
    {
        await using var harness = await Harness.CreateAsync();
        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var reportDate = report.Generation!.Snapshot.ReportDate;

        var nextDay = Harness.StartUtc.AddDays(1);
        var nextDayStore = harness.StoreAt(nextDay);
        var nextDayLease = await new AcquireCaseEditLease(
                new EfCaseWorkflowStore(harness.Factory, Harness.ClockAt(nextDay)))
            .ExecuteAsync(
                new(harness.CaseId, 1, harness.StaffActor, "lease-report-next-day"),
                CancellationToken.None);
        var feeNote = await harness.Generate(
                new RecordingCustody(harness), new RecordingRenderer(harness), store: nextDayStore)
            .ExecuteAsync(
                harness.Request(
                    CaseReportArtifactKind.FeeNote,
                    "case-report-fee-next-day",
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
        await using var harness = await Harness.CreateAsync();

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
                .ExecuteAsync(
                    harness.Request(
                        CaseReportArtifactKind.FeeNote,
                        "case-report-fee-none",
                        targetGenerationId: Guid.NewGuid()),
                    CancellationToken.None));

        Assert.Contains("unavailable", refusal.Message, StringComparison.Ordinal);
        Assert.Empty(await harness.GenerationRowsAsync());
    }

    [Fact]
    public async Task SeparateFeeNoteIsRefusedForAStaleCurrentGeneration()
    {
        await using var harness = await Harness.CreateAsync();
        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        await harness.Store.MarkStaleAsync(
            harness.CaseId, CaseReportStaleReasons.AssessmentFactsChanged, CancellationToken.None);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
                .ExecuteAsync(
                    harness.Request(
                        CaseReportArtifactKind.FeeNote,
                        "case-report-fee-stale",
                        targetGenerationId: report.Generation!.Id),
                    CancellationToken.None));

        Assert.Contains("unavailable", refusal.Message, StringComparison.Ordinal);
        Assert.Single((await harness.GenerationRowsAsync()).Single().Artifacts);
    }

    [Fact]
    public async Task SeparateFeeNoteIsRefusedWhenTheReportAlreadyContainsIt()
    {
        await using var harness = await Harness.CreateAsync();
        var report = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(includeFeeNote: true), CancellationToken.None);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
                .ExecuteAsync(
                    harness.Request(
                        CaseReportArtifactKind.FeeNote,
                        "case-report-fee-embedded",
                        targetGenerationId: report.Generation!.Id),
                    CancellationToken.None));

        Assert.Contains("unavailable", refusal.Message, StringComparison.Ordinal);
        Assert.Single((await harness.GenerationRowsAsync()).Single().Artifacts);
    }

    [Fact]
    public async Task CustodyConfirmationAloneIsNeverTheReadyTransition()
    {
        await using var harness = await Harness.CreateAsync();
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
        await using var harness = await Harness.CreateAsync();
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

    /// <summary>
    /// R34B: choosing the combined document freezes that choice in the
    /// immutable snapshot and still produces exactly one artifact, under the
    /// report's own file name. The fee note is inside those bytes, so no
    /// second fee-note artifact is written.
    /// </summary>
    [Fact]
    public async Task TheCombinedReportFreezesItsPackagingChoiceAndStoresOneArtifact()
    {
        await using var harness = await Harness.CreateAsync();

        var generated = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(includeFeeNote: true), CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, generated.Outcome);
        var artifact = Assert.Single(generated.Generation!.Artifacts);
        Assert.Equal(CaseReportArtifactKind.AssessmentReport, artifact.Kind);
        Assert.Equal(CaseReportArtifactStatus.Confirmed, artifact.Status);
        Assert.DoesNotContain("fee_note", artifact.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.True(generated.Generation.Snapshot.Report.IncludeFeeNote);

        // Reloaded from the persisted snapshot JSON, not from the caller's
        // request: an issued report renders the same way again.
        var reloaded = await harness.Store.GetCurrentAsync(
            harness.StaffActor, harness.CaseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.True(reloaded!.Snapshot.Report.IncludeFeeNote);
        Assert.Single(reloaded.Artifacts);

        // The plain report is a different frozen deliverable, so it freezes
        // its own generation rather than reusing the combined one.
        var plain = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(
                harness.Request(CaseReportArtifactKind.AssessmentReport, "case-report-plain"),
                CancellationToken.None);

        Assert.NotEqual(generated.Generation.Id, plain.Generation!.Id);
        Assert.False(plain.Generation.Snapshot.Report.IncludeFeeNote);
        Assert.Equal(2, (await harness.GenerationRowsAsync()).Count);
    }

    [Fact]
    public async Task RegeneratingAfterAMaterialChangeNeverRewritesThePriorGeneration()
    {
        await using var harness = await Harness.CreateAsync();
        var first = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var firstArtifact = Assert.Single(first.Generation!.Artifacts);

        await harness.Store.MarkStaleAsync(
            harness.CaseId, CaseReportStaleReasons.ValuationChanged, CancellationToken.None);
        harness.AcceptEngineerValue(5_250m);

        var second = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(
                harness.Request(CaseReportArtifactKind.AssessmentReport, "case-report-2"),
                CancellationToken.None);

        Assert.NotEqual(first.Generation.Id, second.Generation!.Id);
        Assert.NotEqual(first.Generation.SnapshotHash, second.Generation.SnapshotHash);
        Assert.Equal(5_250m, second.Generation.Snapshot.AcceptedEngineerValue);
        Assert.Equal(CaseReportGenerationState.Confirmed, second.Generation.State);

        // The prior generation keeps its bytes, its confirmed artifact and its
        // history exactly as issued; only its currency changed.
        var prior = await harness.Store.GetAsync(
            harness.StaffActor, harness.CaseId, first.Generation.Id, CancellationToken.None);
        Assert.Equal(CaseReportGenerationState.Stale, prior!.State);
        Assert.Equal(second.Generation.Id, prior.SupersededById);
        Assert.Equal(5_000m, prior.Snapshot.AcceptedEngineerValue);
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
    /// Issue #834: the report prints only Category S, so a total loss of any
    /// other category is named before the freeze writes anything. The
    /// confirmed current generation stays current and nothing supersedes it.
    /// </summary>
    [Fact]
    public async Task ANonPrintableSalvageCategoryIsNamedBeforeAnyGenerationIsWritten()
    {
        await using var harness = await Harness.CreateAsync();
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
                .. assessment.Fields.Where(field => field.Path != AssessmentVocabulary.Outcome),
                Recorded(AssessmentVocabulary.Outcome, "total_loss"),
                Recorded(AssessmentVocabulary.SalvageCategory, "B"),
                Recorded(AssessmentVocabulary.SalvageValue, "500.00"),
            ],
        });
        var renderer = new RecordingRenderer(harness);
        var refused = await harness.Generate(new RecordingCustody(harness), renderer)
            .ExecuteAsync(harness.Request(operationKey: "case-report-category-b"), CancellationToken.None);

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
        await using var harness = await Harness.CreateAsync();
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

    [Fact]
    public async Task GeneratedReportKeepsCustodyNameUntilPreparationRenamesItAtSendBoundary()
    {
        await using var harness = await Harness.CreateAsync();
        var generated = await harness.Generate(new RecordingCustody(harness), new RecordingRenderer(harness))
            .ExecuteAsync(harness.Request(), CancellationToken.None);
        var generation = generated.Generation!;
        var suggestions = new ReportRecipientSuggestions(
            generation.Snapshot.CaseReference,
            PrincipalReportRecipientSettings.Normalize(false, ["digital@collisionengineers.co.uk"]),
            null);
        var preparation = await harness.PrepareDeliveryAsync(
            generation,
            recipientSuggestionFingerprint: suggestions.Fingerprint);

        var custodyAttachment = Assert.Single(preparation.Preparation.Artifacts);
        Assert.Equal("AssessmentReport.pdf", custodyAttachment.FileName);

        var store = new EfCaseReportDeliveryPreparationStore(harness.Factory, Harness.Clock);
        var send = new RecordingReportSend();
        var operation = await new SendPreparedCaseReport(
            store,
            new FixedRecipientSuggestions(suggestions),
            new FixedApprovedMailboxes(TestMailbox()),
            new ReportSendReadiness(store),
            send)
            .ExecuteAsync(
                new(
                    harness.StaffActor,
                    harness.CaseId,
                    preparation.Preparation.Id,
                    preparation.Preparation.Version,
                    "send-report"),
                CancellationToken.None);

        Assert.Equal(StaffMailState.Unknown, operation.State);
        var command = Assert.Single(send.Commands);
        var expectedReportName = CaseReportDeliveryNaming.ReportName(
            generation.Snapshot.CaseReference,
            generation.Snapshot.Report.Vehicle.Registration,
            generation.Snapshot.Report.Outcome.ToString(),
            0);
        Assert.Equal(expectedReportName + ".pdf", Assert.Single(command.Mail.Attachments).FileName);
        Assert.NotEqual(custodyAttachment.FileName, command.Mail.Attachments[0].FileName);
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
        await using var harness = await Harness.CreateAsync();

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
        await using var harness = await Harness.CreateAsync();
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
        await using var harness = await Harness.CreateAsync();
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

    private sealed class Harness : IAsyncDisposable
    {
        internal const string OperationKey = "case-report-1";
        internal static readonly DateTimeOffset StartUtc = new(2026, 9, 6, 23, 30, 0, TimeSpan.Zero);
        internal static TimeProvider Clock => ClockAt(StartUtc);
        internal static TimeProvider ClockAt(DateTimeOffset now) => new FixedTimeProvider(now);

        private readonly LocalDbTestDatabase database;
        private readonly string contentRoot;
        private readonly FakeSnapshotSource snapshotSource;

        private Harness(
            LocalDbTestDatabase database,
            string contentRoot,
            PooledDbContextFactory<PegasusDbContext> factory,
            Guid caseId,
            ActionActor staffActor,
            CaseEditLease lease,
            FakeSnapshotSource snapshotSource,
            SeededDocument closeUp,
            SeededDocument overview,
            SeededDocument source)
        {
            this.database = database;
            this.contentRoot = contentRoot;
            this.snapshotSource = snapshotSource;
            Factory = factory;
            CaseId = caseId;
            StaffActor = staffActor;
            Lease = lease;
            CloseUp = closeUp;
            Overview = overview;
            Source = source;
            var store = new EfCaseReportGenerationStore(
                factory, snapshotSource, new FakeDocumentReader(this), new FixedTimeProvider(StartUtc));
            Store = store;
        }

        public PooledDbContextFactory<PegasusDbContext> Factory { get; }

        public Guid CaseId { get; }

        public ActionActor StaffActor { get; }

        public CaseEditLease Lease { get; }

        public SeededDocument CloseUp { get; }

        public SeededDocument Overview { get; }

        public SeededDocument Source { get; }

        public EfCaseReportGenerationStore Store { get; }

        public List<string> Sequence { get; } = [];

        /// <summary>The bytes each fake custody object retained, by version.</summary>
        public Dictionary<Guid, byte[]> RetainedContent { get; } = [];

        /// <summary>The seeded evidence bytes a frozen image hash pins.</summary>
        public Dictionary<string, byte[]> EvidenceContent { get; } =
            new(StringComparer.Ordinal);

        public static async Task<Harness> CreateAsync(StaffRole staffRole = StaffRole.Engineer)
        {
            var contentRoot = Path.Combine(Path.GetTempPath(), "Pegasus.ReportTests", Guid.NewGuid().ToString("N"));
            var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => contentRoot,
                configureServices: IdentityPersistenceTestServices.Configure);
            try
            {
                var options = new DbContextOptionsBuilder<PegasusDbContext>()
                    .UseSqlServer(database.ConnectionString)
                    .Options;
                var factory = new PooledDbContextFactory<PegasusDbContext>(options);
                var staffActor = ActionActor.Staff(Guid.NewGuid(), [staffRole]);
                var caseId = await SeedCaseAsync(factory);
                await SeedSignatoryAsync(factory);
                var closeUp = await SeedDocumentAsync(factory, caseId, "close-up.png", "image/png", 1);
                var overview = await SeedDocumentAsync(factory, caseId, "overview.png", "image/png", 2);
                var source = await SeedDocumentAsync(
                    factory, caseId, "instruction.pdf", "application/pdf", 3);
                var lease = await new AcquireCaseEditLease(
                        new EfCaseWorkflowStore(factory, new FixedTimeProvider(StartUtc)))
                    .ExecuteAsync(new(caseId, 1, staffActor, "lease-report"), CancellationToken.None);
                await using var sourceContext = await factory.CreateDbContextAsync();
                var confirmed = await EfAssessmentReportProjectionSource.ConfirmedDocumentsAsync(
                    sourceContext, caseId, default);
                var snapshotSource = new FakeSnapshotSource(caseId, closeUp, overview, confirmed);
                var harness = new Harness(
                    database, contentRoot, factory, caseId, staffActor, lease, snapshotSource,
                    closeUp, overview, source);
                foreach (var document in new[] { closeUp, overview, source })
                {
                    harness.EvidenceContent[document.Sha256] = document.Content;
                }

                return harness;
            }
            catch
            {
                await database.DisposeAsync();
                if (Directory.Exists(contentRoot))
                {
                    Directory.Delete(contentRoot, recursive: true);
                }
                throw;
            }
        }

        public GenerateCaseReport Generate(
            RecordingCustody custody,
            IAssessmentReportRenderer renderer,
            RecordingCustodyStatus? custodyStatus = null,
            EfCaseReportGenerationStore? store = null,
            IRenderCaseEstimateDocument? repairSpecificationDocuments = null) => new(
            new RecordingStore(store ?? Store, Sequence),
            new FakeContentSource(this),
            renderer,
            repairSpecificationDocuments ?? new RefusingRepairSpecificationDocuments(),
                custody,
                custodyStatus ?? new RecordingCustodyStatus(),
                new FixedTimeProvider(StartUtc));

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
            string operationKey = OperationKey,
            bool includeFeeNote = false,
            Guid? targetGenerationId = null) => new(
                StaffActor, CaseId, 1, Lease.Token, operationKey, kind,
                "Generate the immutable case report", includeFeeNote, targetGenerationId);

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
            return await source.GetAsync(CaseId, StaffActor, CaseWorkSelector.Current, default);
        }

        public async Task AddSourceAsync()
        {
            await using var scope = database.CreateAsyncScope();
            var store = new EfDocumentCustodyStore(Factory,
                scope.ServiceProvider.GetRequiredService<IDocumentContentStore>(), Clock);
            await store.ExecuteAsync(new(CaseId, "instruction.pdf", "application/pdf", Source.Content,
                DocumentSemanticRole.Instruction, DocumentSource.Generated, "source-change",
                StaffActor, "add-source", 1, Lease.Token), default);
        }

        public async Task RemoveSourceAsync()
        {
            await using var scope = database.CreateAsyncScope();
            ILogicallyRemoveDocument store = new EfDocumentCustodyStore(Factory,
                scope.ServiceProvider.GetRequiredService<IDocumentContentStore>(), Clock);
            await store.ExecuteAsync(new(CaseId, Source.OccurrenceId, StaffActor,
                "Incorrect source document", "remove-source", 1, Lease.Token), default);
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

        public Task<CaseReportDeliveryPreparationRecord> PrepareDeliveryAsync(
            CaseReportGenerationRecord generation,
            long expectedCaseVersion = 1,
            string? leaseToken = null,
            string? recipientSuggestionFingerprint = null) =>
            new EfCaseReportDeliveryPreparationStore(Factory, Clock).PrepareAsync(
                new(new(
                        StaffActor,
                        CaseId,
                        expectedCaseVersion,
                        leaseToken ?? Lease.Token,
                        generation.Id,
                        generation.Version,
                        "prepare-report"),
                    new([new StaffMailRecipient("digital@collisionengineers.co.uk", "pegasustest")], [], "Case report"),
                    recipientSuggestionFingerprint ?? new string('a', 64),
                    CaseReportSendHistory.None), default);

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

        public Task RequireDeliveryReadyAsync(CaseReportDeliveryPreparationRecord record) =>
            new ReportSendReadiness(new EfCaseReportDeliveryPreparationStore(Factory, Clock)).RequireReadyAsync(
                new(StaffActor, CaseId, record.FrozenCaseVersion, record.Preparation.GenerationId,
                    record.Preparation.GenerationVersion, record.Preparation.Id, record.Preparation.Version,
                    record.Preparation.Artifacts), default);

        public async Task ChangeSignatoryAsync(string change)
        {
            await using var scope = database.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<PegasusDbContext>();
            var store = new EfStaffAccountAdministration(context,
                scope.ServiceProvider.GetRequiredService<UserManager<PegasusIdentityUser>>(), Clock);
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
            var account = await context.Users.SingleAsync(
                item => item.Id == FakeSnapshotSource.SignatoryId);
            if (change == "disabled")
            {
                await store.DisableAsync(new(
                    actor, account.Id, "Unavailable", "change-signatory", account.Version), default);
            }
            else if (change == "role")
            {
                await store.UpdateAsync(new(
                    actor, account.Id, StaffRole.User, true, "Ed Mawdsley", "ATA VDA AQP", SignatureBytes, true,
                    "change-signatory", account.Version), default);
            }
            else
            {
                await store.UpdateAsync(new(
                    actor, account.Id, StaffRole.Engineer, change != "eligibility",
                    change == "name" ? "Ed M" : "Ed Mawdsley",
                    change == "qualifications" ? "ATA VDA" : "ATA VDA AQP",
                    change == "signature" ? EvidenceBytesOf(7) : SignatureBytes,
                    change != "eligibility", "change-signatory",
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

        private static async Task SeedSignatoryAsync(PooledDbContextFactory<PegasusDbContext> factory)
        {
            await using var context = await factory.CreateDbContextAsync();
            var engineerRole = await context.Roles.SingleAsync(role => role.NormalizedName == "ENGINEER");
            context.Users.Add(new PegasusIdentityUser
            {
                Id = FakeSnapshotSource.SignatoryId,
                UserName = "ed-mawdsley", NormalizedUserName = "ED-MAWDSLEY",
                SecurityStamp = Guid.NewGuid().ToString(), ConcurrencyStamp = Guid.NewGuid().ToString(),
                IsEnabled = true, IsSignOffEngineer = true, IsDefaultSignOffEngineer = true,
                SignOffPrintedName = "Ed Mawdsley", SignOffQualifications = "ATA VDA AQP",
                SignOffSignature = SignatureBytes,
                SignOffSignatureDigest = Convert.ToHexStringLower(SHA256.HashData(SignatureBytes)),
            });
            context.UserRoles.Add(new IdentityUserRole<Guid>
            {
                UserId = FakeSnapshotSource.SignatoryId, RoleId = engineerRole.Id,
            });
            await context.SaveChangesAsync();
        }

        /// <summary>Accepts a different Engineer's Value, a material change.</summary>
        public void AcceptEngineerValue(decimal value) => snapshotSource.AcceptEngineerValue(value);

        /// <summary>Records a revised assessment, which every later freeze reads.</summary>
        public void ReviseAssessment(Func<CaseAssessmentProjection, CaseAssessmentProjection> revise) =>
            snapshotSource.TransformAssessment(revise);

        public static byte[] SignatureBytes => FakeSnapshotSource.SignatureBytes;

        public ReportImageEvidence[] RehydratedPhotos(
            CaseReportGenerationSnapshot snapshot) => snapshot.Images
                .Select(image => new ReportImageEvidence(
                    $"{image.OccurrenceId:D}.png", image.ContentType, EvidenceContent[image.Sha256],
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

        public async ValueTask DisposeAsync()
        {
            await database.DisposeAsync();
            if (Directory.Exists(contentRoot))
            {
                Directory.Delete(contentRoot, recursive: true);
            }
        }

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
                    PreparationRole = nameof(CaseAssetReportRole.NotUsed)
                });
            await context.SaveChangesAsync();
            return new(documentId, versionId, sha256, content) { OccurrenceId = occurrenceId };
        }

        private static async Task<Guid> SeedCaseAsync(PooledDbContextFactory<PegasusDbContext> factory)
        {
            await using var context = await factory.CreateDbContextAsync();
            var organizationId = Guid.NewGuid();
            var lineageId = Guid.NewGuid();
            var principalId = Guid.NewGuid();
            var receiptId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            context.AddRange(
                new OrganizationEntity { Id = organizationId, Name = "Report generation test", Version = 0 },
                new PrincipalSequenceLineageEntity { Id = lineageId, CreatedAtUtc = StartUtc },
                new PrincipalEntity
                {
                    Id = principalId,
                    OrganizationId = organizationId,
                    SequenceLineageId = lineageId,
                    Code = "RPT31001",
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
                    Sequence = 1,
                    Reference = "RPT31001",
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
            return caseId;
        }

        internal sealed record ArtifactRow(Guid Id, string Kind, string State, string OperationKey);

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

    private sealed class MutatingWorkspaceSource(Harness harness) : IGetAssessmentWorkspace
    {
        public async Task<AssessmentWorkspace?> ExecuteAsync(
            GetAssessmentWorkspaceQuery query, CancellationToken cancellationToken = default)
        {
            var workspace = AssessmentWorkspaceTestData.Create(
                AssessmentReportDraftWebTests.FullAssessmentProjection(harness.CaseId) with { CaseVersion = 1 });
            await harness.AddSourceAsync();
            return workspace;
        }
    }

    /// <summary>
    /// The one read model a freeze loads, built from the same accepted
    /// fixtures the routed report tests use so readiness is genuinely met.
    /// </summary>
    private sealed class FakeSnapshotSource : ICaseReportSnapshotSource
    {
        internal static readonly byte[] SignatureBytes = [137, 80, 78, 71, 9, 9, 9, 9];

        private static readonly DateTimeOffset RecordedAtUtc = new(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);
        internal static readonly Guid SignatoryId = Guid.NewGuid();

        private readonly Guid caseId;
        private readonly CaseAssessmentProjection assessment;
        private readonly AssessmentReportProjectionInput projection;
        private readonly Harness.SeededDocument closeUp;
        private readonly Harness.SeededDocument overview;
        private readonly IReadOnlyDictionary<Guid, DocumentVersion> confirmedSources;
        private readonly RepairSpecificationVersion estimate =
            AssessmentReportDraftWebTests.CurrentEstimate();
        private readonly Guid valuationId = Guid.NewGuid();
        private readonly Guid guideValuationId = Guid.NewGuid();
        private decimal engineerValue = 5_000m;
        private Func<CaseAssessmentProjection, CaseAssessmentProjection>? transform;

        public FakeSnapshotSource(
            Guid caseId,
            Harness.SeededDocument closeUp,
            Harness.SeededDocument overview,
            IReadOnlyList<EfAssessmentReportProjectionSource.ConfirmedDocumentRow> confirmed)
        {
            this.caseId = caseId;
            this.closeUp = closeUp;
            this.overview = overview;
            confirmedSources = EfAssessmentReportProjectionSource.ConfirmedImageSources(confirmed);
            assessment = AssessmentReportDraftWebTests.FullAssessmentProjection(caseId);
            projection = AssessmentReportDraftWebTests.ReadyInput(caseId) with
            {
                Assessment = assessment,
                ReportDate = null,
                CurrentEstimate = estimate,
                Signatory = new ReportSignatory("Ed Mawdsley", "ATA VDA AQP", SignatureBytes, "image/png"),
                Photos =
                [
                    Photo(closeUp, CaseAssetReportRole.CloseUp),
                    Photo(overview, CaseAssetReportRole.Overview),
                ],
                Sources = EfAssessmentReportProjectionSource.ReportSources(confirmed),
            };
        }

        public void AcceptEngineerValue(decimal value) => engineerValue = value;

        /// <summary>
        /// Applies <paramref name="revise"/> to the accepted assessment every
        /// later read returns, both to what the report prints and to what
        /// readiness is decided from.
        /// </summary>
        public void TransformAssessment(Func<CaseAssessmentProjection, CaseAssessmentProjection> revise) =>
            transform = revise;

        public Task<CaseReportFreezeInputs?> GetAsync(
            Guid requestedCaseId, ActionActor actor, CaseWorkSelector work, CancellationToken cancellationToken)
        {
            var current = transform?.Invoke(assessment) ?? assessment;
            return Task.FromResult<CaseReportFreezeInputs?>(
                requestedCaseId == caseId
                    // The seeded Case has only its primary work, whose id is the Case's.
                    ? new(projection with { Assessment = current }, Readiness(current), "RPT31001", 1) { WorkId = caseId }
                    : null);
        }

        private CaseReportReadinessInput Readiness(CaseAssessmentProjection current) => new(
            current,
            SignatoryId,
            null,
            [new SignOffEngineerProfile(
                SignatoryId, "Ed Mawdsley", "ATA VDA AQP", SignatureBytes, "image/png", IsDefault: true)],
            estimate,
            Valuation(),
            [Preparation(closeUp, CaseAssetReportRole.CloseUp), Preparation(overview, CaseAssetReportRole.Overview)],
            confirmedSources);

        private AppliedValuation Valuation() => new(
            valuationId, caseId, 1, guideValuationId, RecordedAtUtc,
            new ValuationCalculation(
                engineerValue, false, 0m, engineerValue, null, 0m, [], 0m, 0m, engineerValue),
            engineerValue, "engineer-1", RecordedAtUtc, "Accepted the guide value",
            "case-valuation-calculation/v1");

        private static ReportImageEvidence Photo(
            Harness.SeededDocument document, CaseAssetReportRole role) => new(
                $"{document.OccurrenceId:D}.png", "image/png", document.Content,
                document.Sha256, role, null, CaseAssetRotation.None, CaseAssetCrop.Full,
                document.OccurrenceId, document.VersionId,
                $"box-file-{document.VersionId:N}", $"box-version-{document.VersionId:N}",
                role == CaseAssetReportRole.CloseUp);

        private CaseAssetPreparation Preparation(
            Harness.SeededDocument document, CaseAssetReportRole role) => new(
                caseId, document.OccurrenceId, document.DocumentId, document.VersionId, 1,
                document.Sha256, "image/png", role, null, CaseAssetRotation.None, CaseAssetCrop.Full,
                1, "engineer-1", RecordedAtUtc, role == CaseAssetReportRole.CloseUp);

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

        public async Task<RenderedReportArtifact> RenderAsync(
            AssessmentReportSnapshot snapshot,
            CaseReportArtifactKind kind,
            CancellationToken cancellationToken = default)
        {
            Kinds.Add(kind);
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

        public CaseArtifactCustodyResult Result { get; init; } = new(
            CaseArtifactCustodyDisposition.Unknown, null, null, null, null, null, null, null, null, null, null);

        public Task<CaseArtifactCustodyResult> GetAsync(
            ActionActor actor, Guid caseId, Guid documentId, Guid versionId, Guid occurrenceId,
            CancellationToken cancellationToken)
        {
            LastQuery = (caseId, documentId, versionId, occurrenceId);
            return Task.FromResult(Result);
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
            return Task.FromResult<CaseArtifactCustodyResult?>(Result);
        }
    }

    private static ApprovedMailbox TestMailbox() => new(
        Guid.NewGuid(),
        "reports@collisionengineers.example",
        [ApprovedMailboxRouteScope.StaffSend, ApprovedMailboxRouteScope.SentEvidence],
        ApprovedMailboxState.Approved,
        "identity",
        "inbox",
        "sent",
        IdentityIsBound: true,
        ActivatedAtUtc: new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero),
        Version: 1,
        FolderBindings: [],
        Generation: 3);

    private sealed class FixedRecipientSuggestions(ReportRecipientSuggestions suggestions)
        : IReportRecipientSuggestionQueries
    {
        public Task<ReportRecipientSuggestions?> GetAsync(
            Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<ReportRecipientSuggestions?>(suggestions);
    }

    private sealed class FixedApprovedMailboxes(params ApprovedMailbox[] mailboxes)
        : IApprovedMailboxStore
    {
        public Task<IReadOnlyList<ApprovedMailbox>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ApprovedMailbox>>(mailboxes);

        public Task<ApprovedMailbox> UpdateAsync(
            UpdateApprovedMailboxRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ApprovedMailbox> SetDefaultAsync(
            SetDefaultApprovedMailboxRequest request, CancellationToken cancellationToken) =>
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
