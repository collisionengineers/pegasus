using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Web.Mcp;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ConfigurationModel(
    GetWorkflowConfiguration getWorkflowConfiguration,
    UpdateWorkflowConfiguration updateWorkflowConfiguration,
    LabourRateCardAdministration rateCards) : AdministrationPageModel
{
    public CaseWorkflowConfiguration Configuration { get; private set; } = null!;
    public IReadOnlyList<LabourRateCard> RateCards { get; private set; } = [];
    public bool AutomationComposed { get; private set; }
    public bool AddCardOpen { get; private set; }
    public bool CardPostFailed { get; private set; }
    public string HourlyRateInput { get; private set; } = string.Empty;

    [BindProperty] public long WorkflowExpectedVersion { get; set; }
    [BindProperty] public string WorkflowOperationKey { get; set; } = NewOperationKey();
    [BindProperty] public bool RequireInstructions { get; set; }
    [BindProperty] public bool RequireImages { get; set; }
    [BindProperty] public int ChaseIntervalDays { get; set; } = 7;
    [BindProperty] public int UnidentifiedTargetDays { get; set; }
    [BindProperty] public int TriageTargetDays { get; set; } = 1;
    [BindProperty] public int HeldTargetDays { get; set; } = 7;
    [BindProperty] public int ReviewTargetDays { get; set; } = 1;
    [BindProperty] public int AiDraftTargetDays { get; set; } = 1;

    [BindProperty] public Guid CardId { get; set; }
    [BindProperty] public long CardExpectedVersion { get; set; }
    [BindProperty] public string CardOperationKey { get; set; } = NewOperationKey();
    [BindProperty] public string? CardName { get; set; } = string.Empty;
    [BindProperty] public decimal HourlyRate { get; set; }
    [BindProperty] public bool Enabled { get; set; } = true;
    [BindProperty] public string? Reason { get; set; }

    /// <summary>One workflow setting: its posted field, label, range, the saved value and the value the form carries.</summary>
    public sealed record WorkflowSetting(string Field, string Label, int Minimum, int Maximum, int Current, int Posted);

    /// <summary>The chase interval and five Work Centre due targets.</summary>
    public IReadOnlyList<WorkflowSetting> WorkflowSettings => Configuration is null ? [] :
    [
        new(nameof(ChaseIntervalDays), "Chase interval", 1, 365, Configuration.ChaseIntervalDays, ChaseIntervalDays),
        new(nameof(UnidentifiedTargetDays), "Unidentified target", CaseWorkflowConfiguration.MinimumTargetDays, CaseWorkflowConfiguration.MaximumTargetDays, Configuration.UnidentifiedTargetDays, UnidentifiedTargetDays),
        new(nameof(TriageTargetDays), "Triage target", CaseWorkflowConfiguration.MinimumTargetDays, CaseWorkflowConfiguration.MaximumTargetDays, Configuration.TriageTargetDays, TriageTargetDays),
        new(nameof(HeldTargetDays), "Held decision target", CaseWorkflowConfiguration.MinimumTargetDays, CaseWorkflowConfiguration.MaximumTargetDays, Configuration.HeldTargetDays, HeldTargetDays),
        new(nameof(ReviewTargetDays), "Review target", CaseWorkflowConfiguration.MinimumTargetDays, CaseWorkflowConfiguration.MaximumTargetDays, Configuration.ReviewTargetDays, ReviewTargetDays),
        new(nameof(AiDraftTargetDays), "AI draft target", CaseWorkflowConfiguration.MinimumTargetDays, CaseWorkflowConfiguration.MaximumTargetDays, Configuration.AiDraftTargetDays, AiDraftTargetDays)
    ];

    public static string Days(int days) => days == 1 ? "1 day" : $"{days} days";

    public string WorkflowInputValue(string field, int posted) =>
        ModelState.TryGetValue(field, out var entry) && entry.AttemptedValue is { } attempted
            ? attempted
            : posted.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private bool Posted(string field) => Request.HasFormContentType && Request.Form.ContainsKey(field);

    private void ValidateWorkflowRanges()
    {
        foreach (var setting in WorkflowSettings)
        {
            if (setting.Posted < setting.Minimum || setting.Posted > setting.Maximum)
            {
                ModelState.AddModelError(
                    setting.Field,
                    $"{setting.Label} must be between {setting.Minimum} and {setting.Maximum} days.");
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
                var current = Configuration;
                await updateWorkflowConfiguration.ExecuteAsync(
                    new(checked((int)WorkflowExpectedVersion), actor, WorkflowOperationKey)
                    {
                        RequireInstructions = RequireInstructions,
                        RequireImages = RequireImages,
                        ChaseIntervalDays = ChaseIntervalDays,
                        UnidentifiedTargetDays = Posted(nameof(UnidentifiedTargetDays)) ? UnidentifiedTargetDays : current.UnidentifiedTargetDays,
                        TriageTargetDays = Posted(nameof(TriageTargetDays)) ? TriageTargetDays : current.TriageTargetDays,
                        HeldTargetDays = Posted(nameof(HeldTargetDays)) ? HeldTargetDays : current.HeldTargetDays,
                        ReviewTargetDays = Posted(nameof(ReviewTargetDays)) ? ReviewTargetDays : current.ReviewTargetDays,
                        AiDraftTargetDays = Posted(nameof(AiDraftTargetDays)) ? AiDraftTargetDays : current.AiDraftTargetDays
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

    public async Task<IActionResult> OnPostNewCardAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        ModelState.Remove(nameof(WorkflowOperationKey));
        ModelState.Remove(nameof(CardOperationKey));
        await LoadAsync(actor, initializeWorkflowSettings: true, cancellationToken);
        AddCardOpen = true;
        CardId = Guid.NewGuid();
        CardExpectedVersion = 0;
        CardOperationKey = NewOperationKey();
        CardName = string.Empty;
        HourlyRate = 0;
        HourlyRateInput = string.Empty;
        Enabled = true;
        Reason = null;
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
                    CardName ?? string.Empty,
                    HourlyRate,
                    Enabled,
                    CardExpectedVersion,
                    actor,
                    Reason,
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
        AddCardOpen = !RateCards.Any(card => card.Id == CardId);
        return Page();
    }

    private async Task LoadAsync(ActionActor actor, bool initializeWorkflowSettings, CancellationToken cancellationToken)
    {
        AutomationComposed = HttpContext.RequestServices.GetService<AutomationClientRegistry>() is not null;
        Configuration = await getWorkflowConfiguration.ExecuteAsync(actor, cancellationToken);
        RateCards = await rateCards.ListAsync(actor, cancellationToken);
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
