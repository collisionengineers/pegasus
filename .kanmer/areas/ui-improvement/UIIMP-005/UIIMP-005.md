---
id: UIIMP-005
type: ticket
title: >-
  Harden the generated Test UI snapshot tooling (deferred review findings from
  UIIMP-004)
status: preparing
area: ui-improvement
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-28T08:08:12.055Z'
labels:
  - ui
  - design
  - tests
groups:
  - EPIC-011
links:
  - UIIMP-004
refs:
  - docs/frd/frd-12-operator-experience.md
deployment: n/a
archived: false
created: '2026-08-27T08:17:30.643Z'
updated: '2026-08-28T08:08:12.055Z'
---

## What

Findings on PR #562 ([[UIIMP-004]]) that were dispositioned "defer" so the
reviewed snapshot tooling could merge:

- Fresh-capture nondeterminism: `OperationId`, `ExternalReceiptToken`,
  request tokens, `Activity.Current?.Id` on `/Error` and per-run GUIDs survive
  normalization, so update-then-verify without `-SkipCapture` reports drift.
- `TestUiFocusedRenderTests.OpenUnidentifiedDetailRendersThroughRazor` invents
  domain data (`test detail`, `test-worker`) — use a documented fixture.
- `Update-TestUiSnapshots.ps1` omits `Category!=Corpus` and the browser
  `xUnit.MaxParallelThreads=2` cap; concurrent identical captures can collide
  on the same hash directory.
- `-Verify` never runs in CI and does not reject orphaned `pages/*.html`;
  CRLF checkouts fail verify.
- Offline pages keep `data-auto-refresh`, `data-mail-preview-url` and the
  `?handler=CaseSearch` JSON endpoint rewritten to HTML.
- Evidence-image fallback maps any receipt to the first captured image.
- `AGENTS.md` does not record the regenerate/verify convention.

## Outcome
