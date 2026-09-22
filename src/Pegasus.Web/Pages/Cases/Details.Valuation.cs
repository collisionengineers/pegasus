using System.Globalization;
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
/// Core policy shape — presets, a preview that Core computes, an Apply that
/// adopts the Engineer's Value with its applied-history row — and the
/// per-source Get valuation controls: AI market research starts the existing
/// Market research job for the chosen month, and every guide source posts to
/// GetValuation, which records the connected provider's figures as a card
/// and answers with a notice while that source has no provider.
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

    /// <summary>The guide card the calculator starts from: the latest adoption's basis, else the first recorded guide.</summary>
    public CaseValuation? DefaultBasis =>
        (LatestAppliedValuation is { } applied
            ? Valuations.FirstOrDefault(valuation => valuation.ValuationId == applied.GuideValuationId)
            : null)
        ?? (GuideValuations.Count > 0 ? GuideValuations[0] : null);

    /// <summary>Whether the claimant is VAT registered, which means there was never a commercial addition to make.</summary>
    public bool ClaimantVatRegistered =>
        string.Equals(
            Assessment?.Field(AssessmentVocabulary.SettlementClaimantVatRegistered)?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase);

    /// <summary>The card's own last-written stamp, which an Apply pins itself to.</summary>
    public static DateTimeOffset StampOf(CaseValuation valuation) =>
        valuation.LastEditedAtUtc ?? valuation.RecordedAtUtc;

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
                return ValuationCalculationPolicy.Calculate(new ValuationCalculationInput(
                    basis.Details.RetailValue,
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
    /// The calculator's preview and history, read with the section's cards.
    /// The presets are read only while the section edits, because read mode
    /// shows applied increases alone.
    /// </summary>
    private async Task LoadValuationSectionAsync(Guid caseId, ActionActor actor, CancellationToken cancellationToken)
    {
        Valuations = await listCaseValuations.ExecuteAsync(caseId, cancellationToken);
        AppliedValuations = await listAppliedValuations.ExecuteAsync(caseId, cancellationToken);
        PendingMarketResearch = await marketResearchQueries.GetPendingAsync(caseId, cancellationToken);
        if (LatestAppliedValuation is { } applied)
        {
            AppliedByDisplayName = Guid.TryParse(applied.AcceptedBy, out var staffId)
                ? (await staffAccountQueries.GetAsync(staffId, cancellationToken))?.UserName ?? ActorDisplayNames.UnknownStaff
                : ActorDisplayNames.UnknownStaff;
        }
        if (CanEditEngineering)
        {
            ValuationPresets = await listValuationPresets.ExecuteAsync(actor, cancellationToken);
        }
    }

    /// <summary>
    /// The selection as the calculator's form posts it. The addition rows are
    /// parallel arrays over every row on the screen; <see cref="AdditionSelected"/>
    /// holds the indexes of the ticked rows, so an unticked row posts without
    /// script and is still left out.
    /// </summary>
    public sealed class ValuationSelectionForm
    {
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
    /// Apply as Engineer's Value: adopts the calculated proposal (or the
    /// Engineer's corrected figure over the same basis) and records the
    /// applied-history row. The stamp pins the adoption to the card shown.
    /// </summary>
    public async Task<IActionResult> OnPostApplyValuationAsync(
        Guid id,
        long expectedVersion,
        string operationKey,
        string? editLeaseToken,
        DateTimeOffset guideValuationStampUtc,
        string? reason,
        ValuationSelectionForm selection,
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
            await applyValuation.ExecuteAsync(
                new(
                    id,
                    expectedVersion,
                    actor,
                    operationKey,
                    string.IsNullOrWhiteSpace(reason) ? "Engineer's Value applied." : reason,
                    editLeaseToken!,
                    selection.ToSelection(),
                    guideValuationStampUtc),
                cancellationToken);
            RecordEditorCommit("case-valuation-form", operationKey, expectedVersion);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or KeyNotFoundException)
        {
            HandleLeaseFailure(id, editLeaseToken, exception);
            TempData["CaseError"] = MutationRefusalMessage(
                exception, "The Engineer's Value was not applied. Retry the operation.");
            return RedirectToValuation(id);
        }

        ClearLeaseState();
        await ReclaimLeaseAsync(id, cancellationToken);
        TempData["CaseStatus"] = "The Engineer's Value was applied.";
        return RedirectToValuation(id);
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
    /// The figures Get valuation brought back for one source's card, held for
    /// the redirect that redraws the section: the card shows them in its
    /// boxes over its recorded values, and Save records them. Nothing is
    /// written until then.
    /// </summary>
    public sealed record GuideValuationPrefill(decimal RetailValue, decimal TradeValue, long Mileage, DateOnly GuideMonth);

    private static string PrefillKey(ValuationSource source) => "Valuation.Prefill:" + source;

    public GuideValuationPrefill? ValuationPrefill(ValuationSource source)
    {
        if (TempData[PrefillKey(source)] is not string held)
        {
            return null;
        }

        var parts = held.Split('|');
        return parts.Length == 4
            && decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var retail)
            && decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var trade)
            && long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mileage)
            && DateOnly.TryParseExact(parts[3], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month)
                ? new GuideValuationPrefill(retail, trade, mileage, month)
                : null;
    }

    /// <summary>
    /// Save one guide source's card (Glass's, Brego, Super CAP): the figures in
    /// its boxes, typed or brought back by Get valuation, become the source's
    /// record for that month through the one valuation save, which ends and
    /// re-claims the edit session; the same source and month replaces the
    /// earlier card.
    /// </summary>
    public async Task<IActionResult> OnPostSaveValuationAsync(
        Guid id,
        string operationKey,
        string? editLeaseToken,
        long expectedVersion,
        ValuationSource source,
        string? guideMonth,
        long mileage,
        decimal retailValue,
        decimal tradeValue,
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
            var recordedAt = Pegasus.Core.LondonCalendar.TimeAt(DateTimeOffset.UtcNow);
            await saveValuation.ExecuteAsync(
                new(
                    id,
                    // The submitted version travels unchanged: the store
                    // enforces it against the live case, and a network replay
                    // must keep the request's original fingerprint rather
                    // than being rewritten with a newer version.
                    expectedVersion,
                    actor,
                    operationKey,
                    "Valuation recorded.",
                    editLeaseToken!,
                    new(
                        source,
                        DateOnly.FromDateTime(recordedAt),
                        TimeOnly.FromDateTime(recordedAt),
                        mileage,
                        retailValue,
                        tradeValue,
                        ParseGuideMonth(guideMonth))),
                cancellationToken);
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException)
        {
            TempData["CaseError"] = MutationRefusalMessage(
                exception, "The valuation was not recorded. Retry the operation.");
            return RedirectToValuation(id);
        }

        ClearLeaseState();
        await ReclaimLeaseAsync(id, cancellationToken);
        TempData["CaseStatus"] = "The valuation was recorded.";
        return RedirectToValuation(id);
    }

    /// <summary>
    /// Get valuation for one guide source (Glass's, Brego, Super CAP): the
    /// connected provider's figures for the chosen month fill that source's
    /// card, and the edit session continues; nothing is recorded until the
    /// card is saved. A source with no connected provider answers with a
    /// notice.
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
        var guard = await GuardValuationCommandAsync(id, operationKey, editLeaseToken, cancellationToken);
        if (guard is not null)
        {
            return guard;
        }
        if (!TryGetActor(out var actor))
        {
            return Forbid();
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
            TempData[PrefillKey(source)] = string.Join(
                '|',
                quote.RetailValue.ToString(CultureInfo.InvariantCulture),
                quote.TradeValue.ToString(CultureInfo.InvariantCulture),
                quote.Mileage.ToString(CultureInfo.InvariantCulture),
                quote.GuideMonth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (GuideValuationProviderUnavailableException)
        {
            TempData["CaseError"] = CaseWorkspaceLabels.Valuation.Error;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or KeyNotFoundException)
        {
            TempData["CaseError"] = MutationRefusalMessage(
                exception, "The valuation was not fetched. Retry the operation.");
        }

        return RedirectToValuation(id);
    }
}
