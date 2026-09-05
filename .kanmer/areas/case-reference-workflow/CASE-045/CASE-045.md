---
id: CASE-045
type: ticket
title: Show an optional known principal on image-initiated cases
status: review
area: case-reference-workflow
assignee: wf-build/case-045
profile: feature
stageEntered:
  preparing: '2026-09-04T13:15:19.801Z'
  review: '2026-09-05T09:39:55.441Z'
taken_at: '2026-09-05T08:16:20.852Z'
branch: task/case-045-image-initiated-principal
worktree: .worktrees/case-045
claim_expires_at: '2026-09-05T08:46:20.852Z'
claim_controller: wf-build/case-045
lease_id: a928f4bf-cac4-4d7e-a2f2-4b37b0509d44
lease_revision: 1
lease_workspace: 'worktree:c:\users\pc\documents\github\pegasus\.worktrees\case-045'
lease_provider: codex
lease_phase: implementing
lease_heartbeat_at: '2026-09-05T08:16:20.852Z'
labels:
  - image-intake
  - principal
  - ui
groups:
  - EPIC-011
  - EPIC-012
links:
  - CASE-042
  - CASE-032
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-12-operator-experience.md
prs:
  - '671'
archived: false
created: '2026-09-04T10:21:48.548Z'
updated: '2026-09-05T09:39:55.441Z'
---

## What

Give an image-initiated record an optional principal and show it wherever the record is drawn: the Awaiting instruction row and quick view on `/Cases` ([[CASE-042]]) and the image-initiated detail page. A principal will often not be known when image material arrives; when none is recorded the field shows the exact label `Not known` (D51 — an operator-directed exception to the absent-not-drawn rule, for this field only).

Do not require a principal to create or retain an image-initiated case, and never infer or fabricate one (no sender-address or registration matching).

## Why

Operator answer of 2026-09-04 (D51): there must be the possibility of knowing which principal an image-initiated record belongs to, it will not always be known, and it must be displayed either way so the operator can see the state.

## Approach

- One nullable `PrincipalId` on the image-initiated record (`ImageIntakeEntity`), with its migration, grants and bootstrap census in the same diff; projected through `ImageIntakeSummary` inside the existing queue reads (no N+1) and the detail projection.
- Writers: staff set it on the image-initiated detail page from the active principals list (default `Not known`); an intake route that already knows the principal because it is principal-authenticated records it at registration if such a route exists today (research says which; none is built).
- Extend the shapes [[CASE-032]] (row projection, PR #659) and [[CASE-042]] (Awaiting tab and quick view) land; this ticket merges after both and is written as a delta on them. Labels in `OperatorLabels` only.

## Verification

- [ ] An image-initiated record with a recorded principal shows it on the Awaiting row, the quick view and the detail page.
- [ ] A record without one remains valid and shows `Not known` in the same places; nothing is inferred.
- [ ] Staff can set the principal on the detail page; the queue read count is unchanged.
- [ ] Migration, grants and census ship together; `Test-MigrationGrants.ps1` passes.
- [ ] No new principal-matching or case-creation rule is introduced.

## Outcome
