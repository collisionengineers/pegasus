# Plan — CASE-039 (2026-09-02, gpt-5.6-terra high)

Codex (gpt-5.6-terra, effort high) planned against the detached
`.worktrees/research` checkout at `origin/dev` 897db953, read-only; the
wrapper (Claude) checked every reuse name below and records its own
decisions and corrections in the last section.

## Objective

Deliver CASE-039: append-only, attributed staff notes addressed to the
Engineer, rendered as a separate Case section and never included in the
Notes history.

Starting point: `origin/dev` is `897db9530a45063e8f684f2800685afbfdced006`.
The working tree is clean.
Refresh the CASE-038 status, the shared locks and the migration lock
immediately before taking the ticket.

## Governing docs and constraints

- FRD-01 D32 requires attributed, append-only staff Engineer notes, separate
  from Notes history; corrections are new notes and there is no edit or delete.
- The Case-workspace design requires the Engineer-notes section in its stated
  order, Date/Time/ID/text rows, and Add note in editing only. Engineer notes
  are not among the sections made read-only once Complete.
- No additional lifecycle-state gate is added. The add action is available
  only while the existing edit lease is held; terminal cases may enter edit
  mode under the current rule.
- No explanatory empty-state copy, disabled substitute action, CSS, or
  JavaScript is added. Labels live only in
  `src/Pegasus.Web/Presentation/OperatorLabels.cs`. State labels are the
  exact `OperatorLabels.CaseStage` values; nothing is drawn disabled as a
  substitute for an absent action.
- CASE-038 must merge first. CASE-039 then adapts its section slot, navigation,
  and lazy-partial shape after `git merge --no-edit origin/dev`.
- UIIMP-014 owns new Test UI states and catalogue entries. CASE-039 only
  regenerates the changed default snapshot when its routed page change does
  so.
- The migration lock is exclusive. Take it for one migration only, and release
  it when the migration is merged.

## Ordered steps

### Step 1 — Clear execution dependencies and record the bounded decision

- **Files:** none.
- **Reuse:** the CASE-038 frame and its merged section/lazy-fetch contract;
  Kanmer shared-lock and migration-lock records.
- Confirm CASE-038 has merged, acquire the permitted shared locks, refresh
  from `origin/dev`, and inspect the resulting Details-page section contract
  before coding. Do not modify the pre-CASE-038 alternate-section frame.
- Keep the history-line question operator-owned. The implemented default is
  no `CaseWorkflowEvents` write and no `CaseDetails.History` projection. A
  future operator approval is a separately bounded follow-up for a history
  event type and label, not scope for CASE-039.

### Step 2 — Define the Engineer-note Core contract and validation

- **Files:** `src/Pegasus.Core/Cases/EngineerNotes.cs`;
  `tests/Pegasus.Core.Tests/Cases/EngineerNotesTests.cs`.
- **Reuse:** `AddCaseNote`, `ICaseNoteStore`, and its recording-store unit-test
  shape in `CaseNotes.cs` and `AddCaseNoteTests.cs`.
- Define the add request, append-only store port, list-query port, note
  projection, and `IAddEngineerNote` command in the new file. The request
  carries case ID, staff actor, operation key, note text, and edit-lease
  token.
- Require `ActorKind.Staff` with `PerformCasework`; refuse Provider,
  Automation, and every other actor. Trim text, require non-blank text, and
  retain the established 2,000-character limit.
- Keep the command version-neutral: it has no expected version and does not
  increment Case version. It delegates the persisted lease check and
  idempotent write to its store port.
- Unit-test staff authorization, rejected actor kinds, missing/overlong text,
  trimming, operation-key forwarding, lease-token forwarding, and the
  newest-first list contract. A new port is necessary because the existing
  Notes contract deliberately permits Provider and writes to the timeline.

### Step 3 — Persist and query the separate append-only table

- **Files:** `src/Pegasus.Infrastructure/Persistence/EngineerNoteEntities.cs`;
  `src/Pegasus.Infrastructure/Persistence/EngineerNotesModelConfiguration.cs`;
  `src/Pegasus.Infrastructure/Persistence/EfEngineerNoteStore.cs`;
  `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`;
  `src/Pegasus.Infrastructure/DependencyInjection.cs`;
  `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_EngineerNotes.cs`;
  `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_EngineerNotes.Designer.cs`;
  `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`;
  `tests/Pegasus.IntegrationTests/EngineerNotePersistenceTests.cs`.
- **Reuse:** `AssessmentEntities.cs`, `AssessmentModelConfiguration.cs`,
  `EfCaseNoteStore.cs`, `CaseValuations` migration, and
  `CaseNotePersistenceTests.cs`.
- Add one `EngineerNotes` table with immutable ID, Case FK, operation key,
  request hash, actor attribution, text, and recorded UTC timestamp. Configure
  a Case/operation-key replay constraint and a `(CaseId, OccurredAtUtc, Id)`
  retrieval index; query descending by timestamp then ID.
- Use `IDbContextFactory<PegasusDbContext>` and the separate table for both
  replay detection and insertion. Load the Case workflow and call the existing
  persistence-side `CaseMutationGuard.RequireLease`, which delegates the
  lease-policy decision to `CaseEditAuthority.RequireLease`. Do not call
  `CaseMutationGuard.Require`, because that adds a terminal-state gate that
  D32 does not require.
- Persist no `CaseWorkflowEvents` row and do not modify `CaseNotes.cs`,
  `EfCaseNoteStore.cs`, or `_CaseHistory.cshtml`.
- Register the new ports and command through the existing DI pattern.
- Generate one migration with the table, FK, constraints, indexes, snapshot,
  and SQL Server-only runtime-role guard. Grant only `SELECT, INSERT` on
  `EngineerNotes` to `pegasus_web_runtime_role`; revoke the same privileges in
  `Down`. Do not grant worker access or `UPDATE`/`DELETE`.
- Integration-test attribution, operation-key replay, stable newest-first
  ordering, separate-table persistence, and absence of an Engineer-note row
  from `CaseWorkflowEvents`.

### Step 4 — Add the leased Case-section surface after CASE-038

- **Files:** `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`;
  `src/Pegasus.Web/Pages/Cases/Details.cshtml`;
  `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml`;
  `src/Pegasus.Web/Pages/Cases/Shared/_CaseEngineerNotes.cshtml`;
  `src/Pegasus.Web/Presentation/OperatorLabels.cs`.
- **Dependency:** adapt only the merged CASE-038 Details and lazy-fetch shape;
  do not plan a competing frame, navigation, CSS, or JavaScript change.
- **Reuse:** `CaseMutationPageModel.ExecuteCaseCommandAsync`,
  `NewOperationKey()`, `_CaseHistory.cshtml` note-row classes,
  `ActorDisplayNames.ResolveStaffNamesAsync`, `OfficeDate`, and `OfficeClock`.
- Add the `engineer-notes` section normalization, route it through the
  CASE-038 section slot, and load its separate projection only when that
  section is requested by the merged lazy-section contract.
- Resolve staff display names through the existing
  `IStaffAccountQueries` dependency and `ActorDisplayNames`; never render the
  persisted actor subject ID.
- Add `OnPostAddEngineerNoteAsync` on Details. Its form carries case ID,
  operation key, note, and edit-lease token, then invokes
  `ExecuteCaseCommandAsync`. It intentionally has no expected-version input
  and no added lifecycle check.
- Render Date, Time, resolved ID, and text in newest-first order. Render no
  content for an empty read-only list. Offer the named Engineer-note add
  action/form only in edit mode, with the required Note field and existing
  antiforgery form generation.
- Put every visible Engineer-note string, including title, action, dialog
  title, field label, singular/plural count, and outcome messages, in
  `OperatorLabels.CaseWorkspace`. Do not add a Notes-history event label.

### Step 5 — Prove the routed page and shared-frame integration

- **Files:** `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`;
  `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` only if
  UIIMP-014's holder agrees the existing browser route needs it;
  `docs/design/test-ui/pages/case-details--default.html` only as regenerated
  output.
- **Reuse:** section-selection, resolved-actor-name, lease-envelope, and
  manual-chase tests in `CaseDetailsWebTests.cs`; the existing Case workspace
  journey and Test UI capture scripts.
- Extend the Details-web fixture to prove the Engineer-notes section is
  selected, presents no empty-state prose, resolves the actor name, renders
  no Notes-history leakage, and posts a staff-only leased append command with
  antiforgery, operation key, and lease token.
- Add the browser assertion only when the agreed browser route reaches the
  new section. Do not create snapshot states or catalogue entries.
- Regenerate the default Case Details snapshot after the routed-page change
  and ensure its new navigation/section output matches the merged CASE-038
  structure.

### Step 6 — Run the delivery checks and hand off

- **Files:** no additional files.
- **Reuse:** repository canonical restore/build/test commands,
  `Test-MigrationGrants.ps1`, Test UI snapshot verification, and catalogue
  verification.
- Record command exit codes and failures in the post-implementation report.
  A failure stops the ticket for disposition; do not weaken tests.
- Open the PR against `dev`, place CASE-039 in Review, and stop. Do not merge
  the PR or begin another ticket.

## Acceptance checks

- A staff member in edit mode can append an attributed Engineer note.
- Provider and Automation actors cannot add one.
- Note text is trimmed, required, and limited to 2,000 characters.
- Entries render newest first by recorded UTC timestamp and ID, with Date,
  Time, resolved staff ID, and text.
- There is no Engineer-note edit or delete path, no update/delete database
  grant, and corrections create a new entry.
- The separate `EngineerNotes` table is the only Engineer-note persistence
  destination; Notes history and `CaseWorkflowEvents` remain unchanged.
- The add form requires the current edit lease but adds no lifecycle-state
  restriction and does not increment Case version.
- The new section uses the CASE-038 merged frame, needs no new CSS or
  JavaScript, and emits no explanatory empty state.
- The migration has exactly the web runtime `SELECT, INSERT` grant and passes
  the migration-grant check.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
./scripts/Test-MigrationGrants.ps1
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

If `OperatorJourneyTests.cs` changes, also run its focused browser test using
the repository's established browser-test invocation.

## Stop condition

The CASE-039 PR is open against `dev`, all required checks pass, the
post-implementation report is written, and the ticket is in Review. No merge
or follow-on ticket work occurs in this ticket.

## Wrapper checks and decisions (Claude, 2026-09-02)

Read-only checks on `origin/dev` 897db953 (`.worktrees/research`):

- CONFIRMED DELIV-041 (PR #647) merged: `docs/frd/frd-01-case-identity-and-lifecycle.md`
  §Engineer notes, `docs/design/README.md` §Case workspace ("Add note
  (editing only); entries Date, Time, ID and text, append-only, no edit
  and no delete (D32)") and `docs/capabilities.md` row CASE-33 are on dev.
- CONFIRMED `CaseMutationGuard.Require` (Infrastructure, line 16) throws
  `CaseTerminalMutationException` on `CaseLifecycleRules.IsTerminal`;
  `CaseMutationGuard.RequireLease` (line 50) only delegates to
  `CaseEditAuthority.RequireLease` (Core, line 65). Step 3's choice of
  `RequireLease` is therefore exactly "lease only, no state gate". The
  store must also call `ArchivedCaseGuard.RequireNotArchived` and
  `StaffAuthorization.Require(actor, PerformCasework)` itself, since it
  bypasses `Require`.
- CONFIRMED `Details.cshtml` offers Edit Case (`ClaimLease`) whenever
  `ActiveEditLease is null`, with no terminal-state check (lines 227-238);
  "Reopen Case" renders inside edit mode for closed cases. "Editing only"
  therefore does not refuse a note on a closed case by itself.
- CONFIRMED `ActorDisplayNames.ResolveStaffNamesAsync` (Core/Actors, line
  26) and `IStaffAccountQueries` already injected into `DetailsModel`
  (line 34); `CaseMutationPageModel.ExecuteCaseCommandAsync(id,
  editLeaseToken, commandName, execute, successMessage)` (line 323).
- CONFIRMED CASE-038 is still `preparing`; Step 4 stays a dependency on
  its merge and the checklist waits for it.

Decisions recorded for the open questions:

- Q1 (history line) stays operator-only and unticked. The plan ships the
  safe default: no `CaseWorkflowEvents` row, no `CaseDetails.History`
  entry. An operator "yes" is one bounded follow-up (a new history event
  type plus its `HistoryEvent` label), not CASE-039 scope.
- Q2 (terminal states) is settled by the governing docs and ticked: Add
  note is offered in editing only (lease token carried through
  `ExecuteCaseCommandAsync`, validated by `CaseMutationGuard.RequireLease`)
  with no additional lifecycle-state gate, because the design README's
  read-only-once-Complete list (Damage, Valuation, Estimate, Settlement,
  Report) excludes Engineer notes, FRD-01 §Engineer notes states no state
  rule, and the edit lease is claimable on a terminal case today. The
  mockup's `state !== 'closed'` hide is superseded by "editing only".

Corrections to the Codex output: removed its "Kanmer tunnel unavailable"
aside (Codex never reaches the board by design); added the exact-state-label
and absent-versus-disabled rule to the constraints; split the checklist so
each item is one verifiable step. No other change.

Simplification pass: recorded at execution time under a dated heading here.

## Resolutions (2026-09-03)

- Operator: no event in the case Notes history when an Engineer note is added; the plan's default stands.
