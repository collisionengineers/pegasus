using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ConfigurationModel(
    GetWorkflowConfiguration getWorkflowConfiguration,
    UpdateWorkflowConfiguration updateWorkflowConfiguration)
    : AdministrationPageModel
{
    public CaseWorkflowConfiguration Configuration { get; private set; } = null!;

    [BindProperty]
    public bool RequireStaffInstructionReviewBeforeEngineerAssignment { get; set; }

    [BindProperty]
    public bool RequireStaffImageReviewBeforeEngineerAssignment { get; set; }

    [BindProperty]
    [Range(1, int.MaxValue)]
    public int ExpectedVersion { get; set; }

    [BindProperty]
    [Required, StringLength(1000, MinimumLength = 1)]
    public string Reason { get; set; } = string.Empty;

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageWorkflowConfiguration);
        await LoadAsync(actor, populateForm: true, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        StaffAuthorization.Require(actor, StaffAccessRight.ManageWorkflowConfiguration);
        if (!IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        }

        if (ModelState.IsValid)
        {
            try
            {
                var updated = await updateWorkflowConfiguration.ExecuteAsync(
                    new(
                        RequireStaffInstructionReviewBeforeEngineerAssignment,
                        RequireStaffImageReviewBeforeEngineerAssignment,
                        ExpectedVersion,
                        actor,
                        Reason,
                        OperationKey),
                    cancellationToken);
                TempData["AdministrationStatus"] =
                    $"Workflow configuration version {updated.PolicyVersion} was recorded.";
                return RedirectToPage();
            }
            catch (WorkflowConfigurationVersionConflictException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "The workflow configuration changed after this form was loaded. " +
                    "Your change was not applied; review the current values and retry.");
            }
            catch (WorkflowConfigurationOperationConflictException)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "This form was already used for another change. Review the current values and retry.");
            }
        }

        await LoadAsync(actor, populateForm: false, cancellationToken);
        ExpectedVersion = Configuration.PolicyVersion;
        OperationKey = NewOperationKey();
        return Page();
    }

    private async Task LoadAsync(
        ActionActor actor,
        bool populateForm,
        CancellationToken cancellationToken)
    {
        Configuration = await getWorkflowConfiguration.ExecuteAsync(actor, cancellationToken);
        if (!populateForm)
        {
            return;
        }

        RequireStaffInstructionReviewBeforeEngineerAssignment =
            Configuration.RequireStaffInstructionReviewBeforeEngineerAssignment;
        RequireStaffImageReviewBeforeEngineerAssignment =
            Configuration.RequireStaffImageReviewBeforeEngineerAssignment;
        ExpectedVersion = Configuration.PolicyVersion;
        Reason = string.Empty;
        OperationKey = NewOperationKey();
    }
}
