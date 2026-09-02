# Files — UIIMP-013

## Modify

| Path | Responsibility |
| --- | --- |
| `scripts/Update-TestUiSnapshots.ps1` | Partition the existing capture selection into browser and non-browser phases, reuse the same capture directory, avoid repeated builds, and report phase timing. |
| `.github/workflows/ci.yml` | Preserve the build-affecting trigger, correct the measurement commentary, set bounded timeouts, and distinguish an incomplete run from an explicit stale-corpus assertion. |
| `docs/runbook.md` | Record the existing browser/non-browser concurrency policy as applied to Test UI capture. |

## Inspect only

- `tests/Pegasus.IntegrationTests/xunit.runner.json` — owns the default
  four-thread cap.
- `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` — owns complete
  state generation, stale-file checks, orphan checks, and offline rendering.
- `tests/Pegasus.IntegrationTests/TestUiResponseCapture.cs` — proves the
  shared capture directory is content-addressed and write-once.
- `docs/design/README.md` — governs the Test UI catalogue.

## Do not modify

- `docs/design/test-ui/**`
- `src/**`
- `tests/**`
- `AGENTS.md`
- Other CI jobs or scripts

## Verified premises

- Recent successful build-relevant runs execute the snapshot step in
  approximately 24–27 minutes; the 40m23s run is a historical outlier, not the
  current baseline.
- The current capture command pins both browser and non-browser tests to two
  threads. The project default is four, while only browser tests require two.
- Verify is one `TestUiSnapshotTests` fact over the retained capture, not a
  second pass through all capture tests.
- Reusing the broader SQL/browser lanes would capture responses outside the
  curated selection and can change snapshot candidate selection.
- UI-only or post-merge scheduling weakens detection for indirect render
  changes, so the full gate remains on build-affecting pull requests.
