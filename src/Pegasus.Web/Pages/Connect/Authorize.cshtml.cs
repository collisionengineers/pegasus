using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Pegasus.Core.Identity;
using Pegasus.Web.Mcp;
using Pegasus.Web.Pages.Administration;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Pegasus.Web.Pages.Connect;

/// <summary>
/// The Administrator consent step of the authorization-code flow for external
/// MCP connectors. OpenIddict has already validated the request (client,
/// exact redirect URI, PKCE challenge, scopes) before this page runs. A signed
/// in Administrator with the manage-automation-clients right sees who is
/// asking and for which scopes, each with a tick box ticked by default;
/// approving issues a code for the Automation Actor principal — never for the
/// staff member — holding the ticked scopes (ADR-0087), and the decision is
/// permanent history. Denying, or approving with no scope ticked, returns
/// <c>access_denied</c> to the connector.
/// </summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class AuthorizeModel : AdministrationPageModel, Pegasus.Web.Presentation.IShellFreeGet
{
    internal static readonly IReadOnlyDictionary<string, string> ScopeDescriptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AutomationMcp.CasesScope] = "Search and read cases, take and renew edit leases, update case details.",
            [AutomationMcp.IntakeScope] = "List the queue, submit, and work Unidentified and Triage records.",
            [AutomationMcp.DocumentsScope] = "Add and download case documents.",
            [AutomationMcp.AssessmentScope] = "Read and update assessment values under an edit lease.",
            [AutomationMcp.MailScope] = "List and read retained mail and correct a message's classification.",
            [AutomationMcp.JobsScope] = "List, create, take, progress, complete, fail, release, cancel and confirm AI jobs.",
            [AutomationMcp.SendScope] = "Send a generated report to the Principal, and send e-mail: new, reply, forward and the Triage reply."
        };

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
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
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
        RequestParameters = request.GetParameters()
            .Where(parameter => !string.Equals(parameter.Key, "__RequestVerificationToken", StringComparison.Ordinal)
                && !string.Equals(parameter.Key, nameof(OperationKey), StringComparison.Ordinal))
            .Select(parameter => new KeyValuePair<string, string>(parameter.Key, (string?)parameter.Value ?? string.Empty))
            .ToArray();
        return Page();
    }

    public async Task<IActionResult> OnPostAcceptAsync(
        [FromServices] AutomationClientRegistry? registry,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var actor))
        {
            return Forbid();
        }
        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
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
        StaffAuthorization.Require(actor, StaffAccessRight.ManageAutomationClients);
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
