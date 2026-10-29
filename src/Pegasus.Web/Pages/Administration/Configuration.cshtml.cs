using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ConfigurationModel(
    GetWorkflowConfiguration getWorkflowConfiguration,
    UpdateWorkflowConfiguration updateWorkflowConfiguration,
    LabourRateCardAdministration rateCards) : AdministrationPageModel
{
    public CaseWorkflowConfiguration Configuration { get; private set; } = null!;
    public IReadOnlyList<LabourRateCard> RateCards { get; private set; } = [];
    public bool CardPostFailed { get; private set; }
    public string HourlyRateInput { get; private set; } = string.Empty;

    [BindProperty] public long WorkflowExpectedVersion { get; set; }
    [BindProperty] public string WorkflowOperationKey { get; set; } = NewOperationKey();
    [BindProperty] public bool RequireInstructions { get; set; }
    [BindProperty] public bool RequireImages { get; set; }
    [BindProperty] public int? ChaseIntervalDays { get; set; }
    [BindProperty] public int? UnidentifiedTargetDays { get; set; }
    [BindProperty] public int? TriageTargetDays { get; set; }
    [BindProperty] public int? HeldTargetDays { get; set; }
    [BindProperty] public int? ReviewTargetDays { get; set; }
    [BindProperty] public int? AiDraftTargetDays { get; set; }

    [BindProperty] public Guid CardId { get; set; }
    [BindProperty] public long CardExpectedVersion { get; set; }
    [BindProperty] public string CardOperationKey { get; set; } = NewOperationKey();
    [BindProperty] public decimal HourlyRate { get; set; }

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
            : posted?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

    // A value the binder could not read keeps the binder's own refusal; an
    // omitted or out-of-range value is refused with the setting's range.
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

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        await LoadAsync(actor, initializeWorkflowSettings: true, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveWorkflowAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        ModelState.Remove(nameof(CardOperationKey));
        await LoadAsync(actor, initializeWorkflowSettings: false, cancellationToken);
        if (WorkflowExpectedVersion < 1 || WorkflowExpectedVersion > int.MaxValue)
            ModelState.AddModelError(string.Empty, "The settings version is invalid. Reload and try again.");
        if (!IsOperationKeyValid(WorkflowOperationKey))
            ModelState.AddModelError(string.Empty, "The form has expired. Reload and try again.");
        ValidateWorkflowRanges();

        if (ModelState.IsValid)
        {
            try
            {
                // ValidateWorkflowRanges refused every missing value above.
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
                        AiDraftTargetDays = AiDraftTargetDays!.Value
                    },
                    cancellationToken);
                TempData["Confirmation"] = "Workflow settings saved.";
                return RedirectToPage();
            }
            catch (WorkflowConfigurationVersionConflictException)
            {
                ModelState.AddModelError(string.Empty, "Workflow settings changed. Reload the page before trying again.");
            }
            catch (WorkflowConfigurationOperationConflictException)
            {
                ModelState.AddModelError(string.Empty, "This workflow settings form was already used. Reload the page before trying again.");
            }
            catch (ArgumentException exception)
            {
                ModelState.AddModelError(string.Empty, exception.Message);
            }
            await LoadAsync(actor, initializeWorkflowSettings: false, cancellationToken);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveCardAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        ModelState.Remove(nameof(WorkflowOperationKey));
        HourlyRateInput = Request.Form[nameof(HourlyRate)].ToString();
        if (string.IsNullOrWhiteSpace(HourlyRateInput))
            HourlyRateInput = HourlyRate.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await LoadAsync(actor, initializeWorkflowSettings: true, cancellationToken);

        if (CardId == Guid.Empty)
            ModelState.AddModelError(string.Empty, "The rate-card form has expired. Reload the page and try again.");
        if (!IsOperationKeyValid(CardOperationKey))
            ModelState.AddModelError(string.Empty, "The rate-card form has expired. Reload the page and try again.");

        if (ModelState.IsValid)
        {
            try
            {
                await rateCards.SaveAsync(new(
                    CardId,
                    HourlyRate,
                    CardExpectedVersion,
                    actor,
                    Reason: null,
                    CardOperationKey), cancellationToken);
                TempData["Confirmation"] = "The labour-rate card was saved.";
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
        }

        CardPostFailed = true;
        await LoadAsync(actor, initializeWorkflowSettings: true, cancellationToken);
        return Page();
    }

    private async Task LoadAsync(ActionActor actor, bool initializeWorkflowSettings, CancellationToken cancellationToken)
    {
        // Two independent reads, each on a context of its own from the factory,
        // so they run side by side.
        var configuration = getWorkflowConfiguration.ExecuteAsync(actor, cancellationToken);
        var cards = rateCards.ListAsync(actor, cancellationToken);
        await Task.WhenAll(configuration, cards);
        Configuration = await configuration;
        RateCards = await cards;
        if (!initializeWorkflowSettings) return;

        WorkflowExpectedVersion = Configuration.PolicyVersion;
        WorkflowOperationKey = NewOperationKey();
        RequireInstructions = Configuration.RequireInstructions;
        RequireImages = Configuration.RequireImages;
        ChaseIntervalDays = Configuration.ChaseIntervalDays;
        UnidentifiedTargetDays = Configuration.UnidentifiedTargetDays;
        TriageTargetDays = Configuration.TriageTargetDays;
        HeldTargetDays = Configuration.HeldTargetDays;
        ReviewTargetDays = Configuration.ReviewTargetDays;
        AiDraftTargetDays = Configuration.AiDraftTargetDays;
    }
}
