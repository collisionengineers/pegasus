---
id: INTK-046
type: ticket
title: 'Port Triage, Unidentified, Received and the image-record pages'
status: backlog
area: intake-processing
assignee: ''
profile: feature
labels:
  - ui
  - wave-2
  - triage
  - unidentified
groups:
  - EPIC-011
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-03-triage.md
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-08-28T08:35:23.884Z'
updated: '2026-08-28T08:35:23.884Z'
---

## What

Wave 2 lane C2 of [[EPIC-011]]. Port `Pages/Triage/Details` (§1.5: determinations panel saving both findings through the existing `OnPostActionAsync` record_finding path, source panel, notes panel; the other transitions stay reachable through dialogs where a handler exists), `Pages/Unidentified/Details` (§1.6: retained source panel, history, resolve dialog with destination select), `Pages/Intake/Details` (Received workbench restyled, handlers unchanged) and `Pages/ImageIntake/Details` (the image record per D1, gallery retained, back link to `/Cases?tab=not_ready`).

## Owns

`src/Pegasus.Web/Pages/Triage/Details.*`, `Pages/Unidentified/Details.*`, `Pages/Intake/**`, `Pages/ImageIntake/Details.*`, tests `TriageEvidenceImagesWebTests.cs`, `QdosIntakeWebTests.cs`, `GroupedIntakeWebTests.cs`, `ImageIntakeWebTests.cs`, `ImageViewingWebTests.cs`.

## Blocked by

[[PLAT-029]].

## Verification

- [ ] Every button posts an existing handler; no inert control.
- [ ] No clipped text/overflow at 1580/1100/760.
