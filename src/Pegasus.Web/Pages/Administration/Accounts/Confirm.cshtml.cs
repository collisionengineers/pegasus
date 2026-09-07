using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pegasus.Core.Identity;

namespace Pegasus.Web.Pages.Administration.Accounts;

[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class ConfirmModel(
    IGetStaffAccount getStaffAccount,
    IGetStaffHeldCaseEditLeases getStaffHeldCaseEditLeases) : AdministrationPageModel
{
    public string HandlerName { get; private set; } = string.Empty;

    public string Heading { get; private set; } = string.Empty;

    public string Operation { get; private set; } = string.Empty;

    public Guid StaffId { get; private set; }
    public StaffAccountSummary? Account { get; private set; }
    public IReadOnlyList<StaffHeldCaseEditLease> Leases { get; private set; } = [];
    public Guid CaseId { get; private set; }
    public long LeaseGeneration { get; private set; }

    public string OperationKey { get; } = NewOperationKey();

    public async Task<IActionResult> OnGetAsync(
        string operation, Guid staffId, Guid? caseId, long? leaseGeneration,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor) || staffId == Guid.Empty) return NotFound();

        Operation = operation switch
        {
            "Disable" or "Enable" or "ForceLogout" or "ResetPassword" or "Delete" => operation,
            "ClearLease" when caseId is not null && leaseGeneration is not null => operation,
            _ => string.Empty
        };
        if (Operation.Length == 0) return NotFound();

        Account = (await getStaffAccount.ExecuteAsync(new(actor, staffId), cancellationToken))?.Account;
        if (Account is null) return NotFound();
        StaffId = staffId;
        Leases = (await getStaffHeldCaseEditLeases.ExecuteAsync(new(actor, staffId), cancellationToken)).Leases;
        if (Operation == "ClearLease")
        {
            var lease = Leases.SingleOrDefault(item => item.CaseId == caseId && item.LeaseGeneration == leaseGeneration);
            if (lease is null) return NotFound();
            CaseId = lease.CaseId;
            LeaseGeneration = lease.LeaseGeneration;
        }
        HandlerName = Operation;
        Heading = Operation switch
        {
            "Disable" => "Disable account",
            "Enable" => "Enable account",
            "ForceLogout" => "Force logout",
            "ResetPassword" => "Reset password",
            "Delete" => "Delete account",
            _ => "Clear case edit hold"
        };
        return Page();
    }
}
