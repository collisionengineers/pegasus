---
id: CASE-024
type: ticket
title: >-
  Hold the case edit lease indefinitely while editing, and give Assessment its
  own edit mode
status: verifying
area: case-reference-workflow
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-28T05:09:24.672Z'
  review: '2026-08-28T07:05:45.536Z'
  verifying: '2026-08-28T10:52:02.387Z'
taken_at: '2026-08-28T05:11:57.614Z'
branch: task/case-024-edit-lease-heartbeat
worktree: 'C:/Users/Alex/Documents/GitHub/pegasus-worktrees/case-024-edit-lease-heartbeat'
labels:
  - CASE-27
  - lease
  - concurrency
links:
  - KANMER-005
blocks:
  - KANMER-005
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-02-intake-and-source-identity.md
commits:
  - 76be6c9c
  - 940d476b
  - 35a13141
  - 3fee076b
  - bd08df8a
  - 20a58bb4
  - 747ecc47
prs:
  - '581'
archived: false
created: '2026-08-28T05:09:12.450Z'
updated: '2026-08-28T10:52:02.387Z'
---

## What

Two changes to the one server-owned case edit lease (CASE-27).

1. **Editing holds the lease indefinitely.** A client heartbeat renews the
   existing five-minute lease every 60 seconds, so an operator is never timed
   out mid-edit. The duration constant does not move; the heartbeat is purely
   additive.
2. **Assessment holds the lease too.** `Pages/Cases/Assessment/` gets an
   explicit Edit / Finish editing mode claiming the same single case lease,
   replacing the four inline self-claims at `Index.cshtml.cs:216,409,442,535`.

## Why

An operator loses edit mode while still typing, because nothing renews the
lease automatically and five minutes is shorter than a real editing session.
Separately, an engineer working an assessment appears *unlocked* to every other
member of staff, and their save fails outright if anyone else is in edit mode.

## Settled before starting

- **The lease already ends on save.** Every mutation clears it inside the same
  transaction (`CaseMutationGuard.Complete`). The "ends within 60 seconds of a
  save" requirement is met at 0 seconds; **no save path changes**, and a
  regression test pins it, including that a heartbeat cannot resurrect a lease
  a save just cleared.
- **The five-minute duration has no documentary authority** — no PRD, FRD, ADR,
  operator note or reference document states it. It is kept anyway, because
  keeping it means no expiry arithmetic and no FRD expiry sentence changes.
- **Automatic mail association stops yielding to the lease.**
  `EfIntakeMutationStore.cs:107-112` guards a case write that never happens —
  that path writes receipt-side rows only, never touches `caseWorkflow.Version`,
  and records `ExpectedCaseVersion = null`. FRD-01:89 already places these
  records outside editable case state. The image-intake path at `:510` keeps its
  check, because that one really does mutate the case.

## Needs operator sign-off before merge

- The operator copy deletions (every current sentence names an expiry time that
  a heartbeat-held lease makes wrong).
- Widening the recorded UI-15 exception at `docs/design/README.md:896-910` by
  adding an edit-mode control to the assessment surface.

Related: [[KANMER-005]] — lease exclusivity between staff and Automation
Actors is a separate open defect and is not fixed here.
