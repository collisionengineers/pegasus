# Plan — ENG-027 Case valuations

## Steps (all delivered in one commit `26c1bbab`)

1. **Core record and ports** — `src/Pegasus.Core/Assessment/Valuations.cs`,
   modelled on `Estimates.cs` (reuses `CaseMutationRequest`, `ActionActor`,
   `CaseLifecycleRules.ValidateMutation`, `RepairSpecificationPolicy
   .RequireEngineer`, `LondonCalendar`). New: the one `ValuationSource`
   vocabulary (Glass's / Cazana / Engineer's Value), `ValuationDetails`/
   `CaseValuation`, save/edit/list ports, and `IGetCurrentEngineersValue` as
   the ENG-028 seam.
2. **Infrastructure adapter** — `EfValuationStore.cs`, reusing the
   Serializable-transaction + operation-key-replay + version/lease/archived
   guard + triple history-write shape from `EfRepairSpecificationStore.cs`.
3. **EF wiring** — entity in `AssessmentEntities.cs`, Fluent config in
   `AssessmentModelConfiguration.cs` (check constraints generated from the
   one `ValuationSources.All` list, not hand-duplicated), `DbSet` in
   `PegasusDbContext.cs`.
4. **Migration + grants + census in the same diff** —
   `20260829095336_CaseValuations`, following `GrantAiJobs`'s
   `IsSqlServer()`/`RequireRuntimeRole` shape, plus the
   `Invoke-AzureDatabaseBootstrap.ps1` census entry (rule 16).
5. **Composition root** — five ports registered as `Scoped` in
   `DependencyInjection.cs`.
6. **Tests** — `ValuationTests.cs` (Core, 5 tests: vocabulary closure,
   validation, save/edit actor + forwarding, current-Engineer's-Value
   tie-break, empty-case-id rejection) and two additions to
   `AssessmentPersistenceIntegrationTests.cs` (persistence/replay/history/
   current-value round-trip, and production-composition resolution).

## Reuse named per step

Every step above names the existing file/pattern it copies; no new
abstraction was introduced beyond the one seam (`IGetCurrentEngineersValue`)
the epic's own wave plan names a concrete future caller for (ENG-028).

## Disposition — findings from my own independent verification pass (2026-08-29)

Verified independently (build, two focused test filters, migration-grants
script, full diff read) rather than trusting codex's self-report; all of its
reported numbers reproduced exactly. No findings required a fix:

1. **Naming**: ticket body says `IRecordCaseValuation`/`IAmendCaseValuation`;
   delivered `ISaveValuation`/`IEditValuation`. **Accept as correct** — matches
   the Estimates convention already in the codebase ("the existing convention
   wins" rail); behaviour is unaffected.
2. **`assessment.values.engineer` write-through** named in the ticket body's
   "What" is not delivered here. **Accept as correct** — it is explicitly
   ENG-028's job per the wave plan and this ticket's own task packet; only the
   read seam (`IGetCurrentEngineersValue`) was in scope, and it is delivered,
   registered, and covered by
   `ValuationPortsResolveFromProductionComposition`.
3. **No Pages/Web caller yet** for any of the five new ports. **Accept as
   correct, not a defect for this ticket** — this is a deliberate wave-3
   (backend) / wave-4 (UI) split named in `waves.md`; CASE-029 and ENG-028
   are the named forthcoming callers. Recorded here so the gap is visible,
   not silently carried.

No out-of-scope defect was found in any file this ticket touches or in
neighbouring files.

## Review findings — dispositions (round 2), 2026-08-29

Remediated by Claude (a different reasoner from the Codex implementation).
Merged `origin/dev` first: already up to date at `b92cb9a7`.

### [high] PR #621 CI red — the new migration was never pinned

**Fixed** (`b92edbf0`). `20260829095336_CaseValuations` appended after
`20260828112103_NamedEstimates` in the expected-migrations list at
`tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`, exactly
as ENG-026 did for its own migration. The assertion is tightened, never
loosened. `IntakePersistenceIntegrationTests.CommittedMigrationCreatesTheSqlServerSchema`
now passes locally.

### [high] Engineer's Value had two owners; the Verification criterion was unmet

**Fixed** (`d9d32f48`). The ticket's own "What" is now delivered:
`assessment.values.engineer` is written through, and the second reader is gone.

- **Write-through.** Recording or correcting an Engineer's Value row writes the
  confirmed `assessment.values.engineer` field inside the same transaction, so
  the number production actually consumes (`AiJobOperations` `EngineerValueAtSend`,
  `AssessmentReportProjection.EngineerValue`, the Assessment page) is set by the
  Valuations tab. It could not be a second `ISaveAssessment` call: the first
  mutation clears the edit lease (`CaseMutationGuard.ClearLease`), so a chained
  save would fail closed. It is one mutation, one version bump, one lease.
- **Which number.** The row carries retail and trade; the field is one Money.
  The retail figure is written, because the Engineer's Value is the pre-accident
  value a settlement is measured from (FRD-11 total-loss report: "the accepted
  Engineer value less the accepted salvage value"). **This is the one judgement
  call in the remediation** — flagged so a reviewer or the operator can overturn
  it in one line if trade was meant.
- **Latest, not last-written.** The field is resolved from the case's latest
  entered Engineer's Value row, so correcting an older row does not demote the
  field and a backdated row never overwrites a newer one. Proved by
  `ValuationsSaveEditListAndOwnTheConfirmedEngineersValueField`.
- **Second owner removed.** `IGetCurrentEngineersValue`,
  `GetCurrentEngineersValue`, `ValuationPolicy.CurrentEngineersValue` and
  `ValuedAtUtc` are deleted. They were a reader over the valuation rows that the
  ticket never named and no caller resolved; ENG-028 reads the assessment
  projection its page already uses. Their Core test went with the capability —
  stated plainly, not silently.
- **Fail closed.** An Engineer's Value row is refused when the case's assessment
  is not writable (`AssessmentPolicy.IsWritableState`) or when its retail figure
  cannot become the field, rather than persisted and silently dropped.
- **Not erased.** A row edited away from Engineer's Value re-resolves the field
  from the rows that remain; when none remain the confirmed finding is left
  standing, because nothing here erases a professional finding.

The ticket's Verification line — "Engineer's Value has one owner; grant census
passes" — is now met: one owner, and `Test-MigrationGrants.ps1` exits 0.

### [medium] Authorisation narrower than the ticket specifies, unreported

**Fixed** (`d9d32f48`). `RepairSpecificationPolicy.RequireEngineer` is gone from
this file. A market source (Glass's, Cazana) now takes the ticket's own staff
`PerformCasework`, via `StaffAuthorization.Require` — the same right every other
case mutation uses, so the Automation Actor keeps it too (ADR-0011).

An Engineer's Value row additionally requires an authenticated staff Engineer,
and that is **not** a re-narrowing: the row writes the confirmed
`assessment.values.engineer` field, which `AssessmentVocabulary` marks
`IsFinding: true`, and a professional finding has always been staff-Engineer-only.
That rule is now taken from its single owner rather than restated:
`AssessmentPolicy.RequireFindingConfirmationAuthority` (`4981a4c3`), which
`AssessmentPolicy.ValidateAndNormalize` calls on its own staff branch,
behaviour-preserving. The ticket's `PerformCasework` and the finding rule are
both honoured; the deviation is now the deliberate, stated consequence of the
write-through the ticket asked for.

### [medium] A valuation refusal spoke about a repair specification

**Fixed** (`d9d32f48`), by the same change: nothing in the valuation path calls
`RepairSpecificationPolicy` any more, so "…change or accept a repair
specification" can no longer surface on a valuation.

### [low] Two orderings of "the latest valuation"

**Fixed** (`d9d32f48`). One `OrderKey` — entered date, entered time, audit time,
identity — serves both the row listing and the current-value resolution. The
UTC-instant ordering went with `ValuedAtUtc`, so the two can no longer disagree
across the DST fall-back hour. The SQL `ORDER BY` became an in-memory sort over
the same key: a case's valuations are a handful of rows, and one definition
beats two that agree by inspection.

### [low] No production caller for the new ports — rule 14

**Accepted, with the gap reported, not closed.** This lane cannot close it:
CASE-029 owns `Pages/Cases/Valuations.*` and ENG-028 owns
`Pages/Cases/Assessment/**` (waves.md, wave 4), and building either here would
absorb another ticket's scope. Two things did change:

- The unreachable surface is smaller — four registrations, not five.
- The Engineer's Value is no longer stranded. Whatever wave 4 builds, the
  number reaches production through the field the product already reads.

Under EPIC-011 D20 this still blocks **Done**, not review. It is disclosed here
so the Verifying gate sees it rather than inheriting a claim.

### Unreported defect found while remediating — fixed in this lane's own file

`ValuationSourceDefinition` carried operator-facing names ("Glass's", "Cazana",
"Engineer's Value") in `Pegasus.Core`, while the codebase's own convention puts a
source's operator-facing name in `Presentation/OperatorLabels.cs`
(`RepairSpecificationSourceRoute.Glasses => "imported from Glass's"`), and
EPIC-011's context binds every member ticket to "labels live in
OperatorLabels.cs". No caller read the names — their only reader was the test
asserting them. **Fixed** (`21364cc5`): the enum is the whole vocabulary, the
check constraint is generated from `Enum.GetValues`, and the label belongs to
CASE-029 when it renders the tab. Adding it to `OperatorLabels.cs` now would be
an unreachable registration.

### Correction to the round-1 post-implementation report

Two claims in it no longer stand and should not be read forward:

- `"outcome": "pr-ready"` was refuted by red CI; it is only defensible after
  PR #621 re-runs green on this push.
- "ENG-028 … must write `assessment.values.engineer` through the existing
  `ISaveAssessment`" named work no board record carried. It is moot: this lane
  writes it.

### Verification re-run (this branch, after every change above)

| Command | Result |
| --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release` | exit 0 — Build succeeded, 0 Warning(s), 0 Error(s) |
| `dotnet test ./tests/Pegasus.Core.Tests` | exit 0 — Failed: 0, Passed: 1140, Skipped: 0 |
| `dotnet test ./tests/Pegasus.ArchitectureTests` | exit 0 — Failed: 0, Passed: 100, Skipped: 0 |
| `dotnet test ./tests/Pegasus.IntegrationTests --filter 'FullyQualifiedName~AssessmentPersistenceIntegrationTests\|…IntakePersistenceIntegrationTests.CommittedMigrationCreatesTheSqlServerSchema'` | exit 0 — Failed: 0, Passed: 13, Skipped: 0 (1 m 19 s) |
| `scripts/Test-MigrationGrants.ps1` | exit 0 — 83 migration files checked, every created table is granted or exempted |

The full `Category!=Corpus&Category!=Browser` integration suite also ran green
(exit 0 — Failed: 0, Passed: 1024, Skipped: 2, 14 m 53 s), on the build one
commit before `21364cc5`; the focused re-run above covers everything that commit
touched.

### Simplification pass — 2026-08-29 (round 2)

Run over this round's own diff. Findings all applied, none deferred: three
concepts that had two homes were given one each
(`RequireFindingConfirmationAuthority`, `NormalizeFieldValue`,
`AssessmentFieldWriter`), one ordering replaced two, and four dead symbols plus
one dead label list were deleted rather than left behind. Net: 8 files, and the
lane's Core file is shorter than it was.

## Round 3 — Codex remediation of the round-2 verifier's refusal, 2026-08-29

The round-2 Codex verifier closed the migration-list finding but refused the
single-owner finding, reporting that round 2's own remediation had introduced
two new defects in `EfValuationStore.WriteEngineersValueAsync`. Claude does not
get a third attempt at its own work on this lane; Codex (`gpt-5.6-sol`,
`model_reasoning_effort=xhigh`) drove the fix, with the orchestrating agent
merging `origin/dev` first and independently re-verifying every claim below
rather than trusting Codex's self-report.

### Merge

`origin/dev` merged clean except two additive conflicts, both resolved by
keeping both sides in migration-timestamp order: the expected-migrations list
in `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` (this
lane's `CaseValuations` entry plus TICK-058's `ProviderSubmissions`/
`ProviderDeclaredInstruction` entries) and the matching grant-census block in
`scripts/Invoke-AzureDatabaseBootstrap.ps1`. `OperatorLabels.cs` merged
automatically with every lane's nested static class intact. Merge commit
`370c53d2`.

### [high] Backdated Engineer's Value stamped with the wrong provenance — fixed

`WriteEngineersValueAsync` selects the case's latest **entered** Engineer's
Value row (by date/time, not save order), but was stamping the resulting
`assessment.values.engineer` field with the *current request's* actor/time
regardless of which row was actually selected. A backdated row from a
different Engineer could therefore overwrite another Engineer's provenance on
a value it did not confirm. Fixed (`3ad69881`): the field now takes
`recordedBy`/`recordedAtUtc` from the **selected** row's own
`LastEditedBy ?? RecordedBy` / `LastEditedAtUtc ?? RecordedAtUtc`, via a
widened `AssessmentFieldWriter.Write` that accepts explicit provenance instead
of assuming the caller's actor. `EfCaseAssessmentStore` (the only other
caller) passes its own request actor/time through unchanged — behaviour there
is unaffected.

Regression: `BackdatedEngineersValueKeepsTheSelectedRowsProvenance` — a second
Engineer saves an earlier-dated row after the first Engineer's row exists;
proves the field keeps the first Engineer's identity/time, not the second
Engineer's. Independently confirmed: reverting the three production files to
their pre-fix state and rebuilding fails this test (`Assert.Equal` on the
recorded-by GUID, actual is the backdating Engineer's id, not the selected
row's) and the clearing test below; both pass again once the fix is restored.

### [high] Editing the only Engineer's Value to another source left a stale field — fixed

Editing a case's last remaining Engineer's Value row to Glass's/Cazana drove
`latest` to null, and the method returned early **without touching the
existing field** — the confirmed `assessment.values.engineer` value from the
now-gone row stayed on the Case, readable as if it were still current. Fixed
(`3ad69881`): when no Engineer's Value row remains, the existing field is
removed from `CaseAssessmentFields` (history records a nullable `After`); a
Case with no Engineer's Value row now reads as having none.

Regression: `EditingTheOnlyEngineersValueToAnotherSourceClearsTheAssessmentOwner`
— saves one Engineer's Value, confirms the field exists, edits it to Glass's,
asserts the field is gone. Independently confirmed failing pre-fix (asserted
null, actual was the stale `12000.00` field) and passing post-fix.

### Verification re-run (this branch, independently, after restoring the fix)

| Command | Result |
| --- | --- |
| `dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj -c Release` | exit 0 — 0 Warning(s), 0 Error(s) |
| `dotnet test … --filter FullyQualifiedName~BackdatedEngineersValueKeepsTheSelectedRowsProvenance\|…EditingTheOnlyEngineersValueToAnotherSourceClearsTheAssessmentOwner\|…ValuationsSaveEditListAndOwnTheConfirmedEngineersValueField` | exit 0 — Passed: 3, Failed: 0 |
| Same filter (two new tests only) against source reverted to pre-fix | exit 1 — Passed: 0, Failed: 2 (proves the tests are real, not vacuous) |
| `./scripts/Test-MigrationGrants.ps1` | `$? = True` — 86 migration files checked, every created table granted or exempted |
| `dotnet build ./Pegasus.slnx -c Release` | exit 1 — **see below**, not this lane's defect |

`git diff origin/dev...HEAD -- tests/` carries no removed `Assert.` line and no
new `Skip` attribute anywhere in the lane's full diff; the round-3 test edits
only strengthen the existing ownership test (added RecordedBy/RecordedAtUtc/
ConfirmedAtUtc checks, switched the read helper to go through the production
`GetCaseAssessment` reader ENG-028 will use) and add the two regressions
above.

### Out-of-lane defect found, not fixed — solution-wide build is broken on `origin/dev`

`dotnet build ./Pegasus.slnx --configuration Release` fails (exit 1) with one
compile error, confirmed to originate on `origin/dev` itself (`git diff
origin/dev...HEAD` is empty for both files; `git show origin/dev:<path>`
carries the same mismatch):

```
tests/Pegasus.Core.Tests/ProviderApi/ProviderSubmissionTests.cs(284,13): error CS1739:
The best overload for 'QueuedIntakeStatus' does not have a parameter named 'CaseId'
```

`QueuedIntakeStatus` (`src/Pegasus.Core/Intake/DurableIntake.cs:93`) carries no
`CaseId` parameter; the test constructs one with `CaseId: null`. Both files are
byte-identical to `origin/dev` — this lane touches neither. It looks like a
merge-shaped gap between TICK-058 (added the test) and a concurrent lane that
narrowed `QueuedIntakeStatus`, neither conflicting at the text level so GitHub's
merge let both through. `Pegasus.Core`, `Pegasus.Infrastructure`, `Pegasus.Web`,
`Pegasus.IntegrationTests`, `Pegasus.Worker` and `Pegasus.ArchitectureTests` all
build clean; only `Pegasus.Core.Tests` fails on this one file. Reported per
AGENTS.md ("touch only your lane's files; report defects outside them"); not
touched here. Whoever owns `ProviderApi`/`DurableIntake` (TICK-058/061 lineage)
needs to fix this before any lane can claim a green solution-wide build.

### Verdict

Both high findings from the round-2 verifier are closed with tests that fail
on the pre-fix code and pass on the fix, independently reproduced by the
orchestrating agent (not just Codex's self-report). The ticket's Verification
line — one owner for the Engineer's Value, grant census passing — holds.
Commits pushed: `370c53d2` (merge), `3ad69881` (remediation). Ticket remains in
`review`; not moved.
