using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Labels = Pegasus.Web.Presentation.OperatorLabels.WorkflowConfiguration;

namespace Pegasus.Web.Pages.Administration;

/// <summary>
/// Workflow configuration (FRD-17): one panel per concern, each with its own
/// Save (v50 A to E; operator, 10 November 2026; R910). A panel posts only its
/// own settings; the handler reads the stored settings, lays the panel's over
/// them and saves under the version the page was drawn with, so a save made
/// after another Administrator's is still refused. Send a photo link's
/// settings are on Administration › Photo links (R911) and are kept as stored.
/// </summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ConfigurationModel(
    GetWorkflowConfiguration getWorkflowConfiguration,
    UpdateWorkflowConfiguration updateWorkflowConfiguration,
    LabourRateCardAdministration rateCards,
    ICaseReferenceSuffixControl caseReferenceSuffix) : AdministrationPageModel
{
    /// <summary>The panel a post came from.</summary>
    private enum Panel { Review, Reference, Targets }

    public CaseWorkflowConfiguration Configuration { get; private set; } = null!;
    public IReadOnlyList<LabourRateCard> RateCards { get; private set; } = [];

    /// <summary>The temporary Case/PO suffix switch as stored (R656).</summary>
    public bool CaseReferenceSuffixStored { get; private set; }

    [BindProperty] public long WorkflowExpectedVersion { get; set; }
    [BindProperty] public string WorkflowOperationKey { get; set; } = NewOperationKey();
    [BindProperty] public bool RequireInstructions { get; set; }
    [BindProperty] public bool RequireImages { get; set; }
    [BindProperty] public bool CaseReferenceSuffix { get; set; }
    [BindProperty] public int? ChaseIntervalDays { get; set; }
    [BindProperty] public int? UnidentifiedTargetDays { get; set; }
    [BindProperty] public int? TriageTargetDays { get; set; }
    [BindProperty] public int? HeldTargetDays { get; set; }
    [BindProperty] public int? ReviewTargetDays { get; set; }
    [BindProperty] public int? AiDraftTargetDays { get; set; }

    // The labour-rate cards save together (v50 E): one row per card, each
    // with its own version and operation key.
    [BindProperty] public Guid[] CardIds { get; set; } = [];
    [BindProperty] public string[] HourlyRates { get; set; } = [];
    [BindProperty] public long[] CardVersions { get; set; } = [];
    [BindProperty] public string[] CardOperationKeys { get; set; } = [];

    /// <summary>The rates as typed on a refused save, by card; empty otherwise.</summary>
    public IReadOnlyDictionary<Guid, string> PostedRates { get; private set; } = new Dictionary<Guid, string>();

    /// <summary>
    /// The chase interval and five Work Centre due targets, each with the value
    /// the form carries; Core owns every label and range.
    /// </summary>
    public IReadOnlyList<(WorkflowDaySetting Setting, int? Posted)> WorkflowSettings =>
    [
        (WorkflowDaySetting.ChaseInterval, ChaseIntervalDays),
        (WorkflowDaySetting.UnidentifiedTarget, UnidentifiedTargetDays),
        (WorkflowDaySetting.TriageTarget, TriageTargetDays),
        (WorkflowDaySetting.HeldTarget, HeldTargetDays),
        (WorkflowDaySetting.ReviewTarget, ReviewTargetDays),
        (WorkflowDaySetting.AiDraftTarget, AiDraftTargetDays)
    ];

    public string WorkflowInputValue(string field, int? posted) =>
        ModelState.TryGetValue(field, out var entry) && entry.AttemptedValue is { } attempted
            ? attempted
            : posted?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    public string RateInputValue(LabourRateCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return PostedRates.TryGetValue(card.Id, out var posted)
            ? posted
            : card.HourlyRate.ToString(CultureInfo.InvariantCulture);
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        await LoadAsync(actor, cancellationToken);
        DrawStored();
        return Page();
    }

    public Task<IActionResult> OnPostSaveReviewAsync(CancellationToken cancellationToken) =>
        SavePanelAsync(Panel.Review, cancellationToken);

    public Task<IActionResult> OnPostSaveReferenceAsync(CancellationToken cancellationToken) =>
        SavePanelAsync(Panel.Reference, cancellationToken);

    public Task<IActionResult> OnPostSaveTargetsAsync(CancellationToken cancellationToken) =>
        SavePanelAsync(Panel.Targets, cancellationToken);

    private async Task<IActionResult> SavePanelAsync(Panel panel, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();

        // What this panel posted; every other setting is drawn as stored.
        var version = WorkflowExpectedVersion;
        var operationKey = WorkflowOperationKey;
        var (instructions, images, suffix) = (RequireInstructions, RequireImages, CaseReferenceSuffix);
        var days = WorkflowSettings.Select(entry => entry.Posted).ToArray();
        await LoadAsync(actor, cancellationToken);
        DrawStored();
        WorkflowExpectedVersion = version;
        WorkflowOperationKey = operationKey;
        switch (panel)
        {
            case Panel.Review:
                (RequireInstructions, RequireImages) = (instructions, images);
                break;
            case Panel.Reference:
                CaseReferenceSuffix = suffix;
                break;
            case Panel.Targets:
                (ChaseIntervalDays, UnidentifiedTargetDays, TriageTargetDays, HeldTargetDays, ReviewTargetDays, AiDraftTargetDays) =
                    (days[0], days[1], days[2], days[3], days[4], days[5]);
                ValidateWorkflowRanges();
                break;
        }

        if (WorkflowExpectedVersion < 1 || WorkflowExpectedVersion > int.MaxValue)
            ModelState.AddModelError(string.Empty, "The settings version is invalid. Reload and try again.");
        if (!IsOperationKeyValid(WorkflowOperationKey))
            ModelState.AddModelError(string.Empty, "The form has expired. Reload and try again.");
        if (!ModelState.IsValid) return Page();

        try
        {
            if (panel == Panel.Reference)
            {
                // The suffix switch is its own record (R656): saved only when it
                // changes, so an unchanged save writes no history.
                if (CaseReferenceSuffix != CaseReferenceSuffixStored)
                {
                    await caseReferenceSuffix.SetEnabledAsync(
                        CaseReferenceSuffix, actor, WorkflowOperationKey, cancellationToken);
                }
            }
            else
            {
                // ValidateWorkflowRanges refused every missing value above; the
                // other panels' values are the stored ones.
                await updateWorkflowConfiguration.ExecuteAsync(
                    new(checked((int)WorkflowExpectedVersion), actor, WorkflowOperationKey)
                    {
                        RequireInstructions = RequireInstructions,
                        RequireImages = RequireImages,
                        ChaseIntervalDays = ChaseIntervalDays!.Value,
                        UnidentifiedTargetDays = UnidentifiedTargetDays!.Value,
                        TriageTargetDays = TriageTargetDays!.Value,
                        HeldTargetDays = HeldTargetDays!.Value,
                        ReviewTargetDays = ReviewTargetDays!.Value,
                        AiDraftTargetDays = AiDraftTargetDays!.Value,
                        PhotoLinkStartMode = Configuration.PhotoLinkStartMode,
                        PhotoLinkStartLiveCameraOnly = Configuration.PhotoLinkStartLiveCameraOnly,
                        PhotoLinkStartOriginals = Configuration.PhotoLinkStartOriginals,
                        PhotoLinkLifetimesOffered = Configuration.PhotoLinkLifetimesOffered,
                        PhotoLinkDefaultLifetimeDays = Configuration.PhotoLinkDefaultLifetimeDays
                    },
                    cancellationToken);
            }

            TempData["Confirmation"] = Labels.Saved;
            return RedirectToPage();
        }
        catch (WorkflowConfigurationVersionConflictException)
        {
            ModelState.AddModelError(string.Empty, Labels.Changed);
        }
        catch (WorkflowConfigurationOperationConflictException)
        {
            ModelState.AddModelError(string.Empty, Labels.FormUsed);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        return Page();
    }

    /// <summary>
    /// Saves whichever labour-rate card the Administrator changed (v50 E), each
    /// under its own version; an unchanged card is not written.
    /// </summary>
    public async Task<IActionResult> OnPostSaveCardsAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        ModelState.Remove(nameof(WorkflowOperationKey));
        var posted = CardIds.Length == HourlyRates.Length
            && CardIds.Length == CardVersions.Length
            && CardIds.Length == CardOperationKeys.Length;
        await LoadAsync(actor, cancellationToken);
        DrawStored();
        if (!posted || CardIds.Length == 0)
        {
            ModelState.AddModelError(string.Empty, "The rate-card form has expired. Reload the page and try again.");
            return Page();
        }

        PostedRates = CardIds.Zip(HourlyRates).ToDictionary(pair => pair.First, pair => pair.Second ?? string.Empty);
        var changes = new List<(LabourRateCard Card, decimal Rate, long Version, string Key)>();
        for (var index = 0; index < CardIds.Length; index++)
        {
            var card = RateCards.FirstOrDefault(candidate => candidate.Id == CardIds[index]);
            if (card is null || !IsOperationKeyValid(CardOperationKeys[index]))
            {
                ModelState.AddModelError(string.Empty, "The rate-card form has expired. Reload the page and try again.");
                return Page();
            }

            // The box is a required number box; a rate it cannot hold is not the page's post.
            if (!decimal.TryParse(HourlyRates[index], NumberStyles.Number, CultureInfo.InvariantCulture, out var rate))
            {
                return BadRequest();
            }

            if (rate != card.HourlyRate)
            {
                changes.Add((card, rate, CardVersions[index], CardOperationKeys[index]));
            }
        }

        try
        {
            foreach (var (card, rate, version, key) in changes)
            {
                await rateCards.SaveAsync(new(card.Id, rate, version, actor, Reason: null, key), cancellationToken);
            }

            TempData["Confirmation"] = Labels.CardSaved;
            return RedirectToPage();
        }
        catch (LabourRateCardConflictException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
        }

        // A card saved before the refusal shows as stored; the rest as typed.
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    // A value the binder could not read keeps the binder's own refusal; an
    // omitted or out-of-range value is refused with the setting's range (R435).
    private void ValidateWorkflowRanges()
    {
        foreach (var (setting, posted) in WorkflowSettings)
        {
            if (!setting.Accepts(posted)
                && ModelState.GetFieldValidationState(setting.Field) != ModelValidationState.Invalid)
            {
                ModelState.AddModelError(setting.Field, setting.RangeMessage);
            }
        }
    }

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        // Independent reads, each on a context of its own from the factory,
        // so they run side by side.
        var configuration = getWorkflowConfiguration.ExecuteAsync(actor, cancellationToken);
        var cards = rateCards.ListAsync(actor, cancellationToken);
        var suffix = caseReferenceSuffix.IsEnabledAsync(cancellationToken);
        await Task.WhenAll(configuration, cards, suffix);
        Configuration = await configuration;
        RateCards = await cards;
        CaseReferenceSuffixStored = await suffix;
    }

    /// <summary>Every panel's values as stored, with a fresh version and operation key.</summary>
    private void DrawStored()
    {
        WorkflowExpectedVersion = Configuration.PolicyVersion;
        WorkflowOperationKey = NewOperationKey();
        RequireInstructions = Configuration.RequireInstructions;
        RequireImages = Configuration.RequireImages;
        CaseReferenceSuffix = CaseReferenceSuffixStored;
        ChaseIntervalDays = Configuration.ChaseIntervalDays;
        UnidentifiedTargetDays = Configuration.UnidentifiedTargetDays;
        TriageTargetDays = Configuration.TriageTargetDays;
        HeldTargetDays = Configuration.HeldTargetDays;
        ReviewTargetDays = Configuration.ReviewTargetDays;
        AiDraftTargetDays = Configuration.AiDraftTargetDays;
    }
}
