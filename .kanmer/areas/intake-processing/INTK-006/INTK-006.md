---
id: INTK-006
type: ticket
title: Diagnose and fix vehicle-image uploads that produce no case or visible outcome
status: backlog
area: intake-processing
assignee: ''
profile: fix
labels:
  - upload
  - production-diagnostics
  - intake
  - vehicle-image
links:
  - TICK-011
  - PLAT-006
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
archived: false
created: '2026-08-19T09:13:45.922Z'
updated: '2026-08-19T09:13:45.922Z'
---

## What
Investigate the production report that uploading a vehicle image appeared to do nothing and created no case. Restore an honest, observable outcome and the intended case/intake behaviour.

## Why
A silent upload outcome loses operator trust and may strand source evidence. The system must distinguish accepted/queued, fail-closed, and failed processing rather than appearing to discard the image.

## Verification
- Azure runtime evidence identifies where the reported submission stopped, or records the limits of available correlation evidence.
- A vehicle-image upload produces a visible receipt and processing state.
- A case is created only when the principal, processing result, and allocation gates are complete; otherwise the UI shows the specific withheld/failed state without losing the receipt.
- Regression evidence covers the diagnosed path.

## Outcome
