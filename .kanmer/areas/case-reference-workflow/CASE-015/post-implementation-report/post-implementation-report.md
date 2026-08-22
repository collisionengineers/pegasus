# Post-implementation report

**Branch:** `task/qdos26009-operator-fixes` · **PR:** #506 · **Commit:** `43488ea9`

## What was built

The case summary calls `Vehicle.Mileage` **Mileage**, which is what the workflow panel, the
assessment prefill, the DVSA-derived value and the underlying `vehicle_mileage` field
already called it. One line.

## The answer to the question actually asked

The operator asked whether the database carries duplicate fields. **It does not.** There is
one value wearing two names, which is exactly why only one ever looked populated. Saying so
is part of the deliverable — a fix that quietly renamed a label without answering the
question would have left the operator still wondering about the schema.

## The sweep, and a judgement recorded rather than buried

Comparing every label across the two case panels for the same underlying field found two
more disagreements:

| Field | Summary | Workflow |
| --- | --- | --- |
| `Vehicle.Make` | Make | Vehicle make |
| `Vehicle.Model` | Model | Vehicle model |

**Left alone.** "Make" and "Vehicle make" read as obviously the same field; "Odometer" and
"Mileage" read as two different numbers. Renaming the first pair is churn that removes no
confusion. That is a judgement, not an oversight, and it is on the record so a reviewer can
disagree with it.

One oddity found and left: `Cases/Assessment/Index.cshtml:329` labels a control **Mileage**
whose id is `vehicle-odometer`. The label is right; the id is internal.

## Untouched, with a reason

"Odometer reading" on the assessment surfaces and in `AssessmentPolicy.cs` is the
engineer's recorded reading during assessment — a different fact from the mileage extracted
from documents — and its label is Core-owned. Renaming it would have merged two concepts
that are genuinely distinct.

## Evidence

- `Pegasus.Web` builds clean; the only remaining "Odometer" in `Pages/**` is the assessment
  suggestion label
- Live: the summary and workflow panels agreeing — Phase 6
