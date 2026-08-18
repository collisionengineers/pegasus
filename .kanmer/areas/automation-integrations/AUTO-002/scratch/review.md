## Review — 2026-08-18 (independent reviewer agent; claude-code applied dispositions)

PR #405 reviewed at `17545b6f`; verdict PASS with non-blocking findings; follow-ups in `15e98424`.

### Reviewer's reading
Flow security: no path issues a staff principal or bypasses `ManageAutomationClients` (GET/Accept/Deny + class `[Authorize(Administrator)]`); CSRF via Razor Pages antiforgery + strict same-site cookie; the page re-emits only OAuth parameters; kill switch refuses at GET, Accept, code/refresh exchange, and the descriptor drops every connector grant on disable; redirect URIs exact/https-or-loopback/no fragment and granted only when enabled+configured; `offline_access` only on the connector path; client-credentials cannot obtain a refresh token. OpenIddict: `/mcp` resource string matches what the protected-resource metadata advertises; `?handler=` harmless; middleware seeding on `/authorize` leaves token/mcp denial logging unchanged. Tests cover round trip, scoped `/mcp`, refresh, deny, redirect/PKCE refusals, disabled client, history. ADR-0027 valid, one decision, index in order; operations paragraph factual, no live claim.

### Dispositions
1. non-blocking — fixed: Accept with zero granted scopes now refuses (`invalid_scope`) instead of issuing an unusable code.
2. non-blocking — fixed: `DisableSlidingRefreshTokenExpiration()`; ADR-0027 states the hard 14-day re-consent.
3. non-blocking — fixed: `docs/current-architecture.md` names both grants.
4. non-blocking — fixed: deny records the same granted-scope list as approve.
5. non-blocking — won't-do: logging OpenIddict-level `/authorize` refusals (unknown client / unregistered redirect) as automation security events — those are pre-consent, connector-side validation failures answered by OpenIddict; left as-is, can be added if the operator wants a signal.
6. non-blocking — won't-do here: tests for refresh-after-disable and non-Administrator staff refusal (DevelopmentOffline host authenticates everyone as Administrator); the kill switch is re-checked on the refresh path by the same code as the code path.
7. CI: `changes` job cancelled by a hosted-runner git-fetch timeout on the first run; re-run required before merge.
