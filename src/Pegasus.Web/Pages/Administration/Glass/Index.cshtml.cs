using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
/// The post carries only what
/// <see cref="IPerUserExternalCredentialAdministration"/> takes — the expected
/// version, the account name and the secret — plus the antiforgery token the
/// form tag helper writes. A reason or an operation key would be an inert
/// required control: the contract carries neither, and the store already
/// records the actor, the moment and the credential generation itself.
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
                await credentials.ClearAsync(actor, staffId, Provider, ExpectedVersion, token);
                return CaseWorkspaceLabels.GlassCredential.Cleared;
            },
            cancellationToken);

    /// <summary>
    /// The one place an operation is authorised, run, turned into an operator
    /// message and followed by a reload. Only the store's two named refusals
    /// are turned into a message: a stale expected version, which the store
    /// raises as EF Core's concurrency exception, and material it will not
    /// accept. Anything else propagates, because a page that swallowed it
    /// would report a failure as a refusal the operator could retry.
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
        if (Username.Length == 0)
        {
            Username = Status.Username ?? string.Empty;
        }

        return true;
    }
}
