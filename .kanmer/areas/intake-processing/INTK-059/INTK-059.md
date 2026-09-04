---
id: INTK-059
type: ticket
title: Show the valid principal on the Triage case page
status: backlog
area: intake-processing
assignee: ''
profile: feature
labels:
  - triage
  - principal
  - ui
groups:
  - EPIC-011
links:
  - INTK-046
refs:
  - docs/frd/frd-03-triage.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-09-04T10:21:48.346Z'
updated: '2026-09-04T10:21:48.346Z'
---

## What

Show the Triage case's valid principal as a visible, read-only field on
`/triage/{id}`.

A Triage case cannot be created without a valid principal. Keep that
creation rule intact; do not broaden or replace the current QDOS-specific
Triage matching logic as part of this work.

## Why

Operators need to see the principal that was validated when the Triage case
was created.

## Approach

Extend the existing Triage page projection and presentation owned by
[[INTK-046]]. Reuse the established principal identity/display value; do not
introduce a second matching path or principal lookup.

## Verification

- [ ] A Triage case with a valid principal displays that principal on its
      Triage page.
- [ ] The field is read-only and the existing creation validation remains
      unchanged.
- [ ] The page shows the principal from the existing Triage record, not a
      QDOS-specific display reconstruction.

## Outcome
