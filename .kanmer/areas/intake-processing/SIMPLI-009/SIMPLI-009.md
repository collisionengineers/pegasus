---
id: SIMPLI-009
type: ticket
title: Make Worker the sole processor for queued intake
status: done
area: intake-processing
order: 310
assignee: claude-code
profile: feature
stageEntered:
  review: '2026-08-17T10:19:40.488Z'
  verifying: '2026-08-17T11:16:20.475Z'
  done: '2026-08-17T11:54:59.805Z'
labels: []
groups:
  - EPIC-002
  - HZN-003
links: []
blocks: []
commits:
  - 195154f9
  - e9f27fe7
  - caad05e8
  - 8bf0a3e6
  - fc144848
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/385'
deployment: not-deployed
archived: false
created: '2026-08-13T12:12:48.881Z'
updated: '2026-09-01T14:44:31.934Z'
---

## What

Make the Worker the only component that processes queued intake.

## Why

The current Web and Worker paths compete for ownership, creating permission, durability, and recovery risks.

## Approach

- Stage work as pending and dispatch it through the queue.
- Remove Web inline processing and polling.
- Repair stranded dispatched work and classify unexpected failures explicitly.

## Verification

- [x] Duplicate delivery, crash-after-stage, lease expiry, poison handling, and Web/Worker permission-boundary tests pass — see `proof`.

## Outcome

Shipped in PR #385 (https://github.com/collisionengineers/pegasus/pull/385), merged to `dev` as `fc144848` on 2026-08-17; not deployed. Delivered together with [[SIMPLI-008]] on `task/simpli-009`.

Shipped differently than planned (recorded as plan amendments after review + simplification pass): one fault taxonomy in `ProcessQueuedIntake` (terminal-input codes / transient with `InnerException` unwrap / shared `IsRecoverable` catch-all) instead of three lists; unexpected faults are persisted terminal **then rethrown** to the host rather than returned as an outcome and logged by the Worker; `IntakeWorkFunction` unchanged. "Repair stranded dispatched work" — a read-only production count found 0 unleased `dispatched` rows, so nothing to repair; the lost-message resilience gap is [[INTK-003]].

Follow-ups: [[INTK-001]] (retry-scheduled honesty + auto-associated case link on the status page), [[INTK-002]] (adapter-wide fault naming, Web-composition architecture fact, `IIntakeSubmission` leftover), [[INTK-003]] (stale `dispatched` recovery), [[DELIV-001]] (simplicity rails for AGENTS.md).
