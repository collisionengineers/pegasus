---
id: CASE-029
type: ticket
title: >-
  Case: Valuation section, DVLA & MOT lookup with per-field suggestion chips,
  upload-request dialog fields
status: implementing
area: case-reference-workflow
assignee: wf-build/case-029
profile: feature
stageEntered:
  preparing: '2026-09-02T22:22:26.445Z'
taken_at: '2026-09-04T19:13:23.957Z'
branch: task/case-029-valuation-lookup-chips
worktree: .worktrees/case-029
claim_expires_at: '2026-09-04T19:43:23.957Z'
claim_controller: wf-build/case-029
lease_id: 91b6f6ca-dcbb-4aa7-af4b-84ddb035b03f
lease_revision: 1
lease_workspace: 'worktree:c:\users\pc\documents\github\pegasus\.worktrees\case-029'
lease_provider: codex
lease_phase: implementing
lease_heartbeat_at: '2026-09-04T19:13:23.957Z'
labels:
  - ui
  - case
  - case-workspace-v2
groups:
  - EPIC-011
  - EPIC-012
links: []
blocks:
  - CASE-012
  - UIIMP-014
  - CASE-043
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-28T08:35:24.142Z'
updated: '2026-09-04T19:13:23.957Z'
---

## What

Re-scoped 2026-09-02 for [[EPIC-012]] (D34, D40). Three pieces on the Case page:

- **Valuation section** (`Pages/Cases/Shared/_CaseValuation.cshtml`, new): source cards and an Add valuation dialog with sources Glass's (valuation), Cazana (disabled seam), Engineer's Value and AI market research (row shape from the market-research ticket); guide month and mileage per entry. Glass's valuation and Glass's estimate import are two systems: separate label entries, never merged. Adjustments, rationale and history stay with EXT-10 ([[TICK-083]], later).
- **One vehicle lookup**: a single "Look up DVLA & MOT" action on the Vehicle section header. Looked-up values become a suggestion map on the case data (`Suggestion` kind in `CaseDataValueKind`) and render as per-field chips that fill the field when chosen. The vehicle-checks panel and any suggestion table are removed. Experian stays a disabled seam.
- **Upload-request dialog fields**: Recipient + Reason (policy values read-only) and the Record-chase dialog fields mapped to `ManualChaseRecord`.

The Notes timeline part of the original scope shipped with CASE-017/CASE-028 and is dropped here.

## Why

Operator direction 2026-09-02; mockup source `Pegasus_UI_v2_src/src/21-case-sections.js` §vehicle (`apply-suggestion`) and `22-case-engineer.js` §valuation.

## Owns

`src/Pegasus.Web/Pages/Cases/Shared/_CaseVehicle.cshtml`, `_CaseValuation.cshtml` (new), `Pages/Cases/Vehicle.*`, `Custody.*` (dialog fields), `src/Pegasus.Core/Documents/RequestUploadPolicy.cs` (RecipientLabel/Reason), tests.

## Verification

- [ ] One lookup action; chips appear only for fields whose looked-up value differs; choosing a chip fills the field and clears the chip.
- [ ] Valuation sources listed as above; Cazana disabled with its condition; no adjustments UI.
- [ ] Upload-request and chase dialogs carry the mapped fields.

## Blocked by

The frame ticket and the vocabulary ticket in EPIC-012; [[ENG-027]] (valuation record).

## Outcome
