---
id: AUTO-001
type: ticket
title: Activate the Pegasus Automation MCP gate
status: done
area: automation-integrations
assignee: claude-code
profile: feature
stageEntered:
  implementing: '2026-08-18T09:45:52.837Z'
  review: '2026-08-18T11:17:41.883Z'
  verifying: '2026-08-18T11:34:59.459Z'
  done: '2026-08-18T12:22:59.396Z'
taken_at: '2026-08-18T11:12:31.011Z'
branch: task/auto-001-activate-mcp-gate
worktree: ../pegasus-worktrees/auto-001-activate-mcp-gate
labels:
  - now
  - requires-live-approval
  - MCP
links:
  - TICK-027
refs:
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
  - docs/adr/0021-automation-actor-direct-write-assessment-contract.md
commits:
  - a593bc89
  - f5c6840c
  - 17696a9c
  - db3f57db
  - 3f836469
  - f1e116c6
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/403'
deployment: production
archived: false
created: '2026-08-18T08:49:54.851Z'
updated: '2026-08-18T12:26:40.017Z'
---

## Why

Activate the existing Pegasus Web Automation Actor MCP composition gate only after recording the exact target, approval, credential custody, transport, scope, rate-limit, rollback, and external-caller evidence required for live use.

## Verification

- [x] The approved target and activation authority are recorded before configuration changes.
- [x] The named Automation Actor obtains a scoped token and reaches the protected `/mcp` endpoint.
- [x] The approved 15-tool inventory is exercised with success, denial, and permanent-history evidence.
- [x] The Administrator kill switch and rollback return the ingress to its closed state.

## Outcome

Automation MCP is enabled in production by explicit configuration (ADR-0026) since release 9 (2026-08-18): the source guard removed, the Key Vault secret reference and four settings rendered from Bicep on revision `pegasus-prod-web-252ow37gij--f1e116c6eb93` (PR #403, merged as `f1e116c6`). Live evidence: token issuance and refusal, unauthenticated 401 with resource metadata, 15-tool inventory, read-tool success with `Succeeded` history, scope denial and validation refusal with security events / `Failed` history, kill switch disable → tokens refused (in-flight within 12 s) → re-enable; registration left enabled. Taken over from the previous agent whose out-of-band image (`a593bc89`) was never redeployed. Not proved: an external Claude Desktop/Code connector session (endpoint, client id, secret location and scopes recorded in operations for the operator). Follow-ups: rotate `automation-mcp-client-secret` if its value was surfaced outside Key Vault; the seeded `claudeuiverification` Administrator remains flagged for removal before go-live. Closed out 2026-08-18.
