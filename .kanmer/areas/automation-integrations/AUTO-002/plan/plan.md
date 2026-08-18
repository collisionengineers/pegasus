# Plan — AUTO-002

## Approach

Extend the existing OpenIddict server (not a second server) so the one seeded
Automation client can also obtain tokens by authorization-code + PKCE with an
Administrator consent step, and issue refresh tokens for connectors. The token
principal is identical in shape to today's client-credentials principal
(`sub = client id`, scopes, MCP audience), so `AutomationActorResolver`, the
kill switch, rate limit and permanent history need no change. Redirect URIs
are configuration rendered from Bicep; when none are configured the
authorization-code permissions are simply not granted and the ingress behaves
exactly as release 9.

Rejected: a per-staff OAuth grant (ADR-0011 excludes staff MCP access) and
dynamic client registration (unbounded clients; the connector supplies the
configured client id/secret).

## Governing docs

- FRD-10 — unchanged: same actor, inventory, scopes, history, kill switch.
- ADR-0026 — stands (explicit configuration enables the ingress); ADR-0027
  (new, thin) records the additional grant, the consent boundary and the
  administrator-managed redirect URIs.

## Steps (each names what it reuses)

1. `AutomationMcpOptions.TryCreate`: parse `AutomationMcp:RedirectUris` (reuse the
   existing validation style; absolute URIs; https unless localhost). Add
   `AutomationMcp.AuthorizationEndpointPath`, `RefreshTokenLifetime` (14 days).
2. `AutomationMcpExtensions.AddPegasusAutomationMcp`: register the authorization
   endpoint, auth-code + PKCE + refresh flows, passthrough. Nothing else changes.
3. `AutomationClientRegistry.CanonicalDescriptor(enabled)`: when enabled and
   `options.RedirectUris` non-empty, add authorization endpoint / auth-code /
   refresh / `code` response type permissions, PKCE requirement, redirect URIs.
   `IsEnabledAsync` keeps its client-credentials probe (all grants are removed
   together on disable). Add `RecordConnectorDecisionAsync(actor, redirectUri,
   scopes, approved, operationKey)` reusing the ActionHistory writer already
   injected (`automation_connector_authorized` / `_denied`).
4. `AutomationTokenEndpoint.ExchangeAsync`: branch on
   `IsAuthorizationCodeGrantType()`/`IsRefreshTokenGrantType()`: authenticate
   with the OpenIddict scheme, kill-switch re-check by `request.ClientId`,
   rebuild the identity (subject, scopes incl. `offline_access`, resources,
   destinations) and `SignIn`. Client-credentials path unchanged.
5. New Razor Page `Pages/Connect/Authorize.cshtml(.cs)` at route `/authorize`,
   `[Authorize(Policy = StaffRoleNames.Administrator)]`, base
   `AdministrationPageModel` (reuse `TryGetActor`, `NewOperationKey`). GET:
   `GetOpenIddictServerRequest()`, require `ManageAutomationClients` via
   `StaffAuthorization.Require`, kill switch via registry, render client
   display name, redirect host, requested scopes with descriptions, hidden
   inputs echoing every OAuth parameter, Approve/Deny buttons (antiforgery).
   POST Approve: build principal (`sub`, scopes = requested ∩ granted +
   `offline_access`, resources, destinations), record history, `SignIn(...,
   OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)`. POST Deny:
   record history, `Forbid` with `access_denied`.
6. Bicep: `automationMcpRedirectUris` param (main → module → env
   `AutomationMcp__RedirectUris`), parameters.json
   `${AUTOMATION_MCP_REDIRECT_URIS=https://claude.ai/api/mcp/auth_callback}`.
7. Tests (new `AutomationConnectorAuthorizationTests`, reusing
   `AutomationMcpTestSupport` + the DevelopmentOffline Administrator):
   round trip (authorize → consent → code → token → `/mcp` tools/list),
   refresh grant, deny → `error=access_denied`, unregistered redirect URI
   refused by OpenIddict, missing `code_challenge` refused, disabled client
   refused at `/authorize` and at code exchange, ActionHistory row present.
   Existing ingress tests must stay green.
8. Docs: ADR-0027 + index; `docs/operations.md` MCP section (connector flow +
   redirect-URI configuration + Administrator sign-in on cross-site arrival);
   capability rows unchanged.
9. Simplification pass over the diff; post-implementation report; PR to `dev`;
   independent review; merge; release 10 (promotion + provision with the new
   env + smoke) and live connector evidence for proof.

## Verification

- `dotnet build -c Release`; focused `AutomationConnectorAuthorizationTests|AutomationMcpIngressTests|AutomationDocumentIngressTests|AutomationAssessmentIngressTests`; `Pegasus.ArchitectureTests`; `Test-AzureDeploymentPlan -Mode Local`; `Test-DocumentationLinks`.
- Live after release 10: Claude.ai connector authorises through `/authorize` (Administrator consent), lists 15 tools; ActionHistory `automation_connector_authorized`.

## Simplification pass — (to be recorded before the PR)
