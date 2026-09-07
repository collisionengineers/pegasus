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
/// asking and for which scopes; approving issues a code for the Automation
/// Actor principal — never for the staff member — and the decision is
/// permanent history. Denying returns <c>access_denied</c> to the connector.
/// </summary>
[Authorize(Policy = StaffRoleNames.Administrator)]
public sealed class AuthorizeModel : AdministrationPageModel
{
    private static readonly IReadOnlyDictionary<string, string> ScopeDescriptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AutomationMcp.CasesScope] = "Search and read cases, take and renew edit leases, update case details.",
            [AutomationMcp.IntakeScope] = "List the intake queue and submit intake on the automation channel.",
            [AutomationMcp.DocumentsScope] = "Add, download and export case documents.",
            [AutomationMcp.AssessmentScope] = "Read and update assessment values under an edit lease.",
            [AutomationMcp.MailScope] = "List and read retained mail and correct a message's classification.",
            [AutomationMcp.JobsScope] = "List, take, progress, complete, fail and release AI jobs; create Unidentified-queue passes."
        };

    public string ClientDisplayName { get; private set; } = string.Empty;

    public string ConnectorOrigin { get; private set; } = string.Empty;

    public IReadOnlyList<(string Scope, string Description)> RequestedScopes { get; private set; } = [];

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
        RequestedScopes = GrantedScopes(request, status)
            .Select(scope => (scope, ScopeDescriptions.GetValueOrDefault(scope, scope)))
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
        var scopes = GrantedScopes(request, status);
        if (scopes.Length == 0)
        {
            // A code without any granted scope could never call a tool; refuse
            // it here rather than issue a token /mcp will reject.
            return Refuse(Errors.InvalidScope, "The connector requested no granted scope.");
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
        await registry.RecordConnectorDecisionAsync(
            actor,
            $"denied:{Guid.NewGuid():N}",
            RedirectUri(request),
            GrantedScopes(request, status),
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
