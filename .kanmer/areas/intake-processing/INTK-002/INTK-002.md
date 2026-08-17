---
id: INTK-002
type: ticket
title: >-
  Name intake faults adapter-wide and assert Web composes no queue client or
  processor
status: backlog
area: intake-processing
assignee: ''
profile: chore
labels: []
groups:
  - EPIC-002
links:
  - SIMPLI-009
archived: false
created: '2026-08-17T11:10:37.292Z'
updated: '2026-08-17T11:10:37.292Z'
---

## What

Carry-forward from the PR #385 simplification pass on [[SIMPLI-009]]:

- Adapter-wide fault naming: `AzureBlobIntakeArtifactStore` translates only its read/upload paths; `FileSystemIntakeArtifactStore` throws raw `IOException`; EF stores surface raw SQL faults; `EfIntakeReceiptStore.StoreAsync` throws a bare `InvalidOperationException` after three consecutive deadlocks (terminal under the new taxonomy). Adapters should name faults (`IntakeDependencyUnavailableException` / a named concurrency conflict) so `ProcessQueuedIntake.IsTransientProcessingFailure` matches intake types only.
- `DependencyDirectionTests`: a fact that `Pegasus.Web` composes no queue client and cannot resolve `ProcessQueuedIntake` (today asserted only inside `QdosIntakeWebTests`).
- `IIntakeSubmission` has one implementation (`ReceiveIntake`) and two Web callers — fold the callers onto `ReceiveIntake` and delete the interface and its registration unless a test double is genuinely wanted.

## Why

One place per taxonomy; adapters name faults, Core matches its own types; invariants asserted where architecture tests live; no leftover abstraction.

## Verification

- [ ] `IsTransientProcessingFailure` lists no BCL exception types once every adapter in the processor's path names its faults (or the plan records why one remains).
- [ ] Architecture test fails if Web registers a queue client or the processor.
- [ ] `IIntakeSubmission` removed or its second concrete need recorded.

## Outcome
