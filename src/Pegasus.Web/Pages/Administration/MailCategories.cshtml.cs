using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class MailCategoriesModel : AdministrationPageModel
{
    public IActionResult OnGet() =>
        RedirectToPagePermanent("/Administration/Mailboxes");
}
