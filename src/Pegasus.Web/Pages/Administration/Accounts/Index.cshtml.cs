using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
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
    IPerUserExternalCredentialAdministration externalCredentials) : AdministrationPageModel
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
    public long SettingsPostVersion { get; private set; }
    public string? ResetTemporaryPassword { get; private set; }

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

        SettingsPostStaffId = staffId;
        SettingsPostRole = account.Role;
        SettingsPostIsSignOffEngineer = account.SignOff.IsSignOffEngineer;
        SettingsPostPrintedName = account.SignOff.PrintedName ?? string.Empty;
        SettingsPostQualifications = account.SignOff.Qualifications ?? string.Empty;
        SettingsPostIsDefault = account.SignOff.IsDefault;
        SettingsPostVersion = account.Version;
        return Page();
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
        string? reason,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        SettingsPostStaffId = staffId;
        SettingsPostIsSignOffEngineer = isSignOffEngineer;
        SettingsPostPrintedName = printedName ?? string.Empty;
        SettingsPostQualifications = qualifications ?? string.Empty;
        SettingsPostIsDefault = isDefaultSignOffEngineer;
        SettingsPostVersion = expectedVersion;
        return RunAsync(async actor =>
        {
            if (!RequireStaffId(staffId) || !ValidateOperationKey(operationKey))
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
                    reason),
                cancellationToken);
            return "Account settings saved.";
        }, cancellationToken);
    }

    public Task<IActionResult> OnPostDisableAsync(Guid staffId, long expectedVersion, string? reason, string? operationKey,
        CancellationToken cancellationToken) => RunAdministrativeActionAsync(staffId, reason, operationKey,
        (actor, validReason, validKey) => disableStaffAccount.ExecuteAsync(new(actor, staffId, validReason, validKey, expectedVersion), cancellationToken),
        "The account was disabled. Existing browser sessions were revoked.", cancellationToken);

    public Task<IActionResult> OnPostEnableAsync(Guid staffId, long expectedVersion, string? reason, string? operationKey,
        CancellationToken cancellationToken) => RunAdministrativeActionAsync(staffId, reason, operationKey,
        (actor, validReason, validKey) => enableStaffAccount.ExecuteAsync(new(actor, staffId, validReason, validKey, expectedVersion), cancellationToken),
        "The account was enabled.", cancellationToken);

    public Task<IActionResult> OnPostForceLogoutAsync(Guid staffId, long expectedVersion, string? reason, string? operationKey,
        CancellationToken cancellationToken) => RunAdministrativeActionAsync(staffId, reason, operationKey,
        (actor, validReason, validKey) => forceStaffLogout.ExecuteAsync(new(actor, staffId, validReason, validKey, expectedVersion), cancellationToken),
        "Existing browser sessions were revoked.", cancellationToken);

    public async Task<IActionResult> OnPostResetPasswordAsync(Guid staffId, long expectedVersion, string? reason, string? operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (!ValidateOperationKey(operationKey) | !RequireStaffId(staffId))
        {
            await LoadAsync(actor, cancellationToken);
            return Page();
        }
        try
        {
            ResetTemporaryPassword = (await resetStaffPassword.ExecuteAsync(
                new(actor, staffId, reason, operationKey!, expectedVersion), cancellationToken)).TemporaryPassword;
            Response.Headers.CacheControl = "no-store, no-cache";
            Response.Headers.Pragma = "no-cache";
        }
        catch (StaffAccountAdministrationException exception) { ModelState.AddModelError(string.Empty, MutationErrorMessage(exception.Error)); }
        catch (ArgumentException) { ModelState.AddModelError(string.Empty, "The change was not accepted."); }
        await LoadAsync(actor, cancellationToken);
        return Page();
    }

    public Task<IActionResult> OnPostDeleteAsync(Guid staffId, long expectedVersion, string? reason, string? operationKey,
        CancellationToken cancellationToken) => RunAdministrativeActionAsync(staffId, reason, operationKey,
        (actor, validReason, validKey) => deleteStaffAccount.ExecuteAsync(new(actor, staffId, validReason, validKey, expectedVersion), cancellationToken),
        "The account was deleted.", cancellationToken);

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
        Func<ActionActor, string?, string, Task> action, string confirmation, CancellationToken cancellationToken) => RunAsync(async actor =>
    {
        if (!ValidateOperationKey(operationKey) | !RequireStaffId(staffId)) return null;
        await action(actor, reason, operationKey!);
        return confirmation;
    }, cancellationToken);

    private async Task<IActionResult> RunAsync(Func<ActionActor, Task<string?>> operation, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        string? confirmation = null;
        try { confirmation = await operation(actor); }
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
        StaffAccountAdministrationError.StaleVersion => "The staff account changed. Reload and try again.",
        StaffAccountAdministrationError.DuplicateUserName => "That username is already assigned.",
        StaffAccountAdministrationError.LastAdministrator => "The change was denied because at least one enabled Administrator must remain.",
        StaffAccountAdministrationError.AssignedToOpenCases => "The account is the Engineer or Sign-off Engineer on open cases. Reassign those cases first.",
        StaffAccountAdministrationError.SelfAction => "An account cannot act on itself.",
        StaffAccountAdministrationError.OperationConflict => "The form was already used for a different operation. Retry from the current page.",
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
        var glassByAccount = await externalCredentials.GetManyAsync(
            actor,
            accounts.Accounts.Select(account => account.Id).ToArray(),
            ExternalCredentialProvider.GlassRepairEstimate,
            cancellationToken);
        var rows = new List<StaffAccountRow>(accounts.Accounts.Count);
        foreach (var account in accounts.Accounts)
        {
            // Decision M (v26): the Glass's column puts each account's credential on
            // the list; the credential itself is still managed from the account.
            rows.Add(new StaffAccountRow(
                account,
                account.Id == currentOperatorId,
                glassByAccount.GetValueOrDefault(account.Id)));
        }
        Rows = rows;
    }
}

public sealed record StaffAccountRow(
    StaffAccountSummary Account,
    bool IsCurrentOperator,
    PerUserExternalCredentialStatus? Glass = null)
{
    /// <summary>The Glass's column: Not set, Disabled, or the login username.</summary>
    public string GlassLabel => Glass switch
    {
        null or { Configured: false } => "Not set",
        { Enabled: false } => "Disabled",
        { Username: { Length: > 0 } username } => username,
        _ => "Set"
    };
}
