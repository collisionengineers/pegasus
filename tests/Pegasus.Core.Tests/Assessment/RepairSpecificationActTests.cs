using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Assessment;

/// <summary>
/// The Engineer's acts on a repair specification (v28, ruled 20 September
/// 2026): target-% scaling with its floors, the line-by-line comparison
/// Compare and Supplementary read, and the words the record and the report
/// carry.
/// </summary>
public sealed class RepairSpecificationActTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 21, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly ActionActor Engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

    [Fact]
    public void ScalingLowersPricesMaterialsAndTheRateButNeverTheHours()
    {
        var specification = Estimate(
            Header(rate: 80m),
            Line("new_part", price: 400m, quantity: 2),
            Line("repair", workUnits: 4m),
            Line("paint_repair", paintWorkUnits: 3m, materials: 200m));
        var before = EstimateTotals.Compute(specification).Printed.Gross;

        var result = RepairSpecificationScaling.Scale(specification, before * 0.6m, ScalingFloors.Default);

        Assert.True(result.GrossAfter < before);
        Assert.Equal(before, result.GrossBefore);
        // Every hour the Engineer recorded survives the scaling.
        Assert.Equal([4m, 3m], [result.Lines[1].WorkUnits, result.Lines[2].PaintWorkUnits]);
        Assert.True(result.Lines[0].Price < 400m);
        Assert.True(result.Lines[2].Materials < 200m);
        Assert.True(result.Details.BaseHourlyRate < 80m);
    }

    [Fact]
    public void ScalingStopsAtTheFloorsAndNeverBelowThem()
    {
        var specification = Estimate(
            Header(rate: 80m),
            Line("new_part", price: 1_000m),
            Line("repair", workUnits: 10m));
        var floors = new ScalingFloors(50m, 65m);

        var (floor, asEstimated) = RepairSpecificationScaling.Range(specification, floors);
        Assert.True(floor > 0m);
        Assert.True(asEstimated > floor);

        // A target below what the floors allow lands on the floor, not under it.
        var result = RepairSpecificationScaling.Scale(specification, 1m, floors);
        Assert.Equal(floor, result.GrossAfter);
        Assert.Equal(650m, result.Lines[0].Price);
        Assert.Equal(50m, result.Details.BaseHourlyRate);
        Assert.Equal(0.65m, decimal.Round(result.PriceFactor, 2));

        // A rate already under its floor is left alone rather than lifted.
        var cheap = Estimate(Header(rate: 40m), Line("repair", workUnits: 1m));
        Assert.Equal(40m, RepairSpecificationScaling.Scale(cheap, 1m, floors).Details.BaseHourlyRate);
    }

    [Fact]
    public void ScalingRefusesASpecificationWithNoLinesAndValidatesItsFloors()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RepairSpecificationScaling.Scale(Estimate(Header(rate: 50m)), 100m, ScalingFloors.Default));
        Assert.Throws<ArgumentException>(() => ScalingFloors.Validate(new(-1m, 50m)));
        Assert.Throws<ArgumentException>(() => ScalingFloors.Validate(new(50m, 120m)));
        Assert.Equal((50m, 65m), (ScalingFloors.Default.LabourRatePerHour, ScalingFloors.Default.PricePercent));
    }

    [Fact]
    public void ComparisonMatchesLinesByPartNumberOrDescriptionAndNamesTheChangedCells()
    {
        var from = Estimate(
            Header(rate: 48m),
            Line("new_part", price: 412.50m, quantity: 1) with { Description = "Rear bumper cover", PartNumber = "EX-1001" },
            Line("repair", workUnits: 2m) with { Description = "Repair boot floor" });
        var to = Estimate(
            Header(rate: 48m),
            Line("new_part", price: 538m, quantity: 1) with { Description = "Rear bumper cover and reinforcement", PartNumber = "EX-1001" },
            Line("paint_repair", paintWorkUnits: 3m) with { Description = "Paint bumper" });

        var diff = RepairSpecificationComparison.Compare(from, to);

        var changed = Assert.Single(diff.Changed);
        Assert.Equal("Rear bumper cover and reinforcement", changed.To.Description);
        Assert.Contains("unit amount", changed.ChangedFields);
        Assert.Contains("description", changed.ChangedFields);
        Assert.Equal("Paint bumper", Assert.Single(diff.Added).Description);
        Assert.Equal("Repair boot floor", Assert.Single(diff.Removed).Description);
        Assert.Equal(diff.GrossTo - diff.GrossFrom, diff.GrossDelta);
    }

    [Fact]
    public void TheSupplementaryStatementNamesWhatChangedAndHowTheCostMoved()
    {
        var from = Estimate(Header(rate: 48m), Line("new_part", price: 100m) with { Description = "Bumper" });
        var to = Estimate(
            Header(rate: 48m),
            Line("new_part", price: 100m) with { Description = "Bumper" },
            Line("new_part", price: 250m) with { Description = "Reinforcement bar" });

        var statement = RepairSpecificationComparison.SupplementaryStatement(
            RepairSpecificationComparison.Compare(from, to), "dismantle");

        Assert.StartsWith(
            "Following dismantling of the vehicle, further damage was identified and the following additional items are now required: Reinforcement bar.",
            statement,
            StringComparison.Ordinal);
        Assert.Contains("increased from", statement, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() =>
            RepairSpecificationComparison.SupplementaryStatement(
                RepairSpecificationComparison.Compare(from, to), "not-a-reason"));
        Assert.Equal(4, RepairSpecificationComparison.SupplementaryReasons.Count);
    }

    [Fact]
    public void ARemovedLineAndARevisedRepairTimeReadInTheStatement()
    {
        var from = Estimate(
            Header(rate: 48m),
            Line("repair", workUnits: 2m) with { Description = "Repair wing" },
            Line("new_part", price: 600m) with { Description = "Front wing" });
        var to = Estimate(Header(rate: 48m), Line("repair", workUnits: 5m) with { Description = "Repair wing" });

        var statement = RepairSpecificationComparison.SupplementaryStatement(
            RepairSpecificationComparison.Compare(from, to), "inspect");

        Assert.Contains("The repair time for Repair wing (2 h → 5 h) has been revised.", statement, StringComparison.Ordinal);
        Assert.Contains("Front wing is no longer required.", statement, StringComparison.Ordinal);
        Assert.Contains("reduced from", statement, StringComparison.Ordinal);
    }

    [Fact]
    public void TheContractRepairSentenceNamesTheAgreedSumAsTheCap()
    {
        Assert.Equal(
            "A contract repair has been agreed for the total sum of £1,250.00. Costs cannot increase above this figure.",
            RepairSpecificationWording.ContractRepair(1_250m));
        Assert.Equal(AssessmentFieldType.Money,
            AssessmentVocabulary.Definitions[AssessmentVocabulary.SettlementContractSum].Type);
    }

    [Fact]
    public async Task ApplyFreezesTheOutgoingDraftSavesTheScaledSpecAndFreezesItAgain()
    {
        var specification = Estimate(
            Header(rate: 80m),
            Line("new_part", price: 500m),
            Line("repair", workUnits: 4m));
        var store = new RecordingStore(specification);
        var snapshots = new RecordingSnapshots();
        var target = EstimateTotals.Compute(specification).Printed.Gross * 0.7m;

        await new ScaleRepairSpecification(store, snapshots).ExecuteAsync(
            new(CaseId, 3, Engineer, "op-scale", new string('l', 32), specification.SpecificationId, target, ScalingFloors.Default, 40m),
            CancellationToken.None);

        Assert.Equal(
            [RepairSpecificationSnapshotKind.BeforeScaling, RepairSpecificationSnapshotKind.Scaled],
            snapshots.Frozen.Select(request => request.Kind));
        var saved = Assert.Single(store.Saves);
        Assert.Equal("estimate_scaled", saved.EventType);
        Assert.Contains("Repair spec scaled:", saved.Reason, StringComparison.Ordinal);
        Assert.Contains("(40.0 % of value)", saved.Reason, StringComparison.Ordinal);
        // The saved lines keep every line identity, so nothing is re-created.
        Assert.Equal(
            specification.Lines.Select(line => (Guid?)line.Id),
            saved.ExistingLineIds!);
    }

    [Fact]
    public async Task RemoveScalingReturnsTheDraftToTheVersionFrozenBeforeTheLastApply()
    {
        var specification = Estimate(Header(rate: 40m), Line("new_part", price: 100m));
        var original = Estimate(Header(rate: 80m), Line("new_part", price: 500m));
        var store = new RecordingStore(specification);
        var snapshots = new RecordingSnapshots
        {
            Versions =
            [
                Snapshot(1, RepairSpecificationSnapshotKind.BeforeScaling, specification.SpecificationId, original),
                Snapshot(2, RepairSpecificationSnapshotKind.Scaled, specification.SpecificationId, specification),
            ],
        };

        await new RemoveRepairSpecificationScaling(store, snapshots).ExecuteAsync(
            new(CaseId, 3, Engineer, "op-remove", new string('l', 32), specification.SpecificationId),
            CancellationToken.None);

        var saved = Assert.Single(store.Saves);
        Assert.Equal("estimate_scaling_removed", saved.EventType);
        Assert.Equal(80m, saved.Details.BaseHourlyRate);
        Assert.Equal(500m, Assert.Single(saved.Lines).Price);

        // Nothing to return to is a refusal, not a silent no-op.
        snapshots.Versions = [];
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RemoveRepairSpecificationScaling(store, snapshots).ExecuteAsync(
                new(CaseId, 3, Engineer, "op-remove-2", new string('l', 32), specification.SpecificationId),
                CancellationToken.None));
    }

    [Fact]
    public async Task RestoreFreezesTheOutgoingDraftFirstAndRefusesAnotherSpecificationsVersion()
    {
        var specification = Estimate(Header(rate: 40m), Line("new_part", price: 100m));
        var restored = Estimate(Header(rate: 62m), Line("new_part", price: 300m));
        var store = new RecordingStore(specification);
        var version = Snapshot(4, RepairSpecificationSnapshotKind.Imported, specification.SpecificationId, restored);
        var snapshots = new RecordingSnapshots { Versions = [version] };

        await new RestoreRepairSpecificationSnapshot(store, snapshots).ExecuteAsync(
            new(CaseId, 3, Engineer, "op-restore", new string('l', 32), specification.SpecificationId, version.Id),
            CancellationToken.None);

        Assert.Equal(RepairSpecificationSnapshotKind.BeforeRestore, Assert.Single(snapshots.Frozen).Kind);
        var saved = Assert.Single(store.Saves);
        Assert.Equal("estimate_restored", saved.EventType);
        Assert.Equal(62m, saved.Details.BaseHourlyRate);
        Assert.Contains("Restored from v4", saved.Reason, StringComparison.Ordinal);
        // A line the draft no longer holds is restored as a new line, not an edit of one.
        Assert.Equal([null], saved.ExistingLineIds!);

        var foreign = version with { Id = Guid.NewGuid(), SpecificationId = Guid.NewGuid() };
        snapshots.Versions = [foreign];
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RestoreRepairSpecificationSnapshot(store, snapshots).ExecuteAsync(
                new(CaseId, 3, Engineer, "op-restore-2", new string('l', 32), specification.SpecificationId, foreign.Id),
                CancellationToken.None));
    }

    [Fact]
    public async Task OnlyAnEngineerActsOnARepairSpecification()
    {
        var specification = Estimate(Header(rate: 40m), Line("new_part", price: 100m));
        var store = new RecordingStore(specification);
        var snapshots = new RecordingSnapshots();
        var user = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new ScaleRepairSpecification(store, snapshots).ExecuteAsync(
                new(CaseId, 3, user, "op", new string('l', 32), specification.SpecificationId, 100m, ScalingFloors.Default),
                CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RemoveRepairSpecificationScaling(store, snapshots).ExecuteAsync(
                new(CaseId, 3, user, "op", new string('l', 32), specification.SpecificationId),
                CancellationToken.None));
        Assert.Empty(store.Saves);
    }

    private static EstimateDetails Header(decimal rate) => new("Repair spec", rate, null, 20m,
        Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered));

    private static RepairSpecificationVersion Estimate(
        EstimateDetails details, params CaseEstimateLineRecord[] lines) => new(
        Guid.NewGuid(), CaseId, 1, RepairSpecificationState.Draft,
        new(RepairSpecificationSourceRoute.Manual, null, null, null),
        lines, null, Engineer.SubjectId, Now, null, null, null, null, details);

    private static CaseEstimateLineRecord Line(
        string type, decimal? workUnits = null, decimal? paintWorkUnits = null,
        decimal? price = null, int? quantity = null, decimal? materials = null) => new(
        Guid.NewGuid(), 1, type, null, "Line", workUnits, price, false, null, null, null, null, null,
        ActorKind.Staff, Engineer.SubjectId, Now, Engineer.SubjectId, Now,
        paintWorkUnits, quantity, materials);

    private static RepairSpecificationSnapshot Snapshot(
        int number, RepairSpecificationSnapshotKind kind, Guid specificationId, RepairSpecificationVersion of) => new(
        Guid.NewGuid(), CaseId, specificationId, number, kind, "Origin", Engineer.SubjectId, Now,
        of.Details, of.Lines, EstimateTotals.Compute(of).Printed.Gross, false);

    private sealed class RecordingStore(RepairSpecificationVersion specification) : IRepairSpecificationStore
    {
        public List<SaveEstimateRequest> Saves { get; } = [];

        public Task<RepairSpecificationVersion> SaveEstimateAsync(SaveEstimateRequest request, CancellationToken cancellationToken)
        {
            Saves.Add(request);
            return Task.FromResult(specification with
            {
                Details = request.Details,
                Lines = [.. request.Lines.Select((line, index) => new CaseEstimateLineRecord(
                    Guid.NewGuid(), index + 1, line.Type, line.GuideCode, line.Description, line.WorkUnits,
                    line.Price, line.Unpriced, line.PartNumber, line.Betterment, line.Status, line.EvidenceLabel,
                    line.Justification, ActorKind.Staff, Engineer.SubjectId, Now, null, null,
                    line.PaintWorkUnits, line.Quantity, line.Materials))],
            });
        }

        public Task<RepairSpecificationVersion?> GetVersionAsync(Guid caseId, Guid specificationId, CancellationToken cancellationToken) =>
            Task.FromResult<RepairSpecificationVersion?>(
                specificationId == specification.SpecificationId ? specification : null);

        public Task RequireImportAuthorityAsync(ImportRawEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<RepairSpecificationVersion> SaveImportedEstimateAsync(SaveEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<RepairSpecificationVersion> StartDraftAsync(StartRepairSpecificationDraftRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<RepairSpecificationVersion> AcceptAsync(AcceptRepairSpecificationRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<RepairSpecificationVersion?> GetCurrentAcceptedAsync(Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<RepairSpecificationVersion?> GetCurrentDraftAsync(Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<RepairSpecificationVersion> DuplicateEstimateAsync(DuplicateEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<RepairSpecificationVersion> DiscardEstimateAsync(DiscardEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<RepairSpecificationVersion> SetCurrentEstimateAsync(SetCurrentEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<RepairSpecificationVersion>> ListEstimatesAsync(Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<CaseEstimatePageItem>> ListByCursorAsync(
            Guid caseId, int? afterVersion, Guid? afterId, int fetchCount, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingSnapshots : IRepairSpecificationSnapshotStore
    {
        public List<FreezeRepairSpecificationRequest> Frozen { get; } = [];

        public IReadOnlyList<RepairSpecificationSnapshot> Versions { get; set; } = [];

        public Task<RepairSpecificationSnapshot> FreezeAsync(FreezeRepairSpecificationRequest request, CancellationToken cancellationToken)
        {
            Frozen.Add(request);
            return Task.FromResult(new RepairSpecificationSnapshot(
                Guid.NewGuid(), request.CaseId, request.SpecificationId, Versions.Count + Frozen.Count,
                request.Kind, request.Origin, request.Actor.SubjectId, Now,
                new("Repair spec", 40m, null, 20m), [], 0m, false));
        }

        public Task<IReadOnlyList<RepairSpecificationSnapshot>> ListAsync(Guid caseId, Guid specificationId, CancellationToken cancellationToken) =>
            Task.FromResult(Versions);

        public Task<RepairSpecificationSnapshot?> GetAsync(Guid caseId, Guid snapshotId, CancellationToken cancellationToken) =>
            Task.FromResult(Versions.FirstOrDefault(version => version.Id == snapshotId));
    }
}
