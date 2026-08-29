---
id: PLAT-056
type: ticket
title: >-
  External-work state vocabulary: fold the remaining ten Infrastructure files
  onto ExternalWorkStatePersistence
status: backlog
area: platform-operations
assignee: ''
profile: fix
labels:
  - backend
  - simplification
groups:
  - EPIC-011
links:
  - PLAT-053
archived: false
created: '2026-08-29T08:03:26.586Z'
updated: '2026-08-29T08:03:26.586Z'
---

## What

[[PLAT-053]] gave the persisted `ExternalWorkItems.State` vocabulary
(`pending`, `dispatching`, `queued`, `processing`, `completed`, `failed`) one
internal owner — `Persistence/ExternalWorkStatePersistence.cs` — but migrated
only the three files its own "Owns" list named. The same six words are still
spelled as string literals against the same `context.ExternalWorkItems` table
in ten further Infrastructure classes. Fold them all onto the existing owner.

## Files still carrying the literals

Named in the PLAT-053 review:

- `EfVehicleLookupWorkStore.cs` — lines 49, 53, 57, 145, and
  `MapWorkState` (~line 422)
- `EfAutomaticEvaSubmissionStore.cs` — line 95
- `EfQueuedCustodyProcessor.cs` — lines 43, 1049, 1076
- `EfOperationsStore.cs` — lines 287, 460
- `EfCaseWorkflowStore.cs` — lines 1178-1184

Missed by the PLAT-053 report and added here (all
`context.ExternalWorkItems.Add(new { State = "pending" })`):

- `EfCaseAcceptanceStore.cs` — line 391
- `EfImageIntakeStore.cs` — lines 212, 632
- `EfLinkedCaseReplacementStore.cs` — line 214
- `EfVehicleWorkflowStore.cs` — lines 134, 903

## The parse rule

`EfVehicleLookupWorkStore.MapWorkState` is the pre-existing owner of the
string-to-enum parse rule (persisted word + `AttemptCount > 0` →
`RetryScheduled`). PLAT-053 briefly added a second copy of that rule against
`EvaSubmissionWorkState` and removed it again on review, because the two map
to different Core enums and folding them needs a shape this ticket should
choose deliberately. Decide here whether one generic parse helper is earned or
whether `MapWorkState` stays the single per-enum copy — do not add a third.

## Why it was not done in PLAT-053

PLAT-053's "Owns" list named exactly three files, and EPIC-011's contract is
one ticket per whole file with no cross-lane edits. Ten more Infrastructure
files spanning custody, image intake, vehicle workflow and operations reach
into several live wave-3 lanes.

Raised in the PLAT-053 adversarial verification (2026-08-29).
