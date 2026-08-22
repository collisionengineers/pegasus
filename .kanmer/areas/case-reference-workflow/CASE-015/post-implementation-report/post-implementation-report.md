# Post-implementation report

**Branch:** `task/qdos26009-operator-fixes` · **PR:** #506 · **Commit:** `43488ea9`

## The database question, answered with evidence

The operator asked whether these are *"duplicate database fields"*. They are not, and this
was checked rather than assumed:

```sql
SELECT DISTINCT FieldName FROM CaseDataFields;   -- production
```

returns twelve names in use, and the full declared vocabulary is nineteen
(`CaseDataFieldNames`, enforced by the `CK_CaseDataFields_FieldName` check constraint):

```
work_provider_code  claimant_name   claim_number      vehicle_registration
vehicle_make        vehicle_model   vehicle_mileage   vehicle_mileage_unit
accident_circumstances  incident_date  contact_name   contact_email_address
contact_phone_number    instruction_date  vat_status  inspection_date
inspection_deadline     inspection_address  inspection_mode
```

**There is no `odometer` field and no second mileage field.** One value,
`vehicle_mileage`, with its unit alongside. A check constraint makes a duplicate
impossible to introduce by accident.

## What was actually wrong

One value wearing two names in the UI: `_CaseSummary.cshtml` called it **Odometer**,
`_CaseWorkflow.cshtml` called it **Mileage**. Only one ever looked populated because they
are two projections of the same field. The summary now says Mileage — the name the
workflow panel, the assessment prefill, the DVSA-derived value and the underlying
`vehicle_mileage` all already used.

## The sweep, and a judgement recorded rather than buried

Comparing every label across the two case panels for the same field found two more
disagreements:

| Field | Summary | Workflow |
| --- | --- | --- |
| `Vehicle.Make` | Make | Vehicle make |
| `Vehicle.Model` | Model | Vehicle model |

**Left alone.** "Make" and "Vehicle make" read as obviously the same field; "Odometer" and
"Mileage" read as two different numbers. That is a judgement, on the record so a reviewer
can disagree with it.

One oddity found and left: `Cases/Assessment/Index.cshtml:329` labels a control **Mileage**
whose id is `vehicle-odometer`. The label is right; the id is internal.

## Untouched, with a reason

"Odometer reading" on the assessment surfaces and in `AssessmentPolicy.cs` is the
engineer's recorded reading during assessment — a different fact from the mileage extracted
from documents, and Core-owned. Renaming it would merge two genuinely distinct concepts.

## Evidence

- Production `CaseDataFields` vocabulary read directly — no duplicate fields
- `Pegasus.Web` builds clean; the only remaining "Odometer" in `Pages/**` is the assessment
  suggestion label
- Live: the summary and workflow panels agreeing — next deploy
