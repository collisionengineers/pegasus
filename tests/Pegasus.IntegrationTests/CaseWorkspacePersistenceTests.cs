using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Address;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
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
                    "5 Repairer Way, Leeds"),
                Vehicle = new(
                    "AB12 CDE",
                    "Ford",
                    "Focus",
                    new(72_850, CaseOdometerUnit.Miles, "repairer", CaseOdometerUnit.Kilometres),
                    new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        [AssessmentVocabulary.VehicleCondition] = "good"
                    }),
                Damage = new([new("left_front_wing", "light", "Scuffed")], null),
                Completeness = new(true, true)
            },
            CancellationToken.None);

        Assert.False(result.WasReplay);
        Assert.Equal(initial.Version + 1, result.Version);
        Assert.Equal(historyBefore + 1, await harness.HistoryCountAsync());
        Assert.Equal(1, await WorkflowEventCountAsync(harness, "case_workspace_saved"));

        Assert.Equal("Jane Example", result.Data.Claimant.Name.Confirmed?.Value);
        Assert.Equal("AB12CDE", result.Data.Vehicle.Registration.Confirmed?.Value);
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
