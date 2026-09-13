using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration;

/// <summary>
/// The former Action logs address. Action logs are the first tab of
/// Administration › Logs (13 September); the old route redirects there with its
/// filters intact.
/// </summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ActionLogsModel : AdministrationPageModel
{
    public IActionResult OnGet() =>
        RedirectPermanent("/Administration/Logs" + Request.QueryString.Value);
}
