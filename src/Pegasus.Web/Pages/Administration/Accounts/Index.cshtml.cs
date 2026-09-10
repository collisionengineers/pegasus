using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Administration.Accounts;

/// <summary>The compact staff-account administration area.</summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(
    IListStaffAccounts listStaffAccounts,
    ICreateStaffAccount createStaffAccount,
    IUpdateStaffAccountSettings updateStaffAccountSettings,
    IDisableStaffAccount disableStaffAccount,
    IEnableStaffAccount enableStaffAccount,
    IForceStaffLogout forceStaffLogout,
    IResetStaffPassword resetStaffPassword,
    IDeleteStaffAccount deleteStaffAccount,
    IClearCaseEditLease clearCaseEditLease,
    IEditScopeLeases editScopes) : AdministrationPageModel
{
    public IReadOnlyList<StaffAccountRow> Rows { get; private set; } = [];
    public bool HasMoreAccounts { get; private set; }
    public bool AutomationComposed { get; private set; }
    public string NewUserName { get; private set; } = string.Empty;
    public string CreateOperationKey { get; private set; } = NewOperationKey();
    public bool CreatePostSubmitted { get; private set; }
    public Guid SettingsPostStaffId { get; private set; }
    public StaffRole SettingsPostRole { get; private set; } = StaffRole.User;
    public bool SettingsPostIsSignOffEngineer { get; private set; }
    public string SettingsPostPrintedName { get; private set; } = string.Empty;
    public string SettingsPostQualifications { get; private set; } = string.Empty;
    public bool SettingsPostIsDefault { get; private set; }
    public string SettingsPostReason { get; private set; } = string.Empty;
    public long SettingsPostVersion { get; private set; }
    public string SettingsLeaseToken { get; private set; } = string.Empty;
    public string? ResetTemporaryPassword { get; private set; }

    /// <summary>
    /// The account this operator is already editing in another window, offered
    /// with the take-over that ends the other window's claim.
    /// </summary>
    public Guid TakeOverStaffId { get; private set; }

    /// <summary>The record as the operator reading an ownership sentence names it.</summary>
    private const string RecordName = "account";

    /// <summary>
    /// Another colleague's claim. This area never resolves the holder's name,
    /// so the shared wording is used with an unnamed holder rather than a
    /// second sentence of its own.
    /// </summary>
    private static readonly string HeldByAnother =
        EditModeDisplay.HeldBy(RecordName, CaseEditAuthorityHolder.Unnamed, isSelf: false);

    public async Task<IActionResult> OnGetAsync(
        Guid? editStaffId,
        long? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        await LoadAsync(actor, cancellationToken);
        if (editStaffId is not { } staffId) return Page();

        var account = Rows.SingleOrDefault(item => item.Account.Id == staffId)?.Account;
        if (account is null) return NotFound();
        if (expectedVersion != account.Version)
        {
            ModelState.AddModelError(string.Empty, "The account changed. Reload and try again.");
            return Page();
        }

        try
        {
            // A plain GET never takes over another window's claim; that is a
            // mutating act and stays behind the posted Edit handler below.
            var lease = await editScopes.ClaimAsync(
                new(EditScopeKind.StaffAccount, staffId, account.Version, actor, NewOperationKey())
                {
                    TakeOver = false
                },
                cancellationToken);
            SettingsPostStaffId = staffId;
            SettingsPostRole = account.Role;
            SettingsPostIsSignOffEngineer = account.SignOff.IsSignOffEngineer;
            SettingsPostPrintedName = account.SignOff.PrintedName ?? string.Empty;
            SettingsPostQualifications = account.SignOff.Qualifications ?? string.Empty;
            SettingsPostIsDefault = account.SignOff.IsDefault;
            SettingsPostVersion = account.Version;
            SettingsLeaseToken = lease.Token;
        }
        catch (EditScopeHeldElsewhereException)
        {
            TakeOverStaffId = staffId;
            ModelState.AddModelError(string.Empty, EditModeDisplay.HeldElsewhere(RecordName));
        }
        catch (EditScopeConflictException)
        {
            ModelState.AddModelError(string.Empty, HeldByAnother);
        }
        catch (EditScopeVersionConflictException)
        {
            ModelState.AddModelError(string.Empty, "The account changed. Reload and try again.");
        }

        return Page();
    }

    /// <summary>
    /// The take-over claim: the same edit-scope acquisition the Settings link
    /// performs, but posted rather than followed as a plain link, because
    /// taking over ends another window's claim and a GET must never do that.
    /// </summary>
    public async Task<IActionResult> OnPostEditAsync(
        Guid staffId,
        long expectedVersion,
        string? operationKey,
        bool takeOver,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (staffId == Guid.Empty || !IsOperationKeyValid(operationKey)) return BadRequest();
        await LoadAsync(actor, cancellationToken);

        var account = Rows.SingleOrDefault(item => item.Account.Id == staffId)?.Account;
        if (account is null) return NotFound();
        if (expectedVersion != account.Version)
        {
            ModelState.AddModelError(string.Empty, "The account changed. Reload and try again.");
            return Page();
        }

        try
        {
            var lease = await editScopes.ClaimAsync(
                new(EditScopeKind.StaffAccount, staffId, account.Version, actor, operationKey!)
                {
                    TakeOver = takeOver
                },
                cancellationToken);
            SettingsPostStaffId = staffId;
            SettingsPostRole = account.Role;
            SettingsPostIsSignOffEngineer = account.SignOff.IsSignOffEngineer;
            SettingsPostPrintedName = account.SignOff.PrintedName ?? string.Empty;
            SettingsPostQualifications = account.SignOff.Qualifications ?? string.Empty;
            SettingsPostIsDefault = account.SignOff.IsDefault;
            SettingsPostVersion = account.Version;
            SettingsLeaseToken = lease.Token;
        }
        catch (EditScopeHeldElsewhereException)
        {
            TakeOverStaffId = staffId;
            ModelState.AddModelError(string.Empty, EditModeDisplay.HeldElsewhere(RecordName));
        }
        catch (EditScopeConflictException)
        {
            ModelState.AddModelError(string.Empty, HeldByAnother);
        }
        catch (EditScopeVersionConflictException)
        {
            ModelState.AddModelError(string.Empty, "The account changed. Reload and try again.");
        }

        return Page();
    }

    /// <summary>
    /// The release a leaving page beacons. It is not an operator action: it
    /// answers 204 whether or not a scope was still there to release, so a
    /// duplicate beacon and a beacon that lost a race with Cancel are both
    /// ordinary outcomes. Antiforgery is validated as it is for every post.
    /// </summary>
    public async Task<IActionResult> OnPostReleaseScopeBeaconAsync(
        Guid staffId,
        string? editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (staffId == Guid.Empty || string.IsNullOrWhiteSpace(editLeaseToken)) return new NoContentResult();
        try
        {
            await editScopes.ReleaseAsync(
                new(EditScopeKind.StaffAccount, staffId, actor, NewOperationKey(), editLeaseToken),
                cancellationToken);
        }
        catch (Exception exception)
            when (exception is EditScopeExpiredException or EditScopeConflictException)
        {
            // The scope has already gone or has already been re-claimed by a
            // newer window of this operator's own session.
        }
        return new NoContentResult();
    }

    public Task<IActionResult> OnPostCreateAsync(
        string? userName,
        string? temporaryPassword,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        CreatePostSubmitted = true;
        NewUserName = userName?.Trim() ?? string.Empty;
        return RunAsync(async actor =>
        {
            if (!ValidateOperationKey(operationKey)
                | !Require(NewUserName, "Enter a username.")
                | !Require(temporaryPassword, "Enter a temporary password.")) return null;
            await createStaffAccount.ExecuteAsync(
                new(actor, NewUserName, temporaryPassword!, operationKey!), cancellationToken);
            return "The staff account was created and must change its password at first sign-in.";
        }, cancellationToken);
    }

    public Task<IActionResult> OnPostSettingsAsync(
        Guid staffId,
        string? role,
        bool isSignOffEngineer,
        string? printedName,
        string? qualifications,
        bool isDefaultSignOffEngineer,
        IFormFile? signature,
        long expectedVersion,
        string? editLeaseToken,
        string? reason,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        SettingsPostStaffId = staffId;
        SettingsPostIsSignOffEngineer = isSignOffEngineer;
        SettingsPostPrintedName = printedName ?? string.Empty;
        SettingsPostQualifications = qualifications ?? string.Empty;
        SettingsPostIsDefault = isDefaultSignOffEngineer;
        SettingsPostReason = reason ?? string.Empty;
        SettingsPostVersion = expectedVersion;
        SettingsLeaseToken = editLeaseToken ?? string.Empty;
        return RunAsync(async actor =>
        {
            if (!RequireStaffId(staffId) || !ValidateReasoned(operationKey, reason))
            {
                return null;
            }
            if (!Enum.TryParse<StaffRole>(role, ignoreCase: false, out var selectedRole)
                || !Enum.IsDefined(selectedRole))
            {
                ModelState.AddModelError(string.Empty, "Select a supported staff role.");
                return null;
            }
            SettingsPostRole = selectedRole;
            if (selectedRole == StaffRole.User)
            {
                isSignOffEngineer = false;
                isDefaultSignOffEngineer = false;
            }
            if (isSignOffEngineer && !Require(printedName, OperatorLabels.StaffAccounts.PrintedNameRequired))
            {
                return null;
            }

            var signatureBytes = await ReadSignatureAsync(signature, cancellationToken);
            if (!ModelState.IsValid) return null;
            await updateStaffAccountSettings.ExecuteAsync(
                new(
                    actor, staffId, selectedRole, isSignOffEngineer, printedName, qualifications,
                    signatureBytes, isDefaultSignOffEngineer, operationKey!, expectedVersion,
                    editLeaseToken ?? string.Empty, reason),
                cancellationToken);
            return "Account settings saved.";
        }, cancellationToken);
    }

    public async Task<IActionResult> OnPostCancelSettingsAsync(
        Guid staffId,
        string? editLeaseToken,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (staffId == Guid.Empty || !IsOperationKeyValid(operationKey) || string.IsNullOrWhiteSpace(editLeaseToken))
        {
            return RedirectToPage();
        }
        try
        {
            await editScopes.ReleaseAsync(
                new(EditScopeKind.StaffAccount, staffId, actor, operationKey!, editLeaseToken),
                cancellationToken);
        }
        catch (EditScopeExpiredException)
        {
            // Cancel remains a no-op when the scope has already expired.
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostHeartbeatSettingsAsync(
        Guid staffId,
        string? editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (staffId == Guid.Empty || string.IsNullOrWhiteSpace(editLeaseToken))
        {
            return BadRequest();
        }

        try
        {
            var lease = await editScopes.HeartbeatAsync(
                new(EditScopeKind.StaffAccount, staffId, actor, editLeaseToken),
                cancellationToken);
            return new JsonResult(new { expiresAtUtc = lease.ExpiresAtUtc });
        }
        catch (EditScopeConflictException)
        {
            return new ConflictObjectResult("Editing this staff account has ended. Reload it before making further changes.");
        }
        catch (EditScopeExpiredException)
        {
            return new ConflictObjectResult("Editing this staff account has ended. Reload it before making further changes.");
        }
    }

    public Task<IActionResult> OnPostDisableAsync(Guid staffId, long expectedVersion, string? editLeaseToken, string? reason, string? operationKey,
        CancellationToken cancellationToken) => RunAdministrativeActionAsync(staffId, reason, operationKey,
        (actor, validReason, validKey) => disableStaffAccount.ExecuteAsync(new(actor, staffId, validReason, validKey, expectedVersion, editLeaseToken ?? string.Empty), cancellationToken),
        "The account was disabled. Existing browser sessions were revoked.", cancellationToken);

    public Task<IActionResult> OnPostEnableAsync(Guid staffId, long expectedVersion, string? editLeaseToken, string? reason, string? operationKey,
        CancellationToken cancellationToken) => RunAdministrativeActionAsync(staffId, reason, operationKey,
        (actor, validReason, validKey) => enableStaffAccount.ExecuteAsync(new(actor, staffId, validReason, validKey, expectedVersion, editLeaseToken ?? string.Empty), cancellationToken),
        "The account was enabled.", cancellationToken);

    public Task<IActionResult> OnPostForceLogoutAsync(Guid staffId, long expectedVersion, string? editLeaseToken, string? reason, string? operationKey,
        CancellationToken cancellationToken) => RunAdministrativeActionAsync(staffId, reason, operationKey,
        (actor, validReason, validKey) => forceStaffLogout.ExecuteAsync(new(actor, staffId, validReason, validKey, expectedVersion, editLeaseToken ?? string.Empty), cancellationToken),
        "Existing browser sessions were revoked.", cancellationToken);

    public async Task<IActionResult> OnPostResetPasswordAsync(Guid staffId, long expectedVersion, string? editLeaseToken, string? reason, string? operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!ValidateReasoned(operationKey, reason) | !RequireStaffId(staffId))
        {
            await LoadAsync(actor, cancellationToken);
            return Page();
        }
        try
        {
            ResetTemporaryPassword = (await resetStaffPassword.ExecuteAsync(
                new(actor, staffId, reason!, operationKey!, expectedVersion, editLeaseToken ?? string.Empty), cancellationToken)).TemporaryPassword;
            Response.Headers.CacheControl = "no-store, no-cache";
            Response.Headers.Pragma = "no-cache";
        }
        catch (StaffAccountAdministrationException exception) { ModelState.AddModelError(string.Empty, MutationErrorMessage(exception.Error)); }
        catch (EditScopeConflictException) { ModelState.AddModelError(string.Empty, HeldByAnother); }
        catch (EditScopeExpiredException) { ModelState.AddModelError(string.Empty, "Your account edit session expired. Reopen account settings and try again."); }
        catch (EditScopeVersionConflictException) { ModelState.AddModelError(string.Empty, "The account changed. Reload and try again."); }
        catch (ArgumentException) { ModelState.AddModelError(string.Empty, "The change was not accepted."); }
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    public Task<IActionResult> OnPostDeleteAsync(Guid staffId, long expectedVersion, string? editLeaseToken, string? reason, string? operationKey,
        CancellationToken cancellationToken) => RunAdministrativeActionAsync(staffId, reason, operationKey,
        (actor, validReason, validKey) => deleteStaffAccount.ExecuteAsync(new(actor, staffId, validReason, validKey, expectedVersion, editLeaseToken ?? string.Empty), cancellationToken),
        "The account was deleted.", cancellationToken);

    public Task<IActionResult> OnPostClearLeaseAsync(Guid staffId, Guid caseId, long expectedLeaseGeneration,
        string? reason, string? operationKey, CancellationToken cancellationToken) => RunAdministrativeActionAsync(
        staffId, reason, operationKey,
        (actor, validReason, validKey) => clearCaseEditLease.ExecuteAsync(
            new(caseId, staffId, expectedLeaseGeneration, actor, validKey, validReason), cancellationToken),
        "The case edit hold was cleared.", cancellationToken,
        requireCaseId: caseId != Guid.Empty && expectedLeaseGeneration >= 0);

    private async Task<byte[]?> ReadSignatureAsync(IFormFile? signature, CancellationToken cancellationToken)
    {
        if (signature is null) return null;
        if (!string.Equals(signature.ContentType, SignOffSignaturePolicy.MediaType, StringComparison.OrdinalIgnoreCase)
            || signature.Length <= 0 || signature.Length > SignOffSignaturePolicy.MaximumBytes)
        {
            ModelState.AddModelError(string.Empty, OperatorLabels.StaffAccounts.SignatureInvalid);
            return null;
        }
        await using var content = new MemoryStream((int)signature.Length);
        await signature.CopyToAsync(content, cancellationToken);
        return content.ToArray();
    }

    private Task<IActionResult> RunAdministrativeActionAsync(Guid staffId, string? reason, string? operationKey,
        Func<ActionActor, string, string, Task> action, string confirmation, CancellationToken cancellationToken,
        bool requireCaseId = true) => RunAsync(async actor =>
    {
        if (!ValidateReasoned(operationKey, reason) | !RequireStaffId(staffId) || !requireCaseId) return null;
        await action(actor, reason!, operationKey!);
        return confirmation;
    }, cancellationToken);

    private async Task<IActionResult> RunAsync(Func<ActionActor, Task<string?>> operation, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        string? confirmation = null;
        try { confirmation = await operation(actor); }
        catch (CaseEditLeaseConflictException) { ModelState.AddModelError(string.Empty, "The case edit hold changed. Reload the account before trying again."); }
        catch (EditScopeConflictException) { ModelState.AddModelError(string.Empty, HeldByAnother); }
        catch (EditScopeExpiredException) { ModelState.AddModelError(string.Empty, "Your account edit session expired. Reopen account settings and try again."); }
        catch (EditScopeVersionConflictException) { ModelState.AddModelError(string.Empty, "The account changed. Reload and try again."); }
        catch (StaffAccountAdministrationException exception) { ModelState.AddModelError(string.Empty, MutationErrorMessage(exception.Error)); }
        catch (ArgumentException) { ModelState.AddModelError(string.Empty, "The change was not accepted."); }
        if (confirmation is not null)
        {
            TempData["Confirmation"] = confirmation;
            return RedirectToPage();
        }
        CreateOperationKey = NewOperationKey();
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    private bool ValidateReasoned(string? operationKey, string? reason)
    {
        var valid = ValidateOperationKey(operationKey);
        if (!Require(reason, "Enter a reason.")) valid = false;
        else if (reason!.Trim().Length > StaffAccountAdministrationPolicy.MaximumReasonLength)
        {
            ModelState.AddModelError(string.Empty, $"A reason is at most {StaffAccountAdministrationPolicy.MaximumReasonLength} characters.");
            valid = false;
        }
        return valid;
    }

    private bool ValidateOperationKey(string? operationKey)
    {
        if (IsOperationKeyValid(operationKey)) return true;
        ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        return false;
    }

    private bool RequireStaffId(Guid staffId)
    {
        if (staffId != Guid.Empty) return true;
        ModelState.AddModelError(string.Empty, "The form has expired. Retry the operation.");
        return false;
    }

    private bool Require(string? value, string message)
    {
        if (!string.IsNullOrWhiteSpace(value)) return true;
        ModelState.AddModelError(string.Empty, message);
        return false;
    }

    private static string MutationErrorMessage(StaffAccountAdministrationError error) => error switch
    {
        StaffAccountAdministrationError.StaffAccountNotFound => "The staff account no longer exists.",
        StaffAccountAdministrationError.DuplicateUserName => "That username is already assigned.",
        StaffAccountAdministrationError.LastAdministrator => "The change was denied because at least one enabled Administrator must remain.",
        StaffAccountAdministrationError.SelfAction => "An account cannot act on itself.",
        StaffAccountAdministrationError.OperationConflict => "The form was already used for a different operation. Retry from the current page.",
        StaffAccountAdministrationError.SignOffEngineerRequiresEngineerRole => OperatorLabels.StaffAccounts.EngineerRoleRequired,
        StaffAccountAdministrationError.SignOffPrintedNameRequired => OperatorLabels.StaffAccounts.PrintedNameRequired,
        StaffAccountAdministrationError.IneligibleSignOffEngineer => OperatorLabels.StaffAccounts.DefaultRequiresEligible,
        _ => "The change was not accepted."
    };

    private async Task LoadAsync(ActionActor actor, CancellationToken cancellationToken)
    {
        AutomationComposed = HttpContext.RequestServices.GetService<AutomationClientRegistry>() is not null;
        var accounts = await listStaffAccounts.ExecuteAsync(new(actor, PageSize: ListStaffAccounts.MaximumPageSize), cancellationToken);
        HasMoreAccounts = accounts.HasMoreAccounts;
        var currentOperatorId = Guid.TryParse(actor.SubjectId, out var id) ? id : (Guid?)null;
        Rows = accounts.Accounts.Select(account => new StaffAccountRow(account, account.Id == currentOperatorId)).ToArray();
    }
}

public sealed record StaffAccountRow(StaffAccountSummary Account, bool IsCurrentOperator);
