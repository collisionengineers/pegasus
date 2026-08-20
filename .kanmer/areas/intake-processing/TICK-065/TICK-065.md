---
id: TICK-065
type: ticket
title: >-
  INT-32 — Instruction/image halves retain separate age and chase state;
  definitive pairing notifies staff that the job is ready
status: verifying
area: intake-processing
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-20T03:49:51.314Z'
  review: '2026-08-20T05:56:09.385Z'
  implementing: '2026-08-20T06:11:40.868Z'
  verifying: '2026-08-20T06:11:52.493Z'
taken_at: '2026-08-20T05:46:41.267Z'
branch: task/tick-065-int-32-completion
worktree: ../pegasus-worktrees/tick-065
labels:
  - capability
  - INT-32
  - now
groups:
  - HZN-003
links: []
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-08-12T15:05:19.575Z'
updated: '2026-08-20T06:11:52.493Z'
---

## What

Plan and research **INT-32**: Instruction/image halves retain separate age and chase state; definitive pairing notifies staff that the job is ready

## Why

This is allocated to **Now / 0.1.0-alpha.1** in `docs/capabilities.md`. It is a current allocated outcome with remaining caller/evidence work.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — INT-32.

## Verification status (2026-08-20, PROOFS-lane audit — see research.md)

**Partial — prepared, remainder open. Do not close this ticket.**

- **Shipped (release 12, present at production SHA `2325ed4a`):** the derived pairing-visibility half. `ImageInitiatedCaseState` lifecycle (`src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs:31-35`, migration `20260819112914_ImageInitiatedLifecycle.cs`) and the `Associated with Case` / `Image intake registered` label (`src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:209`), derived from the origin receipt so it can never disagree with it.
- **Missing:** the age/chase-state half. `ImageIntakeSummary`/`ImageIntakeRecord` carry only `RegisteredAtUtc` — no derived age, no due-work timer, no chase state for the image side (verified: zero "age"/"chase" hits in `src/Pegasus.Web/Pages/Triage/Index.cshtml.cs`, the Not-ready/image queue page). The case-side chase machinery (`src/Pegasus.Core/Tasks/CaseWorkScheduling.cs`, `RunDueChasers.cs`) has no image-intake counterpart. There is also no active "ready" notification beyond the passive label a staff member sees by revisiting the Cases list.
- **Next step:** an implementation lane should add an image-half due-work projection (modelled on `ICaseDueWorkQueries.GetDueAsync`) and a ready-notification, per the seam in `capability-survey.md` §4. Moved backlog → preparing only; left here for that lane.
