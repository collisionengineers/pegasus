# Post-implementation report — CASE-029

Branch `task/case-029-valuation-lookup-chips`, worktree `.worktrees/case-029`,
head `ffa1effed73f91c922d687081549c0fd88b08022`.
PR: https://github.com/collisionengineers/pegasus/pull/670 (targets `dev`).

## Files changed

Core:

- `src/Pegasus.Core/Vehicle/VehicleWorkflow.cs` — keyed one-field vehicle
  suggestion acceptance (make, model, mileage+unit atomically) replacing
  whole-observation accept/correct.
- `src/Pegasus.Core/Assessment/Valuations.cs` — valuation guide month;
  `ValuationPolicy` refuses a hand-recorded `Cazana` row (disabled seam).
- `src/Pegasus.Core/Documents/RequestUploadPolicy.cs` — Recipient/Reason on
  `RequestUploadLink`/`CreateRequestUploadLinkCommand`, normalised/validated.
- `src/Pegasus.Core/Cases/CaseQueries.cs` — `CaseRequestUploadSummary` gains
  nullable Recipient/Reason members (per the 2026-09-04 controller
  Correction; CASE-009's prior change to this file already merged).

Infrastructure:

- `EfVehicleWorkflowStore.cs` — per-field accept persists and clears only
  the accepted field's suggestion row; `EfVehicleLookupWorkStore`'s existing
  `AddLookupSuggestionsAsync` stays the only suggestion writer.
- `AssessmentEntities.cs`, `AssessmentModelConfiguration.cs`,
  `EfValuationStore.cs` — guide-month persistence and mapping.
- `CustodyEntities.cs`, `CustodyModelConfiguration.cs`,
  `EfDocumentRequestStore.cs` — Recipient/Reason columns, carried into
  `RequestUploadHistoryValue` so `RequireExactReplay` refuses a
  same-key/different-metadata replay.
- `EfCaseQueryStore.cs` — projects Recipient/Reason to the Case page.
- `Migrations/20260904210602_CaseValuationGuideMonthAndRequestUploadMetadata(.Designer).cs`
  + `PegasusDbContextModelSnapshot.cs` — one additive migration (columns
  only; no new table, so no new grant).

Web:

- `Pages/Cases/Shared/_CaseVehicle.cshtml` — one "Look up DVLA & MOT" action;
  per-field suggestion chips compared against `Confirmed ?? Fact`; checks
  panel/history table/whole-record forms removed; Experian stays disabled.
- `Pages/Cases/Shared/_CaseValuation.cshtml` (new) — one card per persisted
  valuation row + Add valuation dialog (Glass's; Cazana disabled).
- `Pages/Cases/Shared/_CaseDocuments.cshtml` — upload-request dialog
  (Recipient, read-only policy values, Reason) replacing direct creation.
- `Pages/Cases/Shared/_CaseHistory.cshtml` — Record-chase dialog fields
  (Recipient, Channel, Content, Outcome, Reason).
- `Pages/Cases/Vehicle.cshtml.cs` — binds the lookup and per-field chip post.
- `Pages/Cases/Valuation.cshtml` + `.cs` (new) — mutation-only route,
  `ISaveValuation` alone, lease/version/operation-key/authorization checks.
- `Pages/Cases/Custody.cshtml.cs` — binds Recipient/Reason.
- `Pages/Cases/Tasks.cshtml.cs` — maps Recipient → `TargetPartyOrAddress`,
  Content → `Note`; `AttemptedAtUtc` stays server-supplied.
- `Pages/Cases/Details.cshtml` — composes `_CaseValuation.cshtml` into the
  `section-valuation` host CASE-038 reserved (valuation include point only).
- `Pages/Cases/Details.cshtml.cs` — loads the valuation projection via the
  existing `IListCaseValuations` port inside the existing section-load path
  (valuation projection only).
- `Presentation/OperatorLabels.cs` — new CASE-029-delimited label block.
- `wwwroot/css/site.css` — valuation card/chip presentation.

Tests:

- `Pegasus.Core.Tests/Vehicle/VehicleWorkflowTests.cs`,
  `Pegasus.Core.Tests/Assessment/ValuationTests.cs`.
- `Pegasus.IntegrationTests/VehicleLookupGapFillTests.cs`,
  `AssessmentPersistenceIntegrationTests.cs`, `CaseVehicleWebTests.cs`,
  `CaseDetailsWebTests.cs`, `DocumentCustodyDurabilityTests.cs`,
  `IntakePersistenceIntegrationTests.cs` (migration list pin + a
  `requestUploadLimitsFactory` pass-through on `LocalDbTestDatabase`,
  mirroring its existing `localArtifactRootFactory` parameter).

Docs: `docs/design/test-ui/catalogue.json`, `case-details--default.html`,
`case-details--conflict.html` regenerated/updated.

## Commands and exit codes

- `dotnet restore ./Pegasus.slnx --locked-mode` — exit 0.
- `dotnet build ./Pegasus.slnx --configuration Release` — exit 0.
- `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` — 1225/1225 passed.
- `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` — 100/100 passed.
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~VehicleLookupGapFillTests|FullyQualifiedName~AssessmentPersistenceIntegrationTests|FullyQualifiedName~CaseVehicleWebTests|FullyQualifiedName~CaseDetailsWebTests|FullyQualifiedName~DocumentCustodyDurabilityTests|FullyQualifiedName~IntakePersistenceIntegrationTests" -- xUnit.MaxParallelThreads=2` — 110/110 passed.
- `pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1` — exit 0 (92 migration files checked, every created table granted or exempted; this migration adds columns only).
- `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Scope case-details -CaptureFilter "FullyQualifiedName~CaseDetailsWebTests|FullyQualifiedName~CaseVehicleWebTests|FullyQualifiedName~TestUiFocusedRenderTests"` — exit 0.
- `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope case-details` — exit 0.
- `pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1` — exit 0 (55 routed sources, 59 prototypes, 0 broken references — after adding the missing `Valuation.cshtml` entry).
- The solution-wide `dotnet test ... --filter "Category!=Corpus"` was **not**
  run locally, per the run's MECHANICS (it duplicates CI and takes ~25
  minutes); GitHub CI runs it as the merge gate.

## Snapshot artifact facts

`docs/design/test-ui/pages/case-details--default.html`: 65,965 bytes, begins
`<!doctype html>`, contains `class="case-sticky"` (1) and exactly eleven
`id="section-*"` hosts (overview, engineer-notes, inspection, vehicle,
damage, valuation, estimate, settlement, report, files, notes), no
`<img src="#">`.
`case-details--conflict.html`: 40,012 bytes, same markers.
`case-details--unavailable.html`: 24,390 bytes (error-state page; no section
hosts, as expected for that state).

## Deviations from the plan

An earlier Codex attempt (before this run resumed the ticket) used a packet
that predated the ticket's 2026-09-04 controller Resolutions and still
forbade `Details.cshtml`/`.cs`; it correctly stopped with outcome `waiting`.
The packet handed to the retry included the current plan/files documents
verbatim (which already authorize the valuation include point in
`Details.cshtml`, the projection in `Details.cshtml.cs`, and the
`CaseRequestUploadSummary` members in `CaseQueries.cs`), and the retry
completed the implementation.

Independent verification (not trusting Codex's own numbers) found and fixed
several issues, all within owned files — recorded in full, with the specific
lines and reasoning, in the ticket's plan document under "Implementation
notes and deviations (2026-09-05)". Summary: a null-reference regression in
`_CaseVehicle.cshtml` (`details.Data!` dereferenced without null-conditional
access — reverted to the pre-existing nullable pattern); two test bugs in the
new tests (wrong expected exception type; a hard-coded expected case version
that ignored an existing, unrelated version bump); a missing
`requestUploadLimitsFactory` pass-through on the shared `LocalDbTestDatabase`
test harness (added, mirroring its existing `localArtifactRootFactory`
parameter) so a new durability test could exercise the real
`EfDocumentRequestStore`; two stale test expectations from before `valuation`
became a served section; a dropped hidden `attemptedAtUtc` field and stale
chase-form field names left over from the Recipient/Content rename; an
HTML-encoding mismatch in a test assertion; and one missing
`docs/design/test-ui/catalogue.json` entry for the new `Valuation.cshtml`
route.

The Simplification pass (gpt-5.6-sol, low) found two further
behaviour-preserving cleanups (a redundant `Correction = null` reassignment;
a duplicated `observation.Id.ToString("D")` computation), both applied and
re-verified — recorded under "Simplification pass (2026-09-05)" in the plan.

No scope was added beyond the ticket's owned-path table, its 2026-09-04
Resolutions/Correction, and the one `docs/design/test-ui/catalogue.json`
entry plus the `LocalDbTestDatabase` parameter (both within the already-owned
`docs/design/test-ui/**` and `IntakePersistenceIntegrationTests.cs` files).

## Risks / follow-ups

- Per the plan's stop condition: this PR is not "wired"/Done on its own — it
  depends on [[CASE-038]] (merged) for the `Details.*` frame; [[AUTO-018]]
  adds the `MarketResearch` source/label/action to `_CaseValuation.cshtml`
  after this merges; [[UIIMP-014]] reconciles the catalogue across lanes;
  [[CASE-043]] (filed, blocked by this ticket) extends the case vehicle
  record beyond make/model/mileage.
- CI (the full `Category!=Corpus` suite and the Browser lane) has not yet run
  on this PR's head as of writing this report; the PR body names the exact
  commands and results this worktree ran.
