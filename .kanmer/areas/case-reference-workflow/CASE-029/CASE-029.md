---
id: CASE-029
type: ticket
title: >-
  Case: Valuations tab, Notes timeline, Vehicle checks and upload-request dialog
  fields
status: backlog
area: case-reference-workflow
assignee: ''
profile: feature
labels:
  - ui
  - wave-4
  - case
groups:
  - EPIC-011
links: []
blocks:
  - CASE-012
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-28T08:35:24.142Z'
updated: '2026-08-29T13:10:44.450Z'
---

## What

Wave 4 of [[EPIC-011]]. New `Pages/Cases/Valuations.*` (table + add/edit dialog per §1.8), Notes view switched to the merged `GetCaseTimeline` with actor IDs, Vehicle checks state list and Vehicle History wired, upload-request dialog gains Recipient + Reason (policy values read-only) and the Record-chase dialog fields mapped to `ManualChaseRecord`.

## Owns

`src/Pegasus.Web/Pages/Cases/Valuations.*` (new), `Tasks.*`, `Vehicle.*`, `Custody.*` (dialog fields), `src/Pegasus.Core/Documents/RequestUploadPolicy.cs` (RecipientLabel/Reason), migration, tests.

## Blocked by

The Case views port ticket, the valuations ticket, the timeline ticket.
