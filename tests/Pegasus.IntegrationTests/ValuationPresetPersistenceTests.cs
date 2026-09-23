using Microsoft.EntityFrameworkCore;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The maintained valuation presets and the adoption of a calculated
/// Engineer's Value, proved against LocalDB.
/// </summary>
/// <remarks>
/// A partial of the assessment persistence tests so the adoption reuses that
/// file's <c>Harness</c> — the one accepted-case fixture with its lease,
/// version and assessment read — rather than seeding a second copy of it.
/// </remarks>
public sealed partial class AssessmentPersistenceIntegrationTests
{
    private static readonly Guid TowBarPresetId =
        Guid.Parse("00000000-0000-4000-8000-00000000f001");
    private static readonly Guid DecalsPresetId =
        Guid.Parse("00000000-0000-4000-8000-00000000f003");

    /// <summary>
    /// The five approved additions arrive with the schema. Nothing in Core or
    /// Infrastructure seeds them, so this is the only assertion of what they
    /// are.
    /// </summary>
    [Fact]
    public async Task TheApprovedValuationPresetsArriveWithTheSchema()
    {
        await using var harness = await Harness.CreateAsync();
        var presets = await new ListValuationPresets(
                new EfValuationPresetStore(harness.Factory, harness.Clock))
            .ExecuteAsync(harness.EngineerActor, CancellationToken.None);

        Assert.Equal(
            [
                ("Camper conversion", 0m),
                ("Decals", 500m),
                ("Driving tuition", 500m),
                ("PCO plated", 1500m),
                ("Tow bar", 300m)
            ],
            presets.Select(preset => (preset.Label, preset.SuggestedAmount)));
        Assert.All(
            presets,
            preset =>
            {
                Assert.True(preset.Active);
                Assert.Equal(1, preset.Version);
                Assert.Equal("system:v1-foundation", preset.UpdatedBy);
            });
        Assert.Equal(300m, presets.Single(preset => preset.Id == TowBarPresetId).SuggestedAmount);
    }

    /// <summary>
    /// Editing a preset versions it, disabling keeps it readable, a duplicate
    /// label is refused, and an expected version that has moved is refused
    /// with the version that is now current.
    /// </summary>
    [Fact]
    public async Task EditingVersionsAPresetAndDisablingKeepsItReadable()
    {
        await using var harness = await Harness.CreateAsync();
        var store = new EfValuationPresetStore(harness.Factory, harness.Clock);
        var save = new SaveValuationPreset(store);
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

        var edited = await save.ExecuteAsync(
            new(
                TowBarPresetId,
                "Tow bar",
                350m,
                Active: true,
                ExpectedVersion: 1,
                administrator,
                "valuation-preset-edit"),
            CancellationToken.None);
        Assert.Equal(2, edited.Version);
        Assert.Equal(350m, edited.SuggestedAmount);
        Assert.Equal(administrator.SubjectId, edited.UpdatedBy);

        var disabled = await save.ExecuteAsync(
            new(
                DecalsPresetId,
                "Decals",
                500m,
                Active: false,
                ExpectedVersion: 1,
                administrator,
                "valuation-preset-disable"),
            CancellationToken.None);
        Assert.False(disabled.Active);
        Assert.Equal(2, disabled.Version);

        await using (var historyContext = await harness.Factory.CreateDbContextAsync())
        {
            var history = await historyContext.ActionHistory.SingleAsync(item =>
                item.AggregateType == "valuation_preset"
                && item.CorrelationId == "valuation-preset-edit");
            Assert.Equal(administrator.SubjectId, history.ActorSubjectId);
            Assert.Equal("Staff", history.ActorKind);
            Assert.Equal("valuation-preset-edit", history.CorrelationId);
            Assert.Null(history.Reason);
            Assert.NotEqual(default, history.OccurredAtUtc);
            Assert.NotNull(history.BeforeJson);
            Assert.NotNull(history.AfterJson);
        }

        var presets = await store.ListAsync(CancellationToken.None);
        Assert.Equal(5, presets.Count);
        Assert.False(presets.Single(preset => preset.Id == DecalsPresetId).Active);

        var duplicate = await Assert.ThrowsAsync<ValuationPresetException>(() =>
            save.ExecuteAsync(
                new(
                    Guid.NewGuid(),
                    "  tow BAR  ",
                    100m,
                    Active: true,
                    ExpectedVersion: 0,
                    administrator,
                    "valuation-preset-duplicate"),
                CancellationToken.None));
        Assert.Equal(ValuationPresetError.DuplicateLabel, duplicate.Error);

        var stale = await Assert.ThrowsAsync<ValuationPresetException>(() =>
            save.ExecuteAsync(
                new(
                    TowBarPresetId,
                    "Tow bar",
                    400m,
                    Active: true,
                    ExpectedVersion: 1,
                    administrator,
                    "valuation-preset-stale"),
                CancellationToken.None));
        Assert.Equal(ValuationPresetError.VersionConflict, stale.Error);
        Assert.Equal(2, stale.CurrentVersion);

        var missing = await Assert.ThrowsAsync<ValuationPresetException>(() =>
            save.ExecuteAsync(
                new(
                    Guid.NewGuid(),
                    "Unknown preset",
                    100m,
                    Active: true,
                    ExpectedVersion: 3,
                    administrator,
                    "valuation-preset-missing"),
                CancellationToken.None));
        Assert.Equal(ValuationPresetError.NotFound, missing.Error);
    }

    /// <summary>
    /// Removing a preset is soft: the maintained list drops it while the row,
    /// its history and its recorded valuations stay. A retried post replays
    /// the same removal, and afterwards neither a second removal nor an edit
    /// is accepted.
    /// </summary>
    [Fact]
    public async Task RemovingAPresetHidesItFromTheListAndRefusesFurtherChanges()
    {
        await using var harness = await Harness.CreateAsync();
        var store = new EfValuationPresetStore(harness.Factory, harness.Clock);
        var remove = new RemoveValuationPreset(store);
        var save = new SaveValuationPreset(store);
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var request = new RemoveValuationPresetRequest(
            DecalsPresetId,
            ExpectedVersion: 1,
            administrator,
            "valuation-preset-remove",
            "The addition is no longer offered.");

        var removed = await remove.ExecuteAsync(request, CancellationToken.None);
        Assert.Equal(2, removed.Version);
        Assert.NotNull(removed.RemovedAtUtc);
        Assert.Equal(administrator.SubjectId, removed.UpdatedBy);

        var presets = await store.ListAsync(CancellationToken.None);
        Assert.Equal(4, presets.Count);
        Assert.DoesNotContain(DecalsPresetId, presets.Select(preset => preset.Id));

        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var history = await context.ActionHistory.SingleAsync(item =>
                item.AggregateType == "valuation_preset"
                && item.CorrelationId == "valuation-preset-remove");
            Assert.Equal("valuation_preset_removed", history.EventKind);
            Assert.Equal(administrator.SubjectId, history.ActorSubjectId);
            Assert.Equal("The addition is no longer offered.", history.Reason);
            Assert.NotNull(history.BeforeJson);
            Assert.NotNull(history.AfterJson);

            // The row itself is kept, which is what lets history and any
            // recorded valuation that names this preset stay readable.
            Assert.NotNull(await context.Set<ValuationPresetEntity>()
                .SingleOrDefaultAsync(item => item.Id == DecalsPresetId));
        }

        var replayed = await remove.ExecuteAsync(request, CancellationToken.None);
        Assert.Equal(removed, replayed);

        var again = await Assert.ThrowsAsync<ValuationPresetException>(() =>
            remove.ExecuteAsync(
                request with { OperationKey = "valuation-preset-remove-again" },
                CancellationToken.None));
        Assert.Equal(ValuationPresetError.Removed, again.Error);

        var edited = await Assert.ThrowsAsync<ValuationPresetException>(() =>
            save.ExecuteAsync(
                new(
                    DecalsPresetId,
                    "Decals",
                    600m,
                    Active: true,
                    ExpectedVersion: 2,
                    administrator,
                    "valuation-preset-edit-removed"),
                CancellationToken.None));
        Assert.Equal(ValuationPresetError.Removed, edited.Error);
    }

    /// <summary>
    /// A retried post replays its own recorded result; the same key carrying a
    /// different request is a conflict, not a second edit.
    /// </summary>
    [Fact]
    public async Task ARepeatedPresetOperationKeyReplaysAndADifferentRequestConflicts()
    {
        await using var harness = await Harness.CreateAsync();
        var save = new SaveValuationPreset(
            new EfValuationPresetStore(harness.Factory, harness.Clock));
        var administrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var request = new SaveValuationPresetRequest(
            Guid.NewGuid(),
            "Roof rack",
            125m,
            Active: true,
            ExpectedVersion: 0,
            administrator,
            "valuation-preset-create");

        var created = await save.ExecuteAsync(request, CancellationToken.None);
        var replayed = await save.ExecuteAsync(request, CancellationToken.None);

        Assert.Equal(created, replayed);

        var conflict = await Assert.ThrowsAsync<ValuationPresetException>(() =>
            save.ExecuteAsync(
                request with { SuggestedAmount = 200m },
                CancellationToken.None));
        Assert.Equal(ValuationPresetError.OperationConflict, conflict.Error);

        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.Equal(
            1,
            await context.Set<ValuationPresetEntity>()
                .CountAsync(item => item.Id == request.PresetId));
    }

    /// <summary>
    /// The adoption is part of the one Case Save (23 September 2026) and of
    /// its one transaction: the accepted figure reaches the confirmed
    /// <c>assessment.values.engineer</c> field and the whole ordered
    /// calculation reaches the snapshot table, or neither does. A retried
    /// operation key replays that same save, a preset that moved underneath
    /// the form is refused before anything is written, and returning to an
    /// earlier calculation later is a new adoption.
    /// </summary>
    [Fact]
    public async Task ApplyingAValuationStoresTheEngineersValueAndItsCalculationSnapshot()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("valuation-apply-accept-case");
        var caseId = outcome.Identity.CaseId;
        var engineer = harness.EngineerActor;
        var presets = new EfValuationPresetStore(harness.Factory, harness.Clock);
        var workspace = new EfCaseWorkspaceStore(harness.Factory, harness.Clock);
        var preview = new PreviewValuationCalculation(harness.Valuations);
        long version = 0;

        async Task<CaseEditLease> LeaseAsync(string key) =>
            await harness.AcquireLeaseAsync(caseId, version, engineer, key);

        var guideLease = await LeaseAsync("valuation-apply-guide-lease");
        var guide = await new SaveValuation(harness.Valuations).ExecuteAsync(
            new(
                caseId,
                guideLease.Version,
                engineer,
                "valuation-apply-guide",
                "Recorded the Glass's guide figure.",
                guideLease.Token,
                new(
                    ValuationSource.Glasses,
                    new DateOnly(2031, 5, 8),
                    new TimeOnly(9, 0),
                    42000,
                    3100m,
                    2800m,
                    new DateOnly(2031, 5, 1))),
            CancellationToken.None);
        version++;
        Assert.Null(await ReadEngineersValueAsync(harness, caseId));
        var guideStamp = (await harness.Valuations.ReadBasisAsync(
            caseId,
            guide.ValuationId,
            CancellationToken.None)).GuideValuationStampUtc;

        var selection = new ValuationCalculationSelection(
            guide.ValuationId,
            CommercialVat: true,
            PriorTotalLossPercentage: 0.20m,
            [new(TowBarPresetId, 1, null, 300m)],
            ConditionDeduction: 100m);

        var previewed = await preview.ExecuteAsync(
            new(caseId, engineer, selection),
            CancellationToken.None);
        Assert.Equal(3176m, previewed.Calculation.Proposal);
        Assert.Equal("Tow bar", Assert.Single(previewed.Calculation.Additions).Label);
        Assert.Null(await ReadEngineersValueAsync(harness, caseId));

        // A preset that moved after the form was rendered is refused, and the
        // refusal leaves the Case exactly as it was.
        var presetAdministrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        await new SaveValuationPreset(presets).ExecuteAsync(
            new(
                TowBarPresetId,
                "Tow bar",
                350m,
                Active: true,
                ExpectedVersion: 1,
                presetAdministrator,
                "valuation-apply-preset-edit"),
            CancellationToken.None);
        var applyLease = await LeaseAsync("valuation-apply-lease");
        var stale = await Assert.ThrowsAsync<ValuationPresetException>(() =>
            workspace.SaveAsync(
                AdoptRequest(applyLease, selection, "valuation-apply-stale"),
                CancellationToken.None));
        Assert.Equal(ValuationPresetError.VersionConflict, stale.Error);
        Assert.Equal(2, stale.CurrentVersion);
        Assert.Null(await ReadEngineersValueAsync(harness, caseId));

        // The refusal left the Case exactly as it was, edit lease and version
        // included, so the corrected save carries on with the same lease.
        selection = selection with { Additions = [new(TowBarPresetId, 2, null, 300m)] };
        var request = AdoptRequest(applyLease, selection, "valuation-apply");
        var saved = await workspace.SaveAsync(request, CancellationToken.None);
        version++;
        Assert.Equal(version, saved.Version);

        var applied = Assert.Single(await new ListAppliedValuations(harness.Valuations)
            .ExecuteAsync(caseId, CancellationToken.None));
        Assert.Equal(3176m, applied.AcceptedEngineerValue);
        Assert.Equal(guide.ValuationId, applied.GuideValuationId);
        Assert.Equal(guideStamp, applied.GuideValuationStampUtc);
        Assert.Equal(3100m, applied.Calculation.GuideRetailValue);
        Assert.True(applied.Calculation.CommercialVatApplied);
        Assert.Equal(620m, applied.Calculation.CommercialVatAmount);
        Assert.Equal(744m, applied.Calculation.PriorTotalLossAmount);
        Assert.Equal(350m, Assert.Single(applied.Calculation.Additions).SuggestedAmount);
        Assert.Equal(300m, Assert.Single(applied.Calculation.Additions).Amount);
        Assert.Equal(version, applied.CaseVersion);
        Assert.Equal(ValuationCalculationPolicy.AppliedReason, applied.Reason);
        Assert.Equal(ValuationCalculationPolicy.PolicyStamp, applied.CalculationPolicyVersion);

        var replayed = await workspace.SaveAsync(request, CancellationToken.None);
        Assert.True(replayed.WasReplay);
        Assert.Equal(version, replayed.Version);

        var field = Assert.IsType<AssessmentFieldValue>(
            await ReadEngineersValueAsync(harness, caseId));
        Assert.Equal("3176.00", field.Value);
        Assert.Equal(engineer.SubjectId, field.ConfirmedBy);

        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var snapshot = await context.Set<AppliedValuationSnapshotEntity>()
                .SingleAsync(item => item.CaseId == caseId);
            Assert.Equal(applied.Id, snapshot.Id);
            Assert.Equal(3176m, snapshot.AcceptedEngineerValue);
            Assert.Equal(engineer.SubjectId, snapshot.AcceptedBy);
            Assert.Contains("\"proposal\":3176", snapshot.SnapshotJson, StringComparison.Ordinal);
            // One Save, one workflow event: the adoption is the save's own
            // action-history entry, named in its history line.
            var saveEvent = await context.CaseWorkflowEvents.SingleAsync(
                item => item.CaseId == caseId && item.OperationKey == "valuation-apply");
            Assert.Equal("case_workspace_saved", saveEvent.EventType);
            Assert.Contains("Engineer's Value applied", saveEvent.Reason, StringComparison.Ordinal);
            Assert.Equal(
                1,
                await context.ActionHistory.CountAsync(
                    item => item.AggregateType == "case_applied_valuation"
                        && item.EventKind == "valuation_applied"
                        && item.CorrelationId == "valuation-apply"));
        }

        // A later save with changed inputs records its own calculated proposal
        // as a further snapshot row.
        harness.Advance(TimeSpan.FromMinutes(1));
        var correctionLease = await LeaseAsync("valuation-correction-lease");
        await workspace.SaveAsync(
            AdoptRequest(correctionLease, selection with { ConditionDeduction = 26m }, "valuation-correction"),
            CancellationToken.None);
        version++;
        Assert.Equal(
            "3250.00",
            Assert.IsType<AssessmentFieldValue>(
                await ReadEngineersValueAsync(harness, caseId)).Value);

        // Returning to the earlier calculation is a new adoption, not a repeat
        // of the first: the Case version it produced is part of what it is.
        harness.Advance(TimeSpan.FromMinutes(1));
        var returnLease = await LeaseAsync("valuation-return-lease");
        await workspace.SaveAsync(
            AdoptRequest(returnLease, selection, "valuation-return"),
            CancellationToken.None);
        version++;
        Assert.Equal(
            "3176.00",
            Assert.IsType<AssessmentFieldValue>(
                await ReadEngineersValueAsync(harness, caseId)).Value);

        // The Case's own recorded additions travel with the basis, which is
        // what makes a recorded valuation immune to later preset maintenance:
        // re-applying reads them back instead of resolving the preset again.
        var recordedBasis = await harness.Valuations.ReadBasisAsync(
            caseId,
            guide.ValuationId,
            CancellationToken.None);
        Assert.Equal(
            [(TowBarPresetId, 2L, "Tow bar")],
            recordedBasis.RecordedAdditions.Select(item =>
                (item.PresetId, item.PresetVersion, item.Label)));

        var history = await new ListAppliedValuations(harness.Valuations)
            .ExecuteAsync(caseId, CancellationToken.None);
        Assert.Equal(
            [3176m, 3250m, 3176m],
            history.Select(item => item.AcceptedEngineerValue));
        Assert.Equal(applied.Id, history[^1].Id);

        SaveCaseWorkspaceRequest AdoptRequest(
            CaseEditLease lease,
            ValuationCalculationSelection chosen,
            string operationKey) => new(
            caseId,
            lease.Version,
            engineer,
            operationKey,
            null,
            lease.Token)
        {
            Valuation = new([], chosen)
        };
    }

}
