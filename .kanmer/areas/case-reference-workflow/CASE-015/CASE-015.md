---
id: CASE-015
type: ticket
title: One mileage value is labelled Odometer in one panel and Mileage in another
status: done
area: case-reference-workflow
order: 960
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-22T00:48:43.226Z'
  implementing: '2026-08-22T00:48:46.503Z'
  review: '2026-08-22T00:51:07.212Z'
  verifying: '2026-08-22T03:44:45.252Z'
  done: '2026-08-22T03:44:54.519Z'
labels:
  - qdos26009
  - ui
  - operator-reported
links: []
docs_todo: true
deployment: production
archived: false
created: '2026-08-21T23:30:27.962Z'
updated: '2026-09-03T09:06:47.148Z'
---

## Why

The operator reports apparently duplicate case fields — Odometer and Mileage both present, only one populated — and asked whether the database carries duplicates.

## Evidence — it is not a duplicate field, it is a duplicate label

There is **one** underlying value, `Vehicle.Mileage`, rendered under two different names:

| Surface | Label | Source |
| --- | --- | --- |
| `_CaseSummary.cshtml:99` | **Odometer** | `data.Vehicle.Mileage` |
| `_CaseWorkflow.cshtml:106` | **Mileage** | `data.Vehicle.Mileage.Fact` / `.Confirmed` |
| `_CaseWorkflow.cshtml:445` | **Mileage** | the MOT observation estimate |
| `Assessment/Index.cshtml:329` | **Mileage** (on `#vehicle-odometer`) | assessment prefill |

The assessment page even labels a control `Mileage` whose id is `vehicle-odometer` — the two names have already collided in one element.

Only one is populated at a time because they are different projections of the same field, which is exactly why it reads as a half-filled duplicate.

This breaks the repository's *one list per concept* rule: a label table must live in exactly one place, and the operator-facing name for this value must be settled once.

## Scope

- Settle one operator-facing name for the value and use it everywhere.
- Sweep the remaining case fields for the same defect — the operator asked for other duplicates to be checked, so this is a pass over the case surfaces, not a single rename.

## How to verify

The case page names the value once; no surface shows two labels for one field; the sweep's findings are recorded even where nothing needed changing.
