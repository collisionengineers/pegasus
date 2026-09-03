---
id: PLAT-070
type: ticket
title: >-
  Remove the staff review requirement flags and the Workflow configuration
  review panel (D44); record D45 no damage type
status: review
area: platform-operations
assignee: wf-build/plat-070
profile: fix
stageEntered:
  preparing: '2026-09-03T08:06:45.998Z'
  review: '2026-09-03T16:17:56.104Z'
taken_at: '2026-09-03T13:04:09.384Z'
branch: task/plat-070-remove-review-flags
worktree: .worktrees/plat-070
claim_expires_at: '2026-09-03T15:59:07.343Z'
claim_controller: wf-build/plat-070
lease_id: 42d2b76d-05ab-4e03-8e6b-2119c79cd74a
lease_revision: 3
lease_workspace: 'worktree:c:\users\pc\documents\github\pegasus\.worktrees\plat-070'
lease_phase: implementing
lease_heartbeat_at: '2026-09-03T15:29:07.343Z'
lease_reclaimed_from: wf-build/plat-070
labels:
  - case-workspace-v2
  - d44
  - d45
groups:
  - EPIC-012
  - EPIC-011
links:
  - PLAT-072
  - PLAT-062
blocks:
  - CASE-038
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
  - docs/design/README.md
prs:
  - '649'
archived: false
created: '2026-09-03T07:47:48.081Z'
updated: '2026-09-03T18:19:43.145Z'
---

## What

D44 (operator, 2026-09-03): "Review" is a stage, not an action; pressing Send to EVA is the implicit review. Remove the staff review function: `CaseWorkflowConfiguration.RequireStaffImageReviewBeforeEngineerAssignment` and its clause in `src/Pegasus.Core/Lifecycle/CaseLifecycle.cs`, the `ImagesReviewedByStaff` evidence value in `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs`, the hidden fields in `Pages/Cases/Shared/_ReadinessHiddenFields.cshtml`, the "Images staff-reviewed" label and field handling in `Pages/Cases/Details.cshtml(.cs)`, `_CaseWorkflow.cshtml`, `CaseMutationPageModel.cs`, `Closure.cshtml.cs`, `Workflow.cshtml.cs`, and the "Staff review requirements" panel of Administration → Workflow configuration (and its test-ui snapshot). Not ready → Review is decided by completeness only; Review → With Engineer happens through Send to EVA.

D45 (operator, 2026-09-03): every case is collision work, so a damage zone records zone, severity and note only — no damage type. D39 is amended accordingly.

## Why

The mockup and the shipped configuration both carried a staff "review instructions/images" act that does not exist in the business. Deleting it (greenfield rule 6, no deprecation path) keeps one readiness rule. D45 narrows ENG-035 and ENG-036 before they build.

## Approach

- Delete, do not deprecate; migration only if the configuration is persisted (then grants ride the same diff and `scripts/Test-MigrationGrants.ps1` runs).
- Record D44 and D45 in frd-01 (readiness), frd-12 (Case record, Workflow configuration), frd-06 (damage record), `docs/design/README.md` (Workflow configuration panel list; damage-diagram row), and EPIC-012/EPIC-011 `context.md` (board docs) in the same PR.
- Wave 1, serial before [[CASE-038]] (shared lock on `Pages/Cases/Details.cshtml`). Implementer gpt-5.6-sol medium under a Sonnet wrapper.

## Verification

- [ ] `git grep -i "ReviewedByStaff\|RequireStaffImageReview\|staff-reviewed"` returns nothing on the branch.
- [ ] Workflow configuration page renders no review panel; snapshots regenerated and verified; `Test-UiCatalogue.ps1` passes.
- [ ] Core lifecycle tests updated: a case with complete instruction and images reaches Review with no review flag.
- [ ] D44 and D45 grep in frd-01, frd-06, frd-12, design README and both group context docs.

## Outcome
