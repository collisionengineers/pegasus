---
id: INTK-062
type: ticket
title: Bound public upload bodies before multipart buffering
status: verifying
area: intake-processing
assignee: principal_delivery_audit
profile: fix
stageEntered:
  preparing: '2026-09-07T21:24:37.487Z'
  review: '2026-09-07T22:44:35.228Z'
  verifying: '2026-09-07T22:49:48.666Z'
taken_at: '2026-09-07T21:30:17.097Z'
branch: INTK-062-public-upload-bound
worktree: .worktrees/intk-062
claim_expires_at: '2026-09-07T23:14:35.321Z'
claim_controller: principal_delivery_audit
lease_id: d0b1cbfa-ae1b-4bd3-ad4d-4ff5fa94c265
lease_revision: 4
lease_controller_run: 20260907T200500Z-v1-remediation
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\intk-062'
lease_phase: review
lease_heartbeat_at: '2026-09-07T22:44:35.321Z'
labels: []
groups:
  - EPIC-014
links:
  - INTK-055
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
commits:
  - abf3657691a230bdc20e81d4744e6634a6d73f85
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/684'
archived: false
created: '2026-09-07T20:01:20.291Z'
updated: '2026-09-07T22:49:48.666Z'
---

## What

Correct PR675 review finding 2: apply the configured public-link file/submission transport limit to anonymous request bodies before antiforgery and multipart model binding buffer them. Preserve supported multipart overhead, reject unknown-token and oversized requests early through the existing public upload route.

## Acceptance

Focused HTTP tests demonstrate early bounded rejection and successful supported uploads. No new request framework, domain data or broad infrastructure. User authorizes v1 remediation, merge and deployment.

## Outcome
