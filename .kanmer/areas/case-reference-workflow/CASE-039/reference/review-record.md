# Review record — CASE-039 (PR https://github.com/collisionengineers/pegasus/pull/669)

## Scope

| Field | Value |
| --- | --- |
| Branch | `task/case-039-engineer-notes` |
| Reviewed head | `cc2920bf86ecfc301a9972df0c9d3d4d844349de` (confirmed by `git rev-parse HEAD` in `.worktrees/case-039-review`) |
| Built by | Codex gpt-5.6-sol |
| Independent read | Codex gpt-5.6-terra, `model_reasoning_effort=xhigh`, read-only detached checkout |
| Dispositions, gate, merge | Claude Opus |
| Date | 2026-09-04 |

Head `cc2920bf8` is a merge of `origin/dev` into the implementation head
`7a00b2873`. `git show --stat cc2920bf8` shows the merge brought in only
release scripts, `AGENTS.md`, ADR-0037 and `docs/runbook.md` — no CASE-039
file, no `src/Pegasus.Web`, no migration, no snapshot. The implementation
delta is unchanged from the head the post-implementation report describes.

## Verdict

**APPROVE.** Three findings from the independent read plus one reviewer
finding, all documentation-accuracy; none blocks the merge. The change is
bounded, wired, proven, and its migration ships with its grants and census.

## Findings and dispositions

| # | Severity | Location | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | should-fix | `post-implementation-report.md` head SHA | The report records `7a00b2873…` while the reviewed PR head is `cc2920bf8…`, so its exit codes are not bound to the reviewed head. | **Accepted; evidence re-established here.** The reviewer independently re-ran restore, Release build, Core, Architecture, the three changed integration classes and `Test-MigrationGrants.ps1` at `cc2920bf8` — all exit 0 (table below). The merge touched no CASE-039 file. No implementer action. |
| 2 | should-fix | `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs:15` | The plan says `EngineerNotes` was added to the exhaustive expected-schema and web-grant lists; it was not — a focused `[Fact]` was added instead. | **Rejected with reason.** `ExpectedSchemaTableSpec` and `ExpectedWebGrantSpec` are pinned to the historical migration `20260729199000_RuntimeRoleReconciliation` (`TerminalUpgradeReconciles…` line 466 migrates only to `RuntimeRoleMigration`, not to latest). `CaseValuations` — the plan's own named precedent — appears **nowhere** in those lists either; later grant-carrying migrations get a focused fact, as `RetainedMailSearchProjectionUsesExactCallerPermissions` (line 564) and `RetainedMailFolderMovesUseExactWebOnlyAppendPermissions` (line 588) do. Adding `EngineerNotes` to a historical-point assertion would falsify it. The plan text was imprecise; the implementation followed the established convention correctly. |
| 3 | nit | `post-implementation-report.md` §Files changed | The report names the persisted type `EngineerNoteRow`; the diff defines `EngineerNoteEntity`. | **Accepted as a documentation nit.** Recorded here; no code change. |
| 4 | nit (reviewer) | `post-implementation-report.md` §Snapshot artifact facts | Reported byte sizes (65,498 / 40,012) are the Git blob (LF) sizes; the on-disk CRLF working-tree sizes are 66,577 / 40,674. Content is identical. The report also implies the Engineer-notes markers are present in `case-details--conflict.html`; that state carries only the `id="section-engineer-notes"` host, because the section is lazily fetched. | **Accepted as a documentation nit.** The artifacts themselves are correct (verified below). |

No finding was silenced. Nothing was deferred to another ticket.

## Independent artifact verification (reviewer, at the reviewed head)

`docs/design/test-ui/pages/case-details--default.html` — 66,577 bytes on
disk, begins `<!DOCTYPE html>`, one `class="case-sticky"`, exactly eleven
section hosts (`section-overview`, `-engineer-notes`, `-inspection`,
`-vehicle`, `-damage`, `-valuation`, `-estimate`, `-settlement`, `-report`,
`-files`, `-notes`; the other five `id="section-*"` matches are `-title`
ids), zero `<img src="#">`, and the full Engineer-notes panel at line 505 —
section host, `case-engineer-notes-title` heading, and the edit-mode add
form carrying `id`, `expectedVersion`, `operationKey` and `editLeaseToken`
hidden inputs. No explanatory copy.

`docs/design/test-ui/pages/case-details--conflict.html` — 40,674 bytes,
begins `<!DOCTYPE html>`, one `case-sticky`, the same eleven section hosts,
zero `<img src="#">`, `section-engineer-notes` host present with a lazily
fetched body.

## Review questions

- **Every drawn control has a named production handler.** The single add
  form (`_CaseEngineerNotes.cshtml:39-56`) posts
  `asp-page-handler="AddEngineerNote"` to `OnPostAddEngineerNoteAsync`
  (`Details.cshtml.cs:479-493`), which routes through the existing
  `ExecuteCaseCommandAsync`. `IAddEngineerNote` / `IEngineerNoteStore` /
  `IEngineerNoteQueries` are registered in `DependencyInjection.cs:330-335`
  and injected into `DetailsModel`.
- **No explanatory copy.** An empty read-only section renders its heading
  only (`_CaseEngineerNotes.cshtml:9-20`); no field hint, no empty-state
  panel, nothing drawn disabled as a substitute for the absent add action.
  Proven by `EngineerNotesEmptyReadOnlySectionHasNoEmptyStateProse`.
- **Labels.** All six new members sit in one `// CASE-039: Engineer notes` …
  `// end CASE-039` block in `OperatorLabels.cs:1372-1381`; the duplicated
  `"Engineer notes"` literal in `CaseWorkspace.Sections` was replaced by a
  reference to `EngineerNotesSectionTitle` (line 1440) — one list per
  concept.
- **Owned paths only.** All 20 changed files are within the plan's and
  `files.md`'s named set. No `site.css`, `site.js`, `CaseNotes.cs`,
  `EfCaseNoteStore.cs`, `_CaseHistory.cshtml`, `docs/frd/**`,
  `TestUiSnapshotTests.cs`, `ci.yml` or `scripts/Test-*.ps1` change. The one
  `scripts/*.ps1` edit is the migration's bootstrap census
  (`Invoke-AzureDatabaseBootstrap.ps1:404-408`), which CLAUDE.md rule 16
  explicitly requires to ride the same diff.
- **Core owns policy.** `AddEngineerNote` (`EngineerNotes.cs:38-86`) owns
  staff-only authorization, trim, required, 2,000-character limit. The
  store's repeat of the `ActorKind.Staff` check is not a duplicate rule but
  a necessary boundary guard: `StaffAuthorization.PerformCasework` admits
  `ActorKind.Automation`, which D32 excludes.
- **Tests prove the claim.** `git diff origin/dev...HEAD -- tests/` removes
  no assertion. The one edited existing test updated `DeferredSections` from
  three to four entries because `engineer-notes` is now genuinely lazy — a
  strengthened, not weakened, expectation.
  `EngineerNotesRenderAttributedAndSeparateWithoutEditOrDeleteAffordances`
  asserts the resolved name is shown, the staff GUID is not, no `<form>`, no
  "edit", no "delete" and no "No notes";
  `EngineerNotePostCarriesTheLeasedStaffMutationEnvelope` proves the
  antiforgery-protected leased POST with operation key and expected version,
  on a `PostReportComplete` (terminal) case. `EngineerNotePersistenceTests`
  covers attribution, exact replay, same-key/altered-payload conflict, stale
  version, missing and expired lease, lease clearing, terminal-state append,
  correction as a second row, ordering, separate-table destination and
  absence from `CaseWorkflowEvents`.
- **Migration ships with its grants.** `20260904210022_EngineerNotes.cs`
  creates the table, FK (`Restrict`), the unique `(CaseId, OperationKey)`
  replay index and the `(CaseId, RecordedAtUtc DESC, Id DESC)` retrieval
  index, then under `IsSqlServer()` calls `RequireRuntimeRole` and grants
  `SELECT, INSERT` to `pegasus_web_runtime_role` only (lines 53-61), with
  the matching `REVOKE` in `Down` (lines 67-71). No worker grant, no
  `UPDATE`, no `DELETE` — append-only enforced at the database.
- **Correctness.** The store follows `EfRecordEngineerFinding`: a
  `Serializable` transaction, exact-replay short-circuit before the
  current-state guards, guards in the order `StaffAuthorization` →
  `RequireNotArchived` → `RequireVersion` → `RequireLease` (never
  `Require`, so no terminal-state gate — correct under D30, which does not
  list Engineer notes among the read-only-once-Complete sections),
  `ClearLease` in the same transaction as the insert, a
  `CaseOperationConflictException` on same key with a different payload,
  and a bounded winner re-read after a uniqueness race
  (`EfEngineerNoteStore.cs:36-108`). Note text is Razor-encoded via
  `@entry.Note`. Ordering is `RecordedAtUtc DESC, Id DESC`, matching the
  index. The append does not increment the Case version.
- **Report and checklist match the diff**, apart from findings 1-4 above.
  The single unticked checklist item (the optional browser-journey
  assertion) is honestly recorded as deferred to UIIMP-014.
- **Simplification pass dispositions are honest.** Both claimed fixes are
  in the tree: `EngineerNoteDisplay` (`Details.cshtml.cs:61-64`) carries no
  `Id` member, and no reflection-based Engineer-note test remains.
- **D44–D50 untouched.** No review flag, damage type, crop, EVA, repairer
  address, vehicle-record or Awaiting-instruction change appears in the
  diff. No new package.

## Commands and exit codes (reviewer, `.worktrees/case-039-review` at `cc2920bf8`)

| Command | Exit | Result |
| --- | ---: | --- |
| `git rev-parse HEAD` | 0 | `cc2920bf86ecfc301a9972df0c9d3d4d844349de` |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | restored |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | 0 warnings, 0 errors |
| `dotnet test ./tests/Pegasus.Core.Tests/… --configuration Release --no-build` | 0 | 1,234 passed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/… --configuration Release --no-build` | 0 | 100 passed |
| `dotnet test ./tests/Pegasus.IntegrationTests/… --filter "FullyQualifiedName~EngineerNotePersistenceTests\|FullyQualifiedName~CaseDetailsWebTests\|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests" -- xUnit.MaxParallelThreads=2` | 0 | 94 passed, 6 m 32 s |
| `pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1` | 0 | 92 migration files checked, every created table granted or exempted |

Scope rationale: the diff adds exactly three type families — the Core
contract (`Pegasus.Core.Tests`), the EF store plus migration
(`EngineerNotePersistenceTests`, `AzureSqlRuntimeRoleMigrationTests`,
`Test-MigrationGrants.ps1`) and the routed page surface
(`CaseDetailsWebTests`). `Pegasus.ArchitectureTests` covers the new
Core→Infrastructure→Web dependency direction. The full solution suite and
the unfiltered browser suite are CI's gate on the exact head, per the
EPIC-012 §Build policy rule against duplicating CI locally.

## CI gate — BLOCKED (2026-09-04)

`gh run list --branch task/case-039-engineer-notes --limit 1` →
run `33924646833`, `headSha` `cc2920bf86ecfc301a9972df0c9d3d4d844349de`
(matches the reviewed head), `status: completed`, **`conclusion: failure`**.

Job results: `reference-data`, `local-development-scripts`, `changes`,
`documentation`, `infrastructure`, `test-ui`, `unit`, `browser`,
`sql-integration (2)`, `sql-integration (3)`, `sql-integration-coverage` all
**success**; **`sql-integration (1)` failure**.

The failure is not the `changes` job and is not a flake, so no rerun was
attempted and the merge is refused.

### Blocking finding 5

| # | Severity | Location | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 5 | **blocker** | `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs:121` | The exhaustive applied-migrations list in `CommittedMigrationCreatesTheSqlServerSchema` was not extended with this ticket's migration. It ends at `"20260903233954_MarketResearchAiJob"`; the database now also carries `20260904210022_EngineerNotes`, so the assertion fails on CI. | **Fix — returned to the implementer.** |

CI evidence (`sql-integration (1)`, 2026-09-04T22:19:44Z):

```
Pegasus.IntegrationTests.IntakePersistenceIntegrationTests.CommittedMigrationCreatesTheSqlServerSchema [FAIL]
Assert.Equal() Failure: Collections differ
Expected: [···, "20260903225331_StaffAccountSignOff", "20260903233954_MarketResearchAiJob"]
Actual:   [···, "20260903225331_StaffAccountSignOff", "20260903233954_MarketResearchAiJob", "20260904210022_EngineerNotes"]
                                                                                            ↑ (pos 91)
at tests\Pegasus.IntegrationTests\IntakePersistenceIntegrationTests.cs:line 29
Failed! - Failed: 1, Passed: 407, Skipped: 1, Total: 409
```

Required fix: append `"20260904210022_EngineerNotes"` as the last entry of
that list, keeping the migrations in chronological order. EPIC-012
§Build policy names this file explicitly under merge prep — "the
applied-migrations list in `IntakePersistenceIntegrationTests.cs` keeps every
migration in chronological order" — so it is inside this ticket's scope, is
not tooling, and is the correct place for the change. This is a test that
proves the claim being brought up to date with the schema the ticket ships,
not a weakened assertion.

Why the lane missed it: the focused local filter
(`EngineerNotePersistenceTests`, `CaseDetailsWebTests`,
`AzureSqlRuntimeRoleMigrationTests`) does not include
`IntakePersistenceIntegrationTests`, and `Test-MigrationGrants.ps1` checks
grants, not the applied-migrations census. Any future migration lane should
add `FullyQualifiedName~IntakePersistenceIntegrationTests` to its focused
filter.

Nothing was merged. The ticket stays in Review. Findings 1-4 above keep their
dispositions and need no further action; only finding 5 must be applied,
after which CI must go green on the new head and this record is amended.

---

# Review record — CASE-039 (PR https://github.com/collisionengineers/pegasus/pull/669) — re-review

## Scope

| Field | Value |
| --- | --- |
| Branch | `task/case-039-engineer-notes` |
| Reviewed head | `ae38f570e3b854863c4a1961f655644f08e4a7af` (confirmed by `git rev-parse HEAD` in `.worktrees/case-039-review`) |
| Previous reviewed head | `cc2920bf86ecfc301a9972df0c9d3d4d844349de` (blocked on finding 5) |
| Built by | Codex gpt-5.6-sol |
| Independent read | Codex gpt-5.6-terra, `model_reasoning_effort=xhigh`, read-only detached checkout |
| Dispositions, gate, merge | Claude Opus |
| Date | 2026-09-05 |

The delta from the previously reviewed head is exactly one commit,
`ae38f570e`, whose diff touches one file
(`tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`,
2 insertions / 1 deletion). No production file, migration, snapshot or
script changed in this round.

## Verdict

**APPROVE.** Finding 5 (the only blocker) is closed. The independent read
returned APPROVE with no findings. CI is green on the exact reviewed head.
Findings 1–4 of the first record keep their dispositions and need no
further action.

## Findings and dispositions

| # | Severity | Location | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 5 | blocker (carried) | `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs:122` | The exhaustive applied-migrations list omitted `20260904210022_EngineerNotes`, failing `sql-integration (1)`. | **Closed — fix confirmed.** `ae38f570e` appends `"20260904210022_EngineerNotes"` as the last entry, after `"20260903233954_MarketResearchAiJob"`. Reviewer check: the 92 non-designer, non-snapshot migration files in `src/Pegasus.Infrastructure/Persistence/Migrations/` and the 92 asserted names are identical sets (`diff` clean), and the asserted sequence is already sorted, so it is in chronological order. No entry was removed, renamed or relaxed — the assertion stays exhaustive. `CommittedMigrationCreatesTheSqlServerSchema` now passes locally and on CI. |
| 6 | — | — | Regression search over the fix commit. | **None found.** `git show --stat ae38f570e` lists one file; `git diff origin/dev...HEAD --name-only` still lists exactly the 21 owned paths of the first review; the two snapshot artifacts are byte-identical to the previously verified ones. |
| 1–4 | should-fix / nit (carried) | post-implementation report accuracy; `AzureSqlRuntimeRoleMigrationTests` convention | See the first record. | **Unchanged.** 1 accepted (evidence re-established below at this head), 2 rejected with reason, 3 and 4 accepted as documentation nits. The report's line-9 head SHA still reads `7a00b2873…`; the round section at lines 165–186 correctly records `ae38f570e` and its exit codes. Documentation-accuracy only; no code consequence. |

No finding was silenced. Nothing was deferred to another ticket.

## Independent read

Codex gpt-5.6-terra at `model_reasoning_effort=xhigh`, read-only in the
detached `.worktrees/case-039-review` at `ae38f570e`:

> **Verdict: APPROVE. No findings.** Finding 5 is closed: `ae38f570e`
> changes only `IntakePersistenceIntegrationTests.cs`, appending
> `20260904210022_EngineerNotes` after `20260903233954_MarketResearchAiJob`
> at lines 121–122. The assertion matches all 92 migration files exactly —
> no missing, extra, duplicate, or out-of-order entries. The whole diff
> remains within the 21 owned paths; handler wiring, labels, Core policy,
> append-only grants/census, tests, report/checklist, and simplification
> dispositions remain consistent. No regression was introduced.

## Review questions at this head

- **Every drawn control has a named production handler.** Unchanged from the
  first record: the single add form posts `AddEngineerNote` to
  `OnPostAddEngineerNoteAsync` via `ExecuteCaseCommandAsync`.
- **No explanatory copy; labels only in `OperatorLabels.cs`; Core owns
  policy; migration ships with grants and census.** Unchanged — no
  production file moved in this round.
- **Owned paths only.** The one changed file this round,
  `IntakePersistenceIntegrationTests.cs`, is named explicitly by EPIC-012
  §Build policy as in-ticket merge prep. It is not tooling.
- **Tests prove the claim and none was weakened.** The change adds an
  expected entry to an exhaustive list so it matches the schema the ticket
  ships; nothing was removed or loosened. Verified by re-running the class.
- **Report and checklist match the diff.** The report's review-round section
  names the same single file and the same commit. The one unticked checklist
  item (the optional browser-journey assertion) remains honestly deferred to
  UIIMP-014.
- **Snapshot artifacts re-verified at this head.**
  `docs/design/test-ui/pages/case-details--default.html` — 66,577 bytes on
  disk, begins `<!DOCTYPE html>`, one `class="case-sticky"`, eleven distinct
  `id="section-*"` hosts, zero `<img src="#">`, `section-engineer-notes`
  present. `case-details--conflict.html` — 40,674 bytes, same markers.
- **D44–D50 untouched.** No new package.

## Commands and exit codes (reviewer, `.worktrees/case-039-review` at `ae38f570e`)

| Command | Exit | Result |
| --- | ---: | --- |
| `git rev-parse HEAD` | 0 | `ae38f570e3b854863c4a1961f655644f08e4a7af` |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | restored |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | 0 warnings, 0 errors |
| `dotnet test ./tests/Pegasus.Core.Tests/… --configuration Release --no-build` | 0 | 1,234 passed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/… --configuration Release --no-build` | 0 | 100 passed |
| `dotnet test ./tests/Pegasus.IntegrationTests/… --filter "FullyQualifiedName~EngineerNotePersistenceTests\|FullyQualifiedName~CaseDetailsWebTests\|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests\|FullyQualifiedName~IntakePersistenceIntegrationTests" -- xUnit.MaxParallelThreads=2` | 0 | 104 passed, 6 m 5 s |
| `pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1` | 0 | 92 migration files checked, every created table granted or exempted |

The integration filter adds `IntakePersistenceIntegrationTests` to the first
round's three classes — the class the fix touches, and the one whose absence
from the earlier local filter let finding 5 reach CI.

## CI gate — GREEN

`gh run list --branch task/case-039-engineer-notes --limit 1` →
run `33927559491`, `headSha`
`ae38f570e3b854863c4a1961f655644f08e4a7af` (equals the reviewed head),
`status: completed`, **`conclusion: success`**.

All twelve jobs succeeded: `changes`, `local-development-scripts`,
`reference-data`, `documentation`, `test-ui`, `infrastructure`, `browser`,
`unit`, `sql-integration (1)`, `sql-integration (2)`, `sql-integration (3)`,
`sql-integration-coverage`. No rerun was needed.

Merged to `dev` by the reviewer after this record was written.
