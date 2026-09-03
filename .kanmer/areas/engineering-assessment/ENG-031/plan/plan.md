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
  is therefore: if the operator confirms approval as the snapshot boundary,
  the `ReportApprovalSubmission` extension in `CaseWorkflowContracts.cs` needs
  a matching change in those two files — sequence behind CASE-040 for
  `CaseLifecycle.cs`, and record `Closure.cshtml.cs` as an ENG-031 conditional
  change in the Files document at take time. No Razor control posts to that
  handler today (0 matches in `src/Pegasus.Web` `.cshtml`), so approval has
  no operator caller yet; choosing it as the snapshot boundary also needs
  that caller, which is a dependency, not ENG-031 scope.
- FRD-11 (lines 108–118) confirms the premise behind the Q1 default: draft
  generation "saves nothing"; approval and issue are separate human acts.
  The default stays ASSUMED and the question stays open on the board.
- The conditional approval-linkage rows (Steps 2–3 and Dependency 5) must
  not be built until Q1 is answered or parked; everything else in the plan is
  unconditional.
- `docs/design/README.md` line 840 is the source of the production CSP
  no-inline-style rule the plan cites.

The Codex plan follows unchanged apart from the `CaseStage` wording.

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
| VERIFIED | `Get-Content -Raw src/Pegasus.Core/Workflow/CaseEditAuthority.cs; Get-Content -Raw src/Pegasus.Infrastructure/Persistence/CaseMutationGuard.cs` | Existing guard owns expected-version, lease, replay, and stale-write refusal. |
| VERIFIED | `rg -n -C 3 'PlaywrightAssessmentReportRenderer|Photos\\(|SkiaSharp|IAssessmentReportProjectionSource' src/Pegasus.Infrastructure src/Pegasus.Core` | Existing renderer, projection port, content-store read path, and approved SkiaSharp dependency can be extended. |
| VERIFIED | `Test-Path src/Pegasus.Web/Pages/Cases/Shared/_CaseReportImages.cshtml; Test-Path src/Pegasus.Web/wwwroot/js/cropper.js; rg -n -i 'report-image|cropper' src/Pegasus.Web/wwwroot/css/site.css` | Both owned Web files are absent; required CSS classes have no rules. |
| VERIFIED | `mcp__kanmer__get_ticket_doc ENG-034 plan/plan.md; rg -n '@page|GenerateReportDraft|PreviewReportDraft' src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` | ENG-034 moves the Report host; CASE-038 owns `DetailsModel` and its Case-page handler surface. |
| VERIFIED | `Get-Content -Raw scripts/Test-MigrationGrants.ps1; rg -n 'Expected(Web|Worker)GrantSpec|\\$expected' scripts/Invoke-AzureDatabaseBootstrap.ps1 tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` | Every created runtime table needs grants, bootstrap census entries, and grant assertions. |
| ASSUMED | Supplied EPIC-012 allocation and lock order. | `CASE-038 → ENG-034 → ENG-031` serializes `OperatorLabels.cs` and the Report host; the migration lane is separately capacity-one. |
| ASSUMED | Verified draft/approval code contradicts snapshot-at-draft persistence. | Approval is the durable issue boundary for this ticket: persist the approved artifact's exact curation snapshot with its identity and hash. |
| ASSUMED | Verified absence of a reflection classification. | `Not used` is the operator-controlled exclusion; no reflection marker is added. Third-party exclusion is enforced by Core eligibility. |

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
- **ASSUMED:** approval is the durable issue boundary. The current draft is
  transient and FRD-11 says draft generation saves nothing, so ENG-031 does
  not create a durable draft-report version. Approval persists an immutable
  curation snapshot tied to the approved artifact identity and SHA-256.
- **ASSUMED:** an Engineer marks a reflected image `Not used`; no reflection
  marker is introduced. The curation policy independently excludes confirmed
  third-party vehicle evidence.
- Source bytes and their custody hashes remain unchanged. Crop, rotation, and
  rendition happen only in memory during report rendering.

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
   `src/Pegasus.Core/Lifecycle/CaseLifecycle.cs` are outside this ticket's
   owned paths but construct and validate `ReportApprovalSubmission`. Their
   owner must accept the new snapshot reference required by the approval
   contract; ENG-031 does not edit them.

6. Do not modify the Files document's prohibited paths, including Case
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
   its lane is free. Record the CSS hand-off from CASE-038.

   Tests: use `rg` over the merged host for `_CaseReportImages`,
   `SaveReportImageCuration`, and `cropper.js`; stop if any hand-off is absent.

2. **Add the Core-owned curation contract and report projection rules.**

   Files:
   `src/Pegasus.Core/Reports/ReportImageCuration.cs`,
   `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`,
   `src/Pegasus.Core/Reports/AssessmentReportRendering.cs`, and
   `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs`.

   Reuses: `AssessmentReportProjection`, `ReportImageEvidence.Validate`,
   `IAssessmentReportProjectionSource`, and the eligibility shape in
   `EvaHandoffPolicy.SelectEligibleImages`.

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
   `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`,
   `src/Pegasus.Infrastructure/Persistence/CaseWorkflowEntities.cs`,
   `src/Pegasus.Infrastructure/Persistence/CaseWorkflowModelConfiguration.cs`,
   `src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs`, and
   `src/Pegasus.Infrastructure/DependencyInjection.cs`.

   Reuses: `CaseMutationGuard`, repair-specification versioning, current
   assessment mappings, `IDocumentContentStore.ReadVersionsAsync`, and the
   existing Playwright renderer with SkiaSharp.

   Change: persist versioned curation headers, selected source-version rows,
   and immutable approval snapshots with constraints for roles, ordering,
   hashes, and foreign keys. Use the Case mutation guard for authorization,
   lease, expected-version, replay, history, and atomicity. Project only
   curated eligible versions, apply crop/rotation to transient renderer bytes,
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
   `src/Pegasus.Web/Pages/Cases/Shared/_CaseReportImages.cshtml`, and
   `src/Pegasus.Web/wwwroot/js/cropper.js`.

   Reuses: `OperatorLabels.CaseWorkspace`, CASE-006/DOCS-011 evidence-viewer
   URLs and `data-evidence-item` convention, the external-script IIFE pattern
   in `site.js`, and the single Case edit-lease hidden fields.

   Change: add all operator-visible roles and actions only in
   `OperatorLabels.cs`. Render eligible images, role selection, deterministic
   supporting-order controls, crop action, and the crop dialog. The external
   cropper provides pointer and keyboard move/resize, quarter-turn rotation,
   aspect choice, reset, full-frame, preview, and saved ordering. It assists
   the form only; server-side Core validation remains authoritative. Render
   edit controls only while the Case is editable.

   Tests: extend `AssessmentReportDraftWebTests.cs` to prove the Report caller
   posts through the handed-off Case route and renders readiness from Core.

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
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
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
