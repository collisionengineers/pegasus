# Files — PR-058

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` | Replace the serial eligible-image read loop with the existing one-batch call and reuse the established address-to-read and row-to-bundle helpers. Risk: row/content index drift; preserve the query's occurrence-ordinal order and project returned contents by the same index. |
| `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` | Extend the existing EVA persistence source-shape check to require one batch content-store call and forbid the serial content-store call. This directly guards the accidental regression without building test-only storage infrastructure. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Core/Documents/DocumentContracts.cs` | `ManagedDocumentContentRead` carries the exact address, expected SHA-256 and expected length; `ReadVersionsAsync` is already the common port and has a safe default. |
| `src/Pegasus.Infrastructure/Custody/BoxDocumentContentStore.cs` | The production override resolves/lists once, bounds concurrent downloads, preserves request order and verifies every returned payload. Do not duplicate this logic in the exporter. |
| `tests/Pegasus.IntegrationTests/BoxDocumentContentStoreTests.cs` | Existing tests prove the batch's request cost, order equivalence and fail-closed hash/missing-content behavior. |
| `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` | `ExportingACaseProducesTheEvaFormatArchive` remains the end-to-end proof that ordered image bytes reach the archive. |
| `src/Pegasus.Core/Eva/EvaBundleSchema.cs` | Core owns image eligibility and deterministic bundle construction; batching must not change either. |
| `docs/frd/frd-07-eva-and-external-engineering-handoff.md` | Export must include every eligible retained Case-vehicle image in the deterministic package; storage verification is technical content loading, not another business-readiness gate. |
| `origin/dev:src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` and commit `4d3a3d04` | Exact prior implementation to restore: select rows, create one ordered read set, read once, pair by index. |

## Ripple effects

- The `IExportCaseBundle` caller and HTTP Export page do not change.
- Box production exports regain one Case-folder resolution/listing and bounded concurrent downloads.
- Non-Box stores keep working through the interface's default batch implementation.
- Existing hash, length, missing-file, eligibility, archive-order and end-to-end export tests remain authoritative; run the focused architecture, Box content-store and EVA export tests.
- No governing-document change is needed because the FRD behavior is unchanged.

## Out of scope

- No change to Review readiness, suggested values, mileage/VAT rules, custody semantics, action history, replay handling, EVA API work, direct estimating integrations, or the archive schema.
- No new abstraction, caching, queueing, background work, configuration, or compatibility path.
- No general refactor of other one-at-a-time document consumers; their streaming or memory requirements differ and this ticket concerns only the EVA archive caller.
