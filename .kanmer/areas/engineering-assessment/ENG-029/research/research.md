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
