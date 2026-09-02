---
id: DELIV-001
type: ticket
title: >-
  Add simplicity rails to AGENTS.md and a simplification pass to the task
  workflow
status: done
area: delivery-repository
order: 380
assignee: claude-code
profile: chore
stageEntered:
  implementing: '2026-08-17T12:55:15.719Z'
  review: '2026-08-17T12:56:45.634Z'
  verifying: '2026-08-17T13:13:58.358Z'
  done: '2026-08-17T13:14:42.461Z'
labels: []
groups:
  - EPIC-002
links: []
commits:
  - fde7cebe
  - dbbf3214
  - 7bb184cb
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/390'
deployment: n/a
archived: false
created: '2026-08-17T11:05:47.371Z'
updated: '2026-09-01T14:44:32.007Z'
---

## What

Amend `AGENTS.md` (repository rule/process owner) so over-engineering is caught by the workflow rather than by review: a short **Simplicity rails** section, a **simplification pass** step before every code PR (`/simplify` four lenses + `code-simplifier`, findings recorded in the ticket plan), one reviewer sentence, and one plan-hygiene sentence. Move the mechanics into a new `docs/engineering.md#simplicity` section (the four lenses, skip rules, balance, scope/timing, fault-handling shape, test-support shape, plan sizing).

## Why

The 2026-08-17 simplification pass over PR #385 ([[SIMPLI-009]]/[[SIMPLI-008]]) found, in one 29-file diff: a result record invented to smuggle an exception past a design constraint; three parallel exception-type lists in one class; a second copy of the persisted-state string table in another layer; a new `TempData` convention beside the existing `?duplicate=` route-value one; three copies of one test fake and two of one drain loop. `docs/engineering.md` already forbade most of this — the rules were not in the file every agent reads first, and no workflow step forced the check. Separately, [[SIMPLI-010]]'s plan was 13 steps for a ~50-line change and argued a production-data premise instead of running a read-only query.

## Approach

- Proposal text (A–F + addendum with `[skill]`/`[agent]` provenance) on `scratch-proposal`.
- Docs-only; PR to `dev`; independent docs-only review.

## Verification

- [x] AGENTS.md carries the Simplicity rails and amended workflow steps 3–5; `docs/engineering.md` carries `## Simplicity`; link check passes — see `proof`.
- [x] Code PRs record a "Simplification pass" heading before opening — already true for SIMPLI-010 (#387) and SIMPLI-007 (#388).

## Outcome

Shipped in PR #390 (https://github.com/collisionengineers/pegasus/pull/390), merged to `dev` as `7bb184cb` on 2026-08-17; docs-only (deployment n/a). Review caught one real issue — the abstraction rail had dropped engineering.md's "or an accepted ADR" clause and would have tightened policy — fixed before merge, along with de-duplicating the rails that restated mechanics (now one line + anchor) and an explicit docs-only exemption for the pass. Skill-side follow-through (Kanmer's `kanmer-execute`/`-plan`/`-review` prompts) is not repository-owned; AGENTS.md carries the requirement.
