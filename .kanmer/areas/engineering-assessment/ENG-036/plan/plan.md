# Plan — ENG-036: edit and print the Case damage diagram

## Objective

Complete the existing Damage section: a keyboard-operable diagram, severity
and note per recorded region, existing tyre/restraint and narrative fields,
all through the single global workspace Save; print the same marked geometry
from the accepted frozen report data. No Type or image-preparation feature.

## Starting state

Evidence: research@c56007a0e80d6697; files@66f9e028c99fd81e.
Prepared 2026-09-08 by intake_audit against accepted dev aefe4c32 and the
separately identified prospective ENG-029 writer. Root later reported
INTK-064 merge 96777888; execution must pin fresh accepted dev after ownership
release. No source edit, take, build or test is authorized by this document.

Core already owns 23 detailed regions, eight broad regions and three
auxiliary regions, five severities, validation, derived headlines and typed
CaseWorkspaceDamage. The Case partial only shows three read-only values.
The report has an impacts collection/table but loses canonical identities
during projection and prints no diagram. ENG-029 supplies the required
single SaveCaseWorkspace Web writer; its implementation must be integrated
or explicitly handed off before this ticket begins.

This current plan supersedes obsolete execution premises in
plan@f5a5ac268914babd (2026-09-03, terra/sol/Claude Opus).
That complete version and its nine review dispositions remain attributable
history; their requirements are either already met or explicitly dispositioned
below. The original research is preserved before the current continuation.

## Governing docs

- **Meets** FRD-06 Damage: detailed/broad independence, severity/note,
  tyres/restraints, narratives, Core-derived location/severity.
- **Meets** FRD-11 report content and frozen-generation history.
- **Meets** FRD-12: one always-viewable Case, one Save/Discard/lease;
  existing authorized engineering edit states, including PostReport.
- **Clarifies only** the design README's Damage bullet to match the existing
  FRD-06 23 detailed/eight broad/three auxiliary model. No new requirement.
- D39 is read as corrected by D45 (no Type). Astra B02/B05 apply;
  B06 image preparation remains ENG-031. No ADR or package is necessary.

## Required changes and constraints

1. Keep Core's sole code/display/parent/validation owner. Controls read the
   existing vocabularies and field Definitions; no JavaScript taxonomy,
   Infrastructure parser, severity ranking or duplicate label-to-code map.
   Keep broad and auxiliary entries visible without expanding them to
   selected detailed children.
2. Transcribe the hash-bound v3 geometry into one embedded SVG asset.
   Preserve exact supplied coordinates; use existing canonical wheel keys.
   A narrow shared markup composer serves the actual Razor partial and
   existing Playwright renderer. Reuse the existing resource loader/cache;
   no second loader, static endpoint, SVG copy or service registration.
   All caller values are encoded; input never becomes arbitrary SVG markup.
3. Keep ReportImpact's three properties, but Zone/Severity carry canonical
   codes through projection/freezing. Existing Presentation methods produce
   visible labels at rendering. Update all three fixture consumers and bump
   the current TemplateVersion (v3 at research); no extra code/display fields,
   reverse labels, dual projection or rewrite of issued history.
4. Add native form-associated controls to case-edit-form. Bind an indexed
   impacts collection to existing AssessmentImpact/CaseWorkspaceDamage;
   the DOM retains one entirely blank add row, allowing an explicit empty
   list even after removal. All-empty row is ignored; partially filled rows
   are refused. Posted collection keys distinguish explicit clearing from
   an omitted Damage section. Do not require JavaScript to serialize JSON
   for the only persistence path. Native selects/inputs remain usable.
5. Extend ENG-029's one OnPostSaveAsync: include Damage in engineering
   submission/state checks, pass the submitted lease/version unchanged, route
   supported Damage scalar fields via the existing editor-path membership,
   and let Core validate the complete typed impacts. Null/unposted fields
   preserve accepted facts; explicit empties clear. No second Save handler,
   server-side derived input, silent invalid-data dropping or rebase.
6. Extend existing retained-proposal labels for bounded indexed damage fields
   and scalar editors. Preserve valid failed submissions, including clears,
   in the existing comparison UI. Keep existing oversize reporting and never
   retain/replay authority tokens or offer an automatic apply action.
7. Case-only JS enhances the server-rendered SVG only inside the lawful edit
   host: click/Enter/Space toggle a region and corresponding native row,
   focus remains meaningful, severity/note edits update markers and native
   input events reach existing dirty tracking. Bind once through the existing
   convention. Read-only diagrams are visible but not toggleable/focusable.
   Damage is eager; do not add a loader or lazy-mount contract.
8. Render all existing Damage values in read-only mode, and their controls
   only under the existing Engineer/state/live-lease boundary. Four tyre/belt
   cards, spare, centre belt, unrelated text/deduction, material transfer and
   Nature of incident use their current Core definitions. Derived headlines
   remain read-only. Labels/values only, no explanatory or empty-state prose.
   No image crop/rotation/ordering, estimates or settlement duplication.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Add | `docs/design/assets/report-renderer/templates/damage-diagram.svg` | Single supplied 23-region geometry; current Core data-zone keys. |
| Modify | `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` | Embed that one asset with existing report resources. |
| Add | `src/Pegasus.Infrastructure/Reports/DamageDiagramMarkup.cs` | Narrow encoded composition reused by the actual Case and PDF callers; reuse existing resource loader. |
| Modify | `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` | Keep ReportImpact's three members, retain canonical codes; advance existing TemplateVersion. |
| Modify | `src/Pegasus.Core/Reports/AssessmentReportProjection.cs` | BuildDamage retains accepted canonical identities; existing presentation owner supplies labels later. |
| Modify | `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs` | Call shared diagram composer and convert impact labels through existing Presentation methods. |
| Modify | `docs/design/assets/report-renderer/templates/assessment_report.scriban` | One marked-diagram slot in the existing Damage block; retain table and other report content. |
| Modify | `docs/design/assets/report-renderer/templates/report.css` | Bounded print sizing and visible selected markers. |
| Modify | `src/Pegasus.Web/Pages/Cases/Shared/_CaseDamage.cshtml` | Full recorded Damage view and native form-associated engineering editor. |
| Modify | `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | Extend accepted ENG-029 OnPostSaveAsync with typed Damage, posted-presence and existing authority checks. |
| Modify | `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs` | Retain bounded proposed damage row/scalar values in existing refusal UI; never authority tokens. |
| Modify | `src/Pegasus.Web/Presentation/CaseWorkspaceLabels.cs` | Extend existing editor-path and retained-label ownership; reuse Core zone/severity/code vocabularies. |
| Modify | `src/Pegasus.Web/wwwroot/js/case-workspace.js` | Idempotent diagram editing enhancement; native controls and existing form dirty events. |
| Modify | `src/Pegasus.Web/wwwroot/css/case-workspace.css` | Case-only responsive diagram, impact rows, tyre cards and focus states. |
| Modify | `tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs` | Canonical-code projection and saved-impact semantics. |
| Modify | `tests/Pegasus.Core.Tests/Reports/AssessmentReportRenderingTests.cs` | Existing contract/version and all fixture consumers updated without extra fields. |
| Modify | `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs` | Existing harness: exact SVG marker membership/encoding and actual rendered PDF. |
| Modify | `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | Existing actual Save route: typed fields, explicit clear versus omission, refusal/authority retention. |
| Modify | `tests/Pegasus.IntegrationTests/CaseEngineerSectionsWebTests.cs` | Existing recorded-value fixture: full read-only Damage and lawful edit-state controls. |
| Modify | `tests/Pegasus.IntegrationTests/Reports/CaseReportGenerationPersistenceTests.cs` | Existing SQL harness: saved canonical Damage reaches frozen report; edits stale current output without rewriting prior snapshot. |
| Add | `tests/Pegasus.IntegrationTests/Browser/CaseDamageDiagramBrowserTests.cs` | Existing BrowserTestSupport/estate: click/Enter/Space, Save/Discard and 3-width editable/read-only evidence; no new host. |
| Modify | `docs/design/README.md` | Only Damage bullet: current 23 detailed, 8 broad, 3 auxiliary scope and severity/note; retain current global Save layout. |
| Generated | `docs/design/test-ui/pages/case-details--default.html` | Regenerate only canonical default Case capture. |
| Generated | `docs/design/test-ui/pages/case-details--conflict.html` | Regenerate only canonical conflict capture. |
| Generated | `docs/design/test-ui/pages/case-details--unavailable.html` | Regenerate canonical unavailable capture if bytes change. |
| Generated if changed | `docs/design/test-ui/index.html` | Scoped catalogue generation only, after explicit index ownership handoff. |

The files document includes context-only owners and exclusions. No other
path is implicitly authorized; even a fixture omission requires an amended
whole plan/map before edits.

## Ownership prerequisite

Do not take until root approves this complete plan and records exact shared
path release/handoff from ENG-029 and any applicable historical ENG-034 /
CASE-038 claims. Keep their verification debts and records untouched.
Serialize the overlapping Details/labels/Case JS/CSS/report-generation
fixture/README/capture work with ENG-031. A technical dependency already
integrated is not an authority to release another ticket's claim.
The generated index needs its own precise handoff. No blanket docs/UI claim.

## Ordered steps

### Step 1 — Shared geometry and canonical report data

- Preconditions: approved packet, exact isolated recorded worktree from fresh
  accepted dev, resolved path handoffs.
- Files: mapped SVG, Infrastructure project, DamageDiagramMarkup,
  AssessmentReportRendering, AssessmentReportProjection,
  PlaywrightAssessmentReportRenderer, assessment_report.scriban, report.css
  and the three mapped report fixture classes.
- Change: one embedded geometry/composer; canonical ReportImpact data and
  TemplateVersion; existing display conversion and report diagram slot.
- Preserved behaviour: report table, existing fields, escaping, immutable
  older artifacts/snapshots and production renderer resource conventions.
- Negative cases: no Type member/control; no marker for unselected regions;
  broad/auxiliary entries do not light detailed children; four wheels stay
  independent; malformed/unknown code is refused rather than mislabelled.
- Done when: source census has exactly one geometry owner and both named
  production consumers, and every current ReportImpact fixture agrees.

### Step 2 — One native Case editor and writer

- Files: _CaseDamage, DetailsModel, CaseMutationPageModel,
  CaseWorkspaceLabels, case-workspace.js/css, design README and the mapped
  Case Web/section/browser tests.
- Change: implement the required native rows/scalars, lawful read-only/edit
  rendering, diagram enhancement and typed Damage contribution to the
  accepted global Save. Reuse the current dirty/lease/refusal behavior.
- Preserved behaviour: ENG-029 fields, global atomic save, version/lease,
  PostReport edit policy, recorded values in all lifecycle states and
  ENG-031 image module behavior if integrated before this work.
- Negative cases: duplicate/unknown zone, unsupported severity, incomplete
  row, 201-character note, forged engineering submission, stale version or
  lease mismatch cannot partly save; omitted differs from explicit clear.
- Done when: actual route tests capture the canonical Damage request without
  replacing the existing writer or dropping other posted sections.

### Step 3 — Focused actual-caller and visual evidence

- Files: only the mapped existing tests/new browser class; no new host or
  evidence framework. No production change merely to make a test pass.
- Tests: extend the existing SQL report-generation harness with a global
  Save of Damage, read via the existing assessment/projection source and
  freeze through the actual store. Assert canonical values in the frozen
  report, later edit stales the current generation, prior snapshot unchanged.
  Reuse existing one-workspace-save/replay/rollback tests, not new duplicates.
- Tests: deterministic SVG membership equals selected detailed zones,
  including a wheel and unselected control, encoding and broad independence.
  Render one actual PDF with the existing renderer provider; retain the
  established report text assertions and inspect an actual rendered page
  for visible correct markers. Text extraction alone is not visual proof.
- Tests: one browser cohort at 1580/1100/760 exercises click, Enter, Space,
  severity/note, explicit removal, Save/Discard and dirty behavior. Read-only
  states have no diagram mutator; native controls have labels and visible
  focus, no overflow. Capture the actual editable Damage fixture as extra
  visual evidence; default Review snapshot is not that proof.
- Commands: root alone owns locked restore/build and the focused filters in
  Commands below. Author freezes and supplies exact final method names.
- Done when: actual commands exit zero with no skipped claimed acceptance,
  artifacts and all failures retained, independent visual inspection
  completed. Missing runtime/provider/browser evidence is not PASS.

### Step 4 — Scoped snapshots and publication boundary

- Files: only the three listed Case snapshots and conditional index.
- Change: root runs the exact canonical capture inputs below, scoped update,
  verify reusing that capture, and catalogue/doc-link checks. Commit only
  generated files whose bytes changed alongside this page change.
- Preserved behaviour: no other Case route/prototype or generated page
  changes; editable Damage evidence is separate from those three baselines.
- Done when: final source/consumer/simplification review is complete, failures
  are dispositioned without erasure, root runtime evidence is recorded, and
  author report/checklist/PR to dev reach independent Review. No self-merge.

## Commands

Future execution only; root is the sole heavy owner. PowerShell on this
Windows host, cwd the recorded ENG-036 worktree. No build or capture is run
during preparation. Use the existing locked restore and one Release build:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
```

Proposed bounded runtime cohort, final new names fixed at code freeze:

- Core: existing AssessmentPolicyTests damage tests and the two mapped
  report projection/contract classes; no new damage-policy test class.
- Integration: new Damage-specific Web/global Save/retention cases,
  existing OneWorkspaceSaveWritesOneWorkflowEventAndBumpsTheVersionExactlyOnce,
  one saved-Damage freeze/staleness case, structural SVG cases and one actual
  renderer case. Use existing supplied/seeded estate; no fabricated email.
- Browser: only CaseDamageDiagramBrowserTests. Existing BrowserTestSupport
  and pinned installed browser; no new framework or speculative installs.
- Keep unique TRX/artifact paths per attempt. Stop a failed cohort, diagnose,
  and rerun only failures plus affected regressions under root scheduling.

Canonical snapshot capture inputs (default/conflict/unavailable only):

```powershell
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Scope case-details -CaptureFilter "FullyQualifiedName~CaseDetailsWebTests.ARefusedCompletenessChangeKeepsUncheckedProposalsBesideTheCurrentValues|FullyQualifiedName~CaseDetailsWebTests.CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey|FullyQualifiedName~TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor"
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope case-details
pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1
pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1
```

No repeated broad CI/build/capture loop. After independent review and merge,
exact-merge verification is separately assigned; neither this plan nor
premerge evidence claims integration/deployment.

## Historical review dispositions carried forward

The original nine-finding review and Claude Opus dispositions remain in
plan@f5a5ac268914babd and the original research (unchanged before the current
continuation). Current disposition, without rewriting those authors:

1. Report overlap/absent collection: ENG-035 collection is now integrated;
   ENG-029 overlap is an explicit execution stop, canonical-code correction
   and two actual consumers now specified.
2. Missing loader: current Case-only module already loads; use it and the
   existing binder, no new script/loader handoff.
3. Snapshot ownership: retain the 2026-09-03 resolved same-PR rule, exact
   generated ownership and root serial capture, not verify-only.
4. Verification omission: root's current focused one-heavy-owner contract
   replaces the old broad repeated commands; actual renderer/browser proof
   remains required, not a speculative reinstall.
5. D44 flags: accepted PLAT-072/CASE-049 state already removed dead flags;
   do not restore a review checkbox or add this unrelated scope.
6. Auxiliary controls: all three auxiliary and eight broad values are
   selectable/retained independently, plus 23 detailed regions.
7. Core proof: reuse existing AssessmentPolicyTests instead of duplicating
   accepted policy in a new DamageZoneTests class.
8. Marker/saved caller proof: structural SVG, actual saved-data freeze and
   actual PDF plus visual marker evidence all remain mandatory.
9. Web asset/simplification: embedded SVG serves both real callers with no
   linked static Web item; bounded independent source/simplification review
   remains owed. D45 residue is corrected in current body, not restored.

## Failure and stop conditions

Stop on an unresolved claim, changed predecessor contract, unlisted file,
new abstraction beyond the two-caller markup boundary, schema/package need,
new business choice or failed test. Report and amend before proceeding;
do not clear a historical claim, weaken assertions or invent fixtures.

Current stop: **Preparing, untaken; root full-plan review and path handoffs
outstanding**. Next is kanmer-execute only after explicit root authorization
and fresh gates/packet. Future author execution stops at independent Review,
never self-review, merge or Done.
