# Research — CASE-039 (2026-09-02, gpt-5.6-terra medium, wrapper-checked)

## Research

**Scope baseline — VERIFIED**
`git rev-parse --verify HEAD` returned
`cad00be9d42dbeaee9edf34c2d24de222d7ddb9d`, with `HEAD, origin/dev`.
`git status --short` was empty. `dotnet --list-sdks` found 10.0.204 and
10.0.303. No build or test command was run.

**Triage reuse premise — REFUTED / VERIFIED**
`rg --files ... | rg -i triage` finds Triage lifecycle, query, EF, page and
history files, but
`rg -n -i 'triage.*note|note.*triage|AddTriage|TriageNote' src tests`
finds no Triage note entity, command, or store. The current Triage page
explicitly says it has no note entity. INTK-054's proposed append-only shape
is therefore not available to reuse at `cad00be9`; CASE-039 must not depend
on it landing first.

**Nearest reusable shape — VERIFIED**
`Get-Content src/Pegasus.Core/Cases/CaseNotes.cs` and
`tests/Pegasus.Core.Tests/Cases/AddCaseNoteTests.cs` show that `AddCaseNote`:

- accepts `CaseId`, `ActionActor`, operation key, and text;
- trims text, requires it, and caps it at 2,000 characters;
- permits `Staff` with `PerformCasework`, and `Provider` with
  `SubmitProviderInstruction`; it rejects Automation and every other kind;
- delegates persistence through `ICaseNoteStore`;
- is append-only by design and idempotent on the operation key.

`Get-Content src/Pegasus.Infrastructure/Persistence/EfCaseNoteStore.cs` and
`tests/Pegasus.IntegrationTests/CaseNotePersistenceTests.cs` verify that it
writes an `operator_note` row to `CaseWorkflowEvents`, with actor kind,
subject, roles, text, timestamp, equal before/after versions, and operation
key replay protection. `EfCaseQueryStore` projects that table into
`CaseDetails.History`, and `_CaseHistory.cshtml` renders it. It is therefore
the correct validation, attribution, clock, operation-key, EF factory, and
staff-authorization convention to reuse, but its table and projection must
not be reused: doing so would put Engineer notes in Notes history.

**Absent implementation premise — VERIFIED**
`rg --files src tests | rg 'EngineerNotes|_CaseEngineerNotes'` returns no
matches. `src/Pegasus.Core/Cases/EngineerNotes.cs` and
`Pages/Cases/Shared/_CaseEngineerNotes.cshtml` do not exist.

**Current Case page — VERIFIED**
`Details.cshtml` selects exactly one query-string section and renders
`_CaseHistory`, `_CaseFiles`, `_CaseVehicle`, `_CaseInspectionAddress`, or
the Overview partials. `_CaseWorkspaceNav.cshtml` owns the current six-link
section list. `DetailsModel.Section` normalizes only those keys.

`DetailsModel` and `CaseMutationPageModel` own the Case edit lease. Lease
commands carry expected version, operation key and lease token; normal case
mutations use `ExecuteCaseCommandAsync`. Forms use Razor's normal POST form
antiforgery generation. The existing Notes form posts to
`/Cases/Tasks?handler=AddNote`; `TasksModel.OnPostAddNoteAsync` deliberately
does not require a lease or expected version because an operator note is
append-only rather than a Case mutation.

**Case history projection — VERIFIED**
`_CaseHistory.cshtml`, `EfCaseQueryStore.cs`, and
`CaseQueries.cs` show that Notes is `CaseDetails.History`, built from the
latest 200 `CaseWorkflowEvents` ordered newest first. It renders date, time,
resolved actor display name, event label, and optional reason. Engineer notes
need their own projection and partial; they must not extend
`CaseHistoryEntry` or `CaseWorkflowEvents`.

**Closed-case convention — VERIFIED**
`rg -n -C 4 'IsTerminal|PostReportComplete|Closed' ...` shows
`CaseLifecycleRules.IsTerminal` covers `PostReportComplete`,
`ProviderCancelled`, `CollisionEngineersRejected`, `CreatedInError`, and
`SourceEmailUnlinked`. Existing `AddCaseNote` has no lifecycle-state check;
the mockup hides the Engineer-note action only when `state === 'closed'`.
D30 additionally makes Engineer sections read-only once Complete.

**Mockup — VERIFIED**
`Get-Content 21-case-sections.js | Select-Object -Skip 44 -First 9` verifies:

- a separate `engineer-notes` section with newest-first rows of date, time,
  author, and text;
- a derived `N notes` meta count;
- "Add note for Engineer," hidden for mockup `closed` cases;
- a required `Note` textarea in "Note for the Engineer";
- client-side prepend and a separate mockup history line.

The same command set over `04-fixtures.js`, `03-labels.js`, and `20-case.js`
verifies the separate case-level `engineerNotes` array, the distinct
`vehicle.engineerNotes` string, the "Engineer notes" label, and its count
badge. The mockup's "No notes for the Engineer" prose conflicts with the
repository's no-explanatory-copy rule; do not ship that empty state.

**Labels — VERIFIED**
`rg -n -C 2 'CaseWorkspace|Notes|History' OperatorLabels.cs` shows
`OperatorLabels.CaseWorkspace` is the Case-workspace grouping and
`HistoryEvent("operator_note")` supplies Notes-history wording. CASE-039
needs a new `CaseWorkspace` label set for section title, add action, dialog
title, field label, singular/plural count, and success/failure messages.
It must not add an `operator_note` history-event label.

**Persistence and grants — VERIFIED**
`PegasusDbContext.cs` exposes DB sets and invokes model configuration;
`AssessmentModelConfiguration.cs` and `AssessmentEntities.cs` demonstrate
the entity/configuration convention. `CaseValuations` creates a table and
grants its web runtime role `SELECT, INSERT, UPDATE`. The provider-recovery
migration demonstrates the worker role grant pattern when the worker writes.
`Test-MigrationGrants.ps1` requires every `Up()` `CreateTable` to be granted
somewhere in the migrations directory unless explicitly exempted. CASE-039's
web-only add/list store should grant the web role only; a worker grant would
be unjustified unless a named worker caller is added.

**Tests and Test UI — VERIFIED**
`CaseDetailsWebTests.cs` covers section selection, history rendering,
authorization, and lease behaviour. `CaseNotePersistenceTests.cs` proves
the existing Notes-table destination and replay protection;
`AddCaseNoteTests.cs` proves Core validation and authorization.
`Browser/OperatorJourneyTests.cs` covers Case workspace navigation but there
is no dedicated Engineer-notes browser test.

`rg --files docs/design/test-ui | rg -i 'case|details'` finds
`case-details--default.html`, `--unavailable.html`, and `--conflict.html`.
`Update-TestUiSnapshots.ps1` captures Web and Browser responses and compares
them through `TestUiSnapshotTests`; the default Case Details snapshot will
change when the section navigation changes.

**Remote-lane check — VERIFIED**
`git branch -r`, `git log --oneline -30 origin/dev`, and
`git log origin/dev..origin/task/...` were run for CASE-012, CASE-027,
ENG-027, and PLAT-029 branches. No checked branch has commits ahead of
`origin/dev`; UIIMP-013 is the current `cad00be9` commit. No currently
verifiable remote lane owns an unmerged Case-page frame change. Shared-lock
paths still require serialized ownership before implementation. (Wrapper:
the board's lane ownership is recorded below and supersedes this view.)

**Gap list**

- No Engineer-note Core command/query contracts or store exist.
- No separate `EngineerNotes` table, entity, mapping, grant, or migration
  exists.
- No Case-details projection, Razor partial, navigation entry, or POST
  handler exists.
- No labels or tests cover the feature.
- The current Notes path is unsuitable because it writes and renders the
  Case workflow timeline.
- The mockup history line cannot be copied into the existing Notes history.

**Reusable conventions**

- `AddCaseNote` / `ICaseNoteStore`: validation, attribution, authorization,
  2,000-character limit, `TimeProvider`, and operation-key idempotency.
- `EfCaseNoteStore`: `IDbContextFactory`, replay query before insert, and
  append-only persistence.
- `CaseMutationPageModel.NewOperationKey()`: form operation key.
- `TasksModel.OnPostAddNoteAsync`: lease-free append redirect/error shape.
- `_CaseHistory.cshtml`: newest-first note row date/time/actor layout.
- `CaseWorkspace` in `OperatorLabels`: one owner for operator wording.
- `CaseValuations` migration and `Test-MigrationGrants.ps1`: migration and
  runtime-grant convention.

**Risks**

- Writing to `CaseWorkflowEvents` or extending `CaseDetails.History` would
  violate the explicit "not in Notes history" acceptance criterion.
- Copying the mockup history event creates the same violation unless a new,
  non-Notes audit surface is explicitly authorized.
- Reusing `AddCaseNote` authorization would incorrectly admit Provider;
  D32 says staff notes.
- The mockup's `closed` terminology does not precisely map to all current
  terminal states.
- `Details.cshtml`, `_CaseWorkspaceNav.cshtml`, `OperatorLabels.cs`, the
  migration folder, and Test UI snapshots are shared-lock paths.

**Questions for the plan**

1. **Operator-only:** Should an Engineer-note add create a non-Notes audit
   event? The mockup says yes, while D32 and the ticket forbid appearance in
   Notes history. The current repository exposes its timeline as Notes, so
   this cannot be silently implemented as a history event.

2. **Design choice:** Staff only. D32 says staff notes; unlike `AddCaseNote`,
   do not admit Provider or Automation.

3. **Operator-only:** Should adds be refused for every terminal state, or only
   the mockup's `closed` state? D30 clearly makes Engineer sections read-only
   once Complete, but does not fully specify the other terminal outcomes.
   (Wrapper: see below — the design README's "editing only" rule may settle
   this through the existing edit-lease rule for terminal cases.)

4. **Design choice:** Lease-free append, matching `AddCaseNote`; it does not
   alter Case versioned data and must not contend with edit mode.
   (Wrapper: contradicted by the design README — see below.)

5. **Design choice:** Use the established 2,000-character limit and order by
   recorded UTC timestamp descending, with a stable identifier tie-breaker.
   This matches the existing note limit and the mockup's newest-first order.

## Wrapper checks (Claude, 2026-09-02)

Spot-checks of Codex's VERIFIED claims, run in the main checkout
`C:/Users/PC/Documents/GitHub/pegasus` (dev `1e6ac077`) and the detached
`.worktrees/research` (origin/dev `cad00be9`):

- CONFIRMED `TasksModel.OnPostAddNoteAsync` (`Pages/Cases/Tasks.cshtml.cs`
  lines 33–59) binds `id, operationKey, note` only — no lease token, no
  expected version — and redirects via `RedirectToDetails`.
- CONFIRMED `EfCaseQueryStore` builds `History` from `CaseWorkflowEvents`
  ordered by `OccurredAtUtc` desc, `Id` desc, `Take(200)` (lines 181–187).
- CONFIRMED `20260829095336_CaseValuations.cs` line 61 grants
  `SELECT, INSERT, UPDATE` on `[dbo].[CaseValuations]` to the web runtime
  role only; `20260828084644_GrantAiJobs.cs` shows the grant-only migration
  shape with `IsSqlServer()` and `RequireRuntimeRole`.
- CONFIRMED `EfCaseNoteStore` writes `AddCaseNote.EventType` rows into
  `CaseWorkflowEvents` with `BeforeVersion == AfterVersion` and operation-key
  replay protection; `DependencyInjection.cs` lines 325–326 register
  `ICaseNoteStore`/`IAddCaseNote`.
- CONFIRMED `CaseLifecycleRules.IsTerminal` exists and gates lifecycle
  commands (`Core/Lifecycle/CaseLifecycle.cs`); the exact member list Codex
  quotes was not re-verified by the wrapper.
- CONFIRMED no Triage note entity exists (`grep -rn TriageNote|AppendTriage`
  over `src` and `tests` is empty) and INTK-054 is `backlog` on the board.
  The ticket's "reuse the Triage append-only note shape (INTK-054)" premise
  is therefore stale; the reusable shape is `CaseNotes.cs`.

### Governing text not yet on origin/dev (DELIV-041, PR #647, Review)

DELIV-041's branch (`.worktrees/deliv-041`, `2944cbf1`) records D32 in the
governing docs; none of it is on `origin/dev` yet:

- `docs/frd/frd-01-case-identity-and-lifecycle.md` §Engineer notes:
  "append-only, attributed staff notes addressed to the Engineer (D32) …
  a separate section of the Case record from the Notes history; a
  correction is a new note, and there is no edit or delete."
- `docs/design/README.md` §Case workspace: "**Engineer notes:** Add note
  (editing only); entries Date, Time, ID and text, append-only, no edit and
  no delete (D32)."
- `docs/capabilities.md` row `CASE-33` → allocated to `CASE-039`.

Consequences for the plan:

- **Codex's design choice 4 (lease-free append) conflicts with the design
  authority.** The design README says the Add note action is offered in
  editing only, i.e. inside the one edit mode over one lease that covers
  every section. The plan should follow the README: the add form carries the
  lease token (like `OnPostSaveAsync`, via `ExecuteCaseCommandAsync`), while
  the Core command itself stays append-only and version-neutral (no
  `expectedVersion` bump). This also answers question 3: whether a terminal
  case can take a note is whatever the edit-lease rule already says for
  terminal cases — the planner checks the lease claim rule once rather than
  inventing a second state rule.
- Rendered columns are Date, Time, ID (actor display name as `_CaseHistory`
  resolves it) and text; newest first.
- The ticket needs a `refs:` entry to `docs/frd/frd-01-case-identity-and-lifecycle.md`
  (and `docs/design/README.md`) for the feature profile's `governing-doc`
  leave-backlog gate; it currently has none. The FRD section lands when
  PR #647 merges.

### Lane ownership from the board (corrects Codex's "none")

Codex checked remote branches only; the board records the lane owners:

- **CASE-038** (single-scroll frame) `blocks` CASE-039 and owns
  `Pages/Cases/Details.*`, `_CaseWorkspaceNav.cshtml`, `wwwroot/css/site.css`,
  `wwwroot/js/site.js` and `Presentation/OperatorLabels.cs` for its wave. It
  is `backlog`. CASE-039 must not add the nav entry or section dispatch
  itself before CASE-038 merges; after the merge, CASE-039 extends the
  frame's section slot and lazy partial-fetch handler under the
  `Pages/Cases/Shared/*` and `OperatorLabels.cs` shared locks.
- **UIIMP-014** owns `docs/design/test-ui/**` for the wave (CASE-039 blocks
  it); CASE-039 does not add snapshot states, only regenerates what its own
  page change moves.
- **ENG-034** owns the Assessment-to-sections move (`Pages/Cases/Assessment/*`
  and extracted partials); **ENG-035** owns `Core/Assessment/*` vocabulary;
  **CASE-041** owns `_CaseInspectionAddress` and the storage-location column;
  **CASE-040** owns `Eva/Send.*` and the sign-off field.
- **Migration lock** (`Persistence/Migrations/**`) has capacity one; CASE-039
  takes it for exactly one migration (table + web grant) and yields it.
