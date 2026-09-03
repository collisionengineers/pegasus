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
