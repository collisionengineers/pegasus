using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Accounts;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ConfirmModel : AdministrationPageModel
{
    public string HandlerName { get; private set; } = string.Empty;

    public string Heading { get; private set; } = string.Empty;

    public string Operation { get; private set; } = string.Empty;

    public Guid StaffId { get; private set; }

    public string OperationKey { get; } = NewOperationKey();

    public bool IsDisable => string.Equals(Operation, "Disable", StringComparison.Ordinal);

    public IActionResult OnGet(string operation, Guid staffId)
    {
        if (staffId == Guid.Empty)
        {
            return NotFound();
        }

        Operation = operation switch
        {
            "Disable" => "Disable",
            "Review" => "Review",
            _ => string.Empty
        };
        if (Operation.Length == 0)
        {
            return NotFound();
        }

        StaffId = staffId;
        HandlerName = Operation;
        Heading = IsDisable ? "Disable account" : "Review account";
        return Page();
    }
}
