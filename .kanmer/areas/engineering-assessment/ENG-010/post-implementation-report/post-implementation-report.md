# Post-implementation report

**Branch:** `task/qdos26008-regressions` · **PR:** #505 · **Commits:** `df6d3b66`, `e4ce8e3b`

## The ticket was re-scoped by its own evidence step, and that was the point

The ticket said "surface MOT-derived mileage on the case, not only the Assessment
prefill", and required an evidence step first: *"read production and confirm the MOT
tests and the calculated mileage are actually present. If the lookup did not run, that is
a different defect and this ticket re-scopes to it."*

It did re-scope. Production held **zero MOT observations and no recorded failure** for
every vehicle ever looked up — including a 2007 Toyota and a 2018 Mercedes that plainly
have MOT history. A live DVSA call with the production credentials returned HTTP 200 with
five tests for DP07EFB (odometer 113,068 KM, 2026-05-14) and seven for the Mercedes.

There was never any MOT-derived mileage to surface. The gap the ticket described was a
symptom.

## What was built

**`df6d3b66` — the parse defect.** `ParseMot` read `completedDate` with
`DateOnly.TryParse`; the MOT History API writes that field as a full instant
(`2026-05-14T13:11:22.000Z`), which `DateOnly` rejects. Every test failed to parse, every
test was filtered out, and the empty list was indistinguishable from a vehicle with no
history.

- provider dates now parse as a date **or** an instant, applied to `expiryDate` too;
- tests offered but none readable is now a named `dvsa_unreadable_tests` failure rather
  than silence. Treating "no history" and "unreadable history" as the same empty result is
  what kept this invisible for every release.

**`e4ce8e3b` — kilometres.** Operator direction: *"Converting is not a data fidelity call.
I have already specified about converting KM to miles automatically."* `VehicleMileagePolicy`
converts at 1.609344, rounds to the nearest whole mile, compares observations **after**
conversion so one reading in two units does not register as a conflict, and always reports
`Miles`. Method version 1 → 2. Raw observations keep their own units, as FRD-06 requires.

## Why CI never caught it

Every DVSA fixture used a date-only string the real API does not send. The new test
carries the live response verbatim, kilometres and all. That is the actual lesson here: a
fixture that does not match the wire format hides the bug it was written to prevent.

## Departure from the plan

The plan's `CaseDataSnapshotFactory` work was **not** needed and was not done. Once the
tests parse and the derived value is in miles, the existing derived-mileage path carries
it. Adding a second write for a value that already had one would have been the wrong fix.

## Evidence

- Live DVSA call against production credentials — read-only, permitted without approval
- `Pegasus.Core.Tests` — 908 passed
- `ProductionVehicleLookupTests` carries the live response verbatim
- Live: a case showing derived mileage in miles with provenance — Phase 6

## Honest limitation

The live DVSA call proved the API returns the data and that the new parser reads it. It
did **not** prove the value reaches a case's overview, because the Azure test data was
cleared earlier in this session and no case exists to check. That is Phase 6's job and it
is not claimed here.
