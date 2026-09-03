# Plan — ENG-036 (2026-09-03, gpt-5.6-terra xhigh)

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
this planning run, so execution must refresh live gates before any ticket
move.

`damage-diagram.js`, `_CaseDamage.cshtml`, and `damage-diagram.svg` do not
exist. The existing reusable seams are `AssessmentVocabulary`,
`ISaveAssessment`, `CaseMutationPageModel.NewOperationKey`,
`data-edit-save`, `ResourceText`, `Encode`, `BrowserTestSupport`, and
`AssessmentReportRendererTests`.

## Governing documents

- FRD-06: damage is structured engineering evidence and the two scalar impact
  values are derived by Core.
- FRD-11: the assessment report includes the marked diagram through the
  existing `GenerateAssessmentReportDraft` caller.
- FRD-12: Damage is always viewable, editable only through the one Case edit
  lease, and read-only once Complete.
- EPIC-012 D39/D45 override the stale "type" wording in the ticket body and
  the FRDs: zones hold only zone, severity, and note.
- No explanatory copy; web labels live only in
  `Presentation/OperatorLabels.cs`; exact state labels come from the existing
  state-label owner; excluded controls are absent, not disabled.
- Core owns validation, code sets, JSON normalization, and derived impact
  values. No package, migration, table, second writer, or compatibility path
  is added.

## Required hand-offs before implementation

1. ENG-035 must be merged and expose its D45-conformant `damage.impacts`
   contract: the canonical zone codes, existing severity codes, unique
   zones, no `type`, and Core-derived `impact_location`/`impact_severity`.

2. ENG-034 and CASE-038 must be merged with the Case Damage shell,
   `DetailsModel` Damage projection, `AssessmentIsReadOnly`, lazy-section
   composition, and one lease-bearing Case form.

3. ENG-029 must extend its sole `OnPostSaveAssessmentAsync` Case writer to
   accept the submitted D39 fields for `section=damage`, still forwarding raw
   values through `ISaveAssessment`. ENG-036 must not edit
   `Details.cshtml.cs` or create a second save path.

4. CASE-038 must hand off the `site.css`, `site.js`, and
   `OperatorLabels.cs` locks. Its loader must load `damage-diagram.js` once
   and invoke its initializer after the initial and every lazy Damage mount.

5. ENG-035 must either transfer whole-file ownership of the report renderer,
   report template, and renderer test to ENG-036, or make the precise
   renderer-side change supplied by this plan. Without one of those recorded
   outcomes, stop: the marked report requirement cannot be claimed.

6. UIIMP-014 owns the generated Case snapshots and the three-width browser
   walk. It must add the D39 interaction assertions and own any snapshot
   changes. ENG-036 does not edit `docs/design/test-ui/**` or its browser-test
   files.

## Expected files

| Action | Path | Responsibility |
| --- | --- | --- |
| Add | `docs/design/assets/report-renderer/templates/damage-diagram.svg` | Single owner of zone geometry and marker anchors; no visible labels or business policy. |
| Modify | `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` | Embed that SVG for the report renderer. |
| Modify | `src/Pegasus.Web/Pegasus.Web.csproj` | Link the same SVG into `wwwroot` as a static asset; do not copy it. |
| Add | `src/Pegasus.Web/wwwroot/js/damage-diagram.js` | One direct-event component initializer. |
| Modify | `src/Pegasus.Web/Pages/Cases/Shared/_CaseDamage.cshtml` | Replace ENG-034's shell after it merges. |
| Modify, after lock hand-off | `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Add only required D39 web labels; no type labels. |
| Modify, after lock hand-off | `src/Pegasus.Web/wwwroot/css/site.css` | Component layout and the 1180px/760px reflow. |
| Modify, only after renderer hand-off | `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs` | Convert the embedded shared SVG into marked report HTML. |
| Modify, only after renderer hand-off | `docs/design/assets/report-renderer/templates/assessment_report.scriban` | Place the renderer-provided diagram in the report. |
| Modify, only after renderer hand-off | `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs` | Prove the actual PDF contains the marked diagram. |

Do not modify Core, persistence, migrations, Case handler files, `site.js`
before its transfer, report-image paths, Test UI artefacts, or any path listed
as another ticket's exclusion (see `files/files.md`'s "Files ENG-036 must not
touch").

## Ordered steps

### Step 1 — Confirm merged contracts and acquire only transferred locks

- Files: none.
- Reuses: the Kanmer live gate report, ENG-035's `AssessmentVocabulary`,
  ENG-034's Damage partial contract, CASE-038's lazy-section lifecycle, and
  ENG-029's single `ISaveAssessment` handler.
- Change: verify all six hand-offs above against merged `origin/dev`; record
  the renderer ownership decision before changing any file.
- Preserved behaviour: one Case edit mode, one lease, and one assessment
  writer.
- Forbidden: a `type` member, direct writes to derived impact fields, a
  second handler, an extra stylesheet, a new package, or a migration.
- Done when: every dependency is present and all files in subsequent steps
  are owned by ENG-036.
- Deviation stop: missing vocabulary, writer support, a lock hand-off, or
  renderer allocation stops the ticket for coordination.

### Step 2 — Add one shared, publishable vehicle SVG

- Files: `docs/design/assets/report-renderer/templates/damage-diagram.svg`,
  `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj`,
  `src/Pegasus.Web/Pegasus.Web.csproj`.
- Reuses: existing report-template embedded resources and the SDK content-item
  convention.
- Change: create a static top-down SVG whose `data-zone` identifiers match
  ENG-035's canonical codes, including its wheel codes. It owns paths,
  marker anchors, and fixed presentation attributes only; it contains no
  zone/severity labels, no user values, no code validation list, and no
  executable content. Embed it in Infrastructure and link that same source
  into Web static assets.
- Preserved behaviour: report assets remain embedded; browser and renderer use
  exactly one geometry file.
- Done when: a build can locate the embedded resource and serve the linked
  static asset.

### Step 3 — Implement the accessible diagram component

- Files: `src/Pegasus.Web/wwwroot/js/damage-diagram.js`.
- Reuses: targeted `data-*` hooks and `addEventListener`; no general action
  dispatcher exists to extend.
- Change: expose one narrow initializer for CASE-038's loader. It fetches and
  clones the static SVG, receives server-rendered labels/configuration, and
  renders markers from the Core-normalized records. Click, Enter, and Space
  toggle a unique zone; adding a zone creates an Impacts row with canonical
  `moderate` severity and an empty note, while removal deletes that entry.
  It serializes only the `damage.impacts` value for the existing Case form.
- Preserved behaviour: all label text and ARIA names come from Razor-provided
  `OperatorLabels` values; JSON validation, canonicalization, and derivation
  remain in Core.
- Forbidden: hard-coded labels, a damage-type control, duplicate Core codes,
  inline scripts/styles, a new edit mode, or silently discarding a malformed
  server value.
- Negative cases: unknown zones are not rendered; read-only diagrams expose
  markers but no focusable/toggleable zone; Enter and Space do not scroll the
  page.
- Done when: the component can initialize each lazy Damage fragment exactly
  once and maintains the posted impacts value.

### Step 4 — Compose the Damage section into the existing Case form

- Files: `src/Pegasus.Web/Pages/Cases/Shared/_CaseDamage.cshtml`,
  `src/Pegasus.Web/Presentation/OperatorLabels.cs`,
  `src/Pegasus.Web/wwwroot/css/site.css`.
- Reuses: the merged `DetailsModel`, ENG-029's
  `OnPostSaveAssessmentAsync`/`ISaveAssessment` route, Case partial form
  conventions, `CaseMutationPageModel` lease fields, `data-edit-save`, and
  existing panel/form responsive rules.
- Change: replace the shell with the diagram, impact rows, four tyre/belt
  cards, spare tyre, centre belt, unrelated-damage deduction, and
  material-transfer fields. Bind only ENG-035 vocabulary paths. Show
  Core-derived impact location and severity as values, never editable fields.
  Render the form only for an active editable Case lease; otherwise render
  recorded values and a non-interactive marked diagram.
- Change: add only the web labels needed for headings, zones, severity,
  tyres, belts, derived values, and "Not recorded". Add the 1180px one-column
  and 760px single-column rules using the existing stylesheet.
- Preserved behaviour: Complete remains viewable and read-only; save posts
  only the Case's existing expected-version, operation-key, lease, and D39
  values to the one writer.
- Forbidden: explanatory or empty-state copy, a disabled inert edit surface,
  type labels/inputs/columns, CSS in Razor, calculations in Razor/JavaScript,
  or a crop/image feature.
- Done when: all D39 fields render through the one Case section and the
  component's hidden field reaches ENG-029's writer.

### Step 5 — Render the same marked geometry in the report

- Preconditions: the explicit ENG-035 report-file hand-off is recorded.
- Files: `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`,
  `docs/design/assets/report-renderer/templates/assessment_report.scriban`,
  `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs`.
- Reuses: `ResourceText`, `Encode`, the existing Scriban context, embedded
  template loading, Playwright `SetContentAsync`/`PdfAsync`, and PdfPig test
  extraction.
- Change: read the embedded shared SVG, apply markers only for the
  Core-projected impact zones, and provide the controlled SVG HTML to the
  existing report template. The template prints the marked diagram and the
  existing Core-presented impact facts; it has Zone, Severity, and Note only.
- Preserved behaviour: user notes remain HTML-encoded, Chromium prints with
  backgrounds, and no template performs policy/calculation.
- Negative cases: unmarked records do not fabricate markers; a wheel uses its
  canonical wheel-zone geometry; no type wording appears in the HTML or PDF.
- Tests: extend the production-composition renderer test with selected
  body/wheel zones and assert the generated PDF contains the diagram section,
  selected zone/severity/note evidence, and its marker rendering.
- Done when: the PDF is derived from the exact SVG asset the browser uses.

### Step 6 — Delegate visual proof, run rails, and hand off

- Files: no additional ENG-036 files.
- Reuses: UIIMP-014's `BrowserTestSupport`, seeded Case walk, three-width
  layout checks, and snapshot tooling.
- Change: give UIIMP-014 the exact D39 assertions: click and Enter create
  rows, severity/note save through the Case writer, read-only has no toggle,
  and the layout has no overflow at 1580, 1100, and 760. If snapshot commands
  change its owned artefacts, stop and hand those diffs to UIIMP-014.
- Acceptance: no test is weakened; the renderer test proves the report path;
  UIIMP-014 provides the interaction/snapshot proof.
- Migration: none is in scope. If one becomes necessary, stop and assign it
  to the serialized migration owner; only then run
  `./scripts/Test-MigrationGrants.ps1` with its grants in the same diff.
- Stop condition: all owned tests and hand-off evidence pass, the
  post-implementation report is written, the PR targeting `dev` is open, and
  ENG-036 is in Review. Do not merge, write proof, or begin another ticket.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "FullyQualifiedName~AssessmentReportRendererTests"
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

`./scripts/Test-MigrationGrants.ps1` is not run unless a migration enters
scope; that is a stop-and-reassign condition.
