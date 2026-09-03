# Plan — ENG-034 (2026-09-02, gpt-5.6-terra xhigh)

## Wrapper check (Claude, 2026-09-02)

Codex ran read-only in the shared detached checkout `.worktrees/research`
at `origin/dev` = `897db953` (three DELIV-041 docs-only commits after the
`cad00be9d` the research was written at; no code changed). `git status
--porcelain` was empty afterwards. Spot-checked with my own commands in the
same checkout, all confirmed:

- Every vocabulary path step 2 reuses exists in
  `src/Pegasus.Core/Assessment/AssessmentContracts.cs` (`ImpactLocation`,
  `ImpactSeverity`, `NatureOfIncident`, `Outcome`, `SalvageCategory`,
  `SalvageValue`, `CostRecoveryCharge`, `CostStorageCharge`,
  `CostRepairerVatRegistered`, `EngineersComments`, `HistoryCheck`,
  `EngineerName`, `EngineerQualifications`, `EngineerSignature`,
  `AgreedFee`, `FeeDescriptionLines`, `StatementOfTruth`).
- `OperatorLabels.CaseStage` (line 134/261), `RepairSpecificationRoute`
  (378) and `EstimateLineType` (396) exist; there is no Engineer-section
  label group yet.
- The `Assessment/Index.cshtml` line ranges in step 2 are right: 194–218
  Engineer action block, 219–224 the disabled Glass's/Audatex buttons,
  225–241 Send to Claude entry, 243–267 report-draft controls, 346–552 the
  `assessment-v3-main` Estimates pane, 556–602 import dialog, 604–635
  discard dialog, 637–683 Send to Claude dialog, 688–742 `@functions`.
- `OnGetPreviewReportDraftAsync` returns `File(pdf, "application/pdf")` or
  `RedirectToPage` on `NotReady` (`Index.cshtml.cs` 579–603).
- `AssessmentWorkspaceTestData.Create` and `FakeGetAssessmentAccess`
  (`AssessmentWorkspaceTestData.cs` 11/97), `IntakeWebApplicationFactory`
  (`IntakeWebTestSupport.cs`), `RecordingStores`
  (`AssessmentEstimateImportWebTests.cs`) and `AudatexEstimateFixture`
  exist. Assessment-route callers per test file: `AssessmentCopyWebTests`
  4, `AssessmentEstimateImportWebTests` 33, `AssessmentVehiclePrefillWebTests`
  2, `Browser/AssessmentReadinessSummaryBrowserTests` 1,
  `Reports/AssessmentReportDraftWebTests` 8, `SendToAiIntegrationTests` 9;
  the remaining two (`CaseDetailsWebTests` 1, `OperationsWebTests` 1) are
  CASE-038's and the Operations lane's.
- Every governing-doc heading cited below exists after DELIV-041
  (`frd-12` § Case workspace / § Assessment; `frd-06` § Damage record /
  § Valuation sources / § Settlement; `frd-11` § Report-draft entry point;
  design README § Voice, § No explanatory copy, § Absent versus disabled,
  § Case workspace, § Assessment; `engineering.md` § One Core owner,
  § Test support, § Case Workspace v2 fixture values (D43), § Plan sizing).
  The design README (line 755) already records the 301 as "delivered with
  the sections move (D30, ENG-034)".

Two wrapper corrections, both folded into the Decision record below:

1. **Partial composition order.** Option A has CASE-038's `Details.cshtml`
   render `<partial name="Cases/Shared/_CaseDamage" model="Model" />` (and
   the other three) — but those files are ENG-034's `create` rows, and a
   Razor `<partial>` whose file is missing fails at render time, so the
   CASE-038 PR could not merge green ahead of ENG-034. Contract item 6
   settles it: CASE-038 ships the four files as heading-only section shells
   (the same "reserve the container" shape its research already adopts for
   `section-valuation`), and ENG-034 then owns and replaces them. The Files
   document's `create` rows for the four partials therefore become `change`
   once CASE-038 merges; nothing else in Files moves.
2. **Exact `Location`.** The redirect stub builds its target as a literal
   string (`Pages/Triage/Index.cshtml.cs` pattern), so the test asserts
   exactly `/Cases/{id}?section=estimate` — no trailing slash, no
   framework-generated form. Step 5 is worded that way.

Board dependencies at planning time: CASE-038 and ENG-035 are both in
`preparing`; this plan's step 1 waits for both to clear. CASE-038's own
plan (not yet written) must honour the seven contract items below.

## Decision record

Starting point: read-only `origin/dev` at `897db953` (DELIV-041). The
governing record is now in:

- `docs/frd/frd-12-operator-experience.md` — [Case workspace] and
  [Assessment].
- `docs/frd/frd-06-vehicle-and-engineering-evidence.md` — [Damage record],
  [Valuation sources], and [Settlement].
- `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` —
  [Report-draft entry point].
- `docs/design/README.md` — [Voice, labels and necessary copy], [No
  explanatory copy and page economy], [Absent versus disabled], [Case
  workspace `/Cases/{id}`], and [Assessment `/Cases/{id}/Assessment`].
- `docs/engineering.md` — [One Core owner], [Test support], [Case Workspace
  v2 fixture values (D43)], and [Plan sizing].

### Handler-host decision

Adopt option A: [[CASE-038]] hosts the moved Assessment handler surface in
`DetailsModel`; ENG-034 only removes the originals when it retires the
Assessment page.

| Option | Decision and reason | Cost |
| --- | --- | --- |
| A — CASE-038 hosts | Adopted. `Details.cshtml.cs` is CASE-038's whole-file lease and CASE-038 already owns the Case frame and single edit lease. | A temporary duplication window exists until ENG-034 removes the old handlers; CASE-038 must retarget its handler tests before ENG-034 can retire the route. |
| B — ENG-034 moves after CASE-038 | Rejected. It would require ENG-034 to take CASE-038's file after it merges, extending the capacity-one lease and making the planned dependency a second implementation task. | Delays retirement and creates an avoidable second ownership hand-off. |
| C — retain Assessment POST handlers | Rejected. Forms would continue posting to a route whose GET is retired, leaving a second handler path and contradicting the ticket's “retire” wording. | Avoids a move initially, but preserves obsolete routing and splits the one Case lease. |

[[CASE-038]] is a hard dependency. Before ENG-034 starts, its merged contract
must provide all seven of the following:

1. The D30 Case frame, including `section-damage`, `section-valuation`,
   `section-estimate`, `section-settlement`, and `section-report`, in order,
   with all sections viewable and no “Open Assessment” action or
   `CanOpenAssessment` visibility gate.
2. Composition of ENG-034's four partials with `model="Model"`. `DetailsModel`
   exposes the Case id, assessment projection, estimates and selected editor
   state, actor role, operation keys, `LeaseToken`, and an
   `AssessmentIsReadOnly` value derived directly from
   `AssessmentAccessPolicy.IsReadOnly`.
3. The Case-page implementations of lease claim, heartbeat, and release;
   `GenerateReportDraft`, `PreviewReportDraft` (GET), `SendToClaude`,
   `SaveEstimate`, `EditLine`, `DuplicateEstimate`, `DiscardEstimate`,
   `SetCurrentEstimate`, and `ImportEstimate`. Mutating results redirect to
   `/Cases/{id}?section=estimate`, preserving `estimate` or `dialog` state
   where the existing flow requires it.
4. `OnGetPreviewReportDraftAsync` on `DetailsModel`; `_CaseReport` links to
   `/Cases/{id}?handler=PreviewReportDraft` and keeps its PDF response and
   new-window behaviour.
5. One Case edit lease across lazy fragments. Lazy mounting must preserve an
   unsaved estimate form and rebind the existing dirty-form and heartbeat
   behaviour.
6. The four section files `_CaseDamage.cshtml`, `_CaseEstimate.cshtml`,
   `_CaseSettlement.cshtml` and `_CaseReport.cshtml` exist as heading-only
   shells composed by `Details.cshtml`, so the frame PR renders green
   before ENG-034 starts; ENG-034 then owns and replaces their content
   (wrapper correction 1).
7. An addressed `?section=damage|estimate|settlement|report` renders that
   section server-side on the first GET (CASE-038 research correction 3),
   so ENG-034's integration tests need no script to see the section.

ENG-034 renders exactly four new partials: Damage, Estimate, Settlement, and
Report. It does not create `_CaseValuation.cshtml`. `section-valuation` is
CASE-038's container and [[CASE-029]] supplies its content.

[[ENG-035]] remains a board dependency, so implementation waits for its
dependency to clear. Technically, its new structured damage, valuation, and
settlement vocabulary is not required for this bounded move: the shells use
only existing `AssessmentVocabulary` scalar fields. If ENG-035 has not merged,
do not create Core, persistence, or migration work as a substitute; wait for
the board dependency. After it merges, reconcile only additive projection
changes needed to compile; do not render its deferred fields.

The mockup is a shape reference only. Reuse existing generic `panel`,
`panel-head`, `panel-body`, `detail-list`, `case-overview-grid`,
`estimate-*`, and `dropzone` classes. CASE-038 owns `wwwroot/css/site.css`,
the `section-*` frame, lazy-fragment CSS, and any new Damage or Settlement
layout styles. Do not copy fixture values from `04-fixtures.js`; D43 sign-off
for their use is not available.

## Steps

1. Confirm the merged [[CASE-038]] host contract, its handler names and Case
   redirects, then confirm [[ENG-035]] has cleared its board dependency.
   Acquire the capacity-one leases for `OperatorLabels.cs`,
   `Pages/Cases/Assessment/**`, and the one catalogue entry. Reuse
   `DetailsModel`, `CaseMutationPageModel`, and the existing
   `_EditHeartbeat` plumbing; no new host, lease, or handler path is created.
   Stop rather than modifying CASE-038, CASE-029, ENG-035, CSS, JS, or Core
   files.

2. Add the ENG-034-owned `OperatorLabels.CaseWorkspace.EngineerSections`
   group and fill the four Case partials (replacing CASE-038's heading-only
   shells), each strongly typed as `Pegasus.Web.Pages.Cases.DetailsModel`.

   Files: `src/Pegasus.Web/Presentation/OperatorLabels.cs`,
   `src/Pegasus.Web/Pages/Cases/Shared/_CaseDamage.cshtml`,
   `src/Pegasus.Web/Pages/Cases/Shared/_CaseEstimate.cshtml`,
   `src/Pegasus.Web/Pages/Cases/Shared/_CaseSettlement.cshtml`, and
   `src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml`.

   The new label keys are:

   - `Damage`, `ImpactLocation`, `ImpactSeverity`, `IncidentNarrative`,
     `NotRecorded`.
   - `Estimate`, `Estimates`, `NoEstimatesRecorded`, `NewEstimate`,
     `ImportEstimate`, `SendToClaude`.
   - `Settlement`, `Outcome`, `SalvageCategory`, `SalvageValue`,
     `RecoveryCharge`, `StorageCharge`, `RepairerVatRegistered`.
   - `Report`, `EngineersComments`, `HistoryCheck`, `Signatory`,
     `Qualifications`, `Signature`, `AgreedFee`, `FeeDescription`,
     `StatementOfTruth`, `GenerateReportDraft`, and `PreviewReportDraft`.

   Reuse `OperatorLabels.RepairSpecificationRoute`,
   `OperatorLabels.EstimateLineType`, and `OperatorLabels.CaseStage`; do not
   introduce another state-label map. Reuse these existing scalar vocabulary
   paths rather than defining fields:

   - Damage: `ImpactLocation`, `ImpactSeverity`, `NatureOfIncident`.
   - Settlement: `Outcome`, `SalvageCategory`, `SalvageValue`,
     `CostRecoveryCharge`, `CostStorageCharge`, and
     `CostRepairerVatRegistered`.
   - Report: `EngineersComments`, `HistoryCheck`, `EngineerName`,
     `EngineerQualifications`, `EngineerSignature`, `AgreedFee`,
     `FeeDescriptionLines`, and `StatementOfTruth`.

   Copy the existing `_CaseVehicle.cshtml` / `_CaseSummary.cshtml` local
   `Value(...)` display convention, using the central `NotRecorded` label
   whenever a scalar is absent. Damage and Settlement are display-only
   `<dl>` shells; they do not add the damage map, settlement editors,
   derived-equity figures, tyres, belts, or salvage logistics.

   Move the Estimate markup without changing its behaviour:

   | Assessment source range | Destination |
   | --- | --- |
   | 194–218 and 225–241 | Estimate action controls, import entry, and Send to Claude entry |
   | 346–552 | Estimate tabs, selected editor, lines, totals, and read-only display |
   | 556–602 | Import dialog and `data-dropzone` form |
   | 604–635 | Discard-estimate dialog |
   | 637–683 | Send to Claude dialog |
   | 688–742 | `EstimateTotals` and specification-line render helpers |

   Lines 219–224 are not moved: direct Glass's and Audatex launch controls
   are absent under D21. Lines 243–267 move to `_CaseReport`; the old
   record/evidence wrappers do not move. `_CaseEstimate` reuses
   `EstimateTotals.Compute`, `EstimatePolicy`, existing operation keys, the
   selected labour-rate-card state, and CASE-038's Case handler host. It does
   not duplicate totals, import parsing, rate selection, or AI-job creation.
   The whole-page D16 drop must continue to submit the existing
   `ImportEstimate` command through the CASE-038 host.

   `_CaseReport` reuses `AssessmentReportProjection.Prepare` and the moved
   Generate/Preview entry points only. It does not add image curation,
   fee-note preview, Report editors, or a sign-off tuple editor.

   Every mutation control is omitted when `Model.AssessmentIsReadOnly` is
   true. A Complete render still shows sections and recorded values, but has
   no estimate mutation, import, Claude, draft-generation, or report-edit
   control. `CanOpen` is never used to decide whether a section renders.

3. Retire the Assessment route after the CASE-038 host is present.

   Files: `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml`,
   `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs`, and
   `src/Pegasus.Web/Pages/Cases/Assessment/Suggestions.cshtml`.

   Keep the existing route declaration but reduce the page model to the
   authorised redirect-stub shape used by
   `Pages/Triage/Index.cshtml.cs` and `Pages/Unidentified/Index.cshtml.cs`.
   `OnGetAsync` returns
   `RedirectPermanent("/Cases/{id}?section=estimate")`; remove the source
   handler surface only after CASE-038 owns it. Retarget the Suggestions Back
   link to the Case Estimate section. No POST continues to target
   `/Cases/{id}/Assessment`.

4. Reclassify the retired routed Razor page in the Test UI catalogue.

   Files: `docs/design/test-ui/catalogue.json` and
   `docs/design/test-ui/pages/case-assessment--default.html`.

   Change only the `Pages/Cases/Assessment/Index.cshtml` entry from `visual`
   to `redirect`, with a D30 reason, and delete its stale snapshot. Reuse the
   existing redirect-entry shape for Triage and Unidentified. Do not edit
   UIIMP-014's Case-record snapshots or catalogue entries.

5. Retarget route and handler coverage, then add focused Case-section
   coverage.

   Files: `tests/Pegasus.IntegrationTests/AssessmentCopyWebTests.cs`,
   `tests/Pegasus.IntegrationTests/AssessmentEstimateImportWebTests.cs`,
   `tests/Pegasus.IntegrationTests/AssessmentVehiclePrefillWebTests.cs`,
   `tests/Pegasus.IntegrationTests/Browser/AssessmentReadinessSummaryBrowserTests.cs`,
   `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs`,
   `tests/Pegasus.IntegrationTests/SendToAiIntegrationTests.cs`, and
   `tests/Pegasus.IntegrationTests/CaseEngineerSectionsWebTests.cs`.

   Retarget existing GETs, forms, antiforgery reads, and assertions to the
   CASE-038 Case handler host. Preserve estimate-import, line, save,
   duplicate, discard, current-estimate, totals, report-draft, preview, and
   Send-to-Claude behaviour assertions. Add a non-following GET assertion for
   the retired route: `HttpStatusCode.MovedPermanently` and exact
   `Location` `/Cases/{id}?section=estimate` — the stub builds the target
   as a literal string, as `Pages/Triage/Index.cshtml.cs` does, so there
   is no trailing slash (wrapper correction 2).

   Reuse `AssessmentWorkspaceTestData.Create`, the shared
   `FakeGetAssessmentAccess`, `IntakeWebApplicationFactory`, each test's
   existing `FakeGetCase` / `FakeGetAssessmentWorkspace` pattern, and
   `AssessmentEstimateImportWebTests.RecordingStores` plus
   `AudatexEstimateFixture`. The new test proves all five section IDs in each
   lifecycle state and proves Complete has values but no mutation controls.
   It does not create fixture data from the mockup.

6. Run the required verification, inspect the diff for scope and simplification,
   record the implementation report, and open the PR to `dev`.

   Files: no additional owned files.

   Confirm no modified path belongs to CASE-038, CASE-029, CASE-039, ENG-035,
   ENG-029, ENG-031, ENG-036, DOCS-018, or UIIMP-014. No migration is part of
   ENG-034, so `Test-MigrationGrants.ps1` is not applicable.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

`./scripts/Test-MigrationGrants.ps1` is not run: ENG-034 has no migration.
If a migration becomes necessary, stop and hand it to the serialized migration
lane rather than adding it to this ticket.

## Acceptance conditions

- CASE-038's Case frame renders all five Engineer section IDs for every Case
  lifecycle state; ENG-034 supplies only Damage, Estimate, Settlement, and
  Report content.
- Damage, Settlement, and Report show existing scalar values or `Not recorded`;
  they do not render deferred fields, disabled placeholders, or explanatory
  copy.
- Estimate tabs, line editing, whole-page import, totals, rate-card selection,
  dialogs, Send to Claude, and all existing Core-owned commands behave as
  before; the existing totals assertions still pass.
- A Complete Case renders every Engineer section read-only, with mutation
  controls absent rather than disabled.
- `/Cases/{id}/Assessment` returns a 301 with the exact Case Estimate
  location, and no Assessment-specific POST handler remains reachable.
- Report draft generation and preview use the Case-page host; preview remains
  a GET returning the existing PDF response.
- The retired page is a `redirect` catalogue entry, its old snapshot is gone,
  and the Test UI catalogue passes.
- No mockup fixture value, Core/persistence change, CSS/JS change, valuation
  shell, damage-map work, settlement editor, report-image curation, or
  fee-note preview is included.

## Design rules that bind

- D29/D30 in FRD-12 require the single-scroll Case record, five always-viewable
  Engineer sections, the fixed section order, and the Assessment 301.
- D11 as narrowed by D30 is `AssessmentAccessPolicy.IsReadOnly` only:
  `PostReportComplete` makes controls absent; `CanOpen` cannot hide sections.
- D16 and D17 preserve the existing import and estimate/rate-card behaviour;
  no second calculation, importer, or rate-selection owner is introduced.
- `docs/design/README.md` § Voice and § No explanatory copy permit labels,
  values, and controls only; no hint, empty-state panel, or how-it-works copy.
- Operator-facing labels live only in `Presentation/OperatorLabels.cs`; exact
  Case state wording comes from `OperatorLabels.CaseStage`.
- D7 permits a disabled control only for a named, composed integration seam or
  named state condition. D21 requires excluded capabilities to be absent:
  direct Glass's and Audatex launch controls are not moved.
- `docs/engineering.md` § One Core owner prohibits Web copies of policy,
  totals, imports, report projection, or AI-job creation.
- D43 is not authorisation to copy mockup fixture values in this task.

## Stop condition

The implementation PR is open against `dev`, ENG-034 is in Review, and no
merge, release, proof, or neighbouring-ticket work has been performed.

## Simplification pass

(Written by the implementer after the diff exists: dated heading, the
four lenses, findings and dispositions.)
