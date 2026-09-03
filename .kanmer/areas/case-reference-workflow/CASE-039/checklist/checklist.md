# Checklist — CASE-039 (2026-09-02)

- [ ] CASE-038 merged to `origin/dev`; ticket branch refreshed with
  `git merge --no-edit origin/dev`; shared locks (`Pages/Cases/Shared/*`,
  `Presentation/OperatorLabels.cs`, `Persistence/Migrations/**`) held.
- [ ] `src/Pegasus.Core/Cases/EngineerNotes.cs`: add request (case id,
  staff actor, operation key, note, edit-lease token), `IAddEngineerNote`,
  `IEngineerNoteStore`, list query port and projection; staff-only
  `PerformCasework`; trim, require, 2,000-character limit; version-neutral.
- [ ] `tests/Pegasus.Core.Tests/Cases/EngineerNotesTests.cs`: staff-only,
  rejected actor kinds, blank/overlong text, trimming, operation-key and
  lease-token forwarding, newest-first contract.
- [ ] `EngineerNoteEntities.cs`, `EngineerNotesModelConfiguration.cs`,
  `PegasusDbContext.cs`: separate `EngineerNotes` entity, Case FK,
  Case/operation-key replay constraint, `(CaseId, OccurredAtUtc, Id)` index.
- [ ] `EfEngineerNoteStore.cs`: `IDbContextFactory`, load the workflow row,
  `StaffAuthorization` + `ArchivedCaseGuard.RequireNotArchived` +
  `CaseMutationGuard.RequireLease` (not `Require`), replay check, insert;
  list newest first; no `CaseWorkflowEvents` write.
- [ ] `DependencyInjection.cs`: register the store, command and query.
- [ ] One migration `<ts>_EngineerNotes` (+ Designer, snapshot): table, FK,
  constraints, index; SQL Server-only `RequireRuntimeRole` and
  `GRANT SELECT, INSERT` to `pegasus_web_runtime_role`; matching REVOKE in
  `Down`; no worker grant, no UPDATE/DELETE.
- [ ] `tests/Pegasus.IntegrationTests/EngineerNotePersistenceTests.cs`:
  attribution, replay protection, lease refusal, ordering, separate-table
  destination, no row in `CaseWorkflowEvents`.
- [ ] `Presentation/OperatorLabels.cs` (`CaseWorkspace`): section title,
  add action, dialog title, field label, singular/plural count, outcome
  messages; no `HistoryEvent` entry.
- [ ] `Pages/Cases/Details.cshtml.cs`: `engineer-notes` section key, notes
  load through the merged CASE-038 lazy-section contract, actor names via
  `ActorDisplayNames.ResolveStaffNamesAsync`, `OnPostAddEngineerNoteAsync`
  through `ExecuteCaseCommandAsync` (id, operationKey, note, editLeaseToken;
  no expectedVersion).
- [ ] `Pages/Cases/Shared/_CaseEngineerNotes.cshtml`: Date, Time, ID, text
  rows newest first using `_CaseHistory` classes; nothing rendered for an
  empty read-only list; add form in edit mode only; no CSS/JS.
- [ ] `Details.cshtml` / `_CaseWorkspaceNav.cshtml`: section slot and nav
  entry in the exact shape CASE-038 merged; no frame change.
- [ ] `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`: section
  renders, no empty-state prose, resolved actor name, no Notes-history
  leakage, leased staff-only POST with antiforgery and operation key.
- [ ] Browser journey assertion only if UIIMP-014's holder agrees the
  existing route reaches the section; no new snapshot states or
  `catalogue.json` entries.
- [ ] Run `dotnet restore ./Pegasus.slnx --locked-mode` (exit code recorded).
- [ ] Run `dotnet build ./Pegasus.slnx --configuration Release --no-restore`.
- [ ] Run `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"`.
- [ ] Run `./scripts/Test-MigrationGrants.ps1`.
- [ ] Run `./scripts/Update-TestUiSnapshots.ps1`; commit the regenerated
  `docs/design/test-ui/pages/case-details--default.html` with the page change.
- [ ] Run `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture`.
- [ ] Run `./scripts/Test-UiCatalogue.ps1`.
- [ ] Run the focused browser test if `OperatorJourneyTests.cs` changed.
- [ ] Simplification pass recorded in the plan under a dated heading.
- [ ] post-implementation report written
- [ ] PR opened with Kanmer: CASE-039
