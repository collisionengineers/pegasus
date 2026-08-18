# Proof — AUTO-002 (verified on merged `main` `d8de29cb`, deployed as release 10, 2026-08-18)

## Merged-code evidence (release-10 worktree at `d8de29cb`, Release build, LocalDB)

- `AutomationConnectorAuthorizationTests` 4/4 + ingress/document/assessment → **19/19**; `Pegasus.ArchitectureTests` 96/96; `Test-AzureDeploymentPlan.ps1 -Mode Local` pass; PR #405 CI 10/10 (after a hosted-runner checkout timeout re-run); independent review PASS (`3f…`/`15e98424` follow-ups).

## Live evidence (production, revision `pegasus-prod-web-252ow37gij--d8de29cb94f3`, 14:2x UTC)

| Step | Result |
| --- | --- |
| `/.well-known/oauth-authorization-server` | `authorization_endpoint …/authorize`, `token_endpoint …/connect/token`, `grant_types_supported [client_credentials, authorization_code, refresh_token]`, `code_challenge_methods_supported [plain, S256]`, scopes incl. `offline_access` + the four automation scopes |
| `/.well-known/oauth-protected-resource/mcp` | resource `…/mcp`, authorization server = origin, four scopes |
| Claude's exact `/authorize?…redirect_uri=https://claude.ai/api/mcp/auth_callback…` (anonymous) | 302 → `/Account/SignIn?ReturnUrl=/authorize?…` (request preserved) |
| Sign in as the seeded Administrator with that ReturnUrl | 302 → `/authorize` |
| Consent page | 200; names `https://claude.ai` and the requested scopes; hidden fields = OAuth parameters + OperationKey + antiforgery |
| Approve | 302 → `https://claude.ai/api/mcp/auth_callback?code=…&state=<state>` |
| Exchange (code + PKCE verifier + client id/secret) | access token (`expires_in` 600) + refresh token; scope `automation.cases automation.documents offline_access` |
| `/mcp` `tools/list` with the connector token | 200, 15 tools |
| `pegasus_intake_queue_list` with that token | refused: "The 'automation.intake' scope is required for this tool." |
| Refresh grant | new access + refresh token |
| ActionHistory | `automation_connector_authorized`, Outcome Succeeded, ActorKind Staff, Reason "Connector https://claude.ai; scopes: automation.cases automation.documents" (two rows: two evidence runs) |

Not proved: the Claude.ai product itself completing the flow — the operator connects the connector from their account (URL `<origin>/mcp`, client id `pegasus-automation`, secret from Key Vault). Everything the product does (discovery → `/authorize` → consent → callback with code → token exchange → `/mcp`) is exercised above against production with the product's registered redirect URI.

PR #405 merged 2026-08-18 (`d8de29cb`); shipped by release 10 ([[DELIV-009]]).

## Addendum — Claude.ai product connected (2026-08-18, ~15:07–15:09 UTC)

The operator's own Claude.ai custom connector completed the flow against production after re-entering the client secret (the first two attempts at 14:52 UTC passed consent but the exchange was refused `invalid_client` — an unauthenticated exchange, reproduced as the only failing shape). Live web console log: `Client (Anthropic/ClaudeAI 1.0.0)` ↔ `Server (pegasus-automation 0.1.0-alpha.1)`: 4× `tools/list`, 6× `tools/call`, 12× `server/discover`; case queries executed under the Automation actor. Diagnosis method for future incidents: OpenIddict token denials write `automation_token_rejected` without the OAuth error; the exchange shapes that succeed are client_secret_basic (raw or URL-encoded secret) and client_secret_post, with or without `resource`; only a secret-less exchange fails. The tier-5 external-client evidence for the connector flow is therefore complete.
