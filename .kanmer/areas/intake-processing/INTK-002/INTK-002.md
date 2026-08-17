---
id: INTK-002
type: ticket
title: >-
  Intake duplication chores: adapter-wide fault naming, one decision-code table,
  Web-composition assertion, leftover port
status: backlog
area: intake-processing
assignee: ''
profile: chore
labels: []
groups:
  - EPIC-002
links:
  - SIMPLI-009
  - SIMPLI-010
archived: false
created: '2026-08-17T11:10:37.292Z'
updated: '2026-08-17T12:00:17.228Z'
---

## What

Carry-forward from the simplification passes on [[SIMPLI-009]] (PR #385) and [[SIMPLI-010]]:

- Adapter-wide fault naming: `AzureBlobIntakeArtifactStore` translates only its read/upload paths; `FileSystemIntakeArtifactStore` throws raw `IOException`; EF stores surface raw SQL faults; `EfIntakeReceiptStore.StoreAsync` throws a bare `InvalidOperationException` after three consecutive deadlocks (terminal under the new taxonomy). Adapters should name faults (`IntakeDependencyUnavailableException` / a named concurrency conflict) so `ProcessQueuedIntake.IsTransientProcessingFailure` matches intake types only.
- One decision-code table: `EfOperationsStore.MapIntakeState` is a second hand-kept copy of `EfIntakeReceiptStore.ParseDecision`'s string set, and `IntakeMcpTools` a third — SIMPLI-010 had to edit two of them to remove one code. Collapse onto `ParseDecision` (same assembly) or a shared code table; note `ParseDecision` throws on unknown codes where `MapIntakeState` returns `Unknown` (and omits `blocked_intake`/`image_intake_registered`), so decide the fail-closed behaviour explicitly.
- `DependencyDirectionTests`: a fact that `Pegasus.Web` composes no queue client and cannot resolve `ProcessQueuedIntake` (today asserted only inside `QdosIntakeWebTests`).
- `IIntakeSubmission` has one implementation (`ReceiveIntake`) and two Web callers — fold the callers onto `ReceiveIntake` and delete the interface and its registration unless a test double is genuinely wanted.

## Why

One place per taxonomy and per code table; adapters name faults, Core matches its own types; invariants asserted where architecture tests live; no leftover abstraction.

## Verification

- [ ] `IsTransientProcessingFailure` lists no BCL exception types once every adapter in the processor's path names its faults (or the plan records why one remains).
- [ ] Exactly one place enumerates persisted intake decision codes; Operations and MCP read through it.
- [ ] Architecture test fails if Web registers a queue client or the processor.
- [ ] `IIntakeSubmission` removed or its second concrete need recorded.

## Outcome
