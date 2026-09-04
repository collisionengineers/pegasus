# Checklist — CASE-039 (2026-09-02)

- [x] CASE-038 merged to `origin/dev`; ticket branch refreshed with
  `git merge --no-edit origin/dev`; shared locks (`Pages/Cases/Shared/*`,
  `Presentation/OperatorLabels.cs`, `Persistence/Migrations/**`) held.
- [x] `src/Pegasus.Core/Cases/EngineerNotes.cs`: add request (case id,
  staff actor, expected Case version, operation key, note, edit-lease
  token), `IAddEngineerNote`, `IEngineerNoteStore`, list query port and
  projection; staff-only `PerformCasework`; trim, require, 2,000-character
  limit; the append does not increment the Case version.
- [x] `tests/Pegasus.Core.Tests/Cases/EngineerNotesTests.cs`: staff-only,
  rejected actor kinds, blank/overlong text, trimming, operation-key,
  expected-version and lease-token forwarding, newest-first contract.
- [x] `EngineerNoteEntities.cs`, `EngineerNotesModelConfiguration.cs`,
  `PegasusDbContext.cs`: separate `EngineerNotes` entity, Case FK,
  Case/operation-key replay constraint, `(CaseId, OccurredAtUtc, Id)` index.
- [x] `EfEngineerNoteStore.cs`: follow `EfRecordEngineerFinding.cs` —
  `IDbContextFactory`, `Serializable` transaction, normalized request hash,
  exact-replay return before the guards, `CaseOperationConflictException` on
  same key with a different payload, winner re-read after a uniqueness race.
- [x] `EfEngineerNoteStore.cs` guards in order: `StaffAuthorization` +
  `ArchivedCaseGuard.RequireNotArchived` + `CaseMutationGuard.RequireVersion`
  + `CaseMutationGuard.RequireLease` (never `Require`), then insert and
  `CaseMutationGuard.ClearLease(workflow)` in the same transaction; list
  newest first; no `CaseWorkflowEvents` write.
- [x] `DependencyInjection.cs`: register the store, command and query.
- [x] One migration `<ts>_EngineerNotes` (+ Designer, snapshot): table, FK,
  constraints, index; SQL Server-only `RequireRuntimeRole` and
  `GRANT SELECT, INSERT` to `pegasus_web_runtime_role`; matching REVOKE in
  `Down`; no worker grant, no UPDATE/DELETE.
- [x] `scripts/Invoke-AzureDatabaseBootstrap.ps1`: add the
  `pegasus_web_runtime_role|G|SELECT|EngineerNotes` and `|G|INSERT|EngineerNotes`
  census rows with the caller comment; no worker row, no UPDATE/DELETE.
- [x] `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs`:
  add `EngineerNotes` to the expected schema table list and to the web-role
  grant list only.
- [x] `tests/Pegasus.IntegrationTests/EngineerNotePersistenceTests.cs`:
  attribution, exact replay, same-key altered-payload conflict, stale-version
  refusal, missing/expired-lease refusal, persisted lease cleared after a
  successful append, terminal-state case with a held lease accepts a note, a
  correction under a new operation key appends a second row, ordering,
  separate-table destination, no row in `CaseWorkflowEvents`.
- [x] `Presentation/OperatorLabels.cs` (`CaseWorkspace`): section title,
  add action, dialog title, field label, singular/plural count, outcome
  messages; no `HistoryEvent` entry.
- [x] `Pages/Cases/Details.cshtml.cs`: `engineer-notes` section key, notes
  load through the merged CASE-038 lazy-section contract, actor names via
  `ActorDisplayNames.ResolveStaffNamesAsync`, `OnPostAddEngineerNoteAsync`
  through `ExecuteCaseCommandAsync` (id, expectedVersion, operationKey, note,
  editLeaseToken).
- [x] `Pages/Cases/Shared/_CaseEngineerNotes.cshtml`: Date, Time, ID, text
  rows newest first using `_CaseHistory` classes; nothing rendered for an
  empty read-only list; add form in edit mode only; no CSS/JS.
- [x] `Details.cshtml` / `_CaseWorkspaceNav.cshtml`: section slot and nav
  entry in the exact shape CASE-038 merged; no frame change (`_CaseWorkspaceNav.cshtml`
  needed no edit — its Engineer-notes entry was already correctly placed).
- [x] `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`: section
  renders, no empty-state prose, resolved actor name, no Notes-history
  leakage, append form present and no edit or delete affordance, leased
  staff-only POST with antiforgery, operation key and expected version.
- [x] UIIMP-014 handoff recorded for
  `docs/design/test-ui/pages/case-details--default.html` before the
  regenerated snapshot is committed; no new snapshot states or
  `catalogue.json` entries. Deviation: the lazy Engineer-notes section also
  legitimately changes the already-catalogued `case-details--conflict.html`
  state (not a new state), so that file was regenerated too — see
  post-implementation report.
- [ ] Browser journey assertion only if UIIMP-014's holder agrees the
  existing route reaches the section. Not done — deferred to UIIMP-014
  coordination; no browser-journey test was added or required by the
  focused local checks run for this ticket.
- [x] Run `dotnet restore ./Pegasus.slnx --locked-mode` (exit code recorded).
- [x] Run `dotnet build ./Pegasus.slnx --configuration Release --no-restore`.
- [x] Run the Core and Architecture test projects plus the focused
  integration classes this ticket changed (`EngineerNotePersistenceTests`,
  `CaseDetailsWebTests`, `AzureSqlRuntimeRoleMigrationTests`) instead of the
  full solution-wide `Category!=Corpus` filter — per this run's binding
  orchestrator instruction that the unfiltered/full suite duplicates CI and
  is not to be run locally; CI runs the canonical gate on the PR.
- [x] Run `./scripts/Test-MigrationGrants.ps1`.
- [x] Run `./scripts/Update-TestUiSnapshots.ps1` (scoped to `case-details`);
  commit the regenerated `docs/design/test-ui/pages/case-details--default.html`
  and `case-details--conflict.html` with the page change.
- [x] Run `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture`
  (scoped to `case-details`).
- [x] Run `./scripts/Test-UiCatalogue.ps1`.
- [x] Simplification pass recorded in the plan under a dated heading.
- [x] post-implementation report written
- [x] PR opened with Kanmer: CASE-039

- [x] Review round fix (2026-09-04): applied finding 5 (blocker) —
  appended `20260904210022_EngineerNotes` to the applied-migrations list in
  `IntakePersistenceIntegrationTests.cs`; re-ran the fast checks plus the
  focused integration filter including `IntakePersistenceIntegrationTests`
  (104 passed); committed `ae38f570e` and pushed to
  `origin/task/case-039-engineer-notes`.
