using Pegasus.Core.Cases;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Actors;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Cases;

/// <summary>
/// The Valuation section's members (v26 § Valuation): the calculator on the
/// Core policy shape — presets, a preview that Core computes, and the
/// calculation the page opened on, against which the Case Save decides
/// whether the operator changed it — and the per-source Get valuation
/// controls: AI market research starts the existing Market research job for
/// the chosen month, and every guide source asks GetValuation, which answers
/// the connected provider's figures for the card to show in its boxes, or
/// that the source is unavailable. The one Case Save (23 September 2026)
/// records the cards, the Retail, Trade and Engineer's value boxes and a
/// calculation the Engineer chose to use or changed; nothing here writes.
/// </summary>
public sealed partial class DetailsModel
{
    /// <summary>The maintained valuation additions, loaded with the section while editing.</summary>
    public IReadOnlyList<ValuationPreset> ValuationPresets { get; private set; } = [];

    /// <summary>Every adoption of an Engineer's Value on this Case, newest first.</summary>
    public IReadOnlyList<AppliedValuation> AppliedValuations { get; private set; } = [];

    /// <summary>The Market research job still in progress, so the card reads as pending.</summary>
    public AiJobRecord? PendingMarketResearch { get; private set; }

    /// <summary>The month the Get valuation buttons run for: the pending job's, else the current month.</summary>
    public string ValuationMonth
    {
        get
        {
            var pending = PendingMarketResearch?.Instruction;
            if (pending is not null)
            {
                var at = pending.LastIndexOf(' ');
                if (at >= 0 && pending.Length - at - 1 >= 7)
                {
                    var candidate = pending.Substring(at + 1, 7);
                    if (DateOnly.TryParseExact(candidate, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month))
                    {
                        return month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                    }
                }
            }
            return Pegasus.Core.LondonCalendar.DateAt(DateTimeOffset.UtcNow).ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>The month a pending research card names, as "Sep 2026".</summary>
    public string PendingMarketResearchMonth =>
        DateOnly.TryParseExact(ValuationMonth + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month)
            ? month.ToString("MMM yyyy", CultureInfo.InvariantCulture)
            : ValuationMonth;

    /// <summary>
    /// Whether a guide source has a connected provider. The card says so
    /// before Get valuation is pressed, and offers the button only when it
    /// can work (operator, 28 September 2026).
    /// </summary>
    public bool GuideSourceConnected(ValuationSource source) => fetchGuideValuation.IsConnected(source);

    /// <summary>The Case's latest applied Engineer's Value, if any.</summary>
    public AppliedValuation? LatestAppliedValuation => AppliedValuations.Count > 0 ? AppliedValuations[0] : null;

    /// <summary>
    /// The recorded calculation the section shows and the calculator opens
    /// on: the latest one, while the Engineer's Value still holds its figure
    /// (operator, 6 October 2026). A different figure typed over it is the
    /// Engineer's own, so the earlier calculation is no longer shown beside
    /// it; it stays in the Case's history. Core's own test of the box decides.
    /// </summary>
    public AppliedValuation? RecordedValuation =>
        LatestAppliedValuation is { } applied
            && ValuationCalculationPolicy.IsEngineerValueBox(
                applied.AcceptedEngineerValue,
                Assessment?.Field(AssessmentVocabulary.ValueEngineer)?.Value)
            ? applied
            : null;

    /// <summary>
    /// The source the Engineer's Value came from, as the one word its label
    /// carries: the recorded calculation's basis, while the box holds that
    /// calculation's figure. A typed or empty box carries none.
    /// </summary>
    public CaseValuation? RecordedBasis =>
        RecordedValuation is { } recorded && EngineerValue == recorded.AcceptedEngineerValue
            ? Valuations.FirstOrDefault(valuation => valuation.ValuationId == recorded.GuideValuationId)
            : null;

    /// <summary>The guide cards the calculator can start from: every recorded source but the Engineer's own figure.</summary>
    public IReadOnlyList<CaseValuation> GuideValuations =>
        [.. Valuations.Where(valuation => valuation.Details.Source != ValuationSource.EngineersValue)];

    /// <summary>
    /// The recorded source's word on the Engineer's Value label: its name, or
    /// the AI tag's own word for AI market research.
    /// </summary>
    public string? RecordedBasisWord => RecordedBasis is not { } basis
        ? null
        : RecordedBasisIsResearch ? "AI" : CaseWorkspaceLabels.Valuation.SourceLabel(basis.Details.Source);

    /// <summary>Whether the recorded source is AI market research, which carries the AI tag's tone.</summary>
    public bool RecordedBasisIsResearch => RecordedBasis?.Details.Source == ValuationSource.AiMarketResearch;

    /// <summary>The section head's figure: the Engineer's Value the Case holds, or a dash.</summary>
    public string ValuationHead =>
        CaseWorkspaceLabels.Valuation.EngineersValueHead + " "
        + (EngineerValue is { } value ? ValuationCalculationPolicy.FormatMoney(value) : "—");

    /// <summary>
    /// What the section shows of the Case as saved, for the script to redraw
    /// after a commit answered in place: the head's figure, and the recorded
    /// calculation's figure and source word while the Engineer's Value holds it.
    /// </summary>
    public string ValuationSaved => JsonSerializer.Serialize(new
    {
        head = ValuationHead,
        value = RecordedBasis is null
            ? null
            : RecordedValuation!.AcceptedEngineerValue.ToString("0.00", CultureInfo.InvariantCulture),
        word = RecordedBasisWord,
        research = RecordedBasisIsResearch,
    });

    /// <summary>
    /// The guide card the calculator starts from: the recorded calculation's
    /// basis, else the first recorded guide. The calculation starts from retail, so a
    /// card recorded without a retail value is never a basis.
    /// </summary>
    public CaseValuation? DefaultBasis =>
        (RecordedValuation is { } applied
            ? Valuations.FirstOrDefault(valuation => valuation.ValuationId == applied.GuideValuationId)
            : null)
        ?? GuideValuations.FirstOrDefault(CanBeBasis);

    /// <summary>Whether a card can be the calculation's basis: it has a retail value.</summary>
    public static bool CanBeBasis(CaseValuation valuation) => valuation.Details.RetailValue is not null;

    /// <summary>Whether the claimant is VAT registered, which means there was never a commercial addition to make.</summary>
    public bool ClaimantVatRegistered =>
        string.Equals(
            Assessment?.Field(AssessmentVocabulary.SettlementClaimantVatRegistered)?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The calculation the calculator opens on: the default basis card's
    /// retail with nothing applied, from Core's own arithmetic. Null when
    /// there is no basis to calculate from.
    /// </summary>
    public ValuationCalculation? DefaultCalculation
    {
        get
        {
            if (DefaultBasis is not { } basis)
            {
                return null;
            }

            try
            {
                if (basis.Details.RetailValue is not { } basisRetail)
                {
                    return null;
                }
                return ValuationCalculationPolicy.Calculate(new ValuationCalculationInput(
                    basisRetail,
                    false,
                    ClaimantVatRegistered,
                    null,
                    [],
                    0m));
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// One value-increase row of the calculator while editing: an active
    /// preset (ticked where the recorded calculation applied it, with the amount it
    /// applied, else the preset's suggestion), or a custom row carrying an
    /// applied increase that is not an active preset, or blank.
    /// </summary>
    public sealed record ValuationIncreaseRow(
        Guid PresetId,
        long PresetVersion,
        string? Label,
        decimal? Amount,
        bool Selected);

    private IReadOnlyList<ValuationIncreaseRow>? valuationIncreaseRows;

    /// <summary>
    /// The value-increase rows, in the order the screen draws them in both
    /// modes: read mode shows only the applied ones.
    /// </summary>
    public IReadOnlyList<ValuationIncreaseRow> ValuationIncreaseRows => valuationIncreaseRows ??= IncreaseRows();

    private List<ValuationIncreaseRow> IncreaseRows()
    {
        var applied = RecordedValuation?.Calculation.Additions ?? [];
        var active = ValuationPresets.Where(preset => preset.Active && preset.RemovedAtUtc is null).ToArray();
        var rows = new List<ValuationIncreaseRow>(active.Length + 2);
        foreach (var preset in active)
        {
            var chosen = applied.FirstOrDefault(addition =>
                addition.PresetId != Guid.Empty && addition.PresetId == preset.Id);
            rows.Add(new(preset.Id, preset.Version, preset.Label, chosen?.Amount ?? preset.SuggestedAmount, chosen is not null));
        }
        // An applied increase that is not an active preset (a custom one,
        // or a preset since withdrawn) opens in a custom row, so the
        // calculation opens as it was applied.
        var others = applied
            .Where(addition => addition.PresetId == Guid.Empty || active.All(preset => preset.Id != addition.PresetId))
            .ToArray();
        for (var custom = 0; custom < Math.Max(2, others.Length); custom++)
        {
            var carried = custom < others.Length ? others[custom] : null;
            rows.Add(new(Guid.Empty, 0, carried?.Label, carried?.Amount, carried is not null));
        }
        return rows;
    }

    /// <summary>
    /// The calculation the calculator opens on while editing: the default
    /// basis card, with the recorded calculation's controls. The Case Save compares
    /// what it posts with this, so an untouched calculator adopts nothing.
    /// </summary>
    private ValuationCalculationSelection OpeningValuationSelection => new(
        DefaultBasis?.ValuationId ?? Guid.Empty,
        !ClaimantVatRegistered && RecordedValuation is { Calculation.CommercialVatApplied: true },
        RecordedValuation?.Calculation.PriorTotalLossPercentage,
        [.. ValuationIncreaseRows
            .Where(row => row.Selected)
            .Select(row => new ValuationAdditionSelection(
                row.PresetId,
                row.PresetVersion,
                row.PresetId == Guid.Empty ? row.Label : null,
                row.Amount ?? 0m))],
        RecordedValuation?.Calculation.ConditionDeduction ?? 0m);

    /// <summary>What the calculator opened on, as the page posts it back beside the calculator.</summary>
    public string OpeningValuationCanonical => ValuationSelectionForm.Canonical(
        OpeningValuationSelection,
        DefaultBasis?.Details.RetailValue?.ToString("0.00", CultureInfo.InvariantCulture),
        DefaultBasis?.Details.TradeValue?.ToString("0.00", CultureInfo.InvariantCulture));

    /// <summary>
    /// The latest recorded card of one guide source, which that source's card
    /// shows in both modes: older months stay in the history, not on screen.
    /// </summary>
    public CaseValuation? LatestRecorded(ValuationSource source) => GuideValuations
        .Where(valuation => valuation.Details.Source == source)
        .OrderByDescending(valuation => valuation.Details.Date)
        .ThenByDescending(valuation => valuation.Details.Time)
        .ThenByDescending(valuation => valuation.RecordedAtUtc)
        .FirstOrDefault();

    /// <summary>The valuation section's reads, applied to the page together.</summary>
    private sealed record ValuationSectionReads(
        IReadOnlyList<CaseValuation> Valuations,
        IReadOnlyList<AppliedValuation> AppliedValuations,
        IReadOnlyList<ValuationPreset>? Presets);

    /// <summary>
    /// Whether this request read the applied valuations, so the report
    /// snapshot may use them rather than reading them again.
    /// </summary>
    private bool appliedValuationsLoaded;

    /// <summary>
    /// The mounted section's reads. The full page takes the pending research
    /// from the Case's AI jobs it reads for the Next action instead.
    /// </summary>
    private async Task LoadValuationSectionAsync(Guid caseId, ActionActor actor, CancellationToken cancellationToken)
    {
        ApplyValuationSection(await ReadValuationSectionAsync(caseId, actor, WorkSelector, cancellationToken));
        PendingMarketResearch = MarketResearchPolicy.PendingOf(
            await aiJobs.ListForSubjectAsync(caseId, cancellationToken));
    }

    private async Task<ValuationSectionReads> ReadValuationSectionAsync(
        Guid caseId,
        ActionActor actor,
        CaseWorkSelector work,
        CancellationToken cancellationToken)
    {
        var valuations = await listCaseValuations.ExecuteAsync(caseId, work, cancellationToken);
        var applied = await listAppliedValuations.ExecuteAsync(caseId, work, cancellationToken);
        var presets = StaffAuthorization.IsAuthorized(actor, StaffAccessRight.PerformCasework)
            ? await listValuationPresets.ExecuteAsync(actor, cancellationToken)
            : null;
        return new(valuations, applied, presets);
    }

    private void ApplyValuationSection(ValuationSectionReads reads)
    {
        Valuations = reads.Valuations;
        AppliedValuations = reads.AppliedValuations;
        appliedValuationsLoaded = true;
        if (reads.Presets is not null)
        {
            ValuationPresets = reads.Presets;
        }
    }

    /// <summary>
    /// The selection as the calculator posts it with the Case form. The
    /// addition rows are parallel arrays over every row on the screen;
    /// <see cref="AdditionSelected"/> holds the indexes of the ticked rows, so
    /// an unticked row posts without script and is still left out.
    /// <see cref="Opening"/> is the calculation the page opened on.
    /// </summary>
    public sealed class ValuationSelectionForm
    {
        public string? Opening { get; set; }

        public Guid GuideValuationId { get; set; }

        /// <summary>
        /// The basis when its card is not recorded yet, so it has no identity:
        /// a guide card typed in this edit. Posted only by Use this value.
        /// </summary>
        public ValuationSource? GuideSource { get; set; }

        /// <summary>
        /// The Engineer pressed Use this value: the Save records the
        /// calculation against its basis card even when the calculation is the
        /// one the page opened on. Absent on every other save.
        /// </summary>
        public bool Use { get; set; }

        public bool CommercialVat { get; set; }

        /// <summary>The prior total loss as the screen offers it: 10 or 20 (per cent), or none.</summary>
        public decimal? PriorTotalLossPercentage { get; set; }

        public decimal? ConditionDeduction { get; set; }

        public int[]? AdditionSelected { get; set; }

        public Guid[]? AdditionPresetId { get; set; }

        public long[]? AdditionPresetVersion { get; set; }

        public string?[]? AdditionLabel { get; set; }

        public decimal?[]? AdditionAmount { get; set; }

        public ValuationCalculationSelection ToSelection()
        {
            var ids = AdditionPresetId ?? [];
            var selected = new HashSet<int>(AdditionSelected ?? []);
            var additions = new List<ValuationAdditionSelection>(ids.Length);
            for (var index = 0; index < ids.Length; index++)
            {
                if (!selected.Contains(index))
                {
                    continue;
                }
                var amount = AdditionAmount is { } amounts && index < amounts.Length ? amounts[index] ?? 0m : 0m;
                var label = AdditionLabel is { } labels && index < labels.Length ? labels[index] : null;
                if (ids[index] == Guid.Empty && amount <= 0m && string.IsNullOrWhiteSpace(label))
                {
                    // A custom row left blank is no addition.
                    continue;
                }
                additions.Add(new(
                    ids[index],
                    AdditionPresetVersion is { } versions && index < versions.Length ? versions[index] : 0,
                    label,
                    amount));
            }
            return new(
                GuideValuationId,
                CommercialVat,
                PriorTotalLossPercentage is { } percentage ? percentage / 100m : null,
                additions,
                ConditionDeduction ?? 0m)
            {
                GuideSource = GuideValuationId == Guid.Empty ? GuideSource : null,
                Use = Use,
            };
        }

        /// <summary>
        /// One calculation written the one way, so the one the page opened on
        /// and the one it posts compare as text: the basis card, that card's
        /// retail and trade as shown, and every control, with amounts to two
        /// places.
        /// </summary>
        public static string Canonical(ValuationCalculationSelection selection, string? basisRetail, string? basisTrade)
        {
            ArgumentNullException.ThrowIfNull(selection);
            static string Amount(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
            static string? Figure(string? shown) => string.IsNullOrWhiteSpace(shown)
                ? null
                : decimal.TryParse(shown.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                    ? Amount(parsed)
                    : shown.Trim();
            return JsonSerializer.Serialize(new
            {
                basis = selection.GuideValuationId,
                source = selection.GuideValuationId == Guid.Empty ? selection.GuideSource?.ToString() : null,
                retail = Figure(basisRetail),
                trade = Figure(basisTrade),
                vat = selection.CommercialVat,
                ptl = selection.PriorTotalLossPercentage is { } percentage ? Amount(percentage) : null,
                deduction = Amount(selection.ConditionDeduction),
                increases = selection.Additions.Select(addition => new
                {
                    preset = addition.PresetId,
                    version = addition.PresetId == Guid.Empty ? 0 : addition.PresetVersion,
                    label = addition.PresetId == Guid.Empty ? addition.Label?.Trim() : null,
                    amount = Amount(addition.Amount),
                }),
            });
        }
    }

    /// <summary>
    /// The calculation this save records against its basis card: the posted
    /// one, when the Engineer pressed Use this value (operator, 28 September
    /// 2026: an unchanged default figure can be used too), or when what the
    /// calculator shows changed since the page opened (operator, 23 September
    /// 2026) — a different basis card, the basis card's retail or trade, or
    /// any calculator control. Any other save records nothing, so an unrelated
    /// save never adopts. A basis card left with no retail to calculate from
    /// records nothing, and is refused when the Engineer asked to use it. The
    /// values themselves are the boxes the same save writes.
    /// </summary>
    private static ValuationCalculationSelection? ChosenCalculation(
        ValuationSelectionForm? selection,
        GuideEntryForm[] guideEntries,
        IReadOnlyList<CaseValuation> recorded)
    {
        if (selection is null)
        {
            return null;
        }

        // The basis card: a recorded one by its identity, or the source's
        // latest recorded card (there may be none) for a card typed in this edit.
        CaseValuation? basis;
        ValuationSource source;
        if (selection.GuideValuationId != Guid.Empty)
        {
            basis = recorded.FirstOrDefault(card => card.ValuationId == selection.GuideValuationId);
            if (basis is null)
            {
                return null;
            }
            source = basis.Details.Source;
        }
        else if (selection.GuideSource is { } chosen && ValuationSources.IsGuide(chosen))
        {
            source = chosen;
            basis = recorded
                .Where(card => card.Details.Source == chosen)
                .OrderByDescending(card => card.Details.Date)
                .ThenByDescending(card => card.Details.Time)
                .ThenByDescending(card => card.RecordedAtUtc)
                .FirstOrDefault();
        }
        else
        {
            return null;
        }

        // The basis card's figures as the page now shows them: a guide
        // source's card shows them in its own boxes, any other card as recorded.
        var shown = guideEntries.FirstOrDefault(entry => entry.Source == source);
        var shownRetail = shown is null
            ? basis?.Details.RetailValue?.ToString("0.00", CultureInfo.InvariantCulture)
            : shown.RetailValue;
        var shownTrade = shown is null
            ? basis?.Details.TradeValue?.ToString("0.00", CultureInfo.InvariantCulture)
            : shown.TradeValue;
        if (string.IsNullOrWhiteSpace(shownRetail))
        {
            if (selection.Use)
            {
                throw new InvalidOperationException("Enter the retail value on the card you chose to use.");
            }
            return null;
        }

        var posted = selection.ToSelection();
        if (selection.Use)
        {
            return posted;
        }
        return string.Equals(
            ValuationSelectionForm.Canonical(posted, shownRetail, shownTrade),
            selection.Opening,
            StringComparison.Ordinal)
            ? null
            : posted;
    }

    /// <summary>
    /// The calculation for the posted selection, computed by Core and
    /// returned as the preview partial. Script calls it on every change; it
    /// writes nothing. The Engineer sees what the Save will use: the basis
    /// retail as typed (<paramref name="basisRetail"/>) and the claimant's VAT
    /// position as the form holds it, both resolved by the one Core owner. A
    /// calculation that cannot be worked out answers with its own reason, not
    /// an empty state.
    /// </summary>
    public async Task<IActionResult> OnPostPreviewValuationAsync(
        Guid id,
        ValuationSelectionForm selection,
        string? basisRetail,
        [FromForm(Name = "assessmentFields")] Dictionary<string, string?>? assessmentFields,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!await HasAssessmentAccessAsync(id, actor, cancellationToken))
        {
            return FragmentNotFound();
        }

        // No basis chosen is not a fault: there is nothing to calculate yet.
        // (A post with no calculator field at all binds no selection.)
        selection ??= new();
        if (selection.GuideValuationId == Guid.Empty && selection.GuideSource is null)
        {
            return Partial("Cases/Shared/_CaseValuationPreview", (ValuationCalculation?)null);
        }

        try
        {
            // A retail box posted empty is "no retail" (Core answers that it is
            // required), never the recorded card: the Save reads the same box.
            decimal? retail = null;
            if (!string.IsNullOrWhiteSpace(basisRetail))
            {
                retail = decimal.TryParse(
                    basisRetail.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var typed)
                    ? typed
                    : throw new InvalidOperationException("The basis retail value is not a number.");
            }
            else if (Request.Form.ContainsKey(nameof(basisRetail)))
            {
                retail = 0m;
            }
            bool? claimantVat = assessmentFields is not null
                && assessmentFields.TryGetValue(AssessmentVocabulary.SettlementClaimantVatRegistered, out var vat)
                ? string.Equals(vat, "true", StringComparison.Ordinal)
                : null;
            var preview = await previewValuation.ExecuteAsync(
                new(id, actor, selection.ToSelection())
                {
                    GuideRetailValue = retail,
                    ClaimantVatRegistered = claimantVat,
                    Work = WorkSelector,
                },
                cancellationToken);
            return Partial("Cases/Shared/_CaseValuationPreview", preview.Calculation);
        }
        catch (ValuationPresetException)
        {
            return RefusedPreview(CaseWorkspaceLabels.Valuation.PresetChanged);
        }
        catch (InvalidOperationException exception)
        {
            return RefusedPreview(exception.Message);
        }
        catch (KeyNotFoundException)
        {
            return RefusedPreview(CaseWorkspaceLabels.Valuation.BasisGone);
        }
        catch (ArgumentException)
        {
            return RefusedPreview(CaseWorkspaceLabels.Valuation.CannotCalculate);
        }
    }

    /// <summary>The preview partial carrying the reason the calculation could not be worked out.</summary>
    private PartialViewResult RefusedPreview(string message)
    {
        // Partial(name, null) hands the view an empty ViewData, so the reason
        // travels on a typed copy of this page's own.
        var viewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary<ValuationCalculation?>(ViewData, null);
        viewData["ValuationRefusal"] = message;
        return new PartialViewResult { ViewName = "Cases/Shared/_CaseValuationPreview", ViewData = viewData };
    }

    /// <summary>
    /// AI market research for the chosen month, through the existing job.
    /// </summary>
    public async Task<IActionResult> OnPostStartMarketResearchAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        string? guideMonth,
        CancellationToken cancellationToken)
    {
        var guard = await GuardValuationCommandAsync(id, operationKey, editLeaseToken, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var today = Pegasus.Core.LondonCalendar.DateAt(DateTimeOffset.UtcNow);
            var month = ParseGuideMonth(guideMonth) ?? new DateOnly(today.Year, today.Month, 1);
            await startMarketResearch.ExecuteAsync(new(id, month, actor, operationKey), cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            TempData["CaseError"] = MutationRefusalMessage(
                exception, "The market research was not started. Retry the operation.");
            return RedirectToValuation(id);
        }

        // The job takes no lease: the edit session carries on as it was.
        PreserveLeaseState(id, editLeaseToken);
        TempData["CaseStatus"] = "AI market research was started.";
        return RedirectToValuation(id);
    }

    /// <summary>
    /// One guide source's card as the Case save posts it: the card's boxes
    /// joined to the one Case form (23 September 2026). The boxes are text so a
    /// card left blank posts as nothing rather than as a binding error.
    /// </summary>
    public sealed class GuideEntryForm
    {
        public ValuationSource Source { get; set; }

        public string? GuideMonth { get; set; }

        public string? RetailValue { get; set; }

        public string? TradeValue { get; set; }
    }

    /// <summary>
    /// The guide cards this save records, each with whatever of its boxes
    /// were entered (operator, 23 September 2026: any box may be left blank).
    /// A card with every box empty records nothing, and so does a card still
    /// showing the source's recorded figures, so an untouched card never
    /// writes a row. Each card is stamped with the moment of the save, as a
    /// hand-recorded guide card always was.
    /// </summary>
    private static List<ValuationDetails> GuideEntriesToRecord(
        GuideEntryForm[] forms,
        IReadOnlyList<CaseValuation> recorded)
    {
        var recordedAt = Pegasus.Core.LondonCalendar.TimeAt(DateTimeOffset.UtcNow);
        var entries = new List<ValuationDetails>(forms.Length);
        foreach (var form in forms)
        {
            if (new[] { form.RetailValue, form.TradeValue, form.GuideMonth }.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var details = new ValuationDetails(
                form.Source,
                DateOnly.FromDateTime(recordedAt),
                TimeOnly.FromDateTime(recordedAt),
                // A guide card carries no mileage: the Case's own is used
                // wherever a valuation needs one (operator, 24 September 2026).
                null,
                Box(form.RetailValue, value => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture)),
                Box(form.TradeValue, value => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture)),
                ParseGuideMonth(form.GuideMonth));
            // A card carries no mileage, so one a card recorded earlier is no difference.
            if (ValuationPolicy.FindReplaced(details, recorded) is { } replaced
                && ValuationPolicy.IsUnchanged(details with { Mileage = replaced.Details.Mileage }, replaced.Details))
            {
                continue;
            }

            entries.Add(details);
        }
        return entries;
    }

    // One box of a card: absent when left blank. The boxes are number
    // inputs, so a value that does not read as a number is not a Case field.
    private static T? Box<T>(string? text, Func<string, T> parse) where T : struct
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        try
        {
            return parse(text.Trim());
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("A submitted Case field is invalid.", exception);
        }
        catch (OverflowException exception)
        {
            throw new InvalidOperationException("A submitted Case field is invalid.", exception);
        }
    }

    /// <summary>Whether the caller asked for the figures as JSON: the card's own script does.</summary>
    private bool AnswersJson =>
        Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

    private static JsonResult RefusedJson(int statusCode) =>
        new(new { status = "refused" }) { StatusCode = statusCode };

    /// <summary>
    /// Get valuation for one guide source: the connected provider's figures for
    /// the chosen month, for the card to show in its boxes. The figures are not
    /// written here; the Case save records the card. A VIN the source names
    /// fills the Case's empty VIN as system work, and the edit session carries
    /// on as it was. The card's script asks for JSON — the figures, "unavailable" while
    /// the source has no connected provider, "not_valued" and its sentence when
    /// the source answers that it cannot value the vehicle, or a refusal and its message — so
    /// the page is never redrawn and nothing unsaved is put at risk. Any other
    /// caller is answered on the Valuation section.
    /// </summary>
    public async Task<IActionResult> OnPostGetValuationAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        long expectedVersion,
        string? guideMonth,
        ValuationSource source,
        CancellationToken cancellationToken)
    {
        var json = AnswersJson;
        var guard = await GuardValuationCommandAsync(id, operationKey, editLeaseToken, cancellationToken);
        if (guard is not null)
        {
            if (!json)
            {
                return guard;
            }
            var refusal = TempData["CaseError"] as string;
            TempData.Remove("CaseError");
            return new JsonResult(new { status = "refused", message = refusal })
            {
                StatusCode = guard switch
                {
                    ForbidResult => StatusCodes.Status403Forbidden,
                    NotFoundResult => StatusCodes.Status404NotFound,
                    _ => StatusCodes.Status409Conflict
                }
            };
        }
        if (!TryGetActor(out var actor))
        {
            return json ? RefusedJson(StatusCodes.Status403Forbidden) : (IActionResult)Forbid();
        }

        // Only system work is written here, so the lease stands as it was in every outcome.
        PreserveLeaseState(id, editLeaseToken);
        try
        {
            var today = Pegasus.Core.LondonCalendar.DateAt(DateTimeOffset.UtcNow);
            var month = ParseGuideMonth(guideMonth) ?? new DateOnly(today.Year, today.Month, 1);
            var quote = await fetchGuideValuation.ExecuteAsync(
                new(id, expectedVersion, actor, operationKey, editLeaseToken!, source, month) { Work = WorkSelector },
                cancellationToken);
            if (json)
            {
                return new JsonResult(new
                {
                    status = "ok",
                    retail = quote.RetailValue.ToString("0.00", CultureInfo.InvariantCulture),
                    trade = quote.TradeValue.ToString("0.00", CultureInfo.InvariantCulture),
                    guideMonth = quote.GuideMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture)
                });
            }
        }
        catch (StaffAuthorizationException)
        {
            return json ? RefusedJson(StatusCodes.Status403Forbidden) : (IActionResult)Forbid();
        }
        catch (GuideValuationProviderUnavailableException)
        {
            if (json)
            {
                return new JsonResult(new { status = "unavailable" });
            }
            TempData["CaseError"] = CaseWorkspaceLabels.Valuation.Unavailable(source);
        }
        catch (GuideValuationNotValuedException notValued)
        {
            var message = CaseWorkspaceLabels.Valuation.NotValued(notValued.Reason);
            if (json)
            {
                return new JsonResult(new { status = "not_valued", message });
            }
            TempData["CaseError"] = message;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or KeyNotFoundException)
        {
            var message = MutationRefusalMessage(
                exception, "The valuation was not fetched. Retry the operation.");
            if (json)
            {
                return new JsonResult(new { status = "refused", message });
            }
            TempData["CaseError"] = message;
        }

        return RedirectToValuation(id);
    }
}
