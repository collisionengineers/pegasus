using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Address;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
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
                Damage = new([new("left_front_wing", "light", "Scuffed")], null),
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
                RendererVersion = "playwright/v1",
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
