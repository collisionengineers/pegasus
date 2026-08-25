# Post-implementation report — PR-061

## Outcome
Export now re-reads and validates the CaseWorkflow state under the same serializable UPDLOCK/HOLDLOCK transaction that owns replay, proxy and history recording. A state other than Review throws the existing CaseNotInReviewException before any export record is written.

## Files
- `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs`: the existing lock query returns State and RecordExportAsync validates Review.
- `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs`: holds the workflow lock, starts Export, commits a NotReady demotion, and proves Export fails without proxy/history; existing success/replay coverage then continues.

## Evidence
Release build passed with 0 warnings/errors. Focused Integration project build passed with 0 warnings/errors. `ExportingACaseProducesTheEvaFormatArchive` passed (1/1, 23s). Diff checks passed. Simplification found no new abstraction, schema, retry or compatibility path. Commit `cc6b0ee7`, PR #539. Not deployed.

## Final review and merge evidence — 2026-08-25
Independent Kanmer review passed on final head `cc6b0ee75edd413537a16445a42f95a329c309fe`. GitHub reported all 11 checks successful: changes, documentation, local-development-scripts, reference-data, infrastructure, unit, three SQL integration shards, browser, and sql-integration-coverage. PR #539 merged to `dev` at 2026-08-25T00:47:21Z as merge commit `d973ead358f75736bdbdec3aa123d7d88a0083bd`. Deployment is not claimed; merged-dev verification and proof remain next.
