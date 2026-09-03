---
id: CASE-013
type: ticket
title: A complete case stays Not ready because no completeness flag is ever set
status: done
area: case-reference-workflow
order: 940
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-22T00:47:27.434Z'
  implementing: '2026-08-22T00:47:30.371Z'
  review: '2026-08-22T00:51:01.093Z'
  verifying: '2026-08-22T04:36:05.923Z'
  done: '2026-08-22T08:01:06.453Z'
labels:
  - regression
  - qdos26009
  - release-17
links: []
docs_todo: true
deployment: production
archived: false
created: '2026-08-21T23:30:27.847Z'
updated: '2026-09-03T09:06:47.040Z'
---

## Why

QDOS26009 shows "details incomplete" with no indication of what is missing. The operator can see claimant, claim number, incident date, vehicle make, model and registration on the case — it looks complete, and it should have moved to Review.

## Evidence read from production (2026-08-22)

The case genuinely holds the details:

```
claim_number  fact  SCL/ND/47620/1        vehicle_make   fact  BMW
claimant_name fact  Mr David Smith        vehicle_model  fact  420D M SPORT
incident_date fact  2026-08-10            vehicle_registration fact DF18FEJ
instruction_date fact 2026-08-21          work_provider_code   fact QDOS
inspection_address confirmed Image Based Assessment
```

And yet:

```
InstructionComplete=False  ImagesComplete=False
InstructionConfirmedByStaff=False  ImagesConfirmedByStaff=False
```

`EfQueuedCustodyProcessor.CompleteCaseCustodyAsync` only promotes to Review when **all four** are true, so the case cannot leave `NotReady`.

## Two questions this ticket must answer

1. Why did intake record the instruction as incomplete when every field it names is present? 
2. Should an **image-based audit** ever require `ImagesConfirmedByStaff`? There is no inspection and no staff image review in this flow, so a gate that can never be satisfied is a gate that strands every case of this kind.

Note the custody failure on the same case is tracked separately — a case that fails custody never reaches the promotion check at all, so these two may compound.

## How to verify

The live QDOS26009 shape reaches Review, and where a case genuinely is incomplete the page names **which** evidence is missing rather than saying only that it is.
