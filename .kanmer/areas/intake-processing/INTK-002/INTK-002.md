---
id: INTK-002
type: ticket
title: Name intake adapter faults and assert the Web composition boundary
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
updated: '2026-08-25T06:40:23.157Z'
---

## What

Carry forward two focused intake simplification chores from [[SIMPLI-009]] and [[SIMPLI-010]]:

1. Name adapter faults across the queued-intake path so Core transient-failure policy matches intake-owned exception types rather than raw BCL or provider exceptions.
2. Add an architecture assertion that Pegasus.Web composes neither a queue client nor `ProcessQueuedIntake`.

## Why

`AzureBlobIntakeArtifactStore` names only some failures, while the file-system and EF paths can surface raw I/O, SQL, or terminal deadlock exceptions. Core should classify supported intake faults, not guess over provider exceptions. The Web composition boundary is currently asserted only inside a feature integration test and belongs in the architecture tests.

`IIntakeSubmission` is deliberately not part of this ticket: it has real Web callers, so its existence alone is not evidence of an obsolete abstraction. The decision-code and operator-label work is owned by [[INTK-004]].

## Verification

- [ ] Every supported adapter failure reaching queued-intake retry policy is translated to a named intake fault, or a concrete exception is documented.
- [ ] `IsTransientProcessingFailure` contains no raw BCL/provider exception taxonomy once the adapters own translation.
- [ ] The dependency-direction suite fails if Web registers a queue client or resolves `ProcessQueuedIntake`.
- [ ] No decision-code table, label mapping, or `IIntakeSubmission` removal is implemented under this ticket.

## Outcome
