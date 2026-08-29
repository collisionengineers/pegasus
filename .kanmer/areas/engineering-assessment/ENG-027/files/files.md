# Files — ENG-027 Case valuations

## Owned and changed

- `src/Pegasus.Core/Assessment/Valuations.cs` (new) — `ValuationSource`
  enum + `ValuationSources` (the one source vocabulary), `ValuationDetails`,
  `CaseValuation`, `SaveValuationRequest`/`EditValuationRequest`,
  `ValuationPolicy` (validation, actor rule, `ValuedAtUtc`,
  `CurrentEngineersValue`), ports `IValuationStore`/`ISaveValuation`/
  `IEditValuation`/`IListCaseValuations`/`IGetCurrentEngineersValue`, and
  their command classes.
- `src/Pegasus.Infrastructure/Persistence/EfValuationStore.cs` (new) — EF
  adapter implementing `IValuationStore`.
- `src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs` — added
  `CaseValuationEntity`.
- `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs` —
  added the `CaseValuationEntity` Fluent config (check constraints generated
  from `ValuationSources.All`, precision, index, Case FK).
- `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` — added the
  `CaseValuations` `DbSet`.
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260829095336_CaseValuations.cs`
  (+ `.Designer.cs`) and
  `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`
  — the one migration: `CaseValuations` table, check constraints, FK, index,
  and the Web runtime-role grant in the same file.
- `src/Pegasus.Infrastructure/DependencyInjection.cs` — production
  registration of all five valuation ports.
- `scripts/Invoke-AzureDatabaseBootstrap.ps1` — `CaseValuations` added to the
  expected runtime-permission census (same diff as the migration, rule 16).
- `tests/Pegasus.Core.Tests/Assessment/ValuationTests.cs` (new) — Core unit
  tests.
- `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs` —
  added `ValuationsSaveEditListAndResolveTheCurrentEngineersValue` and
  `ValuationPortsResolveFromProductionComposition`, and extended the shared
  `Harness` with an `EfValuationStore`.

## Explicitly not touched (another ticket's scope)

- `Pages/**`, `Presentation/OperatorLabels.cs` — no UI in this ticket
  (CASE-029/ENG-028).
- `src/Pegasus.Core/Assessment/Estimates.cs`,
  `AssessmentWorkspace.cs`/`ISaveAssessment` — read as a template only, not
  modified.
- Any other migration file — this is the only migration in the branch ahead
  of `origin/dev` (verified: `git diff --name-only origin/dev...HEAD --
  .../Migrations/` returns exactly one non-generated file).
