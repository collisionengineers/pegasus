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
/// records the cards and adopts a changed calculation; nothing here writes.
/// </summary>
public sealed partial class DetailsModel
{
    /// <summary>The maintained valuation additions, loaded with the section while editing.</summary>
    public IReadOnlyList<ValuationPreset> ValuationPresets { get; private set; } = [];

    /// <summary>Every adoption of an Engineer's Value on this Case, newest first.</summary>
    public IReadOnlyList<AppliedValuation> AppliedValuations { get; private set; } = [];

    /// <summary>The Market research job still in progress, so the card reads as pending.</summary>
    public AiJobRecord? PendingMarketResearch { get; private set; }

    /// <summary>
    /// The operator-facing name of whoever applied the latest Engineer's Value.
    /// The record keeps a subject identifier, which is never printed.
    /// </summary>
    public string? AppliedByDisplayName { get; private set; }

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

    /// <summary>The Case's latest applied Engineer's Value, if any.</summary>
    public AppliedValuation? LatestAppliedValuation => AppliedValuations.Count > 0 ? AppliedValuations[0] : null;

    /// <summary>The guide cards the calculator can start from: every recorded source but the Engineer's own figure.</summary>
    public IReadOnlyList<CaseValuation> GuideValuations =>
        [.. Valuations.Where(valuation => valuation.Details.Source != ValuationSource.EngineersValue)];

    /// <summary>
    /// The guide card the calculator starts from: the latest adoption's basis,
    /// else the first recorded guide. The calculation starts from retail, so a
    /// card recorded without a retail value is never a basis.
    /// </summary>
    public CaseValuation? DefaultBasis =>
        (LatestAppliedValuation is { } applied
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
    /// preset (ticked where the latest adoption applied it, with the amount it
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
    /// modes: read mode shows the preset rows and the applied ones.
    /// </summary>
    public IReadOnlyList<ValuationIncreaseRow> ValuationIncreaseRows => valuationIncreaseRows ??= IncreaseRows();

    private List<ValuationIncreaseRow> IncreaseRows()
    {
        var applied = LatestAppliedValuation?.Calculation.Additions ?? [];
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
    /// basis card, with the latest adoption's controls. The Case Save compares
    /// what it posts with this, so an untouched calculator adopts nothing.
    /// </summary>
    private ValuationCalculationSelection OpeningValuationSelection => new(
        DefaultBasis?.ValuationId ?? Guid.Empty,
        !ClaimantVatRegistered && LatestAppliedValuation is { Calculation.CommercialVatApplied: true },
        LatestAppliedValuation?.Calculation.PriorTotalLossPercentage,
        [.. ValuationIncreaseRows
            .Where(row => row.Selected)
            .Select(row => new ValuationAdditionSelection(
                row.PresetId,
                row.PresetVersion,
                row.PresetId == Guid.Empty ? row.Label : null,
                row.Amount ?? 0m))],
        LatestAppliedValuation?.Calculation.ConditionDeduction ?? 0m);

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

    /// <summary>
    /// The calculator's preview and history, read with the section's cards.
    /// The presets are read in both modes: read and edit list the same value
    /// increases, ticked where the latest adoption applied them.
    /// </summary>
    private async Task LoadValuationSectionAsync(Guid caseId, ActionActor actor, CancellationToken cancellationToken)
    {
        Valuations = await listCaseValuations.ExecuteAsync(caseId, WorkSelector, cancellationToken);
        AppliedValuations = await listAppliedValuations.ExecuteAsync(caseId, WorkSelector, cancellationToken);
        PendingMarketResearch = await marketResearchQueries.GetPendingAsync(caseId, cancellationToken);
        if (LatestAppliedValuation is { } applied)
        {
            AppliedByDisplayName = Guid.TryParse(applied.AcceptedBy, out var staffId)
                ? (await staffAccountQueries.GetAsync(staffId, cancellationToken))?.UserName ?? ActorDisplayNames.UnknownStaff
                : ActorDisplayNames.UnknownStaff;
        }
        if (StaffAuthorization.IsAuthorized(actor, StaffAccessRight.PerformCasework))
        {
            ValuationPresets = await listValuationPresets.ExecuteAsync(actor, cancellationToken);
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
                ConditionDeduction ?? 0m);
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
    /// The calculation this save adopts (operator, 23 September 2026): the
    /// posted one, when what the calculator shows changed since the page
    /// opened — a different basis card, the basis card's retail or trade (the
    /// adoption records both; operator, 24 September 2026), or any calculator
    /// control. An untouched calculator adopts nothing, and so does a basis
    /// card left with no retail to calculate from.
    /// </summary>
    private static ValuationCalculationSelection? ChangedCalculation(
        ValuationSelectionForm? selection,
        GuideEntryForm[] guideEntries,
        IReadOnlyList<CaseValuation> recorded)
    {
        if (selection is null || selection.GuideValuationId == Guid.Empty)
        {
            return null;
        }

        var basis = recorded.FirstOrDefault(card => card.ValuationId == selection.GuideValuationId);
        if (basis is null)
        {
            return null;
        }
        // The basis card's figures as the page now shows them: a guide
        // source's card shows them in its own boxes, any other card as recorded.
        var shown = guideEntries.FirstOrDefault(entry => entry.Source == basis.Details.Source);
        var shownRetail = shown is null
            ? basis.Details.RetailValue?.ToString("0.00", CultureInfo.InvariantCulture)
            : shown.RetailValue;
        var shownTrade = shown is null
            ? basis.Details.TradeValue?.ToString("0.00", CultureInfo.InvariantCulture)
            : shown.TradeValue;
        if (string.IsNullOrWhiteSpace(shownRetail))
        {
            return null;
        }

        var posted = selection.ToSelection();
        return string.Equals(
            ValuationSelectionForm.Canonical(posted, shownRetail, shownTrade),
            selection.Opening,
            StringComparison.Ordinal)
            ? null
            : posted;
    }

    /// <summary>
    /// The calculation lines for the posted selection, computed by Core and
    /// returned as the lines partial. Script calls it on every change; it
    /// writes nothing.
    /// </summary>
    public async Task<IActionResult> OnPostPreviewValuationAsync(
        Guid id,
        ValuationSelectionForm selection,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (!await HasAssessmentAccessAsync(id, actor, cancellationToken))
        {
            return NotFound();
        }

        try
        {
            var preview = await previewValuation.ExecuteAsync(
                new(id, actor, selection.ToSelection()),
                cancellationToken);
            return Partial("Cases/Shared/_CaseValuationLines", preview.Calculation);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            return Partial("Cases/Shared/_CaseValuationLines", (ValuationCalculation?)null);
        }
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
    /// the chosen month, for the card to show in its boxes. Nothing is written
    /// here and the edit session carries on as it was; the Case save records the
    /// card. The card's script asks for JSON — the figures, "unavailable" while
    /// the source has no connected provider, or a refusal and its message — so
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

        // Nothing is written here, so the lease stands as it was in every outcome.
        PreserveLeaseState(id, editLeaseToken);
        try
        {
            var today = Pegasus.Core.LondonCalendar.DateAt(DateTimeOffset.UtcNow);
            var month = ParseGuideMonth(guideMonth) ?? new DateOnly(today.Year, today.Month, 1);
            var quote = await fetchGuideValuation.ExecuteAsync(
                new(id, expectedVersion, actor, operationKey, editLeaseToken!, source, month),
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
