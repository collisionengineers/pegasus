using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class MailCategoriesModel(
    ListApprovedOutlookCategories listCategories,
    UpdateApprovedOutlookCategory updateCategory) : AdministrationPageModel
{
    public IReadOnlyList<ApprovedOutlookCategory> Categories { get; private set; } = [];
    public Guid NewCategoryId { get; private set; }

    [BindProperty] public Guid CategoryId { get; set; }
    [BindProperty, Required, StringLength(255, MinimumLength = 1)] public string DisplayName { get; set; } = "";
    [BindProperty, Required] public string SelectedState { get; set; } = ApprovedOutlookCategoryState.Active.ToString();
    [BindProperty, Range(0, int.MaxValue)] public int ExpectedVersion { get; set; }
    [BindProperty, Required, StringLength(1000, MinimumLength = 1)] public string Reason { get; set; } = "";
    [BindProperty] public string OperationKey { get; set; } = NewOperationKey();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        Categories = await listCategories.ExecuteAsync(actor, cancellationToken);
        NewCategoryId = Guid.NewGuid();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!Enum.TryParse<ApprovedOutlookCategoryState>(SelectedState, false, out var state)
            || !Enum.IsDefined(state))
            ModelState.AddModelError(nameof(SelectedState), "Select a supported state.");
        if (CategoryId == Guid.Empty || !IsOperationKeyValid(OperationKey))
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");

        if (ModelState.IsValid)
        {
            try
            {
                var saved = await updateCategory.ExecuteAsync(new(
                    CategoryId, DisplayName, state, ExpectedVersion, actor, Reason, OperationKey), cancellationToken);
                TempData["AdministrationStatus"] = $"The Outlook category policy for {saved.DisplayName} was saved.";
                return RedirectToPage();
            }
            catch (ApprovedOutlookCategoryUpdateException exception)
            {
                ModelState.AddModelError(string.Empty, ErrorMessage(exception));
            }
            catch (ArgumentException)
            {
                ModelState.AddModelError(nameof(DisplayName), "Enter a supported display name and reason.");
            }
        }

        Categories = await listCategories.ExecuteAsync(actor, cancellationToken);
        NewCategoryId = ExpectedVersion == 0 ? CategoryId : Guid.NewGuid();
        OperationKey = NewOperationKey();
        return Page();
    }

    private static string ErrorMessage(ApprovedOutlookCategoryUpdateException exception) => exception.Error switch
    {
        ApprovedOutlookCategoryUpdateError.DuplicateDisplayName => "That display name is already configured.",
        ApprovedOutlookCategoryUpdateError.VersionConflict => "The category policy changed. Review it and retry.",
        ApprovedOutlookCategoryUpdateError.OperationConflict => "This form was already used for another change. Review and retry.",
        ApprovedOutlookCategoryUpdateError.NotFound => "The category policy no longer exists.",
        _ => "The category policy was not saved."
    };
}
