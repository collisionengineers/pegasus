using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
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

        // Nothing changed, and the act leaves no mark of its own: the same version answers.
        var again = await snapshots.FreezeAsync(
            new(caseId, specification.SpecificationId, engineer, RepairSpecificationSnapshotKind.BeforeRestore, "Before a restore"),
            CancellationToken.None);
        Assert.Equal(first.Id, again.Id);

        // Scaled and Sent always leave their own version, unchanged or not.
        var scaled = await snapshots.FreezeAsync(
            new(caseId, specification.SpecificationId, engineer, RepairSpecificationSnapshotKind.Scaled, "Scaled: nothing moved"),
            CancellationToken.None);
        Assert.Equal(2, scaled.Number);
        var sent = await snapshots.FreezeAsync(
            new(caseId, specification.SpecificationId, engineer, RepairSpecificationSnapshotKind.Sent, "As sent on the report"),
            CancellationToken.None);
        Assert.Equal(3, sent.Number);
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
        Assert.Equal(4, repriced.Number);
        Assert.Equal(55m, repriced.Details.BaseHourlyRate);

        var listed = await snapshots.ListAsync(caseId, specification.SpecificationId, CancellationToken.None);
        Assert.Equal([1, 2, 3, 4], listed.Select(version => version.Number));
        var read = await snapshots.GetAsync(caseId, repriced.Id, CancellationToken.None);
        Assert.NotNull(read);
        Assert.Equal(repriced.Origin, read.Origin);
        Assert.Equal("Door skin", read.Lines[0].Description);
        Assert.Null(await snapshots.GetAsync(caseId, Guid.NewGuid(), CancellationToken.None));
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

        var lease = await harness.AcquireLeaseAsync(caseId, 0, engineer, "spec-scale-lease-1");
        var specification = await save.ExecuteAsync(
            new(caseId, lease.Version, engineer, "spec-scale-save", "Recorded the repairer's estimate.",
                lease.Token, null,
                new("Repairer", 80m, null, 20m, Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
                [
                    new("new_part", null, "Door skin", null, 500m, false, "P-1", null, "confirmed", "official", null, Quantity: 1),
                    new("repair", null, "Repair door", 4m, null, false, null, null, "confirmed", "judgement", null),
                ],
                new(RepairSpecificationSourceRoute.Manual, null, null, null)),
            CancellationToken.None);
        var before = EstimateTotals.Compute(specification).Printed.Gross;

        var scaleLease = await harness.AcquireLeaseAsync(caseId, 1, engineer, "spec-scale-lease-2");
        var scaled = await new ScaleRepairSpecification(harness.RepairSpecifications, snapshots).ExecuteAsync(
            new(caseId, scaleLease.Version, engineer, "spec-scale-apply", scaleLease.Token,
                specification.SpecificationId, before * 0.6m, ScalingFloors.Default, 45m),
            CancellationToken.None);

        Assert.True(EstimateTotals.Compute(scaled).Printed.Gross < before);
        Assert.True(scaled.Details.BaseHourlyRate < 80m);
        // Hours never move.
        Assert.Equal(4m, scaled.Lines.Single(line => line.Type == "repair").WorkUnits);
        var versions = await snapshots.ListAsync(caseId, specification.SpecificationId, CancellationToken.None);
        Assert.Equal(
            [RepairSpecificationSnapshotKind.BeforeScaling, RepairSpecificationSnapshotKind.Scaled],
            versions.Select(version => version.Kind));
        Assert.Contains("Repair spec scaled:", versions[1].Origin, StringComparison.Ordinal);

        var removeLease = await harness.AcquireLeaseAsync(caseId, 2, engineer, "spec-scale-lease-3");
        var restored = await new RemoveRepairSpecificationScaling(harness.RepairSpecifications, snapshots).ExecuteAsync(
            new(caseId, removeLease.Version, engineer, "spec-scale-remove", removeLease.Token, specification.SpecificationId),
            CancellationToken.None);

        Assert.Equal(80m, restored.Details.BaseHourlyRate);
        Assert.Equal(500m, restored.Lines.Single(line => line.Type == "new_part").Price);
        Assert.Equal(before, EstimateTotals.Compute(restored).Printed.Gross);
    }
}
