---
id: INTK-020
type: ticket
title: >-
  Treat an upload submission as one unit: one decision card, thumbnails
  everywhere images appear
status: done
area: intake-processing
order: 30
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-20T14:11:41.964Z'
  review: '2026-08-20T14:53:26.920Z'
  verifying: '2026-08-20T16:38:26.794Z'
  done: '2026-08-20T20:51:14.288Z'
labels:
  - upload
  - ui
  - operator-reported
  - grouped-upload
links: []
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-12-operator-experience.md
deployment: production
archived: false
created: '2026-08-20T14:08:22.595Z'
updated: '2026-08-25T06:40:07.592Z'
---

## Why

Operator, 2026-08-20, verbatim: *"Upload received page still having the same issue as INTK-016 … its basically treating each thing as unique but an upload as a group should all be considered the same. The page is offering to create a case for each image. Additionally cant even see the images. Need to be able to see these on this page, and any other relevant pages e.g. evidence page (thumbnail with expandable view when clicking)."*

## What

- While any member of a submission group is undecided, `/Upload/Group/{id}` shows exactly ONE decision card for the whole submission: create a vehicle-image case (registration input when no VRM was read), add all to an existing case (autocomplete), cancel. Per-file rows become an informational list — no per-file Create/Review buttons.
- Image members render thumbnails (lazy img to the existing `/Received/{id}/Image` endpoint, click to expand) on UploadGroupStatus, UploadStatus, Unidentified details, and the received-material evidence view.
- Group actions are single operations: one replay key per group, all member receipts linked/registered together, open Unidentified rows resolved by the existing reconciliation.

## How to verify

A mixed multi-file upload shows one decision card + thumbnails; choosing each action affects every member; no per-file case offers; integration + browser suites green.

## Outcome
