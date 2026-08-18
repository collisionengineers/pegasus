---
id: DELIV-009
type: ticket
title: 'Release 10: promote dev to main and deploy the connector authorization flow'
status: done
area: delivery-repository
assignee: claude-code
profile: chore
stageEntered:
  preparing: '2026-08-18T13:22:46.527Z'
  review: '2026-08-18T14:45:30.802Z'
  verifying: '2026-08-18T14:45:35.013Z'
  done: '2026-08-18T14:45:39.890Z'
taken_at: '2026-08-18T13:36:35.288Z'
branch: task/deliv-009-release-10
worktree: ../pegasus-worktrees/deliv-009-release-10
labels:
  - release
  - requires-live-approval
links:
  - AUTO-002
  - DELIV-008
deployment: not-deployed
archived: false
created: '2026-08-18T13:22:24.613Z'
updated: '2026-08-18T14:45:39.890Z'
---

## What

Second exact-SHA fast-forward release: promote `dev` (release-9 docs refresh +
[[AUTO-002]]) to `main`, build the immutable artifacts, push the image,
`azd provision` the web revision (renders `AutomationMcp__RedirectUris`),
redeploy the Worker package, smoke, capture the live connector authorisation
evidence, and refresh the current-state docs. No migrations are pending.

## Why

The Claude.ai connector cannot connect until the authorization-code flow
([[AUTO-002]]) is live. Same route and targets as release 9 ([[DELIV-008]]).

## Verification

- Both remote heads equal the promoted SHA; main-push `repository-check` green.
- `Invoke-ProductionSmoke.ps1` passes; `/diagnostics/version` == promoted SHA;
  `/.well-known/oauth-authorization-server` advertises `authorization_endpoint`.
- Live: `/authorize` reaches the Administrator consent page; the connector
  completes the flow (or the flow is exercised end-to-end over HTTP with the
  claude.ai redirect URI up to the redirect).

## Outcome
