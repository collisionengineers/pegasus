# Independent review — PR #477 at `5e8217a1d3f23caf7a137b24cdc79366175c35c8` — 2026-08-20

This is an independent review; I did not implement TICK-049.

## Changes

- `docs/capabilities.md`: changes MAIL-07 scheduling/evidence wording to local implementation, explicitly not deployed.
- `docs/current-architecture.md`: records the local/default-off confirmed-move shape.
- `scripts/Invoke-AzureDatabaseBootstrap.ps1`: adds the migration-defined Web grants and Web/Worker DELETE denials to the exhaustive bootstrap matrix.
- `src/Pegasus.Core/Intake/RetainedMail.cs`: adds mailbox freshness/move availability to the recommendation and latest move result to detail.
- `src/Pegasus.Core/Intake/RetainedMailFolderMove.cs`: adds the concrete request/result, mover/store ports, validation use case, and unavailable mover.
- `src/Pegasus.Infrastructure/DependencyInjection.cs`: composes the EF store/use case with an unavailable mover by default.
- `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs`: extends the existing Graph client with exact folder-scoped move and immutable-id parent-folder probe; the available Graph mover remains unregistered outside tests.
- `src/Pegasus.Infrastructure/Persistence/EfRetainedMailFolderMoveStore.cs`: resolves classification/policy/binding/current location server-side, persists operations, calls/reconciles the provider, and writes action history.
- `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs`: excludes messages with a succeeded move from Inbox queries.
- `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs`: adds the concrete folder-move entity.
- `src/Pegasus.Infrastructure/Persistence/MailboxModelConfiguration.cs`: configures lengths, operation-key uniqueness, message/time index, and restrictive retained-message FK.
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260820144004_RetainedMailFolderMoves.cs`: creates the operation table/indexes and runtime grants/denials.
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260820144004_RetainedMailFolderMoves.Designer.cs`: generated target model for the migration.
- `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`: updates the current EF model snapshot.
- `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`: exposes the new DbSet.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml`: renders move status and a shared reason-dialog confirmation without transport identities.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs`: binds freshness/operation inputs, invokes the Core use case, renders outcomes, and treats moved Inbox detail as outside the originating scope.
- `tests/Pegasus.Core.Tests/Intake/RetainedMailFolderMoveTests.cs`: covers Core authorization/input normalization and validation.
- `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs`: adds exact runtime grant/deny assertions.
- `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`: updates the expected committed migration set.
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs`: adds one authenticated happy-path caller test.
- `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs`: adds fake-handler move/probe request-shape tests.
- `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs`: adds one successful move/replay/Inbox exclusion/arrival preservation test.

## Comments and disposition

1. **Blocking — concurrent claim race.** The existing pending/succeeded checks and insert are not atomic across different operation keys; only `OperationKey` is unique, so two confirmations can both call Graph. **Disposition:** filed [[PR-038]], which blocks TICK-049.
2. **Blocking — uncertain recovery is unreachable from Web.** The result instructs same-confirmation retry, but the redirected page generates a new key and the store rejects it while the original remains uncertain. **Disposition:** filed [[PR-039]], which blocks TICK-049.
3. **Blocking — later reclassification cannot be moved.** FRD-08 requires a new separate confirmation when a later classification changes the designated folder. The store permanently refuses any message with one success and Razor hides the action after any latest success. **Disposition:** filed [[PR-040]], which blocks TICK-049.
4. **Blocking — successful move is not findable through destination scope/search.** The only query overlay is Inbox exclusion; the current list/search vocabulary has no designated destination scope, so the message disappears from all list/search entry points. **Disposition:** filed [[PR-041]], which blocks TICK-049.
5. **Blocking — plan/report evidence is overstated.** The PIR claims stale/conflict/concurrency/failure/uncertain-recovery/retry/classification-preservation evidence that the added tests do not contain, and it does not give an exact changed-file rationale for all 23 changed files. **Disposition:** filed [[PR-042]], which blocks TICK-049.
6. **Non-blocking positive finding — exact server-side binding.** The browser posts no mailbox/source/destination Graph identity; the store resolves the retained row, exact ordinal mailbox identity, approved state/version, typed binding, and current provider parent.
7. **Non-blocking positive finding — restricted/default-off composition.** The migration grants Web only SELECT/INSERT/UPDATE on the operation table, denies deletion to Web/Worker, gives Worker no write grant, and the production composition registers only the unavailable mover. The available Graph mover appears only in fake/local tests.
8. **Non-blocking positive finding — simplicity.** The diff reuses the existing classification policy, approved-mailbox binding, Graph client, retained-mail store, reason dialog, ActionHistory and Core/Infrastructure/Web boundaries. It introduces one concrete external-operation record because SQL and Graph cannot be atomic, and no generic mail-action framework/runtime/taxonomy duplication.
9. **CI status.** Replacement run 32382992598 was observed through its requested gate. Changes, reference-data, local-development-scripts, unit, infrastructure, browser and SQL shard 1 were green. Documentation failed after 10m7s inside `actions/checkout@v7` before either repository documentation test ran, indicating checkout/infrastructure failure rather than a demonstrated ticket defect; GitHub withholds logs until the still-running workflow finishes. SQL shards 2/3 were still running when this needs-changes verdict was finalized. The PR cannot merge on either the blocking findings or this non-green run.

## Governing docs and report check

The chosen thin Core/Infrastructure/Web design is authorized by FRD-08 and does not require a new ADR. The default-off/no-live-write documentation wording is appropriately qualified, and no external mailbox/cloud/permission/deployment write occurred. However, FRD-08 lines 246–252 require repeat confirmation after reclassification and destination-scope/search findability, which the current diff misses. The post-implementation report broadly describes the files but overclaims failure-path evidence and omits exact rationales for several changed support/test files; [[PR-042]] owns reconciliation.

## Verdict

**Needs changes.** PR #477 was not merged and TICK-049 remains in Review. Resolve [[PR-038]], [[PR-039]], [[PR-040]], [[PR-041]], and [[PR-042]], obtain a fully green replacement CI run, then run an independent re-review at the new head.

### CI status correction at handoff

GitHub subsequently finalized the documentation job as **cancelled**, not failed; it still ended inside `actions/checkout@v7` before repository documentation checks ran. SQL shard 2 also completed green. SQL shard 3 remained in progress. This does not change the needs-changes verdict or no-merge decision.
