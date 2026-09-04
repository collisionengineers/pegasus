---
id: CASE-040
type: ticket
title: >-
  Sign-off Engineer on the Case with the default rule, ribbon field and Send to
  EVA dialog (re-send from With Engineer)
status: review
area: case-reference-workflow
assignee: wf-build/case-040
profile: feature
stageEntered:
  preparing: '2026-09-02T22:11:20.520Z'
  review: '2026-09-04T20:27:33.931Z'
taken_at: '2026-09-04T18:42:59.177Z'
branch: task/case-040-sign-off-engineer-eva
worktree: .worktrees/case-040
claim_expires_at: '2026-09-04T19:12:59.177Z'
claim_controller: wf-build/case-040
lease_id: a164d9b0-11ce-4bc5-a341-dd51e48867b5
lease_revision: 1
lease_workspace: 'worktree:c:\users\pc\documents\github\pegasus\.worktrees\case-040'
lease_provider: codex
lease_phase: implementing
lease_heartbeat_at: '2026-09-04T18:42:59.177Z'
labels:
  - case
  - sign-off
  - eva
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links: []
blocks:
  - UIIMP-014
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-07-eva-and-external-engineering-handoff.md
  - docs/frd/frd-04-parties-accounts-and-access.md
prs:
  - '666'
archived: false
created: '2026-09-02T20:31:38.755Z'
updated: '2026-09-04T20:27:33.931Z'
---

## What

A Sign-off Engineer field beside Engineer (ribbon, Overview, Current position); default to the assigned Engineer when flagged, otherwise A Patterson; the Send to EVA dialog carries Engineer, Sign-off Engineer, Download ZIP or Send via API and is offered in Review and With Engineer; "Download EVA package" is retired.

## Why

D31, D36. Mockup source: `Pegasus_UI_v2_src/src/05-state.js` (`defaultSignoff`), `20-case.js` (`case-eva` dialog).

## Approach

- Reuse the CASE-012 EVA handoff dialog and `Eva/Send.*`; the flag comes from the account setting ticket.

## Verification

- [ ] Default rule holds for a flagged and an unflagged Engineer.
- [ ] Re-send from With Engineer records a new handoff without changing state.

## Outcome
