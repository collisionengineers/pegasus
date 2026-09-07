---
id: INTK-062
type: ticket
title: Bound public upload bodies before multipart buffering
status: implementing
area: intake-processing
assignee: principal_delivery_audit
profile: fix
stageEntered:
  preparing: '2026-09-07T21:24:37.487Z'
taken_at: '2026-09-07T21:30:17.097Z'
branch: INTK-062-public-upload-bound
worktree: .worktrees/intk-062
claim_expires_at: '2026-09-07T22:06:07.252Z'
claim_controller: principal_delivery_audit
lease_id: d0b1cbfa-ae1b-4bd3-ad4d-4ff5fa94c265
lease_revision: 2
lease_controller_run: 20260907T200500Z-v1-remediation
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\intk-062'
lease_phase: implementing
lease_heartbeat_at: '2026-09-07T21:36:07.252Z'
labels: []
groups:
  - EPIC-014
links:
  - INTK-055
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-09-07T20:01:20.291Z'
updated: '2026-09-07T21:36:07.252Z'
---

## What

Correct PR675 review finding 2: apply the configured public-link file/submission transport limit to anonymous request bodies before antiforgery and multipart model binding buffer them. Preserve supported multipart overhead, reject unknown-token and oversized requests early through the existing public upload route.

## Acceptance

Focused HTTP tests demonstrate early bounded rejection and successful supported uploads. No new request framework, domain data or broad infrastructure. User authorizes v1 remediation, merge and deployment.

## Outcome
