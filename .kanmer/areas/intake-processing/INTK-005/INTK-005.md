---
id: INTK-005
type: ticket
title: Allow one Upload submission to accept and track multiple files
status: backlog
area: intake-processing
assignee: ''
profile: feature
labels:
  - upload
  - ui
  - intake
links:
  - PLAT-006
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-19T09:13:42.674Z'
updated: '2026-08-19T09:13:42.674Z'
---

## What
Expand the authenticated Upload page so one submission can accept as many selected files as staff need, rather than enforcing a single-file input.

## Why
Staff intake commonly consists of several related documents and vehicle images. Repeating a one-file workflow is unnecessary friction and can obscure which files belong to the same submission.

## Verification
- Staff can select or add multiple files in one Upload interaction.
- Every selected file is visibly listed and submitted without silent loss.
- Each file receives an honest receipt/processing outcome, including partial failures.
- Existing single-file upload remains supported.

## Outcome
