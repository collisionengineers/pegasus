---
id: DOCS-017
type: ticket
title: 'Report signatory policy: sign-off Engineer tuple on every report'
status: preparing
area: documents-reports
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-09-02T20:51:35.667Z'
labels:
  - sign-off
  - renderer
  - case-workspace-v2
groups:
  - EPIC-011
  - EPIC-004
  - EPIC-012
links:
  - DELIV-040
  - TICK-216
  - TICK-081
  - TICK-097
  - DOCS-001
blocks:
  - CASE-040
  - PLAT-068
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/frd/frd-04-parties-accounts-and-access.md
archived: false
created: '2026-09-02T11:20:50.869Z'
updated: '2026-09-02T20:51:35.667Z'
---

## What

Reopened 2026-09-02 by operator decision D31 ([[EPIC-012]] context): reports render the Case's sign-off Engineer tuple (name, qualifications, signature image) read from the flagged staff account, instead of the fixed `A Patterson | M.Inst.IAEA | andy_patterson` tuple. Andy, Neil and Ed sign; Andy is the default; Neil's qualifications are recorded later by an Administrator (a report with a missing qualification prints the name alone).

## Why

Operator review of [[DELIV-040]] had withdrawn D18; the 2026-09-02 review reinstated a signatory policy with a separate sign-off Engineer. D31 supersedes D18.

## Approach

- Renderer signature block reads the tuple supplied by the report projection; the projection reads the Case's sign-off Engineer and the account setting (EPIC-012 account-setting ticket).
- Keep generation deterministic, versioned, retained and review-gated; generation, approval, issue, sending, receipt and closure remain distinct events.

## Verification

- [ ] A report for a Case whose sign-off is Ed renders Ed's tuple; missing qualifications print the name alone.
- [ ] An unflagged Engineer cannot be chosen as sign-off.

## Outcome
