# Plan — ENG-029

## Decision and starting state

Root approved bounded author execution after whole plan/map read and the
2026-09-08 05:17 UTC source handoff recorded in scratch/execution.md.
Author worktree baseline: aefe4c32d078ad79c0368666b5666032e6865248.
Current integration tip is 96777888bfa7ee7f85d63979a4a09ae10cda7d13
(INTK-064 PR699, merged 2026-09-08T06:16:37Z); do not merge it into the
dirty author worktree before root's coordinated checkpoint.
TICK-085 PR698 and DOCS-019 are integrated; preserve their changes. The earlier
preparation baseline was 498144b0bb55b68fd53b9a31ffc89ef90622c73a.
The current files document owns the exact scope; historical versions remain.
Fresh ready whole-ticket packet and exact isolated worktree/take are required.
No author build/test; root owns heavy verification and publication follows PASS.

This replaces current instructions in plan fd4c8f09cc5eb6ec and checklist
4315a6e0b2bfc2d1. Their earlier eight review-finding dispositions and research
remain available in board history; nothing here reclassifies historical
verification as current PASS. Current user scope, FRD-06/11, current Case
design and root's explicit v1 decisions supersede section-specific POSTs,
missing-vocabulary/schema work, a second repair-duration field and Vehicle
History in Report. Root synchronized the ticket body before this handoff.

## Outcome and exclusions

An authorized person edits the required Settlement/Report values on the Case,
then the existing single Save records them with the other Case edits in one
transaction. Reloaded fields, named readiness and report preview use those
same persisted facts. Retained content remains viewable without edit access.

Reuse ISaveCaseWorkspace/CaseWorkspacePolicy/EfCaseWorkspaceStore;
AssessmentVocabulary/AssessmentPolicy; existing field/lease/conflict controls;
CaseReportReadiness and ICaseReportSnapshotSource; existing sign-off resolver
and account query; AssessmentReportProjection/ReportRepairCosts/EstimateTotals.
No new form endpoint, store, schema, package, queue, policy taxonomy, generic
form framework, renderer, AI or provider integration.

Do not implement ENG-031 image crop controls, ENG-036 diagram, the Estimate
import/Glass work of TICK-085, new optional ratios, live sends or deployment.
Existing Statement of truth display stays unchanged: this is not permission
to invent accepted legal wording or a new wording editor.

## Field and screen contract

| Location | Existing owner written by the one Save |
| --- | --- |
| Settlement | Outcome/category/salvage value; excess/betterment/claimant VAT/reserve; repair/report delays; hire start/daily cost/diminution; salvage location/agent/reference/moved/owner retains/value agreed/settled. Use existing canonical paths and closed codes/types, never labels as keys. |
| Settlement typed costs | Storage per day and recovery through CaseWorkspaceInspection.StoragePerDay/RecoveryCharge, not free dictionary copies. Existing lump storage charge and Repairer VAT remain distinct; do not silently relabel them. |
| Settlement derived | Current accepted estimate's repair total and RepairDays, accepted Engineer value and Core-owned equity. RepairDays is edited only through the existing Estimate detail/correction workflow. No new ratio calculation. |
| Vehicle | One Vehicle History textarea/value using AssessmentVocabulary.HistoryCheck, routed through the existing Vehicle section, absent from Report controls. |
| Report | Engineer comments, agreed fee, description lines; flagged eligible sign-off account; Disclose guide source/Include valuation commentary/Include unrelated damage; report-date override and typed report date. Existing generation/preview/fee-note/delivery actions remain their own accepted commands. |

Root decided the unused settlement.repair_duration constant/definition is
removed, not retained as an ineffective writable field. The complete source,
test and documentation census found no consumer beyond those two declarations.
RepairDays already has accepted Estimate/projection tests; preserve them.

### UI/UX brief

Case Details is the existing route/host. Retain the eleven sections and
case-edit-form in _CaseWorkflow; all new native controls use
form="case-edit-form", the one sticky Save/Discard, existing reason/operation,
version and lease. No nested forms, per-section Save or broad autosave.
Outcome choices use a native select with current field styling; codes come
from Core. The design's outcome-option class has no current stylesheet rule;
root approved this native-control refinement without CSS or an inert class. Money/date/flag/text controls follow
their current definitions; absent is blank, never an output placeholder.
Ordinary flags preserve absent/false/true as supported; report switches follow
their existing absent-is-off semantics. Default focus/tab order follows the
visible section, labels associate with controls, errors associate with fields.

Read-only states render current recorded values and no inert explanatory
panels. New finding controls are editable only for authenticated Engineers;
non-finding controls do not imply that role. Held, Complete/terminal/archive
and missing/foreign/expired lease states remain non-editable. All engineering
submissions, including crafted POSTs, consult the existing strict Core
AssessmentAccessPolicy. NotReady/Review keep their ordinary Case-fact editor,
not engineering-field editing. Do not add a second lifecycle rule in Web.

Use current panel/grid/spacing/classes and dirty/discard behavior; site.js
already sees form-associated controls. No CSS/JS change is planned. Verify
the existing supported 1580/1100/760 layouts for editable, read-only and
conflict states. Existing global save-in-Review warning claiming forced
demotion is removed because workspace save re-evaluates factual completeness,
not a reviewed checkbox.

## Implementation sequence

1. After explicit root approval/ownership clearance, fresh get_status/gates,
   active claim/capability/file/worktree census and exact packet; take only
   ENG-029's isolated recorded branch/worktree from current accepted dev.
   Reconcile TICK-085's accepted Details delta before edits. Never clear the
   historical ENG-034/CASE-040/CASE-047 claims to make a gate look ready.

2. Replace Details.OnPostSaveAsync's existing ISaveCase call, not add another
   handler. Bind canonical controls and populate existing optional workspace
   sections. Missing submitted section remains null/untouched. A submitted
   section's unshown accepted claim-source, storage-business, inspection
   provenance/contact/notes, dates, odometer and display-unit members are
   preserved using the existing projection, not defaulted to null. Preserve
   Confirmed-before-Fact and never promote Current's suggestion fallback.
   Distinguish explicit blank/false from no submission. Keep the original
   expectedVersion/operation/reason/lease; never refresh authority to hide a
   stale form. Route history/storage/recovery/report date only once.

   New engineering field posts use AssessmentAccessPolicy before invoking
   the command; the transaction's version/lease/archive guard still binds
   that read to current persisted state. Keep ordinary pre-handoff Case-data
   save behavior. Add only PostReport to AssessmentPolicy.IsWritableState;
   no other state/role broadening. Existing Core finding and merged-state
   required-when validation remain authoritative. All prior SaveCase Web
   fixture assertions must be translated to the actual workspace request,
   not removed. The separate existing MCP/address ISaveCase callers remain.

3. Add the form-associated editors in the existing partials, and the one
   Vehicle History control. Add missing display labels in the existing
   presentation owner. If a shared editor-field presentation list is needed
   for render/binding/proposed retention, keep it once there with Core path
   constants; do not copy types, limits, codes or business permissions.
   Retain all untouched existing component/action markup.
   Extend the existing bounded proposed-value flow for these same values,
   including explicit clears/false and safe sign-off display; exclude tokens
   and concurrency authority. On validation/stale/lease refusal, no success,
   no partial save and no silent loss: current-versus-proposed survives with
   current bounded shortened/dropped warnings where applicable.

4. Load report readiness through ICaseReportSnapshotSource's metadata-only
   path and CaseReportReadiness.Evaluate. Reuse its eligible sign-off data
   and persisted/assigned resolver; no manually assembled qualification/
   signature tuple, fake readiness or image content/rendering on GET.
   Show named outstanding requirements, never percentages. Generation and
   preview still re-read/revalidate through their existing actual callers;
   a page indicator is not authority to render. ReportDate override false
   must not accidentally clear an unposted stored date; no hand-typed
   signatory identity substitution or ineligible account option.

   Make the existing BuildSettlement calculation reusable by the page through
   a small pure method in AssessmentReportProjection, retaining its existing
   report call and ReportSettlement output. Missing accepted estimate/value
   means absent derived figures, not zero facts or a placeholder readiness
   pass. Reuse ReportRepairCosts/EstimateTotals; no Razor/JS arithmetic.
   Remove the dead repair-duration path and align directly affected canonical
   documentation. Stop for root/map amendment if a current dependency needs
   changes outside the exact approved file map.

5. Freeze source and provide root exact focused filters. Root alone restores/
   builds once and runs the bounded actual callers below; retain every failure.
   Correct an observed failure at its actual owner, not by weakening assertions.
   Root performs scoped capture/update/verify/catalogue; manual visual acceptance
   follows the explicit sequencing below if its browser environment is blocked.
   After actual runtime and scoped snapshot PASS, write report, update checklist/traceability, publish the
   same ticket PR to configured dev and stop at independent review. No self
   review/merge or claimed deployment.

## Focused acceptance and verification

- Existing Core AssessmentPolicyTests plus AssessmentReportProjectionTests:
  retained normalization/bounds/required-when/finding behavior; PostReport
  acceptance with unchanged other states; removed dead path refused; derived
  totals/equity/RepairDays are identical to the accepted report projection
  and absent inputs do not become zero-valued facts.
- Existing CaseDetailsWebTests + CaseEditModeWebTests: render/form ownership;
  one global Save receives every listed current canonical value and typed
  member; old claimant/address/inspection/hidden facts still carried; only
  eligible signers; blank/false values; stale/lease/role/state/antiforgery
  refusals retain proposed values; no percentage or explanatory new copy.
- Extend CaseWorkspacePersistenceTests' existing CaseDataHarness for one
  complete Overview/Settlement/Report transaction in each writable engineering
  state. Assert exact fields, sign-off/date, preserved unsubmitted facts,
  factual completeness (no forced NotReady), one Case version/history event,
  current report invalidation, identical operation replay and changed-intent/
  stale/foreign lease/invalid finding or terminal refusal with no partial
  persistence. Reuse existing fixture values; no new SQL host or domain pack.
- Existing AssessmentReportDraftWebTests: real Case GET readiness matches the
  metadata source (including accepted/ineligible signer and current estimate),
  ordinary GET opens no image bytes and invokes no renderer; after a real
  workspace save, actual preview receives the same persisted settlement/
  comments/history/fee/content/date/signatory facts. Existing preview outcome,
  clock and authorization assertions remain.
- Root scoped snapshots:
  Update-TestUiSnapshots.ps1 -Scope case-details
  -CaptureFilter "FullyQualifiedName~CaseDetailsWebTests";
  then -Verify -SkipCapture -Scope case-details and Test-UiCatalogue.ps1.
  Reuse that successful capture for verification, not another whole capture.
  Browser/visual checks of edit/read-only/conflict at the named widths use
  existing fixtures and capture infrastructure; no broad renderer/full-browser
  cohort is required for a field writer.

Acceptance is actual focused proof, not new test infrastructure or repeated
whole-repository rails. Root retains the final converged EPIC-014 solution/
release obligation. A fresh build alone is not caller or visual acceptance.

## Gates, ownership and stop

Live feature gates require research/files/plan/checklist and resolved questions.
ENG-035 vocabulary and PLAT-068 sign-off have integrated Done work; the old
blocking edges are not fresh missing implementations. ENG-034 hosting is
integrated but its historical taken record remains; do not call it Done here.
TICK-085's integrated Details, labels, FRD-06 and Case captures/index are
preserved under root's exact mapped source handoff. Any later correction to
those files must coordinate with this lane. Historical ENG-034/CASE-040/
CASE-047 claims remain unchanged; only the obsolete ENG-034 blocking edge
was removed because PR674's host is integrated.

The ready packet authorizes only this exact map and the implementation
sequence above. Freeze for root's focused runtime/capture verification before
publication. After root PASS, report/push one dev PR and stop for independent
review; no self-review/merge, live/provider action, new schema or deployment.

## Direct-caller wording correction approved during implementation

Root's independent Core-slice review found the three direct writable-state
callers repeat a now-stale allowed-state list. In the mapped
EfCaseWorkspaceStore, EfCaseAssessmentStore and EfValuationStore, replace
only that denial wording with a current-state refusal. No guard, query or
permission changes. The Core writable-state owner remains the single list.
Root approved this exact bounded extension before edits; no tests assert the
old wording. Preserve all other behavior and historical review dispositions.

## Existing Engineer-section fixture caller amendment

Root approved adapting CaseEngineerSectionsWebTests' existing source fake to
supply the newly consumed metadata snapshot port. Its two existing test
methods keep their full lifecycle, recorded-value and read-only assertions;
no new fixture host or production change. Root separately owns the already
mapped CaseWorkspacePersistenceTests and AssessmentReportDraftWebTests
changes while pack_reconcile owns the other Web fixtures and source.

## Unchanged accepted-value provenance correction

Root confirmed the current shared CaseDataFieldWriter.SetConfirmed recreates
an equal accepted Fact as a staff confirmation and refreshes equal Confirmed
actor/time when a submitted workspace section carries unshown fields. This
contradicts this plan's preserved-facts requirement. Add only
src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs to the write map:
make an exact equal value/type no-op when it matches an existing Confirmed,
or a Fact when no Confirmed exists. Suggestion is never accepted for this
comparison. Keep actual changed-value and explicit-clear behavior, source
metadata for real changes and the existing two callers: workspace save and
EfCaseDataStore's SaveCase path (including its current MCP caller).

No reader, schema, request flag, additional store or independent writer.
Root owns the focused existing CaseWorkspacePersistenceTests submitted-
Overview proof: equal Fact remains Fact with original provenance, equal
Confirmed retains actor/time, Suggestion-only input is not treated as an
accepted no-op, and actual changes/clears plus one transaction/replay remain
covered. Do not claim this SQL proof until root executes it. The author owns
only the existing writer guard and must freeze it before root verification.

Root also identified the existing assertion consumer
CaseDataCompletenessPersistenceTests.ConfirmAndSaveUseSharedVersionLeaseReplayAndImmutableHistory:
its equal Jane Example and AB12CDE expectations currently require redundant
Confirmed rows. Update only those assertions to unchanged original Fact/source
and no Confirmed, preserving all version/lease/replay/history/identity and
readiness assertions. Add that exact method to focused verification. Its file
contains INTK-064's integrated constructor change. Root's explicit handoff
after PR699 merged at 96777888bfa7ee7f85d63979a4a09ae10cda7d13 releases
only this assertion method. Preserve the constructor change during later
coordinated integration; do not cherry-pick or merge into the dirty tree. Root still owns the separate
submitted-Overview test in CaseWorkspacePersistenceTests.

## Publication sequencing — approved 2026-09-08

Root and the independent reviewer explicitly permit source/runtime integration
with manual visual acceptance retained as an outstanding Verifying obligation.
The corrected source has focused runtime and scoped capture/update/verify/
catalogue PASS. Manual inspection at 1580/1100/760 for editable, read-only and
conflict states was blocked by browser local-file URL security and is
INCONCLUSIVE, not waived or substituted by the existing 1440 snapshot checks.
Keep its checklist marker unchecked and do not claim Done until the required
visual acceptance is genuinely completed. No extra backend, renderer or test
infrastructure is authorized to work around this environment restriction.

For this publication only, checkpoint the 25 mapped source/doc/test files and
actual scoped snapshot deltas, then normally merge accepted origin/dev
96777888bfa7ee7f85d63979a4a09ae10cda7d13 into the clean author branch.
Preserve INTK-064's constructor change alongside the handed-off assertion
method. No new build at this step. Return the integrated checkpoint/source
census to root before push/PR. Independent whole-ticket review is owned by
principal_delivery_audit; root contributed Core/SQL/report tests and is not
that reviewer. Exact merged-source verification and manual visual acceptance
remain later gates, with no provider/deployment claim.

## Remediation round 1 — F-004 existing caller fixtures

Independent review e636932136f144cc returned PR700 at exact
f86054c0e7cc73cb6245355dd21c03e58196d582 to Implementing on
2026-09-08T07:21:12.133Z. Root approved one two-file correction batch.
Resume ENG-029-case-workspace-editors in .worktrees/eng-029 through the
fresh exact recorded packet; no take, new branch/worktree, base merge or PR.
The initial fresh-take/checkpoint steps above are historical, not re-entry
instructions. Current accepted dev is26ba4ed408317cccdb354dc1e115b0297f15df94;
do not merge it into this source during the correction without coordination.

### Expected files for this correction only

- tests/Pegasus.IntegrationTests/Browser/AssessmentReadinessSummaryBrowserTests.cs
- tests/Pegasus.IntegrationTests/AssessmentEstimateImportWebTests.cs

The original 27-path PR remains frozen evidence; it does not authorize
additional edits during this remediation. No production source, Core gate,
Razor markup, schema, snapshot, shared fake, source manifest, intake fixture
or unrelated failing CI case changes. Root exclusively runs verification.

### Ordered correction

1. Reuse the existing browser projection fake as ICaseReportSnapshotSource,
   replacing its obsolete IAssessmentReportProjectionSource registration.
   Return metadata-only CaseReportFreezeInputs/CaseReportReadinessInput from
   the same near-empty fixture, with empty photos/preparations and absent
   accepted estimate/value/signatory. Make Case, assessment/workspace and
   existing access fake consistently ReportPreparation (post-handoff but
   report-incomplete). No invented ready facts. Preserve all existing named
   Not ready, disabled button, no Preview, shell/estimate-empty and axe
   assertions. Add only a direct metadata-read assertion if useful to prove
   the actual production port was called; no new host or reader.
2. In the existing RecordingStores.SetCurrentEstimate implementation only,
   use EstimateTotals.Compute on the Draft before marking it Accepted and
   record the same totals/basis as real acceptance. If an already Accepted
   estimate is selected, retain its frozen breakdown. Keep every existing
   UseEstimateRecordsTheEngineersAcceptance request, version, lease,
   no-lease-claim and redirected-message assertion. Do not change the
   production ForProjection invariant or add a second totals formula.
3. Freeze exactly those two files for root's incremental Integration build
   and focused test filter:
   FullyQualifiedName~AssessmentReadinessSummaryBrowserTests.NotReadyReportDraftControlsStateTheConditionAndTheShellRenders|FullyQualifiedName~AssessmentEstimateImportWebTests.UseEstimateRecordsTheEngineersAcceptance
   No broad browser/capture/SQL cohort rerun is authorized. Report actual
   results with all prior failures, then normal commit/push to the same
   PR700 only after root approval; fresh gates to Review and independent
   F-004 delta review. Do not self-review or merge.

### Governing contract and ownership

FRD-11 Readiness owns the metadata-only snapshot and existing named Core
readiness; accepted estimate figures use their stored breakdown. This batch
updates the two missed test consumers to those unchanged required contracts,
not the other way round. F-001/F-002/F-003 stay fixed; F-005 manual multi-width
visual remains INCONCLUSIVE and required in Verifying before Done.

Root's bilateral scratch handoff releases only the Principal paragraph in
docs/design/README.md and generated docs/design/test-ui/index.html to PLAT-050.
Preserve ENG-029's committed Case design change through normal integration.
docs/index.md was never owned by this ticket; root corrected that shorthand.
No whole-ticket claim release or visual waiver. Source-manifest and unrelated
intake/browser/SQL CI failures remain separate existing-owner work.

## Current stop condition

Stop with the two mapped fixture corrections source-frozen and exact filter
sent to root. No author build/test, push/PR update, merge, deployment or Done
before the next explicitly authorized verification/publication handoff.
