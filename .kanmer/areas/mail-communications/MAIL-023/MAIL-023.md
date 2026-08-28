---
id: MAIL-023
type: ticket
title: >-
  Test UI snapshots on dev are stale against their generator and capture
  selection is nondeterministic
status: backlog
area: mail-communications
assignee: ''
profile: fix
labels:
  - test-ui
  - snapshots
links:
  - MAIL-018
archived: false
created: '2026-08-27T17:34:40.284Z'
updated: '2026-08-27T17:34:40.284Z'
---

## Problem

Running `scripts/Update-TestUiSnapshots.ps1` on a branch cut from `dev` (a9184315) for MAIL-018 regenerated 50 files, of which 49 are unrelated to the change:

- Every page: the brand mark `<img src="../../../../src/Pegasus.Web/wwwroot/images/marks/pegasus-lockup.png">` is replaced by an inlined `data:image/png;base64,…` URI — the generator on dev (`TestUiSnapshotTests.cs` ~:280, commit 44d16f46 "Embed captured evidence images in Test UI") inlines captured assets, so the committed snapshots no longer match the generator that produces them; `administration--default.html` also inlines its 8 card icons.
- `case-create--default.html`: a different capture candidate is chosen per run (pre-filled hand-keyed form vs empty form) — capture-selection nondeterminism.
- `upload-group-status--processing.html` and a few others: the `OperationId` hidden-field nonce differs between captures.
- The generator writes LF while checkouts with autocrlf report `LF will be replaced by CRLF` on all 50 files.

MAIL-018 committed only its own page (`administration-mailboxes--default.html`, image line kept as the committed path) per the scope rule; details in MAIL-018 `scratch/snapshots`.

## Required outcome

`Update-TestUiSnapshots.ps1` followed by `-Verify` is a no-op on a clean `dev` checkout: snapshots match the generator, capture selection is deterministic (stable fixture choice, nonce masked), and the line-ending convention is fixed in `.gitattributes` or the writer.
