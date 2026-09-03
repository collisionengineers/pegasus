# Plan — ENG-036 (2026-09-03, gpt-5.6-terra xhigh; corrected 2026-09-03 after gpt-5.6-sol review)

## Objective

Deliver the D39 Damage Case section and a single shared SVG geometry source:
editable under the existing Case lease, read-only once Complete, and marked in
the rendered report. A damage entry is exactly `zone`, `severity`, and `note`;
no type field, label, JSON member, UI control, or report column exists.

## Starting state

Evidence was rechecked read-only at `origin/dev`/HEAD
`07ac7f1be9fc9fc04814fd5347ae5da30aff62da`. The ENG-036 ticket remains
Preparing; ENG-035 is implementing, while ENG-034, CASE-038, ENG-029, and
UIIMP-014 are Preparing. The Kanmer tunnel was unavailable (HTTP 404) during
the original planning run, so execution must refresh live gates before any
ticket move.

`damage-diagram.js`, `_CaseDamage.cshtml`, and `damage-diagram.svg` do not
exist. The existing reusable seams are `AssessmentVocabulary`,
`ISaveAssessment`, `CaseMutationPageModel.NewOperationKey`,
`data-edit-save`, `ResourceText`, `Encode`, `BrowserTestSupport`, and
`AssessmentReportRendererTests`. `Pegasus.Infrastructure.csproj` carries
`<InternalsVisibleTo Include="Pegasus.IntegrationTests" />`, so a new
internal Infrastructure type is directly assertable from an integration test.

## Governing documents

- FRD-06: damage is structured engineering evidence and the two scalar impact
  values are derived by Core.
- FRD-11: the assessment report includes the marked diagram through the
  existing `GenerateAssessmentReportDraft` caller.
- FRD-12: Damage is always viewable, editable only through the one Case edit
  lease, and read-only once Complete.
- EPIC-012 D39/D45 override the stale "type" wording in the ticket body, the
  FRDs, and `docs/design/README.md` (its Damage bullet at `origin/dev` still
  reads "each with Severity, Type, Note"): zones hold only zone, severity,
  and note. ENG-036 does not edit governing documents; the correction is
  raised in `open-questions`.
- D44: no staff act of reviewing instructions or images exists. PLAT-070 owns
  removing the surviving review flags and controls; see hand-off 7.
- No explanatory copy; web labels live only in
  `Presentation/OperatorLabels.cs`; exact state labels come from the existing
  state-label owner; excluded controls are absent, not disabled.
- Core owns validation, code sets, JSON normalization, and derived impact
  values. No package, migration, table, second writer, or compatibility path
  is added.

## Required hand-offs before implementation

1. ENG-035 must be merged and expose its D45-conformant `damage.impacts`
   contract: the canonical zone codes (the eleven body/interior codes plus
   the four individual wheel codes), existing severity codes, unique zones,
   no `type`, and Core-derived `impact_location`/`impact_severity`
   (severity = the highest zone severity).

2. ENG-035 must additionally expose the **per-zone collection** on
   `AssessmentReportSnapshot` and `AssessmentReportProjection`. At
   `origin/dev` the snapshot carries only the scalar `ImpactSeverity` and
   `ImpactLocation` (`AssessmentReportRendering.cs`,
   `AssessmentReportProjection.cs`), which cannot mark a diagram. Without a
   named, merged, test-backed zone collection on the snapshot, Step 5 stops.

3. ENG-034 and CASE-038 must be merged with the Case Damage shell,
   `DetailsModel` Damage projection, `AssessmentIsReadOnly`, lazy-section
   composition, and one lease-bearing Case form.

4. ENG-029 must extend its sole `OnPostSaveAssessmentAsync` Case writer to
   accept the submitted D39 fields for `section=damage`, still forwarding raw
   values through `ISaveAssessment`. ENG-036 must not edit
   `Details.cshtml.cs` or create a second save path.

5. CASE-038 must hand off the `site.css` and `OperatorLabels.cs` locks and
   must **keep** `site.js`. ENG-036 does not edit `site.js`. The contract
   CASE-038 must have merged, verified by name in Step 1:
   - the layout loads `~/js/damage-diagram.js` once (deferred), and
   - CASE-038's section-mount code calls
     `window.pegasusDamageDiagram.init(sectionRoot)` after the initial render
     **and** after every lazy Damage mount, exactly once per mount.
   If the merged `site.js` does not call that initializer, ENG-036 stops for
   coordination rather than shipping an unreachable component or taking the
   `site.js` lock.

6. Report files. ENG-035 keeps whole-file ownership of
   `PlaywrightAssessmentReportRenderer.cs`, `assessment_report.scriban`, and
   `AssessmentReportRendererTests.cs`. ENG-036 owns the **damage partial**
   instead — a new `DamageDiagramMarkup.cs`, the shared SVG, `report.css`
   marker rules, and a new test file — and hands ENG-035 exactly two named
   insertions:
   - `assessment["damage_diagram"] = DamageDiagramMarkup.Compose(snapshot.DamageZones);`
     in the renderer's assessment context, and
   - one `{{ assessment.damage_diagram }}` slot plus the Zone/Severity/Note
     rows in `assessment_report.scriban`.
   Both are ENG-035 edits inside ENG-035-owned files. Without those two
   insertions recorded as accepted, Step 5's report claim stops; the
   component, the SVG and the marker composition still ship and are proved by
   ENG-036's own tests.

7. PLAT-070 must be merged and the staff-review surface gone. At `origin/dev`
   `_ReadinessHiddenFields.cshtml` still posts `instructionsReviewedByStaff`
   and `imagesReviewedByStaff`, and `_CaseWorkflow.cshtml` still renders the
   "Instructions staff-reviewed" and "Images staff-reviewed" checkboxes.
   Those are `Pages/Cases/Shared/*` files ENG-036 must not touch. Step 1 fails
   closed unless the merged Case form, its handlers, its retained-value logic
   and its history carry no instruction/image review flag or act (D44).

8. UIIMP-014 owns the generated Case snapshots and the three-width browser
   walk. It must add the D39 interaction assertions and own any snapshot
   changes. ENG-036 does not edit `docs/design/test-ui/**` or its browser-test
   files. The unresolved consequence — the repository requires regenerated
   snapshots to ship in the same change set as a routed Razor-page change,
   while `docs/design/test-ui/**` is a capacity-one lock held by UIIMP-014 —
   is raised in `open-questions` and must be answered before implementation.

## Expected files

| Action | Path | Responsibility |
| --- | --- | --- |
| Add | `docs/design/assets/report-renderer/templates/damage-diagram.svg` | Single owner of zone geometry and marker anchors; no visible labels or business policy. |
| Modify | `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` | Embed that SVG beside the existing report templates. |
| Modify | `src/Pegasus.Web/Pegasus.Web.csproj` | Add the one static-web-asset item that publishes the same SVG source under `wwwroot`; no copy in the tree. |
| Add | `src/Pegasus.Web/wwwroot/js/damage-diagram.js` | One direct-event component initializer exposed as `window.pegasusDamageDiagram.init`. |
| Modify | `src/Pegasus.Web/Pages/Cases/Shared/_CaseDamage.cshtml` | Replace ENG-034's shell after it merges. |
| Modify, after lock hand-off | `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Add only required D39 web labels; no type labels. |
| Modify, after lock hand-off | `src/Pegasus.Web/wwwroot/css/site.css` | Component layout and the 1180px/760px reflow. |
| Add | `src/Pegasus.Infrastructure/Reports/DamageDiagramMarkup.cs` | Internal: read the embedded SVG and return marked report HTML for the projected zones. New file — no ENG-035 overlap. |
| Modify | `docs/design/assets/report-renderer/templates/report.css` | Marker and diagram print rules. Unclaimed by any EPIC-012 lane; Step 1 stops if a concurrent lane holds it. |
| Add | `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDamageDiagramTests.cs` | Prove the marker composition and the printed PDF. New file — ENG-035 keeps `AssessmentReportRendererTests.cs`. |
| Add | `tests/Pegasus.Core.Tests/Assessment/DamageZoneTests.cs` | Prove the D39/D45 contract ENG-036 consumes: canonical zone set, unique zones, highest-severity derivation, individual wheels, and no `type` member. New file — ENG-035 keeps `AssessmentPolicyTests.cs`. |

Do not modify Core production code, persistence, migrations, Case handler
files, `site.js`, `_ReadinessHiddenFields.cshtml`, `_CaseWorkflow.cshtml`,
`PlaywrightAssessmentReportRenderer.cs`, `assessment_report.scriban`,
`AssessmentReportRendererTests.cs`, report-image paths, governing documents,
Test UI artefacts, or any path listed as another ticket's exclusion (see
`files/files.md`'s "Files ENG-036 must not touch").

## Ordered steps

### Step 1 — Confirm merged contracts and acquire only transferred locks

- Files: none.
- Reuses: the Kanmer live gate report, ENG-035's `AssessmentVocabulary` and
  report snapshot, ENG-034's Damage partial contract, CASE-038's lazy-section
  lifecycle, and ENG-029's single `ISaveAssessment` handler.
- Change: verify all eight hand-offs above against merged `origin/dev` and
  record the result of each. Specifically:
  - grep the merged tree for `type` inside the damage contract, for
    `instructionConfirmedByStaff` / `imagesConfirmedByStaff` /
    `instructionsReviewedByStaff` / `imagesReviewedByStaff` /
    `RequireStaffImageReviewBeforeEngineerAssignment` / `ImagesReviewedByStaff`
    (D44/D45 must return nothing), and for the per-zone collection on
    `AssessmentReportSnapshot`;
  - grep the merged `site.js` for `pegasusDamageDiagram` and confirm it is
    invoked on the initial render and on lazy mount;
  - confirm `report.css` is held by no other open lane.
- Preserved behaviour: one Case edit mode, one lease, and one assessment
  writer.
- Forbidden: a `type` member, direct writes to derived impact fields, a
  second handler, an extra stylesheet, a new package, or a migration.
- Done when: every dependency is present and every file in the steps below is
  owned by ENG-036.
- Deviation stop: any failed check above — missing vocabulary, missing zone
  collection, missing writer support, a missing loader hook, a surviving
  review flag, a held `report.css`, or an unanswered open question — stops
  the ticket for coordination.

### Step 2 — Add one shared, publishable vehicle SVG

- Files: `docs/design/assets/report-renderer/templates/damage-diagram.svg`,
  `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj`,
  `src/Pegasus.Web/Pegasus.Web.csproj`.
- Reuses: the existing embedded-resource entries in
  `Pegasus.Infrastructure.csproj`, which already include the report templates
  from `..\..\docs\design\assets\report-renderer\templates\`.
- No existing convention fits the Web side: `Pegasus.Web.csproj` today only
  removes `wwwroot\lib\**` and has no linked external static asset. The new
  item is therefore a single explicit static-web-asset entry
  (`Content Include="..\..\docs\design\assets\report-renderer\templates\damage-diagram.svg"`
  with `Link="wwwroot\img\damage-diagram.svg"` and
  `CopyToPublishDirectory="PreserveNewest"`), stated here as new rather than
  claimed as existing.
- Change: create a static top-down SVG whose `data-zone` identifiers match
  ENG-035's canonical codes, including the four individual wheel codes. It
  owns paths, marker anchors, and fixed presentation attributes only; it
  contains no zone/severity labels, no user values, no code validation list,
  no `style` attribute (CSP is `default-src 'self'` with no
  `unsafe-inline`), and no executable content.
- Preserved behaviour: report assets remain embedded; browser and renderer use
  exactly one geometry file.
- Done when: the embedded resource resolves from Infrastructure and an
  integration assertion proves `GET /img/damage-diagram.svg` returns the same
  bytes with `image/svg+xml`.

### Step 3 — Implement the accessible diagram component

- Files: `src/Pegasus.Web/wwwroot/js/damage-diagram.js`.
- Reuses: targeted `data-*` hooks and `addEventListener`; no general action
  dispatcher exists to extend (the only `data-action` use is the
  `tr[data-action]` row selector in `site.js`).
- Change: expose one narrow initializer, `window.pegasusDamageDiagram.init(root)`,
  for CASE-038's loader, idempotent per mount. It fetches the same-origin SVG
  once, clones it per mount, receives server-rendered labels and the current
  records from a Razor-emitted `application/json` script block, and renders
  markers from the Core-normalized records.
- Interaction contract, stated completely:
  - **Add**: click, Enter or Space on a zone group, or on one of the three
    non-geometric zone chips (`underside`, `interior`, `mechanical` — they
    have no top-down geometry, so they are `OperatorLabels`-backed toggle
    buttons beside the diagram, as the design README's zone list requires),
    appends `{zone, severity: "moderate", note: ""}`.
  - **Remove**: the same toggle, or the row's remove control, deletes that
    entry.
  - **Severity change / note change**: the row's `select` and `input` write
    straight back into the same entry.
  - After every one of those four events the component rewrites the single
    hidden `damage.impacts` JSON field, updates the zone's marker class,
    `aria-pressed` and accessible name, updates the row list, and returns
    focus to the control that was activated.
- Preserved behaviour: all label text and ARIA names come from Razor-provided
  `OperatorLabels` values; JSON validation, canonicalization, and derivation
  remain in Core. The `moderate` default is a UI seed value for a new row,
  not a policy decision — Core still validates and derives.
- Forbidden: hard-coded labels, a damage-type control, duplicate Core codes,
  inline scripts/styles, a new edit mode, or silently discarding a malformed
  server value.
- Negative cases: unknown zones are not rendered; read-only diagrams expose
  markers but no focusable/toggleable zone or chip; Enter and Space do not
  scroll the page.
- Done when: the component initializes each Damage mount exactly once and
  maintains the posted impacts value across all four events.

### Step 4 — Compose the Damage section into the existing Case form

- Files: `src/Pegasus.Web/Pages/Cases/Shared/_CaseDamage.cshtml`,
  `src/Pegasus.Web/Presentation/OperatorLabels.cs`,
  `src/Pegasus.Web/wwwroot/css/site.css`.
- Reuses: the merged `DetailsModel`, ENG-029's
  `OnPostSaveAssessmentAsync`/`ISaveAssessment` route, Case partial form
  conventions, `CaseMutationPageModel` lease fields, `data-edit-save`, and
  existing panel/form responsive rules.
- Change: replace the shell with the diagram, the three non-geometric zone
  chips, impact rows, four tyre/belt cards, spare tyre, centre belt,
  unrelated-damage deduction, and material-transfer fields. Bind only ENG-035
  vocabulary paths. Show Core-derived impact location and severity as
  `derived` values, never editable fields. Render the form only for an active
  editable Case lease; otherwise render recorded values and a
  non-interactive marked diagram.
- Change: add only the web labels needed for headings, zones, severity,
  tyres, belts, derived values, and "Not recorded" — no type labels. Add the
  1180px one-column and 760px single-column rules using the existing
  stylesheet, under the `damage-diagram` / `impact` / `tyre-card` / `derived`
  class vocabulary the design README already carries.
- Preserved behaviour: Complete remains viewable and read-only; save posts
  only the Case's existing expected-version, operation-key, lease, and D39
  values to the one writer.
- Forbidden: explanatory or empty-state copy, a disabled inert edit surface,
  type labels/inputs/columns, CSS in Razor, calculations in Razor/JavaScript,
  or a crop/image feature.
- Done when: all D39 fields render through the one Case section and the
  component's hidden field reaches ENG-029's writer.

### Step 5 — Render the same marked geometry in the report

- Preconditions: hand-offs 2 and 6 are recorded as accepted.
- Files: `src/Pegasus.Infrastructure/Reports/DamageDiagramMarkup.cs`,
  `docs/design/assets/report-renderer/templates/report.css`,
  `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDamageDiagramTests.cs`,
  `tests/Pegasus.Core.Tests/Assessment/DamageZoneTests.cs`.
- Reuses: `ResourceText`, `Encode`, the existing Scriban context, embedded
  template loading, Playwright `SetContentAsync`/`PdfAsync`, PdfPig test
  extraction, and `InternalsVisibleTo Pegasus.IntegrationTests`.
- Change: `DamageDiagramMarkup.Compose` reads the embedded shared SVG and
  returns HTML in which exactly the projected zones carry the `impact` marker
  class and severity modifier, notes stay HTML-encoded, and nothing else in
  the document changes. `report.css` gains the marker and print rules. ENG-035
  performs the two named insertions from hand-off 6; ENG-036 supplies them
  verbatim and does not edit those two files.
- Preserved behaviour: user notes remain HTML-encoded, Chromium prints with
  backgrounds, and no template performs policy or calculation.
- Negative cases: unmarked records do not fabricate markers; each of the four
  wheels uses its own canonical wheel geometry; no type wording appears in
  the HTML or the PDF.
- Tests, in `AssessmentReportDamageDiagramTests.cs`:
  1. **Marker composition (deterministic, structural).** Call
     `DamageDiagramMarkup.Compose` directly with a body zone, one individual
     wheel, and at least one deliberately unselected zone; parse the returned
     SVG and assert the marked set is exactly the projected set — selected
     anchors present with the right severity modifier, unselected anchors
     present and unmarked. This is the assertion that proves the marker, which
     PdfPig text extraction cannot.
  2. **Printed output.** Render the report through
     `GenerateAssessmentReportDraft` from a snapshot carrying those zones and
     assert the extracted PDF text contains the diagram section heading and
     the Zone/Severity/Note evidence, and contains no type wording.
  3. **Caller evidence.** Assert that a saved `damage.impacts` value reaches
     the snapshot through ENG-035's projection, so the printed diagram is
     driven by the saved record and not by a hand-built snapshot.
- Tests, in `DamageZoneTests.cs`: canonical zone set, unique zones,
  highest-severity derivation, the four individual wheels, and the absence of
  any `type` member.
- Done when: the PDF is derived from the exact SVG asset the browser uses and
  the assertions above pass.

### Step 6 — Simplification pass, delegated visual proof, rails, and hand-off

- Files: no additional ENG-036 files.
- Simplification pass (required before the PR, AGENTS.md step 4): run
  `/simplify` plus the `code-simplifier` agent over this branch's own diff
  across the four lenses — reuse, simplification, efficiency, altitude — apply
  the behaviour-preserving fixes, and record every finding and its
  disposition in this plan under a dated "Simplification pass" heading.
  Unapplied findings are named with a reason or a follow-up ticket.
- Reuses: UIIMP-014's `BrowserTestSupport`, seeded Case walk, three-width
  layout checks, and snapshot tooling.
- Change: give UIIMP-014 the exact D39 assertions: click and Enter create
  rows from both the diagram and the three zone chips; the component
  initializes on the initial render **and** on a lazy Damage mount;
  severity/note save through the Case writer; read-only has no toggle; and the
  layout has no overflow at 1580, 1100, and 760.
- Snapshots: ENG-036 runs `./scripts/Update-TestUiSnapshots.ps1 -Verify` only.
  The update-mode run rewrites `docs/design/test-ui/**`
  (`TestUiSnapshotTests` writes the catalogue root in `update` mode), which is
  UIIMP-014's capacity-one lock. If verify reports drift, stop and hand the
  drift to UIIMP-014 under the answer recorded in `open-questions`.
- Acceptance: no test is weakened; ENG-036's own tests prove the marker
  composition and the printed diagram; UIIMP-014 provides the
  interaction/snapshot proof.
- Migration: none is in scope. If one becomes necessary, stop and assign it
  to the serialized migration owner; only then run
  `./scripts/Test-MigrationGrants.ps1` with its grants in the same diff.
- Stop condition: all owned tests and hand-off evidence pass, the
  simplification pass is recorded, the post-implementation report is written,
  the PR targeting `dev` is open, and ENG-036 is in Review. Do not merge,
  write proof, or begin another ticket.

## Commands

The canonical delivery gate, exactly as the runbook states it:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
```

Then the pinned Chromium and the report proof, and the read-only Test UI
checks:

```powershell
pwsh ./tests/Pegasus.IntegrationTests/bin/Release/net10.0/playwright.ps1 install chromium
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AssessmentReportDamageDiagramTests"
./scripts/Update-TestUiSnapshots.ps1 -Verify
./scripts/Test-UiCatalogue.ps1
```

`./scripts/Update-TestUiSnapshots.ps1` without `-Verify` is **not** run by
ENG-036: it writes `docs/design/test-ui/**`, which UIIMP-014 owns.
`./scripts/Test-MigrationGrants.ps1` is not run unless a migration enters
scope; that is a stop-and-reassign condition.

## Plan review (2026-09-03, gpt-5.6-sol xhigh; dispositions Claude Opus)

Read at `origin/dev` `07ac7f1b`. Verdict: REQUEST CHANGES; nine findings, all
dispositioned below. The reviewer's read-only checkout was clean afterwards.

| # | Severity | Finding | Disposition | Evidence |
| --- | --- | --- | --- | --- |
| 1 | blocker | Report ownership left as two alternatives; Expected files still overlap ENG-035, and the current snapshot carries only scalar `ImpactSeverity`/`ImpactLocation`, which cannot mark a diagram. | **Fixed.** The report work is decomposed into an ENG-036-owned damage partial (`DamageDiagramMarkup.cs`, the SVG, `report.css`, a new test file) plus two named insertions ENG-035 makes in its own files (hand-off 6). ENG-035 keeps the renderer, template and its test whole. Hand-off 2 now requires a merged, test-backed per-zone collection on `AssessmentReportSnapshot`, and Step 5 stops without it. | Confirmed: `AssessmentReportRendering.cs` / `AssessmentReportProjection.cs` expose only the two scalars; ENG-035's `files/files.md` claims all three report files. |
| 2 | blocker | The `damage-diagram.js` loader was required of CASE-038 but appeared in no step and no Expected file, so the component could ship unreachable. | **Fixed.** Hand-off 5 now states the exact contract (layout loads the script; `window.pegasusDamageDiagram.init(sectionRoot)` called once per initial and lazy mount), Step 1 greps merged `site.js` for it and stops otherwise, and Step 6 delegates a browser assertion covering both mount paths. `site.js` stays with CASE-038 — ENG-036 does not take the lock. | Confirmed: `_Layout.cshtml` loads only `site.css` and `site.js`. |
| 3 | blocker | `Update-TestUiSnapshots.ps1` mutates `docs/design/test-ui/**`, which the plan assigns exclusively to UIIMP-014, while the repository requires snapshots to ship with a routed-page change. | **Fixed in part, escalated in part.** Fixed: the checklist and Commands now run `-Verify` only, with a stop-and-hand-off on drift. Escalated: the underlying conflict between the same-change-set snapshot rule and the capacity-one lock is an operator/coordination decision, raised as an unticked question in `open-questions`. | Confirmed: `TestUiSnapshotTests` writes the catalogue root in `update` mode; `AGENTS.md` requires snapshots in the same change set. |
| 4 | blocker | Commands omitted the canonical `Category!=Corpus` gate and the pinned Playwright Chromium install. | **Fixed.** Commands and checklist now carry the exact canonical three commands plus `playwright.ps1 install chromium` before the report proof. | Confirmed against `docs/runbook.md` "Locked restore, build, and test" and its report-rendering section. |
| 5 | blocker | D44 was never a prerequisite, yet the staff-review flags and controls survive on `origin/dev`. | **Fixed.** D44 added to Governing documents; new hand-off 7 requires PLAT-070 merged; Step 1 fails closed on any surviving review flag or control, by name. The two files are `Pages/Cases/Shared/*` and stay excluded from ENG-036. | Confirmed: `_ReadinessHiddenFields.cshtml` posts `instructionsReviewedByStaff` / `imagesReviewedByStaff`; `_CaseWorkflow.cshtml` renders both staff-reviewed checkboxes. |
| 6 | should-fix | `underside`, `interior` and `mechanical` have no top-down geometry and had no stated control; the add/remove/severity/note event contract was not wired to the posted JSON. | **Fixed.** Step 3 now specifies `OperatorLabels`-backed zone chips for the three non-geometric zones and states all four events and what each rewrites (hidden JSON, marker class, `aria-pressed`, accessible name, row list, focus). Step 4 renders the chips. | Confirmed: the mockup uses `zone-chip` controls for exactly those three; `docs/design/README.md` requires a marker per zone including them. |
| 7 | should-fix | The task's owned paths include `tests/Pegasus.Core.Tests` damage zone tests, but the plan carried no Core proof of the contract it consumes. | **Fixed.** New ENG-036-owned file `tests/Pegasus.Core.Tests/Assessment/DamageZoneTests.cs` proves canonical zones, uniqueness, highest-severity derivation, individual wheels and the absence of `type`. ENG-035 keeps `AssessmentPolicyTests.cs`. | New file; no overlap with ENG-035's `files/files.md`. |
| 8 | blocker | PdfPig text extraction cannot prove a marker was rendered, and the proposed test bypassed the saved-record caller. | **Fixed.** Step 5 names three assertions: a deterministic structural assertion on `DamageDiagramMarkup.Compose` output (marked set exactly equals the projected set, including an unmarked control and an individual wheel), a PDF text assertion for the diagram section and Zone/Severity/Note, and caller evidence that a saved `damage.impacts` reaches the snapshot. | Confirmed: `Pegasus.Infrastructure.csproj` has `InternalsVisibleTo Pegasus.IntegrationTests`, so the internal composer is directly assertable. |
| 9 | should-fix | The Web "SDK content-item convention" does not exist in this repository, and the mandatory pre-PR simplification pass was missing. | **Fixed.** Step 2 states plainly that no Web linked-asset example exists, names the exact item and a publish/HTTP assertion; Step 6 and the checklist carry the dated simplification pass with dispositions. | Confirmed: `Pegasus.Web.csproj` contains only `<Content Remove="wwwroot\lib\**" />`. |

Two further items the reviewer did not raise, found during disposition and
fixed here: `docs/design/README.md` at `origin/dev` still describes a damage
zone as "each with Severity, Type, Note" (a D45 residue in the design
authority) and ENG-035's `files/files.md` still says "zone/type structures".
ENG-036 owns neither file; the README correction is raised in
`open-questions`, and hand-off 1 already refuses a non-D45 contract.

## Resolutions (2026-09-03)

1. **Snapshots.** ENG-036 regenerates the Test UI snapshots its own page
   change affects and commits `docs/design/test-ui/` with the change:
   `./scripts/Update-TestUiSnapshots.ps1`, then
   `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` and
   `./scripts/Test-UiCatalogue.ps1`. It is serial in wave 4 and holds the
   capacity-one lock while it runs. [[UIIMP-014]] adds new states, catalogue
   entries and the browser walk in wave 5. Step 1 no longer stops here.
2. **Design authority.** [[PLAT-070]] removes the damage `Type` from
   `docs/design/README.md` in wave 1; ENG-036 edits no governing document and
   builds against the corrected authority.
