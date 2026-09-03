# Research — CASE-038 (2026-09-02, gpt-5.6-terra xhigh, wrapper-checked)

## Wrapper check (Claude, 2026-09-02)

Codex ran read-only in the shared detached checkout `.worktrees/research`
at `origin/dev` = `cad00be9`; `git status --porcelain` was empty afterwards.
The wrapper did every board read (ticket body, EPIC-012 / EPIC-011 context,
waves, sibling lanes ENG-034, CASE-039/040/041/029/042, UIIMP-014, DELIV-041);
Codex's "Kanmer tunnel rate-limited" note below is therefore harmless — the
supplied context was read from the board by the wrapper. Spot-checked
against the main checkout with my own commands, all confirmed:

- `Details.cshtml.cs` lines 65–76: `SectionFilter` normalises exactly
  `vehicle`, `valuations`, `inspection-address`, `case-files`, `notes`, else
  `overview`; `OnGetAsync` (line 192) loads `ImagesByIntake` only when
  `Section == "case-files"`; `CanOpenAssessment` (line 165/210) drives the
  "Open Assessment" link at `Details.cshtml` lines 274–281.
- `_CaseWorkspaceNav.cshtml`: six `asp-route-section` links, class
  `case-section-nav`, `aria-current="page"` for `Model.Section`.
- `site.css` (minified frame block): `.edit-bar{position:sticky;top:51px}`
  line 390, `.case-section-nav{position:sticky;top:61px}` line 399,
  `.case-context{position:sticky;top:61px}` line 405; `@media(max-width:
  1360px|1180px|1100px|980px|900px|760px)` at lines 752–800.
- `rg "PartialViewResult|return Partial|ViewComponent" src/Pegasus.Web`
  returns nothing — no Razor-fragment-over-HTTP pattern exists yet.
- `Details.cshtml.cs` lines 466–481: Engineer name resolved through
  `IStaffAccountQueries.GetAsync(AssignedEngineerId)`; `EngineerOptions`
  built only when `workflow.State == Review`.
- The current ribbon (`Details.cshtml` lines 104–124) has Case/PO,
  Registration, Claimant, Principal, State — no Engineer, no Sign-off.
- `tests/.../Browser/LayoutIntegrityTests.cs` runs
  `AccessibilityTests.AuthenticatedRouteList` × {1580, 1100, 760}; that list
  holds `/Cases`, `/Cases?tab=…` but no `/Cases/{id}` record — a seeded Case
  route must be added.
- `tests/.../CaseDetailsWebTests.cs` line 49 asserts "Open Assessment" in the
  action bar, lines 155–161 assert one current section per `?section=`, line
  1280 locates `class="case-section-nav"`.
- `docs/design/test-ui/catalogue.json` lines 322–346: Details is `visual`
  with states `default`, `unavailable`, `conflict`.
- `CASE_SECTIONS` order lives in mockup `03-labels.js` line 110 (the brief
  said `05-state.js`; Codex found the right file).

Two wrapper corrections and one addition to the Codex text:

1. **Snapshots ride in CASE-038's own PR.** Codex hands
   `docs/design/test-ui/**` to UIIMP-014 and says CASE-038 "cannot honestly
   pass the routed-page snapshot gate". Repository rule (AGENTS.md,
   Commands): after changing a routed Razor page, regenerate the snapshots
   with `./scripts/Update-TestUiSnapshots.ps1`, prove them with `-Verify`
   and `./scripts/Test-UiCatalogue.ps1`, and commit `docs/design/test-ui/`
   with the page change — CI runs the same verify on every change set. So
   the regenerated `pages/case-details--default.html` and
   `pages/case-details--conflict.html` (and the `catalogue.json` branch text
   for those two states if it changes) are a capacity-one lease taken by
   CASE-038, exactly as ENG-034's wrapper correction did for the Assessment
   row. UIIMP-014 keeps the *new* per-section edit/read-only states. The
   Files document carries the rows.
2. **The one-form invariant breaks.** `_CaseWorkflow.cshtml` line 161 and
   `_CaseInspectionAddress.cshtml` line 76 both render
   `id="case-edit-form" data-edit-save`, relying on the comment "only one
   section renders an edit form at a time". Rendering Overview and
   Inspection on one page yields duplicate ids; `site.js` line 1474
   (`document.querySelector('[data-edit-save]')`, Ctrl+S) submits only the
   first; the CASE-007 dirty guard (`site.js` lines 568–600) attaches
   `input`/`submit` listeners to the forms present at load, so a lazily
   mounted form is unguarded unless the fragment initialiser rebinds. The
   inspection partial is CASE-041's file — the plan must settle which single
   form the sticky Save targets (or one form spanning sections, per "one
   edit mode over one lease") and report the partial change to CASE-041
   rather than editing it.
3. **`?section=` retains its no-script meaning.** The many callers that
   already use `?section=case-files|vehicle|notes|inspection-address`
   (`CaseCustodyWebTests`, `CaseVehicleWebTests` line 223, `CaseTasksWebTests`,
   `ImageIntakeWebTests`, `ImageViewingWebTests`, `OperatorJourneyTests`
   lines 128/301) keep working only if the addressed section is rendered
   server-side on the first GET, as Codex's "addressed host" assumption
   says; the plan should make that a stated acceptance condition.

All current-state claims below are Codex's, labelled as it labelled them;
the wrapper confirmed the ones listed above.

## Research — evidence boundary

- **VERIFIED** — `git rev-parse HEAD; git log -1 --oneline; git
  status --short` reports clean detached revision
  `cad00be9d42dbeaee9edf34c2d24de222d7ddb9d`, headed by
  `Reduce the Test UI snapshot gate critical path (UIIMP-013) (#644)`.
  No files were edited and no build or test was run.

- **VERIFIED** — `dotnet --list-sdks` reports SDKs `10.0.204` and
  `10.0.303`.

- **ASSUMED** — the supplied CASE-038 text, D29–D43, EPIC-012 lane
  ownership, and D30 supersession are the authoritative task context. The
  live Kanmer tunnel returned a rate-limit response, so its current documents
  could not be independently read.

## Current Case workspace

- **VERIFIED** — `Get-Content
  src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` shows `SectionFilter` binds
  `?section=` and normalizes only `overview`, `vehicle`, `valuations`,
  `inspection-address`, `case-files`, and `notes`; every other value becomes
  `overview`.

- **VERIFIED** — `Get-Content
  src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml` shows a
  six-item side-nav. Each item is an `asp-route-section` link and sets
  `aria-current="page"` only for `Model.Section`.

- **VERIFIED** — `Get-Content
  src/Pegasus.Web/Pages/Cases/Details.cshtml` shows the page renders exactly
  one main body: Notes → `_CaseHistory`, Files → `_CaseFiles`, Vehicle →
  `_CaseVehicle`, Inspection → `_CaseInspectionAddress`, Valuations → an
  empty heading shell, otherwise `_CaseWorkflow` plus `_CaseSummary`.

- **VERIFIED** — the same command shows the existing identity ribbon has
  Case/PO, Registration, Claimant, Principal, and State. It has no Engineer
  or Sign-off value. The context column has State, Version, Due, Engineer,
  and Edit authority.

- **VERIFIED** — `rg -n -i "sign.?off|signatory"
  Details.cshtml Details.cshtml.cs CaseWorkflowContracts.cs
  CaseWorkflowEntities.cs` finds no current Case sign-off field. `AssignedEngineerId`
  is the only stored workflow assignment.

- **VERIFIED** — `rg -n -C 2 "AssignedEngineerId|GetAsync\(engineerId|
  ListAsync\(0, 100" Details.cshtml.cs` shows the page resolves Engineer by
  `workflow.AssignedEngineerId` through `IStaffAccountQueries.GetAsync`.
  It builds EVA `EngineerOptions` from enabled accounts in the Engineer role
  only while the case is in Review.

- **VERIFIED** — `Get-Content Details.cshtml.cs` shows `OnGetAsync` loads
  `IGetCase`, `IGetAssessmentAccess`, image intake summaries, evidence
  images, and extra workspace state. It loads `ImagesByIntake` only when
  `Section == "case-files"`.

- **VERIFIED** — the existing action bar uses `CanOpenAssessment` to render
  an enabled `/Cases/{id}/Assessment` link or a disabled-looking "Open
  Assessment" control. `IGetAssessmentAccess` sets that property.

- **ASSUMED** — D30 requires every section to remain visible regardless of
  the old Assessment-access gate. CASE-038 must therefore remove "Open
  Assessment" from the Case frame and must not use `CanOpenAssessment` for
  section visibility. ENG-034 owns retirement of the Assessment route.

## Lease, edits, and handler surface

- **VERIFIED** — `Get-Content
  src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs` shows one
  cookie-backed `LeaseToken`, restored against the server's active lease by
  `RestoreLeaseState`. Claim, renew, heartbeat, release, and failure handling
  are shared by the Case and Assessment page models.

- **VERIFIED** — `Get-Content Details.cshtml` and
  `Pages/Shared/_EditHeartbeat.cshtml` show every mutating Case form carries
  its antiforgery token, case id, operation key, and, where applicable,
  `editLeaseToken`. `_EditHeartbeat` posts to the page that rendered it; the
  existing Details handler returns 204 while the lease remains valid.

- **VERIFIED** — `Get-Content src/Pegasus.Web/wwwroot/js/site.js` shows
  the dirty-form guard watches every form containing `editLeaseToken`.
  Its heartbeat posts `new FormData(form)`, preserving antiforgery plumbing.
  It stops only on a non-204 response.

- **VERIFIED** — `rg -n "public .*On(Post|Get).*Async"
  Pages/Cases/Assessment/Index.cshtml.cs` finds the Assessment handlers:
  claim/heartbeat/release lease; GET; report draft generation/preview; Send
  to Claude; save/edit-line/duplicate/discard/set-current estimate; and
  estimate import.

- **ASSUMED** — per the ENG-034 contract, CASE-038 owns the destination
  handler host: `DetailsModel` must host the moved Assessment handlers and
  redirect to `/Cases/{id}?section=estimate` while retaining `estimate` or
  `dialog` query state where needed. ENG-034 owns removing those handlers
  from `Assessment/Index.cshtml.cs`; it must not create a second handler path.

## Core, Infrastructure, and migrations

- **VERIFIED** — `Get-Content
  src/Pegasus.Core/Assessment/AssessmentWorkspace.cs` shows
  `IGetAssessmentAccess` and `IGetAssessmentWorkspace` are existing Core
  ports. `AssessmentWorkspace` already carries case data, assessment data,
  draft/accepted repair specifications, and the latest AI request.

- **VERIFIED** — the same command shows `CanOpen` currently requires
  Report preparation/Post report/Complete plus a current Review export;
  `IsReadOnly` is exactly `PostReportComplete`. The latter remains useful for
  the D30 Complete read-only rule; the former is not a visibility condition.

- **VERIFIED** — `rg --files src/Pegasus.Infrastructure/Persistence |
  rg "(Assessment|Estimate|Valuation|Repair)"` finds
  `EfAssessmentAccessSource`, `EfAssessmentWorkspaceSource`,
  `EfCaseAssessmentStore`, `EfRepairSpecificationStore`,
  `EfValuationStore`, and `AssessmentFieldWriter`.

- **VERIFIED** — `rg -n "IGetAssessmentAccess|IGetAssessmentWorkspace"
  src/Pegasus.Infrastructure/DependencyInjection.cs` shows those ports are
  already composed to their EF adapters. CASE-038 needs no new Core port,
  adapter, registration, schema, or migration.

- **VERIFIED** — `rg -n -C 3 "CaseValuations|CASE-029"
  Persistence/Migrations/20260829095336_CaseValuations.cs` says CASE-029 is
  the Web Case-workspace valuation owner. CASE-038 must not change this
  migration or create another one.

## Existing frontend conventions

- **VERIFIED** — `rg -n "PartialViewResult|return\s+Partial\s*\("
  src/Pegasus.Web` finds no existing Razor partial-over-HTTP pattern.
  CASE-038 introduces the first targeted fragment handler; it should share
  Details' authorized case-loading path rather than add a parallel query path.

- **VERIFIED** — `rg -n -C 2 "fetch\(" site.js` finds fetches only for
  enhanced upload/heartbeat POSTs and JSON search/preview responses. No
  existing fetch inserts Razor HTML.

- **VERIFIED** — `Get-Content site.js` shows dialogs and form enhancements
  bind directly to the document's initial markup. A lazy fragment containing
  dialogs or enhanced forms will not acquire those bindings automatically.

- **ASSUMED** — the smallest safe extension is a Case-workspace-specific,
  idempotent fragment initializer or delegated handler. It must replace only a
  named lazy placeholder, never replace `case-main`, the sticky frame, or an
  already-rendered form. That preserves unsaved values and the one lease.

- **VERIFIED** — `Get-Content site.css` shows existing reusable classes:
  `.record`, `.record-ribbon`, `.record-bar`, `.edit-bar`,
  `.case-workspace`, `.case-main`, `.case-context`, and
  `.case-section-nav`. The current side-nav and context use `top: 61px`;
  the edit bar uses `top: 51px`.

- **VERIFIED** — the same stylesheet has the existing reflows at 1360, 1180,
  1100, 980, 900, and 760px. At 1360 the context hides; at 980 the nav becomes
  a horizontal scroller; at 760 the workspace stacks and sticky edit bar is
  static.

## Mockup and required frame

- **VERIFIED** — `rg -n -C 3 "CASE_SECTIONS"
  Pegasus_UI_v2_src/src/03-labels.js` defines this order: Overview, Engineer
  notes, Inspection, Vehicle, Damage, Valuation, Estimate, Settlement, Report,
  Files, Notes.

- **VERIFIED** — `rg -n -C 4 "renderCase|data-lazy|scrollToSection|
  scroll-spy" Pegasus_UI_v2_src/src/20-case.js` shows the mockup renders the
  first three sections, uses `section-{key}` IDs and `data-lazy` placeholders,
  scrolls an addressed section into view, and marks the active jump link while
  scrolling.

- **VERIFIED** — `Get-Content
  Pegasus_UI_v2_src/src/30-record.css` shows `.case-sticky` sits below
  `--utility-h + --tabs-h`; `.case-context` and each `.case-section` include
  the measured `--sticky-h` offset; breakpoints include 1180, 1100, 900, and
  760px.

- **ASSUMED** — D29 removes the mockup's Scroll/Tabs comparison control.
  CASE-038 supplies only the sticky single-scroll frame and jump-nav.

- **ASSUMED** — `?section=estimate` becomes a jump request, not a
  server-selected exclusive panel. The GET must render the first three
  sections plus the addressed section when it is later in the order, then
  client code scrolls to `#section-estimate`. This preserves a meaningful
  no-script URL and avoids an absent anchor.

- **ASSUMED** — the frame reserves a Sign-off ribbon/context value with the
  central label and ordinary no-value rendering only. CASE-040 later provides
  the Case field, eligible-account query, default rule, and EVA dialog value;
  CASE-038 must not manufacture that data.

## Gap list

- **VERIFIED/ASSUMED** — the current six exclusive `?section=` bodies and
  side-nav differ from the supplied eleven-section scrolling D29/D30 frame.

- **VERIFIED/ASSUMED** — current CSS has independently sticky side-nav,
  context, and edit-bar offsets; the target needs one measured sticky stack
  and section `scroll-margin-top`.

- **VERIFIED/ASSUMED** — no Razor-fragment fetch exists; CASE-038 needs a
  bounded `OnGetSection` response and lazy DOM replacement that preserves
  existing forms.

- **VERIFIED/ASSUMED** — the Case page gates "Open Assessment" through
  `CanOpenAssessment`; D30 instead makes all Engineer sections viewable and
  relies only on the Complete read-only rule.

- **VERIFIED/ASSUMED** — `OperatorLabels.CaseWorkspace` holds Vehicle,
  Inspection-address, and Files body labels, but no frame navigation,
  Sign-off, Engineer-notes, Damage, Valuation, Estimate, Settlement, or
  Report labels. CASE-038 owns the frame/ribbon/nav vocabulary; section lanes
  add their body-specific labels through the serialized shared-file lock.

## ENG-034 contract

- **ASSUMED** — CASE-038 can satisfy all five ENG-034 contract points from
  the frame: render all eleven containers in D30 order; pass Case id,
  Case/Assessment projections, estimate selection, actor, lease, operation
  keys, and `IsReadOnly`; host the moved handlers; preserve the single lease
  across lazy loads; and remove the Assessment action/gate.

- **ASSUMED** — former Assessment POSTs land on
  `Pages/Cases/Details.cshtml.cs`, owned by CASE-038. ENG-034 owns the source
  Assessment files, moved Engineer partials, route 301, and retargeted
  Assessment-specific tests. No handler is duplicated.

## Tests, catalogue, and design authority

- **VERIFIED** — `Get-Content
  tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs` shows the
  existing browser theory runs 1580/1100/760 viewports and checks HTTP 200,
  horizontal overflow, clipping, exactly one `main` and `h1`, and no inline
  styles.

- **VERIFIED** — `Get-Content
  tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs` shows that
  theory's route list does not contain a seeded Case record. CASE-038 must add
  a seeded/injected Case-frame scenario at all three widths; the present
  generic theory does not prove the record frame.

- **VERIFIED** — `rg -n "section=|Open Assessment|case-section-nav"
  CaseDetailsWebTests.cs ImageIntakeWebTests.cs ImageViewingWebTests.cs
  Browser/OperatorJourneyTests.cs` finds:
  `CaseDetailsWebTests` asserts one current section and the Assessment action;
  it must be retargeted to anchor/jump semantics and the removed action.
  Image Intake, Image Viewing, and Operator Journey already use
  `?section=case-files`; they can retain that URL if the forced addressed
  section renders server-side.

- **VERIFIED** — `Get-Content scripts/Update-TestUiSnapshots.ps1` shows a
  fresh capture runs browser and non-browser render tests, then generates or
  verifies snapshots. `Test-UiCatalogue.ps1` requires every routed Razor page
  to have one valid catalogue classification and every visual state to have
  one existing prototype.

- **VERIFIED** — `Get-Content docs/design/test-ui/catalogue.json` shows
  Details currently has `default`, `unavailable`, and `conflict` snapshots.
  The default and conflict HTML necessarily change with the frame; unavailable
  is expected to remain byte-identical because Details returns before the
  record markup.

- **ASSUMED (wrapper-corrected, see correction 1 above)** — Codex placed
  the regenerated `case-details--default.html` / `--conflict.html` and the
  matching `catalogue.json` edit with UIIMP-014. The repository rule puts
  them in CASE-038's own PR under the capacity-one `docs/design/test-ui/**`
  lease; UIIMP-014 owns the additional per-section edit/read-only snapshots.

- **VERIFIED** — `Get-Content docs/design/README.md` shows a component
  vocabulary table containing the current Record rows and describes
  `/Cases/{id}` as `?section=` selection with a side-nav. `rg -n -i
  "section.*tab|tab.*section" docs/design/README.md` finds no literal
  "sections as tabs" sentence in this checkout.

- **ASSUMED** — DELIV-041 changes the Case-specific route/workspace wording
  to D29's single scrolling record while preserving generic tab rules for
  other surfaces. CASE-038 adds only the five vocabulary rows:
  `section-nav`, `case-sticky`, `suggest-btn`, `derived`, and
  `outcome-option`, after DELIV-041 clears the capacity-one governing-doc
  lock.

## Risks

- **VERIFIED/ASSUMED** — fragment replacement can detach unsaved inputs or
  leave unbound dialogs/actions. Replace placeholders only and make fragment
  enhancement idempotent.

- **VERIFIED (wrapper)** — two partials render `id="case-edit-form"
  data-edit-save` and the Ctrl+S / dirty-guard script assumes one such form
  in the document (see correction 2 above).

- **VERIFIED/ASSUMED** — Details currently conditionally loads Files images.
  The fragment handler must load only the projection needed by the requested
  section, with the same authorization, case id, and lease restoration as the
  full GET.

- **VERIFIED/ASSUMED** — `site.css`, `site.js`, `OperatorLabels.cs`,
  `docs/design/README.md`, and Test UI files are shared-lock paths. Their
  work must be serialized with DELIV-041 and UIIMP-014.

- **ASSUMED** — no corpus-derived mockup fixture values may be copied into
  source or tests before the D43 sign-off path. Reuse existing documented test
  data only.

## Open questions for the operator

none
