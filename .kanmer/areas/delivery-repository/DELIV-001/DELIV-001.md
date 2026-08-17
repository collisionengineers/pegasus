---
id: DELIV-001
type: ticket
title: >-
  Add simplicity rails to AGENTS.md and a simplification pass to the task
  workflow
status: review
area: delivery-repository
assignee: claude-code
profile: chore
stageEntered:
  implementing: '2026-08-17T12:55:15.719Z'
  review: '2026-08-17T12:56:45.634Z'
taken_at: '2026-08-17T12:55:00.605Z'
branch: task/deliv-001-simplicity-rails
worktree: ../pegasus-worktrees/deliv-001-simplicity-rails
labels: []
groups:
  - EPIC-002
links: []
commits:
  - fde7cebe
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/390'
archived: false
created: '2026-08-17T11:05:47.371Z'
updated: '2026-08-17T12:56:45.634Z'
---

## What

Amend `AGENTS.md` (repository rule/process owner) so over-engineering is caught by the workflow rather than by review: a short **Simplicity rails** section, a **simplification pass** step before every PR (`/simplify` four lenses + `code-simplifier`, findings recorded in the ticket plan), one reviewer sentence, and one plan-hygiene sentence. Move the mechanics into a new `docs/engineering.md#simplicity` section (fault-handling shape, test-support shape, plan sizing, the four lenses).

## Why

The 2026-08-17 simplification pass over PR #385 ([[SIMPLI-009]]/[[SIMPLI-008]]) found, in one 29-file diff: a result record invented to smuggle an exception past a design constraint; three parallel exception-type lists in one class; a second copy of the persisted-state string table in another layer; a new `TempData` convention beside the existing `?duplicate=` route-value one; three copies of one test fake and two of one drain loop. `docs/engineering.md` already forbids most of this — the rules are not in the file every agent reads first, and no workflow step forces the check. Separately, [[SIMPLI-010]]'s plan was 13 steps for a ~50-line change and argued a production-data premise instead of running a read-only query: process over-engineering the same rails should catch.

## Approach

- The full proposed text (sections A–F: AGENTS.md inserts, engineering.md section, skill follow-through) is in this ticket's `scratch-proposal`.
- Docs-only task: no root plan needed; PR to `dev`; independent review of the diff for unauthorised scope.
- Do not restate mechanics in AGENTS.md — one line per rule plus a pointer to `docs/engineering.md#simplicity`.

## Verification

- [ ] AGENTS.md carries the Simplicity rails section and the amended workflow steps 3–5; `docs/engineering.md` carries `## Simplicity`; `scripts/Test-DocumentationLinks.ps1` passes.
- [ ] The next SIMPLI ticket to open a PR (e.g. [[SIMPLI-010]]) records a "Simplification pass" heading in its plan before the PR opens.

## Outcome
