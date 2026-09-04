# Post-implementation report — CASE-032

## What shipped

Both queue-row halves EPIC-011 §1.4 required and no ticket previously
implemented:

1. **Image-initiated row — `files·custody`.** `ImageIntakeSummary`/
   `ImageIntakeDetail` gained a nullable Core `ImageCustodyState` (`Pending`,
   `Confirmed`, `Merged`, `Failed`). `EfImageIntakeStore.ProjectAsync` and
   `ToDetailAsync` project the existing `ImageIntakes.CustodyState` column
   through the existing `ImageIntakeEntities.ImageCustodyStates` persisted
   constants — no new query. `ImageRow` renders it beside the file count via
   the existing `Join` helper; a pre-custody row (null) renders the file
   count alone.
2. **Triage row — `ref·reg` and `provider·assignee`.** `TriageSummary` gained
   nullable `Reference`/`Provider`, backed by the existing
   `InstructionDraft.ClaimNumber`/`.SuggestedPrincipalCode` owners.
   `EfTriageStore.ListAsync` and `GetByOriginReceiptAsync` share one query
   (`TriageWithDraftQuery`) that left-joins `InstructionDrafts` on
   `OriginReceiptId` — one SQL statement, no per-row lookup. `TriageRow`
   titles with `reference·registration` and metas with `provider·assignee`.

No migration: both halves project columns that already existed
(`ImageIntakes.CustodyState`, `InstructionDrafts.ClaimNumber`,
`InstructionDrafts.SuggestedPrincipalCode`).

## Files changed

- `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs` — `ImageCustodyState`
  enum; `Custody` member on `ImageIntakeSummary` (before the defaulted
  parameters) and `ImageIntakeDetail`.
- `src/Pegasus.Core/Triage/TriageContracts.cs` — nullable `Reference`,
  `Provider` on `TriageSummary`.
- `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` — selects
  `CustodyState` in `ProjectAsync` and `ToDetailAsync`; a private
  `ParseCustodyState` fail-closed mapper.
- `src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs` — one shared
  `TriageWithDraftQuery(context, predicate)` and `ToSummary(row)` behind both
  `ListAsync` and `GetByOriginReceiptAsync`; the state/origin-receipt filter
  is applied on the Triage side before the join/projection (see Deviations).
- `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs` — `ImageRow`/`TriageRow` and
  their quick-detail lists only; tabs, rail, filters, selection and the
  loader bodies untouched.
- `src/Pegasus.Web/Pages/Search/Index.cshtml.cs` — passes `byReference.Custody`
  through the exact-reference summary reconstruction.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — one
  `ImageCustodyState` mapping in a `CASE-032`-delimited block.
- `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` — extended the
  image-row test (reference, registration, file count, custody asserted
  separately); added a seeded Triage-row test asserting reference,
  registration, provider and assignee independently; added
  `StageAndCompleteEvaluationAsync`, a helper that drives
  `IIntakeWorkStore` (`ReceiveAsync`→`ClaimDispatchAsync`→
  `MarkDispatchedAsync`→`ClaimProcessingAsync`→`CompleteProcessingAsync`) so
  the seeded receipt has the real `IntakeEvaluations` row the
  `TriageOrigin.EvaluationRevisionId` FK requires.
- `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeCasePairingTests.cs`,
  `tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs`,
  `tests/Pegasus.Core.Tests/Intake/ReconcileUnidentifiedDestinationsTests.cs`
  — compatibility updates for the three positional `ImageIntakeSummary`/
  `TriageSummary` construction sites the new members broke (the last file
  was not in the files document; see Deviations).

## Deviations from the plan/files document

1. **A third `TriageSummary` construction site the files document missed.**
   `tests/Pegasus.Core.Tests/Intake/ReconcileUnidentifiedDestinationsTests.cs:142`
   also constructs `TriageSummary` positionally and failed to compile after
   Step 4. Fixed it (named arguments) as a compile-only compatibility update,
   the same category as the two named helpers.
2. **EF Core translation limitation found and fixed during the
   simplification pass.** The simplification pass's own extraction (a shared
   query returning a named `TriageWithDraftRow` record) broke `ListAsync`'s
   state filter: EF Core cannot translate a `.Where` composed *after* a
   `.Select` into a user-defined record constructor (it can after a `.Select`
   into an anonymous type, which is what the original code used). This
   surfaced as an `InvalidOperationException` → 500 on the Work Centre home
   page, caught by `NotReadyRailCountMatchesRowsAcrossBothOrigins`. Fixed by
   moving the filter onto the Triage side of the query, before the
   join/projection (an `Expression<Func<TriageEntity, bool>>` parameter on
   the shared query method). Recorded in the plan's simplification-pass
   section with the full diagnosis.
3. **Snapshot capture ran the full capture, not a scoped one** — `UIIMP-015`'s
   `-Scope`/`-CaptureFilter` switches are not present on
   `scripts/Update-TestUiSnapshots.ps1` at this branch's base (`80f0ca26`),
   confirmed by `grep -c "Scope" scripts/Update-TestUiSnapshots.ps1` returning
   `0`, exactly as the plan anticipated. Ran the full capture instead (the
   plan's fallback), under the capture lock, then verify and the catalogue
   check.

Neither deviation touches a file outside `src/Pegasus.Core`,
`src/Pegasus.Infrastructure/Persistence`, `src/Pegasus.Web`,
`tests/Pegasus.Core.Tests`, or `tests/Pegasus.IntegrationTests` — the same
project boundaries the files document names.

## Snapshot artifact

Full capture (`./scripts/Update-TestUiSnapshots.ps1`, then `-Verify
-SkipCapture`, then `Test-UiCatalogue.ps1`) ran clean — 120 browser-response
tests, 298 non-browser tests, 1 snapshot-update test, then 1 verify test, all
passed; catalogue: "54 routed sources, 58 prototypes, 0 broken local
references."

`docs/design/test-ui/pages/queues--default.html` and `queues--empty.html`
came back **byte-identical** to their committed content — 31199 bytes and
30187 bytes respectively, both begin with `<!DOCTYPE html>` — exactly as the
plan predicted ("Neither captured state currently contains an image or
Triage row"). `git diff`/`git status --porcelain -- docs/design/test-ui/`
after `git update-index --refresh` confirms zero content difference across
every page in that directory (a stat-cache artefact from the capture
rewriting timestamps, not a real change); nothing from `docs/design/test-ui/`
was committed.

## Commands run (exit codes)

- `dotnet restore ./Pegasus.slnx --locked-mode` — 0
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — 0
  (final run, after the simplification-pass fix)
- `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` — 0 (1219 passed)
- `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` — 0 (100 passed)
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~TriageQueuesWebTests"` — 0 (9 passed, final run)
- `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1` — 0
- `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` — 0
- `pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1` — 0

An earlier `dotnet test` run surfaced one real regression from the
simplification pass (`NotReadyRailCountMatchesRowsAcrossBothOrigins`
failing with `InvalidOperationException`); fixed and re-verified (see
Deviations #2) before any commit.

## Simplification pass

Recorded in the ticket plan under "## Simplification pass (2026-09-04)" and
its correction subsection: one finding rejected (the `OperatorLabels`
CASE-032 delimiter comments are the mandated parallel-build convention, not
scaffolding), two applied (named arguments in a test helper; the shared
Triage query/projection, corrected for the EF translation limitation it
introduced).

## Risks / follow-ups

None identified beyond what CASE-042 already expects (it adds the
Awaiting-instruction tab to the same `Pages/Cases/Index.cshtml.cs` after this
merges; the diff here stays confined to `ImageRow`/`TriageRow` and their
loaders are untouched).

## PR

https://github.com/collisionengineers/pegasus/pull/new/task/case-032-queue-row-projections
(opened via `gh pr create` — see the ticket's `prs` field for the number once
recorded).
