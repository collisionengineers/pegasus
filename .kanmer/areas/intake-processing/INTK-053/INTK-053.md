---
id: INTK-053
type: ticket
title: A swallowed intake bookkeeping conflict is logged nowhere
status: backlog
area: intake-processing
assignee: ''
profile: fix
labels:
  - diagnostics
  - unidentified
links:
  - PR-069
  - INTK-048
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-08-30T00:10:57.940Z'
updated: '2026-08-30T00:10:57.940Z'
---

## What

Give the two swallowed-and-counted failure paths on the Unidentified
reconciliation route enough detail to diagnose a persistent failure.

## Why

Two catches currently discard the cause:

1. **`DurableIntake.cs` around line 913 (`ProcessQueuedIntake`'s advisory
   catch)** swallows its exception set with **no log line at all**.
2. **`ReconcileUnidentifiedDestinations.ExecuteAsync`'s `failures++` catch**
   reaches the Worker log through `LogUnidentifiedDestinationReconciliation`,
   but only at Information level and with **no exception detail**.

So a sweep that is failing every ten seconds looks like
`N candidates, 0 resolved, 0 corrected, N failures` with nothing saying why.
**That is the 2026-08-14 worker-grant-gap diagnostic problem in miniature**: a
missing permission fails only against the deployed estate, because local and CI
runs are full-privilege, and the log gives the operator nothing to act on.

The identical shape exists on the provider-submission sweep and is being fixed
there as part of [[AUTO-012]]'s remediation — this ticket is the Unidentified
half, kept separate because it is not that lane's scope.

## Why it was not fixed in the PR-069 remediation

Recorded honestly by that lane rather than silently skipped:

- The `ProcessQueuedIntake` catch runs in the Worker with no operator to surface
  to, and throwing would turn a bookkeeping conflict into a failed intake pass.
  Its "the sweep is the backstop" claim is now **actually true** — under the old
  receipt-derived operation key it was false, which was the defect PR-069 fixed
  — so the swallow is defensible; the missing log line is not.
- Narrowing the sweep's `failures++` catch to a transient-only filter would
  break the existing assertion `AResolveFailureIsCountedAndNeverStopsTheSweep`,
  which pins that a plain `InvalidOperationException` is counted and does not
  stop the sweep. **Rule 19 says an existing assertion decides that, not the
  lane** — so changing the classification needs its own ticket and its own
  argument, which is this one.

## Approach

Log, do not re-classify. Add the exception type and message to both counted
failures, at a level an operator will see. Do not weaken
`AResolveFailureIsCountedAndNeverStopsTheSweep`; if the classification should
change, argue it here first.

Check whether the same gap exists on the other bounded sweeps sharing
`StagedArtifactReconciliationFunction` before fixing only these two — one list
per concept applies to diagnostics too.

## Verification

- [ ] A permission-denied failure on the sweep names the cause in the log
- [ ] `AResolveFailureIsCountedAndNeverStopsTheSweep` still passes unchanged
- [ ] The advisory catch in `ProcessQueuedIntake` records what it swallowed
