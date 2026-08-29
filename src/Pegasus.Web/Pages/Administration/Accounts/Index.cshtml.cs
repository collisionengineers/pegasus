using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Web.Mcp;

namespace Pegasus.Web.Pages.Administration.Accounts;

/// <summary>
/// The one "Staff accounts &amp; roles" administration area (EPIC-011 §1.12).
/// </summary>
/// <remarks>
/// It carries what three separate pages used to: the account list and
/// creation (<c>Accounts/Index</c>), role assignment (<c>Roles/Index</c>),
/// account disable (<c>Accounts/Edit</c>) and the access review
/// (<c>Access/Index</c>). Every operation still runs through the Core use
/// case that owns it, so the distinct rights those use cases require —
/// <see cref="StaffAccessRight.ManageStaffAccounts"/>,
/// <see cref="StaffAccessRight.AssignStaffRoles"/> and
/// <see cref="StaffAccessRight.ReviewStaffAccess"/> — are unchanged by the
/// fold; nothing is re-implemented here.
///
/// Four forms post to this page, so the submitted values arrive as handler
/// parameters rather than bound properties: a <c>[Required]</c> property
/// belonging to one form would invalidate every other form's post.
/// </remarks>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(
    IListStaffAccounts listStaffAccounts,
    IGetAccessReview getAccessReview,
    ICreateStaffAccount createStaffAccount,
    IAssignStaffRoles assignStaffRoles,
    IDisableStaffAccount disableStaffAccount,
    IReviewStaffAccess reviewStaffAccess) : AdministrationPageModel
{
    /// <summary>One row per staff account, in the Core query's order.</summary>
    public IReadOnlyList<StaffAccountRow> Rows { get; private set; } = [];

    /// <summary>Whether the Automation ingress exists in this deployment.</summary>
    public bool AutomationComposed { get; private set; }

    /// <summary>The username typed into Create staff account, kept over a failed post.</summary>
    public string NewUserName { get; private set; } = string.Empty;

    /// <summary>The operation key the Create staff account form carries.</summary>
    public string CreateOperationKey { get; private set; } = NewOperationKey();

    public Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
        RunAsync(_ => Task.FromResult<string?>(null), cancellationToken);

    public Task<IActionResult> OnPostCreateAsync(
        string? userName,
        string? temporaryPassword,
        string? reason,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        NewUserName = userName?.Trim() ?? string.Empty;
        return RunAsync(
            async actor =>
            {
                if (!Validate(operationKey, reason)
                    | !Require(NewUserName, "Enter a username.")
                    | !Require(temporaryPassword, "Enter a temporary password."))
                {
                    return null;
                }

                await createStaffAccount.ExecuteAsync(
                    new(actor, NewUserName, temporaryPassword!, reason!, operationKey!),
                    cancellationToken);
                NewUserName = string.Empty;
                return "The staff account was created and must change its password at first sign-in.";
            },
            cancellationToken);
    }

    public Task<IActionResult> OnPostRolesAsync(
        Guid staffId,
        string[]? selectedRoles,
        string? reason,
        string? operationKey,
        CancellationToken cancellationToken) =>
        RunAsync(
            async actor =>
            {
                if (!Validate(operationKey, reason) | !RequireStaffId(staffId))
                {
                    return null;
                }

                var roles = new HashSet<StaffRole>();
                foreach (var roleName in selectedRoles ?? [])
                {
                    if (!Enum.TryParse<StaffRole>(roleName, ignoreCase: false, out var role)
                        || !Enum.IsDefined(role))
                    {
                        ModelState.AddModelError(string.Empty, "Select only supported staff roles.");
                        return null;
                    }

                    roles.Add(role);
                }

                if (roles.Count == 0)
                {
                    ModelState.AddModelError(string.Empty, "Select at least one role.");
                    return null;
                }

                await assignStaffRoles.ExecuteAsync(
                    new(actor, staffId, roles, reason!, operationKey!),
                    cancellationToken);
                return "Roles updated. Existing browser sessions were revoked.";
            },
            cancellationToken);

    public Task<IActionResult> OnPostDisableAsync(
        Guid staffId,
        string? reason,
        string? operationKey,
        CancellationToken cancellationToken) =>
        RunAsync(
            async actor =>
            {
                if (!Validate(operationKey, reason) | !RequireStaffId(staffId))
                {
                    return null;
                }

                await disableStaffAccount.ExecuteAsync(
                    new(actor, staffId, reason!, operationKey!),
                    cancellationToken);
                return "The account was disabled. Existing browser sessions were revoked.";
            },
            cancellationToken);

    public Task<IActionResult> OnPostReviewAsync(
        Guid staffId,
        string? reason,
        string? operationKey,
        CancellationToken cancellationToken) =>
        RunAsync(
            async actor =>
            {
                if (!Validate(operationKey, reason) | !RequireStaffId(staffId))
                {
                    return null;
                }

                await reviewStaffAccess.ExecuteAsync(
                    new(actor, staffId, reason!, operationKey!),
                    cancellationToken);
                return "The access review was recorded.";
            },
            cancellationToken);

    /// <summary>
    /// The one place an operation is authorised, run, translated into an
    /// operator message and followed by a reload — so the four handlers hold
    /// only what actually differs between them.
    /// </summary>
    private async Task<IActionResult> RunAsync(
        Func<ActionActor, Task<string?>> operation,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        string? confirmation = null;
        try
        {
            confirmation = await operation(actor);
        }
        catch (StaffAccountAdministrationException exception)
        {
            ModelState.AddModelError(string.Empty, MutationErrorMessage(exception.Error));
        }
        catch (ArgumentException)
        {
            ModelState.AddModelError(string.Empty, "The change was not accepted.");
        }

        if (confirmation is not null)
        {
            TempData["Confirmation"] = confirmation;
            return RedirectToPage();
        }

        CreateOperationKey = NewOperationKey();
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    private bool Validate(string? operationKey, string? reason)
    {
        var valid = true;
        if (string.IsNullOrWhiteSpace(operationKey) || !IsOperationKeyValid(operationKey))
        {
            ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
            valid = false;
        }

        if (!Require(reason, "Enter a reason."))
        {
            valid = false;
        }
        else if (reason!.Trim().Length > StaffAccountAdministrationPolicy.MaximumReasonLength)
        {
            ModelState.AddModelError(
                string.Empty,
                $"A reason is at most {StaffAccountAdministrationPolicy.MaximumReasonLength} characters.");
            valid = false;
        }

        return valid;
    }

    private bool RequireStaffId(Guid staffId)
    {
        if (staffId != Guid.Empty)
        {
            return true;
        }

        ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        return false;
    }

    private bool Require(string? value, string message)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        ModelState.AddModelError(string.Empty, message);
        return false;
    }

    private static string MutationErrorMessage(StaffAccountAdministrationError error) => error switch
    {
        StaffAccountAdministrationError.StaffAccountNotFound =>
            "The staff account no longer exists.",
        StaffAccountAdministrationError.DuplicateUserName =>
            "That username is already assigned.",
        StaffAccountAdministrationError.LastAdministrator =>
            "The change was denied because at least one enabled Administrator must remain.",
        StaffAccountAdministrationError.OperationConflict =>
            "The form was already used for a different operation. Retry from the current page.",
        _ => "The change was not accepted."
    };

    /// <summary>
    /// Reads the account list and the access review and joins them by staff
    /// id. Both are bounded Core queries over the same accounts; the review
    /// read stays because <c>ReviewIsOutstanding</c> is Core policy and
    /// re-deriving it here would put a business rule outside Core.
    /// </summary>
    private async Task LoadAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        AutomationComposed =
            HttpContext.RequestServices.GetService<AutomationClientRegistry>() is not null;

        var accounts = await listStaffAccounts.ExecuteAsync(
            new(actor, PageSize: ListStaffAccounts.MaximumPageSize),
            cancellationToken);
        var review = await getAccessReview.ExecuteAsync(
            new(actor, MaximumResults: ListStaffAccounts.MaximumPageSize),
            cancellationToken);
        var outstanding = review.Accounts.ToDictionary(
            item => item.StaffId,
            item => item.ReviewIsOutstanding);

        Rows = accounts.Accounts
            .Select(account => new StaffAccountRow(
                account,
                outstanding.TryGetValue(account.Id, out var isOutstanding) && isOutstanding))
            .ToArray();
    }
}

/// <summary>
/// One accounts-table row: the account summary plus Core's outstanding
/// access-review verdict for it.
/// </summary>
public sealed record StaffAccountRow(
    StaffAccountSummary Account,
    bool ReviewIsOutstanding);
