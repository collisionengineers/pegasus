# Post-implementation report — AUTO-002

## Summary

The Automation MCP authorization server now supports authorization code +
PKCE and refresh tokens for the seeded Automation client, alongside client
credentials, whenever `AutomationMcp:RedirectUris` is configured. `/authorize`
is a Pegasus Administrator consent page; approval issues a code for the
Automation Actor principal (never the staff member) and the decision is
permanent history. Bicep renders the redirect URIs (`AUTOMATION_MCP_REDIRECT_URIS`,
default `https://claude.ai/api/mcp/auth_callback`). Commit `17545b6f` on
`task/auto-002-connector-auth-code`.

## Changes

| File | Change | Why |
| --- | --- | --- |
| `src/Pegasus.Web/Mcp/AutomationMcp.cs` | `AuthorizationEndpointPath`, `RefreshTokenLifetime`; options parse/validate `AutomationMcp:RedirectUris`; `ConnectorAuthorizationEnabled` | administrator-managed exact redirect URIs gate the new grant |
| `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs` | authorization endpoint, auth-code + PKCE + refresh flows, refresh lifetime, `RegisterResources(/mcp)`, passthrough | OpenIddict 7 requires the `resource` indicator MCP clients send to be registered |
| `src/Pegasus.Web/Mcp/AutomationClientRegistry.cs` | descriptor grants auth-code/refresh/`code`/PKCE requirement/resource permission/redirect URIs when enabled+configured; `RecordConnectorDecisionAsync` | kill switch removes all grants together; consent is permanent history |
| `src/Pegasus.Web/Mcp/AutomationTokenEndpoint.cs` | accepts `authorization_code` and `refresh_token`; `AutomationPrincipal.Create` shared factory | one principal shape for every grant |
| `src/Pegasus.Web/Program.cs` | seed the registration on `/authorize`; consent refusals not logged as transport denials | OpenIddict validates the client before the page runs |
| `src/Pegasus.Web/Pages/Connect/Authorize.cshtml(.cs)` (new) | Administrator consent page (GET render; POST Accept → SignIn; POST Deny → access_denied) | the interactive step of the flow |
| `infra/main.bicep`, `infra/main.parameters.json`, `infra/modules/platform.bicep` | `automationMcpRedirectUris` → `AutomationMcp__RedirectUris` | infrastructure owns the enabled connectors |
| `tests/Pegasus.IntegrationTests/AutomationConnectorAuthorizationTests.cs` (new), `AutomationMcpTestSupport.cs` | round trip + refresh + history; deny; unregistered redirect/missing PKCE; disabled client | FRD-10 real-caller evidence bar |
| `docs/adr/0027-…md` (new), `docs/adr/README.md`, `docs/operations.md` | decision + operations connector-flow note | governance |

## Governing docs

- FRD-10 unchanged (actor boundary, inventory, scopes, history, kill switch identical).
- ADR-0027 (accepted) records the added grant, the consent boundary and the redirect-URI configuration; ADR-0011/0021/0026 stand.

## Verification (Release, LocalDB)

- `dotnet build ./Pegasus.slnx -c Release` → 0/0.
- `AutomationConnectorAuthorizationTests` 4/4; with `AutomationMcpIngressTests|AutomationDocumentIngressTests|AutomationAssessmentIngressTests` → 19/19.
- `Pegasus.ArchitectureTests` 96/96; `Test-AzureDeploymentPlan.ps1 -Mode Local` pass; `Test-DocumentationLinks.ps1` pass.

## Risks / follow-ups

- Claude Code CLI callbacks use a random loopback port and cannot be pre-registered exactly; out of scope (follow-up if wanted).
- Refresh tokens are self-contained under ephemeral keys: they die on a Web restart, and the connector re-authorises (Administrator consent again).
- Cross-site arrival with the strict same-site cookie means one Administrator sign-in per authorisation.

## Verification hand-off

On merged `main` after release 10: run the four connector tests + ingress tests; then live: Claude.ai custom connector (URL `<origin>/mcp`, client id/secret) → browser lands on `/authorize` → Administrator signs in and authorises → connector lists 15 tools; `ActionHistory` row `automation_connector_authorized`; discovery `/.well-known/oauth-authorization-server` advertises `authorization_endpoint` and `code_challenge_methods_supported: S256`.
