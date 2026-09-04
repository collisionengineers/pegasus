---
id: INTK-059
type: ticket
title: Persist and show the optional principal on the Triage page
status: preparing
area: intake-processing
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-09-04T10:28:53.909Z'
labels:
  - triage
  - principal
  - ui
groups:
  - EPIC-011
links:
  - INTK-046
  - INTK-033
refs:
  - docs/frd/frd-03-triage.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-09-04T10:21:48.346Z'
updated: '2026-09-04T10:32:09.056Z'
---

## What

Persist an optional principal relationship on each Triage record and show the
known principal as a read-only field on `/triage/{id}`.

Capture the relationship when an accepted route or authenticated Provider API
declaration identifies the principal. A manually classified Triage may have
no principal, as permitted by FRD-03; it remains valid and the page must not
invent one from QDOS text or any other heuristic.

## Why

The current Triage aggregate and `Triage` table have no principal field. The
current automatic mail classification is QDOS-specific, and its route evidence
is not persisted as a principal relationship on Triage.

## Approach

Extend the existing Triage creation, Core contract, persistence mapping and
Triage page that [[INTK-033]] and [[INTK-046]] own. Use the principal resolved
by the accepted route/declaration; do not add another matcher or alter Triage
classification. Ship the schema migration and its required grants with the
change.

## Verification

- [ ] A Triage opened by an accepted route or Provider API declaration stores
      and displays its principal.
- [ ] A manually classified Triage without a principal remains valid and no
      principal value is fabricated or inferred.
- [ ] The field is read-only; existing QDOS classification and Provider API
      declaration behaviour remain unchanged.
- [ ] The migration, foreign-key integrity and affected Core, persistence and
      page paths are covered by tests.

## Outcome
