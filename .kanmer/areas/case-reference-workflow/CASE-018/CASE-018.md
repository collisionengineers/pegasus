---
id: CASE-018
type: ticket
title: Show each case fact once on the case page
status: done
area: case-reference-workflow
order: 890
assignee: claude-code
profile: fix
stageEntered:
  preparing: '2026-08-22T19:43:39.046Z'
  review: '2026-08-22T20:17:21.129Z'
  verifying: '2026-08-22T21:56:31.080Z'
  done: '2026-08-22T22:47:04.686Z'
labels:
  - qdos26011
  - operator-requested
  - ui
links: []
docs_todo: true
commits:
  - 94b6a9dd
  - e05c81ae
  - b6d54ff6
prs:
  - '517'
deployment: production
archived: false
created: '2026-08-22T19:42:25.226Z'
updated: '2026-08-26T14:34:44.115Z'
---

## Why — operator direction (2026-08-22, QDOS26011)

> "This page is essentially duplicating the case details. At the top, there are numerous containers with all the details. Then, there is one large container containing all the details again, below all of this."

> "There are actually 3 separate parts of this page that show the vehicle registration: 1. Vehicle Container at the top 2. Case Details container below this 3. Vehicle evidence tab at the bottom. We only need to be showing the information once."

> "There are also two entirely unnecessary containers: 'Where this case stands' and 'Engineer Queries' - both these should be gone."

> "In the case identity tab, Claimant Name and Claim number are unaligned with the rest of the fields (likely because of the icon indicating they were extracted, but this still should not be happening)"

## What is actually on the page today

The Overview tab renders `Shared/_CaseSummary` then `Shared/_CaseWorkflow`. Between them the same facts appear up to three times:

| Fact | `_CaseSummary` block-grid | `_CaseWorkflow` "Case detail" | `_CaseWorkflow` "Vehicle evidence" |
| --- | --- | --- | --- |
| Registration | Vehicle | row 3 | Confirmed registration + observation |
| Make / Model | Vehicle | rows 4–5 | Confirmed + observation |
| Mileage | Vehicle | row 6 | Confirmed + observation |
| Claimant / Claim number | Case identity | rows 1–2 | — |
| Incident date, circumstances | Dates / Circumstances | rows 7–8 | — |

"Case detail" (`_CaseWorkflow.cshtml:91-222`) is a read-only restatement of the whole projection **plus** the edit form. The edit form is the only part that is not a duplicate.

## Scope

- Delete the read-only row list from "Case detail"; keep the edit form, which appears only under an edit lease. The block-grid becomes the one place a case fact is read.
- "Vehicle evidence" keeps only what the block-grid cannot show — the accept/correct/request-lookup controls. Its restatement of registration, make, model and mileage goes; [[ENG-013]] puts those values on the vehicle fields themselves.
- Remove the "Where this case stands" block. State is already in the header chip; `DueWork` already has its own "Chase history" panel.
- Remove the "Engineer queries" block — an inactive surface with a disabled control and an empty-state panel, which `docs/design/README.md` forbids in read-only view.
- Fix `.datarow` alignment so a row with a provenance icon lines up with a row without one.

## How to verify

Open QDOS26011: registration, make and model appear exactly once; mileage appears once; "Where this case stands" and "Engineer queries" are absent; every row in Case identity shares one left edge for its value column.
