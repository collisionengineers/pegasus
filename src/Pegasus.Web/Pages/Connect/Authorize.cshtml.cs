using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Pegasus.Core.Identity;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Mcp;
using Pegasus.Web.Pages.Administration;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Pegasus.Web.Pages.Connect;

/// <summary>
/// The staff step of the authorization-code flow. OpenIddict has already
/// validated the request (client, redirect URI, PKCE challenge, scopes) before
/// this page runs, and the page branches on the client it names.
/// <para>
/// For the Automation client it is the Administrator consent for an external
/// MCP connector, open only to the manage-automation-clients right: the
/// Administrator sees who is asking and for which scopes, each with a tick
/// box ticked by default; approving issues a code for the Automation Actor
/// principal — never for the staff member — holding the ticked scopes
/// (ADR-0087), and the decision is permanent history. Denying, or approving
/// with no scope ticked, returns <c>access_denied</c> to the connector.
/// </para>
/// <para>
/// For Pegasus Desktop's client it is the sign-in any enabled member of staff
/// may make (ADR-0092, R1297): the page names them, and Sign in issues a code
/// for their own account, so what the application then does is theirs.
/// </para>
/// </summary>
public sealed class AuthorizeModel : AdministrationPageModel, Pegasus.Web.Presentation.IShellFreeGet
{
    internal static readonly IReadOnlyDictionary<string, string> ScopeDescriptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AutomationMcp.CasesScope] = "Search and read Cases; update Case details and notes; hold, release, assign, close, reopen and archive Cases; open and work queries; dismiss Work Centre records; issue, extend and cancel photo links; take and renew edit leases.",
            [AutomationMcp.IntakeScope] = "List the queue and submit sources; create Cases and accept received items; work Unidentified, Triage and Vehicle images records and upload groups, and register images.",
            [AutomationMcp.DocumentsScope] = "Add, download, tag, retype and remove Case documents; generate the report, Repair Spec and images; record report approval; write report wording and prepare report images.",
            [AutomationMcp.AssessmentScope] = "Read and update assessment values, valuations and estimates under an edit lease.",
            [AutomationMcp.MailScope] = "List, read and search retained mail and the approved mailboxes; correct a message's classification; link, unlink, dismiss and restore messages; open a query on a message.",
            [AutomationMcp.JobsScope] = "List, create, take, progress, complete, fail, release, cancel and confirm AI jobs.",
            [AutomationMcp.SendScope] = "Send a generated report to the Principal; send e-mail: chaser, reply, forward and the Triage reply; send a photo link by e-mail or text."
        };

    /// <summary>The request is Pegasus Desktop's sign-in, not a connector's consent.</summary>
    public bool IsDesktopSignIn { get; private set; }

    /// <summary>The account Pegasus Desktop would act as: the name the shell shows.</summary>
    public string SignedInAs { get; private set; } = string.Empty;

    public string ClientDisplayName { get; private set; } = string.Empty;

    public string ConnectorOrigin { get; private set; } = string.Empty;

    /// <summary>
    /// Each requested scope with its words. The identifier is not printed
    /// (v41 S5): it travels only as the value of the scope's tick box.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> RequestedAccess { get; private set; } = [];

    /// <summary>The scopes the Administrator left ticked; the grant holds these.</summary>
    [BindProperty]
    public string[] ApprovedScopes { get; set; } = [];

    public IReadOnlyList<KeyValuePair<string, string>> RequestParameters { get; private set; } = [];

    [BindProperty]
    public string OperationKey { get; set; } = NewOperationKey();

    public async Task<IActionResult> OnGetAsync(
        [FromServices] AutomationClientRegistry? registry,
        [FromServices] EfStaffAccountQueries staffAccounts,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (IsDesktopRequest(registry))
        {
            if (await ReadDesktopAccountAsync(actor, staffAccounts, cancellationToken) is null)
            {
                return Forbid();
            }

            IsDesktopSignIn = true;
            SignedInAs = User.Identity?.Name ?? string.Empty;
            RequestParameters = EchoedParameters(OpenIddictRequest());
            return Page();
        }
        if (!StaffAuthorization.IsAuthorized(actor, StaffAccessRight.ManageAutomationClients))
        {
            return Forbid();
        }
        if (registry is null)
        {
            return NotFound();
        }

        var request = OpenIddictRequest();
        if (!await registry.IsEnabledAsync(request.ClientId ?? string.Empty, cancellationToken))
        {
            return Refuse(Errors.UnauthorizedClient, "The Automation client registration is disabled.");
        }

        var status = await registry.GetStatusAsync(actor, cancellationToken);
        ClientDisplayName = status.DisplayName ?? status.ClientId;
        ConnectorOrigin = RedirectUri(request).GetLeftPart(UriPartial.Authority);
        RequestedAccess = GrantedScopes(request, status)
            .Select(scope => new KeyValuePair<string, string>(scope, ScopeDescriptions.GetValueOrDefault(scope, scope)))
            .ToArray();
        RequestParameters = EchoedParameters(request);
        return Page();
    }

    public async Task<IActionResult> OnPostAcceptAsync(
        [FromServices] AutomationClientRegistry? registry,
        [FromServices] EfStaffAccountQueries staffAccounts,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (IsDesktopRequest(registry))
        {
            return await AcceptDesktopSignInAsync(registry, staffAccounts, actor, cancellationToken);
        }
        if (!StaffAuthorization.IsAuthorized(actor, StaffAccessRight.ManageAutomationClients))
        {
            return Forbid();
        }
        if (registry is null)
        {
            return NotFound();
        }
        if (!IsOperationKeyValid(OperationKey))
        {
            return Refuse(Errors.InvalidRequest, "The consent form has expired. Retry the connection.");
        }

        var request = OpenIddictRequest();
        var clientId = request.ClientId ?? string.Empty;
        if (!await registry.IsEnabledAsync(clientId, cancellationToken))
        {
            return Refuse(Errors.UnauthorizedClient, "The Automation client registration is disabled.");
        }

        var status = await registry.GetStatusAsync(actor, cancellationToken);
        var requested = GrantedScopes(request, status);
        if (requested.Length == 0)
        {
            // A code without any granted scope could never call a tool; refuse
            // it here rather than issue a token /mcp will reject.
            return Refuse(Errors.InvalidScope, "The connector requested no granted scope.");
        }

        // The grant holds the scopes left ticked (ADR-0087). Approving none
        // is the refusal the Refuse button records.
        var scopes = requested
            .Where(scope => ApprovedScopes.Contains(scope, StringComparer.Ordinal))
            .ToArray();
        if (scopes.Length == 0)
        {
            return await DenyAsync(registry, actor, request, requested, cancellationToken);
        }

        var grantId = $"grant:{Guid.NewGuid():N}";
        await registry.RecordConnectorDecisionAsync(
            actor,
            grantId,
            RedirectUri(request),
            scopes,
            approved: true,
            OperationKey,
            cancellationToken);

        // The code is issued for the Automation Actor, never the staff member;
        // offline_access lets the connector refresh without a new consent
        // until the refresh token expires or the client is disabled.
        return SignIn(
            AutomationPrincipal.Create(
                clientId,
                grantId,
                [.. scopes, Scopes.OfflineAccess]),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    public async Task<IActionResult> OnPostDenyAsync(
        [FromServices] AutomationClientRegistry? registry,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        if (IsDesktopRequest(registry))
        {
            await registry.RecordDesktopSignInAsync(
                actor,
                $"refused:{Guid.NewGuid():N}",
                RedirectUri(OpenIddictRequest()),
                approved: false,
                IsOperationKeyValid(OperationKey) ? OperationKey : NewOperationKey(),
                cancellationToken);
            return Refuse(Errors.AccessDenied, "The sign-in was refused.");
        }
        if (!StaffAuthorization.IsAuthorized(actor, StaffAccessRight.ManageAutomationClients))
        {
            return Forbid();
        }
        if (registry is null)
        {
            return NotFound();
        }

        var request = OpenIddictRequest();
        var status = await registry.GetStatusAsync(actor, cancellationToken);
        return await DenyAsync(registry, actor, request, GrantedScopes(request, status), cancellationToken);
    }

    private async Task<IActionResult> DenyAsync(
        AutomationClientRegistry registry,
        ActionActor actor,
        OpenIddictRequest request,
        string[] requestedScopes,
        CancellationToken cancellationToken)
    {
        await registry.RecordConnectorDecisionAsync(
            actor,
            $"denied:{Guid.NewGuid():N}",
            RedirectUri(request),
            requestedScopes,
            approved: false,
            IsOperationKeyValid(OperationKey) ? OperationKey : NewOperationKey(),
            cancellationToken);
        return Refuse(Errors.AccessDenied, "The Administrator refused the connection.");
    }

    /// <summary>
    /// Sign in on the Pegasus Desktop page: the decision is recorded under the
    /// member of staff's name, and the code is issued for their own account,
    /// carrying the security stamp it has now, every scope and
    /// <c>offline_access</c>. What a call may then do is decided by their own
    /// rights, not by a consent (ADR-0092).
    /// </summary>
    private async Task<IActionResult> AcceptDesktopSignInAsync(
        AutomationClientRegistry registry,
        EfStaffAccountQueries staffAccounts,
        ActionActor actor,
        CancellationToken cancellationToken)
    {
        if (!IsOperationKeyValid(OperationKey))
        {
            return Refuse(Errors.InvalidRequest, "The consent form has expired. Retry the connection.");
        }
        if (await ReadDesktopAccountAsync(actor, staffAccounts, cancellationToken) is not { } account)
        {
            return Forbid();
        }

        var grantId = DesktopPrincipal.NewGrantId();
        await registry.RecordDesktopSignInAsync(
            actor,
            grantId,
            RedirectUri(OpenIddictRequest()),
            approved: true,
            OperationKey,
            cancellationToken);
        return SignIn(
            DesktopPrincipal.Create(
                account.StaffId,
                account.SecurityStamp,
                grantId,
                [.. AutomationMcp.Scopes, Scopes.OfflineAccess]),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Whether the validated request names Pegasus Desktop's client in a
    /// deployment that offers its sign-in.
    /// </summary>
    private bool IsDesktopRequest([NotNullWhen(true)] AutomationClientRegistry? registry) =>
        registry is not null
        && registry.IsDesktop(HttpContext.GetOpenIddictServerRequest()?.ClientId);

    /// <summary>
    /// The signed-in member of staff's account as the token check will read
    /// it, or null when it could not sign in to Pegasus now: missing,
    /// disabled, holding no staff role or waiting on a password change.
    /// </summary>
    private static async Task<(Guid StaffId, string SecurityStamp)?> ReadDesktopAccountAsync(
        ActionActor actor,
        EfStaffAccountQueries staffAccounts,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(actor.SubjectId, out var staffId))
        {
            return null;
        }

        var account = await staffAccounts.GetAccountReadAsync(staffId, cancellationToken);
        if (account is not { IsEnabled: true, MustChangePassword: false, Role: not null }
            || string.IsNullOrEmpty(account.SecurityStamp))
        {
            return null;
        }

        return (staffId, account.SecurityStamp);
    }

    private static KeyValuePair<string, string>[] EchoedParameters(OpenIddictRequest request) =>
        request.GetParameters()
            .Where(parameter => !string.Equals(parameter.Key, "__RequestVerificationToken", StringComparison.Ordinal)
                && !string.Equals(parameter.Key, nameof(OperationKey), StringComparison.Ordinal))
            .Select(parameter => new KeyValuePair<string, string>(parameter.Key, (string?)parameter.Value ?? string.Empty))
            .ToArray();

    private OpenIddictRequest OpenIddictRequest() =>
        HttpContext.GetOpenIddictServerRequest()
        ?? throw new InvalidOperationException("The OpenIddict server request is unavailable.");

    private static Uri RedirectUri(OpenIddictRequest request) =>
        new(request.RedirectUri ?? string.Empty, UriKind.Absolute);

    private static string[] GrantedScopes(OpenIddictRequest request, AutomationClientStatus status) =>
        request.GetScopes()
            .Where(scope => status.GrantedScopes.Contains(scope, StringComparer.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private ForbidResult Refuse(string error, string description) =>
        Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
            }),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
}
