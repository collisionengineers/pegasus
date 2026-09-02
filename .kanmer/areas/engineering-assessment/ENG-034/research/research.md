# Research — ENG-034 (2026-09-02, gpt-5.6-terra xhigh, wrapper-checked)

## Wrapper check (Claude, 2026-09-02)

Codex ran read-only in the shared detached checkout `.worktrees/research`
at `origin/dev` = `cad00be9`; `git status --porcelain` was empty afterwards.
Spot-checked against the main checkout with my own commands, all confirmed:

- `AssessmentAccessPolicy.CanOpen` requires ReportPreparation / PostReport /
  PostReportComplete and `IsReadOnly` is exactly `PostReportComplete`
  (`grep -n "CanOpen\|IsReadOnly\|PostReportComplete" src/Pegasus.Core/Assessment/AssessmentWorkspace.cs`).
- Migration `20260829095336_CaseValuations.cs` names CASE-029 as the Web
  Case-workspace valuation owner (line 53).
- Adapters `EfAssessmentAccessSource`, `EfAssessmentWorkspaceSource`,
  `EfRepairSpecificationStore`, `EfValuationStore`, `AssessmentFieldWriter`
  exist under `src/Pegasus.Infrastructure/Persistence/`.
- `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs`
  and `SendToAiIntegrationTests.cs` exist and reference the Assessment route.
- `_CaseValuation.cshtml` does not exist yet; `global.json` pins 10.0.302
  with `latestFeature`; the 301 stubs are `RedirectPermanent` in
  `Pages/Triage/Index.cshtml.cs`, `Pages/Unidentified/Index.cshtml.cs`,
  `Pages/Cases/Index.cshtml.cs`.

One correction to Codex's lane reading (see "Route, tests, and catalogue"
below): `scripts/Test-UiCatalogue.ps1` requires every routed `.cshtml` under
`src/Pegasus.Web/Pages` to be classified in `docs/design/test-ui/catalogue.json`
(`visual` entries need a captured prototype; `redirect` entries need a reason).
Keeping `Pages/Cases/Assessment/Index.cshtml` as a 301 stub therefore forces
its catalogue entry to change from `visual` (`pages/case-assessment--default.html`)
to `redirect` in the same PR, and the stale snapshot file to be removed —
exactly the ticket's own "catalogue updated" verification item, and the same
shape PLAT-029 used for its route stubs. That is a capacity-one lease on
`docs/design/test-ui/**`, not a hand-off to UIIMP-014 (which owns the new
Case-record snapshot states). The Files document carries the extra rows.

Two judgements in the Codex text are design positions, not verified facts,
and belong to the plan: (a) that CASE-038 rather than ENG-034 hosts the moved
Assessment POST handlers on the Case page model, and (b) that ENG-034 renders
no Valuation shell at all. Both are consistent with whole-file ownership
(`Details.cshtml.cs` is CASE-038's; `_CaseValuation.cshtml` is CASE-029's) but
the plan must record the agreed contract with CASE-038 before implementation.

All current-state claims below are **VERIFIED** in
`cad00be9d`; supplied D29–D43 and lane ownership are **ASSUMED** as the
authoritative ticket context.

## Current behaviour

- **VERIFIED** — `git status --short; git diff --exit-code; git rev-parse --short=9 HEAD`
  produced a clean checkout at `cad00be9d`. No files were modified.

- **VERIFIED** — `dotnet --list-sdks; Get-Content -Raw global.json`
  found SDKs `10.0.204` and `10.0.303`; `global.json` requests `10.0.302`
  with `latestFeature` roll-forward. No build or test was run.

- **VERIFIED** — `git log --oneline -20 -- src/Pegasus.Web/Pages/Cases/Assessment`
  shows ENG-028 delivered the named estimate editor in
  `7242dfba`, followed by fixes preserving imported-line evidence, clearing
  priced "To be confirmed" lines, and restoring the empty state. The current
  Assessment route is therefore the live owner of the estimate workbench.

- **VERIFIED** — `rg -n 'On(Get|Post)|AssessmentWorkspace|Estimate|Claude|Import|ReadOnly' src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs`
  shows `Assessment/Index` owns the GET projection and these handlers:
  lease claim/heartbeat/release, report-draft generation and preview, Send to
  Claude, save/duplicate/discard/set-current estimate, and estimate import.
  Its forms carry antiforgery automatically through Razor plus `id`,
  operation key, expected version where applicable, and edit-lease token.

- **VERIFIED** — `Get-Content src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs`
  shows the Assessment page loads `IGetAssessmentAccess`,
  `IGetAssessmentWorkspace`, `IGetCase`, evidence images, and estimates. It
  uses `EstimateTotals.Compute`, `EstimatePolicy`, and
  `AssessmentReportProjection.Prepare`; these are the existing owners to
  reuse, not duplicate.

- **VERIFIED** — `Get-Content -Raw src/Pegasus.Core/Assessment/AssessmentWorkspace.cs`
  shows `AssessmentAccessPolicy.CanOpen` currently requires
  `ReportPreparation`, `PostReport`, or `PostReportComplete` plus a
  current-cycle export. `IsReadOnly` is exactly
  `PostReportComplete`. D30 therefore requires a change in Web composition:
  all five Case sections must render independently of `CanOpen`; only their
  mutation controls use the existing `IsReadOnly` rule.

- **VERIFIED** — `Get-Content src/Pegasus.Web/Pages/Cases/Details.cshtml`
  and `Details.cshtml.cs` show the current Case page is a `?section=` router,
  not the D29 single-scroll frame. It selects one `_Case*` partial at a time,
  loads only `IGetAssessmentAccess`, and shows an "Open Assessment" link when
  `CanOpenAssessment` is true. It already has the one Case edit lease,
  `CaseMutationPageModel` helpers, `LeaseToken`, and the standard hidden-field
  partials.

- **VERIFIED** — `Get-Content -Raw src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml`
  shows the current nav contains Overview, Vehicle, Valuations, Inspection
  address, Case Files, and Notes only. It has neither the five Engineer section
  IDs nor the D29 order.

- **VERIFIED** — `Get-Content -Raw src/Pegasus.Core/Assessment/AssessmentContracts.cs`
  shows existing scalar assessment vocabulary includes impact location and
  severity, incident narrative, retail/trade/Engineer's values, outcome,
  salvage category/value, recovery/storage charges, report history/comments,
  signatory fields, and fee fields. It does not model D39's zone list,
  tyre/seat-belt data, or the wider D41 settlement model.

- **VERIFIED** — `rg --files src/Pegasus.Infrastructure/Persistence | rg 'Assessment|Estimate|Valuation'`
  and the corresponding source reads found existing adapters
  `EfAssessmentAccessSource`, `EfAssessmentWorkspaceSource`,
  `EfRepairSpecificationStore`, `EfValuationStore`, and
  `AssessmentFieldWriter`. Dependency injection already composes the
  Assessment access/workspace ports and AI-job creation.

- **VERIFIED** — `Get-Content -Raw src/Pegasus.Core/Assessment/Valuations.cs`
  and `20260829095336_CaseValuations.cs` show a persisted valuation model
  already exists for Glass's, Cazana, and Engineer's Value. The migration
  explicitly names CASE-029 as the Web Case-workspace owner. ENG-034 needs no
  Core, adapter, or migration change.

- **VERIFIED** — `rg -n -i 'Assessment|Estimate|Damage|Valuation|Settlement|Report' src/Pegasus.Web/Presentation/OperatorLabels.cs`
  shows there is no Engineer-section label group. Existing Case partials use
  literal `Not recorded`, but EPIC-012 requires new section vocabulary to be
  centralised in `OperatorLabels.cs`.

## Mockup and frame

- **VERIFIED** — `Get-Content .../22-case-engineer.js -TotalCount 120`
  shows the intended five sections:
  Damage has a zone diagram, impacts, tyres/seat belts, unrelated damage and
  transfer; Valuation has source cards, Engineer's Value and AI research;
  Estimate has tabs, line editor, totals, rate card, whole-page drop and Send
  to Claude; Settlement has outcomes, derived figures and salvage; Report has
  comments, signatory, fee, images, crop/order, readiness and previews.

- **VERIFIED** — `rg -n -C 2 'section-|data-lazy|scroll|CASE_SECTIONS' .../20-case.js .../21-case-sections.js`
  shows the mockup uses `section-damage`, `section-valuation`,
  `section-estimate`, `section-settlement`, and `section-report`; it renders
  the first sections then lazy-renders later ones, scrolls `?section=` into
  view, and applies scroll-spy.

- **VERIFIED** — `Get-Content -Raw .../Pegasus_UI_v2_notes.md`
  states that every section remains viewable and records `Not recorded` until
  data exists. Its older gate wording is superseded by the supplied D30.

## Bounded section scope

The implementation should be a move of the existing estimate workbench, not a
second assessment implementation.

- Damage: render a read-only shell from current scalar impact/narrative fields,
  using `Not recorded` where absent. Defer the zone diagram, zone list, tyres,
  belts, transfer, and derived impact values to ENG-035/ENG-036.

- Valuation: leave `_CaseValuation.cshtml` entirely to CASE-029. CASE-038 must
  reserve `section-valuation` in the frame so the five-section Case contract is
  complete, but ENG-034 must not create a competing minimal shell.

- Estimate: move the ENG-028 editor, totals, estimate tabs, whole-page import,
  and Send to Claude verbatim in behaviour. Reuse existing Core commands and
  validation; no copied calculation or import path.

- Settlement: render only existing scalar values as read-only where available;
  no D41 editor, derived-equity implementation, or salvage-logistics UI.
  ENG-029 owns those editors.

- Report: render existing static report values and move the existing report
  draft/preview entry points only if CASE-038 supplies their handler host.
  Defer report-image curation to ENG-031, Report field editing to ENG-029, the
  signatory tuple to DOCS-017/CASE-040, and fee-note preview to DOCS-018.

## Required CASE-038 contract

CASE-038 must provide the Case-page composition point, not ENG-034:

1. Render the five section containers with the mockup IDs and D30 ordering.
2. Supply each partial a Case ID, assessment workspace/projection, estimates,
   current edit-lease state, actor role, operation keys, and
   `IsReadOnly == PostReportComplete`.
3. Host or delegate the former Assessment POST handlers so all estimate forms
   post to a live Case-page endpoint and redirect back to
   `/Cases/{id}?section=estimate`.
4. Keep one edit mode over the Case lease. Lazy rendering must not discard
   unsaved Estimate form state.
5. Remove the Case-page "Open Assessment" action; section visibility must not
   reuse the current assessment-access gate.

## Route, tests, and catalogue

- **VERIFIED** — `Get-Content -Raw src/Pegasus.Web/Pages/Triage/Index.cshtml.cs`
  and `Unidentified/Index.cshtml.cs` show the established route-stub pattern is
  a small authorised `PageModel` whose `OnGet` returns `RedirectPermanent(...)`.

- **VERIFIED** — `rg -n 'MovedPermanently|Headers.Location' tests/Pegasus.IntegrationTests`
  shows the test convention asserts both
  `HttpStatusCode.MovedPermanently` and the exact `Location`, for example in
  `AdministrationSearchAccountWebTests`.

- **VERIFIED** — `rg -n '/Cases/.*Assessment' tests/Pegasus.IntegrationTests`
  found direct Assessment GET/POST callers in Assessment copy, vehicle,
  estimate-import, browser, report-draft, and Send-to-AI tests. They will fail
  after the redirect unless retargeted to the Case handler host.

- **VERIFIED** — `Get-Content docs/design/test-ui/catalogue.json | Select-Object -Skip 274 -First 35`
  shows the route is currently visual with
  `pages/case-assessment--default.html`. UIIMP-014 owns the new Case-record
  snapshot states. **Wrapper correction:** the catalogue entry for the retired
  route itself (`visual` → `redirect`, snapshot file removed) must ride in
  ENG-034's PR because `scripts/Test-UiCatalogue.ps1` rejects an unclassified
  or stale-classified routed source; take the `docs/design/test-ui/**` lease
  for that one edit.

## Risks

- Moving only markup leaves POST forms aimed at a retired route; all former
  handler reachability must move with the Estimate partials.
- Reusing `CanOpenAssessment` would violate D30 by hiding sections before With
  Engineer; use it nowhere for section visibility.
- `OperatorLabels.cs`, Case shared partials, frame CSS/JS, migrations, and
  snapshots are capacity-one paths. ENG-034 needs an explicit lease for its
  labels and must wait for CASE-038/ENG-035 contracts.
- The mockup fixtures contain corpus-derived personal data. Do not copy values
  into repository fixtures outside the D43-approved UIIMP workflow.

## Open questions for the operator

none
