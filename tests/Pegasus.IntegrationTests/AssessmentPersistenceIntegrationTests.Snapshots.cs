using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Frozen repair specification versions (v28 P43) against SQL Server: the
/// numbering per specification, the unchanged-draft reuse, the acts that
/// always leave their own mark, and the round trip of a version's header
/// and lines.
/// </summary>
public sealed partial class AssessmentPersistenceIntegrationTests
{
    [Fact]
    public async Task RepairSpecificationVersionsAreNumberedAndReuseAnUnchangedDraft()
    {
        await using var harness = await Harness.CreateAsync();
        var caseId = (await harness.AcceptAsync("spec-snapshot-case")).Identity.CaseId;
        var engineer = harness.EngineerActor;
        var jobs = new EfAiJobStore(harness.Factory, harness.Clock);
        var save = new SaveEstimate(harness.RepairSpecifications, jobs, harness.Clock);
        var snapshots = new EfRepairSpecificationSnapshotStore(harness.Factory, harness.Clock);
        var lines = new EstimateLineInput[]
        {
            new("new_part", null, "Door skin", null, 500m, false, "P-1", null, "confirmed", "official", null, Quantity: 1),
            new("repair", null, "Repair door", 4m, null, false, null, null, "confirmed", "judgement", null),
        };

        var lease = await harness.AcquireLeaseAsync(caseId, 0, engineer, "spec-snapshot-lease-1");
        var specification = await save.ExecuteAsync(
            new(caseId, lease.Version, engineer, "spec-snapshot-save-1", "Recorded the repairer's estimate.",
                lease.Token, null,
                new("Repairer", 80m, null, 20m, Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
                lines, new(RepairSpecificationSourceRoute.Manual, null, null, null)),
            CancellationToken.None);

        var first = await snapshots.FreezeAsync(
            new(caseId, specification.SpecificationId, engineer, RepairSpecificationSnapshotKind.BeforeScaling, "Before scaling"),
            CancellationToken.None);
        Assert.Equal(1, first.Number);
        Assert.Equal(2, first.Lines.Count);
        Assert.Equal(80m, first.Details.BaseHourlyRate);
        Assert.Equal(EstimateTotals.Compute(specification).Printed.Gross, first.Gross);
        Assert.False(first.SentOnReport);

        // Restore is an explicit act: even unchanged content gets its own mark.
        var again = await snapshots.FreezeAsync(
            new(caseId, specification.SpecificationId, engineer, RepairSpecificationSnapshotKind.BeforeRestore, "Before a restore"),
            CancellationToken.None);
        Assert.NotEqual(first.Id, again.Id);
        Assert.Equal(2, again.Number);

        // Scaled and Sent always leave their own version, unchanged or not.
        var scaled = await snapshots.FreezeAsync(
            new(caseId, specification.SpecificationId, engineer, RepairSpecificationSnapshotKind.Scaled, "Scaled: nothing moved"),
            CancellationToken.None);
        Assert.Equal(3, scaled.Number);
        var sent = await snapshots.FreezeAsync(
            new(caseId, specification.SpecificationId, engineer, RepairSpecificationSnapshotKind.Sent, "As sent on the report"),
            CancellationToken.None);
        Assert.Equal(4, sent.Number);
        Assert.True(sent.SentOnReport);

        // A changed draft freezes a version of its own.
        var editLease = await harness.AcquireLeaseAsync(caseId, 1, engineer, "spec-snapshot-lease-2");
        await save.ExecuteAsync(
            new(caseId, editLease.Version, engineer, "spec-snapshot-save-2", "Repriced at the agreed rate.",
                editLease.Token, specification.SpecificationId,
                specification.Details with { LabourRate = 55m }, lines, specification.Source),
            CancellationToken.None);
        var repriced = await snapshots.FreezeAsync(
            new(caseId, specification.SpecificationId, engineer, RepairSpecificationSnapshotKind.BeforeScaling, "Before scaling"),
            CancellationToken.None);
        Assert.Equal(5, repriced.Number);
        Assert.Equal(55m, repriced.Details.BaseHourlyRate);

        var listed = await snapshots.ListAsync(caseId, specification.SpecificationId, CancellationToken.None);
        Assert.Equal([1, 2, 3, 4, 5], listed.Select(version => version.Number));
        var read = await snapshots.GetAsync(caseId, repriced.Id, CancellationToken.None);
        Assert.NotNull(read);
        Assert.Equal(repriced.Origin, read.Origin);
        Assert.Equal("Door skin", read.Lines[0].Description);
        Assert.Null(await snapshots.GetAsync(caseId, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task RestoreIsAtomicAndRecordsTheBeforeAndRestoredVersions()
    {
        await using var harness = await Harness.CreateAsync();
        var caseId = (await harness.AcceptAsync("spec-restore-case")).Identity.CaseId;
        var engineer = harness.EngineerActor;
        var jobs = new EfAiJobStore(harness.Factory, harness.Clock);
        var save = new SaveEstimate(harness.RepairSpecifications, jobs, harness.Clock);
        var snapshots = new EfRepairSpecificationSnapshotStore(harness.Factory, harness.Clock);
        var lease = await harness.AcquireLeaseAsync(caseId, 0, engineer, "spec-restore-save-lease");
        var specification = await save.ExecuteAsync(
            new(
                caseId,
                lease.Version,
                engineer,
                "spec-restore-save",
                "Recorded the repairer's estimate.",
                lease.Token,
                null,
                new("Repairer", 80m, null, 20m, Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
                [new("new_part", null, "Door skin", null, 500m, false, "P-1", null, "confirmed", "official", null, Quantity: 1)],
                new(RepairSpecificationSourceRoute.Manual, null, null, null)),
            CancellationToken.None);
        var version = await snapshots.FreezeAsync(
            new(caseId, specification.SpecificationId, engineer, RepairSpecificationSnapshotKind.Imported, "Initial import"),
            CancellationToken.None);
        var restore = new RestoreRepairSpecificationSnapshot(harness.RepairSpecifications);

        await Assert.ThrowsAsync<CaseVersionConflictException>(() =>
            restore.ExecuteAsync(
                new(caseId, lease.Version, engineer, "spec-restore-stale", lease.Token, specification.SpecificationId, version.Id),
                CancellationToken.None));
        Assert.Single(await snapshots.ListAsync(caseId, specification.SpecificationId, CancellationToken.None));

        var restoreLease = await harness.AcquireLeaseAsync(caseId, 1, engineer, "spec-restore-lease");
        await restore.ExecuteAsync(
            new(caseId, restoreLease.Version, engineer, "spec-restore", restoreLease.Token, specification.SpecificationId, version.Id),
            CancellationToken.None);

        var versions = await snapshots.ListAsync(caseId, specification.SpecificationId, CancellationToken.None);
        Assert.Equal(
            [
                RepairSpecificationSnapshotKind.Imported,
                RepairSpecificationSnapshotKind.BeforeRestore,
                RepairSpecificationSnapshotKind.Restored,
            ],
            versions.Select(item => item.Kind));
        Assert.Contains("Restored from v1", versions[^1].Origin, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Engineer's Apply and Remove scaling through the real store: the
    /// draft is frozen, the scaled figures are persisted, and removing the
    /// scaling puts the specification back exactly as it stood.
    /// </summary>
    [Fact]
    public async Task ScalingARepairSpecificationIsFrozenAndReversible()
    {
        await using var harness = await Harness.CreateAsync();
        var caseId = (await harness.AcceptAsync("spec-scale-case")).Identity.CaseId;
        var engineer = harness.EngineerActor;
        var jobs = new EfAiJobStore(harness.Factory, harness.Clock);
        var save = new SaveEstimate(harness.RepairSpecifications, jobs, harness.Clock);
        var snapshots = new EfRepairSpecificationSnapshotStore(harness.Factory, harness.Clock);
        var rateCard = await new EfLabourRateCardStore(harness.Factory, harness.Clock).SaveAsync(
            new(Guid.NewGuid(), "Panel and paint", 80m, true, 0,
                ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
                "Create scaling rate card", "spec-scale-rate-create"),
            CancellationToken.None);

        var lease = await harness.AcquireLeaseAsync(caseId, 0, engineer, "spec-scale-lease-1");
        var specification = await save.ExecuteAsync(
            new(caseId, lease.Version, engineer, "spec-scale-save", "Recorded the repairer's estimate.",
                lease.Token, null,
                new("Repairer", 80m, null, 20m, Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
                [
                    new("new_part", null, "Door skin", null, 500m, false, "P-1", null, "confirmed", "official", null, Quantity: 1),
                    new("repair", null, "Repair door", 4m, null, false, null, null, "confirmed", "judgement", null),
                ],
                new(RepairSpecificationSourceRoute.Manual, null, null, null))
            {
                SelectedRateCardId = rateCard.Id,
                SelectedRateCardVersion = rateCard.Version,
            },
            CancellationToken.None);
        var before = EstimateTotals.Compute(specification).Printed.Gross;

        var saveValuation = new SaveValuation(harness.Valuations);
        var valueLease = await harness.AcquireLeaseAsync(caseId, 1, engineer, "spec-scale-value-lease");
        await saveValuation.ExecuteAsync(
            new SaveValuationRequest(
                caseId,
                valueLease.Version,
                engineer,
                "spec-scale-value",
                "Recorded the confirmed Engineer's Value.",
                valueLease.Token,
                new(
                    ValuationSource.EngineersValue,
                    new DateOnly(2031, 5, 8),
                    new TimeOnly(9, 0),
                    42000,
                    5000m,
                    3000m)),
            CancellationToken.None);

        var scaleLease = await harness.AcquireLeaseAsync(caseId, 2, engineer, "spec-scale-lease-2");
        var assessment = new EfCaseAssessmentStore(harness.Factory, harness.Clock, harness.RepairSpecifications);
        var scaled = await new SaveAndScaleRepairSpecification(assessment, harness.RepairSpecifications).ExecuteAsync(
            new(
                new SaveEstimateRequest(
                    caseId,
                    scaleLease.Version,
                    engineer,
                    "spec-scale-apply",
                    "Repair spec scaled",
                    scaleLease.Token,
                    specification.SpecificationId,
                    specification.Details,
                    specification.Lines.Select(RepairSpecificationScaling.ToInput).ToArray(),
                    specification.Source,
                    ExistingLineIds: specification.Lines.Select(line => (Guid?)line.Id).ToArray())
                {
                    SelectedRateCardId = rateCard.Id,
                    SelectedRateCardVersion = rateCard.Version,
                },
                // The spec is about £984 against a £5,000 Engineer's Value, so a
                // 45% target sits above it and scaling would have nothing to do.
                // 15% asks for a real reduction, which is what this test proves.
                15m,
                ScalingFloors.Default),
            CancellationToken.None);

        Assert.True(EstimateTotals.Compute(scaled).Printed.Gross < before);
        Assert.True(scaled.Details.BaseHourlyRate < 80m);
        Assert.Null(scaled.Details.Rate);
        // Hours never move.
        Assert.Equal(4m, scaled.Lines.Single(line => line.Type == "repair").WorkUnits);
        var versions = await snapshots.ListAsync(caseId, specification.SpecificationId, CancellationToken.None);
        Assert.Equal(
            [RepairSpecificationSnapshotKind.BeforeScaling, RepairSpecificationSnapshotKind.Scaled],
            versions.Select(version => version.Kind));
        Assert.Equal(new EstimateRateSnapshot(rateCard.Id, rateCard.Version, 80m), versions[0].Details.Rate);
        Assert.Null(versions[1].Details.Rate);
        Assert.Contains("Repair spec scaled:", versions[1].Origin, StringComparison.Ordinal);

        var removeLease = await harness.AcquireLeaseAsync(caseId, 3, engineer, "spec-scale-lease-3");
        var restored = await new RemoveRepairSpecificationScaling(harness.RepairSpecifications).ExecuteAsync(
            new(caseId, removeLease.Version, engineer, "spec-scale-remove", removeLease.Token, specification.SpecificationId),
            CancellationToken.None);

        Assert.Equal(80m, restored.Details.BaseHourlyRate);
        Assert.Equal(new EstimateRateSnapshot(rateCard.Id, rateCard.Version, 80m), restored.Details.Rate);
        Assert.Equal(500m, restored.Lines.Single(line => line.Type == "new_part").Price);
        Assert.Equal(before, EstimateTotals.Compute(restored).Printed.Gross);
        var versionsAfterRemoval = await snapshots.ListAsync(caseId, specification.SpecificationId, CancellationToken.None);
        Assert.Equal(RepairSpecificationSnapshotKind.ScalingRemoved, versionsAfterRemoval[^1].Kind);

        var laterEditLease = await harness.AcquireLeaseAsync(caseId, 4, engineer, "spec-scale-lease-4");
        var later = await save.ExecuteAsync(
            new(caseId, laterEditLease.Version, engineer, "spec-scale-later-edit", "Revised after scaling removal.",
                laterEditLease.Token, specification.SpecificationId,
                restored.Details with { LabourRate = 72m },
                restored.Lines.Select(RepairSpecificationScaling.ToInput).ToArray(),
                restored.Source,
                ExistingLineIds: restored.Lines.Select(line => (Guid?)line.Id).ToArray()),
            CancellationToken.None);
        Assert.Equal(72m, later.Details.BaseHourlyRate);

        var secondRemovalLease = await harness.AcquireLeaseAsync(caseId, 5, engineer, "spec-scale-lease-5");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RemoveRepairSpecificationScaling(harness.RepairSpecifications).ExecuteAsync(
                new(caseId, secondRemovalLease.Version, engineer, "spec-scale-remove-again", secondRemovalLease.Token, specification.SpecificationId),
                CancellationToken.None));
    }
}
