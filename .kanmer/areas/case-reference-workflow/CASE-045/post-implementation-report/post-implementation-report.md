# Post-implementation report — CASE-045

Branch `task/case-045-image-initiated-principal`, worktree `.worktrees/case-045`,
head `743311a0f4ac68794672510e596abd7d89ae47bb`, PR
https://github.com/collisionengineers/pegasus/pull/671 targeting `dev`.
Base: `origin/dev` `a2658300e` (CASE-042 #663 and CASE-032 #659 both already
merged; verified via `git log origin/dev`).

## Provenance

The delegated implementer (Codex, gpt-5.6-sol, medium) produced most of
steps 1-5 in the worktree but exhausted its usage quota mid-task
(`ERROR: You've hit your usage limit ... try again at Sep 8th, 2026 10:16 AM`)
before running a single command or writing an implementation summary. A
quota probe confirmed the same limit still applied later in the session, so
retrying Codex was not viable. I (Claude, acting as the execute agent)
reviewed the entire diff against the plan and codebase conventions, fixed
the defects found (below), ran every verification command myself, ran the
simplification pass myself, and completed steps 6 and the PR.

## Files changed

- `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs` — nullable
  `PrincipalId` on `ImageIntakeRecord`; `PrincipalCode` on
  `ImageIntakeSummary`/`ImageIntakeDetail`; `SetImageIntakePrincipalRequest`;
  `IImageIntakeQueries.ListActivePrincipalsAsync` default member;
  `IImageIntakeStore.SetPrincipalAsync` default member; updated the
  now-false `IImageIntakeStore`/`ImageIntakeDetail` doc comments.
- `src/Pegasus.Core/ImageIntake/ImageIntakeLifecycle.cs` —
  `ImageIntakeLifecycleRules.ValidateSetPrincipal` (staff authorization,
  non-empty id, non-negative expected version).
- `src/Pegasus.Infrastructure/Persistence/ImageIntakeEntities.cs` — nullable
  `PrincipalId`/`Principal` navigation on `ImageIntakeEntity`.
- `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` — FK
  (Restrict) + index on `ImageIntakes.PrincipalId`.
- `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` —
  `SetPrincipalAsync` (Serializable transaction, matching every other write
  method in the file; stale-version rejection; inactive-principal
  rejection; no lifecycle event — idempotent by construction);
  `ListActivePrincipalsAsync` (IsActive, ordered by Code, reusing
  `EfOrganizationAdministration.ToPrincipal`); `PrincipalCode` added to the
  existing bulk `ProjectAsync`/`ToDetailAsync` projections (no per-row
  query).
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260905082255_ImageIntakePrincipal.{cs,Designer.cs}`,
  `PegasusDbContextModelSnapshot.cs` — nullable column + FK + index
  migration, sorted after `dev`'s tail.
- `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml(.cs)` — Principal fact
  and staff select/POST handler (`OnPostPrincipalAsync`), reusing the
  existing `OnPostCloseAsync` error/reload/antiforgery conventions
  verbatim.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — CASE-045-delimited
  block: `ImageIntakePrincipal` = "Principal",
  `ImageIntakePrincipalNotKnown` = "Not known".
- `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs` — Principal fact added to
  CASE-042's `ImageRow` (both the row subtitle and the quick-view facts);
  no `.cshtml` change needed since `QueueRow.Facts` renders generically.
- `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeLifecycleTests.cs`,
  `tests/Pegasus.IntegrationTests/{ImageIntakePersistenceTests,ImageIntakeWebTests,TriageQueuesWebTests,IntakePersistenceIntegrationTests,IntakeWebTestSupport}.cs`
  — contract test, round-trip/inactive/stale-version persistence test,
  detail-page set/replace/clear test, Awaiting row/quick-view + read-count
  test, applied-migrations + schema assertions, and the
  `commandInterceptor` constructor parameter on `IntakeWebApplicationFactory`.
- `docs/design/test-ui/pages/vehicle-images-details--default.html` —
  regenerated (full capture); the only Test UI page with a real content
  diff.

## Defects found and fixed during verification

1. **Wrong exception type asserted.** `ImageIntakePersistenceTests.cs`
   asserted `ArgumentException` for an inactive-principal write; the store
   actually (and correctly, per its own convention at
   `EfOrganizationAdministration.cs:714`) throws
   `InvalidOperationException`. Fixed the assertion.
2. **Read-count proof wired to the wrong DbContextFactory.** The
   `commandInterceptor` was originally passed only into
   `LocalDbTestDatabase`'s private service provider (used solely for
   schema-management, never touched by an HTTP request). Moved the wiring
   into `IntakeWebApplicationFactory.ConfigureWebHost`'s
   `services.ConfigureServices`, replacing the host's own
   `IDbContextFactory<PegasusDbContext>` registration with one carrying the
   interceptor. Confirmed by re-running: the count went from a silent 0 to
   a real 14.
3. **Unverified hard-coded baseline.** The test asserted `4` reader commands
   with no evidence that was CASE-042's actual baseline. Measured the true
   value directly: built a disposable detached worktree at CASE-042's exact
   merged head (`a2658300e`), ported the same interceptor-wiring fix and an
   equivalent throwaway measurement test (3 registered image rows, 1
   selected), ran it, and got 14 — identical to CASE-045's own measured
   count. Replaced the hard-coded `4` with `14` and a comment. The scratch
   worktree (`../pegasus-worktrees/case-045-baseline`) was removed
   afterward (`git worktree remove --force`, then `dotnet build-server
   shutdown` to release locked DLL handles, then `git worktree prune`).

Full detail and disposition of the simplification pass (which found no
further changes needed — every reviewed pattern already matches an
established codebase convention) is recorded in the ticket plan under
"## Simplification pass (2026-09-05)".

## Migration / grants

Migration `20260905082255_ImageIntakePrincipal` adds a nullable
`PrincipalId` column, its FK (Restrict) and index to `ImageIntakes`. No
backfill; existing rows stay null and display `Not known`. Down drops the
FK, index and column — rollback is limited to dropping the optional value.

No grant was added: verified `pegasus_web_runtime_role` and
`pegasus_worker_runtime_role` already hold `UPDATE` on `ImageIntakes`
(`scripts/Invoke-AzureDatabaseBootstrap.ps1:313-317`) and `SELECT` on
`Principals` (`Migrations/20260729199000_RuntimeRoleReconciliation.cs:252,289`).
`Test-MigrationGrants.ps1` is smoke only (this migration creates no table);
the actual verb proof is `AzureSqlRuntimeRoleMigrationTests`, which passed.

## Snapshot artifact record

Full capture run (queues-prefix known-scoping-limit rule); capture lock
held only for the capture + verify + catalogue window, released
immediately after.

- `docs/design/test-ui/pages/vehicle-images-details--default.html` — 35,027
  bytes; begins `<!DOCTYPE html>`; contains `Principal` and `Not known`
  (the field label and its absent-state value). This is the only page with
  a real content diff — every other file `git status` flagged modified was
  a pure line-ending (LF/CRLF) artifact of the full capture with zero
  content diff (`git diff --numstat` empty), reverted with `git checkout --`
  to keep the PR scoped.
- `docs/design/test-ui/pages/queues--default.html` (31,687 bytes) /
  `queues--empty.html` (29,803 bytes) — unchanged content; both catalogue
  states are "Triage tab" / "empty tab result" per
  `docs/design/test-ui/catalogue.json`, neither exercises the Awaiting tab,
  so no Principal marker is expected there. Verified via `git diff
  --numstat` (zero) that nothing regressed.

## Commands run (all exit 0)

```
dotnet restore ./Pegasus.slnx --locked-mode                                              → 0
dotnet build ./Pegasus.slnx --configuration Release --no-restore                         → 0
dotnet test tests/Pegasus.Core.Tests --configuration Release --no-build                  → 0 (1242 passed; one pre-existing unrelated flaky test — Regex timeout in QdosInstructionExtractionPolicyTests, tracked separately as DELIV-036 — failed once on the first run and passed on immediate retry, both in isolation and as part of the full re-run)
dotnet test tests/Pegasus.ArchitectureTests --configuration Release --no-build           → 0 (100 passed)
dotnet test ...IntegrationTests --filter FullyQualifiedName~ImageIntakePersistenceTests       → 0 (9 passed)
dotnet test ...IntegrationTests --filter FullyQualifiedName~ImageIntakeWebTests               → 0 (3 passed)
dotnet test ...IntegrationTests --filter FullyQualifiedName~TriageQueuesWebTests              → 0 (15 passed)
dotnet test ...IntegrationTests --filter FullyQualifiedName~IntakePersistenceIntegrationTests → 0 (10 passed)
dotnet test ...IntegrationTests --filter FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests → 0 (15 passed)
pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1                                 → 0 (94 migrations checked, every created table granted/exempted)
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 (full capture)                → 0
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture          → 0
pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1                                     → 0 (54 routed sources, 59 prototypes, 0 broken references)
```

Final combined re-run of all five changed integration classes together:
37 passed, 0 failed.

## Deviations from the plan

- Codex did not finish; I completed verification, the fixes above, step 6
  and the PR myself (see Provenance).
- The plan's step 6 said "regenerate the scoped Test UI snapshot capture";
  per the controller's SNAPSHOTS instruction (the queues-prefix known
  scoping limit), I ran the FULL capture instead, then reverted the
  line-ending-only no-op diffs it produced across unrelated pages to keep
  the committed delta to the one page that actually changed.
- No `Invoke-AzureDatabaseBootstrap.ps1` edit was made, per the plan's own
  stated condition (verified: no new grant needed).

## Not run

Full/whole integration or browser suite, and the solution-wide test
command — GitHub CI is the merge gate for those (EPIC-012 Build policy).
