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
- UIIMP-014 owns `docs/design/test-ui/**`. CASE-039 adds no state and no
  catalogue entry; it only regenerates the changed default snapshot that its
  routed page change forces, and only under a recorded handoff for that one
  file (Step 5).
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
- The Notes-history question is settled, not deferred: the operator answered
  on 2026-09-03 that nothing about an Engineer note appears in the Notes
  history (D32). The implementation writes no `CaseWorkflowEvents` row and
  adds no `CaseDetails.History` projection. This is the rule, not a default
  awaiting approval.
- The ticket's "reuse the Triage append-only note shape (INTK-054)" line is
  stale and is not an execution dependency: INTK-054 is `backlog` and no
  Triage note entity, command or store exists on `origin/dev`
  (`Pages/Triage/Details.cshtml` line 395 states Triage has no note entity).
  CASE-039 reuses the committed `AddCaseNote` / `EfRecordEngineerFinding`
  shapes instead; making a backlog ticket a wave-3 blocker would absorb
  another ticket's scope.

### Step 2 — Define the Engineer-note Core contract and validation

- **Files:** `src/Pegasus.Core/Cases/EngineerNotes.cs`;
  `tests/Pegasus.Core.Tests/Cases/EngineerNotesTests.cs`.
- **Reuse:** `AddCaseNote`, `ICaseNoteStore`, and its recording-store unit-test
  shape in `CaseNotes.cs` and `AddCaseNoteTests.cs`.
- Define the add request, append-only store port, list-query port, note
  projection, and `IAddEngineerNote` command in the new file. The request
  carries case ID, staff actor, expected Case version, operation key, note
  text, and edit-lease token.
- Require `ActorKind.Staff` with `PerformCasework`; refuse Provider,
  Automation, and every other actor. Trim text, require non-blank text, and
  retain the established 2,000-character limit.
- The expected Case version travels with the lease token because FRD-01
  §Case edit authority requires every staff case mutation to present both,
  and `EfRecordEngineerFinding` (lines 55-65) is the committed precedent that
  calls `RequireVersion` then `RequireLease` without the terminal-state gate.
  The append itself does not increment the Case version. The command
  delegates the persisted version check, lease check and idempotent write to
  its store port.
- Unit-test staff authorization, rejected actor kinds, missing/overlong text,
  trimming, operation-key forwarding, expected-version and lease-token
  forwarding, and the newest-first list contract. A new port is necessary
  because the existing Notes contract deliberately permits Provider and
  writes to the timeline.

### Step 3 — Persist and query the separate append-only table

- **Files:** `src/Pegasus.Infrastructure/Persistence/EngineerNoteEntities.cs`;
  `src/Pegasus.Infrastructure/Persistence/EngineerNotesModelConfiguration.cs`;
  `src/Pegasus.Infrastructure/Persistence/EfEngineerNoteStore.cs`;
  `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`;
  `src/Pegasus.Infrastructure/DependencyInjection.cs`;
  `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_EngineerNotes.cs`;
  `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_EngineerNotes.Designer.cs`;
  `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`;
  `scripts/Invoke-AzureDatabaseBootstrap.ps1`;
  `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs`;
  `tests/Pegasus.IntegrationTests/EngineerNotePersistenceTests.cs`.
- **Reuse:** `AssessmentEntities.cs`, `AssessmentModelConfiguration.cs`,
  `EfRecordEngineerFinding.cs`, `CaseValuations` migration, and
  `CaseNotePersistenceTests.cs`.
- Add one `EngineerNotes` table with immutable ID, Case FK, operation key,
  request hash, actor attribution, text, and recorded UTC timestamp. Configure
  a Case/operation-key replay constraint and a `(CaseId, OccurredAtUtc, Id)`
  retrieval index; query descending by timestamp then ID.
- Use `IDbContextFactory<PegasusDbContext>` and the separate table for both
  replay detection and insertion. Follow `EfRecordEngineerFinding.cs` exactly:
  a `Serializable` transaction, a normalized request hash, an exact-replay
  return before the current-state guards, a `CaseOperationConflictException`
  for the same operation key with a different payload, and a winner re-read
  after a uniqueness race. `EfCaseNoteStore` is not a sufficient precedent —
  it stores the operation key as the request hash (line 55) and silently
  accepts any same-key replay, and a bare pre-check plus unique index would
  throw an unhandled duplicate-key error on a concurrent retry.
- Load the Case workflow, then call `StaffAuthorization.Require(actor,
  PerformCasework)`, `ArchivedCaseGuard.RequireNotArchived`,
  `CaseMutationGuard.RequireVersion` and `CaseMutationGuard.RequireLease`, in
  that order — the same sequence as `EfRecordEngineerFinding` (lines 55-65).
  Do not call `CaseMutationGuard.Require`, because that adds a terminal-state
  gate that D32 does not require.
- Clear the persisted lease with `CaseMutationGuard.ClearLease(workflow)` in
  the same transaction as the insert, as `EfRecordEngineerFinding` line 84
  does. `CaseMutationPageModel.ExecuteCommandAsync` unconditionally calls
  `ClearLeaseState()` on success (line 371), so leaving the server lease held
  would drop the operator into recovery mode on the next GET.
- Persist no `CaseWorkflowEvents` row and do not modify `CaseNotes.cs`,
  `EfCaseNoteStore.cs`, or `_CaseHistory.cshtml`.
- Register the new ports and command through the existing DI pattern.
- Generate one migration with the table, FK, constraints, indexes, snapshot,
  and SQL Server-only runtime-role guard. Grant only `SELECT, INSERT` on
  `EngineerNotes` to `pegasus_web_runtime_role`; revoke the same privileges in
  `Down`. Do not grant worker access or `UPDATE`/`DELETE`.
- The migration, its grants and the bootstrap census ride the same diff.
  `scripts/Invoke-AzureDatabaseBootstrap.ps1` carries the exhaustive expected
  permission matrix and states that a later grant-carrying migration must
  extend it (line 91); add the `pegasus_web_runtime_role|G|SELECT|EngineerNotes`
  and `|G|INSERT|EngineerNotes` rows with the caller comment, and no worker
  row. `AzureSqlRuntimeRoleMigrationTests.cs` holds an exhaustive expected
  schema table list and the per-role grant lists; add `EngineerNotes` there
  too. `Test-MigrationGrants.ps1` alone is insufficient — it only proves some
  grant exists.
- Integration-test attribution, exact operation-key replay, altered-payload
  same-key conflict, stale-version refusal, missing/expired-lease refusal,
  persisted-lease clearing after a successful append, stable newest-first
  ordering, separate-table persistence, and absence of an Engineer-note row
  from `CaseWorkflowEvents`. Also prove the two acceptance points the earlier
  draft left untested: a case in a terminal state whose lease is held accepts
  a note, and a correction under a new operation key appends a second row
  rather than altering the first.

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
  expected Case version, operation key, note, and edit-lease token, then
  invokes `ExecuteCaseCommandAsync`. It adds no lifecycle check of its own;
  a stale version or a lost lease is refused by the store.
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
- **Shared-path handoff (required, not assumed):** UIIMP-014 owns
  `docs/design/test-ui/**` for this wave, and a routed Razor change forces a
  re-snapshot, so the regenerated file cannot simply be omitted. Record an
  explicit UIIMP-014 handoff for `pages/case-details--default.html` — or wait
  for that lane to release the path — before committing the regenerated
  snapshot. CASE-039 still adds no new snapshot state and no
  `catalogue.json` entry. CASE-038's overlap is already sequenced by its
  merge dependency and the migration overlap by the exclusive migration
  lock.
- **Reuse:** section-selection, resolved-actor-name, lease-envelope, and
  manual-chase tests in `CaseDetailsWebTests.cs`; the existing Case workspace
  journey and Test UI capture scripts.
- Extend the Details-web fixture to prove the Engineer-notes section is
  selected, presents no empty-state prose, resolves the actor name, renders
  no Notes-history leakage, exposes an append form and no edit or delete
  affordance, and posts a staff-only leased append command with antiforgery,
  operation key, expected version, and lease token.
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
- The add form presents the loaded Case version and the current edit lease,
  is refused when either is stale or lost, adds no lifecycle-state
  restriction, and does not increment the Case version.
- A successful append clears the persisted edit lease in the same
  transaction, so the page and the server agree that edit mode has ended.
- The migration, its web grant, the bootstrap census row and the Azure SQL
  runtime-role test list all ship in the same diff.
- The new section uses the CASE-038 merged frame, needs no new CSS or
  JavaScript, and emits no explanatory empty state.
- The migration has exactly the web runtime `SELECT, INSERT` grant and passes
  the migration-grant check.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
./scripts/Test-MigrationGrants.ps1
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

The third command is the canonical delivery gate exactly as the runbook
states it (`Category!=Corpus`, runbook line 308); it already includes the
Browser category, so no separate conditional browser run is substituted for
it. Focused per-project forms may be used while iterating only.

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

- Q1 (history line) was answered by the operator on 2026-09-03 and is
  ticked: nothing about an Engineer note appears in the Notes history. The
  implementation writes no `CaseWorkflowEvents` row and adds no
  `CaseDetails.History` entry. There is no pending follow-up.
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

## Plan review (2026-09-03, gpt-5.6-sol xhigh; dispositions Claude Opus)

gpt-5.6-sol at xhigh read the ticket body, plan, checklist, the resolved open
questions, D29-D46 and the lane map independently, in the detached
`.worktrees/research` checkout at `origin/dev`
`897db9530a45063e8f684f2800685afbfdced006`, read-only. Verdict: REQUEST
CHANGES, nine findings. Every finding below was re-checked by the wrapper
against the same checkout before disposition.

| # | Severity | Step | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | 2, 4 | Omitting `expectedVersion` contradicts FRD-01 §Case edit authority ("every save, transition, assignment, association, evidence change, and other staff mutation presents both the lease token and the Case version"). | **Fixed.** CONFIRMED `EfRecordEngineerFinding.cs` lines 55-65 call `RequireVersion` then `RequireLease` with no terminal-state gate, so a versioned check costs nothing the plan was avoiding. The request, the form and the store now carry and check the loaded version; the append still does not increment it. |
| 2 | blocker | 2, 3 | The ticket says to reuse the INTK-054 Triage append-only note shape; the plan substitutes `AddCaseNote` without disposing of that instruction, and sol asked for INTK-054 to become an execution dependency. | **Partly fixed, partly rejected.** CONFIRMED no Triage note entity, command or store exists (`Pages/Triage/Details.cshtml` line 395 says so in the page itself) and INTK-054 is `backlog`. Making a backlog ticket a wave-3 blocker would absorb another ticket's scope, so the dependency is rejected; the stale premise is now stated and disposed of explicitly in Step 1. |
| 3 | blocker | 3, 4 | `ExecuteCommandAsync` clears the browser lease state on success while the planned store never clears the persisted lease, leaving the case in recovery mode on the next GET. | **Fixed.** CONFIRMED `CaseMutationPageModel.ExecuteCommandAsync` line 371 calls `ClearLeaseState()` unconditionally on success, and `EfRecordEngineerFinding` line 84 clears the persisted lease. Step 3 now clears it in the same transaction as the insert, with a test. |
| 4 | blocker | 3, 6 | The schema plan omits the bootstrap census and the Azure SQL runtime-role test, both of which the delivery gate reads. | **Fixed.** CONFIRMED `scripts/Invoke-AzureDatabaseBootstrap.ps1` line 91 states that a later grant-carrying migration must extend the exhaustive matrix (the `CaseValuations` block at line 397 is the model), and `AzureSqlRuntimeRoleMigrationTests.cs` holds an exhaustive expected schema table list. Both files are now Step 3 files and checklist items. |
| 5 | blocker | 5 | `docs/design/test-ui/pages/case-details--default.html` overlaps UIIMP-014's ownership of `docs/design/test-ui/**`; the plan narrowed that ownership without authority. | **Fixed as a coordination requirement.** The snapshot cannot be dropped (a routed Razor change forces a re-snapshot), so Step 5 now requires an explicit recorded UIIMP-014 handoff for that one file before it is committed, rather than assuming the narrowing. |
| 6 | should-fix | 3 | Idempotency and concurrency are underspecified; `EfCaseNoteStore` is a weak precedent and a pre-check plus unique index throws on a concurrent retry. | **Fixed.** CONFIRMED `EfCaseNoteStore.cs` line 55 stores the operation key as the request hash. Step 3 now names `EfRecordEngineerFinding` as the pattern: `Serializable` transaction, normalized request hash, exact replay before the guards, same-key/different-payload conflict, winner re-read after a race. |
| 7 | should-fix | 3, 5 | Tests do not prove terminal-state success, a correction appending a second row, or the absence of an edit/delete affordance. | **Fixed.** All three are now named test cases; database privileges stay covered by finding 4 rather than duplicated here. |
| 8 | should-fix | 6 | The plan's `Category!=Corpus&Category!=Browser` is not the canonical delivery command. | **Fixed.** CONFIRMED runbook line 308 and CLAUDE.md both state `Category!=Corpus`. The plan and checklist now run it exactly, and the conditional browser run is removed as redundant. |
| 9 | nit | 1, wrapper checks | Q1 is still written as operator-owned, unticked, and a reversible default despite the 2026-09-03 resolution. | **Fixed.** Both passages now state the resolved D32 rule directly with no follow-up wording. |

sol raised nothing under D44 (no staff review flag), D45 (no damage type) or
D46 (crop), found no new package or speculative abstraction, and confirmed the
remaining named helpers, ports, scripts, partials and label functions all
exist. No finding was silenced and none needed an operator question.

## Resolutions (2026-09-03)

- Operator: no event in the case Notes history when an Engineer note is
  added. This is the binding rule for the implementation, not a default.

## Simplification pass (2026-09-04)

Independent review (Codex gpt-5.6-sol, low effort) of the full CASE-039
working-tree diff against `origin/dev` for reuse, duplication, unnecessary
abstraction, and dead code.

Findings and dispositions:

1. `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` —
   `EngineerNoteDisplay.Id` was populated from `note.Id` but never consumed
   by `_CaseEngineerNotes.cshtml` or any other touched code (no edit/delete
   affordance renders it). **Fixed** — removed the `Id` record member and the
   corresponding `note.Id` mapping argument.
2. `tests/Pegasus.Core.Tests/Cases/EngineerNotesTests.cs` —
   `QueryContractNamesNewestFirstOrdering` used reflection only to assert
   that `IEngineerNoteQueries.ListNewestFirstAsync` has the return type the
   compiler already enforces; it proved no ordering behaviour. **Fixed** —
   deleted the test. Newest-first ordering is already proven by
   `EngineerNotePersistenceTests.AppendIsAttributedReplaySafeOrderedAndSeparateFromCaseHistory`.

No other findings. Re-ran build (0 warnings/errors), Core tests (1,234
passed), Architecture tests (100 passed), and the changed integration classes
(`EngineerNotePersistenceTests`, `CaseDetailsWebTests`,
`AzureSqlRuntimeRoleMigrationTests`, 94 passed) after applying both fixes.
