# Plan

Committed in `ca564ac5`. A view-only change.

## The line drawn

> "We do not need a whole section for DVSA/DVLA data. We do not need the entire MOT
> history. We mainly just need the double lookup to populate vehicle details if the
> instructions or original report don't have them, and to get a mileage if the original
> documents did not provide one."

So: **what the lookup supplies stays; what the lookup is stays out.**

Removed — the MOT test table, and the rows that describe the mechanism rather than the
vehicle (which provider answered, its version, the outcome of the call itself).

Kept — registration, retrieval time, the vehicle details, and the mileage with its
evidence classification. An operator still needs to see that a value came from an external
lookup rather than a document, which is provenance, not narration.

## What was deliberately not done

The lookups are untouched and the observations are still stored. Discarding them would
undo [[ENG-010]], which fixed the fact that every MOT test DVSA returned was being thrown
away, and would remove the input the derived mileage is computed from.

## Acceptance

- No MOT history and no DVSA/DVLA mechanics section on the case page. ✅
- A vehicle detail or mileage only the lookup could supply is still shown, still labelled
  as externally sourced. ✅
- `Pegasus.Web` builds clean. ✅
- Live: a case with a completed lookup — Phase 6.

## Simplification pass

2026-08-22. Subtractive; nothing added. No findings deferred.
