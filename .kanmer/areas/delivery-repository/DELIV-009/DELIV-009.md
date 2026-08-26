---
id: DELIV-009
type: ticket
title: 'Release 10: promote dev to main and deploy the connector authorization flow'
status: done
area: delivery-repository
order: 440
assignee: claude-code
profile: chore
stageEntered:
  preparing: '2026-08-18T13:22:46.527Z'
  review: '2026-08-18T14:45:30.802Z'
  verifying: '2026-08-18T14:45:35.013Z'
  done: '2026-08-18T14:45:39.890Z'
labels:
  - release
  - requires-live-approval
links:
  - AUTO-002
  - DELIV-008
commits:
  - d8de29cb94f396816595b1f9782980476166dbfa
  - 97514c4a
  - 4519edb2
  - f79c24d9
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/406'
  - 'https://github.com/collisionengineers/pegasus/pull/407'
deployment: production
archived: false
created: '2026-08-18T13:22:24.613Z'
updated: '2026-08-26T14:34:43.320Z'
---

## What

Second exact-SHA fast-forward release: promote `dev` (release-9 docs refresh +
[[AUTO-002]]) to `main`, build the immutable artifacts, push the image,
`azd provision` the web revision (renders `AutomationMcp__RedirectUris`),
redeploy the Worker package, smoke, capture the live connector authorisation
evidence, and refresh the current-state docs. No migrations were pending.

## Why

The Claude.ai connector cannot connect until the authorization-code flow
([[AUTO-002]]) is live. Same route and targets as release 9 ([[DELIV-008]]).

## Verification

- [x] Both remote heads equal the promoted SHA; main-push `repository-check` green (guard: 2 new first-parent commits contained in dev).
- [x] `Invoke-ProductionSmoke.ps1` passes; `/diagnostics/version` == `d8de29cb`; discovery advertises `authorization_endpoint`.
- [x] Live: `/authorize` reaches sign-in then the Administrator consent page; the flow was driven end-to-end over HTTP with the claude.ai redirect URI (code → tokens → `/mcp` → refresh).

## Outcome

Release 10 shipped 2026-08-18: `main` = `dev` = `d8de29cb` (PR #406 auto-merged by the push); web revision `pegasus-prod-web-252ow37gij--d8de29cb94f3` (image `sha256:4bd50f66…`, redirect URI rendered); Worker redeployed via `config-zip`, polling; smoke passed; artifacts retained under `artifacts/releases/release-10-d8de29cb/`; docs refresh merged as PR #407 (`f79c24d9`, rides the next release). Route facts: hosted-runner full-history checkouts of the ~700 MB repository timed out at the 5-minute cap on three of five `changes`/`documentation` jobs today (all green on re-run) → CI reliability follow-up filed; the known `DistinctParallelRetriesResolveToOneCaseAggregate` deadlock flake hit once on the main run and passed on re-run. Closed out 2026-08-18.
