---
id: AUTO-002
type: ticket
title: >-
  Authorization-code + PKCE for external MCP connectors with Administrator
  consent
status: review
area: automation-integrations
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-18T12:57:36.231Z'
  review: '2026-08-18T13:21:40.912Z'
taken_at: '2026-08-18T12:59:02.005Z'
branch: task/auto-002-connector-auth-code
worktree: ../pegasus-worktrees/auto-002-connector-auth-code
labels:
  - now
  - MCP
  - requires-live-approval
links:
  - AUTO-001
refs:
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
  - docs/adr/0026-enable-automation-mcp-by-explicit-deployment-configuration.md
archived: false
created: '2026-08-18T12:57:32.747Z'
updated: '2026-08-18T13:21:40.912Z'
---

## Why

Claude.ai's remote MCP connector (and MCP clients generally) obtain tokens by
the OAuth 2.1 authorization-code flow with PKCE, redirecting the browser to
`<origin>/authorize?response_type=code&client_id=pegasus-automation&redirect_uri=https://claude.ai/api/mcp/auth_callback&code_challenge=…`.
The Pegasus authorization server (release 9) only issues client-credentials
tokens at `/connect/token`; there is no `/authorize`, so the connector lands on
the 404 page and cannot connect (observed 2026-08-18).

## What

Add authorization-code + PKCE (and refresh tokens) for the single seeded
Automation client alongside client-credentials, with an interactive
Administrator consent step: `/authorize` requires a signed-in Pegasus
Administrator with the manage-automation-clients right, shows the requesting
connector's redirect target and requested scopes, and on approval issues a
code for the **Automation Actor** principal (subject `pegasus-automation`,
requested ∩ granted scopes, MCP audience). Redirect URIs are
administrator-managed configuration (`AutomationMcp:RedirectUris`), rendered
from Bicep; no dynamic client registration. Existing kill switch, rate limit,
scopes, actor rights and permanent history are unchanged; the consent
decision is written to ActionHistory.

## Verification

- Integration (HTTP against the DevelopmentOffline host, Administrator): GET
  `/authorize` with a valid PKCE request renders consent; approve → 302 to the
  registered redirect URI with `code`+`state`; code + verifier → tokens; the
  access token calls `/mcp`; refresh grant issues a new token; deny → 
  `error=access_denied`; wrong `redirect_uri` / missing PKCE refused; disabled
  client refused at `/authorize` and `/connect/token`.
- Live: Claude.ai custom connector completes the flow against production and
  lists the 15 tools; ActionHistory carries the consent record.

## Outcome
