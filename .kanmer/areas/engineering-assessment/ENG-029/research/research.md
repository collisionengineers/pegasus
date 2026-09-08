# Research — ENG-029 (2026-09-02, gpt-5.6-terra high, wrapper-checked)

## Wrapper check (Claude, 2026-09-02)

Codex ran read-only in `.worktrees/research` at `897db953` (= `origin/dev`);
the checkout was clean afterwards (`git status --porcelain` empty). The
following VERIFIED claims were independently re-run by the wrapper in the
same checkout and all confirmed:

- `36655f26` (ENG-025) is the commit that removed the Razor `ISaveAssessment`
  caller; its parent carried `OnPostSaveDamageAsync` at
  `Pages/Cases/Assessment/Index.cshtml.cs:200` and the commit itself has zero
  `ISaveAssessment` references in that file.
- `CK_CaseAssessmentFields_FieldPath` in `PegasusDbContextModelSnapshot.cs`
  (line 1150) enumerates exactly the 34 `AssessmentVocabulary` paths; none of
  the D41 additions (excess, betterment, claimant VAT, reserve, hire, storage
  per day, diminution, delays, salvage logistics) exist.
- `AssessmentPolicy.cs:159-161` permits writes in `NotReady`, `Review` and
  `ReportPreparation`; `EfCaseAssessmentStore.cs:260` clears the lease after
  a save.
- `AssessmentReportRendering.cs:160` holds the hard-coded `AcceptedEngineers`
  tuple that D31 supersedes; `rg -i 'sign-off|signoff' src tests` finds no
  sign-off account or Case field on `origin/dev`.
- `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs`
  exists; `CaseMutationPageModel.cs` carries `ExecuteCaseCommandAsync`
  (323) and `HandleLeaseFailure` (501); `OperatorLabels.CaseWorkspace` is at
  `OperatorLabels.cs:1297`.
- The sibling `files/files.md` documents cited under "Must not touch" exist
  on the board for ENG-034, ENG-035, PLAT-068, CASE-040, ENG-031 and
  CASE-029 and name the paths Codex attributes to them (ENG-034 creates the
  `_CaseSettlement`/`_CaseReport` read-only shells and lists
  `Details.cshtml(.cs)` under the CASE-038 boundary).

Nothing was dropped. One wrapper note: Codex's "Research basis" cites
`get_item`/`get_ticket_doc` calls — these resolve to the board files on
disk and were confirmed there.

## Research basis

- **VERIFIED** — `Get-Content -Raw CLAUDE.md` — this detached checkout
  requires Core-owned policy, one Case edit lease, labels in
  `Presentation/OperatorLabels.cs`, and no new migration unless required.

- **VERIFIED** — `git status --short; git diff --exit-code; git rev-parse
  --verify HEAD; dotnet --list-sdks` — checkout is clean at
  `897db9530a45063e8f684f2800685afbfdced006`; installed SDKs are
  `10.0.204` and `10.0.303`.

- **VERIFIED** — `git log --all --oneline -- src/Pegasus.Web/Pages/Cases/
  Assessment` and `git log --all -S'ISaveAssessment' -- .../Assessment` —
  ENG-025 commit `36655f26` removed the Assessment-page `ISaveAssessment`
  caller; ENG-028 subsequently added the named estimate editor.

- **ASSUMED** — the supplied ticket, EPIC-011/012 decisions, and sibling-lane
  ownership are the authoritative intended scope. Current-code claims below
  are separately verified.

## Current behaviour

### Core port and persistence

- **VERIFIED** — `Get-Content -Raw src/Pegasus.Core/Assessment/
  AssessmentContracts.cs` — `ISaveAssessment.ExecuteAsync` accepts a
  `SaveAssessmentRequest` containing Case ID, expected version, actor,
  operation key, reason, edit-lease token, and a path/value field map.

- **VERIFIED** — `Get-Content -Raw src/Pegasus.Core/Assessment/
  AssessmentPolicy.cs` — unknown and Case-owned paths fail closed; supplied
  scalar fields merge with persisted values; professional findings require a
  staff Engineer; a successful save clears the Case lease.

- **VERIFIED** — `Get-Content -Raw src/Pegasus.Infrastructure/Persistence/
  EfCaseAssessmentStore.cs` — the adapter uses a serializable transaction,
  expected-version and edit-lease guards, operation-key replay protection,
  `AssessmentFieldWriter`, and permanent before/after action history.

- **VERIFIED** — `rg -n -C 3 'ISaveAssessment|SaveAssessment' src tests` —
  `ISaveAssessment` is registered in Infrastructure and is currently called
  by the MCP assessment tool, integration tests, and Core; there is no Razor
  Pages caller.

- **VERIFIED** — `git show 36655f26^:src/Pegasus.Web/Pages/Cases/
  Assessment/Index.cshtml.cs | Select-String 'OnPostSaveDamageAsync'
  -Context 0,100` — the removed Razor caller used the existing Case version,
  one edit-lease token, `SaveAssessmentRequest`, `HandleLeaseFailure`, and
  PRG back to the Assessment route. Reuse this handler shape, not a second
  mutation path.

### Vocabulary and migration boundary

- **VERIFIED** — `Get-Content -Raw src/Pegasus.Core/Assessment/
  AssessmentContracts.cs` — fields ENG-029 can bind after the route move are:

  - `assessment.outcome`, `assessment.category`, and
    `assessment.salvage_value`;
  - `narrative.engineers_comments` and `narrative.history_check`;
  - `fee.agreed_fee` and `fee.description_lines`.

- **VERIFIED** — the same command shows legacy `costs.recovery_charge` and
  `costs.storage_charge`, but no D41 paths for excess, betterment, claimant
  VAT, reserve, repair duration/delays, report delay, storage per day, hire,
  diminution, or salvage logistics. Existing estimate-line betterment is not
  the D41 settlement betterment field.

- **VERIFIED** — `rg -n 'CK_CaseAssessmentFields_FieldPath'
  src/Pegasus.Infrastructure/Persistence/Migrations/
  PegasusDbContextModelSnapshot.cs` and `Get-Content -Raw
  src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs` —
  persisted assessment paths are constrained by a SQL check generated from
  `AssessmentVocabulary`; the D41 additions require ENG-035's vocabulary
  change and serialized migration.

- **VERIFIED** — `git show --name-only --format='' e180d61e | rg
  'Migration|Assessment'` — migration
  `20260803205759_SendToAiAssessmentToolset.cs` created the current
  assessment-field storage. ENG-029 must not create or edit a migration.

### Case page and edit convention

- **VERIFIED** — `rg -n 'OnPost|EditMode|Lease|ExpectedVersion|Save'
  src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` and `Get-Content
  src/Pegasus.Web/Pages/Cases/Details.cshtml.cs | Select-Object -Skip 340
  -First 90` — the Case page owns one edit mode and one lease; its `OnPostSave`
  carries expected version, operation key, reason, and edit-lease token.

- **VERIFIED** — `Get-Content -Raw src/Pegasus.Web/Pages/Cases/
  CaseMutationPageModel.cs` — `ExecuteCaseCommandAsync`, `HandleLeaseFailure`,
  `ClearLeaseState`, and `NewOperationKey` are the existing conventions for a
  Case-page assessment save.

- **VERIFIED** — `Get-ChildItem src/Pegasus.Web/Pages/Cases/Shared -File` —
  `_CaseSettlement.cshtml` and `_CaseReport.cshtml` do not exist on
  `origin/dev`; ENG-034 creates their shells before ENG-029 owns their bodies.

- **VERIFIED** — `Get-Content -Raw src/Pegasus.Web/Pages/Cases/Shared/
  _CaseVehicle.cshtml` — shared Case partials use `DetailsModel`, panel/grid
  primitives, named forms, the Case lease fields, and `OperatorLabels`.

- **VERIFIED** — `Get-Content -Raw src/Pegasus.Core/Assessment/
  AssessmentPolicy.cs` — current write policy permits `NotReady`, `Review`,
  and `ReportPreparation`; this does not yet match D30's Case-page
  read-only-at-Complete rule and must not be silently treated as equivalent.

### Current report draft and preview

- **VERIFIED** — `Get-Content -Raw src/Pegasus.Core/Reports/
  AssessmentReportProjection.cs` — the projection already consumes outcome,
  category, salvage, comments, history check, fee fields, Current estimate
  totals, and the Engineer's value.

- **VERIFIED** — the same command shows expanded D41 fields do not flow into
  `AssessmentReportSnapshot`; ENG-035 owns the necessary Core, projection,
  renderer, template, and migration changes.

- **VERIFIED** — `rg -n 'OnPostGenerateReportDraft|OnGetPreviewReportDraft'
  src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` — Generate and
  Preview currently live on the old Assessment route, not on the Case page.

- **VERIFIED** — `Get-Content -Raw tests/Pegasus.IntegrationTests/Reports/
  AssessmentReportDraftWebTests.cs` — existing web coverage proves PDF
  generation, disabled generation when named readiness fails, and the old
  Assessment-route handler.

### Sign-off Engineer and labels

- **VERIFIED** — `rg -n -i 'sign-off|signoff' src tests` — no D31 sign-off
  account or Case field exists on `origin/dev`.

- **VERIFIED** — `rg -n -C 3 'EngineerOption|IStaffAccountQueries'
  src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` — the current Case page can
  list enabled Engineer-role accounts, but it cannot identify flagged
  sign-off Engineers, qualifications, or signature images.

- **VERIFIED** — `rg -n -C 3 'Assessment|Settlement|Report|Outcome|Salvage'
  src/Pegasus.Web/Presentation/OperatorLabels.cs` — there is no settlement or
  report-editor label group. Existing `CaseWorkspace` entries are Vehicle,
  Inspection, and Files vocabulary.

- **ASSUMED** — after ENG-034's label hand-off, ENG-029 needs one
  `OperatorLabels.CaseWorkspace` settlement/report set: section headings,
  Outcome, Category, Salvage value, Excess, Betterment, Claimant VAT
  registered, Reserve, Repair duration, Repair delays, Report delay, Storage
  per day, Recovery, Hire start, Daily hire cost, Diminution, Salvage
  logistics, Engineer's comments, Vehicle history check, Signing Engineer,
  Not chosen, Agreed fee, Fee description, Readiness, Generate report draft,
  and Preview report draft. Derived values require labels, not persisted
  label copies.

## Mockup

- **VERIFIED** — `Get-Content C:\Users\PC\Downloads\Pegasus_UI_v2_src\src\
  22-case-engineer.js | Select-Object -Skip 70 -First 65` — Settlement has
  four outcome buttons, conditional category/salvage controls, D41 fields,
  salvage logistics, and read-only derived figures.

- **VERIFIED** — `Get-Content C:\Users\PC\Downloads\Pegasus_UI_v2_src\src\
  05-state.js | Select-Object -Skip 118 -First 38` — repair cost comes from
  the Current estimate; equity is Engineer's value minus net repair and
  salvage; ratio lines are financial ratios, not readiness percentages.

- **VERIFIED** — the same mockup command shows named readiness items:
  Current estimate, its labour-rate card, Engineer's value, Outcome,
  total-loss category and salvage value, Signing Engineer, Close-up and
  Overview images, and Engineer's comments.

- **VERIFIED** — `rg -n -C 4 'SECTIONS.report|signers|readinessItems'
  C:\Users\PC\Downloads\Pegasus_UI_v2_src\src\22-case-engineer.js` — Report
  displays comments, history check, signing Engineer, fee and description,
  named readiness, Generate, Preview, and a separate fee-note preview.

- **VERIFIED** — `Test-Path C:\Users\PC\Downloads\Pegasus_UI_v2_src\src\
  04-fixtures.js` and `rg -n -C 2 'settlement:|report:|signs:'
  ...\04-fixtures.js` — the source exists and has the supplied settlement,
  report, and staff-signature shapes.

## Gaps

- **VERIFIED** — current Razor has no `ISaveAssessment` caller; ENG-029 must
  restore a real Case-page caller after ENG-034 moves the sections.

- **VERIFIED** — only outcome/category/salvage, comments/history, and fee
  fields can bind today; all remaining D41 fields wait for ENG-035.

- **VERIFIED** — Current estimate and Engineer's value can drive derived
  repair-cost, equity, and ratio display after ENG-035 supplies betterment
  and settlement projection data. The browser must not calculate report
  figures independently of Core.

- **VERIFIED** — current report readiness uses
  `AssessmentPolicy.EvaluatePostReviewReadiness`, reports named items rather
  than a percentage, but still requires the old Engineer name,
  qualifications, and signature fields. D31's Case sign-off tuple requires
  PLAT-068, CASE-040, and DOCS-017.

- **VERIFIED** — report-image readiness cannot be completed by ENG-029:
  current projection offers confirmed images in occurrence order, while
  ENG-031 owns Close-up/Overview selection and curation.

- **ASSUMED** — Fee-note preview remains excluded even though ENG-029 edits
  fee fields: DOCS-018 owns the preview action under D42.

## Existing helpers and conventions to reuse

- **VERIFIED** — `AssessmentVocabulary`, `AssessmentPolicy`, `SaveAssessment`,
  `EfCaseAssessmentStore`, and `AssessmentFieldWriter` are the existing
  assessment-write path.

- **VERIFIED** — `EstimateTotals.Compute` and
  `AssessmentReportProjection.CostsOf` are the existing owners of Current
  estimate totals and report repair costs.

- **VERIFIED** — `AssessmentReportProjection.Prepare` is the existing named
  readiness source for Generate/Preview gating.

- **VERIFIED** — `CaseMutationPageModel.NewOperationKey`,
  `ExecuteCaseCommandAsync`, `HandleLeaseFailure`, and `ClearLeaseState`
  provide the Case-page PRG, lease-loss, and stale-version behaviour.

- **VERIFIED** — `OperatorLabels` is the sole operator-language owner;
  `CaseWorkspace` is the applicable nested label convention.

- **VERIFIED** — `tests/Pegasus.IntegrationTests/
  AssessmentPersistenceIntegrationTests.cs` already proves assessment replay,
  lease enforcement, stale-version refusal, provenance, and history.

- **VERIFIED** — `scripts/Update-TestUiSnapshots.ps1`,
  `scripts/Test-UiCatalogue.ps1`, and `docs/engineering.md` establish the
  snapshot process; `Test-UiCatalogue.ps1` requires every routed Razor page
  to have a valid classification. UIIMP-014 owns the new Case-record states.

## Risks

- **VERIFIED** — `ENG-034` research/files declares
  `Details.cshtml.cs` as the CASE-038 handler-host boundary, while ENG-029
  needs Case-page assessment handlers. Whole-file ownership must be handed
  off before implementation; ENG-029 must not independently modify a
  CASE-038 or CASE-040 version of that file.

- **VERIFIED** — `AssessmentVocabulary` is also a SQL check constraint;
  rendering a D41 control before ENG-035 lands would create an inert or
  fail-closed control.

- **VERIFIED** — every successful assessment save clears the shared Case
  lease. Section forms must reload and reacquire normally; they must not
  preserve a stale token or discard an unrelated concurrent save.

- **VERIFIED** — D31 supersedes the current hard-coded
  `AcceptedEngineers` tuple in `AssessmentReportRendering.cs`; ENG-029 must
  consume the dependent sign-off source rather than recreate a local list.

- **VERIFIED** — `docs/design/README.md` prohibits explanatory field copy and
  requires only relevant populated read-only sections. Mockup hints and empty
  panels must not be ported.

## Open questions for the operator

none

## Current v1 caller audit — 2026-09-08

This section supersedes the historical implementation premises above, not the
record of earlier research/review. Root requested preparation only at accepted
dev 498144b0bb55b68fd53b9a31ffc89ef90622c73a. No source edit, claim, branch,
build, test or live operation was performed. Declared reference sources are
empty; the supplied pack and current repository are sufficient evidence.

### Authority and actual remaining scope

Read current FRD-06 Settlement/canonical specifications, FRD-11 assessment
and report behavior, docs/design/README.md Case workspace, EPIC-011/012
decisions, EPIC-014 constraints and the complete
pegasus_pack/current/preparing-audit-next-10.md. The supplied historical
ui/in-progress-ui-work/Pegasus_UI_v2_src/src/22-case-engineer.js contains
Settlement/Report examples; its per-section saves, second repair-duration
value, Vehicle History in Report and explanatory copy are superseded by the
current canonical design and root's current decisions.

The remaining change is a real Case-page writer, not vocabulary/schema or
section-host construction. The current ticket body still names ISaveAssessment
and puts Vehicle History in Report; those promises must be synchronized to
the current plan before execution. Root decided: repair days belong to the
current accepted estimate; include current Report content switches/date
override/narrative/fee/sign-off controls; Vehicle History is edited in Vehicle
only; fix the PostReport state mismatch without widening other states.

### Production ownership and caller evidence

- Details.cshtml.cs constructor and OnPostSaveAsync (around68,938) use ISaveCase.
  There is no Web/MCP ISaveCaseWorkspace caller. The existing registered
  SaveCaseWorkspace/Core contract and EfCaseWorkspaceStore already implement
  one serializable mutation, expected version, lease, request-hash replay,
  one history event/version advance, accepted facts and assessment fields,
  CaseMatchIndex refresh and same-transaction current-report invalidation.
- _CaseWorkflow.cshtml:159 owns case-edit-form. Sticky Save in Details.cshtml
  already targets it. _CaseInspectionAddress uses native form association.
  site.js:570 onward resolves control.form for dirty/discard/Ctrl+S: no new
  JavaScript form coordinator is needed.
- _CaseSettlement is six read-only values, including old lump storage charge
  and Repairer VAT. Neither is the new Storage per day/Claimant VAT field.
  _CaseReport has generation/delivery/image preparation and read-only scalar
  values, but no writer for required choices. _CaseVehicle lacks the current
  design's Vehicle History writer.
- AssessmentVocabulary.Definitions already owns field paths, types, bounds,
  flags/codes and finding authority. The 23-region damage map is a different
  concept, not an editor field list. Outcome/category/salvage remain Engineer
  findings; ordinary report/settlement inputs do not confer Engineer rights.
  The full tracked src/tests/docs census of SettlementRepairDuration and
  settlement.repair_duration has only the constant and Definitions entry
  (AssessmentContracts.cs:109,285); there is no current consumer. Remove the
  dead writable entry rather than leave an input that cannot affect output.
- CaseWorkspace.cs has replace-all typed sections. Null section means
  untouched; null member in a submitted section clears that fact. Storage/day
  and recovery belong to Inspection.StoragePerDay/RecoveryCharge, not a free
  assessment dictionary; ReportDate and SignOffEngineerId likewise have typed
  members. A small partially populated record would erase unshown claim-source,
  storage-business, inspection provenance/notes, odometer or date facts.
  Preserve current accepted members in the existing mapper and keep the
  submitted expectedVersion; never silently rebase the save to a fresh version.
- CaseField.Current may include an unaccepted suggestion. Existing Vehicle
  display correctly uses Confirmed then Fact. Hidden/readback values must
  follow accepted precedence and retain explicit clearing; do not promote a
  suggestion or submit the display placeholder as real data.
- AssessmentAccessPolicy.IsReadOnly allows engineering edits only in
  ReportPreparation and PostReport. AssessmentPolicy.IsWritableState omits
  PostReport and is used by EfCaseAssessmentStore, EfCaseWorkspaceStore and
  EfValuationStore. Add the missing supported state only; retain ordinary Case
  fact edits in NotReady/Review and the existing terminal/archive refusals.
  New engineering form submissions must still use the stricter Core access
  rule, not treat the broad ordinary-data save gate as engineering permission.
- Details currently calls AssessmentReportProjection.Prepare without a
  signatory, so the page's named readiness can falsely report a missing signer.
  ICaseReportSnapshotSource.GetAsync uses existing
  EfAssessmentReportProjectionSource.LoadAsync(includeImageContent:false):
  it obtains version-bound persisted assessment/accepted estimate/valuation,
  eligible profiles and confirmed image/preparation metadata without opening
  image content. Reuse CaseReportReadiness.Evaluate on that input; do not use
  the preview interface on a page GET, since that loads image bytes.
- AssessmentReportProjection.BuildSettlement:285 already owns equity
  (Engineer value minus repair total less betterment, minus salvage) and reads
  RepairDays from CurrentEstimate.Details. Reuse that calculation for the
  editor's derived figures through a small pure entry point in the same owner,
  returning no derived figure when accepted inputs are absent. Do not copy
  formulas into Razor/JavaScript, introduce a second settlement record, or add
  optional ratio work. Report generation/preview continue to own rendering.
- CaseMutationPageModel retains proposed form values through the existing
  bounded conflict panel, but its scalar allowlist cannot retain the new
  canonical assessment keys; it drops blank values and GUID-valued sign-off
  selections. Extend that existing route-specific presentation retention
  coherently, preserving clear/false intent and safe displayed signer names.
  Preserve explicit existing shortened/dropped signals and exclude authority
  tokens/versions/operation keys. Do not replace it with another conflict store.

### Reuse and focused proof seams

Existing CaseDetailsWebTests recording store and partial CaseEditModeWebTests
cover actual antiforgery/lease/form navigation. Update the recording
ISaveCase registration/SaveCaseRequest assertions to the actual workspace
command, preserving every earlier claimant/address/inspection assertion.
Other direct ISaveCase consumers (AssessmentMcpTools and existing address
tests) remain legitimate separate callers; do not delete their port/store.

CaseWorkspacePersistenceTests reuses the existing
CaseDataCompletenessPersistenceTests.CaseDataHarness: extend it for combined
accepted Overview/Settlement/Report save, current-report stale invalidation,
one version/history/replay and complete rollback on refusal. Existing
AssessmentPolicyTests, CaseWorkspaceTests and AssessmentReportProjectionTests
already own normalization/finding/derived-value evidence. Existing
Reports/AssessmentReportDraftWebTests is the real upstream preview caller with
only the renderer replaced; reuse its accepted fixture for page-readiness and
saved-input-to-preview parity. Root alone runs bounded checks/captures.

### Prerequisites versus historical edges

Live feature gates currently find research/files/plan/checklist and no open
questions; they do not establish that the old documents are accurate.
ENG-035 vocabulary and PLAT-068 sign-off are Done; ENG-034 section hosting is
integrated but historically Verifying/taken. CASE-047 and CASE-040 also retain
foreign historical claims. Do not transfer, release, rewrite or absorb them.

TICK-085 actively owns Details.cshtml.cs, CaseWorkspaceLabels.cs, FRD-06 and
case-details snapshots/index; its actual current file map, not a context-only
reference, is an execution blocker. Preparation does not take those files.
Wait for root-approved plan and explicit current/historical ownership clearance,
fresh source/file census and an isolated ready execution packet. ENG-031 crop
UI and ENG-036 diagram remain independent tickets; preserve their existing
components and future one-save seam, do not implement their work here.

### Risks, exclusions and evidence limits

No credentials are required for this local writer. No new dependency, schema,
store, API, renderer, AI, image preparation, provider/estimate import or release
work is justified. No model-generated domain content is acceptable. Existing
supplied fixtures/accepted test estate are reused; structural probes are
identified as probes, not genuine instructions. All findings above are source
inspection, not new runtime PASS evidence.
