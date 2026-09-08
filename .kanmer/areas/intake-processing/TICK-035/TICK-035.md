---
id: TICK-035
type: ticket
title: Activate evidenced principal routes through automatic intake
status: implementing
area: intake-processing
order: 910
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-09-07T21:12:54.514Z'
  review: '2026-09-08T01:41:18.011Z'
  implementing: '2026-09-08T01:48:12.223Z'
taken_at: '2026-09-07T22:43:36.690Z'
branch: TICK-035-principal-routes
worktree: .worktrees/tick-035
claim_expires_at: '2026-09-08T02:35:02.848Z'
claim_controller: /root
review_round: 1
lease_id: 2076ecaa-2a2a-4aa7-aa8c-a6a997e38744
lease_revision: 28
lease_controller_run: 20260907T200500Z-v1-remediation
lease_worker_run: intake_audit
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\tick-035'
lease_provider: codex
lease_phase: implementing
lease_heartbeat_at: '2026-09-08T02:05:02.848Z'
labels:
  - capability
  - INT-04
groups:
  - EPIC-014
links: []
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-01-case-identity-and-lifecycle.md
commits:
  - 006b556ff3e995c5a2aac0cdb2a4d508ada5be23
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/692'
archived: false
created: '2026-08-12T15:03:53.493Z'
updated: '2026-09-08T02:05:02.848Z'
---

## What

Activate the evidenced top-15 principal routes through the existing automatic
intake workflow, including direct and proved staff-forwarded email. Reuse the
single Core route/classification/matching boundary and existing fifteen
instruction extraction profiles; do not introduce parallel QDOS and generic
business-policy implementations.

## Why

The operator's 7 September 2026 v1 remediation request and Astra Stream C03's
TICK-035 residual supersede this ticket's historical post-alpha deferral.
Fourteen additional extraction profiles are present but ordinary mail intake
still binds routing and extraction to QDOS. Domain/reference candidates are
evidence, not permission to invent mappings.

## Acceptance

- Exact evidenced domain routes identify one active principal; unknown,
  conflicting, shared or intermediary evidence remains explicit and fail-closed.
- The selected existing extraction profile agrees with the accepted route and
  drives the existing Case/Triage/Unidentified destination logic.
- Direct, forwarded, ambiguous and replay cases use genuine existing fixtures
  and prove the destination, principal and lifecycle state, not just selection.
- Canonical FRD-02 and production callers agree. No second principal catalog,
  rules engine, mailbox onboarding or separate pipeline is introduced.

## Coordination

[[PLAT-028]] owns customer contracts, DI and Settings until its announced
merge. Preparation may proceed; overlapping implementation waits for that base.
[[INTK-061]] owns durable routing recovery; Triage automatic linking and
Engineer handoff are separately assigned remediation. [[TICK-036]],
[[TICK-037]] and [[TICK-038]] retain mailbox onboarding ownership.

Only test email recipient is digital@collisionengineers.co.uk. No live mail,
provider or cloud write is part of this preparation. Root is the sole heavy
verification owner.

## Outcome
