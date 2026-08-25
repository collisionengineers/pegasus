# Post-implementation report — PR-061

## Outcome
Export now re-reads and validates the CaseWorkflow state under the same serializable UPDLOCK/HOLDLOCK transaction that owns replay, proxy and history recording. A state other than Review throws the existing CaseNotInReviewException before any export record is written.

## Files
- `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs`: the existing lock query returns State and RecordExportAsync validates Review.
- `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs`: holds the workflow lock, starts Export, commits a NotReady demotion, and proves Export fails without proxy/history; existing success/replay coverage then continues.

## Evidence
Release build passed with 0 warnings/errors. Focused Integration project build passed with 0 warnings/errors. `ExportingACaseProducesTheEvaFormatArchive` passed (1/1, 23s). Diff checks passed. Simplification found no new abstraction, schema, retry or compatibility path. Commit `cc6b0ee7`, PR #539. Not deployed.
