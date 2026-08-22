# Plan

Committed in `43488ea9`.

## It was never duplicate database fields

The operator asked whether the database carries duplicates. It does not. There is one
value, `Vehicle.Mileage`, wearing two names — which is why only one ever looked populated.

| Surface | Was |
| --- | --- |
| `_CaseSummary.cshtml:99` | **Odometer** |
| `_CaseWorkflow.cshtml:106` | **Mileage** |

Both read the same field. The summary now says Mileage — the name the workflow panel, the
assessment prefill, the DVSA-derived value and the underlying `vehicle_mileage` field all
already use.

## The sweep, and what it found

Comparing every label across the two case panels for the same underlying field turned up
two more disagreements:

| Field | Summary | Workflow |
| --- | --- | --- |
| `Vehicle.Make` | Make | Vehicle make |
| `Vehicle.Model` | Model | Vehicle model |

**Left as they are.** "Make" and "Vehicle make" are transparently the same field; renaming
them would be churn without removing confusion. "Odometer" and "Mileage" are two different
words for one number, which is the defect the operator actually hit. Recorded here so the
judgement is visible rather than looking like an oversight.

One genuine oddity found and left: `Cases/Assessment/Index.cshtml:329` labels a control
**Mileage** whose id is `vehicle-odometer`. The label is right and the id is internal.

## Acceptance

- The case page names the value once. ✅
- The remaining variances are recorded with a reason. ✅
- The assessment's "Odometer reading" is untouched — a different fact, Core-owned. ✅
- Live: the case summary and workflow panels agree — Phase 6.

## Simplification pass

2026-08-22. One label table, one name per concept. No findings deferred.
