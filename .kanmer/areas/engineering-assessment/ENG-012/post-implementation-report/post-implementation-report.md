# Post-implementation report

**Branch:** `task/qdos26009-operator-fixes` · **PR:** #506 · **Commit:** `ca564ac5`

## What was built

The MOT history table is gone from the case page, along with the rows that describe the
call rather than the vehicle — "Latest lookup outcome", "Provider" and the provider
version — and the mileage row drops its "latest MOT observation" narration while keeping
the observation date.

## The line drawn, and why

What the lookup **supplies** stays; what the lookup **is** goes. Registration, retrieval
time, make, model, manufacture year, engine capacity, fuel type and mileage remain, with
the mileage's evidence classification — an operator still needs to see that a value came
from an external lookup rather than a document. That is provenance, not narration.

The accept and correct forms stay: they are how a looked-up value becomes a confirmed one,
which is the gap-filling mechanism the operator described as the point of the lookups.

## Deliberately not done

Nothing changes about collection. [[ENG-010]] had just proved that every MOT test DVSA
returned was being silently discarded, and the derived mileage is computed from those
tests. Removing the display is right; removing the data would undo the fix that made the
mileage possible.

## Evidence

- `Pegasus.Web` builds clean; no `MotTests` reference remains in the view
- Live: a case with a completed lookup showing no MOT history and no provider section,
  while a lookup-supplied vehicle detail or mileage is still present and still labelled as
  externally sourced — Phase 6
