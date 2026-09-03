---
id: CASE-040
type: ticket
title: >-
  Sign-off Engineer on the Case with the default rule, ribbon field and Send to
  EVA dialog (re-send from With Engineer)
status: preparing
area: case-reference-workflow
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-09-02T22:11:20.520Z'
labels:
  - case
  - sign-off
  - eva
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links: []
blocks:
  - UIIMP-014
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-07-eva-and-external-engineering-handoff.md
  - docs/frd/frd-04-parties-accounts-and-access.md
archived: false
created: '2026-09-02T20:31:38.755Z'
updated: '2026-09-02T22:11:20.520Z'
---

## What

A Sign-off Engineer field beside Engineer (ribbon, Overview, Current position); default to the assigned Engineer when flagged, otherwise A Patterson; the Send to EVA dialog carries Engineer, Sign-off Engineer, Download ZIP or Send via API and is offered in Review and With Engineer; "Download EVA package" is retired.

## Why

D31, D36. Mockup source: `Pegasus_UI_v2_src/src/05-state.js` (`defaultSignoff`), `20-case.js` (`case-eva` dialog).

## Approach

- Reuse the CASE-012 EVA handoff dialog and `Eva/Send.*`; the flag comes from the account setting ticket.

## Verification

- [ ] Default rule holds for a flagged and an unflagged Engineer.
- [ ] Re-send from With Engineer records a new handoff without changing state.

## Outcome
