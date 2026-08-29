# Post-implementation report — ENG-027

Implemented by an external agent (`codex exec`, model `gpt-5.6-sol`,
reasoning effort xhigh) under a scoped task packet; independently verified by
Claude (this session) before moving the ticket to Review. All numbers below
were re-run by Claude, not copied from the implementer's self-report.

## What changed

One commit, `26c1bbabe81a91ee26acfd6d0d2bd04b69519766`, on
`task/eng-027-case-valuations`, cut from and merged current with
`origin/dev` (merged clean, no conflicts, before implementation started).

- `src/Pegasus.Core/Assessment/Valuations.cs` (new, 240 lines) — the
  `ValuationSource` vocabulary, `ValuationDetails`/`CaseValuation` records,
  `ValuationPolicy` (validation, `RequireEngineer`, `LondonCalendar.ToUtc`,
  `CurrentEngineersValue` tie-break), and five ports (`IValuationStore`,
  `ISaveValuation`, `IEditValuation`, `IListCaseValuations`,
  `IGetCurrentEngineersValue`) with their command classes.
- `src/Pegasus.Infrastructure/Persistence/EfValuationStore.cs` (new, 322
  lines) — EF adapter: Serializable transaction, operation-key replay guard,
  `CaseMutationGuard`/`ArchivedCaseGuard`, and history writes to
  `CaseWorkflowEvents`/`ActionHistory` (`case_valuation` aggregate,
  `valuation_created`/`valuation_updated`)/`CaseHistory`.
- `AssessmentEntities.cs` (+17), `AssessmentModelConfiguration.cs` (+36),
  `PegasusDbContext.cs` (+1) — `CaseValuationEntity` and its Fluent
  configuration (check constraints generated from the one
  `ValuationSources.All` list).
- `Migrations/20260829095336_CaseValuations.cs` (+ Designer, + snapshot) —
  the one migration: `CaseValuations` table, FK to `Cases`, three check
  constraints, an index, and
  `GRANT SELECT, INSERT, UPDATE ON OBJECT::[dbo].[CaseValuations] TO
  [pegasus_web_runtime_role]` in the same file.
- `DependencyInjection.cs` (+7) — all five ports registered `Scoped` in the
  Infrastructure composition root.
- `scripts/Invoke-AzureDatabaseBootstrap.ps1` (+7) — `CaseValuations` added
  to the expected runtime-permission census.
- `tests/Pegasus.Core.Tests/Assessment/ValuationTests.cs` (new, 183 lines,
  5 tests) and `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs`
  (+138: `ValuationsSaveEditListAndResolveTheCurrentEngineersValue`,
  `ValuationPortsResolveFromProductionComposition`, plus `Harness` extension).

12 files changed, 8,542 insertions (7,423 of which are the generated
migration Designer file), 0 deletions.

## Scope conformance (independently checked)

- File set matches the ticket's "Owns" + composition-root/migration-grant
  exceptions exactly. `git diff --name-only origin/dev...HEAD` shows no file
  outside: Core record, Infrastructure store/entity/config/DbContext, the one
  migration (+generated companions), DI, the bootstrap census, and the named
  test files.
- No `Pages/**` file, no `Presentation/OperatorLabels.cs`, `Estimates.cs`, or
  `AssessmentWorkspace.cs` touched.
- Exactly one migration file added ahead of `origin/dev` (confirmed by
  `git diff --name-only origin/dev...HEAD -- .../Migrations/` excluding
  generated files) — this ticket remains the sole head of the wave-3
  migration chain for CASE-028/MAIL-027 to queue behind.
- `git diff origin/dev...HEAD -- tests/` read in full: every change is an
  addition (new test file, new test methods, additive `Harness` fields/
  constructor parameter); no existing assertion was weakened, skipped, or
  inverted.

## Build (re-run independently)

`pwsh -NoProfile -Command "dotnet build ./Pegasus.slnx --configuration
Release"` → exit 0, Build succeeded, 0 Warning(s), 0 Error(s).

## Tests (re-run independently, not copied from the implementer)

- `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj
  --configuration Release --no-build --filter
  "FullyQualifiedName~Pegasus.Core.Tests.Assessment.ValuationTests"` →
  **Passed: 5, Failed: 0, Skipped: 0**.
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj
  --configuration Release --no-build --filter
  "FullyQualifiedName~Pegasus.IntegrationTests.AssessmentPersistenceIntegrationTests.Valuation"`
  → **Passed: 2, Failed: 0, Skipped: 0** (LocalDB-backed).
- `pwsh -NoProfile -Command "& ./scripts/Test-MigrationGrants.ps1"` → exit 0,
  "83 migration files checked, every created table is granted or exempted."

All three counts reproduce the implementer's self-reported numbers exactly.

## Simplification pass (dated 2026-08-29) — dispositions

1. Interface names (`ISaveValuation`/`IEditValuation` vs. the ticket body's
   `IRecordCaseValuation`/`IAmendCaseValuation`) — **fixed by design, no
   change needed**: matches the existing `Estimates.cs` naming convention,
   which the simplicity rails prefer over the ticket body's shorthand.
2. `assessment.values.engineer` write-through named in the ticket body is
   not present — **accept as correct**: it is ENG-028's job per `waves.md`
   and this ticket's binding task packet explicitly excluded
   `AssessmentWorkspace.cs`/`ISaveAssessment`; only the read seam
   (`IGetCurrentEngineersValue`) was in scope and is delivered + covered by
   `ValuationPortsResolveFromProductionComposition`.
3. No `Pages/**` caller yet for any of the five new ports — **accept as
   correct, not a defect of this ticket**: a deliberate wave-3 (backend) /
   wave-4 (UI) split named in `waves.md`, with CASE-029 and ENG-028 the
   named forthcoming callers. Recorded, not silently carried.

No finding required a code fix; no finding was deferred to a new ticket.

## Commits

- `26c1bbabe81a91ee26acfd6d0d2bd04b69519766` — pushed to
  `origin/task/eng-027-case-valuations`; local and remote heads verified
  equal.

## Out-of-scope defects found

None observed in the files this ticket touches or its immediate neighbours.

## Risks / open questions

- "Current Engineer's Value" is defined as the latest entered London
  date/time (audit time, then id, as deterministic tie-breakers). If CASE-029
  or ENG-028 needs one-row-only or an explicit-current flag instead, that is
  their decision to make against this query's contract, not a change owed by
  this ticket.
