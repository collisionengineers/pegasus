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
        if (change == "unchanged")
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
        // The freeze must be committed and lock-free by the time Chromium and
        // Box would run: the renderer reads the artifact row on its own
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
            "IX_CaseReportGenerations_CaseId_SnapshotHash",
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
            harness.StaffActor, harness.CaseId, CancellationToken.None);
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
                harness.Request(CaseReportArtifactKind.FeeNote, "case-report-fee-1"),
                CancellationToken.None);

        Assert.Equal(CaseReportGenerationOutcome.Generated, feeNote.Outcome);
        Assert.Equal(report.Generation.Id, feeNote.Generation!.Id);
        Assert.Equal(2, feeNote.Generation.Artifacts.Count);
        Assert.All(
            feeNote.Generation.Artifacts,
            item => Assert.Equal(CaseReportArtifactStatus.Confirmed, item.Status));
        Assert.True(feeNote.Generation.IsFullyConfirmed);
        Assert.Single(await harness.ReadyEventsAsync());
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
            harness.StaffActor, harness.CaseId, CancellationToken.None);
        Assert.Equal(second.Generation.Id, current!.Id);
        Assert.Equal(2, (await harness.Store.ListAsync(
            harness.StaffActor, harness.CaseId, CancellationToken.None)).Count);
        Assert.Equal(2, (await harness.ReadyEventsAsync()).Count);
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

    private sealed class Harness : IAsyncDisposable
    {
        internal const string OperationKey = "case-report-1";
        internal static readonly DateTimeOffset StartUtc = new(2026, 9, 6, 23, 30, 0, TimeSpan.Zero);
        internal static TimeProvider Clock => new FixedTimeProvider(StartUtc);

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

        public static async Task<Harness> CreateAsync()
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
                var staffActor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
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
            EfCaseReportGenerationStore? store = null) => new(
                new RecordingStore(store ?? Store, Sequence),
                new FakeContentSource(this),
                renderer,
                custody,
                custodyStatus ?? new RecordingCustodyStatus(),
                new FixedTimeProvider(StartUtc));

        public EfCaseReportGenerationStore StoreUsing(IDbContextFactory<PegasusDbContext> factory) =>
            new(factory, snapshotSource, new FakeDocumentReader(this), Clock);

        public GenerateCaseReportRequest Request(
            CaseReportArtifactKind kind = CaseReportArtifactKind.AssessmentReport,
            string operationKey = OperationKey) => new(
                StaffActor, CaseId, 1, Lease.Token, operationKey, kind,
                "Generate the immutable case report");

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
            return await source.GetAsync(CaseId, StaffActor, default);
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

        public Task<CaseReportDeliveryPreparationRecord> PrepareDeliveryAsync(CaseReportGenerationRecord generation) =>
            new EfCaseReportDeliveryPreparationStore(Factory, Clock).PrepareAsync(
                new(new(StaffActor, CaseId, 1, Lease.Token, generation.Id, generation.Version, "prepare-report"),
                    new([new StaffMailRecipient("digital@collisionengineers.co.uk", "pegasustest")], [], "Case report"),
                    new string('a', 64)), default);

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
            var lease = await scope.ServiceProvider.GetRequiredService<IEditScopeLeases>().ClaimAsync(
                new(EditScopeKind.StaffAccount, account.Id, account.Version, actor, "change-signatory"),
                default);
            if (change == "disabled")
            {
                await store.DisableAsync(new(
                    actor, account.Id, "Unavailable", "change-signatory", account.Version, lease.Token), default);
            }
            else if (change == "role")
            {
                await store.UpdateAsync(new(
                    actor, account.Id, StaffRole.User, false, null, null, null, false,
                    "change-signatory", account.Version, lease.Token), default);
            }
            else
            {
                await store.UpdateAsync(new(
                    actor, account.Id, StaffRole.Engineer, change != "eligibility",
                    change == "name" ? "Ed M" : "Ed Mawdsley",
                    change == "qualifications" ? "ATA VDA" : "ATA VDA AQP",
                    change == "signature" ? EvidenceBytesOf(7) : SignatureBytes,
                    change != "eligibility", "change-signatory",
                    account.Version, lease.Token), default);
            }
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

        public static byte[] SignatureBytes => FakeSnapshotSource.SignatureBytes;

        public ReportImageEvidence[] RehydratedPhotos(
            CaseReportGenerationSnapshot snapshot) => snapshot.Images
                .Select(image => new ReportImageEvidence(
                    $"{image.OccurrenceId:D}.png", image.ContentType, EvidenceContent[image.Sha256],
                    image.Sha256, image.Role, image.Order, image.Rotation, image.Crop,
                    image.OccurrenceId, image.VersionId, image.BoxFileId, image.BoxVersionId))
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
            await Store.ListAsync(StaffActor, CaseId, CancellationToken.None);

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
                    Type = "Inspection",
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

        public Task<CaseReportFreezeInputs?> GetAsync(
            Guid requestedCaseId, ActionActor actor, CancellationToken cancellationToken) =>
            Task.FromResult<CaseReportFreezeInputs?>(
                requestedCaseId == caseId
                    ? new(projection, Readiness(), "RPT31001", 1)
                    : null);

        private CaseReportReadinessInput Readiness() => new(
            assessment,
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
                $"box-file-{document.VersionId:N}", $"box-version-{document.VersionId:N}");

        private CaseAssetPreparation Preparation(
            Harness.SeededDocument document, CaseAssetReportRole role) => new(
                caseId, document.OccurrenceId, document.DocumentId, document.VersionId, 1,
                document.Sha256, "image/png", role, null, CaseAssetRotation.None, CaseAssetCrop.Full,
                1, "engineer-1", RecordedAtUtc);

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
            ActionActor actor, Guid caseId, CancellationToken cancellationToken) =>
            inner.GetCurrentAsync(actor, caseId, cancellationToken);

        public Task<IReadOnlyList<CaseReportGenerationRecord>> ListAsync(
            ActionActor actor, Guid caseId, CancellationToken cancellationToken) =>
            inner.ListAsync(actor, caseId, cancellationToken);

        public Task<int> MarkStaleAsync(
            Guid caseId, string reasonCode, CancellationToken cancellationToken) =>
            inner.MarkStaleAsync(caseId, reasonCode, cancellationToken);
    }

    private sealed class RecordingRenderer(CaseReportGenerationPersistenceTests.Harness harness)
        : IAssessmentReportRenderer
    {
        public byte[] Pdf { get; } = [0x25, 0x50, 0x44, 0x46, 1, 2, 3];

        public string Sha256 => Convert.ToHexStringLower(SHA256.HashData(Pdf));

        public string EngineVersion => "Fake/1.0; Chromium";

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
        public string EngineVersion => "Fake/1.0; Chromium";

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
