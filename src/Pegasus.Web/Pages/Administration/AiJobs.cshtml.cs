using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration;

/// <summary>
/// The former AI jobs address. The list is on the Automation &amp; AI page
/// (R467), so this route redirects there, keeping the page asked for.
/// </summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class AiJobsModel : AdministrationPageModel, Pegasus.Web.Presentation.IShellFreeGet
{
    public IActionResult OnGet([FromQuery(Name = "pageNumber")] int? pageNumber) =>
        RedirectToPage("/Administration/Automation/Index", new { pageNumber });
}
