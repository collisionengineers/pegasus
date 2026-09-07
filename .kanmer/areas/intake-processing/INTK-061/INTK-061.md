---
id: INTK-061
type: ticket
title: Restore durable intake custody and exactly one destination after failures
status: backlog
area: intake-processing
assignee: ''
profile: fix
labels: []
groups:
  - EPIC-014
links:
  - INTK-060
  - INTK-033
  - INTK-039
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-05-documents-extraction-and-custody.md
archived: false
created: '2026-09-07T19:58:09.516Z'
updated: '2026-09-07T19:58:09.516Z'
---

## What

Repair the confirmed PR675 intake regressions at dev 3da60bd0: route custody claims using the incoming artifact identity to its actual table; preserve/retry destination work until association, allocation, Triage and Unidentified registration complete; fail closed on a failed unique Case match; retain exactly one group-level Unidentified outcome and canonical reason; select eligible old grouped-image reconciliation candidates before paging; allow OCR analysis retry after OCR completion.

## Acceptance

Existing production Core/store/Worker paths are repaired without a parallel pipeline or broader Worker permissions. Focused tests reproduce SQL runtime-role custody execution, unique-match failure without duplicate allocation, retry after durable processing, one group-level unidentified result, old-group progress and OCR analysis recovery. Relevant canonical docs and callers agree. No soak/capacity suite. Operator authorized remediation and deployment on 7 September; this ticket prepares an independently reviewable correction before release.

## Outcome
