using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Identity;
using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;

namespace Pegasus.Web.Pages.Administration.Glass;

/// <summary>
/// One Engineer's Glass repair-estimate credential. The page states whether a
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
/// <c>Reason</c> and <c>OperationKey</c> gate the post here — a save or clear
/// without either is refused and the key is re-minted — but
/// <see cref="IPerUserExternalCredentialAdministration"/> carries neither
/// parameter, so only the expected version reaches the store. The two values
/// are the ones to forward the moment that contract takes them.
/// </remarks>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class IndexModel(
    IGetStaffAccount getStaffAccount,
    IPerUserExternalCredentialAdministration credentials) : AdministrationPageModel
{
    private const ExternalCredentialProvider Provider =
        ExternalCredentialProvider.GlassRepairEstimate;

    /// <summary>The Engineer whose credential this page administers.</summary>
    public StaffAccountSummary? Account { get; private set; }

    /// <summary>What the store holds for that Engineer. Never the secret.</summary>
    public PerUserExternalCredentialStatus? Status { get; private set; }

    /// <summary>Whether the Automation ingress exists, for the area rail.</summary>
    public bool AutomationComposed { get; private set; }

    /// <summary>The external account name, kept over a failed post.</summary>
    public string Username { get; private set; } = string.Empty;

    /// <summary>The reason typed into the save form, kept over a failed post.</summary>
    public string SaveReason { get; private set; } = string.Empty;

    /// <summary>The reason typed into the clear form, kept over a failed post.</summary>
    public string ClearReason { get; private set; } = string.Empty;

    [BindProperty]
    public string Reason { get; set; } = string.Empty;

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    [BindProperty]
    public long ExpectedVersion { get; set; }

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
        SaveReason = Reason;
        // The submitted secret is read once, here, and removed before any
        // redisplay can reach the model state it would otherwise sit in.
        ModelState.Remove("password");
        return RunAsync(
            staffId,
            async (actor, token) =>
            {
                if (!Validate() | !ValidateCredential(username, password))
                {
                    return null;
                }

                await credentials.ReplaceAsync(
                    actor,
                    staffId,
                    Provider,
                    ExpectedVersion,
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
        CancellationToken cancellationToken)
    {
        ClearReason = Reason;
        return RunAsync(
            staffId,
            async (actor, token) =>
            {
                if (!Validate())
                {
                    return null;
                }

                await credentials.ClearAsync(actor, staffId, Provider, ExpectedVersion, token);
                return CaseWorkspaceLabels.GlassCredential.Cleared;
            },
            cancellationToken);
    }

    /// <summary>
    /// The one place an operation is authorised, run, turned into an operator
    /// message and followed by a reload. A refused post re-mints the operation
    /// key, so a corrected retry cannot replay the key the server already saw.
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
        catch (InvalidOperationException)
        {
            ModelState.AddModelError(
                string.Empty,
                CaseWorkspaceLabels.GlassCredential.StaleVersion);
        }

        OperationKey = NewOperationKey();
        ModelState.Remove(nameof(OperationKey));
        return await LoadAsync(actor, staffId, cancellationToken) ? Page() : NotFound();
    }

    private bool Validate()
    {
        var valid = true;
        if (string.IsNullOrWhiteSpace(OperationKey) || !IsOperationKeyValid(OperationKey))
        {
            ModelState.AddModelError(
                string.Empty,
                CaseWorkspaceLabels.GlassCredential.Expired);
            valid = false;
        }
        if (string.IsNullOrWhiteSpace(Reason))
        {
            ModelState.AddModelError(
                string.Empty,
                CaseWorkspaceLabels.GlassCredential.ReasonRequired);
            valid = false;
        }

        return valid;
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
        if (Username.Length == 0)
        {
            Username = Status.Username ?? string.Empty;
        }

        return true;
    }
}
