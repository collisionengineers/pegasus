using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Administration.Accounts;

/// <summary>
/// The one "Staff accounts &amp; roles" administration area (EPIC-011 §1.12).
/// </summary>
/// <remarks>
/// It carries account creation, role assignment and the selected-account
/// actions. Every operation still runs through the Core use case that owns it,
/// so the distinct rights those use cases require —
/// <see cref="StaffAccessRight.ManageStaffAccounts"/>,
/// <see cref="StaffAccessRight.AssignStaffRoles"/> — are unchanged here;
/// nothing is re-implemented in the page.
///
/// Four forms post to this page, so the submitted values arrive as handler
/// parameters rather than bound properties: a <c>[Required]</c> property
/// belonging to one form would invalidate every other form's post.
/// </remarks>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(
    IListStaffAccounts listStaffAccounts,
    ICreateStaffAccount createStaffAccount,
    IAssignStaffRoles assignStaffRoles,
    IDisableStaffAccount disableStaffAccount,
    IEnableStaffAccount enableStaffAccount,
    IForceStaffLogout forceStaffLogout,
    IResetStaffPassword resetStaffPassword,
    IDeleteStaffAccount deleteStaffAccount,
    IClearCaseEditLease clearCaseEditLease,
    IUpdateStaffAccountSignOff updateStaffAccountSignOff) : AdministrationPageModel
{
    /// <summary>One row per staff account, in the Core query's order.</summary>
    public IReadOnlyList<StaffAccountRow> Rows { get; private set; } = [];

    /// <summary>Whether the bounded account query has another page.</summary>
    public bool HasMoreAccounts { get; private set; }

    /// <summary>Whether the Automation ingress exists in this deployment.</summary>
    public bool AutomationComposed { get; private set; }

    /// <summary>The username typed into Create staff account, kept over a failed post.</summary>
    public string NewUserName { get; private set; } = string.Empty;

    /// <summary>The reason typed into Create staff account, kept over a failed post.</summary>
    public string NewReason { get; private set; } = string.Empty;

    /// <summary>The operation key the Create staff account form carries.</summary>
    public string CreateOperationKey { get; private set; } = NewOperationKey();

    /// <summary>The account targeted by the most recent role post.</summary>
    public Guid RolePostStaffId { get; private set; }

    /// <summary>The role-change reason kept over a rejected post.</summary>
    public string RoleReason { get; private set; } = string.Empty;

    /// <summary>The role names submitted by the most recent role post.</summary>
    public IReadOnlyList<string> RolePostSelectedRoles { get; private set; } = [];

    public string? ResetTemporaryPassword { get; private set; }

    public Guid SignOffPostStaffId { get; private set; }

    public bool SignOffPostIsEnabled { get; private set; }

    public string SignOffPostPrintedName { get; private set; } = string.Empty;

    public string SignOffPostQualifications { get; private set; } = string.Empty;

    public bool SignOffPostIsDefault { get; private set; }

    public string SignOffPostReason { get; private set; } = string.Empty;

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
        NewReason = reason ?? string.Empty;
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
                NewReason = string.Empty;
                return "The staff account was created and must change its password at first sign-in.";
            },
            cancellationToken);
    }

    public Task<IActionResult> OnPostRolesAsync(
        Guid staffId,
        string[]? selectedRoles,
        string? reason,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        RolePostStaffId = staffId;
        RoleReason = reason ?? string.Empty;
        RolePostSelectedRoles = selectedRoles ?? [];
        return RunAsync(
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
    }

    public Task<IActionResult> OnPostDisableAsync(
        Guid staffId,
        string? reason,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        return RunAsync(
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
    }

    public Task<IActionResult> OnPostEnableAsync(
        Guid staffId,
        string? reason,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        return RunAsync(
            async actor =>
            {
                if (!Validate(operationKey, reason) | !RequireStaffId(staffId))
                {
                    return null;
                }

                await enableStaffAccount.ExecuteAsync(
                    new(actor, staffId, reason!, operationKey!),
                    cancellationToken);
                return "The account was enabled.";
            },
            cancellationToken);
    }

    public Task<IActionResult> OnPostForceLogoutAsync(Guid staffId, string? reason, string? operationKey,
        CancellationToken cancellationToken) => RunAdministrativeActionAsync(
            staffId, reason, operationKey,
            (actor, validReason, validKey) => forceStaffLogout.ExecuteAsync(
                new(actor, staffId, validReason, validKey), cancellationToken),
            "Existing browser sessions were revoked.", cancellationToken);

    public async Task<IActionResult> OnPostResetPasswordAsync(Guid staffId, string? reason, string? operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!Validate(operationKey, reason) | !RequireStaffId(staffId))
        {
            await LoadAsync(actor, cancellationToken);
            return Page();
        }

        try
        {
            ResetTemporaryPassword = (await resetStaffPassword.ExecuteAsync(
                new(actor, staffId, reason!, operationKey!), cancellationToken)).TemporaryPassword;
            Response.Headers.CacheControl = "no-store, no-cache";
            Response.Headers.Pragma = "no-cache";
        }
        catch (StaffAccountAdministrationException exception)
        {
            ModelState.AddModelError(string.Empty, MutationErrorMessage(exception.Error));
        }
        catch (ArgumentException)
        {
            ModelState.AddModelError(string.Empty, "The change was not accepted.");
        }

        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    public Task<IActionResult> OnPostDeleteAsync(Guid staffId, string? reason, string? operationKey,
        CancellationToken cancellationToken) => RunAdministrativeActionAsync(
            staffId, reason, operationKey,
            (actor, validReason, validKey) => deleteStaffAccount.ExecuteAsync(
                new(actor, staffId, validReason, validKey), cancellationToken),
            "The account was deleted.", cancellationToken);

    public Task<IActionResult> OnPostClearLeaseAsync(Guid staffId, Guid caseId, long expectedLeaseGeneration,
        string? reason, string? operationKey, CancellationToken cancellationToken) => RunAdministrativeActionAsync(
            staffId, reason, operationKey,
            (actor, validReason, validKey) => clearCaseEditLease.ExecuteAsync(
                new(caseId, staffId, expectedLeaseGeneration, actor, validKey, validReason), cancellationToken),
            "The case edit hold was cleared.", cancellationToken,
            requireCaseId: caseId != Guid.Empty && expectedLeaseGeneration >= 0);

    public Task<IActionResult> OnPostSignOffAsync(
        Guid staffId,
        bool isSignOffEngineer,
        string? printedName,
        string? qualifications,
        bool isDefault,
        IFormFile? signature,
        string? reason,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        SignOffPostStaffId = staffId;
        SignOffPostIsEnabled = isSignOffEngineer;
        SignOffPostPrintedName = printedName ?? string.Empty;
        SignOffPostQualifications = qualifications ?? string.Empty;
        SignOffPostIsDefault = isDefault;
        SignOffPostReason = reason ?? string.Empty;
        return RunAsync(
            async actor =>
            {
                var valid = Validate(operationKey, reason) & RequireStaffId(staffId);
                if (isSignOffEngineer
                    && !Require(
                        printedName,
                        OperatorLabels.StaffAccounts.PrintedNameRequired))
                {
                    valid = false;
                }

                byte[]? signatureBytes = null;
                if (signature is not null)
                {
                    if (!string.Equals(
                            signature.ContentType,
                            SignOffSignaturePolicy.MediaType,
                            StringComparison.OrdinalIgnoreCase)
                        || signature.Length <= 0
                        || signature.Length > SignOffSignaturePolicy.MaximumBytes)
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            OperatorLabels.StaffAccounts.SignatureInvalid);
                        valid = false;
                    }
                    else
                    {
                        await using var content = new MemoryStream((int)signature.Length);
                        await signature.CopyToAsync(content, cancellationToken);
                        signatureBytes = content.ToArray();
                    }
                }

                if (!valid)
                {
                    return null;
                }

                await updateStaffAccountSignOff.ExecuteAsync(
                    new(
                        actor,
                        staffId,
                        isSignOffEngineer,
                        printedName,
                        qualifications,
                        signatureBytes,
                        isDefault,
                        reason!,
                        operationKey!),
                    cancellationToken);
                return OperatorLabels.StaffAccounts.SignOffUpdated;
            },
            cancellationToken);
    }

    public bool IsRoleSelected(StaffAccountRow row, StaffRole role)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Account.Id == RolePostStaffId
            ? RolePostSelectedRoles.Contains(role.ToString(), StringComparer.Ordinal)
            : row.Account.Roles.Contains(role);
    }

    private Task<IActionResult> RunAdministrativeActionAsync(
        Guid staffId, string? reason, string? operationKey,
        Func<ActionActor, string, string, Task> action, string confirmation,
        CancellationToken cancellationToken, bool requireCaseId = true) =>
        RunAsync(async actor =>
        {
            if (!Validate(operationKey, reason) | !RequireStaffId(staffId) || !requireCaseId) return null;
            await action(actor, reason!, operationKey!);
            return confirmation;
        }, cancellationToken);

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
        catch (CaseEditLeaseConflictException)
        {
            ModelState.AddModelError(string.Empty, "The case edit hold changed. Reload the account before trying again.");
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
        StaffAccountAdministrationError.SelfAction => "An account cannot act on itself.",
        StaffAccountAdministrationError.OperationConflict =>
            "The form was already used for a different operation. Retry from the current page.",
        StaffAccountAdministrationError.SignOffEngineerRequiresEngineerRole =>
            OperatorLabels.StaffAccounts.EngineerRoleRequired,
        StaffAccountAdministrationError.SignOffPrintedNameRequired =>
            OperatorLabels.StaffAccounts.PrintedNameRequired,
        StaffAccountAdministrationError.IneligibleSignOffEngineer =>
            OperatorLabels.StaffAccounts.DefaultRequiresEligible,
        _ => "The change was not accepted."
    };

    private async Task LoadAsync(
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        AutomationComposed =
            HttpContext.RequestServices.GetService<AutomationClientRegistry>() is not null;

        var accounts = await listStaffAccounts.ExecuteAsync(
            new(actor, PageSize: ListStaffAccounts.MaximumPageSize),
            cancellationToken);
        HasMoreAccounts = accounts.HasMoreAccounts;

        var currentOperatorId = Guid.TryParse(actor.SubjectId, out var actorStaffId)
            ? actorStaffId
            : (Guid?)null;

        Rows = accounts.Accounts
            .Select(account => new StaffAccountRow(account, account.Id == currentOperatorId))
            .ToArray();
    }
}

/// <summary>
/// One accounts-table row and whether it represents the current operator.
/// </summary>
public sealed record StaffAccountRow(StaffAccountSummary Account, bool IsCurrentOperator);
