using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;
using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Administration.Glass;

/// <summary>
/// One staff account's Glass repair-estimate credential. The page states whether a
/// credential is held and under whose external account name; the secret itself
/// is write-only, so no handler, TempData entry or rendered field ever carries
/// a password back to the browser.
/// </summary>
/// <remarks>
/// The username and password arrive as handler parameters rather than bound
/// properties: a bound password would be redisplayed by the tag helper on a
/// refused post, which is exactly the leak this page must not have. The
/// submitted password is dropped from <see cref="Microsoft.AspNetCore.Mvc.RazorPages.PageModel.ModelState"/>
/// as soon as it has been read for the same reason.
///
/// An existing staff-account scope owns credential changes. The credential
/// keeps its own version as well, so both the account and credential cannot
/// change beneath an edit.
/// </remarks>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(
    IGetStaffAccount getStaffAccount,
    IPerUserExternalCredentialAdministration credentials,
    IEditScopeLeases editScopes) : AdministrationPageModel
{
    private const ExternalCredentialProvider Provider =
        ExternalCredentialProvider.GlassRepairEstimate;

    /// <summary>The staff account whose credential this page administers.</summary>
    public StaffAccountSummary? Account { get; private set; }

    /// <summary>What the store holds for that Engineer. Never the secret.</summary>
    public PerUserExternalCredentialStatus? Status { get; private set; }

    /// <summary>Whether the Automation ingress exists, for the area rail.</summary>
    public bool AutomationComposed { get; private set; }

    /// <summary>The external account name, kept over a failed post.</summary>
    public string Username { get; private set; } = string.Empty;

    [BindProperty]
    public long ExpectedVersion { get; set; }

    [BindProperty]
    public long ExpectedStaffAccountVersion { get; set; }

    [BindProperty]
    public string EditLeaseToken { get; set; } = string.Empty;

    public bool IsEditing => !string.IsNullOrWhiteSpace(EditLeaseToken);

    /// <summary>
    /// Set when this operator's own other window still holds the staff-account
    /// scope: the page offers Take over rather than the ordinary "another
    /// user" wording, which would misname the operator's own second window.
    /// </summary>
    public bool CanTakeOverEdit { get; private set; }

    /// <summary>The record as the operator reading an ownership sentence names it.</summary>
    private const string RecordName = "staff account";

    /// <summary>The chip's word for the stored credential's state.</summary>
    public string StateName => Status is not { Configured: true }
        ? CaseWorkspaceLabels.GlassCredential.NotConfigured
        : Status.Enabled
            ? CaseWorkspaceLabels.GlassCredential.Enabled
            : CaseWorkspaceLabels.GlassCredential.DisabledState;

    public Task<IActionResult> OnGetAsync(Guid staffId, CancellationToken cancellationToken) =>
        RunAsync(staffId, (_, _) => Task.FromResult<string?>(null), cancellationToken);

    public Task<IActionResult> OnPostSaveAsync(
        Guid staffId,
        string? username,
        string? password,
        CancellationToken cancellationToken)
    {
        Username = username?.Trim() ?? string.Empty;
        // The submitted secret is read once, here, and removed before any
        // redisplay can reach the model state it would otherwise sit in.
        ModelState.Remove("password");
        return RunAsync(
            staffId,
            async (actor, token) =>
            {
                if (!ValidateCredential(username, password))
                {
                    return null;
                }

                await credentials.ReplaceAsync(
                    actor,
                    staffId,
                    Provider,
                    ExpectedVersion,
                    ExpectedStaffAccountVersion,
                    EditLeaseToken,
                    Username,
                    password!,
                    enabled: true,
                    token);
                return CaseWorkspaceLabels.GlassCredential.Saved;
            },
            cancellationToken);
    }

    public Task<IActionResult> OnPostClearAsync(
        Guid staffId,
        CancellationToken cancellationToken) =>
        RunAsync(
            staffId,
            async (actor, token) =>
            {
                await credentials.ClearAsync(
                    actor,
                    staffId,
                    Provider,
                    ExpectedVersion,
                    ExpectedStaffAccountVersion,
                    EditLeaseToken,
                    token);
                return CaseWorkspaceLabels.GlassCredential.Cleared;
            },
            cancellationToken);

    public async Task<IActionResult> OnPostEditAsync(
        Guid staffId,
        long expectedStaffAccountVersion,
        string? operationKey,
        bool takeOver,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (staffId == Guid.Empty || !IsOperationKeyValid(operationKey)) return BadRequest();

        var result = await getStaffAccount.ExecuteAsync(new(actor, staffId), cancellationToken);
        if (result is null) return NotFound();
        if (result.Account.Version != expectedStaffAccountVersion)
        {
            ModelState.AddModelError(string.Empty, "The staff account changed. Reload it before editing this credential.");
            return await LoadAsync(actor, staffId, cancellationToken) ? Page() : NotFound();
        }

        try
        {
            var lease = await editScopes.ClaimAsync(
                new(EditScopeKind.StaffAccount, staffId, result.Account.Version, actor, operationKey!)
                {
                    TakeOver = takeOver
                },
                cancellationToken);
            EditLeaseToken = lease.Token;
            ExpectedStaffAccountVersion = result.Account.Version;
        }
        // Checked before the base EditScopeConflictException, which this type
        // derives from: the operator's own live other window is never told
        // "another user" is editing, only offered the take-over that is
        // theirs to make.
        catch (EditScopeHeldElsewhereException)
        {
            CanTakeOverEdit = true;
            ModelState.AddModelError(string.Empty, EditModeDisplay.HeldElsewhere(RecordName));
        }
        catch (EditScopeConflictException)
        {
            ModelState.AddModelError(
                string.Empty,
                EditModeDisplay.HeldBy(RecordName, CaseEditAuthorityHolder.Unnamed, isSelf: false));
        }
        catch (EditScopeVersionConflictException)
        {
            ModelState.AddModelError(string.Empty, "The staff account changed. Reload it before editing this credential.");
        }

        return await LoadAsync(actor, staffId, cancellationToken) ? Page() : NotFound();
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

    public async Task<IActionResult> OnPostCancelEditAsync(
        Guid staffId,
        string? editLeaseToken,
        string? operationKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (staffId == Guid.Empty || string.IsNullOrWhiteSpace(editLeaseToken)
            || !IsOperationKeyValid(operationKey)) return RedirectToPage(new { staffId });

        try
        {
            await editScopes.ReleaseAsync(
                new(EditScopeKind.StaffAccount, staffId, actor, operationKey!, editLeaseToken),
                cancellationToken);
        }
        catch (Exception exception) when (exception is EditScopeExpiredException or EditScopeConflictException)
        {
        }

        return RedirectToPage(new { staffId });
    }

    public async Task<IActionResult> OnPostHeartbeatEditAsync(
        Guid staffId,
        string? editLeaseToken,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor)) return Forbid();
        if (staffId == Guid.Empty || string.IsNullOrWhiteSpace(editLeaseToken))
        {
            return new ConflictObjectResult("Editing this staff account has ended. Reload it before making further changes.");
        }

        try
        {
            await editScopes.HeartbeatAsync(
                new(EditScopeKind.StaffAccount, staffId, actor, editLeaseToken), cancellationToken);
            return new OkResult();
        }
        catch (Exception exception) when (exception is EditScopeExpiredException or EditScopeConflictException)
        {
            return new ConflictObjectResult("Editing this staff account has ended. Reload it before making further changes.");
        }
    }

    /// <summary>
    /// The one place an operation is authorised, run, turned into an operator
    /// message and followed by a reload. Expected version and edit-scope
    /// refusals are actionable; other failures propagate instead of being
    /// reported as something the operator could simply retry.
    /// </summary>
    private async Task<IActionResult> RunAsync(
        Guid staffId,
        Func<ActionActor, CancellationToken, Task<string?>> operation,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }

        try
        {
            var confirmation = await operation(actor, cancellationToken);
            if (confirmation is not null)
            {
                TempData["Confirmation"] = confirmation;
                return RedirectToPage(new { staffId });
            }
        }
        catch (StaffAuthorizationException)
        {
            return Forbid();
        }
        catch (ArgumentException)
        {
            ModelState.AddModelError(
                string.Empty,
                CaseWorkspaceLabels.GlassCredential.NotAccepted);
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(
                string.Empty,
                CaseWorkspaceLabels.GlassCredential.StaleVersion);
        }
        catch (EditScopeConflictException)
        {
            ModelState.AddModelError(string.Empty, "Another user is editing this staff account.");
        }
        catch (EditScopeExpiredException)
        {
            ModelState.AddModelError(string.Empty, "Your edit session expired. Reload the credential before making further changes.");
        }
        catch (EditScopeVersionConflictException)
        {
            ModelState.AddModelError(string.Empty, "The staff account changed. Reload the credential before making further changes.");
        }

        return await LoadAsync(actor, staffId, cancellationToken) ? Page() : NotFound();
    }

    private bool ValidateCredential(string? username, string? password)
    {
        var valid = true;
        if (string.IsNullOrWhiteSpace(username))
        {
            ModelState.AddModelError(
                string.Empty,
                CaseWorkspaceLabels.GlassCredential.UsernameRequired);
            valid = false;
        }
        if (string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError(
                string.Empty,
                CaseWorkspaceLabels.GlassCredential.PasswordRequired);
            valid = false;
        }

        return valid;
    }

    private async Task<bool> LoadAsync(
        ActionActor actor,
        Guid staffId,
        CancellationToken cancellationToken)
    {
        AutomationComposed =
            HttpContext.RequestServices.GetService<AutomationClientRegistry>() is not null;
        var result = await getStaffAccount.ExecuteAsync(new(actor, staffId), cancellationToken);
        Account = result?.Account;
        if (Account is null)
        {
            return false;
        }

        Status = await credentials.GetAsync(actor, staffId, Provider, cancellationToken);
        ExpectedVersion = Status.Version;
        ExpectedStaffAccountVersion = Account.Version;
        if (Username.Length == 0)
        {
            Username = Status.Username ?? string.Empty;
        }

        return true;
    }
}
