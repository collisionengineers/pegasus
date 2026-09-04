---
id: UIIMP-015
type: ticket
title: 'Scoped Test UI snapshot capture: regenerate only the routes a lane changed'
status: preparing
area: ui-improvement
assignee: ''
profile: chore
stageEntered:
  preparing: '2026-09-04T10:13:58.674Z'
labels:
  - test-ui
  - tooling
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links: []
refs:
  - docs/engineering.md
archived: false
created: '2026-09-04T09:53:04.117Z'
updated: '2026-09-04T10:13:58.674Z'
---

## What

Give `scripts/Update-TestUiSnapshots.ps1` a `-Scope <page-prefix,...>` switch, paired with `-CaptureFilter "<xUnit filter>"` naming the test classes that produce those pages, and have `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` honour a `PEGASUS_TEST_UI_SCOPE` variable so that, in update and verify modes, generation, comparison, orphan deletion and the offline Chromium render apply only to `pages/<prefix>--*.html`; `docs/design/test-ui/index.html` is rebuilt from the catalogue as today. With no scope set every behaviour is byte-identical to today, and CI keeps running the unscoped `-Verify`.

## Why

The whole capture cohort (~123 browser + ~308 non-browser tests) takes about 25 minutes locally, and the tooling deletes every committed page it holds no capture for (`WriteGenerated`) and fails verify on "no state generates", so a lane that changed one route cannot regenerate one page. That capture is the largest per-lane cost in [[EPIC-012]] and is what left [[CASE-038]] with a wrong committed artifact. The testing review of 2026-09-03 recommends an explicit capture cohort; this is its local half.

## Approach

- Reuse the existing `PEGASUS_TEST_UI_MODE` / `PEGASUS_TEST_UI_CAPTURE_DIR` environment convention and `Invoke-TestUiPhase`; no new script, project or package.
- Scope is a comma-separated page-name prefix list matched against generated keys (`pages/<prefix>--`); an unset scope takes today's code path unchanged.
- Update the Commands text in `CLAUDE.md`/`AGENTS.md` and `docs/runbook.md` in the same PR (rule 24); the kanmer-managed block is not touched.

## Verification

- [ ] `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Scope case-details -CaptureFilter "FullyQualifiedName~CaseDetailsWebTests"` regenerates only `pages/case-details--*.html` and leaves every other committed page byte-identical.
- [ ] `-Verify -SkipCapture -Scope case-details` compares and offline-renders only those pages.
- [ ] The unscoped `-Verify` on this branch is green in CI with no snapshot diff, proving the default path is unchanged.
- [ ] `./scripts/Test-UiCatalogue.ps1` passes.

## Outcome
