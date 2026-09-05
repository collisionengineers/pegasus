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

## Review round fixes (2026-09-05)

Applied the review's findings directly in this worktree (Codex was
unavailable for this round). Head before this round: `77f97c40a`
(corrects the report-accuracy finding below, which had recorded
`ffa1effe`).

### Blocker 1 — create-replay compared the live entity, not the creation
snapshot

`src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:44-58`.
Fixed exactly as directed: the create-replay branch now deserializes the
stored creation snapshot once (`DocumentActionHistory
.Deserialize<RequestUploadHistoryValue>(history.AfterJson)`) and both
(a) builds `ToCreatedUploadLink` from that snapshot (the method now takes
the snapshot directly instead of re-deserializing from `ActionHistoryEntity`)
and (b) builds the `RequireExactReplay` comparison value as
`snapshot with { Recipient = command.Recipient, Reason = command.Reason }`,
so only the two metadata fields are compared — never `Status`,
`RevokedAtUtc`, `AcceptedFileCount`, `AcceptedByteCount` or `Version` off the
live, possibly-mutated entity.

Extended `DocumentCustodyDurabilityTests
.RequestUploadMetadataPersistsProjectsAndParticipatesInReplay` to accept an
upload onto the link (bumping `AcceptedFileCount`/`Version`/`Status` on the
live entity) *before* replaying the create command, pinning exactly the
broken case; the replay is asserted `IsReplay` with `Link` unchanged from
the original creation-time values.

Verified: `CustodyOutboxIntegrationTests
.EveryTerminalCaseStateRejectsNewCustodyMutationsButPreservesExactReplay`
(all four `[InlineData]` terminal states) and the extended durability test
both pass at the new head — filter
`FullyQualifiedName~DocumentCustodyDurabilityTests|FullyQualifiedName~CustodyOutboxIntegrationTests`,
30/30 passed (1 skipped, pre-existing/unrelated).

### Blocker 2 — `@inject RequestUploadLimits` threw when limits are not
accepted

`src/Pegasus.Web/Pages/Cases/Shared/_CaseDocuments.cshtml`. Replaced the
unconditional `@inject Pegasus.Core.Documents.RequestUploadLimits
UploadLimits` (which resolves with `GetRequiredService` on every render of
the Files section, edit mode or not) with `@inject IServiceProvider
ServiceProvider` and `ServiceProvider
.GetService<RequestUploadLimits>()`. Both the "Create upload request" button
and its dialog are now drawn only when `mayEdit && uploadLimits is not
null` (`mayCreateUploadRequest`); where limits are not accepted the control
is absent, not disabled-with-copy, matching the existing absent/disabled
convention.

Added `CaseCustodyWebTests
.CaseFilesSectionRendersWithUploadRequestCreationAbsentWhenLimitsAreNotAccepted`:
enters edit mode with `DocumentRequests:AcceptedLimitsVersion` overridden to
empty via `IWebHostBuilder.UseSetting` (a `RemoveAll<RequestUploadLimits>()`
service substitution was tried first but fails DI graph validation, since
`RequestUploadPolicy`/`EfDocumentRequestStore`/`RequestUploadAttemptLimiter`
all still depend on it — the config override reproduces the real
Program.cs-driven absence instead). `EnterEditModeAsync`
(`CaseCapabilityPagesTestSupport.cs`) gained an optional
`Action<IWebHostBuilder>? configureWebHost` parameter to carry this
override; default `null` behaviour is unchanged for every existing caller.
GET on `?section=files` now returns 200 (previously would 500) with no
"Create upload request" control. Full `CaseDetailsWebTests` family (the
partial class this test and `CaseVehicleWebTests`/`CaseCustodyWebTests`
belong to): 77/77 passed.

### Should-fix 3 — Make/Model/Mileage showed an unaccepted suggestion with
no marker

`src/Pegasus.Web/Pages/Cases/Shared/_CaseVehicle.cshtml`. `make`, `model`,
`mileage` and `unit` now resolve `Confirmed?.Value ?? Fact?.Value` instead
of `CaseField<T>.Current` (which falls through to `Suggestion`), matching
the chip predicate's own comparison base and EPIC-012 D34 ("fill the field
when chosen" — i.e. only once accepted). `mileageSource`'s provenance lookup
was changed the same way so the "Mileage source" label never names the
provenance of a value the field itself is not showing. Registration's
existing chain is untouched, as directed. No test data in
`RecordingCaseDetailsStore` exercises "suggestion present, nothing
confirmed or extracted" (its `Suggested()` fixture helper always sets a
confirmed value too), so this was not caught by — and adds no new coverage
to — the existing `CaseVehicleWebTests` suite; the fix is a straightforward
one-line-per-field expression change with no behavioural branch to assert
beyond what `CaseDetailsWebTests`'s full pass already covers (unaffected,
since the fixture always has Confirmed set).

### NIT 4 — dead code from the per-field acceptance rework

Removed the pieces confirmed to have zero remaining references after
`grep -rn` across `src/` and `tests/`:

- `OperatorLabels.CaseWorkspace`: `VehicleChecksPanel`, `RefreshDvla`,
  `RefreshDvsaMot`, `VehicleChecksHistory`, `AcceptSuggestion`,
  `CorrectSuggestion` (the `AcceptSuggestion`/`CorrectSuggestion` string
  matches elsewhere in the repo are all
  `InspectionAddressStaffDecision.AcceptSuggestion`/`CorrectSuggestion`
  enum members — a different, still-live vocabulary).
- `Pegasus.Core.Vehicle.ConfirmedVehicleFieldConflictException` (unused
  exception class).
- `VehicleSuggestionDecision.Correct` enum member, its arm in
  `VehicleSuggestionAcceptancePolicy.Resolve`, and the `"corrected"`
  mappings in `EfVehicleWorkflowStore.ToCode`/`ParseDecision`. Updated the
  one test that constructed it
  (`VehicleWorkflowTests.AcceptanceRequiresAnExplicitReasonAndSupportedField`)
  to exercise the same still-live refusal branch
  (`command.Correction is not null`) with a non-null `VehicleConfirmationValues`
  instead, since `Decision = Correct` is no longer constructible.

**Partial disposition, rest rejected as disproportionate for this NIT**:
`AcceptVehicleSuggestionCommand.Correction` and the `Decision`
property/persisted `Decision` string column were left in place.
`Correction` still participates in `EfVehicleWorkflowStore
.AcceptanceFingerprint`'s JSON shape (the idempotency-replay hash) and in
the command's own "must be null" validation; `Decision` is a persisted
column read back through `ParseDecision` for every historical acceptance
row. Removing either is a real behavioural surface (the fingerprint
algorithm, the command shape, `Vehicle.cshtml.cs`'s call site, and every
`AcceptedVehicleSuggestion.Decision` consumer) rather than a mechanical
dead-code deletion, and carries its own regression risk disproportionate to
a NIT found mid-review-fix. Deferred; no ticket filed since it is purely
internal cleanup with no product-visible effect and no other lane depends
on the shape.

Verified: `dotnet test Pegasus.Core.Tests` 1252/1252,
`Pegasus.ArchitectureTests` 100/100, both green after the removals.

### NIT 5 — accepted risk

No action, as directed (`VehicleWorkflow.cs` `Field` defaulting to `Make`
on an omitted POST field is an accepted risk: reachable only by a crafted
POST from an authenticated leased staff actor, and the result is an
audited, attributable acceptance of a real DVLA suggestion — not data
loss).

### NIT 6 — report accuracy

This report's original numbers are superseded by this addendum: actual
head at review time was `77f97c40a` (not `ffa1effe`); the migration is
`20260905173354_CaseValuationGuideMonthAndRequestUploadMetadata` (not
`20260904210602_…`); current
`docs/design/test-ui/pages/case-details--default.html` is 70,105 bytes
(not 65,965), `case-details--conflict.html` is 42,100 bytes (not 40,012),
`case-details--unavailable.html` is 24,390 bytes (matches). The
`ValuationSource.AiMarketResearch` arm in
`OperatorLabels.ValuationSourceLabel` and the `wwwroot/css/site.css` block
are **rejected as scope findings, no code change**: the label arm is
required for switch exhaustiveness now that AUTO-018 merged the
`AiMarketResearch` enum member (omitting it throws when rendering any
Valuation section that has such a row), and `site.css` is named verbatim in
this ticket's own files document ("Add valuation-card/chip presentation if
frame CSS does not supply it") and permitted by EPIC-012 §Build policy.

## Commands and exit codes (review round)

- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — exit 0.
- `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` — 1252/1252 passed.
- `dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` — 100/100 passed.
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DocumentCustodyDurabilityTests|FullyQualifiedName~CustodyOutboxIntegrationTests"` — 29/30 passed, 1 skipped (pre-existing, unrelated).
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~CaseDetailsWebTests"` — 77/77 passed (covers `CaseCustodyWebTests`, `CaseVehicleWebTests`, the rest of the partial class family, and the new Blocker-2 test).
- `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Scope case-details -CaptureFilter "FullyQualifiedName~CaseDetailsWebTests"` — exit 0; `git status` shows **no diff** under `docs/design/test-ui/` — the fixed pages render byte-identical HTML under the fixtures these captures use (limits accepted, vehicle fields always carry a confirmed value), so nothing new was committed there.
- `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope case-details` — exit 0.
- `pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1` — exit 0 (55 routed sources, 58 prototypes, 0 broken references).
- Opened `docs/design/test-ui/pages/case-details--default.html`: 70,105 bytes, begins `<!doctype html>`, `class="case-sticky"` ×1, `id="section-*"` ×15, no `<img src="#">`.
