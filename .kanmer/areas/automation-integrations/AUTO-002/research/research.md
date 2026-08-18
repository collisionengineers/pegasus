# Research — AUTO-002

## Verified by read-only checks (2026-08-18, `main` f1e116c6)

- Claude.ai connector redirect observed: `GET /authorize?response_type=code&client_id=pegasus-automation&redirect_uri=https%3A%2F%2Fclaude.ai%2Fapi%2Fmcp%2Fauth_callback&code_challenge=…&code_challenge_method=S256&state=…&scope=automation.cases+automation.intake+automation.documents+automation.assessment&resource=<origin>/mcp` → app 404 page (no such route).
- Server composition `src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs:35-53`: OpenIddict server with `SetTokenEndpointUris("/connect/token")`, `AllowClientCredentialsFlow()`, four registered scopes, 10-min access tokens, ephemeral signing/encryption keys, `EnableTokenEndpointPassthrough()`, transport-security requirement disabled (TLS at ingress); validation `UseLocalServer` + audience `pegasus-automation-mcp`; MCP auth scheme advertises resource metadata with `AuthorizationServers = PublicOrigin`. No authorization endpoint configured.
- Token endpoint `AutomationTokenEndpoint.cs`: passthrough handler accepting only `client_credentials`; re-checks kill switch; issues principal `sub=<clientId>`, scopes, resources=audience, destinations access token.
- Client registration `AutomationClientRegistry.CanonicalDescriptor` (`:186-207`): confidential client, `ConsentType Implicit`, scope permissions, and when enabled `Endpoints.Token` + `GrantTypes.ClientCredentials`. Kill switch = presence of the client-credentials permission (`IsEnabledAsync`), cached ≤60 s. `SetEnabledAsync` writes ActionHistory `automation_client_enabled/disabled` and requires `StaffAccessRight.ManageAutomationClients`.
- Actor resolution `AutomationActorResolver.RequireAsync`: client id from `NameIdentifier`/`sub` claim → kill switch → scope check → automation actor. Any token whose subject is the client id and carries scopes + audience is accepted — an auth-code token shaped like the client-credentials token works unchanged.
- Staff auth: policy scheme → Identity cookie (`__Host-Pegasus`, SameSite=Strict, LoginPath `/Account/SignIn`); fallback authorization policy requires authenticated user; admin pages use `[Authorize(Policy = StaffRoleNames.Administrator)]` + `AdministrationPageModel.TryGetActor` (`StaffActorFactory`). In DevelopmentOffline the `DevelopmentOfflineAuthenticationHandler` auto-authenticates the seeded Administrator — integration tests can hit `/authorize` as an Administrator without a sign-in dance.
- DB grants (`20260803151159_AutomationActorOpenIddict.cs:195-201`): Web role has SELECT/INSERT/UPDATE on `OpenIddictApplications`, `OpenIddictAuthorizations`, `OpenIddictTokens` — authorization-code/refresh storage needs nothing new.
- Tests: `tests/Pegasus.IntegrationTests/AutomationMcpTestSupport.cs` (`WithAutomationMcp` sets `Features:AutomationMcp`, client id/secret, `PublicOrigin http://localhost/`; token/JSON-RPC helpers), `AutomationMcpIngressTests.cs` (gate/token/inventory/kill-switch/guard tests, ~496 lines).
- Consent-page conventions: `Pages/Administration/Automation/Index.cshtml` (panel/notice/detail-list/button-row classes, `_PageHeader`, antiforgery form with hidden `OperationKey`).
- Docs: FRD-10 does not name the OAuth grant type (no FRD text change needed); ADR-0011/0021/0026 describe client-credentials for the single client → a thin new ADR records the additional grant. `docs/operations.md` MCP section and the connector notes need the auth-code shape.

## Assumed (to be confirmed by tests)

- OpenIddict passthrough for the authorization endpoint delivers GET (query) and POST (form) requests to a Razor Page mapped at `/authorize`; the consent form must re-emit the OAuth parameters as hidden inputs (OpenIddict reads the form body on POST).
- Setting `offline_access` in the principal's scopes at code exchange makes OpenIddict issue a refresh token when the refresh-token flow is allowed and the client has the `GrantTypes.RefreshToken` permission.
- Cross-site arrival with a `SameSite=Strict` cookie means the Administrator signs in once per connector authorisation (the cookie is not sent on the cross-site GET; the sign-in redirect round-trip is same-site) — acceptable.
- Claude.ai supplies the configured client id/secret at the token endpoint (confidential client); no dynamic client registration required.

## Out of scope

- Claude Code CLI callback (`http://localhost:<random-port>/callback`) — cannot be pre-registered exactly; follow-up if wanted.
- Dynamic client registration; per-staff OAuth (ADR-0011 excludes it).
