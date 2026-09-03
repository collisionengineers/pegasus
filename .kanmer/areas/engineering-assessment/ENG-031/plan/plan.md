# Plan — ENG-031 (2026-09-02, gpt-5.6-terra xhigh)

## Wrapper check

Produced by Codex `gpt-5.6-terra` (effort xhigh) in the read-only detached
checkout `.worktrees/research` at `origin/dev` 897db953; the checkout was
clean before and after the run. Codex's environment carried a Kanmer MCP and
it made read-only board calls (`get_status`, `get_item`, `get_links`,
`get_doc_gates`, `get_ticket_doc`); the activity log shows no board write
from the run. The Claude wrapper re-checked the plan's premises in the
repository and corrects or sharpens the following:

- `OperatorLabels.CaseStage` is a method, `CaseStage(string?)`, not a nested
  class; the design rule below reads accordingly.
- The only report-approval caller is
  `Pages/Cases/Closure.cshtml.cs` `OnPostRecordReportApprovalAsync` at
  `/Cases/{id}/Closure?handler=RecordReportApproval` (covered by
  `tests/Pegasus.IntegrationTests/CaseReportApprovalWebTests.cs`); the
  Closure page is a two-line handler-only page whose route is reached from
  `Details.cshtml` for Close/Reopen. `Core/Lifecycle/CaseLifecycle.cs` is
  claimed by CASE-040 this wave; `Closure.cshtml.cs` is claimed by no active
  EPIC-012 lane (CASE-033, backlog, concerns the bodyless page). Dependency 5
  is therefore: with approval confirmed as the snapshot boundary,
  the `ReportApprovalSubmission` extension in `CaseWorkflowContracts.cs` needs
  a matching change in those two files — sequence behind CASE-040 for
  `CaseLifecycle.cs`, and record `Closure.cshtml.cs` as an ENG-031 change in
  the Files document at take time. No Razor control posts to that handler
  today (0 matches in `src/Pegasus.Web` `.cshtml`); the routed handler and its
  integration test are the wiring evidence, and an operator control for it is
  outside ENG-031's owned paths.
- FRD-11 (lines 108–118) confirms the premise behind Q1: draft generation
  "saves nothing"; approval and issue are separate human acts.
- Q1 is resolved (2026-09-03, controller): report approval snapshots the
  curation. Nothing in this plan is conditional any more; the approval-linkage
  rows in Steps 2–3 and Dependency 5 are unconditional work.
- `docs/design/README.md` line 840 is the source of the production CSP
  no-inline-style rule the plan cites.

The Codex plan follows with the `CaseStage` wording corrected and the
2026-09-03 plan-review fixes applied (see the review section at the end).

## Premise checks

| Status | Read-only command | Confirmed or corrected |
| --- | --- | --- |
| VERIFIED | `Get-Content -Raw CLAUDE.md; Get-Content -Raw AGENTS.md` | Kanmer workflow, one Core policy owner, shared locks, and evidence rules bind. |
| VERIFIED | `git rev-parse HEAD; git rev-parse origin/dev; git status --short; git diff --check` | Checkout is clean at `897db953`, equal to `origin/dev`. |
| VERIFIED | `mcp__kanmer__get_status; mcp__kanmer__get_item ENG-031; mcp__kanmer__get_links ENG-031; mcp__kanmer__get_doc_gates ENG-031` | ENG-031 is Preparing, is blocked by ENG-034, and needs plan, checklist, and resolved/deferred questions before Implementing. |
| VERIFIED | `Get-Content -Raw docs/index.md; Get-Content docs/engineering.md` | FRD governs behaviour; engineering requires one Core owner, evidence tiers, four-lens review, and a proportional plan. |
| VERIFIED | `Get-Content -Raw docs/frd/frd-06-vehicle-and-engineering-evidence.md; Get-Content -Raw docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md; Get-Content -Raw docs/frd/frd-12-operator-experience.md` | D19 requires distinct ordered roles, normalized non-destructive curation, lease/version protection, and an issued snapshot. |
| VERIFIED | `rg -n -C 3 'AssessmentReportProjection|ReportImageEvidence|AssessmentReportDraft' src/Pegasus.Core/Reports` | Current projection passes every confirmed photo in occurrence order; drafts are transient and no curation aggregate exists. |
| VERIFIED | `Get-Content src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs \| Select-Object -Skip 485 -First 56; Get-Content -Raw src/Pegasus.Web/Pages/Cases/Closure.cshtml.cs` | Approval stores caller-supplied artifact identity/hash only; it cannot currently identify a generated draft or its curation. |
| VERIFIED | `rg -n -C 3 'ThirdPartyVehicleConfirmedAtUtc|ReadVersionsAsync|occurrence.Ordinal' src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs src/Pegasus.Core/Eva/EvaBundleSchema.cs` | Current report projection omits the third-party exclusion; EVA's Core eligibility shape supplies it. |
| VERIFIED | `rg -n -i 'reflection|reflected' src/Pegasus.Core src/Pegasus.Infrastructure src/Pegasus.Web tests -g '!src/Pegasus.Infrastructure/Persistence/Migrations/**'` | No reflection disposition exists; a new marker would be new scope. |
| VERIFIED | `Get-Content -Raw src/Pegasus.Core/Workflow/CaseEditAuthority.cs; Get-Content -Raw src/Pegasus.Infrastructure/Persistence/CaseMutationGuard.cs` | Corrected 2026-09-03: the guard owns authorization, archive/terminal, expected-version and lease refusal only, delegating the decision to `CaseEditAuthority`. Transaction, operation-key replay and mutation history are the `EfRepairSpecificationStore` convention, not the guard. |
| VERIFIED | `rg -n -C 3 'PlaywrightAssessmentReportRenderer|Photos\\(|SkiaSharp|IAssessmentReportProjectionSource' src/Pegasus.Infrastructure src/Pegasus.Core` | Existing renderer, projection port, content-store read path, and approved SkiaSharp dependency can be extended. |
| VERIFIED | `Test-Path src/Pegasus.Web/Pages/Cases/Shared/_CaseReportImages.cshtml; Test-Path src/Pegasus.Web/wwwroot/js/cropper.js; rg -n -i 'report-image|cropper' src/Pegasus.Web/wwwroot/css/site.css` | Both owned Web files are absent; required CSS classes have no rules. |
| VERIFIED | `mcp__kanmer__get_ticket_doc ENG-034 plan/plan.md; rg -n '@page|GenerateReportDraft|PreviewReportDraft' src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` | ENG-034 moves the Report host; CASE-038 owns `DetailsModel` and its Case-page handler surface. |
| VERIFIED | `Get-Content -Raw scripts/Test-MigrationGrants.ps1; rg -n 'Expected(Web|Worker)GrantSpec|\\$expected' scripts/Invoke-AzureDatabaseBootstrap.ps1 tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` | Every created runtime table needs grants, bootstrap census entries, and grant assertions. |
| ASSUMED | Supplied EPIC-012 allocation and lock order. | `CASE-038 → ENG-034 → ENG-031` serializes `OperatorLabels.cs` and the Report host; the migration lane is separately capacity-one. |
| RESOLVED | Controller answer to Q1 (2026-09-03) plus the verified draft/approval code. | Approval is the durable issue boundary: persist the approved artifact's exact curation snapshot with its identity and hash. |
| RESOLVED | Controller answer to Q2 (2026-09-03) plus the verified absence of a reflection classification. | `Not used` is the operator-controlled exclusion; no reflection marker is added. Third-party exclusion is enforced by Core eligibility. |

## Objective

Estimated diff: approximately 30 owned source and test files, including an EF
migration pair and model snapshot; no new package, Blob store, route, or
standalone Images area.

Add case-scoped report-image curation: eligible evidence can be selected as
distinct `Close-up` then `Overview`, followed by explicitly ordered
`Supporting` images. Persist normalized crops and quarter-turn rotation
non-destructively, render only an in-memory rendition, and retain the approved
artifact's curation snapshot.

## Governing behaviour

- A selected image must be current, custody-confirmed Case vehicle evidence,
  image-role, not logically removed, and not confirmed third-party evidence.
  Core rejects direct submissions that violate this policy.
- `Close-up` is position one and `Overview` is position two; they must be
  distinct. `Supporting` follows saved order. `Not used` is excluded.
- **RESOLVED (Q1, controller 2026-09-03):** approval is the durable issue
  boundary. The current draft is transient and FRD-11 says draft generation
  saves nothing, so ENG-031 does not create a durable draft-report version.
  Approval persists an immutable curation snapshot tied to the approved
  artifact identity and SHA-256. The approval route
  `/Cases/{id}/Closure?handler=RecordReportApproval` is a real production
  handler (`Closure.cshtml.cs` `OnPostRecordReportApprovalAsync`) already
  exercised end-to-end by `tests/Pegasus.IntegrationTests/CaseReportApprovalWebTests.cs`,
  so the snapshot contract is wired at the routed-handler tier. ENG-031 ships
  no approval button; an operator control for that handler stays outside this
  ticket.
- **RESOLVED (Q2, controller 2026-09-03):** an Engineer marks a reflected
  image `Not used`; no reflection marker is introduced. The curation policy
  independently excludes confirmed third-party vehicle evidence.
- Source bytes and their custody hashes remain unchanged. Crop, rotation, and
  rendition happen only in memory during report rendering.
- **D46:** one curation record per image, whichever entry point wrote it. A
  record exists for `Not used` images too — the role is the durable
  disposition, so the record carries role, order (where the role has one),
  normalized crop and quarter-turn rotation. Crop is offered on the Report
  image cards and in the Files image viewer without pressing Edit Case: a save
  arriving without a lease claims one through the existing
  `IAcquireCaseEditLease` and then performs the single guarded write.
- **D44/D45:** ENG-031 introduces no staff-review flag, checkbox, dialog,
  history event or Not-ready-to-Review gate, and no damage type. Curation
  affects report-draft readiness only.

## Dependencies and lock order

1. Wait for `CASE-038` to release `src/Pegasus.Web/Presentation/OperatorLabels.cs`
   and `src/Pegasus.Web/wwwroot/css/site.css`, then for `ENG-034` to land the
   Report section host. ENG-031 acquires the labels lock only after both.

2. `ENG-034` is the blocker for the Report host. Its hand-off must provide:

   - `src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml` composes
     `_CaseReportImages.cshtml` inside the Report section, before the existing
     readiness and Generate/Preview controls.
   - CASE-038's `DetailsModel` exposes
     `OnPostSaveReportImageCurationAsync` at
     `/Cases/{id}?handler=SaveReportImageCuration&section=report`, accepting
     the Case ID, expected version, operation key, edit-lease token, and the
     curation command.
   - `src/Pegasus.Web/Pages/Cases/Details.cshtml` registers the versioned
     external `~/js/cropper.js` script after `site.js`.

   These are dependencies, not ENG-031 changes: the host and handler files are
   outside its allocation. Stop rather than creating an unreferenced partial
   or a second Case mutation route.

3. CASE-038 owns `site.css`. It must hand off rules for `report-image` and
   `cropper` in that stylesheet before ENG-031 uses those classes. No inline
   CSS or script is permitted under the production CSP; `site.js` remains
   untouched.

4. Acquire `src/Pegasus.Infrastructure/Persistence/Migrations/**` only after
   the active migration lane releases it. Do not overlap, reorder, or add a
   second migration stream.

5. `src/Pegasus.Web/Pages/Cases/Closure.cshtml.cs` and
   `src/Pegasus.Core/Lifecycle/CaseLifecycle.cs` construct and validate
   `ReportApprovalSubmission`, so the snapshot reference cannot land without
   them. They are recorded as ENG-031 cross-lane edits sequenced behind
   CASE-040 (`CaseLifecycle.cs`, wave 4) and taken with that lane's hand-off;
   `Closure.cshtml.cs` is claimed by no active EPIC-012 lane. Add both to the
   Files document at take time. Stop if CASE-040 has not released
   `CaseLifecycle.cs`.

6. `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs` is also PLAT-070's
   file: D44 deletes `RequireStaffImageReviewBeforeEngineerAssignment` and
   `ImagesReviewedByStaff` from it. Acquire it only after PLAT-070 has merged,
   and never reintroduce either field or any review gate.

7. `src/Pegasus.Infrastructure/Reports/**` is claimed this wave by ENG-036
   (diagram rendering) and DOCS-018 (fee-note template) as well as ENG-031
   (photo rendering). The file is capacity-one: take
   `PlaywrightAssessmentReportRenderer.cs` only after both lanes release it,
   confine the ENG-031 edit to the `Photos(...)` region and its report CSS,
   and keep the crop/rotation rendition itself in the new ENG-031-owned
   `src/Pegasus.Infrastructure/Reports/ReportImageRendition.cs` so the shared
   file carries one call, not a second image pipeline.

8. The Files entry point (D46) needs document identity in the gallery chain:
   `src/Pegasus.Web/Presentation/GalleryImage.cs` and
   `src/Pegasus.Web/Pages/Shared/_ImageGallery.cshtml` carry
   `Href`/`DownloadHref`/`FileName`/`MediaType` only — no document or case id
   (verified). Both are ENG-031 changes; `_ImageGallery.cshtml` is a
   capacity-one `Pages/Shared/*` path. `_EvidenceViewer.cshtml` is not edited:
   `cropper.js` injects the Crop trigger into the existing viewer footer, and
   only for an item that carries the case/document identity, so the Triage,
   Intake and Image Intake viewers are unchanged.

9. Do not modify the Files document's prohibited paths, including Case
   Details, the retired Assessment page, Case Files/Documents/Vehicle/Custody,
   `site.css`, `site.js`, or `docs/design/test-ui/**`. If a snapshot capture
   changes the latter, hand it to UIIMP-014.

## Steps

1. **Confirm the merged host contract and acquire locks.**

   Files: none; dependency-only checks for the ENG-034/CASE-038 hand-off.

   Reuses: `DetailsModel`, the existing Case lease, and ENG-034's
   `_CaseReport.cshtml` host.

   Change: confirm the exact partial include, external script registration,
   and Case-page POST handler before creating any curation caller. Acquire the
   labels lock after CASE-038 and ENG-034; acquire the migration lock only when
   its lane is free. Record the CSS hand-off from CASE-038. Confirm the
   PLAT-070, CASE-040, ENG-036 and DOCS-018 releases named in the lock order.

   Tests: use `rg` over the merged host for `_CaseReportImages`,
   `SaveReportImageCuration`, and `cropper.js`; stop if any hand-off is absent.

2. **Add the Core-owned curation contract and report projection rules.**

   Files:
   `src/Pegasus.Core/Reports/ReportImageCuration.cs`,
   `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`,
   `src/Pegasus.Core/Reports/AssessmentReportRendering.cs`,
   `src/Pegasus.Core/Eva/EvaBundleSchema.cs`, and
   `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs`.

   Reuses: `AssessmentReportProjection`, `ReportImageEvidence.Validate`,
   `IAssessmentReportProjectionSource`, and — by call, not by copy — the
   eligibility predicate in `EvaHandoffPolicy.SelectEligibleImages`
   (`src/Pegasus.Core/Eva/EvaBundleSchema.cs:114`). One list per concept:
   lift the current/custody-confirmed/image-role/not-removed/not-third-party
   predicate into one Core owner and make both EVA and report curation call
   it; do not restate it in the new curation policy. `EvaBundleSchema.cs` is
   claimed by no other wave-4 lane.

   Change: define roles, normalized geometry, quarter-turn rotation, ordered
   selections, immutable snapshots, Core commands and ports. Reject duplicate
   primaries, invalid orders, ineligible source versions, malformed geometry,
   stale versions, and missing required roles. Make readiness require valid
   curation and project only the selected source versions. Extend rendering
   data with role/order/rendition metadata while preserving the source hash.
   Extend approval contracts with the immutable curation-snapshot reference.

   Tests: add
   `tests/Pegasus.Core.Tests/Reports/ReportImageCurationTests.cs`; extend
   `AssessmentReportProjectionTests.cs` and
   `AssessmentReportRenderingTests.cs`.

3. **Persist curation and render the selected in-memory rendition.**

   Files:
   `src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs`,
   `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs`,
   `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`,
   `src/Pegasus.Infrastructure/Persistence/EfReportImageCurationStore.cs`,
   `src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs`,
   `src/Pegasus.Infrastructure/Reports/ReportImageRendition.cs`,
   `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`
   (the `Photos(...)` region only; capacity-one with ENG-036 and DOCS-018),
   `src/Pegasus.Infrastructure/Persistence/CaseWorkflowEntities.cs`,
   `src/Pegasus.Infrastructure/Persistence/CaseWorkflowModelConfiguration.cs`,
   `src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs`, and
   `src/Pegasus.Infrastructure/DependencyInjection.cs`.

   Reuses: `CaseMutationGuard` for authorization, archive/terminal, expected
   version and lease refusal only — it owns nothing else (verified:
   `CaseMutationGuard.cs` delegates the decision to `CaseEditAuthority`).
   Transaction, operation-key replay and mutation history follow the
   `EfRepairSpecificationStore` convention; no shared helper provides them, so
   this store repeats that store's shape rather than inventing a third. Also
   reuses current assessment mappings,
   `IDocumentContentStore.ReadVersionsAsync`, `IAcquireCaseEditLease` for the
   lease-claiming save (D46), and the existing Playwright renderer with
   SkiaSharp.

   Change: persist versioned curation headers, one curation item per curated
   source image — `Not used` included, since the role is the durable
   disposition (D46) — carrying role, order, crop, rotation and source-version
   identity, and immutable approval snapshots with constraints for roles,
   ordering, hashes, and foreign keys.
   Use the Case mutation guard for authorization, archive/terminal,
   expected-version and lease refusal, and the repair-specification store's
   transaction, replay and history shape around it. A save presented without a
   lease claims one through `IAcquireCaseEditLease` and writes under the
   returned token and version; both entry points update the same curation item
   and never create a duplicate. Project only curated eligible versions,
   apply crop/rotation to transient renderer bytes,
   and retain original bytes and hashes unchanged. Register the new store.

   Tests: add
   `tests/Pegasus.IntegrationTests/ReportImageCurationPersistenceTests.cs`;
   extend `Reports/AssessmentReportRendererTests.cs`,
   `Reports/AssessmentReportDraftWebTests.cs`, and
   `CaseReportApprovalWebTests.cs`.

4. **Create the serialized schema and grant update.**

   Files:
   `src/Pegasus.Infrastructure/Persistence/Migrations/[timestamp]_ReportImageCuration.cs`,
   `src/Pegasus.Infrastructure/Persistence/Migrations/[timestamp]_ReportImageCuration.Designer.cs`,
   `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`,
   `scripts/Invoke-AzureDatabaseBootstrap.ps1`, and
   `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs`.

   Reuses: the current EF migration, runtime-grant, bootstrap-census, and
   grant-spec test conventions.

   Change: generate the additive curation and snapshot schema, with exact
   Web/Worker privileges or an explicit justified exemption for every table.
   Add the same tables and permissions to the production bootstrap census and
   expected grant specifications.

   Tests: run `./scripts/Test-MigrationGrants.ps1` and the integration grant
   assertions through the normal test command.

5. **Compose the Report-section UI and external cropper.**

   Files:
   `src/Pegasus.Web/Presentation/OperatorLabels.cs`,
   `src/Pegasus.Web/Pages/Cases/Shared/_CaseReportImages.cshtml`,
   `src/Pegasus.Web/Presentation/GalleryImage.cs`,
   `src/Pegasus.Web/Pages/Shared/_ImageGallery.cshtml`, and
   `src/Pegasus.Web/wwwroot/js/cropper.js`.

   Reuses: `OperatorLabels.CaseWorkspace`, CASE-006/DOCS-011 evidence-viewer
   URLs and `data-evidence-item` convention, the external-script IIFE pattern
   in `site.js`, and the single Case edit-lease hidden fields.

   Change: add all operator-visible roles and actions only in
   `OperatorLabels.cs`. Render eligible images, role selection, deterministic
   supporting-order controls, and a Crop action on every Report image card.
   Carry the case and document identity on each gallery tile (`GalleryImage`
   plus `_ImageGallery.cshtml`) so `cropper.js` can inject the Crop trigger
   into the existing `_EvidenceViewer` footer for case images — the D46 Files
   entry point — without editing that shared partial and without adding the
   control to the Triage, Intake or Image Intake viewers. The external cropper
   provides pointer drag of the frame, eight resize handles, keyboard arrow
   move and Shift+arrow resize, quarter-turn rotation, Free/4:3/3:2/1:1 aspect
   lock, Reset to the saved preparation, Full frame (`{x:0,y:0,w:1,h:1}`, the
   initial state, not a duplicate of Reset), and a live canvas preview —
   matching `24-cropper.js`. It assists the form only; server-side Core
   validation remains authoritative. Crop and its save are offered whenever the
   Case is mutable, with no prior Edit Case press (D46); the remaining role and
   order editors follow the existing editable-only rule.

   Tests: extend `AssessmentReportDraftWebTests.cs` to prove the Report caller
   posts through the handed-off Case route and renders readiness from Core; add
   `tests/Pegasus.IntegrationTests/Browser/ReportImageCropBrowserTests.cs`
   (`[Trait("Category", "Browser")]`, reusing `Browser/BrowserTestSupport.cs`)
   proving both D46 entry points open the cropper, drag/handle/keyboard/aspect/
   reset/rotate/preview behaviour, the lease-claiming save, and that a second
   crop through the other entry point updates the same curation record.

6. **Run the delivery checks and hand off for review.**

   Files: no additional owned files.

   Reuses: existing report fixtures, report-renderer provider,
   `AssessmentWorkspaceTestData`, and the approval Web-test fake.

   Change: run the four simplification lenses over this branch's diff, record
   dated findings and dispositions, write the post-implementation report, and
   open the PR against `dev`.

   Tests: run the commands below. If the routed Case render changes a Test UI
   artifact, stop that file change and use the UIIMP-014 dependency rather than
   modifying `docs/design/test-ui/**`.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify
./scripts/Test-UiCatalogue.ps1
./scripts/Test-MigrationGrants.ps1
```

## Acceptance conditions

- Report generation fails closed until two distinct eligible images are saved
  as `Close-up` then `Overview`.
- Supporting images render in the saved operator order, after the two required
  roles, and direct invalid or duplicate-role submissions are refused.
- Cropping and rotation change only the transient report rendition; persisted
  source bytes and their custody hashes remain unchanged.
- Confirmed third-party vehicle evidence cannot be curated or rendered.
  Reflected images remain excluded when the Engineer chooses `Not used`.
- A stale or lease-invalid save fails without overwriting another Engineer's
  curation, and an exact replay remains idempotent.
- Approval retains the artifact identity/hash and immutable curation snapshot;
  later source or curation changes cannot alter that snapshot.
- The Report partial has a real Case-page caller and POST handler; no standalone
  Images route, derivative Blob store, second renderer, or second mutation path
  exists.
- New schema tables have matching grants, bootstrap census entries, migration
  metadata, and passing runtime-role checks.
- The UI has keyboard-operable crop and ordering controls, no inline
  styles/scripts, no explanatory copy, and no disabled inert controls.
- Crop opens from a Report image card and from the Files image viewer without
  a prior Edit Case press; a crop saved from either updates the one curation
  record for that image and claims the lease when none is held.
- Exactly one curation record exists per curated image, `Not used` included.
- No staff-review flag, checkbox, dialog, history line or review gate is
  introduced or restored (D44); no damage type appears (D45).

## Design rules that bind

- No explanatory copy: render labels, values, controls, and named readiness
  blockers only; no crop instructions, empty-state panel, or mechanics prose.
- All new operator-visible labels belong in
  `src/Pegasus.Web/Presentation/OperatorLabels.cs`. Use exact role labels:
  `Close-up`, `Overview`, `Supporting`, and `Not used`.
- Existing Case-state display labels remain owned by the
  `OperatorLabels.CaseStage(string?)` mapping; do not add another state-label
  map.
- Report-image preparation is present only on the composed Report section.
  Excluded capabilities are absent; a genuine readiness condition may disable
  Generate report draft and name the outstanding condition.
- `report-image` and `cropper` use the CASE-038 stylesheet hand-off. Production
  CSP forbids inline styles and scripts; `cropper.js` is an external,
  self-registering IIFE.
- Web composes and translates requests only. Core owns eligibility, role/order
  rules, geometry validation, snapshot creation, and conflict semantics.

## Stop condition

The implementation PR is open against `dev`, ENG-031 is in Review, and no
merge, release, proof, or neighbouring-ticket work has been performed.

## Simplification pass

Recorded at execution time under a dated heading.

## Resolutions (2026-09-03)

- Controller: report approval (`CaseReportApprovals`) snapshots the curation.
- Controller: the `Not used` role is the disposition for a reflection image; no new marker.
- Operator (D46): the crop tool behaves like any photo-editing cropper (drag, handles, rotate, aspect, reset, live preview) and opens from the Files image viewer and the Report image cards without pressing Edit Case; saving a crop starts the edit lease. Plan the viewer entry point alongside the card entry point; one curation record per image.

## Plan review (2026-09-03, gpt-5.6-sol xhigh; dispositions Claude Opus)

Verdict read: REQUEST CHANGES. Eight findings; seven fixed in this plan, one
partly rejected with reason. No finding needed an operator decision.

| # | Severity | Step | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | 5, deps | D46's Files entry point is unplanned; the plan renders crop only in edit mode and forbids the Files chain. | Fixed. Step 5 adds `GalleryImage.cs` and `_ImageGallery.cshtml` (case/document identity), `cropper.js` injects the Crop trigger into the existing `_EvidenceViewer` footer for case images only, `_EvidenceViewer.cshtml` stays unedited, and a lease-less save claims one through `IAcquireCaseEditLease` (verified: `CaseCommandContracts.cs:77`, `OnPostClaimLeaseAsync`). Full-frame kept and distinguished from Reset per `24-cropper.js`. |
| 2 | blocker | 3 | "Selected source-version rows" does not give one curation record per image; `Not used` has no durable row. | Fixed. Step 3 persists one curation item per curated image including `Not used`, with role, order, crop, rotation and source-version identity; both entry points update that item; Browser test asserts no duplicate. |
| 3 | blocker | 2–3, dep 5 | Approval boundary still written as ASSUMED/conditional while Q1 is resolved; contract change refuses its matching callers. | Fixed. Conditional language removed; `Closure.cshtml.cs` and `CaseLifecycle.cs` recorded as ENG-031 cross-lane edits sequenced behind CASE-040. Rejected in part: no approval *button* is added — the route is a real production handler already covered by `CaseReportApprovalWebTests.cs`, and an operator control for it is outside ENG-031's owned paths (rule 2). |
| 4 | blocker | 3 | `PlaywrightAssessmentReportRenderer.cs` is claimed by ENG-036 and DOCS-018 too; no hand-off recorded. | Fixed. New lock item 7 serializes the file behind both lanes, confines the ENG-031 edit to the `Photos(...)` region, and moves the crop/rotate rendition into the new ENG-031-owned `Reports/ReportImageRendition.cs`. |
| 5 | blocker | 5–6, commands | Tests cannot prove the cropper; the renderer tests are Browser-tagged (verified `AssessmentReportRendererTests.cs:15,63`) yet the command excluded Browser and departed from the canonical `Category!=Corpus` filter; `-Verify -SkipCapture` reuses evidence. | Fixed. Canonical filter restored, `-SkipCapture` dropped, and `Browser/ReportImageCropBrowserTests.cs` added on the existing `Browser/BrowserTestSupport.cs` harness (no new package). |
| 6 | should-fix | 3 | `CaseMutationGuard` does not own replay, history or atomicity. | Fixed. Reuse claim narrowed to authorization, archive/terminal, version and lease; transaction/replay/history follow the `EfRepairSpecificationStore` convention, stated as a repeat because no shared helper exists. |
| 7 | should-fix | 2 | `CaseWorkflowContracts.cs` is also PLAT-070's D44 deletion file and is absent from the lock order. | Fixed. New lock item 6 sequences it behind PLAT-070 and forbids reintroducing any review field or gate; governing behaviour states the D44/D45 non-goals. |
| 8 | should-fix | 2 | "Reuse the eligibility shape" does not guarantee code reuse; one list per concept. | Fixed. Step 2 now requires one Core eligibility owner called by both EVA and report curation, with `EvaBundleSchema.cs` named (unclaimed by other wave-4 lanes). |
