# Plan

The ticket was filed as "surface MOT-derived mileage on the case". Investigation found
something worse and re-scoped it: **no case had ever had MOT-derived mileage to surface.**

## Defect 1 — every MOT test in production was being silently discarded

Every vehicle lookup in production had recorded zero MOT observations and no failure,
including a 2007 Toyota and a 2018 Mercedes that plainly have MOT history. A live DVSA
call with the production credentials settled it: HTTP 200, five MOT tests for DP07EFB
(odometer 113,068 KM, 2026-05-14) and seven for the Mercedes. The data was always there.

`ParseMot` read `completedDate` with `DateOnly.TryParse`, and the MOT History API writes
that field as a full instant — `2026-05-14T13:11:22.000Z` — which `DateOnly` rejects.
Every test failed to parse, every test was filtered out, and the empty list looked
exactly like a vehicle with no MOT history.

**Why CI never caught it:** every fixture used a date-only string the real API never
sends. The new test carries the live response verbatim.

Two changes:
- provider dates parse as a date **or** an instant, applied to `expiryDate` too, which is
  date-only today and had no business being the next thing to break;
- reading none of the tests offered is now a `dvsa_unreadable_tests` failure rather than
  silence. A vehicle with no history and a vehicle whose history we cannot read produced
  the same empty result, and treating them as the same thing is what kept this invisible.

## Defect 2 — kilometres passed straight through

DP07EFB's history is recorded in kilometres, as imports generally are. The derived case
mileage carried that unit through, and every consumer that asks for miles — the
Assessment prefill among them — ignored it. Operator direction: *"Converting is not a
data fidelity call. I have already specified about converting KM to miles
automatically."*

`VehicleMileagePolicy` converts at 1.609344, rounds to the nearest whole mile so a
converted value never reads as more precise than the odometer behind it, and always
reports `Miles`. Observations are compared **after** conversion, so one reading recorded
in both units agrees with itself instead of registering as a conflict and abstaining.
Method version 1 → 2.

## Acceptance

- The live DVSA response parses to five tests. ✅
- A full-instant `completedDate` reads. ✅
- Tests offered but unreadable produce a named failure, not silence. ✅
- A kilometre reading yields miles; two units for one reading do not conflict. ✅
- Live: a case shows derived mileage in miles with its provenance — Phase 6.

## Simplification pass

2026-08-21. Both fixes are subtractive — one parse path replacing two, one unit at the
derived boundary instead of a unit carried per consumer. No new abstraction. No findings
deferred.
