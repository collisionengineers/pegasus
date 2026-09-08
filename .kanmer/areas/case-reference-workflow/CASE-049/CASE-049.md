---
id: CASE-049
type: ticket
title: Make native engineer handoff the review action without an EVA prerequisite
status: done
area: case-reference-workflow
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-07T23:13:19.040Z'
  review: '2026-09-08T01:12:38.095Z'
  verifying: '2026-09-08T01:19:58.256Z'
  done: '2026-09-08T01:27:22.294Z'
taken_at: '2026-09-08T00:45:33.045Z'
branch: CASE-049-native-handoff
worktree: .worktrees/case-049
claim_expires_at: '2026-09-08T01:51:51.024Z'
claim_controller: codex-mcp-client
lease_id: e470faea-e8e9-4b06-bea3-408641c00770
lease_revision: 5
lease_controller_run: 20260907T231141Z-native-handoff
lease_worker_run: pack_reconcile-case049
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\case-049'
lease_phase: verifying
lease_heartbeat_at: '2026-09-08T01:21:51.024Z'
labels: []
groups:
  - EPIC-014
links:
  - CASE-040
  - ENG-034
  - CASE-047
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/frd/frd-12-operator-experience.md
commits:
  - 24eb2f77276fd7eb847f8c1746e6909113866b58
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/690'
archived: false
created: '2026-09-07T23:11:41.757Z'
updated: '2026-09-08T01:27:22.294Z'
---

## What

Handing a ready Case to an Engineer is the staff review action. It enters With Engineer in one guarded operation and opens native engineering/Glass's work without an EVA export.

## Why

The operator explicitly requires this workflow in the current v1 remediation. Existing assignment only changes the Engineer field; a separate Start report preparation action or optional EVA export currently performs progression. Native assessment queries still require an EVA export even though current reports and operator authority make EVA optional. This residual defect is not delivered by the historical [[CASE-040]], [[ENG-034]] or [[CASE-047]] PRs; their existing claims and evidence remain untouched.

## Approach

Reuse the current assignment command, persisted readiness/lease/version guards, transaction/history and Case action bar. Remove superseded native export gating and two-step handoff behavior; retain genuine optional EVA delivery evidence and existing terminal/read-only restrictions. Align affected canonical behavior docs and focused caller tests.

## Verification

- [ ] Ready Case handoff assigns the eligible Engineer and advances exactly once under the existing lease/version authority.
- [ ] Incomplete, stale, unauthorized and replay requests preserve correct state/history.
- [ ] Native estimation and reports work without an EVA export; unrelated EVA delivery evidence remains correct.
- [ ] One visible native handoff action replaces mandatory two-step progression; current docs and scoped snapshots agree.

## Outcome
