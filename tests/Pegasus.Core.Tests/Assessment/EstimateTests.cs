using System.Security.Cryptography;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Assessment;

/// <summary>
/// ENG-026: the one totals owner, the operation mapping, the estimate
/// policy, and the actor rules of the named-estimate use cases.
/// </summary>
public sealed class EstimateTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly ActionActor Engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
    private static readonly ActionActor User = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
    private static readonly ActionActor Client = ActionActor.Automation("pegasus-automation");
    private static readonly string Lease = new('l', CaseEditAuthority.LeaseTokenLength);

    [Fact]
    public void OneSnapshotRatePricesPanelAndPaintHoursAlike()
    {
        var estimate = Estimate(
            new("Repairer estimate", 3, 40m, 25m, 10m, 17.5m, null,
                Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
            Line("new_part", price: 100m, quantity: 2),
            Line("repair", workUnits: 2.5m),
            Line("paint_repair", paintWorkUnits: 1.5m),
            Line("new_part", price: 33.33m));

        var totals = EstimateTotals.Compute(estimate);

        // The estimate's own paint rate of 30 is not consulted: 2.5 panel
        // hours and 1.5 paint hours are both priced at the one rate of 40.
        Assert.Equal(233.33m, totals.Printed.Parts);
        Assert.Equal(100m, totals.Printed.PanelLabour);
        Assert.Equal(60m, totals.Printed.PaintLabour);
        Assert.Equal(25m, totals.Printed.Materials);
        Assert.Equal(10m, totals.Printed.Specialist);
        Assert.Equal(428.33m, totals.Printed.Net);
        Assert.Equal(74.96m, totals.Printed.Vat);
        Assert.Equal(503.29m, totals.Printed.Gross);
        Assert.Equal(RepairSpecificationPolicy.PolicyVersion, totals.CalculationPolicyVersion);
        Assert.Empty(totals.OffPattern);
    }

    [Fact]
    public void ARateSnapshotOverridesTheTypedLabourRateForBothHourKinds()
    {
        var estimate = Estimate(
            Header(rate: 40m, snapshot: new(Guid.NewGuid(), 7, 52m)),
            Line("repair", workUnits: 2m),
            Line("paint_repair", paintWorkUnits: 3m));

        var totals = EstimateTotals.Compute(estimate);

        Assert.Equal(104m, totals.Printed.PanelLabour);
        Assert.Equal(156m, totals.Printed.PaintLabour);
    }

    [Fact]
    public void TotalsWithoutRatesOrVatAreThePartsAlone()
    {
        var estimate = Estimate(
            new("Parts only", null, null, null, null, 0m, null),
            Line("new_part", price: 50m),
            Line("repair", workUnits: 4m));

        var totals = EstimateTotals.Compute(estimate);

        Assert.Equal(50m, totals.Printed.Net);
        Assert.Equal(0m, totals.Printed.Vat);
        Assert.Equal(50m, totals.Printed.Gross);
    }

    [Fact]
    public void EveryLineTypeMapsToExactlyOneOperationAndBack()
    {
        foreach (var type in EstimateLineCodes.Types)
        {
            var operation = EstimateOperations.FromLineType(type);
            Assert.Equal(operation, EstimateOperations.FromLineType(EstimateOperations.ToLineType(operation)));
        }
        Assert.Equal("new_part", EstimateOperations.ToLineType(EstimateOperation.Replace));
        Assert.Equal("rnr", EstimateOperations.ToLineType(EstimateOperation.RemoveAndRefit));
        Assert.Equal("paint_blend", EstimateOperations.ToLineType(EstimateOperation.Blend));
        Assert.Equal("specialist_fixed", EstimateOperations.ToLineType(EstimateOperation.Specialist));
        Assert.Equal("check_labour", EstimateOperations.ToLineType(EstimateOperation.Other));
        Assert.Equal(EstimateOperation.Blend, EstimateOperations.FromLineType("paint_blend"));
        Assert.Equal(EstimateOperation.Other, EstimateOperations.FromLineType("check_labour"));

        // The types a Paint or Specialist row can already be persisted as
        // are read back as that operation and never rewritten by reading.
        Assert.Equal(EstimateOperation.Paint, EstimateOperations.FromLineType("paint_new"));
        Assert.Equal(EstimateOperation.Paint, EstimateOperations.FromLineType("paint_prep"));
        Assert.Equal(EstimateOperation.Specialist, EstimateOperations.FromLineType("specialist_wu"));

        Assert.True(EstimateOperations.TryParse("R&I", out var parsed));
        Assert.Equal(EstimateOperation.RemoveAndRefit, parsed);
        Assert.True(EstimateOperations.TryParse("Blend", out var blend));
        Assert.Equal(EstimateOperation.Blend, blend);
        Assert.True(EstimateOperations.TryParse("Specialist", out var specialist));
        Assert.Equal(EstimateOperation.Specialist, specialist);
        Assert.False(EstimateOperations.TryParse("Weld", out _));
        Assert.Throws<InvalidOperationException>(() => EstimateOperations.FromLineType("weld"));
    }

    [Fact]
    public void EachOperationLandsInExactlyOneCostBucket()
    {
        var estimate = Estimate(
            Header(rate: 50m, additionalMaterials: 5m, otherCosts: 7m),
            Line("new_part", price: 120m, quantity: 2, workUnits: 0.4m),
            Line("repair", workUnits: 1.5m),
            Line("rnr", workUnits: 0.6m),
            Line("paint_repair", paintWorkUnits: 2m),
            Line("paint_blend", paintWorkUnits: 0.5m),
            Line("specialist_fixed", price: 180m, workUnits: 6m),
            Line("check_labour", workUnits: 0.5m),
            Line("repair", materials: 12m));

        var raw = EstimateTotals.Compute(estimate).Raw;

        Assert.Equal(240m, raw.Parts);
        // 0.4 Replace + 1.5 Repair + 0.6 R&I + 0.5 Other = 3.0 panel hours;
        // the Specialist row's 6 hours are shown but never priced.
        Assert.Equal(150m, raw.PanelLabour);
        Assert.Equal(125m, raw.PaintLabour);
        Assert.Equal(17m, raw.Materials);
        Assert.Equal(187m, raw.Specialist);
        Assert.Equal(0m, raw.OffPattern);
        Assert.Equal(719m, raw.Net);
    }

    [Theory]
    [InlineData("", 20, 40)]
    [InlineData("Estimate", 120, 40)]
    [InlineData("Estimate", 20.005, 40)]
    [InlineData("Estimate", 20, -1)]
    [InlineData("Estimate", 20, 40.123)]
    public void DetailsOutsideTheirBoundsAreRefused(string name, double vat, double rate) =>
        Assert.Throws<ArgumentException>(() => EstimatePolicy.ValidateDetails(
            new(name, null, (decimal)rate, null, null, (decimal)vat, null)));

    [Fact]
    public void DetailsAreTrimmedAndNotesBlankedToNull()
    {
        var details = EstimatePolicy.ValidateDetails(new("  Glass's  ", 2, 40m, null, null, 20m, "   "));
        Assert.Equal("Glass's", details.Name);
        Assert.Null(details.Notes);
    }

    [Fact]
    public void AStaffUserWhoIsNotAnEngineerCannotSaveAnEstimate() =>
        Assert.Throws<InvalidOperationException>(() =>
            EstimatePolicy.ValidateSave(SaveRequest(User, RepairSpecificationSourceRoute.Manual)));

    [Fact]
    public void TheAutomationActorMayOnlySaveAiDraftsThatCiteAJob()
    {
        Assert.Throws<InvalidOperationException>(() =>
            EstimatePolicy.ValidateSave(SaveRequest(Client, RepairSpecificationSourceRoute.Manual, jobId: Guid.NewGuid())));
        Assert.Throws<InvalidOperationException>(() =>
            EstimatePolicy.ValidateSave(SaveRequest(Client, RepairSpecificationSourceRoute.AiDraft, jobId: null)));
        var validated = EstimatePolicy.ValidateSave(
            SaveRequest(Client, RepairSpecificationSourceRoute.AiDraft, jobId: Guid.NewGuid()));
        Assert.Equal(RepairSpecificationSourceRoute.AiDraft, validated.Source.Route);
    }

    [Fact]
    public async Task SaveRefusesAJobThatIsNotAnEstimateJobOnThisCaseHeldByTheClient()
    {
        var jobs = new FakeJobStore();
        var store = new FakeSpecificationStore();
        var save = new SaveEstimate(store, jobs, new FixedClock());

        var wrongKind = jobs.Add(Job(AiJobKind.UnidentifiedResolution, CaseId, AiJobState.Taken, Client.SubjectId));
        var wrongCase = jobs.Add(Job(AiJobKind.Estimate, Guid.NewGuid(), AiJobState.Taken, Client.SubjectId));
        var otherClient = jobs.Add(Job(AiJobKind.Estimate, CaseId, AiJobState.Taken, "other-client"));
        var lapsed = jobs.Add(Job(AiJobKind.Estimate, CaseId, AiJobState.Taken, Client.SubjectId, Now - TimeSpan.FromMinutes(1)));
        var held = jobs.Add(Job(AiJobKind.Estimate, CaseId, AiJobState.Taken, Client.SubjectId));

        foreach (var refused in new[] { Guid.NewGuid(), wrongKind, wrongCase, otherClient, lapsed })
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                save.ExecuteAsync(SaveRequest(Client, RepairSpecificationSourceRoute.AiDraft, refused), CancellationToken.None));
        }
        Assert.Empty(store.Saved);

        var saved = await save.ExecuteAsync(
            SaveRequest(Client, RepairSpecificationSourceRoute.AiDraft, held), CancellationToken.None);
        Assert.Equal(held, saved.AiJobId);
        Assert.Single(store.Saved);
    }

    [Fact]
    public async Task AnEngineerEditingAnAiDraftKeepsItsJobWhicheverStateTheJobIsIn()
    {
        var jobs = new FakeJobStore();
        var completed = jobs.Add(Job(AiJobKind.Estimate, CaseId, AiJobState.Completed, Client.SubjectId));
        var save = new SaveEstimate(new FakeSpecificationStore(), jobs, new FixedClock());

        var saved = await save.ExecuteAsync(
            SaveRequest(Engineer, RepairSpecificationSourceRoute.AiDraft, completed), CancellationToken.None);

        Assert.Equal(completed, saved.AiJobId);
    }

    [Fact]
    public void OnlyADraftIsEditableAndTheAutomationOnlyEditsAiDrafts()
    {
        var accepted = Estimate(Details(), Line("repair", workUnits: 1m)) with { State = RepairSpecificationState.Accepted };
        Assert.Throws<InvalidOperationException>(() => EstimatePolicy.ValidateEditable(accepted, Engineer));
        var manualDraft = Estimate(Details(), Line("repair", workUnits: 1m));
        Assert.Throws<InvalidOperationException>(() => EstimatePolicy.ValidateEditable(manualDraft, Client));
        EstimatePolicy.ValidateEditable(manualDraft, Engineer);
    }

    [Fact]
    public void AnAcceptedOrCurrentEstimateCannotBeDiscardedAndDiscardedCannotBeDuplicated()
    {
        var accepted = Estimate(Details(), Line("repair", workUnits: 1m)) with { State = RepairSpecificationState.Accepted };
        Assert.Throws<InvalidOperationException>(() => EstimatePolicy.ValidateDiscard(accepted));
        var discarded = Estimate(Details(), Line("repair", workUnits: 1m)) with { State = RepairSpecificationState.Discarded };
        Assert.Throws<InvalidOperationException>(() => EstimatePolicy.ValidateDiscard(discarded));
        Assert.Throws<InvalidOperationException>(() => EstimatePolicy.ValidateDuplicate(discarded));
        EstimatePolicy.ValidateDiscard(Estimate(Details(), Line("repair", workUnits: 1m)));
    }

    [Fact]
    public void MakingCurrentIsTheEngineersAcceptanceWithTheTotalsOwnersBasis()
    {
        var draft = Estimate(
            new("Draft", 2, 40m, 25m, 0m, 20m, null,
                Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
            Line("new_part", price: 100m), Line("repair", workUnits: 2m));
        EstimatePolicy.ValidateSetCurrent(draft, Engineer);
        Assert.Throws<InvalidOperationException>(() => EstimatePolicy.ValidateSetCurrent(draft, User));

        var basis = EstimatePolicy.BasisFor(draft);
        var totals = EstimateTotals.Compute(draft);
        Assert.Equal(totals.Printed.Gross, basis.Total);
        Assert.Equal(totals.Printed.Vat, basis.Vat);
        Assert.Equal("repair-specification/v3", basis.PolicyVersion);
        Assert.Equal(basis, RepairSpecificationPolicy.ValidateCalculationBasis(basis));

        var unconfirmed = draft with { Lines = [Line("repair", workUnits: 1m, confirmed: false)] };
        Assert.Throws<InvalidOperationException>(() => EstimatePolicy.ValidateSetCurrent(unconfirmed, Engineer));
        var discarded = draft with { State = RepairSpecificationState.Discarded };
        Assert.Throws<InvalidOperationException>(() => EstimatePolicy.ValidateSetCurrent(discarded, Engineer));
        EstimatePolicy.ValidateSetCurrent(draft with { State = RepairSpecificationState.Accepted }, Engineer);
    }

    [Fact]
    public async Task SetCurrentConfirmsOnlyADraftReadyJobTheEstimateCites()
    {
        var jobs = new FakeJobStore();
        var ready = jobs.Add(Job(AiJobKind.Estimate, CaseId, AiJobState.DraftReady, Client.SubjectId));
        var cancelled = jobs.Add(Job(AiJobKind.Estimate, CaseId, AiJobState.Cancelled, Client.SubjectId));
        var confirm = new RecordingConfirm();
        var store = new FakeSpecificationStore();
        var setCurrent = new SetCurrentEstimate(store, jobs, confirm, new FixedClock());

        store.Current = Estimate(Details(), Line("repair", workUnits: 1m)) with { AiJobId = ready, IsCurrent = true };
        await setCurrent.ExecuteAsync(SetCurrentRequest(Engineer, "op-ready"), CancellationToken.None);
        var command = Assert.Single(confirm.Commands);
        Assert.Equal(ready, command.JobId);
        Assert.Equal("op-ready:job", command.OperationKey);

        store.Current = store.Current with { AiJobId = cancelled };
        await setCurrent.ExecuteAsync(SetCurrentRequest(Engineer, "op-cancelled"), CancellationToken.None);
        Assert.Single(confirm.Commands);

        store.Current = store.Current with { AiJobId = null };
        await setCurrent.ExecuteAsync(SetCurrentRequest(Engineer, "op-none"), CancellationToken.None);
        Assert.Single(confirm.Commands);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            setCurrent.ExecuteAsync(SetCurrentRequest(User, "op-user"), CancellationToken.None));
    }

    // ---- B04 arithmetic: discounts, VAT categories and printed pennies ----

    /// <summary>Plan vector V1: a raw taxable base that rounds up, and a VAT that rounds down.</summary>
    [Fact]
    public void PrintedPartsAndVatAreRoundedIndependentlyAwayFromZero()
    {
        var estimate = Estimate(
            Header(discounts: new(0.5m, 0m, 0m, 0m), vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
            Line("new_part", price: 200.01m));

        var totals = EstimateTotals.Compute(estimate);

        Assert.Equal(100.005m, totals.Raw.Parts);
        Assert.Equal(20.001m, totals.Raw.Vat);
        Assert.Equal(100.01m, totals.Printed.Parts);
        Assert.Equal(20.00m, totals.Printed.Vat);
        Assert.Equal(100.01m, totals.Printed.Net);
        Assert.Equal(120.01m, totals.Printed.Gross);
    }

    /// <summary>
    /// Plan vector V2: the four discounts apply per category and then
    /// overall, and each printed component is rounded on its own.
    /// </summary>
    [Fact]
    public void EachDiscountAppliesToItsCategoryAndThenTheOverallDiscountToAll()
    {
        var totals = EstimateTotals.Compute(DiscountedEstimate(RepairerVatStatus.Registered));

        Assert.Equal(255.766875m, totals.Raw.Parts);
        Assert.Equal(70.0245m, totals.Raw.Materials);
        Assert.Equal(225.95625m, totals.Raw.Specialist);
        Assert.Equal(976.695m, totals.Raw.Category);
        Assert.Equal(952.277625m, totals.Raw.Net);

        Assert.Equal(255.77m, totals.Printed.Parts);
        Assert.Equal(238.29m, totals.Printed.PanelLabour);
        Assert.Equal(162.24m, totals.Printed.PaintLabour);
        Assert.Equal(70.02m, totals.Printed.Materials);
        Assert.Equal(225.96m, totals.Printed.Specialist);
        Assert.Equal(952.28m, totals.Printed.Net);
    }

    /// <summary>Plan vector V2 again, read at each of the three VAT positions.</summary>
    [Fact]
    public void VatIsChargedOnlyOnTheCategoriesTheRepairersStatusSelects()
    {
        var registered = EstimateTotals.Compute(DiscountedEstimate(RepairerVatStatus.Registered));
        Assert.Equal(952.277625m, registered.Raw.Taxable);
        Assert.Equal(190.46m, registered.Printed.Vat);
        Assert.Equal(1_142.74m, registered.Printed.Gross);

        var notRegistered = EstimateTotals.Compute(DiscountedEstimate(RepairerVatStatus.NotRegistered));
        Assert.Equal(325.791375m, notRegistered.Raw.Taxable);
        Assert.Equal(65.16m, notRegistered.Printed.Vat);
        Assert.Equal(1_017.44m, notRegistered.Printed.Gross);

        var none = EstimateTotals.Compute(DiscountedEstimate(
            RepairerVatStatus.Registered, new(RepairerVatStatus.Registered, EstimateVatCategories.None, true)));
        Assert.Equal(0m, none.Raw.Taxable);
        Assert.Equal(0m, none.Printed.Vat);
        Assert.Equal(952.28m, none.Printed.Gross);
    }

    /// <summary>
    /// Plan vector V3: the printed net is the sum of the printed components,
    /// which is a penny above the rounded raw net. Neither figure is moved
    /// to reconcile them.
    /// </summary>
    [Fact]
    public void TheResidualPennyBetweenTheRawAndPrintedNetIsNeverMoved()
    {
        var estimate = Estimate(
            Header(
                additionalMaterials: 100.10m, otherCosts: 60.10m,
                discounts: new(0m, 0m, 0m, 0.05m),
                vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
            Line("new_part", price: 200.10m));

        var totals = EstimateTotals.Compute(estimate);

        Assert.Equal(342.285m, totals.Raw.Net);
        Assert.Equal(342.29m, decimal.Round(totals.Raw.Net, 2, MidpointRounding.AwayFromZero));
        Assert.Equal(342.30m, totals.Printed.Net);
        Assert.Equal(68.457m, totals.Raw.Vat);
        Assert.Equal(68.46m, totals.Printed.Vat);
        Assert.Equal(410.742m, totals.Raw.Gross);
        Assert.Equal(410.76m, totals.Printed.Gross);
        Assert.Equal(
            totals.Printed.Net + totals.Printed.Vat,
            totals.Printed.Parts + totals.Printed.Materials + totals.Printed.Specialist + totals.Printed.Vat);
    }

    [Theory]
    [InlineData(-0.01, 0, 0, 0)]
    [InlineData(0, 1.01, 0, 0)]
    [InlineData(0, 0, 0, -0.0001)]
    [InlineData(0, 0, 0.00001, 0)]
    public void ADiscountOutsideZeroToOneIsRefused(
        double parts, double materials, double specialist, double overall) =>
        Assert.Throws<ArgumentException>(() => EstimatePolicy.ValidateDiscounts(
            new((decimal)parts, (decimal)materials, (decimal)specialist, (decimal)overall)));

    [Fact]
    public void ADiscountOfTheWholeCategoryIsAccepted()
    {
        var estimate = Estimate(
            Header(discounts: new(1m, 0m, 0m, 0m)),
            Line("new_part", price: 500m));

        Assert.Equal(0m, EstimateTotals.Compute(estimate).Printed.Parts);
    }

    // ---- The repairer's VAT status and acceptance ----

    [Fact]
    public void AnUnknownRepairerVatStatusBlocksUseAsCurrentUntilItIsResolved()
    {
        var unknown = Estimate(
            Header(rate: 40m, vat: EstimateVatPolicy.For(RepairerVatStatus.Unknown)),
            Line("repair", workUnits: 1m));
        Assert.True(unknown.Details.VatPolicy.BlocksAcceptance);
        var blocked = Assert.Throws<InvalidOperationException>(
            () => EstimatePolicy.ValidateSetCurrent(unknown, Engineer));
        Assert.Contains("VAT status", blocked.Message, StringComparison.Ordinal);

        // Recording the status resolves it.
        var recorded = unknown with
        {
            Details = unknown.Details with { Vat = EstimateVatPolicy.For(RepairerVatStatus.NotRegistered) },
        };
        EstimatePolicy.ValidateSetCurrent(recorded, Engineer);

        // So does choosing the categories by hand while the status stays unknown.
        var overridden = unknown with
        {
            Details = unknown.Details with
            {
                Vat = new(RepairerVatStatus.Unknown, EstimateVatCategories.Parts, true),
            },
        };
        Assert.False(overridden.Details.VatPolicy.BlocksAcceptance);
        EstimatePolicy.ValidateSetCurrent(overridden, Engineer);

        // Resetting to the repairer's status restores the block.
        var reset = overridden with
        {
            Details = overridden.Details with { Vat = EstimateVatPolicy.For(RepairerVatStatus.Unknown) },
        };
        Assert.True(reset.Details.VatPolicy.BlocksAcceptance);
        Assert.Throws<InvalidOperationException>(() => EstimatePolicy.ValidateSetCurrent(reset, Engineer));
    }

    [Fact]
    public void CategoriesThatDifferFromTheRepairersStatusMustBeRecordedAsAnOverride()
    {
        Assert.Equal(
            EstimateVatCategories.All,
            EstimateVatPolicy.DefaultFor(RepairerVatStatus.Registered));
        Assert.Equal(
            EstimateVatCategories.Parts | EstimateVatCategories.Materials,
            EstimateVatPolicy.DefaultFor(RepairerVatStatus.NotRegistered));
        Assert.Equal(
            EstimateVatCategories.None,
            EstimateVatPolicy.DefaultFor(RepairerVatStatus.Unknown));

        Assert.Throws<ArgumentException>(() => EstimatePolicy.ValidateVatPolicy(
            new(RepairerVatStatus.Registered, EstimateVatCategories.Parts, false)));
        EstimatePolicy.ValidateVatPolicy(
            new(RepairerVatStatus.Registered, EstimateVatCategories.Parts, true));
    }

    // ---- Off-pattern values ----

    [Fact]
    public void AUnitAmountOnALineThatPricesNoPartIsRetainedInSpecialistTreatment()
    {
        var estimate = Estimate(
            Header(rate: 40m, discounts: new(0m, 0m, 0.1m, 0m)),
            Line("repair", workUnits: 1m, price: 12.50m));

        var totals = EstimateTotals.Compute(estimate);

        Assert.Equal(12.50m, totals.Raw.OffPattern);
        Assert.Equal(0m, totals.Raw.Parts);
        Assert.Equal(11.25m, totals.Raw.Specialist);
        var anomaly = Assert.Single(totals.OffPattern);
        Assert.Equal("unit amount", anomaly.Field);
        Assert.Equal(12.50m, anomaly.Value);
    }

    [Fact]
    public void PaintHoursOnALineThatIsNotPaintOrBlendAreFlaggedAndNotPriced()
    {
        var estimate = Estimate(
            Header(rate: 40m),
            Line("repair", workUnits: 1m, paintWorkUnits: 3m));

        var totals = EstimateTotals.Compute(estimate);

        Assert.Equal(40m, totals.Raw.PanelLabour);
        Assert.Equal(0m, totals.Raw.PaintLabour);
        var anomaly = Assert.Single(totals.OffPattern);
        Assert.Equal("paint hours", anomaly.Field);
        Assert.Equal(3m, anomaly.Value);
    }

    [Fact]
    public void SpecialistHoursAreShownAndNeverMultipliedByTheRate()
    {
        var estimate = Estimate(
            Header(rate: 40m),
            Line("specialist_wu", workUnits: 6m, price: 180m));

        var totals = EstimateTotals.Compute(estimate);

        Assert.Equal(180m, totals.Raw.Specialist);
        Assert.Equal(0m, totals.Raw.PanelLabour);
        Assert.Equal(6m, Assert.Single(estimate.Lines).WorkUnits);
    }

    [Fact]
    public void AMissingOrZeroFixedCostQuantityCostsOne()
    {
        var estimate = Estimate(
            Header(),
            Line("specialist_fixed", price: 180m),
            Line("new_part", price: 40m, quantity: 0));

        var raw = EstimateTotals.Compute(estimate).Raw;

        Assert.Equal(180m, raw.Specialist);
        Assert.Equal(40m, raw.Parts);
    }

    [Fact]
    public void ChangingALinesOperationKeepsEveryFieldItAlreadyCarried()
    {
        var repair = Line("repair", workUnits: 1.5m, price: 12.50m, materials: 4m);
        var replaced = repair with { Type = EstimateOperations.ToLineType(EstimateOperation.Replace) };

        Assert.Equal(1.5m, replaced.WorkUnits);
        Assert.Equal(12.50m, replaced.Price);
        Assert.Equal(4m, replaced.Materials);

        var before = EstimateTotals.Compute(Estimate(Header(rate: 40m), repair)).Raw;
        var after = EstimateTotals.Compute(Estimate(Header(rate: 40m), replaced)).Raw;

        // The one moved value is the unit amount, out of off-pattern and into parts.
        Assert.Equal(12.50m, before.OffPattern);
        Assert.Equal(0m, after.OffPattern);
        Assert.Equal(12.50m, after.Parts);
        Assert.Equal(before.PanelLabour, after.PanelLabour);
        Assert.Equal(before.Materials, after.Materials);
    }

    [Fact]
    public void AnAcceptedEstimateKeepsThePolicyVersionItWasCostedUnder()
    {
        // A basis accepted under an earlier policy stays valid as it stands;
        // policy version 3 is stamped only on what this policy costs.
        var historic = RepairSpecificationPolicy.ValidateCalculationBasis(
            new(100m, 20m, 10m, 0m, true, 26m, 156m, "repair-specification/v2"));
        Assert.Equal("repair-specification/v2", historic.PolicyVersion);

        var basis = EstimatePolicy.BasisFor(Estimate(Header(rate: 40m), Line("repair", workUnits: 2m)));
        Assert.Equal("repair-specification/v3", basis.PolicyVersion);
        Assert.Equal(3, RepairSpecificationPolicy.PolicyVersion);
    }

    [Fact]
    public void TheAcceptedBasisCarriesThePrintedBreakdownAndTheVatPolicy()
    {
        var estimate = DiscountedEstimate(RepairerVatStatus.NotRegistered);

        var basis = EstimatePolicy.BasisFor(estimate);

        Assert.Equal(238.29m, basis.Labour);
        Assert.Equal(255.77m, basis.Parts);
        Assert.Equal(232.26m, basis.PaintMaterials);
        Assert.Equal(225.96m, basis.SpecialistOther);
        Assert.False(basis.RepairerVatRegistered);
        Assert.Equal(65.16m, basis.Vat);
        Assert.Equal(1_017.44m, basis.Total);
        Assert.Equal(RepairerVatStatus.NotRegistered, basis.VatPolicy!.RepairerStatus);
        Assert.Equal(952.28m, basis.Printed!.Net);
        Assert.Equal(basis, RepairSpecificationPolicy.ValidateCalculationBasis(basis));
    }

    [Fact]
    public void APrintedBreakdownThatDoesNotAddUpIsRefused()
    {
        var basis = EstimatePolicy.BasisFor(DiscountedEstimate(RepairerVatStatus.Registered));

        Assert.Throws<InvalidOperationException>(() => RepairSpecificationPolicy.ValidateCalculationBasis(
            basis with { Printed = basis.Printed! with { Net = basis.Printed.Net + 0.01m } }));
    }

    // ---- Provider hours are kept at the provider's own precision ----

    [Fact]
    public void ImportedHoursAreKeptAtTheProvidersPrecisionAndBoundedByTheEstimateRule()
    {
        // A third of an hour is 0.333333 at the persisted precision, not 0.3.
        var providerTime = decimal.Round(1m / 3m, EstimatePolicy.WorkUnitDecimals);
        EstimatePolicy.ValidateLineAmounts(LineInput("repair") with { WorkUnits = providerTime });

        Assert.Throws<ArgumentException>(() => EstimatePolicy.ValidateLineAmounts(
            LineInput("repair") with { WorkUnits = 0.1234567m }));
        Assert.Throws<ArgumentException>(() => EstimatePolicy.ValidateLineAmounts(
            LineInput("repair") with { PaintWorkUnits = EstimatePolicy.MaximumLineWorkUnits + 1m }));
        Assert.Throws<ArgumentException>(() => EstimatePolicy.ValidateLineAmounts(
            LineInput("repair") with { Materials = 1.005m }));
    }

    // ---- The one canonical raw-estimate import ----

    [Fact]
    public async Task AnImportUsesTheSuppliedNameAndDerivesOneOnlyWhenNoneIsGiven()
    {
        var save = new RecordingSave();
        var import = new ImportRawEstimate(
            [new StubParser(RepairSpecificationSourceRoute.AudatexPdf, ".pdf", "Audatex"), JsonStub()],
            Retained,
            new StubDocuments(ImportBytes, "estimate.pdf", "application/pdf"),
            new StubList(),
            save);

        await import.ExecuteAsync(ImportRequest(name: "  Repairer quote  "), CancellationToken.None);

        Assert.Equal("Repairer quote", Assert.Single(save.Requests).Details.Name);
    }

    [Fact]
    public async Task AnImportAutoDetectsItsFormatAndLandsOneSourceLabelledDraft()
    {
        var save = new RecordingSave();
        var import = new ImportRawEstimate(
            [new StubParser(RepairSpecificationSourceRoute.AudatexPdf, ".pdf", "Audatex"), JsonStub()],
            Retained,
            new StubDocuments(ImportBytes, "estimate.pdf", "application/pdf"),
            new StubList(),
            save);

        var id = await import.ExecuteAsync(ImportRequest(), CancellationToken.None);

        var saved = Assert.Single(save.Requests);
        Assert.Null(saved.EstimateId);
        Assert.Equal("Audatex 1", saved.Details.Name);
        Assert.Equal(RepairSpecificationSourceRoute.AudatexPdf, saved.Source.Route);
        Assert.Equal($"estimate-import:{ImportOperationKey}", saved.Source.ArtifactReference);
        Assert.Equal("Audatex v1", saved.Source.SourceVersion);
        Assert.Equal(ImportSha256, saved.Source.Sha256);
        Assert.Equal(ImportRawEstimate.ImportReason, saved.Reason);
        Assert.NotEqual(Guid.Empty, id);

        // Every imported row keeps the document, version, hash and row it came from.
        var line = Assert.Single(saved.Lines);
        Assert.Equal($"estimate-import:{ImportOperationKey}", line.SourceDocumentIdentity);
        Assert.Equal(DocumentVersionId, line.SourceDocumentVersionId);
        Assert.Equal(ImportSha256, line.SourceDocumentSha256);
        Assert.Equal("1", line.SourceRowIdentity);
        Assert.Equal("repair", line.Origin!.Type);
        Assert.Equal(1.5m, line.Origin.WorkUnits);
    }

    [Fact]
    public async Task AnImportNamesTheDraftAfterTheImportsTheCaseAlreadyHolds()
    {
        var save = new RecordingSave();
        var existing = Estimate(Details() with { Name = "Audatex 1" }) with
        {
            Source = new(RepairSpecificationSourceRoute.AudatexPdf, "estimate-import:earlier", "v0", new string('b', 64)),
        };
        var import = new ImportRawEstimate(
            [new StubParser(RepairSpecificationSourceRoute.AudatexPdf, ".pdf", "Audatex")],
            Retained,
            new StubDocuments(ImportBytes, "estimate.pdf", "application/pdf"),
            new StubList(existing),
            save);

        await import.ExecuteAsync(ImportRequest(), CancellationToken.None);

        Assert.Equal("Audatex 2", Assert.Single(save.Requests).Details.Name);
    }

    [Fact]
    public async Task AnAmbiguousOrUnrecognizedDocumentImportsNothing()
    {
        var save = new RecordingSave();
        var documents = new StubDocuments(ImportBytes, "estimate.pdf", "application/pdf");

        var ambiguous = new ImportRawEstimate(
            [
                new StubParser(RepairSpecificationSourceRoute.AudatexPdf, ".pdf", "Audatex"),
                new StubParser(RepairSpecificationSourceRoute.Json, ".pdf", "Other"),
            ],
            Retained, documents, new StubList(), save);
        var many = await Assert.ThrowsAsync<EstimateParseRejectedException>(
            () => ambiguous.ExecuteAsync(ImportRequest(), CancellationToken.None));
        Assert.Contains("More than one", many.Message, StringComparison.Ordinal);

        var none = new ImportRawEstimate([JsonStub()], Retained, documents, new StubList(), save);
        var unrecognized = await Assert.ThrowsAsync<EstimateParseRejectedException>(
            () => none.ExecuteAsync(ImportRequest(), CancellationToken.None));
        Assert.Contains("No estimate format", unrecognized.Message, StringComparison.Ordinal);

        Assert.Empty(save.Requests);
    }

    [Fact]
    public async Task AnImportOfTheSameSourceHashReplaysTheEstimateItAlreadyCreated()
    {
        var save = new RecordingSave();
        var already = Estimate(Details()) with
        {
            Source = new(RepairSpecificationSourceRoute.AudatexPdf, "estimate-import:first", "v1", ImportSha256),
        };
        var import = new ImportRawEstimate(
            [new StubParser(RepairSpecificationSourceRoute.AudatexPdf, ".pdf", "Audatex")],
            Retained,
            new StubDocuments(ImportBytes, "estimate.pdf", "application/pdf"),
            new StubList(already),
            save);

        var id = await import.ExecuteAsync(ImportRequest(operationKey: "op-import-2"), CancellationToken.None);

        Assert.Equal(already.SpecificationId, id);
        Assert.Empty(save.Requests);
    }

    [Fact]
    public async Task AnImportRefusesBytesThatDoNotMatchTheHashItRecorded()
    {
        var save = new RecordingSave();
        var import = new ImportRawEstimate(
            [new StubParser(RepairSpecificationSourceRoute.AudatexPdf, ".pdf", "Audatex")],
            Retained,
            new StubDocuments("different bytes"u8.ToArray(), "estimate.pdf", "application/pdf"),
            new StubList(),
            save);

        var rejected = await Assert.ThrowsAsync<EstimateParseRejectedException>(
            () => import.ExecuteAsync(ImportRequest(), CancellationToken.None));

        Assert.Contains("does not match the hash", rejected.Message, StringComparison.Ordinal);
        Assert.Empty(save.Requests);
    }

    [Fact]
    public async Task AnImportRefusesADocumentThatReadsAsAnotherProvidersFormat()
    {
        var save = new RecordingSave();
        var import = new ImportRawEstimate(
            [new StubParser(RepairSpecificationSourceRoute.Json, ".pdf", "Other")],
            Retained,
            new StubDocuments(ImportBytes, "estimate.pdf", "application/pdf"),
            new StubList(),
            save);

        await Assert.ThrowsAsync<EstimateParseRejectedException>(() => import.ExecuteAsync(
            ImportRequest(route: RepairSpecificationSourceRoute.AudatexPdf), CancellationToken.None));

        Assert.Empty(save.Requests);
    }

    [Fact]
    public async Task AnImportOpensTheRetainedVersionAtItsRecordedLength()
    {
        var save = new RecordingSave();
        var documents = new StubDocuments(ImportBytes, "estimate.pdf", "application/pdf");
        var import = new ImportRawEstimate(
            [new StubParser(RepairSpecificationSourceRoute.AudatexPdf, ".pdf", "Audatex")],
            Retained, documents, new StubList(), save);

        await import.ExecuteAsync(ImportRequest(), CancellationToken.None);

        var opened = Assert.Single(documents.Requests);
        Assert.Equal(DocumentId, opened.DocumentId);
        Assert.Equal(DocumentVersionId, opened.VersionId);
        Assert.Equal(CaseId, opened.CaseId);
        Assert.Equal(ImportBytes.Length, opened.ExpectedContentLength);
        Assert.Equal(ImportSha256, opened.ExpectedSha256);
    }

    [Fact]
    public async Task AVersionTheCaseDoesNotHoldImportsNothing()
    {
        var save = new RecordingSave();
        var documents = new StubDocuments(ImportBytes, "estimate.pdf", "application/pdf");
        var import = new ImportRawEstimate(
            [new StubParser(RepairSpecificationSourceRoute.AudatexPdf, ".pdf", "Audatex")],
            new StubMetadata(retained: null), documents, new StubList(), save);

        var rejected = await Assert.ThrowsAsync<EstimateParseRejectedException>(
            () => import.ExecuteAsync(ImportRequest(), CancellationToken.None));

        Assert.Contains("does not hold the document version", rejected.Message, StringComparison.Ordinal);
        Assert.Empty(documents.Requests);
        Assert.Empty(save.Requests);
    }

    [Fact]
    public async Task AReplayIsAuthorizedBeforeTheReplayIsConsulted()
    {
        var save = new RecordingSave();
        var already = Estimate(Details()) with
        {
            Source = new(RepairSpecificationSourceRoute.AudatexPdf, "estimate-import:first", "v1", ImportSha256),
        };
        var estimates = new StubList(already);
        var import = new ImportRawEstimate(
            [new StubParser(RepairSpecificationSourceRoute.AudatexPdf, ".pdf", "Audatex")],
            Retained, new StubDocuments(ImportBytes, "estimate.pdf", "application/pdf"), estimates, save);

        // The same subject under another actor kind, a staff user who is not an
        // Engineer, an invalid expected version and a missing lease are each
        // refused exactly as a first import is, before the replay is read.
        await Assert.ThrowsAsync<InvalidOperationException>(() => import.ExecuteAsync(
            ImportRequest(actor: ActionActor.Automation(Engineer.SubjectId)), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => import.ExecuteAsync(
            ImportRequest(actor: User), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => import.ExecuteAsync(
            ImportRequest(expectedVersion: -1), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => import.ExecuteAsync(
            ImportRequest(lease: ""), CancellationToken.None));
        Assert.Equal(0, estimates.Calls);

        // The legitimate identical replay still resolves to the estimate the
        // first import created at whatever version the case has since reached:
        // a replay writes nothing, and the version is proven on the write.
        Assert.Equal(already.SpecificationId, await import.ExecuteAsync(
            ImportRequest(operationKey: "op-import-2", expectedVersion: 9), CancellationToken.None));
        Assert.Equal(1, estimates.Calls);
        Assert.Empty(save.Requests);
    }

    [Fact]
    public async Task AnUndefinedRouteImportsNothing()
    {
        var save = new RecordingSave();
        var estimates = new StubList();
        var documents = new StubDocuments(ImportBytes, "estimate.pdf", "application/pdf");
        var import = new ImportRawEstimate(
            [new StubParser(RepairSpecificationSourceRoute.AudatexPdf, ".pdf", "Audatex")],
            Retained, documents, estimates, save);

        await Assert.ThrowsAsync<EstimateParseRejectedException>(() => import.ExecuteAsync(
            ImportRequest(route: (RepairSpecificationSourceRoute)99), CancellationToken.None));

        Assert.Equal(0, estimates.Calls);
        Assert.Empty(documents.Requests);
        Assert.Empty(save.Requests);
    }

    private static EstimateDetails Details() => Header(rate: 40m) with { Name = "Estimate 1" };

    private static EstimateDetails Header(
        decimal? rate = null,
        decimal? additionalMaterials = null,
        decimal? otherCosts = null,
        EstimateDiscounts? discounts = null,
        EstimateVatPolicy? vat = null,
        decimal vatPercent = 20m,
        EstimateRateSnapshot? snapshot = null) => new(
        "Estimate", null, rate, additionalMaterials, otherCosts, vatPercent, null,
        discounts, vat, snapshot);

    /// <summary>
    /// Plan vector V2: parts 299.80, 4.7 panel and 3.2 paint hours at 52.00,
    /// materials 45.60 + 30.00, specialist 180.00 + 65.00 with a 12.50
    /// off-pattern amount, discounted 12.5 % / 5 % / 10 % and then 2.5 %.
    /// </summary>
    private static RepairSpecificationVersion DiscountedEstimate(
        RepairerVatStatus status, EstimateVatPolicy? policy = null) => Estimate(
        Header(
            rate: 52m, additionalMaterials: 30m, otherCosts: 65m,
            discounts: new(0.125m, 0.05m, 0.10m, 0.025m),
            vat: policy ?? EstimateVatPolicy.For(status)),
        Line("new_part", price: 125.75m, quantity: 2),
        Line("new_part", price: 48.30m),
        Line("repair", workUnits: 3.5m, materials: 45.60m),
        Line("rnr", workUnits: 1.2m),
        Line("paint_repair", paintWorkUnits: 2.4m),
        Line("paint_blend", paintWorkUnits: 0.8m),
        Line("specialist_fixed", price: 180m),
        Line("check_labour", price: 12.50m));

    private static EstimateLineInput LineInput(string type) =>
        new(type, null, "Line", null, null, false, null, null, null, null, null);

    // ---- Import fixtures ----

    private const string ImportOperationKey = "op-import";
    private static readonly byte[] ImportBytes = "an estimate document"u8.ToArray();
    private static readonly string ImportSha256 =
        Convert.ToHexStringLower(SHA256.HashData(ImportBytes));
    private static readonly Guid OccurrenceId = Guid.NewGuid();
    private static readonly Guid DocumentId = Guid.NewGuid();
    private static readonly Guid DocumentVersionId = Guid.NewGuid();

    /// <summary>The case's own record of the retained version the imports name.</summary>
    private static readonly StubMetadata Retained = new(new CaseDocumentMetadata(
        CaseId, OccurrenceId, DocumentId, DocumentVersionId, "estimate.pdf", "application/pdf",
        ImportBytes.Length, ImportSha256));

    private static ImportRawEstimateRequest ImportRequest(
        string operationKey = ImportOperationKey,
        RepairSpecificationSourceRoute route = RepairSpecificationSourceRoute.AudatexPdf,
        string name = "",
        ActionActor? actor = null,
        long expectedVersion = 4,
        string? lease = null) => new(
        actor ?? Engineer, CaseId, expectedVersion, lease ?? Lease,
        OccurrenceId, DocumentVersionId, ImportSha256, route, operationKey, name);

    private static StubParser JsonStub() =>
        new(RepairSpecificationSourceRoute.Json, ".json", "Repairer");

    private sealed class StubParser(
        RepairSpecificationSourceRoute route, string extension, string providerName) : IEstimateDocumentParser
    {
        public RepairSpecificationSourceRoute Route => route;

        public bool CanParse(string fileName, string mediaType) =>
            string.Equals(Path.GetExtension(fileName), extension, StringComparison.OrdinalIgnoreCase);

        public ParsedEstimate Parse(ReadOnlyMemory<byte> content) => new(
            $"{providerName} v1",
            [LineInput("repair") with { WorkUnits = 1.5m, Materials = 4m }],
            providerName,
            new EstimateSourceTotals(Net: 60m));
    }

    private sealed class StubMetadata(CaseDocumentMetadata? retained) : IGetCaseDocumentMetadata
    {
        public Task<CaseDocumentMetadata?> ExecuteAsync(
            GetCaseDocumentMetadataQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(retained is not null
                && query.CaseId == retained.CaseId
                && query.OccurrenceId == retained.OccurrenceId
                && query.VersionId == retained.VersionId
                ? retained
                : null);
    }

    private sealed class StubDocuments(byte[] content, string fileName, string mediaType)
        : IReadLogicalDocumentVersion
    {
        public List<ReadLogicalDocumentVersionRequest> Requests { get; } = [];

        public Task<LogicalDocumentContent> OpenAsync(
            ReadLogicalDocumentVersionRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(
                new LogicalDocumentContent(
                    new MemoryStream(content), request.DocumentId, request.VersionId, null,
                    request.ExpectedSha256, content.Length, fileName, mediaType));
        }
    }

    private sealed class StubList(params RepairSpecificationVersion[] estimates) : IListCaseEstimates
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<RepairSpecificationVersion>> ExecuteAsync(
            Guid caseId, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<RepairSpecificationVersion>>(estimates);
        }
    }

    private sealed class RecordingSave : ISaveEstimate
    {
        public List<SaveEstimateRequest> Requests { get; } = [];

        public Task<RepairSpecificationVersion> ExecuteAsync(
            SaveEstimateRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Estimate(request.Details) with { Source = request.Source });
        }
    }

    private static SaveEstimateRequest SaveRequest(
        ActionActor actor, RepairSpecificationSourceRoute route, Guid? jobId = null) => new(
        CaseId, 3, actor, "op-save", "Recorded an estimate.", Lease, null, Details(),
        [LineInput("repair") with { Description = "Repair door", WorkUnits = 1.5m }],
        new(route, null, null, null), jobId);

    private static SetCurrentEstimateRequest SetCurrentRequest(ActionActor actor, string operationKey) => new(
        CaseId, 3, actor, operationKey, "Use estimate.", Lease, Guid.NewGuid());

    private static RepairSpecificationVersion Estimate(EstimateDetails details, params CaseEstimateLineRecord[] lines) => new(
        Guid.NewGuid(), CaseId, 1, RepairSpecificationState.Draft,
        new(RepairSpecificationSourceRoute.Manual, null, null, null),
        lines, null, Engineer.SubjectId, Now, null, null, null, null, details);

    private static CaseEstimateLineRecord Line(
        string type, decimal? workUnits = null, decimal? paintWorkUnits = null,
        decimal? price = null, int? quantity = null, bool confirmed = true,
        decimal? materials = null) => new(
        Guid.NewGuid(), 1, type, null, "Line", workUnits, price, false, null, null, null, null, null,
        ActorKind.Staff, Engineer.SubjectId, Now, confirmed ? Engineer.SubjectId : null, confirmed ? Now : null,
        paintWorkUnits, quantity, materials);

    private static AiJobRecord Job(
        AiJobKind kind, Guid subjectId, AiJobState state, string takenBy, DateTimeOffset? leaseExpires = null) => new(
        Guid.NewGuid(), kind, AiJobPolicy.SubjectKindFor(kind), subjectId, "CE-1", "Draft an estimate.",
        kind == AiJobKind.Estimate ? 50 : null, kind == AiJobKind.Estimate ? 12000m : null, state,
        ActorKind.Staff, Engineer.SubjectId, Now - TimeSpan.FromHours(1), Now + TimeSpan.FromHours(23),
        takenBy, Now - TimeSpan.FromMinutes(5), leaseExpires ?? Now + TimeSpan.FromMinutes(25),
        null, null, null, null, null, null, 2);

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeJobStore : IAiJobStore
    {
        private readonly Dictionary<Guid, AiJobRecord> jobs = [];

        public Guid Add(AiJobRecord job)
        {
            jobs[job.JobId] = job;
            return job.JobId;
        }

        public Task<AiJobRecord> CreateAsync(NewAiJob job, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AiJobRecord?> GetAsync(Guid jobId, CancellationToken cancellationToken) =>
            Task.FromResult(jobs.GetValueOrDefault(jobId));

        public Task<AiJobRecord> TransitionAsync(AiJobTransition transition, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingConfirm : IConfirmAiJob
    {
        public List<ConfirmAiJobCommand> Commands { get; } = [];

        public Task<AiJobRecord> ExecuteAsync(ConfirmAiJobCommand command, CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(Job(AiJobKind.Estimate, CaseId, AiJobState.Completed, Client.SubjectId));
        }
    }

    private sealed class FakeSpecificationStore : IRepairSpecificationStore
    {
        public List<SaveEstimateRequest> Saved { get; } = [];

        public RepairSpecificationVersion Current { get; set; } = Estimate(Details(), Line("repair", workUnits: 1m));

        public Task<RepairSpecificationVersion> SaveEstimateAsync(SaveEstimateRequest request, CancellationToken cancellationToken)
        {
            Saved.Add(request);
            return Task.FromResult(Estimate(request.Details) with { AiJobId = request.AiJobId, Source = request.Source });
        }

        public Task<RepairSpecificationVersion> SetCurrentEstimateAsync(SetCurrentEstimateRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(Current);

        public Task<RepairSpecificationVersion> StartDraftAsync(StartRepairSpecificationDraftRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion> AcceptAsync(AcceptRepairSpecificationRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion?> GetVersionAsync(Guid caseId, Guid specificationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion?> GetCurrentAcceptedAsync(Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion?> GetCurrentDraftAsync(Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion> DuplicateEstimateAsync(DuplicateEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RepairSpecificationVersion> DiscardEstimateAsync(DiscardEstimateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RepairSpecificationVersion>> ListEstimatesAsync(Guid caseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CaseEstimatePageItem>> ListByCursorAsync(
            Guid caseId, int? afterVersion, Guid? afterId, int fetchCount, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
