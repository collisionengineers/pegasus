# Research — ENG-036 (2026-09-02, gpt-5.6-terra xhigh, wrapper-checked)

Codex ran read-only in `.worktrees/research` at `origin/dev` 897db953 (the
checkout was clean afterwards). The Claude wrapper re-ran the spot checks in
the "Wrapper checks" section at the end against the repository and the board
files; every claim below either survived that check or is annotated.

## Evidence status

- **VERIFIED** — `Get-Item .\CLAUDE.md` shows it is a symbolic link to
  `AGENTS.md`; both instructions were read.

- **VERIFIED** — `git status --short; git diff --exit-code; git rev-parse
  HEAD; git rev-parse origin/dev` left the checkout clean at
  `897db9530a45063e8f684f2800685afbfdced006`, exactly `origin/dev`.

- **VERIFIED** — Kanmer `get_item ENG-036`, `get_links ENG-036`, and
  `get_doc_gates ENG-036` show a Backlog `feature` ticket, blocked by
  [[ENG-034]] and [[ENG-035]]. [[DELIV-041]] is Done, despite remaining in
  the historical `blockedBy` list.

## Current Core and persistence behaviour

- **VERIFIED** — `rg -n -C 3 'ImpactSeverity|ImpactLocation|...' \
  src/Pegasus.Core/Assessment/AssessmentContracts.cs` shows two current
  scalar paths: `assessment.impact_severity` and
  `assessment.impact_location`.

- **VERIFIED** — that command shows current severity codes are `light`,
  `light_to_moderate`, `moderate`, `moderate_to_heavy`, and `heavy`.
  Location codes are `front`, `left_front`, `right_front`, `left_side`,
  `right_side`, `rear`, `left_rear`, `right_rear`, `roof`, `underside`,
  `wheel`, `interior`, `mechanical`, and `multiple`.

- **VERIFIED** — `rg -n -C 3 'CaseAssessmentField|CaseAssessmentFields|\
  FieldPath|...' src/Pegasus.Infrastructure/Persistence/...` shows fields
  are persisted as rows in `CaseAssessmentFields`, keyed by `FieldPath`, then
  merged into a field map by `EfCaseAssessmentStore`. The database constraint
  admits only `AssessmentVocabulary.Definitions` paths.

- **VERIFIED** — `Get-Content -Raw \
  src/Pegasus.Core/Assessment/AssessmentContracts.cs` shows
  `IGetCaseAssessment`, `ISaveAssessment`, and `ICaseAssessmentStore` are the
  existing Core/adapter seams. [[ENG-035]] owns their vocabulary expansion,
  validation, derived headline fields, and migration.

- **VERIFIED** — `rg -n -i 'DamageZone|impact_severity|_CaseDamage' src tests
  docs` found no [[ENG-034]] or [[ENG-035]] output in this checkout. There is
  no list of impacts, tyre/belt data, damage type, or damage-note path yet.

## Current report behaviour

- **VERIFIED** — `Get-Content -Raw \
  src/Pegasus.Core/Reports/AssessmentReportProjection.cs` shows the report
  projection reads only the two scalar impact fields into the current report
  snapshot.

- **VERIFIED** — `Get-Content -Raw \
  src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`
  shows Scriban renders HTML from the embedded templates, Playwright Chromium
  calls `SetContentAsync`, then `PdfAsync` with `PrintBackground = true`.

- **VERIFIED** — `Get-Content -Raw \
  src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` shows
  `assessment_report.scriban` and `report.css` are embedded from
  `docs/design/assets/report-renderer/templates/`.

- **VERIFIED** — `rg -n -C 3 'PreviewReportDraft|GenerateReportDraft|File\(' \
  src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` shows the current
  preview handler returns the assessment PDF inline as `application/pdf`; the
  POST generates the same PDF as a download.

- **ASSUMED** — Chromium will print inline SVG correctly. The renderer uses
  Chromium, but `rg -n -i '<svg|svg' docs/design/assets/report-renderer\
  /templates src/Pegasus.Infrastructure/Reports \
  tests/Pegasus.IntegrationTests/Reports` found no existing report SVG or SVG
  rendering assertion. Add a renderer integration assertion before claiming
  this capability.

- **VERIFIED** — [[ENG-035]]'s current files document also claims
  `PlaywrightAssessmentReportRenderer.cs`,
  `assessment_report.scriban`, and
  `AssessmentReportRendererTests.cs`. ENG-036 cannot independently edit those
  whole files; renderer ownership must be explicitly transferred or [[ENG-035]]
  must implement the renderer-side contract supplied by ENG-036.

## Current Web behaviour and conventions

- **VERIFIED** — `rg -n -i 'impact|damage|severity|location|tyre|seat' \
  src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml ...` finds no damage-map
  markup or handlers. `git log --all --oneline --grep='ENG-025|ENG-006' -i`
  records the ENG-025 Assessment port; no vehicle diagram remains in Web.

- **VERIFIED** — `rg -n -C 4 'CanOpenAssessment|Open Assessment|\
  _Case[A-Za-z]+' src/Pegasus.Web/Pages/Cases/Details.*` shows the current
  Case page selects one partial by `?section=` and links to the separate
  Assessment page. It has neither `_CaseDamage.cshtml` nor a Case-page
  `ISaveAssessment` caller.

- **VERIFIED** — `rg -n -C 4 'case-edit-form|data-edit-save|EditLease' \
  src/Pegasus.Web/Pages/Cases/... src/Pegasus.Web/wwwroot/js/site.js` shows
  existing partials post one lease-bearing form with expected version,
  operation key, and edit-lease token. Ctrl+S submits `[data-edit-save]`.

- **ASSUMED** — after [[CASE-038]] and [[ENG-029]] land, `_CaseDamage.cshtml`
  must bind damage values to their single Case-page assessment writer; it must
  not create a second edit mode, lease, or save path.

- **VERIFIED** — `Get-ChildItem src/Pegasus.Web/wwwroot/css, \
  src/Pegasus.Web/wwwroot/js` and
  `rg -n '<link[^>]+css|<script[^>]+src' src/Pegasus.Web/Pages` show the
  application loads only `site.css` and `site.js`. There is no component
  stylesheet convention.

- **VERIFIED** — `rg -n 'data-action' src/Pegasus.Web tests` finds only the
  existing table-row action selector. The reusable convention is targeted
  `data-*` hooks plus `addEventListener`, not a general action dispatcher.

- **ASSUMED** — damage CSS belongs in `site.css`, after [[CASE-038]] releases
  or transfers that shared lock. A new stylesheet would not be loaded by the
  current layouts; placing `<style>` attributes in Razor would violate the
  existing browser CSP test.

- **VERIFIED** — `rg -n -i 'impact|damage|tyre|seat|zone|severity' \
  src/Pegasus.Web/Presentation/OperatorLabels.cs` finds no current label group
  for D39. `OperatorLabels.CaseWorkspace` is the nearest presentation group,
  but does not cover zones, severity, type, tyres, or belts.

- **ASSUMED** — a new nested D39 label group will map the [[ENG-035]] codes to
  display strings, and Razor will supply those labels to the JavaScript
  component. JavaScript must not become a second label list.

## Existing tests and design assets

- **VERIFIED** — `Get-Content -Raw \
  tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs` establishes
  the three required widths: 1580, 1100, and 760. It currently covers only
  seedless authenticated routes, not a real Case record.

- **VERIFIED** — `Get-Content -Raw \
  tests/Pegasus.IntegrationTests/Browser/AssessmentReadinessSummaryBrowserTests.cs`
  exercises the old Assessment route at 1920px. [[ENG-034]] owns its route
  retargeting; [[UIIMP-014]] owns the final Case-record browser walk.

- **VERIFIED** — `rg -n 'public.*Task' \
  tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs`
  identifies existing renderer-composition tests, including multipage PDF
  coverage. They are the appropriate proof seam for marked-SVG output.

- **VERIFIED** — `rg -n -i 'OperatorLabels' tests -g '*.cs'` found no
  mechanical label-single-ownership test. The requirement is currently an
  EPIC-012 convention, not a test.

- **VERIFIED** — `rg -n -i 'wwwroot/js|site\.js|JavaScript' \
  tests/Pegasus.ArchitectureTests -g '*.cs'` found no architecture rule
  constraining a new `wwwroot/js` component file.

- **VERIFIED** — `rg -n -C 2 'Assessment/Index|case-assessment' \
  docs/design/test-ui/catalogue.json` shows the old Assessment route remains
  a visual snapshot. [[ENG-034]] owns its redirect reclassification;
  [[UIIMP-014]] owns new Case-record snapshots and catalogue states.

- **VERIFIED** — `rg -n -i 'damage-diagram|impact|tyre-card' \
  docs/design/README.md` shows D39 component vocabulary already includes
  `damage-diagram`, `impact`, `tyre-card`, and `derived`. No ENG-036 README
  edit is needed.

## Mockup findings

- **VERIFIED** — `rg -n -C 3 'ZONES|WHEELS|damageDiagram|zone-toggle|\
  keydown' Pegasus_UI_v2_src/src/23-damage-diagram.js` shows nine body zones,
  four wheel zones, marker centres, click toggles, and Enter/Space keyboard
  activation. Editable zones are button-like and read-only zones are images.

- **VERIFIED** — that source uses mockup wheel codes `wheel_rf`, `wheel_lf`,
  `wheel_rr`, and `wheel_lr`, while the current Core vocabulary has only
  `wheel`. It also uses `light_moderate` and `moderate_heavy`, which differ
  from current Core codes. ENG-036 must consume [[ENG-035]]'s final codes, not
  preserve mockup literals.

- **VERIFIED** — `rg -n -C 3 'SECTIONS\.damage|zone-toggle|tyres|\
  report-preview' Pegasus_UI_v2_src/src/22-case-engineer.js` shows an Impacts
  row per selected zone, defaulting new rows to `moderate` and `collision`,
  with editable severity, type, note, and remove controls.

- **VERIFIED** — the mockup also contains underside/interior/mechanical chips;
  per-corner tyre and belt controls; spare tyre; centre belt; unrelated damage
  and deduction; and paint/material transfer.

- **VERIFIED** — `rg -n -C 2 'damage-layout|damage-diagram|rp-diagram|\
  tyres-grid|@media' Pegasus_UI_v2_src/src/40-engineer.css` shows the intended
  1180px and 760px reflow contracts.

- **VERIFIED** — the mockup contains "Click an area of the vehicle to add an
  impact." and "No damage recorded." They are explanatory/empty-state copy and
  must not be ported.

## Gaps, reuse, and risks

- **VERIFIED** — the current data, projection, persistence constraint, report
  template, Case partial, writer, and browser coverage all lack the D39 shape.
  [[ENG-035]] and [[ENG-034]] must merge before ENG-036 implementation begins.

- **ASSUMED** — use one SVG source asset as the geometry owner. The browser
  component should clone it for interactive/read-only states; the renderer
  should read the same embedded asset and apply report marker classes. This
  avoids duplicated path data in JavaScript and C#.

- **VERIFIED** — reuse `AssessmentVocabulary`, `AssessmentPolicy`,
  `ISaveAssessment`, `CaseMutationPageModel.NewOperationKey`,
  `data-edit-save`, `GenerateCaseAssessmentReportDraft`,
  `PlaywrightAssessmentReportRenderer.Encode`, `BrowserTestSupport`, and
  `AssessmentReportRendererTests`.

- **VERIFIED** — [[CASE-038]] owns `site.css`, `site.js`,
  `OperatorLabels.cs`, and lazy Case-section composition. A damage component
  without its registered loader/initializer would be unreachable after lazy
  rendering.

- **VERIFIED** — [[ENG-035]] owns migrations and the report projection; ENG-036
  must not add another damage table, Core policy, field path, or migration.

- **ASSUMED** — server-rendered labels/configuration must be the source for
  JavaScript ARIA names, so the static component does not duplicate Core codes
  or `OperatorLabels` values.

## Operator-only open questions

none

## Non-operator coordination required

- **VERIFIED** — [[ENG-035]] and ENG-036 both currently name
  `PlaywrightAssessmentReportRenderer.cs` and
  `AssessmentReportRendererTests.cs`. Resolve that whole-file allocation before
  implementation.

- **VERIFIED** — [[CASE-038]] owns the only loaded CSS/JavaScript entry points.
  Its implementation contract must load `damage-diagram.js` once and invoke its
  initializer for each lazy Damage section.

## Wrapper checks (Claude, 2026-09-02)

Spot checks run against `C:/Users/PC/Documents/GitHub/pegasus` (working
checkout) and `.worktrees/research` (`origin/dev` 897db953), plus the board
worktree `.worktrees/kanmer/.kanmer`:

- CONFIRMED — `sed -n 75,95p src/Pegasus.Core/Assessment/AssessmentContracts.cs`:
  `assessment.impact_severity` codes are `light`, `light_to_moderate`,
  `moderate`, `moderate_to_heavy`, `heavy`; `assessment.impact_location`
  codes are `front`, `left_front`, `right_front`, `left_side`, `right_side`,
  `rear`, `left_rear`, `right_rear`, `roof`, `underside`, `wheel`, `interior`,
  `mechanical`, `multiple`. The mockup's four wheel codes and
  `light_moderate` / `moderate_heavy` literals therefore do not match Core;
  the zone keys ENG-036 renders must be whatever [[ENG-035]] defines.
- CONFIRMED — `grep -rn "data-action" src/Pegasus.Web tests`: the only hit is
  the row selector in `wwwroot/js/site.js` (`tr[data-action]`); there is no
  general action dispatcher to reuse.
- CONFIRMED — `LayoutIntegrityTests.cs` iterates `new[] { 1580, 1100, 760 }`.
- CONFIRMED — `Pegasus.Infrastructure.csproj` embeds `assessment_report.scriban`,
  `assessment_fee_note.scriban` and `report.css` from
  `docs/design/assets/report-renderer/templates/`; `grep -rli "<svg"` over
  those templates and `src/Pegasus.Infrastructure/Reports` finds nothing, so
  inline-SVG printing through Chromium is still ASSUMED until a renderer test
  proves it.
- CONFIRMED — `AssessmentModelConfiguration.cs` maps `CaseAssessmentFields`
  with check constraint `CK_CaseAssessmentFields_FieldPath`; the vocabulary
  gate is in the database as well as in Core.
- CONFIRMED — the board's `ENG-035/files/files.md` lists
  `PlaywrightAssessmentReportRenderer.cs`, `assessment_report.scriban` and
  `AssessmentReportRendererTests.cs` as ENG-035 changes, and
  `ENG-034/files/files.md` lists `_CaseDamage.cshtml` as an ENG-034 create
  ("Read-only D30 Damage shell using only currently projected scalar
  values"). The whole-file overlap on the renderer and its tests is real and
  must be settled in the plan.
- CONFIRMED (relabelled) — Codex's "Kanmer get_item" evidence line: the
  board files show `blocks: - ENG-036` in `ENG-034.md`, `ENG-035.md` and
  `DELIV-041.md` (Done); the ticket's own frontmatter has `links: []` and
  `blocks: [UIIMP-014]`. Derived `blockedBy` is therefore ENG-034 and
  ENG-035 (DELIV-041 is satisfied).
- CONFIRMED at `origin/dev` only — `docs/design/README.md` lines 812-813 and
  1003-1006 already list `damage-diagram`, `impact` and `tyre-card` (added by
  DELIV-041, PR #647). The local `dev` checkout at 1e6ac077 predates that
  merge and does not have them; the lane must start from `origin/dev`.
- CONFIRMED — the only stylesheet and script loaded by the layouts are
  `~/css/site.css` and `~/js/site.js` (`grep "<script" src/Pegasus.Web/Pages`),
  so a new `damage-diagram.js` needs a loader change in a CASE-038-owned
  file or a `@section Scripts` convention that does not exist today.
- Codex's "Kanmer" claims were made through its own MCP access; every board
  fact used here was re-read by the wrapper from the board worktree.

## Current v1 continuation — 2026-09-08, intake_audit

This dated continuation supersedes the implementation premises above, not
the attributable 2026-09-02 research or 2026-09-03 review history. Source was
read at accepted dev aefe4c32d078ad79c0368666b5666032e6865248 through the
clean INTK-064 worktree (head 1e4f20e5718ec97abf269563fbba1fd944b944a4);
its Triage-only delta does not touch these files. Root subsequently reported
INTK-064 merge 96777888. ENG-029's unmerged writer was read separately as
prospective predecessor evidence, never represented as accepted dev.
No builds, tests, captures, source edits, take or branch creation occurred.

### Authority and supplied evidence

FRD-06's Damage section, FRD-11's report content, FRD-12's Case workspace
and the design README govern. EPIC-011 D39 is read with EPIC-012 D45:
severity and note only; Type is removed. Original Astra
`v1_implementation_plans/streams/B-casework.md` B02 lines 159–178 owns
damage editing and broad/detailed independence; B05 lines 440–476 owns
frozen report composition. B06 lines 478–516 is image preparation, owned by
[[ENG-031]], not permission to add crop work here. B08 and the ENG-036
register entry require the keyboard-operable 23-region diagram.

The immutable `pegasus_pack/more_docs/Pegasus_UI_v3.html` lines 3603–3626
supply all 23 rectangle geometries in viewBox `20 0 260 540`, body outline
and wheel positions. SHA256:
`9D03DA48CC607DB26FD39D1FDFFD9ABE9B5E54A3AB975048954B0443FE329BE9`.
The manager proposal
`pegasus_pack/ui/managers-ui-proposal/pegasus_case_dashboard_v24.html`
lines 1333–1378 corroborates the geometry, SHA256
`23BF51388ED64D159220671F908BA90DF0847A04FF2A6A88D8C66B4A71ABA9F3`.
The older v2 `src/23-damage-diagram.js` uses fewer regions and is historical,
not a second geometry owner. Transcribe only attributable geometry; use
current Core wheel codes, not prototype aliases. No inline prototype scripts,
simulated Case data, explanatory copy or alternative Case layout is imported.
Kanmer's configured external source survey returned zero sources.

### Existing policy and write path

- `AssessmentContracts.cs` lines 132–200 already owns **34** accepted
  memberships: 23 detailed geometric regions, eight broad regions, and
  underside/interior/mechanical. It owns the five severities and each field's
  tyre/restraint codes. This is the corrected complete census, not 23 total
  zones or nine broad zones. The four detailed wheels use
  `wheel_left_front`, `wheel_right_front`, `wheel_left_rear`,
  `wheel_right_rear`.
- The same file lines 234–247 owns all damage paths: impacts; four tyre and
  four belt values; spare tyre; centre belt; unrelated damage and deduction;
  material transfer; `narrative.nature_of_incident`.
  `AssessmentImpact(Zone, Severity, Note)` has no Type.
- `AssessmentPolicy.cs` lines 150–163 and 504–548 already parses, normalizes
  and derives location/highest severity. It rejects unknown or duplicate
  zones, unknown severities, extra JSON members, control characters,
  notes over 200 and serialized impacts over 4000 characters. Broad records
  remain independent and are not expanded into children. Existing
  `AssessmentPolicyTests` covers these boundaries and the 23/8 census.
- `CaseWorkspace.cs` already has typed `CaseWorkspaceDamage`;
  `CaseWorkspacePolicy.AssessmentFields` serializes its impacts through
  Core. Null means omitted; an explicit empty collection clears impacts.
  `EfCaseWorkspaceStore` owns one transaction, submitted version/lease,
  one history/version change and report staleness. No new store or policy
  is required.
- Accepted `_CaseDamage.cshtml` is only 20 lines showing three read-only
  values; it has neither diagram nor the required field editors/readouts.
  `Details.cshtml` renders Damage eagerly. The existing Case-only JS/CSS
  are already loaded. `site.js` lines 569–609 tracks native input events
  through `control.form` and supports mount binders at 1580; no new loader,
  `site.js` edit or lazy Damage route is necessary.
- ENG-029's prospective `DetailsModel.OnPostSaveAsync` lines 969–1130
  uses the single `ISaveCaseWorkspace` writer, confirmed-only values,
  posted-field presence and existing state/lease guards. Its
  `CaseWorkspaceLabels.Editors` and proposed-value retention are the
  extension points. ENG-036 must consume the accepted version of that work,
  not recreate a separate SaveAssessment or Damage POST.

### Report caller gap and smallest reuse

`AssessmentReportProjection.BuildDamage` lines 260–282 converts canonical
impact codes into display strings before putting them in
`ReportDamage.Impacts`. `PlaywrightAssessmentReportRenderer` lines 75
and 232 prints only the encoded Zone/Severity/Note table; the embedded
`assessment_report.scriban` Damage block contains no diagram.

Keep the three-member `ReportImpact` shape but retain canonical Zone and
Severity codes in it; apply the existing
`AssessmentReportPresentation.DamageZone/DamageSeverity` only when
rendering labels. The full constructor census is production projection plus
fixtures in `AssessmentReportProjectionTests`,
`AssessmentReportRenderingTests` and `AssessmentReportRendererTests`.
Update every consumer and the existing report TemplateVersion. Do not add
code/display duplicate members, reverse-label matching or a compatibility
projection.

One embedded SVG under the existing report template assets and one narrow
Infrastructure markup composer have two named production consumers:
`_CaseDamage.cshtml` and the existing Playwright report renderer. The
composer uses Core identities and encoding, not its own business taxonomy.
The Case gets server-rendered recorded markers; Case-only JS adds editing.
The PDF uses the same geometry with noninteractive marked state. Broad and
auxiliary entries remain visible in the list/controls without fabricating
detailed child markers. Existing report generation freezes snapshots; new
editing must not mutate an older issued artifact or its recorded snapshot.

### Ownership and bounded evidence

[[ENG-035]] is Done, with dev delivery ce027748aa5d00daea13a0359a5eb4a81aad912d.
[[DELIV-041]] is Done. [[ENG-034]] and [[CASE-038]] retain historical
Verifying claims; their integrated host is not permission to clear the claims.
[[ENG-029]] is actively implementing the overlapping writer, labels, retained
proposals, report projection, design README and Case tests/captures.
[[ENG-031]] is Preparing/untaken but proposes overlapping Case JS/CSS, writer,
labels, report-generation tests, docs and generated outputs. Execution needs
root's precise release/handoff and serialization; no historical claim or
dependency edge is changed in this research.

Reuse the existing Case Web factory, SQL workspace/report-generation harness,
renderer provider and BrowserTestSupport. Required focused proof is native
global Save and refusal/clear/omission, canonical saved-data report projection,
structural exact marker identity, one actual rendered PDF with visual marker
inspection, keyboard/mouse interaction at 1580/1100/760 and the three canonical
Case captures. PDF text alone cannot prove markers. Root remains the sole
heavy verifier. No new package, host, schema, worker or test framework.

The ticket title/body's Type and separate JS module promises are stale;
this current continuation and plan follow D45 and the actual Case-only module.
Root authorized title/body synchronization on 2026-09-08, preserving the
original brief separately in scratch; it does not authorize implementation.
