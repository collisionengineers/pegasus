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
