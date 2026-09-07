using System.Text.Json;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Assessment;

public sealed class ValuationCalculationTests
{
    private static readonly DateTimeOffset Now = new(2030, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly Guid CaseId = Guid.NewGuid();
    private static readonly Guid GuideId = Guid.NewGuid();
    private static readonly Guid TowBarId = Guid.Parse("00000000-0000-4000-8000-00000000f001");
    private static readonly Guid DecalsId = Guid.Parse("00000000-0000-4000-8000-00000000f003");
    private static readonly ActionActor Engineer =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
    private static readonly ActionActor Administrator =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
    private static readonly ActionActor User =
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
    private static readonly string Lease = new('l', CaseEditAuthority.LeaseTokenLength);

    private static readonly ValuationPreset TowBar =
        new(TowBarId, "Tow bar", 300m, true, 1, "system:v1-foundation", Now);
    private static readonly ValuationPreset Decals =
        new(DecalsId, "Decals", 500m, true, 1, "system:v1-foundation", Now);

    /// <summary>
    /// The worked example the business gave: £3,100 guide retail, commercial
    /// VAT, a 20% prior total loss, a £300 addition and a £100 condition
    /// deduction come to £3,176, printed to the penny.
    /// </summary>
    [Fact]
    public void TheWorkedExampleCalculatesAndPrintsAsTheBusinessStatesIt()
    {
        var calculation = ValuationCalculationPolicy.Calculate(
            Input(
                3100m,
                commercialVat: true,
                priorTotalLoss: 0.20m,
                additions: [Addition(TowBar, 300m)],
                conditionDeduction: 100m));

        Assert.Equal(3100m, calculation.GuideRetailValue);
        Assert.True(calculation.CommercialVatApplied);
        Assert.Equal(620m, calculation.CommercialVatAmount);
        Assert.Equal(3720m, calculation.ValueIncludingVat);
        Assert.Equal(0.20m, calculation.PriorTotalLossPercentage);
        Assert.Equal(744m, calculation.PriorTotalLossAmount);
        Assert.Equal(300m, calculation.AdditionsTotal);
        Assert.Equal(100m, calculation.ConditionDeduction);
        Assert.Equal(3176m, calculation.Proposal);
        Assert.Equal("£3,176.00", ValuationCalculationPolicy.FormatMoney(calculation.Proposal));
        Assert.Equal("£3,100.00", ValuationCalculationPolicy.FormatMoney(3100m));
        Assert.Equal("£0.00", ValuationCalculationPolicy.FormatMoney(0m));
    }

    /// <summary>
    /// Every step rounds to whole pounds away from zero, and it rounds where
    /// the business rounds: the VAT, the prior-loss reduction and the
    /// proposal are each rounded on their own, never once at the end.
    /// </summary>
    [Theory]
    // VAT midpoint: 20% of 12.50 is 2.50, which rounds away from zero to 3.
    [InlineData(12.5, true, null, 3, 15.5, 0, 16)]
    // VAT non-midpoint: 20% of 12.49 is 2.498, which rounds down to 2.
    [InlineData(12.49, true, null, 2, 14.49, 0, 14)]
    // Prior-loss midpoint: 10% of 105 is 10.50, which rounds up to 11.
    [InlineData(105, false, 0.10, 0, 105, 11, 94)]
    // Prior-loss non-midpoint: 10% of 104 is 10.40, which rounds down to 10.
    [InlineData(104, false, 0.10, 0, 104, 10, 94)]
    // Proposal midpoint: 100.50 rounds up to 101.
    [InlineData(100.5, false, null, 0, 100.5, 0, 101)]
    // Proposal non-midpoint: 100.49 rounds down to 100.
    [InlineData(100.49, false, null, 0, 100.49, 0, 100)]
    public void EveryStepRoundsToWholePoundsAwayFromZero(
        double guideRetail,
        bool commercialVat,
        double? priorTotalLoss,
        double expectedVat,
        double expectedIncludingVat,
        double expectedPriorLoss,
        double expectedProposal)
    {
        var calculation = ValuationCalculationPolicy.Calculate(
            Input(
                (decimal)guideRetail,
                commercialVat,
                priorTotalLoss is null ? null : (decimal)priorTotalLoss.Value));

        Assert.Equal((decimal)expectedVat, calculation.CommercialVatAmount);
        Assert.Equal((decimal)expectedIncludingVat, calculation.ValueIncludingVat);
        Assert.Equal((decimal)expectedPriorLoss, calculation.PriorTotalLossAmount);
        Assert.Equal((decimal)expectedProposal, calculation.Proposal);
    }

    [Fact]
    public void APriorTotalLossIsTenOrTwentyPercentAndNothingElse()
    {
        Assert.Equal([0.10m, 0.20m], ValuationCalculationPolicy.PriorTotalLossPercentages);
        Assert.Equal(
            1000m,
            ValuationCalculationPolicy.Calculate(Input(10000m, priorTotalLoss: 0.10m))
                .PriorTotalLossAmount);
        Assert.Equal(
            2000m,
            ValuationCalculationPolicy.Calculate(Input(10000m, priorTotalLoss: 0.20m))
                .PriorTotalLossAmount);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ValuationCalculationPolicy.Calculate(Input(10000m, priorTotalLoss: 0.15m)));
    }

    /// <summary>
    /// A guide card without a retail figure is not a basis: there is nothing
    /// to calculate from, so the calculator refuses rather than treating the
    /// gap as zero.
    /// </summary>
    [Fact]
    public void AGuideCardWithoutARetailValueIsNotABasis()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ValuationCalculationPolicy.Calculate(Input(0m)));
        Assert.Throws<InvalidOperationException>(() =>
            ValuationCalculationPolicy.Calculate(Input(-1m)));
    }

    /// <summary>
    /// A VAT-registered claimant reclaims the VAT, so there was never a
    /// commercial addition to make: the flag is cleared rather than refused,
    /// and the recorded snapshot says it was not applied.
    /// </summary>
    [Fact]
    public void AVatRegisteredClaimantClearsCommercialVat()
    {
        var calculation = ValuationCalculationPolicy.Calculate(
            Input(3100m, commercialVat: true, claimantVatRegistered: true));

        Assert.False(calculation.CommercialVatApplied);
        Assert.Equal(0m, calculation.CommercialVatAmount);
        Assert.Equal(3100m, calculation.ValueIncludingVat);
        Assert.Equal(3100m, calculation.Proposal);
    }

    [Fact]
    public void NegativeAdditionsDeductionsAndProposalsAreAllRefused()
    {
        Assert.Throws<ArgumentException>(() =>
            ValuationCalculationPolicy.Calculate(Input(3100m, conditionDeduction: -1m)));
        Assert.Throws<ArgumentException>(() =>
            ValuationCalculationPolicy.Calculate(
                Input(3100m, additions: [Addition(TowBar, -1m)])));
        Assert.Throws<InvalidOperationException>(() =>
            ValuationCalculationPolicy.Calculate(Input(3100m, conditionDeduction: 3101m)));
    }

    /// <summary>
    /// The maintained label and suggestion come from the preset record, not
    /// from the form, and the Engineer's own amount rides beside them: the
    /// snapshot therefore says both what was suggested and what was chosen.
    /// </summary>
    [Fact]
    public void SelectingAPresetCopiesItsLabelAndSuggestionAndKeepsTheChosenAmount()
    {
        var input = ValuationCalculationPolicy.Resolve(
            Selection(additions: [new(TowBarId, 1, "Tampered label", 450m)]),
            Basis(3100m));

        var addition = Assert.Single(input.Additions);
        Assert.Equal(TowBarId, addition.PresetId);
        Assert.Equal(1, addition.PresetVersion);
        Assert.Equal("Tow bar", addition.Label);
        Assert.Equal(300m, addition.SuggestedAmount);
        Assert.Equal(450m, addition.Amount);
    }

    [Fact]
    public void ACustomAdditionCarriesItsOwnLabelAndNoSuggestion()
    {
        var input = ValuationCalculationPolicy.Resolve(
            Selection(additions: [new(Guid.Empty, 0, "  Roof rack  ", 75m)]),
            Basis(3100m));

        var addition = Assert.Single(input.Additions);
        Assert.Equal(Guid.Empty, addition.PresetId);
        Assert.Equal("Roof rack", addition.Label);
        Assert.Null(addition.SuggestedAmount);
        Assert.Equal(75m, addition.Amount);

        Assert.Throws<ArgumentException>(() =>
            ValuationCalculationPolicy.ValidateSelection(
                Selection(additions: [new(Guid.Empty, 0, "   ", 75m)]),
                "selection"));
    }

    /// <summary>
    /// A disabled preset stays readable wherever history names it, but it can
    /// never be selected onto a new calculation.
    /// </summary>
    [Fact]
    public void ADisabledPresetCannotBeSelectedAgain()
    {
        var exception = Assert.Throws<ValuationPresetException>(() =>
            ValuationCalculationPolicy.Resolve(
                Selection(additions: [new(DecalsId, 1, null, 500m)]),
                Basis(3100m, presets: [TowBar, Decals with { Active = false }])));

        Assert.Equal(ValuationPresetError.NotSelectable, exception.Error);
    }

    [Fact]
    public void AnUnknownPresetCannotBeSelected()
    {
        var exception = Assert.Throws<ValuationPresetException>(() =>
            ValuationCalculationPolicy.Resolve(
                Selection(additions: [new(Guid.NewGuid(), 1, null, 500m)]),
                Basis(3100m)));

        Assert.Equal(ValuationPresetError.NotSelectable, exception.Error);
    }

    /// <summary>
    /// A preset that moved after the form was rendered is refused with the
    /// version that is now current, so the Engineer re-reads the maintained
    /// amount rather than applying the one they were shown.
    /// </summary>
    [Fact]
    public void APresetThatMovedUnderTheFormIsRefusedWithItsCurrentVersion()
    {
        var exception = Assert.Throws<ValuationPresetException>(() =>
            ValuationCalculationPolicy.Resolve(
                Selection(additions: [new(TowBarId, 1, null, 300m)]),
                Basis(3100m, presets: [TowBar with { Version = 2, SuggestedAmount = 350m }])));

        Assert.Equal(ValuationPresetError.VersionConflict, exception.Error);
        Assert.Equal(2, exception.CurrentVersion);
    }

    /// <summary>
    /// Two cards may carry the same guide name and differ only in the month
    /// they were published. They are two records with two identities, and the
    /// basis is whichever one the Engineer selected.
    /// </summary>
    [Fact]
    public async Task TwoCardsFromTheSameGuideAreTwoSeparateBases()
    {
        var april = Guid.NewGuid();
        var may = Guid.NewGuid();
        var store = new RecordingStore
        {
            Bases =
            {
                [april] = new(april, Now.AddDays(-30), 3100m, false, [TowBar]),
                [may] = new(may, Now, 3250m, false, [TowBar])
            }
        };
        var preview = new PreviewValuationCalculation(store);

        var first = await preview.ExecuteAsync(
            new(CaseId, Engineer, Selection(guideValuationId: april)),
            CancellationToken.None);
        var second = await preview.ExecuteAsync(
            new(CaseId, Engineer, Selection(guideValuationId: may)),
            CancellationToken.None);

        Assert.Equal(april, first.GuideValuationId);
        Assert.Equal(3100m, first.Calculation.Proposal);
        Assert.Equal(may, second.GuideValuationId);
        Assert.Equal(3250m, second.Calculation.Proposal);
        Assert.NotEqual(first.GuideValuationStampUtc, second.GuideValuationStampUtc);
    }

    /// <summary>
    /// A preview is a reading, not a finding: it takes ordinary casework
    /// authority and writes nothing at all.
    /// </summary>
    [Fact]
    public async Task PreviewingNeverAdoptsAnEngineersValue()
    {
        var store = new RecordingStore { Bases = { [GuideId] = Basis(3100m) } };
        var preview = new PreviewValuationCalculation(store);

        var result = await preview.ExecuteAsync(
            new(CaseId, User, Selection(commercialVat: true, priorTotalLoss: 0.20m)),
            CancellationToken.None);

        Assert.Equal(2976m, result.Calculation.Proposal);
        Assert.Empty(store.Applied);
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            preview.ExecuteAsync(
                new(CaseId, ActionActor.RequestLink(Guid.NewGuid()), Selection()),
                CancellationToken.None));
    }

    /// <summary>
    /// Adopting a value is confirming a professional finding, so it takes the
    /// finding rule from its single owner: an Engineer, and an Administrator
    /// who is not one never suffices.
    /// </summary>
    [Fact]
    public async Task OnlyAnEngineerAdoptsAnEngineersValue()
    {
        var store = new RecordingStore { Bases = { [GuideId] = Basis(3100m) } };
        var apply = new ApplyValuationCalculation(store);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            apply.ExecuteAsync(ApplyRequest(Administrator), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            apply.ExecuteAsync(ApplyRequest(User), CancellationToken.None));
        Assert.Empty(store.Applied);

        var applied = await apply.ExecuteAsync(ApplyRequest(Engineer), CancellationToken.None);

        Assert.Equal(3100m, applied.AcceptedEngineerValue);
        Assert.Equal(GuideId, Assert.Single(store.Applied).Selection.GuideValuationId);
    }

    /// <summary>
    /// A repeated operation key that carries a different request is a
    /// conflict, not a retry, and the conflict reaches the caller rather than
    /// being swallowed into a second adoption.
    /// </summary>
    [Fact]
    public async Task AReplayedOperationKeyCarryingADifferentRequestConflicts()
    {
        var store = new RecordingStore { Bases = { [GuideId] = Basis(3100m) } };
        var apply = new ApplyValuationCalculation(store);

        await apply.ExecuteAsync(ApplyRequest(Engineer), CancellationToken.None);

        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            apply.ExecuteAsync(
                ApplyRequest(Engineer) with { CorrectedEngineerValue = 4000m },
                CancellationToken.None));
        Assert.Single(store.Applied);
    }

    /// <summary>
    /// A later manual correction is a further row over the same basis: the
    /// guide card and its stamp are unchanged, the accepted figure is the
    /// Engineer's own, and the reason travels with it.
    /// </summary>
    [Fact]
    public async Task AManualCorrectionKeepsTheAppliedBasisAndAddsItsOwnReason()
    {
        var store = new RecordingStore { Bases = { [GuideId] = Basis(3100m) } };
        var apply = new ApplyValuationCalculation(store);

        var first = await apply.ExecuteAsync(ApplyRequest(Engineer), CancellationToken.None);
        var corrected = await apply.ExecuteAsync(
            ApplyRequest(Engineer, "valuation-correction") with
            {
                CorrectedEngineerValue = 3250m,
                Reason = "Corrected the adopted value after re-reading the guide."
            },
            CancellationToken.None);

        Assert.Equal(3100m, first.AcceptedEngineerValue);
        Assert.Equal(3250m, corrected.AcceptedEngineerValue);
        Assert.Equal(first.GuideValuationId, corrected.GuideValuationId);
        Assert.Equal(first.GuideValuationStampUtc, corrected.GuideValuationStampUtc);
        Assert.Equal(first.Calculation.Proposal, corrected.Calculation.Proposal);
        Assert.Equal(
            "Corrected the adopted value after re-reading the guide.",
            corrected.Reason);
        Assert.Equal(2, store.Applied.Count);
    }

    [Fact]
    public async Task AnAdoptionMustNameACaseAndAGuideCard()
    {
        var store = new RecordingStore { Bases = { [GuideId] = Basis(3100m) } };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ApplyValuationCalculation(store).ExecuteAsync(
                ApplyRequest(Engineer) with { Selection = Selection(guideValuationId: Guid.Empty) },
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ListAppliedValuations(store).ExecuteAsync(Guid.Empty, CancellationToken.None));
    }

    /// <summary>
    /// Maintaining the presets is Administrator configuration, and the
    /// suggested amount is money like every other figure here.
    /// </summary>
    [Fact]
    public async Task MaintainingAPresetIsAdministratorConfiguration()
    {
        var store = new RecordingPresetStore();
        var save = new SaveValuationPreset(store);
        var list = new ListValuationPresets(store);

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            save.ExecuteAsync(PresetRequest(Engineer), CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            save.ExecuteAsync(
                PresetRequest(Administrator) with { SuggestedAmount = -1m },
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            save.ExecuteAsync(
                PresetRequest(Administrator) with { Label = "  " },
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            save.ExecuteAsync(
                PresetRequest(Administrator) with { PresetId = Guid.Empty },
                CancellationToken.None));

        var saved = await save.ExecuteAsync(
            PresetRequest(Administrator) with { Label = "  Tow bar  " },
            CancellationToken.None);
        Assert.Equal("Tow bar", saved.Label);
        Assert.Equal("Tow bar", Assert.Single(store.Saves).Label);
        Assert.Equal([saved], await list.ExecuteAsync(Engineer, CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            list.ExecuteAsync(ActionActor.RequestLink(Guid.NewGuid()), CancellationToken.None));
    }

    private static SaveValuationPresetRequest PresetRequest(ActionActor actor) => new(
        TowBarId,
        "Tow bar",
        300m,
        Active: true,
        ExpectedVersion: 1,
        actor,
        "Corrected the maintained amount.",
        "valuation-preset-save");

    private static ApplyValuationRequest ApplyRequest(
        ActionActor actor,
        string operationKey = "valuation-apply") => new(
        CaseId,
        3,
        actor,
        operationKey,
        "Adopted the calculated Engineer's Value.",
        Lease,
        Selection(),
        Now);

    private static ValuationCalculationSelection Selection(
        Guid? guideValuationId = null,
        bool commercialVat = false,
        decimal? priorTotalLoss = null,
        IReadOnlyList<ValuationAdditionSelection>? additions = null,
        decimal conditionDeduction = 0m) => new(
        guideValuationId ?? GuideId,
        commercialVat,
        priorTotalLoss,
        additions ?? [],
        conditionDeduction);

    private static ValuationCalculationBasis Basis(
        decimal guideRetailValue,
        bool claimantVatRegistered = false,
        IReadOnlyList<ValuationPreset>? presets = null) => new(
        GuideId,
        Now,
        guideRetailValue,
        claimantVatRegistered,
        presets ?? [TowBar, Decals]);

    private static ValuationCalculationInput Input(
        decimal guideRetailValue,
        bool commercialVat = false,
        decimal? priorTotalLoss = null,
        bool claimantVatRegistered = false,
        IReadOnlyList<ValuationAddition>? additions = null,
        decimal conditionDeduction = 0m) => new(
        guideRetailValue,
        commercialVat,
        claimantVatRegistered,
        priorTotalLoss,
        additions ?? [],
        conditionDeduction);

    private static ValuationAddition Addition(ValuationPreset preset, decimal amount) => new(
        preset.Id,
        preset.Version,
        preset.Label,
        preset.SuggestedAmount,
        amount);

    /// <summary>
    /// Stands in for the persistence boundary: it holds the basis each guide
    /// card would be read at, and refuses a repeated operation key that
    /// carries a different request exactly as the store does.
    /// </summary>
    private sealed class RecordingStore : IAppliedValuationStore
    {
        public Dictionary<Guid, ValuationCalculationBasis> Bases { get; } = [];

        public List<ApplyValuationRequest> Applied { get; } = [];

        public Task<ValuationCalculationBasis> ReadBasisAsync(
            Guid caseId,
            Guid guideValuationId,
            CancellationToken cancellationToken) =>
            Bases.TryGetValue(guideValuationId, out var basis)
                ? Task.FromResult(basis)
                : throw new InvalidOperationException(
                    "The selected guide valuation was not found on this case.");

        public async Task<AppliedValuation> ApplyAsync(
            ApplyValuationRequest request,
            CancellationToken cancellationToken)
        {
            var replay = Applied.SingleOrDefault(
                item => item.OperationKey == request.OperationKey);
            if (replay is not null && Hash(replay) != Hash(request))
            {
                throw new CaseOperationConflictException(request.CaseId, request.OperationKey);
            }

            var basis = await ReadBasisAsync(
                request.CaseId,
                request.Selection.GuideValuationId,
                cancellationToken);
            var calculation = ValuationCalculationPolicy.Calculate(
                ValuationCalculationPolicy.Resolve(request.Selection, basis));
            Applied.Add(request);
            return new(
                Guid.NewGuid(),
                request.CaseId,
                request.ExpectedVersion + 1,
                basis.GuideValuationId,
                basis.GuideValuationStampUtc,
                calculation,
                ValuationCalculationPolicy.AcceptedValue(request, calculation),
                request.Actor.SubjectId,
                Now,
                request.Reason,
                ValuationCalculationPolicy.PolicyStamp);
        }

        public Task<IReadOnlyList<AppliedValuation>> ListAppliedAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AppliedValuation>>([]);

        /// <summary>
        /// The store compares a replayed operation key by hashing the whole
        /// serialized request; this double compares the same way, so a retry
        /// and a reused key are told apart here exactly as they are there.
        /// </summary>
        private static string Hash(ApplyValuationRequest request) =>
            JsonSerializer.Serialize(request);
    }

    private sealed class RecordingPresetStore : IValuationPresetStore
    {
        public List<SaveValuationPresetRequest> Saves { get; } = [];

        private readonly List<ValuationPreset> _presets = [];

        public Task<IReadOnlyList<ValuationPreset>> ListAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ValuationPreset>>([.. _presets]);

        public Task<ValuationPreset> SaveAsync(
            SaveValuationPresetRequest request,
            CancellationToken cancellationToken)
        {
            Saves.Add(request);
            var preset = new ValuationPreset(
                request.PresetId,
                request.Label,
                request.SuggestedAmount,
                request.Active,
                request.ExpectedVersion + 1,
                request.Actor.SubjectId,
                Now);
            _presets.Add(preset);
            return Task.FromResult(preset);
        }
    }
}
