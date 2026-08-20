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

# Independent re-review — PR #477 at `fc3b651eda785ad37fbe7c302aec38e2876abc20` — 2026-08-20

This is an independent re-review; I did not implement the replacement head.

## Changes since the previous reviewed head

- `src/Pegasus.Core/Intake/RetainedMail.cs`: projects current logical folder and makes move availability compare the exact approved destination with durable current location.
- `src/Pegasus.Core/Intake/RetainedMailFolderMove.cs`: carries safe operation/recovery freshness fields and adds the current-location query.
- `src/Pegasus.Infrastructure/Persistence/EfRetainedMailFolderMoveStore.cs`: chains source from the latest success, reserves before provider probing, supports replay material, and permits later reclassification moves.
- `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs`: keeps ordinary Inbox browse exclusive while including moved retained rows through non-empty MAIL-11 search and projecting current logical folder.
- `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs`: persists the safe freshness material needed for same-key recovery.
- `src/Pegasus.Infrastructure/Persistence/MailboxModelConfiguration.cs`: adds the filtered unique active-operation index and freshness-field configuration.
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260820144004_RetainedMailFolderMoves.cs`, its Designer, and `PegasusDbContextModelSnapshot.cs`: carry the filtered index and recovery columns in the existing unmerged migration stream.
- `src/Pegasus.Web/Pages/Mail/Index.cshtml`: explains that retained Inbox search spans current folders.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml`: renders failure detail and an authenticated same-key status-check form for uncertain results.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs`: keeps a moved matching message inside its originating search context.
- `tests/Pegasus.Core.Tests/Intake/RetainedMailFolderMoveTests.cs`: updates the focused test fake for the current-location query.
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs`: proves destination/source/unresolved same-key uncertain recovery and current-folder search presentation.
- `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs`: proves different-key claim serialization, stale/current-location refusal, provider failure/new-key retry, immutable classification/arrival evidence, repeat move after reclassification, and search inclusion/paging/mailbox scope.

## Comments and disposition

1. **PR-038 — fixed-in-PR for different keys.** The filtered unique index covers pending/uncertain rows per retained message, terminal failure releases the slot, reservation precedes the exact provider-location probe, and the overlapping different-key test plus schema assertion proves one provider call. The new PR-043 finding below is a distinct same-key in-flight race.
2. **PR-039 — fixed-in-PR for genuinely uncertain operations.** The page posts the original operation key, reason and safe freshness values; the request hash rejects tampering; destination/source/unresolved authenticated cases probe only and assert one move.
3. **PR-040 — fixed-in-PR.** Current source derives from latest successful destination, exact current/destination comparison controls `CanMove`, and the test proves two separate confirmations after reclassification while immutable arrival identity remains unchanged.
4. **PR-041 — fixed-in-PR.** Non-empty retained Inbox search reuses MAIL-11’s canonical query, includes each moved row once, preserves mailbox scope/count/paging, and projects current logical folder. Ordinary Inbox browsing continues to exclude it; no new taxonomy/search store/tab was introduced.
5. **PR-042 — fixed-in-PR.** Named persistence and Web tests now cover material stale/conflict/failure/recovery/retry/preservation paths. TICK-049’s final PIR lists all 23 final changed files in accurate grouped rationales and gives qualified observed counts, including the corrected stale-copy assertion.
6. **Blocking — same-key replay can resolve a live provider call.** At `EfRetainedMailFolderMoveStore.cs:59-62`, both `pending` and `uncertain` immediately call `RecoverAsync`. A duplicate same-key POST during the original provider call can see the source folder, mark the shared row failed, release the filtered slot, and admit a new-key retry before the original call completes. **Disposition:** filed [[PR-043]], which blocks TICK-049.
7. **Simplicity — pass.** The fixes reuse the one concrete MAIL-07 store, MAIL-05 recommendation, MAIL-11 retained search, exact typed bindings, existing Graph mover/probe and shared dialog. The filtered index is the database boundary required by the external side effect; no generic operation framework, worker, second search implementation, category/folder list or destination UI was added.
8. **Safety/composition — pass.** Graph identities remain server-resolved; the browser receives no transport destination/source identity; the live Graph mover remains unregistered in production composition; migration/bootstrap grants remain Web SELECT/INSERT/UPDATE with Web/Worker DELETE denial and no Worker write grant; all evidence is fake/local.
9. **CI status.** Replacement run 32391719482 was still active when this needs-changes verdict was finalized. Changes, reference-data, local-development-scripts and infrastructure were green; documentation, unit, browser and SQL shards 1–3 were still running. A later replacement head must obtain a complete green run.

## Governing docs and report check

The replacement satisfies FRD-08’s separate confirmation, later reclassification move, destination/search findability, preserved classification/arrival evidence, and no arbitrary destination. Its Core/Infrastructure/Web placement and existing Graph client extension remain within the accepted architecture, so no ADR is missing. The final plan’s governing-doc and simplification sections match the diff, and the PIR inventory matches the 23 files in the full PR diff. PR-043 remains the sole blocking correctness gap found at this head.

## Verdict

**Needs changes.** PR #477 was not merged. TICK-049 and PR-038 through PR-042 remain in Review; [[PR-043]] blocks TICK-049 from passing. Fix PR-043, run complete replacement CI, and independently re-review the next exact head.

# Independent re-review — PR #477 at `83293162c3059d52b05d5139e2d1b8ee56b8d5a9` — 2026-08-20

This is an independent re-review; I did not implement this head.

## Changes

- `src/Pegasus.Infrastructure/Persistence/EfRetainedMailFolderMoveStore.cs`: refuses same-key replay while the row is Pending, permits recovery only for Uncertain, and persists Pending→Uncertain after a non-cancellation provider exception before probing.
- `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs`: overlaps the original provider call with same-key and new-key requests, asserting Pending remains active, only one parent probe/move occurs, and completed replay is terminal.

The full PR remains the same 23-file inventory recorded in the final TICK-049 PIR; this head adds no new file or scope. The prior independent dispositions for PR-038 through PR-042 remain passing.

## Comments and disposition

1. **PR-043 — fixed-in-PR for overlapping requests.** Pending replay now returns “still being processed” without probing or mutating. The deterministic blocking mover proves an overlapping same-key request leaves the row Pending, a new key remains blocked, one source probe and one move occur, and a replay after completion returns the existing success.
2. **Blocking — request cancellation can strand Pending forever.** The provider-call/complete block still rethrows `OperationCanceledException` when the request token is cancelled. Cancellation during the external call, or after Graph succeeds while SQL completion uses the cancelled token, leaves the durable row Pending. Because this head correctly refuses all Pending replays and keeps the filtered slot occupied—and deliberately adds no lease/worker—neither same-key recovery nor a new-key retry can ever proceed. **Disposition:** filed [[PR-044]], which blocks TICK-049.
3. **Simplicity — pass except for the missing terminal path.** PR-043 uses two explicit existing states and one durable transition; it adds no flag, wrapper, lease, timer, worker, endpoint or framework. PR-044 should preserve that narrow shape by making the post-provider transition durable independently of request abandonment.
4. **Prior blockers and governing docs — pass.** PR-038 through PR-042 remain correctly addressed: database-enforced different-key serialization, authenticated same-key Uncertain probing, repeat confirmation after reclassification, MAIL-11 search findability, exact failure-path tests, qualified evidence and the complete 23-file PIR inventory. Exact transport identities stay server-side; production composition remains unavailable/default-off; no external write is claimed.
5. **CI status.** Replacement run 32393959663 had only just started when this needs-changes verdict was finalized. Local-development-scripts and reference-data were green; changes and documentation were running and downstream build/test jobs had not all started. The next head requires a complete green replacement run.

## Verdict

**Needs changes.** PR #477 was not merged and no ticket moved. [[PR-044]] is the sole new blocking finding at this head. After it is fixed with cancellation evidence and the replacement CI run is fully green, run another independent review of the exact new head.
