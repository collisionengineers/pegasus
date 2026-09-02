---
id: AUTO-002
type: ticket
title: >-
  Authorization-code + PKCE for external MCP connectors with Administrator
  consent
status: done
area: automation-integrations
order: 350
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-18T12:57:36.231Z'
  review: '2026-08-18T13:21:40.912Z'
  verifying: '2026-08-18T13:52:59.648Z'
  done: '2026-08-18T14:45:26.738Z'
labels:
  - now
  - MCP
  - requires-live-approval
links:
  - AUTO-001
refs:
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
  - docs/adr/0026-enable-automation-mcp-by-explicit-deployment-configuration.md
commits:
  - 17545b6f
  - '15e98424'
  - d8de29cb
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/405'
deployment: production
archived: false
created: '2026-08-18T12:57:32.747Z'
updated: '2026-09-01T14:44:31.978Z'
---

## Why

Claude.ai's remote MCP connector (and MCP clients generally) obtain tokens by
the OAuth 2.1 authorization-code flow with PKCE, redirecting the browser to
`<origin>/authorize?response_type=code&client_id=pegasus-automation&redirect_uri=https://claude.ai/api/mcp/auth_callback&code_challenge=…`.
The Pegasus authorization server (release 9) only issued client-credentials
tokens at `/connect/token`; there was no `/authorize`, so the connector landed
on the 404 page and could not connect (observed 2026-08-18).

## What

Authorization-code + PKCE (and refresh tokens) for the single seeded
Automation client alongside client-credentials, with an interactive
Administrator consent step at `/authorize`; codes are issued for the
**Automation Actor** principal; redirect URIs are administrator-managed
configuration (`AutomationMcp:RedirectUris`) rendered from Bicep; consent
decisions are permanent history. Kill switch, rate limit, scopes and actor
rights unchanged.

## Verification

- [x] Integration: consent renders; approve → 302 to the registered redirect URI with `code`+`state`; code + verifier → tokens; the access token calls `/mcp`; refresh works; deny → `access_denied`; unregistered `redirect_uri` / missing PKCE refused; disabled client refused at `/authorize` and `/connect/token`.
- [x] Live (release 10): discovery, sign-in redirect, Administrator consent naming claude.ai, code to `https://claude.ai/api/mcp/auth_callback`, exchange, `/mcp` 15 tools with scope enforcement, refresh, ActionHistory `automation_connector_authorized`.

## Outcome

Shipped via PR #405 (merged 2026-08-18 as `d8de29cb`) and live in production since release 10 ([[DELIV-009]]) with `AutomationMcp__RedirectUris=https://claude.ai/api/mcp/auth_callback`. ADR-0027 records the decision. The Claude.ai product completing the flow from the operator's own account is the one step not driven here (URL `<origin>/mcp`, client id `pegasus-automation`, secret in Key Vault). Follow-ups: Claude Code CLI loopback callbacks (random port) are not registrable — separate ticket if wanted; consider dropping `plain` from `code_challenge_methods_supported` (S256 only). Closed out 2026-08-18.
