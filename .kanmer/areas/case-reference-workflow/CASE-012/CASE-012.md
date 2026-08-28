---
id: CASE-012
type: ticket
title: Redesign the Case page workspace
status: implementing
area: case-reference-workflow
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-28T11:25:40.733Z'
taken_at: '2026-08-28T15:14:36.607Z'
branch: task/case-012-case-workspace
worktree: ../pegasus-worktrees/case-012-case-workspace
labels:
  - ui
  - case
  - operator-requested
groups:
  - EPIC-008
  - EPIC-011
links: []
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-12-operator-experience.md
prs:
  - '599'
archived: false
created: '2026-08-21T13:19:14.268Z'
updated: '2026-08-28T15:58:53.440Z'
---

## What

Redesign the Case page as part of the administration and workspace redesign programme.

## Why

The Case workspace needs a deliberate new operator experience beyond the completed earlier refresh [[CASE-007]].

## Approach

- Research the current page and governing requirements.
- Capture backend or process gaps as linked follow-up tickets when discovered.

## Verification

- [ ] The approved redesign meets the resulting acceptance criteria and does not regress Case workflows.

## Outcome

## Inherited scope from [[PLAT-015]]

The Case workspace redesign owns the audited Case and Assessment presentation defects:

- Replace task assignee and Engineer GUID inputs or displays with named staff selectors and business-readable names, reusing the existing staff-account query and display-name convention.
- Replace retained report-Sent evidence IDs, Graph handles, hashes, and typed SHA inputs with the mailbox address, relevant times, and a verified evidence statement; keep transport handles internal.
- Remove inactive vehicle/history/query, Audatex/Glass's, estimate-tab, and assessment-form controls until their capabilities are genuinely composed.
- Remove the `_CaseWorkflow` lifecycle/version narration and the Assessment “Most of the report is written for you” explanatory card.

Verification for this inherited scope: the Case and Assessment surfaces expose no raw identifiers, transport handles, hashes, dead controls, or explanatory narration.
