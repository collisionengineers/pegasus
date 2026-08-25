---
id: INTK-001
type: ticket
title: >-
  Make queued upload status honest for retry-scheduled work and auto-associated
  receipts
status: preparing
area: intake-processing
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-25T15:19:39.900Z'
labels: []
groups:
  - EPIC-002
  - HZN-003
links:
  - SIMPLI-008
  - SIMPLI-009
blocks:
  - DELIV-021
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-08-17T11:10:37.268Z'
updated: '2026-08-25T15:19:39.900Z'
---

## What

Two follow-ups from the PR #385 review of [[SIMPLI-008]]: (1) the `/Upload/Status/{id}` page reads `retry_scheduled` as **Received** and reloads every 2 s for as long as the retry is due (30 min – 2 h) — dishonest and wasteful; (2) it links only through `CaseIntakeLinks`, so a receipt auto-associated to an existing case (`IntakeManualAssociations` / `IntakeReceipt.CurrentCaseId`) shows "Open receipt" rather than "Open case".

## Why

SIMPLI-008 promised staff the resulting case and a bounded, truthful state. A retry-scheduled item should either say so or stop polling; an associated receipt should open its case.

## Approach

- Project `WorkItem.DueAtUtc` in `IQueuedIntakeStatusQueries`; derive the refresh interval from it (clamp, e.g. 2 s … 60 s) and skip reloads while `document.hidden`; or add an explicit "Retry scheduled" staff-visible state — decide in plan against FRD-02 and `docs/design/README.md`.
- Resolve the case id the way `IntakeReceipt.CurrentCaseId` does (link **or** association).
- Simplification rails apply: reuse the existing case-id resolution rather than a third copy; one state table.

## Verification

- [ ] A retry-scheduled receipt is either labelled as such or stops the 2 s reload; a background tab does not reload.
- [ ] An auto-associated receipt's status page offers "Open case".

## Outcome

## Inherited scope from [[PLAT-015]]

While making Upload Status truthful, remove the lede paragraphs beneath its H1. The page should present the status, values, and available action without explanatory narration.

Verification for this inherited scope: Upload Status has no lede copy and its retry or case-link state remains truthful.
