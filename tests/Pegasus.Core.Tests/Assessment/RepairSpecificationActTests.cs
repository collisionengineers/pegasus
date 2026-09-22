using System.Globalization;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

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
    public void ScalingUsesTheLastNonExceedingRoundedEndpoint()
    {
        var specification = Estimate(
            Header(rate: 80m),
            Line("new_part", price: 1m),
            Line("repair", workUnits: 1m));

        var result = RepairSpecificationScaling.Scale(specification, 60.79m, ScalingFloors.Default);

        Assert.Equal(60.79m, result.GrossAfter);
        Assert.True(result.GrossAfter <= 60.79m);
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
        var assessment = new RecordingAssessment(10_000m);

        await new SaveAndScaleRepairSpecification(assessment, store).ExecuteAsync(
            new(
                new SaveEstimateRequest(
                    CaseId, 3, Engineer, "op-scale", "Repair spec scaled", new string('l', 32),
                    specification.SpecificationId, specification.Details,
                    specification.Lines.Select(RepairSpecificationScaling.ToInput).ToArray(),
                    specification.Source,
                    ExistingLineIds: specification.Lines.Select(line => (Guid?)line.Id).ToArray()),
                40m,
                ScalingFloors.Default),
            CancellationToken.None);

        var saved = Assert.Single(store.ScaleSaves);
        Assert.Equal(40m, saved.TargetPercentOfValue);
        Assert.Equal(10_000m, saved.EngineerValue);
        Assert.Equal(ScalingFloors.Default, saved.Floors);
        // The saved lines keep every line identity, so nothing is re-created.
        Assert.Equal(
            specification.Lines.Select(line => (Guid?)line.Id),
            saved.Save.ExistingLineIds!);
    }

    [Fact]
    public async Task ContractTargetUsesThePersistedSumInsteadOfPostedPercentage()
    {
        var specification = Estimate(Header(rate: 80m), Line("new_part", price: 500m));
        var store = new RecordingStore(specification);

        await new SaveAndScaleRepairSpecification(new RecordingAssessment(10_000m, 4_500m), store).ExecuteAsync(
            new(
                new SaveEstimateRequest(
                    CaseId, 3, Engineer, "op-contract-scale", "Repair spec scaled", new string('l', 32),
                    specification.SpecificationId, specification.Details,
                    specification.Lines.Select(RepairSpecificationScaling.ToInput).ToArray(),
                    specification.Source),
                1m,
                ScalingFloors.Default,
                ContractTarget: true),
            CancellationToken.None);

        Assert.Equal(45m, Assert.Single(store.ScaleSaves).TargetPercentOfValue);
    }

    [Fact]
    public void ScalingASelectedRateCardProducesATypedRate()
    {
        var specification = Estimate(
            Header(rate: 80m) with { Rate = new EstimateRateSnapshot(Guid.NewGuid(), 3, 80m) },
            Line("repair", workUnits: 4m));

        var result = RepairSpecificationScaling.Scale(specification, 1m, ScalingFloors.Default);

        Assert.Null(result.Details.Rate);
        Assert.Equal(50m, result.Details.BaseHourlyRate);
    }

    [Fact]
    public async Task RemoveScalingReturnsTheDraftToTheVersionFrozenBeforeTheLastApply()
    {
        var specification = Estimate(Header(rate: 40m), Line("new_part", price: 100m));
        var store = new RecordingStore(specification);

        await new RemoveRepairSpecificationScaling(store).ExecuteAsync(
            new(CaseId, 3, Engineer, "op-remove", new string('l', 32), specification.SpecificationId),
            CancellationToken.None);

        Assert.Equal(specification.SpecificationId, Assert.Single(store.Removals).SpecificationId);

        // Nothing to return to is a refusal, not a silent no-op.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RemoveRepairSpecificationScaling(store).ExecuteAsync(
                new(CaseId, 3, Engineer, "op-remove-2", new string('l', 32), specification.SpecificationId),
                CancellationToken.None));
    }

    [Fact]
    public async Task RestoreDelegatesTheAtomicRestoreToTheStore()
    {
        var specification = Estimate(Header(rate: 40m), Line("new_part", price: 100m));
        var restored = Estimate(Header(rate: 62m), Line("new_part", price: 300m));
        var store = new RecordingStore(specification);
        var version = Snapshot(4, RepairSpecificationSnapshotKind.Imported, specification.SpecificationId, restored);
        store.RestoreResult = restored;

        var result = await new RestoreRepairSpecificationSnapshot(store).ExecuteAsync(
            new(CaseId, 3, Engineer, "op-restore", new string('l', 32), specification.SpecificationId, version.Id),
            CancellationToken.None);

        Assert.Equal(version.Id, Assert.Single(store.Restores).SnapshotId);
        Assert.Equal(62m, result.Details.BaseHourlyRate);
        Assert.Empty(store.Saves);
    }

    /// <summary>
    /// PR 792 took the Engineer account type out of the authority rules: scaling
    /// and removing scaling are staff acts. What is still refused is an actor who
    /// is not staff at all, which is what RequireStaffAuthor stands for.
    /// </summary>
    [Fact]
    public async Task OnlyStaffActOnARepairSpecification()
    {
        var specification = Estimate(Header(rate: 40m), Line("new_part", price: 100m));
        var store = new RecordingStore(specification);
        var automation = ActionActor.Automation("pegasus-automation");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new SaveAndScaleRepairSpecification(new RecordingAssessment(10_000m), store).ExecuteAsync(
                new(
                    new SaveEstimateRequest(
                        CaseId, 3, automation, "op", "Scale", new string('l', 32), specification.SpecificationId,
                        specification.Details,
                        specification.Lines.Select(RepairSpecificationScaling.ToInput).ToArray(),
                        specification.Source),
                    40m,
                    ScalingFloors.Default),
                CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RemoveRepairSpecificationScaling(store).ExecuteAsync(
                new(CaseId, 3, automation, "op", new string('l', 32), specification.SpecificationId),
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
        public List<SaveAndScaleRepairSpecificationRequest> ScaleSaves { get; } = [];
        public List<RemoveRepairSpecificationScalingRequest> Removals { get; } = [];
        public List<RestoreRepairSpecificationSnapshotRequest> Restores { get; } = [];
        public RepairSpecificationVersion RestoreResult { get; set; } = null!;

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

        public Task<RepairSpecificationVersion> SaveAndScaleAsync(
            SaveAndScaleRepairSpecificationRequest request, CancellationToken cancellationToken)
        {
            ScaleSaves.Add(request);
            return Task.FromResult(specification);
        }

        public Task<RepairSpecificationVersion> RemoveScalingAsync(
            RemoveRepairSpecificationScalingRequest request, CancellationToken cancellationToken)
        {
            if (Removals.Count > 0)
            {
                throw new InvalidOperationException("The repair specification has no removable scaling.");
            }
            Removals.Add(request);
            return Task.FromResult(specification);
        }

        public Task<RepairSpecificationVersion> RestoreSnapshotAsync(
            RestoreRepairSpecificationSnapshotRequest request, CancellationToken cancellationToken)
        {
            Restores.Add(request);
            return Task.FromResult(RestoreResult);
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

    private sealed class RecordingAssessment(decimal engineerValue, decimal? contractSum = null) : ICaseAssessmentStore
    {
        public Task<CaseAssessmentProjection?> GetAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseAssessmentProjection?>(Projection(caseId));

        private CaseAssessmentProjection Projection(Guid caseId)
        {
            var fields = new List<AssessmentFieldValue>
            {
                new(
                    AssessmentVocabulary.ValueEngineer,
                    engineerValue.ToString(CultureInfo.InvariantCulture),
                    ActorKind.Staff,
                    Engineer.SubjectId,
                    Now,
                    Engineer.SubjectId,
                    Now),
            };
            if (contractSum is { } sum)
            {
                fields.Add(new(
                    AssessmentVocabulary.Outcome,
                    "contract_repair",
                    ActorKind.Staff,
                    Engineer.SubjectId,
                    Now,
                    Engineer.SubjectId,
                    Now));
                fields.Add(new(
                    AssessmentVocabulary.SettlementContractSum,
                    sum.ToString(CultureInfo.InvariantCulture),
                    ActorKind.Staff,
                    Engineer.SubjectId,
                    Now,
                    Engineer.SubjectId,
                    Now));
            }

            return new(
                caseId,
                "CASE-1",
                3,
                CaseLifecycleState.Review,
                null,
                fields,
                [],
                new(null, null, null, null, null, null, "tbc", null, null, null, null));
        }

        public Task<CaseAssessmentProjection> SaveAsync(
            SaveAssessmentRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

}
