# Files — PR-055

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs` | Wrap only export audit/proxy recording in a serializable transaction and lock the existing case/workflow aggregate before checking replay history. Preserve bundle generation, readiness, proxy semantics and exact-replay comparison. |
| `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` | Add real SQL concurrency coverage for two simultaneous identical exports and conflicting reuse, alongside the existing end-to-end EVA bundle assertions. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs` | `AcquireWorkflowMutationLockAsync` is the established SQL Server `UPDLOCK, HOLDLOCK` pattern for serializing operations on one case; it is private, so this ticket should follow the convention without refactoring the workflow store. |
| `src/Pegasus.Infrastructure/Persistence/EfApprovedOutlookCategoryStore.cs` | Shows the full existing convention: serializable transaction, aggregate lock before replay lookup, save, then commit. |
| `src/Pegasus.Infrastructure/Persistence/DocumentActionHistory.cs` | Owns the exact-replay material comparison and conflict exception already used by Export; do not duplicate or weaken it. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | Confirms ActionHistory's correlation index is non-unique; this focused fix does not change the shared schema. |
| `docs/frd/frd-07-eva-and-external-engineering-handoff.md` | Governing behavior: each successful Export has replay-safe action history and only the first records the send proxy. |
| `docs/engineering.md` | Requires persistence/action-history atomicity and explicit concurrency coverage while keeping one coherent, proportionate implementation. |

## Ripple effects

- The Export page and `IExportCaseBundle` contract do not change; callers keep sending the rendered operation key.
- Existing sequential replay, second-distinct-export, proxy and bundle-shape assertions remain relevant.
- The test must use the repository's SQL integration harness/two independently created contexts; an in-memory fake cannot prove database locking.
- No migration, model snapshot, release artifact, configuration, or governing-document edit follows from this implementation.

## Out of scope

- Changing Review readiness, EVA field mapping, suggested values, custody selection or image loading.
- Reintroducing the removed handoff tables or adding a new export-operation table.
- A repository-wide ActionHistory uniqueness migration or generic locking/idempotency abstraction.
- EVA API/direct estimating integrations and release/deployment work.
