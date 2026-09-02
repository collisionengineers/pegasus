---
id: UIIMP-012
type: ticket
title: >-
  Rename the Triage history panel to "Notes" and narrow D7 to uncomposed
  integrations
status: implementing
area: ui-improvement
assignee: claude-code/20260901T215000Z-claude-controller/implementer-a1
profile: chore
stageEntered:
  preparing: '2026-09-02T03:09:03.495Z'
taken_at: '2026-09-02T03:21:10.906Z'
branch: task/uiimp-012-triage-notes
worktree: ../pegasus-worktrees/uiimp-012-triage-notes
claim_expires_at: '2026-09-02T03:51:10.906Z'
claim_controller: claude-code/20260901T215000Z-claude-controller/implementer-a1
lease_id: ce05a2bf-9694-4517-95d6-ee98d710f878
lease_revision: 1
lease_workspace: >-
  worktree:c:\users\pguser\documents\github\pegasus-worktrees\uiimp-012-triage-notes
lease_phase: implementing
lease_heartbeat_at: '2026-09-02T03:21:10.906Z'
labels:
  - ui
  - epic-contract
  - operator-decision
  - triage
  - wave-A
groups:
  - EPIC-011
links:
  - INTK-046
refs:
  - docs/frd/frd-03-triage.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-29T08:29:54.971Z'
updated: '2026-09-02T03:21:10.906Z'
---

## What

Apply the operator's two rulings of 2026-09-01 on the EPIC-011 contract:

1. The Triage history panel is named **Notes**, as `context.md` §1.5 draws it. Rename the heading in `src/Pegasus.Web/Pages/Triage/Details.cshtml` (currently `Permanent history`, `id="history-title"`) and change the pinned assertion in `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs` (the `Assert.Contains("Permanent history", …)` near line 477) in the same diff. Entries keep their shape (Date, Time, ID, text) and remain permanent; [[INTK-054]] adds the append-only staff notes into this panel afterwards (D25).
2. D7's second clause is narrowed to uncomposed integrations: a disabled control is permitted for a named, ticketed integration seam, and the merged `.gated` state/permission convention (Open Assessment, Import estimate, Triage completion) stays legal. This is a `context.md` edit made by the Phase 0 controller; no code changes.

## Why

Raised out of [[INTK-046]] round-2 remediation: two clauses of `context.md` were contradicted by merged `dev` code and its own pinned assertions. The operator ruled on 2026-09-01: contract wins for the panel name (rename the code), and D7 is narrowed. Recorded in `context.md` §2 (D7 amended, D21 added) and the EPIC-011 decisions record.

## Approach

- One small PR to `dev` from `task/uiimp-012-notes-panel`: the heading and the assertion, nothing else; whole-file ownership of `Pages/Triage/Details.cshtml` for the duration (build wave A, before [[INTK-054]]).
- Regenerate and commit `docs/design/test-ui/` for the changed routed page (`triage-details--default.html`) under the catalogue lock.

## Verification

- [ ] The Triage page renders the panel heading `Notes` and `QdosTriageIntegrationTests` is green with the updated assertion.
- [ ] `context.md` §1.5 and D7 agree with the shipped markup and the merged `.gated` convention.
- [ ] Test-UI snapshot for the Triage detail page regenerated and verified.

## Outcome
