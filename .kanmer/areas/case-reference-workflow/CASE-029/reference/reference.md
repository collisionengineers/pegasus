# Review record — CASE-029 (PR https://github.com/collisionengineers/pegasus/pull/670)

Reviewer: Claude Opus 5 (`claude-opus-5[1m]`), independent of the build.
Built by: Claude Sonnet wrapper over gpt-5.6-sol (medium). Codex was
unavailable for the cross-model read (usage limit until 2026-09-08), so the
whole diff was read by Opus instead.

Head reviewed: `77f97c40a3aa0e81b93d9807cd571b97a55b4438`
(branch `task/case-029-valuation-lookup-chips`; merge-base with `origin/dev`
`3284f93fc3ea9fd3bbbea9405ec92dc7818378f2`; 40 files, +8504/-551).
Review checkout: detached worktree `.worktrees/case-029-review`, verified at
that exact SHA.

## Verdict

**REQUEST CHANGES — not merged.** Two blockers, one of them proven by an
existing integration test that fails at this head. The lane's focused test
filter did not include the class that covers it, so the failure was not seen
before the PR opened; CI's full `Category!=Corpus` run will fail on it.

## Findings and dispositions

| # | Severity | Location | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:44-56` | The create-replay branch now builds `afterJson` from the **live** entity (`HistoryValue(replay)`), whose `Status`, `RevokedAtUtc`, `AcceptedFileCount`, `AcceptedByteCount` and `Version` move after a revoke (`:163-166`) or an accepted upload (`:320-324`). `RequireExactReplay` compares `AfterJson` by ordinal string equality (`DocumentActionHistory.cs:84`), so an idempotent replay of the create operation key now throws once the link has been used or revoked, where it previously returned the existing link. `CustodyOutboxIntegrationTests.EveryTerminalCaseStateRejectsNewCustodyMutationsButPreservesExactReplay` fails at this head on all four terminal states. | **Fix** — returned to the implementer. Build the expected value from the stored creation snapshot (`Deserialize<RequestUploadHistoryValue>(history.AfterJson) with { Recipient, Reason }`), so only the two new metadata fields are actually compared, and extend the new durability test to replay after an accepted upload. |
| 2 | blocker | `src/Pegasus.Web/Pages/Cases/Shared/_CaseDocuments.cshtml:2` | `@inject RequestUploadLimits` is unconditional, but the service is registered only when accepted limits are configured (`DependencyInjection.cs:488-490`; `Program.cs:242-252`). Razor resolves `@inject` with `GetRequiredService`, so the Case Files section throws at render wherever limits are not accepted — including a local Development run (`appsettings.Development.json:14` sets `LocalDocumentCustody: true` with no `DocumentRequests:AcceptedLimitsVersion`). Previously the closed gate only made the *create* action fail closed. Invisible in tests because `IntakeWebTestSupport.cs:128-140` always configures accepted limits; invisible in the deployed estate because `infra/modules/platform.bicep:497-498` sets the version. | **Fix** — returned to the implementer. Resolve the limits optionally and draw the control absent (or `.gated` with its condition) when they are not accepted; add a web test rendering the Files section with the registration absent. |
| 3 | should-fix | `src/Pegasus.Web/Pages/Cases/Shared/_CaseVehicle.cshtml:11-14` | Make/Model/Mileage now display `CaseField.Current` (= `Confirmed ?? Fact ?? Suggestion`) where `dev` displayed `Confirmed`. With nothing confirmed or extracted, the field renders the unaccepted DVLA/DVSA suggestion unmarked, and the chip beside it reads "Use RENAULT" next to a field already showing RENAULT. The chip *predicate* correctly uses `Confirmed ?? Fact`, so the two halves disagree. D34 and the mockup (`21-case-sections.js:111,116-120`) render the record's own value and let the chip fill it. | **Fix** — returned with 1 and 2; one-line change in the same partial. |
| 4 | nit | `OperatorLabels.cs:1391-1406`, `VehicleWorkflow.cs:290,340,434`, `EfVehicleWorkflowStore.cs:1003,1010`, `Vehicle.cshtml.cs:59-60` | Replaced code left behind: six `CaseWorkspace` labels with no reference in `src/`; `ConfirmedVehicleFieldConflictException` unreferenced; `VehicleSuggestionDecision.Correct` and `AcceptVehicleSuggestionCommand.Correction` unreachable from any production caller. | **Fix while the branch is open** — greenfield rule 6 ("delete what you replace"); behaviour-preserving. |
| 5 | nit | `src/Pegasus.Core/Vehicle/VehicleWorkflow.cs:110-115` | `Field` is an init-only property defaulting to `Make`, not a positional parameter, so a POST omitting `field` silently accepts Make instead of being refused. | **Accept risk** — reachable only by a crafted POST from an authenticated, leased staff actor, and the outcome is a recorded, audited acceptance of a real DVLA suggestion, not data loss. Tighten if the command is touched again. |
| 6 | nit | `post-implementation-report/post-implementation-report.md` | Stale head (`ffa1effe` vs `77f97c40`), stale migration id (`20260904210602_…` vs `20260905173354_…`), stale snapshot sizes (65,965/40,012/24,390 vs measured 71,276/42,820/24,694), and the "no scope was added" line omits the `ValuationSource.AiMarketResearch` label arm and the `site.css` block. | **Fix the report**; the two additions themselves are **rejected as findings** — the `AiMarketResearch` arm is required for exhaustiveness now that AUTO-018 merged the enum member and rows without a label, and `site.css` is named in the ticket's own files document and permitted by EPIC-012 §Build policy. |

## What passed

- **Owned paths.** Every changed path sits in the files document's table, its
  2026-09-04 Correction (`Details.cshtml`, `Details.cshtml.cs`,
  `CaseQueries.cs`) or its shared-lock table. No tooling file touched:
  `TestUiSnapshotTests.cs`, `.github/workflows/ci.yml` and `scripts/*.ps1`
  are unchanged.
- **Core owns policy.** The manually-recordable-source rule and the guide-month
  rule live in `ValuationPolicy`; Recipient/Reason normalisation in
  `RequestUploadPolicy`; the field key is a Core enum. No business rule in Web
  or Infrastructure. Architecture tests 100/100.
- **One list per concept.** `ValuationSource` stays the single vocabulary and
  `OperatorLabels.ValuationSourceLabel` its only label map; Glass's valuation
  (`:1442`) stays distinct from the Glass's estimate-import label (`:381`).
- **AUTO-018 not regressed.** `ValidateAutomationMarketResearch`
  (`Valuations.cs:127`) does not call `RequireManuallyRecordableSource`, so the
  Automation Actor's `AiMarketResearch` row still saves.
- **Every drawn control has a named handler.** Lookup and the three chips post
  `RequestVehicleLookup` / `AcceptVehicleSuggestion`; Add valuation posts
  `ValuationModel.OnPostAddAsync`; the upload dialog posts
  `CreateRequestUploadLink`. Cazana and Experian are the only disabled seams
  and both state their condition.
- **No test weakened or deleted to pass.** Every removed assertion covers
  behaviour this ticket removed (whole-record Correct, the two refresh
  controls); the replacements assert more (`Assert.Single(store.Saves)`, the
  refused Cazana save, three `name="field"` chips, `DoesNotContain` for the
  legacy controls).
- **Migration ships with its grants.** Columns-only, no new table;
  `Test-MigrationGrants.ps1` exit 0. Timestamp `20260905173354` sorts after
  `dev`'s tail `20260905010654_CaseSignOffEngineer` and is pinned in
  `IntakePersistenceIntegrationTests.cs:124`.
- **Snapshot artifacts opened.** `case-details--default.html` 71,276 bytes,
  `--conflict` 42,820, `--unavailable` 24,694; each begins `<!DOCTYPE html>`;
  the two case pages carry one `class="case-sticky"` and eleven `id="section-*"`
  hosts (overview, engineer-notes, inspection, vehicle, damage, valuation,
  estimate, settlement, report, files, notes); no `<img src="#">`.
- **Simplification pass** is recorded in the plan under a dated heading with
  two honest, behaviour-preserving dispositions.

## Commands and exit codes (review checkout at 77f97c40a3aa)

| Command | Exit |
| --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 (0 warnings, 0 errors) |
| `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` | 0 — 1252/1252 passed |
| `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` | 0 — 100/100 passed |
| `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~VehicleLookupGapFillTests\|…AssessmentPersistenceIntegrationTests\|…CaseVehicleWebTests\|…CaseDetailsWebTests\|…DocumentCustodyDurabilityTests\|…IntakePersistenceIntegrationTests\|…CustodyOutboxIntegrationTests\|…ProductionCompositionTests" -- xUnit.MaxParallelThreads=2` | **1 — 148 passed, 4 failed, 1 skipped** |
| `pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1` | 0 — 95 migration files checked |

Scope rationale: the filter covers every class testing a changed type — the two
Core suites for `ValuationPolicy` and `AcceptVehicleSuggestion`, the EF suites
for `EfVehicleWorkflowStore`, `EfValuationStore`, `EfDocumentRequestStore` and
the migration list, and the two web suites for the changed partials and routes.
`CustodyOutboxIntegrationTests` and `ProductionCompositionTests` were added
beyond the lane's own filter because `EfDocumentRequestStore` and the Web
composition root changed; the first is what caught finding 1.

The four failures are all
`CustodyOutboxIntegrationTests.EveryTerminalCaseStateRejectsNewCustodyMutationsButPreservesExactReplay`
(PostReportComplete, ProviderCancelled, CollisionEngineersRejected,
CreatedInError), each
`System.InvalidOperationException: The document operation key was already used
for a different audited action.` thrown from
`DocumentActionHistory.RequireExactReplay` via `EfDocumentRequestStore.cs:45`.

No CI gate was requested for this head and the PR was **not** merged: a
blocker with a failing test stops the lane. The ticket stays in Review pending
the fixes for findings 1, 2, 3, 4 and 6.
