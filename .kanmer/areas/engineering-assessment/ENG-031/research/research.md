# Research — ENG-031 (2026-09-02, gpt-5.6-terra xhigh, wrapper-checked)

## Wrapper check

Produced by Codex `gpt-5.6-terra` (effort xhigh) in the read-only detached
checkout `.worktrees/research` at `origin/dev` 897db953 (advanced from
cad00be9 before the run; the checkout was clean afterwards). The Claude
wrapper re-ran these checks in the repository and confirmed each:

- `EvaHandoffPolicy.SelectEligibleImages` (`src/Pegasus.Core/Eva/EvaBundleSchema.cs:114`)
  excludes `ThirdPartyVehicleConfirmedAtUtc` images; the string `ThirdParty`
  does not appear in
  `src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs`
  (0 matches) — the report projection does not apply that exclusion today.
- The projection source admits `image/jpeg`, `image/png`, `image/webp` and
  orders by `occurrence.Ordinal` (lines 23, 46).
- `src/Pegasus.Core/Workflow/CaseEditAuthority.cs` and
  `src/Pegasus.Infrastructure/Persistence/CaseMutationGuard.cs` exist;
  `CaseReportApprovalEntity` and `ReportApprovalEvidence` exist at the
  named paths; SkiaSharp 3.116.1 and Microsoft.Playwright are in
  `Pegasus.Infrastructure.csproj`; `docs/design/README.md` lines 817 and
  1022 name `report-image` / `cropper` while `wwwroot/css/site.css` has 0
  matches; every "change" path in the Files document exists on `origin/dev`.
- No `reflect*` identifier exists in Core or Web pages, while
  `docs/frd/frd-06-vehicle-and-engineering-evidence.md:129` states that
  report-image selection "continues to exclude images showing a person's
  reflection" — a documented rule with no implementation (see Operator
  questions).
- Wrapper addition: the `<remarks>` on
  `src/Pegasus.Core/Reports/AssessmentReportProjection.cs` cites a "Report
  images" section of `Pages/Cases/Assessment/Index.cshtml` that no longer
  exists (0 matches); the comment is stale and should be rewritten when the
  projection changes.

## Scope and evidence

**VERIFIED — `git rev-parse HEAD; git rev-parse origin/dev`** The detached
checkout equals `origin/dev`; `git status --short; git diff --check` produced
no changes. No files were edited and no build or test command was run.

**ASSUMED — supplied ENG-031 and EPIC-012 context** The feature owns
non-destructive, case-scoped report-image curation: distinct Close-up then
Overview, ordered Supporting images, normalized crop and quarter-turn rotation,
lease/version protection, and an immutable issued-report snapshot.

## Current behaviour

**VERIFIED — `rg -n -C 4 'ReportImageEvidence|Photos|Project' \
src/Pegasus.Core/Reports`** `AssessmentReportProjectionInput.Photos` becomes
`AssessmentReportSnapshot.Photos`. The current Core projection explicitly says
UI-15 curation is deferred and offers every confirmed image in occurrence
order.

**VERIFIED — `Get-Content -Raw \
src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs`**
The adapter selects Case `DocumentOccurrence` rows that are current, not
logically removed, custody-confirmed, `Image` semantic-role files of
`image/jpeg`, `image/png`, or `image/webp`, ordered by occurrence ordinal. It
reads their original content through `IDocumentContentStore.ReadVersionsAsync`.

**VERIFIED — `rg -n -C 4 'ReportImageEvidence|Photos' \
src/Pegasus.Core/Reports/AssessmentReportRendering.cs \
src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`**
`ReportImageEvidence` carries original bytes and their SHA-256, and validates
the hash. The renderer embeds every image unchanged as a base64 `<img>`; it
has no role, order, crop, or rotation model.

**VERIFIED — `rg -n -i -C 3 'ReportVersion|ImageCuration|Curation' \
src/Pegasus.Core src/Pegasus.Infrastructure src/Pegasus.Web tests`** No
report-image curation or report-version aggregate exists. `AssessmentReportDraft`
contains only transient rendered artifacts.

**VERIFIED — `rg -n -C 4 'CaseReportApprovalEntity|ReportApprovalEvidence' \
src/Pegasus.Core/Workflow src/Pegasus.Infrastructure/Persistence`** A
`CaseReportApprovals` row records an approved immutable artifact identity and
hash, but no report-image selection or crop snapshot. Approval also explicitly
does not claim that a report was sent.

**VERIFIED — `rg -n -C 3 'ThirdPartyVehicleConfirmedAtUtc|\
SelectEligibleImages' src/Pegasus.Core src/Pegasus.Infrastructure`**
`EvaHandoffPolicy.SelectEligibleImages` excludes confirmed third-party vehicle
images. The report projection query does not select that marker, so it
currently does not apply that exclusion.

**VERIFIED — `rg -n -i 'reflection|reflected' src/Pegasus.Core \
src/Pegasus.Infrastructure src/Pegasus.Web tests`** There is no persisted
reflection classification or report-selection exclusion in the current code.

**VERIFIED — `rg -n -C 4 'GenerateReportDraft|PreviewReportDraft|\
ReportDraftPreparation' src/Pegasus.Web/Pages/Cases/Assessment`** The current
`/Cases/{id}/Assessment` page renders Generate and Preview report-draft
controls. Its readiness uses `AssessmentReportProjection.Prepare`, which has
no image-curation requirement.

**VERIFIED — `git log --oneline -20 -- \
src/Pegasus.Web/Pages/Cases/Details.cshtml \
src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml; rg -n '@page' \
src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml`** The D29/D30
documentation commit is present, but the sections move is not: Assessment is
still a routable page, not a 301, and Details still uses the earlier section
frame.

**VERIFIED — `Get-Content -Raw \
src/Pegasus.Web/Pages/Cases/Shared/_CaseFiles.cshtml; \
Get-Content -Raw src/Pegasus.Web/Pages/Shared/_EvidenceViewer.cshtml`** The
CASE-006/DOCS-011 viewer convention is reusable: Case Files supplies
`data-evidence-item`/`data-evidence-set` entries and the shared viewer opens
the authorized document-download route. The existing Rotate view control is a
view-only CSS transform.

**VERIFIED — `rg -n -C 4 'CaseEditAuthority|CaseMutationGuard|\
ExpectedVersion|EditLease' src/Pegasus.Core src/Pegasus.Infrastructure`**
`CaseEditAuthority` owns stale-version and lease refusals. Infrastructure
calls it through `CaseMutationGuard`, increments the Case version, clears the
lease, and records mutation history.

**VERIFIED — `Get-Content -Raw \
src/Pegasus.Web/Presentation/OperatorLabels.cs; \
Get-Content -Raw src/Pegasus.Web/wwwroot/js/site.js`** Operator labels live in
`OperatorLabels.cs`. Page JavaScript uses self-registering, defensive IIFEs;
`_Layout.cshtml` loads versioned external scripts and exposes a `Scripts`
section. No current cropper module exists.

**VERIFIED — `rg -n -i 'cropper|report-image' \
src/Pegasus.Web/wwwroot/css/site.css docs/design/README.md`** The design
authority already defines `report-image` and `cropper`, but the stylesheet has
no implementation for either class.

**VERIFIED — `rg -n -i 'SkiaSharp|Microsoft.Playwright' \
src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj`** SkiaSharp and
Playwright are already approved dependencies. ENG-031 needs no new package;
SkiaSharp can produce an in-memory rendition while the persisted source hash
continues to describe source bytes.

## Mockup findings

**VERIFIED — `Get-Content -Raw 24-cropper.js` from the supplied mockup
`src` directory** The crop dialog starts with full-frame `{ x: 0, y: 0,
w: 1, h: 1 }`, stores crop fractions against the rotated source, and stores
rotation in quarter turns.

**VERIFIED — `Get-Content -Raw 24-cropper.js`** The tool has pointer
move/resize with clamping, eight resize handles, keyboard arrow movement,
Shift+arrow resize, `R` rotation, Free/4:3/3:2/1:1 aspect choices, live canvas
preview, Reset to saved preparation, and Full frame.

**VERIFIED — `rg -n -C 8 'report-images|report-image-role|\
report-image-move|dragstart' 22-case-engineer.js` from the supplied mockup
`src` directory** The Report grid presents Close-up, Overview, Supporting, and
Not used; it supports Supporting ordering by move buttons and drag-and-drop.
The prototype demotes a duplicate primary role to Supporting, so the server
must still independently reject invalid submitted state.

**VERIFIED — `rg -n -C 6 'report:|images:|crop:|rotation:' \
04-fixtures.js` from the supplied mockup `src` directory** Fixtures model a
per-document `{ docId, role, order, crop, rotation }` record and use Close-up
at one, Overview at two, Supporting after them, and Not used outside the
report.

**VERIFIED — `rg -n -C 5 'Report-image preparation' \
Pegasus_UI_v2_notes.md` from the supplied mockup root** The supplied notes
identify ENG-031 as the backend gap and require an issued report to snapshot
its curation.

## Gap list

| Gap | Evidence |
| --- | --- |
| No Core-owned curation policy, record, command, or port exists. | **VERIFIED** by `rg -n -i 'ReportImage|ReportVersion|ImageCuration|Curation' src/Pegasus.Core src/Pegasus.Infrastructure src/Pegasus.Web tests`. |
| Report readiness does not require distinct Close-up and Overview images. | **VERIFIED** by `rg -n -C 4 'Prepare|ReportDraftPreparation' src/Pegasus.Core/Reports src/Pegasus.Web/Pages/Cases/Assessment`. |
| The renderer outputs original bytes without crop or rotation. | **VERIFIED** by `rg -n -C 4 'private static string Photos' src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`. |
| Report selection includes all current confirmed image-role files in occurrence order. | **VERIFIED** by `Get-Content -Raw src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs`. |
| Report selection does not currently exclude confirmed third-party images. | **VERIFIED** by comparing `EfAssessmentReportProjectionSource.cs` with `EvaHandoffPolicy.SelectEligibleImages`. |
| No reflection disposition exists to preserve. | **VERIFIED** by `rg -n -i 'reflection|reflected' src/Pegasus.Core src/Pegasus.Infrastructure src/Pegasus.Web tests`. |
| No durable generated/issued report-image snapshot exists. | **VERIFIED** by `rg -n -i 'ReportVersion|ImageCuration|Curation' src/Pegasus.Core src/Pegasus.Infrastructure src/Pegasus.Web tests`. |
| The intended Report section has no current host because the sections move is pending. | **VERIFIED** by `git log --oneline --all --grep='section' -i` and the still-live Assessment route. |

## Reuse

**VERIFIED — `Get-Content -Raw \
src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs`**
Reuse `IAssessmentReportProjectionSource`, `IGetAssessmentWorkspace`,
`IDocumentContentStore`, `ManagedDocumentContentRead`, and
`ReportImageEvidence` rather than creating a second custody-content route.

**VERIFIED — `rg -n -C 4 'SelectEligibleImages' \
src/Pegasus.Core/Eva/EvaBundleSchema.cs`** Reuse the eligibility shape from
`EvaHandoffPolicy.SelectEligibleImages`: current, confirmed, image-role,
unremoved, and not third-party. Do not copy the list into Web; the
report-specific policy belongs in Core because its role/order rules differ.

**VERIFIED — `Get-Content -Raw \
src/Pegasus.Core/Workflow/CaseEditAuthority.cs; Get-Content -Raw \
src/Pegasus.Infrastructure/Persistence/CaseMutationGuard.cs`** Reuse
`CaseEditAuthority` and `CaseMutationGuard` for expected-version, lease,
authorization, and stale-write failure.

**VERIFIED — `rg -n -C 4 'CaseRepairSpecifications|Version' \
src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs`**
Reuse the immutable/versioned aggregate convention from repair
specifications: a versioned header plus ordered child rows, actor/time,
operation-key replay, constraints, and a current-version query.

**VERIFIED — `Get-Content -Raw \
src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`**
Extend `PlaywrightAssessmentReportRenderer.Photos` and its existing embedded
report CSS rather than introducing a second report renderer.

**VERIFIED — `Get-Content -Raw \
src/Pegasus.Web/Pages/Shared/_EvidenceViewer.cshtml; Get-Content -Raw \
src/Pegasus.Web/wwwroot/js/site.js`** Reuse the authorized evidence viewer,
external-script registration pattern, and keyboard/focus conventions.

**VERIFIED — `Get-Content -Raw scripts/Test-MigrationGrants.ps1; \
Get-Content scripts/Invoke-AzureDatabaseBootstrap.ps1`** Reuse the migration
grant convention: every created table is granted or explicitly exempted, and
the production bootstrap permission census mirrors grant-carrying migrations.

## Risks

**VERIFIED — `Get-Content -Raw \
src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs`**
A curation row must identify the exact document occurrence, version, and
source hash; querying only the then-current document version would let a later
replacement alter a prior report's rendition.

**VERIFIED — `Get-Content -Raw \
src/Pegasus.Core/Workflow/CaseEditAuthority.cs`** A browser-only conflict
check is insufficient: direct or stale submissions must fail through the
existing Case expected-version and lease guard.

**VERIFIED — `rg -n -C 4 'CaseReportApprovals|ReportApprovalEvidence' \
src/Pegasus.Core/Workflow src/Pegasus.Infrastructure/Persistence`** A report
approval currently identifies only an artifact. Snapshot linkage must be added
at the report issue boundary or the issued-report requirement remains
unproven.

**VERIFIED — `rg -n -i 'cropper|report-image' \
src/Pegasus.Web/wwwroot/css/site.css`** The partial cannot safely invent
inline styles: the application documents a production CSP restriction, and
the stylesheet is a serialized EPIC-012 shared-lock path.

**ASSUMED — supplied EPIC-012 context** The mockup's instructional sentence
about order must not ship. The product may retain labels, values, controls,
and a named readiness blocker, but not explanatory panel copy.

Wrapper addition: the change set touches four capacity-one shared-lock
paths (`Pages/Cases/Shared/*`, `Presentation/OperatorLabels.cs`,
`Persistence/Migrations/**`, and — for the `report-image`/`cropper` classes —
`wwwroot/css/site.css`); the plan must schedule those edits against the
EPIC-012 lock order and take the CSS through the lane that holds `site.css`
or a hand-off recorded on this ticket.

## Operator questions

- [ ] Which durable event defines the report version that snapshots curation:
  report-draft generation, report approval, detected sent evidence, or a
  specified combination? The current approval record and sent-evidence record
  are separate.

- [ ] What operator-controlled, durable disposition identifies an image with a
  person's reflection? No such classification exists, yet the governing
  requirement (`docs/frd/frd-06-vehicle-and-engineering-evidence.md:129`)
  says it remains excluded from report selection.

## Current v1 reconciliation — 2026-09-08

Read-only source pin: accepted dev
`aefe4c32d078ad79c0368666b5666032e6865248`. Historical September 2
research above remains evidence of that earlier tree, not current edit
authority. No source, claim, branch, build, test, capture or live write in
this preparation. Declared research sources: none (get_sources).

Current intent is EPIC-012 D46, FRD-06 report-image preparation, FRD-12
Assessment and design README:991–992, plus Astra B-casework B06:495–521.
D46 requires visual frame/handles/rotation/aspect/reset/preview and both
Files-viewer and Report-card entry without first pressing Edit Case.
Astra and the current design require one global Save/Discard covering Files
preparation. Root explicitly retained this requirement on 8 September.

### What is already integrated

- Core Documents/CaseAssetPreparation.cs:34–197 owns quarter rotations,
  normalized rotated-source crops (7 decimal places), occurrence identity,
  source version/hash, role/order, per-preparation version, and queries.
  CaseAssetPreparationPolicy:242–385 owns validation, unique primaries and
  ordered report projection. Do not create the historical plan's second
  ReportImageCuration vocabulary/store/schema.
- EfCaseAssetPreparationStore:267–391 already has transaction-compatible
  PrepareSaveAsync. It merges edits, checks per-image versions and sources,
  invokes Core once, and writes existing DocumentOccurrence columns with
  actor/time. It neither commits nor increments the Case version.
- EfCaseWorkspaceStore:30–216 owns serializable global save, CaseOperationReplay,
  version/lease/archive guards, one CaseMutationGuard.Complete, one three-part
  CaseMutationHistory and atomic MarkStaleAsync. Its request/hash/history
  presently omit image preparation.
- Generation freeze and renderer already retain and consume exact prepared
  image/source identities. CaseReportGenerationPersistenceTests:282–321 proves
  frozen identity/hash custody. No new approval entity or renderer is needed.
- Existing ReportImagePreparationView, _CaseReportImagePreparation and
  case-workspace.js implement numeric crop, quarter rotation, role and
  Supporting move/drag. Both sections read the same occurrence records.
  Report is already eagerly rendered (Details:563–567); Files may lazy-mount.
  The shared EvidenceViewer/site.js owns paging, focus, Escape, downloads,
  original image/PDF preview and mount binders. Raw viewing already requires
  no edit lease and must remain that way.

### Actual gaps and dispositions

1. Visual controls are absent: current cards contain only metadata/numeric
   fields; no frame, handles, aspect lock or live preview. Supplied prototype
   24-cropper.js has the required geometry/interaction, but its global S
   state, hardcoded fixture data, inline styles, autonomous history writes
   and auto-committing Save must not be copied.
2. Immediate image Save/Reset in Details:1532–1646 call a separate committing
   store; Save clears the lease (1580), and EfCaseAssetPreparationStore:107
   completes the Case mutation before global Save. SaveCaseWorkspaceRequest
   has no image member. Root disposition: fold existing preparation edits
   into the existing global transaction; no undo/draft database or secret
   immediate commit. Crop Apply stages page fields; global Save persists.
3. Production caller census for ICaseAssetPreparationStore/Save/Reset is
   Details plus DI only. Remaining references are the existing image
   persistence/Web tests. AssessmentPersistenceIntegrationTests:87 constructs
   the class for its query interface, not mutation. Remove superseded
   immediate public commands/handlers/registration; preserve the query class
   and internal transaction helper, updating that constructor caller.
4. Helper preconditions must be explicit on integration: every edited
   occurrence must have its exact current, confirmed, nonremoved source.
   The current helper silently omits missing current sources, and Core
   intentionally skips freshness for untouched rows absent from the map.
   Enforce complete edited-source membership before calling Core; retain
   historical immutable metadata readers unchanged. Current global save
   accepts general staff/Automation casework, whereas the actual image Web
   command requires Engineer/editable assessment state. Enforce that image-
   payload authority in Core and persisted transaction, not only Web.
5. All image edits, including role/order/Reset, must stage the same existing
   CaseAssetPreparationEdit values. Reset stages NotUsed/no order/no rotation/
   Full crop. No SQL/Blob write occurs until global Save; Discard only releases
   the existing lease/reloads persisted state. Frozen reports stay immutable.
6. D46 lazy acquisition uses existing ClaimLease; its current helper is PRG
   only. Proposed narrow native enhancement: submit crop fields through this
   existing handler, acquire unchanged authority, then render the same page
   with those values staged, without modifying original expected versions.
   This proposal needs root review before execution; no new JSON API, modal
   loader, session draft store or storage-backed undo.
7. Files and Report must not submit duplicate indexed image payloads. Report
   already renders eagerly and can own the single form-associated editable
   value set; Files/viewer and Report-card controls target it. Shared source
   metadata does not create a second mutable preparation record.

### Superseded historical premises, not erased decisions

Q1's former report-approval snapshot mechanism is superseded by the accepted
generation freeze implemented under CASE-047/DOCS-020/ENG-041; the intended
immutable issued source snapshot is retained. Q2's Not used disposition stays
resolved; no reflection marker is introduced. D46 remains binding.
DELIV-040 is Done. ENG-034 remains Verifying historically, but the native
host and moved handlers are integrated through PR674/CASE-047; do not
force-release that historic claim or pretend it is a missing backend.
TICK-095 is archived as an empty superseded umbrella and explicitly names
ENG-031 as the precise image owner.

### Current ownership and proportional proof

ENG-029 (pack confirmed) owns Details.cshtml.cs, CaseMutationPageModel,
_CaseReport, CaseWorkspaceLabels, FRD06/11 and design README, plus its
workspace/assessment fixtures. It does not own case-workspace.js/css,
site.js/css or shared viewer. ENG-031 must start only after exact release/
handoff. INTK-064 owns DI; root serializes generated Case snapshots/index.
No worktree is taken by this preparation.

Reuse CaseAssetPreparation core/persistence/Web assertions, workspace
transaction tests and existing BrowserTestSupport. Add one focused crop
browser fixture, using actual supplied immutable images through existing
authorized content routes. Cover Apply/Discard/Save, same-record both
entrypoints, frame/handles/keyboard/aspect/quarter rotations/reset, read-only
view, lazy mount, stale authority/source and atomic mixed edits. Root alone
schedules focused runtime and scoped case-details capture; no broad repeated
builds or new test infrastructure.
