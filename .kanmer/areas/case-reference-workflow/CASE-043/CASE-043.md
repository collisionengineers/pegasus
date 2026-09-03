---
id: CASE-043
type: ticket
title: >-
  Extend the case vehicle record with the DVLA/MOT fields, populated from the
  instruction first and DVLA/DVSA on intake
status: backlog
area: case-reference-workflow
assignee: ''
profile: feature
labels:
  - case
  - vehicle
  - dvla
  - extraction
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links: []
refs:
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-05-documents-extraction-and-custody.md
archived: false
created: '2026-09-03T10:53:00.314Z'
updated: '2026-09-03T10:53:00.314Z'
---

## What

Give the case vehicle record a Core owner for the fields the v2 Case page
shows beyond registration, make, model and mileage: colour, fuel, engine
capacity, transmission, body, manufacture year, first registration, tax
expiry, MOT expiry and VIN. Extend `CaseVehicleData`
(`src/Pegasus.Core/Cases/CaseDataContracts.cs`), the persisted field-name
allow-list and its database check constraint
(`src/Pegasus.Infrastructure/Persistence/CaseDataEntities.cs`
`CaseDataFieldNames`) and the completeness rules, with the migration and its
grants in the same diff.

Population order, per the operator (2026-09-03):

1. Extract from the supplied instruction material first, as every other case
   field is populated.
2. Then call DVLA and DVSA automatically on intake to fill what extraction
   did not, recorded as a lookup source, never overwriting an operator entry.

## Why

Operator answer to [[CASE-029]]'s open question (2026-09-03): CASE-029 ships
suggestion chips for make, model and mileage only, and a separate ticket owns
the record extension. Without this the v2 Vehicle section has fields with no
Core owner, which the EPIC-012 feature outcome forbids.

## Approach

- Extend the existing contract and allow-list; no parallel vehicle store and
  no second list of field names.
- Reuse the existing intake extraction path and the existing DVLA/MOT lookup
  port added by [[CASE-029]]; call it from intake rather than adding a second
  client.
- One migration, additive, with grants and the bootstrap census.

## Verification

- [ ] Every listed field round-trips through the Core contract and the
      database check constraint.
- [ ] An instructed case whose instruction carries the values shows them
      without a lookup.
- [ ] A case whose instruction lacks them has them filled by the intake
      lookup, attributed to the lookup source.
- [ ] Migration and grants ship in one diff; `Test-MigrationGrants.ps1`
      passes.

## Outcome
