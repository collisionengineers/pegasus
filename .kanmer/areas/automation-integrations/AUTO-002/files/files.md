# Files — AUTO-002

| File | Change |
| --- | --- |
| `src/Pegasus.Web/Mcp/AutomationMcp.cs` | constants `AuthorizationEndpointPath = "/authorize"`, `RefreshTokenLifetime`; `AutomationMcpOptions.RedirectUris` parsed from `AutomationMcp:RedirectUris` (comma/semicolon list; absolute; https except localhost) |
| `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs` | server: authorization endpoint, `AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange()`, `AllowRefreshTokenFlow()`, refresh lifetime, `EnableAuthorizationEndpointPassthrough()` |
| `src/Pegasus.Web/Mcp/AutomationClientRegistry.cs` | descriptor: when redirect URIs are configured and enabled → `Endpoints.Authorization`, `GrantTypes.AuthorizationCode`, `GrantTypes.RefreshToken`, `ResponseTypes.Code`, `Requirements.Features.ProofKeyForCodeExchange`, redirect URIs; new `RecordConsentAsync` history helper |
| `src/Pegasus.Web/Mcp/AutomationTokenEndpoint.cs` | accept `authorization_code` and `refresh_token` grants: authenticate the OpenIddict principal, kill-switch re-check, re-issue with destinations |
| `src/Pegasus.Web/Pages/Connect/Authorize.cshtml` + `.cshtml.cs` (new) | Administrator consent page at `/authorize`: GET renders client/scopes/redirect; POST Accept/Deny → OpenIddict SignIn / Forbid; ActionHistory record |
| `infra/modules/platform.bicep`, `infra/main.bicep`, `infra/main.parameters.json` | `automationMcpRedirectUris` param → `AutomationMcp__RedirectUris` env (`${AUTOMATION_MCP_REDIRECT_URIS=https://claude.ai/api/mcp/auth_callback}`) |
| `tests/Pegasus.IntegrationTests/AutomationMcpTestSupport.cs` | `WithAutomationMcp` sets `AutomationMcp:RedirectUris`; PKCE helpers |
| `tests/Pegasus.IntegrationTests/AutomationConnectorAuthorizationTests.cs` (new) | auth-code round trip, refresh, deny, redirect mismatch, PKCE required, disabled client |
| `docs/adr/0027-authorization-code-for-external-mcp-connectors.md` (new), `docs/adr/README.md` | thin ADR + index row |
| `docs/operations.md` | Automation MCP section: connector flow (auth code + PKCE, Administrator consent, redirect URI config) |
