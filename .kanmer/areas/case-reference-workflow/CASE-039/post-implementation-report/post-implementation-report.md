# Post-implementation report — CASE-039

## Outcome

Implemented on `task/case-039-engineer-notes` in
`.worktrees/case-039`, PR opened against `dev`:
https://github.com/collisionengineers/pegasus/pull/669

Head SHA: `7a00b2873f625e26a946089c77ae003a724270f2`

## Files changed

- `src/Pegasus.Core/Cases/EngineerNotes.cs` (new) — add-note request,
  `AddEngineerNote` command, `IEngineerNoteStore`, `IEngineerNoteQueries`,
  `EngineerNote` projection, staff-only authorization, text trim/require/
  2,000-char validation.
- `src/Pegasus.Infrastructure/Persistence/EngineerNoteEntities.cs` (new) —
  `EngineerNoteRow` persistence entity.
- `src/Pegasus.Infrastructure/Persistence/EngineerNotesModelConfiguration.cs`
  (new) — table mapping, Case FK, replay-key uniqueness constraint,
  retrieval index.
- `src/Pegasus.Infrastructure/Persistence/EfEngineerNoteStore.cs` (new) —
  transactional append: staff authorization, not-archived, version and
  lease guards (`RequireLease`, never `Require`), exact-replay short
  circuit, `CaseOperationConflictException` on same key/different payload,
  insert + `CaseMutationGuard.ClearLease` in one transaction, no Case
  version increment, no `CaseWorkflowEvents` write, newest-first query.
- `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` — new DbSet
  and model configuration registration.
- `src/Pegasus.Infrastructure/DependencyInjection.cs` — command/store/query
  registrations.
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260904210022_EngineerNotes.cs`
  + `.Designer.cs`, `PegasusDbContextModelSnapshot.cs` — table, FK,
  constraints, index, and `GRANT SELECT, INSERT` to
  `pegasus_web_runtime_role` only (matching `REVOKE` in `Down`; no worker
  grant, no UPDATE/DELETE).
- `scripts/Invoke-AzureDatabaseBootstrap.ps1` — the two `EngineerNotes`
  web-runtime census rows only, with a caller comment.
- `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` —
  new focused test asserting the `EngineerNotes` table exists with exactly
  `SELECT`/`INSERT` granted to the web role and nothing to the worker role.
- `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` — lazy `engineer-notes`
  section load, staff display-name resolution via
  `ActorDisplayNames.ResolveStaffNamesAsync`, leased
  `OnPostAddEngineerNoteAsync` POST handler through the existing
  `ExecuteCaseCommandAsync` mutation path.
- `src/Pegasus.Web/Pages/Cases/Details.cshtml` — Engineer-notes section
  route wiring (no frame change).
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseEngineerNotes.cshtml` (new) —
  newest-first note list reusing `_CaseHistory` row classes, no empty-state
  prose, edit-mode-only antiforgery-protected add form, no edit/delete
  affordance.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — one CASE-039-delimited
  label block (`EngineerNotesSectionTitle`, `AddEngineerNote`,
  `AddEngineerNoteTitle`, `EngineerNoteField`, `EngineerNoteAdded`,
  `EngineerNoteCount`); `CaseWorkspace.Sections` now references
  `EngineerNotesSectionTitle` instead of a duplicated literal.
- `tests/Pegasus.Core.Tests/Cases/EngineerNotesTests.cs` (new) —
  authorization, validation, normalization, mutation-envelope forwarding.
- `tests/Pegasus.IntegrationTests/EngineerNotePersistenceTests.cs` (new) —
  attribution, exact replay, same-key/altered-payload conflict, stale
  version, missing/expired lease, lease clearing, terminal-state case with a
  held lease, correction under a new operation key, ordering, separate-table
  destination, no `CaseWorkflowEvents`/history row.
- `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` — section render,
  no empty-state prose, resolved actor name, no Notes-history leakage, add
  form present with no edit/delete affordance, leased staff-only POST with
  antiforgery/operation-key/expected-version.
- `docs/design/test-ui/pages/case-details--default.html` and
  `case-details--conflict.html` — regenerated (see Deviations).

`_CaseWorkspaceNav.cshtml` was not touched: it already renders the single
`OperatorLabels.CaseWorkspace.Sections` list and the Engineer-notes entry
was already in the correct D30 position from an earlier merge.

## Commands and exit codes

All run in `C:/Users/PC/Documents/GitHub/pegasus/.worktrees/case-039`
after `git merge --no-edit origin/dev` (fast-forward, exit 0).

| Command | Exit | Result |
| --- | ---: | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | up to date |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | 0 warnings, 0 errors |
| `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` | 0 | 1,234 passed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` | 0 | 100 passed |
| `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~EngineerNotePersistenceTests\|FullyQualifiedName~CaseDetailsWebTests\|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests" -- xUnit.MaxParallelThreads=2` | 0 | 94 passed |
| `pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1` | 0 | 92 migration files checked, every created table granted or exempted |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Scope case-details -CaptureFilter "FullyQualifiedName~CaseDetailsWebTests\|FullyQualifiedName~TestUiFocusedRenderTests"` | 0 | 78 capture tests + 1 snapshot-update test passed |
| `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope case-details` | 0 | verify test passed |
| `pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1` | 0 | 54 routed sources, 59 prototypes, 0 broken references |
| `git status --porcelain` (final) | 0 | clean — only owned paths were ever modified |
| `git diff --check` | 0 | no whitespace conflicts |

The full solution-wide `dotnet test ... --filter "Category!=Corpus"` and the
unfiltered/browser integration suite were intentionally not run locally —
this run's binding orchestrator instruction reserves those for CI, which
runs them on the PR as the merge gate.

## Deviations from the plan

1. **Bootstrap-census carve-out.** The first implementation attempt
   (Codex) correctly stopped: the packet's general "never edit
   `scripts/*.ps1`" tooling rule appeared to contradict the migration's
   required bootstrap-census rows. This controller re-issued the packet
   with the CLAUDE.md rule 16 carve-out spelled out explicitly (add only
   the two `EngineerNotes` web-runtime census rows plus the matching
   `AzureSqlRuntimeRoleMigrationTests` rows), which is what shipped. No
   other `scripts/*.ps1` file was touched.
2. **`case-details--conflict.html` regeneration.** `files.md` names only
   `case-details--default.html` for this ticket and reserves other
   `docs/design/test-ui/**` states to UIIMP-014. In practice the lazy
   Engineer-notes section renders in every Case Details catalogue state,
   so the already-existing `case-details--conflict.html` snapshot (not a
   *new* state — UIIMP-014 owns adding new states/`catalogue.json`
   entries, not refreshing this ticket's own rendering delta in an
   existing one) no longer matched actual output; `-Verify` failed on it.
   Regenerated it as well so `-Verify` and CI's snapshot gate pass; no
   other `docs/design/test-ui/**` file was kept modified (an incidental
   `index.html` and `case-details--unavailable.html` rewrite from the
   capture step produced byte-identical content — confirmed via
   `git diff --numstat` showing no change — and was reverted with
   `git checkout --`).
3. Two simplification findings applied (see plan doc, "Simplification pass
   (2026-09-04)"): removed an unused `EngineerNoteDisplay.Id` field, and
   deleted a tautological reflection-based test.
4. Checklist item "Browser journey assertion only if UIIMP-014's holder
   agrees the existing route reaches the section" was left undone — no such
   agreement was sought or needed for the focused local checks this run;
   left for UIIMP-014's own scope.

## Snapshot artifact facts

- `docs/design/test-ui/pages/case-details--default.html`: 65,498 bytes,
  begins with `<!doctype html>`, contains `class="case-sticky"` (1 match),
  16 unique `id="section-*"` hosts, zero `<img src="#">`, and the
  `engineer-notes` section/title/add-form/field/limit/version/lease/
  operation-key/antiforgery markers.
- `docs/design/test-ui/pages/case-details--conflict.html`: 40,012 bytes,
  begins with `<!doctype html>`, same `case-sticky`/section-host counts,
  zero `<img src="#">`, and the Engineer-notes markers present.

## PR

https://github.com/collisionengineers/pegasus/pull/669

## Review round fixes (2026-09-04)

Applied the one blocking review finding; findings 1-4 needed no
implementer action (one rejected with reason, three accepted as
documentation nits — see the review record).

**Finding 5 (blocker) — `IntakePersistenceIntegrationTests.cs:121`.**
`CommittedMigrationCreatesTheSqlServerSchema`'s exhaustive
applied-migrations list ended at `20260903233954_MarketResearchAiJob` and
did not include this ticket's own `20260904210022_EngineerNotes`
migration, so CI's `sql-integration (1)` lane failed (run 33924646833,
`Assert.Equal()` collection mismatch at position 91).

Fix: appended `"20260904210022_EngineerNotes"` as the new, chronologically
last entry in that list (confirmed against
`src/Pegasus.Infrastructure/Persistence/Migrations/20260904210022_EngineerNotes.cs`).
No other line in the file, and no other file, was touched. This is a test
kept in sync with the schema this ticket ships, not a weakened assertion —
per EPIC-012 Build policy, which names this exact list as in-ticket-scope
merge prep.

Only `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`
changed in this round; `git status --porcelain` confirmed no other file was
modified before commit.

### Commands and exit codes (review round)

| Command | Exit | Result |
| --- | ---: | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | up to date |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | 0 warnings, 0 errors |
| `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` | 0 | 1,234 passed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` | 0 | 100 passed |
| `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~IntakePersistenceIntegrationTests\|FullyQualifiedName~EngineerNotePersistenceTests\|FullyQualifiedName~CaseDetailsWebTests\|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests" -- xUnit.MaxParallelThreads=2` | 0 | 104 passed (includes the now-passing `CommittedMigrationCreatesTheSqlServerSchema`) |

No routed Razor page, partial, or `catalogue.json` changed in this round,
so no snapshot regeneration was required.

Commit: `ae38f570e` on `task/case-039-engineer-notes`, pushed to
`origin/task/case-039-engineer-notes`. The review resumes at this new head.
