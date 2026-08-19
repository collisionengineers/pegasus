---
id: SIMPLI-011
type: ticket
title: Decompose the Case Details workspace by capability
status: done
area: case-reference-workflow
order: 150
assignee: claude-code
profile: feature
stageEntered:
  review: '2026-08-17T14:36:44.174Z'
  verifying: '2026-08-17T15:48:35.085Z'
  done: '2026-08-17T15:54:09.367Z'
labels: []
groups:
  - EPIC-002
links:
  - PLAT-002
  - CASE-001
blocks: []
commits:
  - 919faed1
  - 8d90490a
  - 9feca869
  - a30e3a13
  - ec0c2220
  - b763157a
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/395'
deployment: not-deployed
archived: false
created: '2026-08-13T12:12:48.922Z'
updated: '2026-08-19T09:39:14.697Z'
---

## What

Keep one Case workspace while moving mutations into capability-specific Razor endpoints.

## Why

The existing page has excessive handlers and dependencies, making safe changes difficult.

## Approach

- Separate workflow, tasks, custody, vehicle/EVA, and closure operations.
- Keep DetailsModel focused on loading and displaying the workspace.

## Verification

- [x] The visible workspace remains intact and extracted operations are covered by behavioural tests.

## Outcome

Shipped in PR #395 (merged to `dev` as `b763157a`, 2026-08-17). `Details.cshtml.cs` 1938 → ~630 lines / 10 dependencies, keeping the workspace (query, edit lease, completeness, save) on a new shared `CaseMutationPageModel`; 28 handlers moved verbatim onto `Cases/Workflow`, `Tasks`, `Custody`, `Vehicle`, `Closure` and `Eva/Download`; the partials post to the owning page; the visible workspace is unchanged. Every capability-page handler plus the workspace's renew/leave now has a behavioural test (six page tests + one edit-mode test on a shared harness). Shipped beyond plan: `Documents/Export` adopted the base (its lease-state encoding had drifted). Simplification pass: 15 applied / 12 skipped-or-deferred (recorded in `plan`). Follow-ups: [[PLAT-002]] (one staff-actor root for the Web page bases), [[CASE-001]] (unread `CaseDetailsStatus` TempData). Not deployed (Web-only refactor; ships with the next release from `dev`).
