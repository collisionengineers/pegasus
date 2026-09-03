---
id: PLAT-043
type: ticket
title: 'Authorize Triage mutations at the Core boundary, not only at the page'
status: backlog
area: platform-operations
order: 740
assignee: ''
profile: fix
labels:
  - review-finding
  - security
  - triage
links: []
archived: false
created: '2026-08-24T14:11:07.721Z'
updated: '2026-09-03T15:15:28.391Z'
---

## Why

Every Triage mutation request carries `string Actor` and validates only that it
is non-empty (`TriageLifecycleRules.ValidateActorAndOperation`). None of
`CreateTriageFromIntake`, `AssignTriage`, `UnassignTriage`, `RecordFinding`,
`SupersedeFinding` or `ChangeState` calls `StaffAuthorization.Require`.

`LinkTriageCase` is the one exception — it takes an `ActionActor` and requires
`PerformCasework` (`TriageLifecycle.cs:413-415`), which is the shape
`docs/frd/frd-04-parties-accounts-and-access.md:25` asks for.

So a non-Web caller of any of the others can act under an arbitrary claimed
subject string. Today the only callers are the Web pages (which do authorize at
the Razor handler) and the intake worker (system actor), so nothing live is
exposed — this is the missing half of the dual boundary, not an open door.

## Not an INTK-035 regression

Raised by automated review on [[INTK-035]] (PR #533), which added a staff-facing
caller for `CreateTriageFromIntake` and authorizes it at the handler, with the
gap named in a comment there. Fixing that one use case alone would make it the
only Triage mutation shaped differently from its five siblings, so the surface
is worth doing together or not at all.

## What to do

Carry `ActionActor` through the Triage mutation requests as `LinkTriageCase`
already does, and require the right in Core. The system-worker path in
`DurableIntake` needs a system actor rather than the current `SystemActor`
string, so check whether `ActionActor.SystemWorker` satisfies the chosen rule
before changing the contract.

## Verify

A direct Core call with a claimed-but-unauthorized actor is refused for every
Triage mutation, and the intake worker still creates Triages unchanged.
