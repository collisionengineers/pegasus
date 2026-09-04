# Plan — ENG-034 (2026-09-02, gpt-5.6-terra xhigh; revised 2026-09-03 after cross-model review)

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
`preparing`; this plan's step 1 waits for both to clear. [[PLAT-070]] blocks
CASE-038, so it reaches `dev` before this ticket starts (review correction 3).
CASE-038's own plan (not yet written) must honour the contract items below.

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
`DetailsModel`; ENG-034 supplies the callers, retargets the handler tests and
removes the originals when it retires the Assessment page.

| Option | Decision and reason | Cost |
| --- | --- | --- |
| A — CASE-038 hosts | Adopted, subject to the open question below. `Details.cshtml.cs` is CASE-038's whole-file lease and CASE-038 already owns the Case frame and single edit lease. | Between CASE-038's merge and ENG-034's, `dev` carries two handler surfaces and CASE-038's are unreachable (no production caller until ENG-034's partials land) — see the open question. |
| B — ENG-034 leases `Details.cshtml.cs` after CASE-038 merges and moves the handlers itself | Not adopted by this plan, but recommended in the open question: it makes the move one atomic cutover with no duplicate surface and no unreachable handler. | ENG-034 joins the capacity-one queue on `Details.cshtml.cs` behind CASE-038 and CASE-039. |
| C — retain Assessment POST handlers | Rejected. Forms would continue posting to a route whose GET is retired, leaving a second handler path and contradicting the ticket's “retire” wording. | Avoids a move initially, but preserves obsolete routing and splits the one Case lease. |

**Open question (review finding 2, unresolved).** Option A merges handlers with
no production caller in CASE-038's own PR, which the repository's "Done means
wired" rule forbids; option B fixes that but moves a file the EPIC-012 whole-file
ownership rule assigns to CASE-038. Choosing between them re-scopes two tickets
and is not ENG-034's to decide — it is recorded in `open-questions/`. Everything
below is written for option A and switches to option B by moving the step-3
handler removal and the contract's items 3–4 into ENG-034 under a
`Details.cshtml.cs` lease; nothing else changes.

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
   where the existing flow requires it. ENG-034, not CASE-038, retargets the
   handler tests listed in step 5 (review finding 2).
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
8. The host carries no staff-review requirement, flag, control or wording:
   [[PLAT-070]] (D44) removes `InstructionConfirmedByStaff` /
   `ImagesConfirmedByStaff` and the "Instructions not staff-reviewed" /
   "Images not staff-reviewed" strings from `Details.cshtml.cs` before
   CASE-038 builds on it (review finding 3).

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

### Estimate import: verified current state (review finding 1)

The ticket's Approach line — "Whole-page estimate drop stays (D16)" — is a
false premise at `origin/dev`. Verified: `Assessment/Index.cshtml` 202–218
renders an **Import estimate** control (enabled or `gated` disabled), 556–602
renders an Import estimate dialog with name, source, reason and a
`data-dropzone` file input, and `wwwroot/js/site.js` 148–164 carries only a
global drop **safety net** that calls `preventDefault()` so a stray drop does
not navigate the tab. There is no whole-page import drop, and there is no
shared Core import command — `OnPostImportEstimateAsync` on the page model is
the only import path.

`docs/design/README.md` line 1158 records exactly this: "the Assessment Import
estimate dialog and its file picker — replaced by the whole-page drop; **still
shipped, removal owed by [[ENG-033]]** (D16)". ENG-033 (`backlog`, EPIC-011
wave B) owns extracting the shared Core command, delivering the whole-page drop
and deleting the dialog.

ENG-034 therefore **moves the existing control and dialog verbatim** and
neither builds nor deletes the D16 drop. Absorbing ENG-033's scope — a shared
Core command, auto-detection, provider-plus-sequence naming, replay assertions —
is forbidden by the "never absorb another ticket's scope" rule, and deleting the
control without ENG-033 would leave no staff import route at all. The design
README's Estimate-section text ("There is no Import estimate control") describes
the post-ENG-033 target and already carries the interim record above, so ENG-034
owes no governing-doc change; those belong to DELIV-041.

The mockup is a shape reference only. Reuse existing generic `panel`,
`panel-head`, `panel-body`, `detail-list`, `case-overview-grid`,
`estimate-*`, and `dropzone` classes. CASE-038 owns `wwwroot/css/site.css`,
the `section-*` frame, lazy-fragment CSS, and any new Damage or Settlement
layout styles. D43 permits the mockup's corpus-derived fixture values
(operator sign-off 2026-09-03), but ENG-034 does not need them: its tests use
the existing `AssessmentWorkspaceTestData` fakes, so no value is copied from
`04-fixtures.js`. That is a proportionality choice, not a restriction
(review finding 7).

## Steps

1. Confirm the merged [[CASE-038]] host contract, its handler names and Case
   redirects, then confirm [[ENG-035]] has cleared its board dependency and
   that [[PLAT-070]] has removed the staff-review flags and wording from the
   host. Acquire the capacity-one leases for `OperatorLabels.cs`,
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
   - `Report`, `EngineersComments`, `HistoryCheck`, `AgreedFee`,
     `FeeDescription`, `StatementOfTruth`, `GenerateReportDraft`, and
     `PreviewReportDraft`.

   **Every operator-visible and accessible-name literal inside the moved
   ranges moves with it and is routed through the same group, at its exact
   current wording** (review finding 5). The moved ranges carry, at least:
   Estimates, No estimates recorded, New estimate, Delete estimate,
   Duplicate, Use estimate, Save estimate, Add line, Estimate name, Source,
   Repair days, Labour rate, Paint labour rate, Paint h, Labour h, Paint
   materials, Other costs, VAT, VAT %, Estimate notes, Parts and operations,
   Operation, Description, Part number, Qty, Action, Notes, the import and
   discard and Send to Claude dialog titles, field labels and buttons, and the
   dialog close/cancel accessible names. Inventory them from the diff before
   opening the PR; add a member for each, reuse an existing member where one
   already carries the string, and change no wording — this is a relocation,
   not a rewrite.

   Reuse `OperatorLabels.RepairSpecificationRoute`,
   `OperatorLabels.EstimateLineType`, and `OperatorLabels.CaseStage`; do not
   introduce another state-label map. Reuse these existing scalar vocabulary
   paths rather than defining fields:

   - Damage: `ImpactLocation`, `ImpactSeverity`, `NatureOfIncident`. D45 means
     there is no damage type: no type field, label list or column appears here.
   - Settlement: `Outcome`, `SalvageCategory`, `SalvageValue`,
     `CostRecoveryCharge`, `CostStorageCharge`, and
     `CostRepairerVatRegistered`.
   - Report: `EngineersComments`, `HistoryCheck`, `AgreedFee`,
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
   The Import estimate control and dialog move verbatim and keep posting the
   existing `ImportEstimate` command through the CASE-038 host; the D16
   whole-page drop and the shared Core import command are [[ENG-033]]'s and are
   neither built nor removed here (see "Estimate import" above).

   `_CaseReport` reuses `AssessmentReportProjection.Prepare` and the moved
   Generate/Preview entry points only. It renders the Engineer's comments,
   history check, agreed fee, fee description lines and statement of truth as
   read-only values. It does **not** render the legacy
   `EngineerName` / `EngineerQualifications` / `EngineerSignature` tuple:
   D31 supersedes D18, and the sign-off tuple is [[CASE-040]]'s Case field
   (with [[DOCS-017]]'s account data), not a `_CaseReport` display of the old
   assessment scalars (review finding 4). It adds no image curation, no crop
   entry, no fee-note preview and no Report field editors. Those surfaces are
   composed into this same file later by their owners — [[ENG-031]] (report
   images and the D46 crop entry from the image cards), [[DOCS-018]] (the D42
   fee-note preview), [[ENG-029]] (Report and Settlement editors) and
   [[CASE-040]] (the sign-off tuple) — each of which ENG-034 blocks, so they
   take the `_CaseReport.cshtml` lease after this ticket merges.

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
   handler surface in the same PR that adds its Case-page callers, so `dev`
   never carries two reachable estimate handler paths. Retarget the
   Suggestions Back link to the Case Estimate section (`Suggestions.cshtml`
   carries no `@page` directive and is design-only, so it needs no catalogue
   change). No POST continues to target `/Cases/{id}/Assessment`.

4. Reclassify the retired routed Razor page in the Test UI catalogue.

   Files: `docs/design/test-ui/catalogue.json` and
   `docs/design/test-ui/pages/case-assessment--default.html`.

   Change only the `Pages/Cases/Assessment/Index.cshtml` entry (line ~285)
   from `visual` to `redirect` with a D30 `reason`, and delete its stale
   snapshot. A `redirect` entry is `source` + `route` + `classification` +
   `reason` with no `states` array, as `Pages/Triage/Index.cshtml` (line 602)
   already shows. Do not edit UIIMP-014's Case-record snapshots or catalogue
   entries.

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
   Send-to-Claude behaviour assertions unchanged — no assertion is weakened or
   deleted to make a retarget pass, and the import cases keep proving the
   existing dialog flow (ENG-033 owns the D16 assertions). Add a
   non-following GET assertion for the retired route:
   `HttpStatusCode.MovedPermanently` and exact `Location`
   `/Cases/{id}?section=estimate` — the stub builds the target as a literal
   string, as `Pages/Triage/Index.cshtml.cs` does, so there is no trailing
   slash (wrapper correction 2).

   Reuse `AssessmentWorkspaceTestData.Create`, the shared
   `FakeGetAssessmentAccess`, `IntakeWebApplicationFactory`, each test's
   existing `FakeGetCase` / `FakeGetAssessmentWorkspace` pattern, and
   `AssessmentEstimateImportWebTests.RecordingStores` plus
   `AudatexEstimateFixture`. The new test proves all five section IDs in each
   lifecycle state, proves Complete has values but no mutation controls, and
   asserts the Case page renders no staff-review wording (D44). It does not
   create fixture data from the mockup.

6. Run the required verification, inspect the diff for scope and simplification,
   record the implementation report, and open the PR to `dev`.

   Files: no additional owned files.

   Confirm no modified path belongs to CASE-038, CASE-029, CASE-039, ENG-035,
   ENG-029, ENG-031, ENG-033, DOCS-018, or UIIMP-014. No migration is part of
   ENG-034, so `Test-MigrationGrants.ps1` is not applicable.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Browser&Category!=Corpus" -- xUnit.MaxParallelThreads=2
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

The browser filter is the runbook's complementary profile
(`docs/runbook.md` line 325). It is required here because step 5 changes
`Browser/AssessmentReadinessSummaryBrowserTests.cs`, which the
`Category!=Browser` profile excludes and the snapshot scripts do not execute
(review finding 6).

`./scripts/Test-MigrationGrants.ps1` is not run: ENG-034 has no migration.
If a migration becomes necessary, stop and hand it to the serialized migration
lane rather than adding it to this ticket.

## Acceptance conditions

- CASE-038's Case frame renders all five Engineer section IDs for every Case
  lifecycle state; ENG-034 supplies only Damage, Estimate, Settlement, and
  Report content.
- Damage, Settlement, and Report show existing scalar values or `Not recorded`;
  they do not render deferred fields, disabled placeholders, or explanatory
  copy. No damage type appears anywhere (D45); no staff-review flag, checkbox,
  dialog or wording appears anywhere (D44).
- Estimate tabs, line editing, the existing import control and dialog, totals,
  rate-card selection, dialogs, Send to Claude, and all existing Core-owned
  commands behave as before; the existing totals assertions still pass.
- Every operator-visible and accessible-name string in the moved markup comes
  from `Presentation/OperatorLabels.cs`, at unchanged wording.
- `_CaseReport` renders no engineer signatory tuple, no report-image curation,
  no crop entry and no fee-note preview.
- A Complete Case renders every Engineer section read-only, with mutation
  controls absent rather than disabled.
- `/Cases/{id}/Assessment` returns a 301 with the exact Case Estimate
  location, and no Assessment-specific POST handler remains reachable.
- Report draft generation and preview use the Case-page host; preview remains
  a GET returning the existing PDF response.
- The retired page is a `redirect` catalogue entry, its old snapshot is gone,
  and the Test UI catalogue passes.
- No mockup fixture value, Core/persistence change, CSS/JS change, valuation
  shell, damage-map work, settlement editor, report-image curation, fee-note
  preview, or D16 whole-page-drop work is included.

## Design rules that bind

- D29/D30 in FRD-12 require the single-scroll Case record, five always-viewable
  Engineer sections, the fixed section order, and the Assessment 301.
- D11 as narrowed by D30 is `AssessmentAccessPolicy.IsReadOnly` only:
  `PostReportComplete` makes controls absent; `CanOpen` cannot hide sections.
- D16 and D17 preserve the existing import and estimate/rate-card behaviour;
  no second calculation, importer, or rate-selection owner is introduced. The
  D16 whole-page drop itself is [[ENG-033]]'s undelivered work.
- D31 supersedes D18: the sign-off tuple is a Case field ([[CASE-040]]), not a
  `_CaseReport` render of the legacy assessment signatory scalars.
- D42 (fee note), D45 (no damage type), D46 (crop from the Report image cards)
  bind the Report and Damage sections; ENG-034 renders none of the surfaces
  they describe and leaves them to [[DOCS-018]], [[ENG-035]]/[[ENG-036]] and
  [[ENG-031]].
- `docs/design/README.md` § Voice and § No explanatory copy permit labels,
  values, and controls only; no hint, empty-state panel, or how-it-works copy.
- Operator-facing labels live only in `Presentation/OperatorLabels.cs`; exact
  Case state wording comes from `OperatorLabels.CaseStage`.
- D7 permits a disabled control only for a named, composed integration seam or
  named state condition. D21 requires excluded capabilities to be absent:
  direct Glass's and Audatex launch controls are not moved.
- `docs/engineering.md` § One Core owner prohibits Web copies of policy,
  totals, imports, report projection, or AI-job creation.
- D43 permits the mockup's corpus-derived fixture values; ENG-034 simply does
  not need them and reuses the existing test fakes instead.

## Stop condition

The implementation PR is open against `dev`, ENG-034 is in Review, and no
merge, release, proof, or neighbouring-ticket work has been performed.

## Simplification pass

(Written by the implementer after the diff exists: dated heading, the
four lenses, findings and dispositions.)

## Plan review (2026-09-03, gpt-5.6-sol xhigh; dispositions Claude Opus)

gpt-5.6-sol read the ticket body, plan, checklist, D29–D46 and the lane
ownership map independently at `origin/dev` in `.worktrees/research` and
returned REQUEST CHANGES with seven findings. Every claim below was
re-verified in that checkout before disposition.

| # | Severity | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | blocker | The plan's D16 premise is wrong and it moves an import control the design authority says the Estimate section has none of. | **Fixed, part rejected.** Verified: there is no whole-page drop (`site.js` 148–164 is a `preventDefault` safety net only) and no shared Core import command; `Assessment/Index.cshtml` 202–218 and 556–602 are the live control and dialog. Added the "Estimate import: verified current state" section and corrected step 2. **Rejected** the suggestion to make [[ENG-033]] a prerequisite and to assert auto-detection, naming and replay: `docs/design/README.md` line 1158 assigns the dialog's removal and the drop to ENG-033, and taking it here would absorb another ticket's scope and leave no staff import route in the interim. |
| 2 | blocker | Option A ships a duplicate handler surface, CASE-038's handlers have no production caller, and the plan contradicts itself about who retargets the handler tests. | **Part fixed, part raised as an operator question.** Fixed the contradiction: contract item 3 and step 5 now both put the retargeting on ENG-034, and step 3 removes the source handlers in the same PR that adds their callers, so `dev` never carries two *reachable* paths. The remaining choice (accept CASE-038 merging unreachable handlers, or let ENG-034 lease `Details.cshtml.cs` and move them atomically) re-scopes two tickets' whole-file ownership and is recorded in `open-questions/` with option B recommended. |
| 3 | blocker | The planned host still assumes staff review flags (D44). | **Fixed, severity reduced.** Verified `Details.cshtml.cs` 136–142 and 597 still carry `InstructionConfirmedByStaff` / `ImagesConfirmedByStaff` and the "not staff-reviewed" strings. But [[PLAT-070]] `blocks` CASE-038, which blocks ENG-034, so the removal already precedes this ticket on the board. Recorded that chain in the wrapper check and step 1, added contract item 8, and added a D44 assertion to the new test in step 5. |
| 4 | blocker | `_CaseReport` conflicts with D31, D42 and D46. | **Part fixed, part rejected.** Fixed the D31 conflict, which was a real internal contradiction: the plan's bounded scope deferred the signatory tuple while its label list added `Signatory` / `Qualifications` / `Signature`. Those three labels and the legacy `EngineerName` / `EngineerQualifications` / `EngineerSignature` render are removed. **Rejected** for D42 and D46: ENG-034 `blocks` [[DOCS-018]] and [[ENG-031]], so the fee-note preview and the report-image/crop surfaces are board-allocated to them by design; step 2 now names each follow-on owner so the composition hand-off is explicit rather than implicit. |
| 5 | should-fix | The label group does not cover the literals in the moved markup. | **Fixed.** Verified: range 346–552 alone carries Estimates, No estimates recorded, New estimate, Delete estimate, Duplicate, Use estimate, Save estimate, Add line, Estimate name, Source, Repair days, Labour rate, Paint labour rate, Paint h, Labour h, Paint materials, Other costs, VAT, VAT %, Estimate notes, Parts and operations, Operation, Description, Part number, Qty, Action and Notes, none of them in `OperatorLabels.cs`. Step 2 now requires every moved operator-visible and accessible-name literal to be routed through the group at unchanged wording, with a checklist item and an acceptance condition. |
| 6 | should-fix | The command set never runs the changed browser test. | **Fixed.** Verified `Category!=Browser` excludes it and the snapshot scripts do not run xUnit. Added the runbook's complementary browser profile (`docs/runbook.md` line 325) to Commands and the checklist. |
| 7 | nit | The D43 authority basis is stale. | **Fixed.** D43 records operator sign-off on 2026-09-03; the plan now says the fixture values are permitted and simply not needed, keeping the no-fixture choice as proportionality rather than a restriction. |

## Resolutions (2026-09-03) — handler host, option B

The open question on who moves the Assessment POST handlers is resolved as
**option B**. The plan above was written for option A; these amendments bind
and take precedence where they differ.

1. **ENG-034 owns the handler move.** Contract items 3 and 4 and the step-3
   handler removal move out of [[CASE-038]] and into this ticket. In a single
   PR, ENG-034 adds its section partials, moves `SaveEstimate`, `EditLine`,
   `DuplicateEstimate`, `DiscardEstimate`, `SetCurrentEstimate`,
   `ImportEstimate`, `SendToClaude`, `GenerateReportDraft`,
   `PreviewReportDraft` and the lease claim/heartbeat/release handlers from
   `Pages/Cases/Assessment/Index.cshtml.cs` to `Pages/Cases/Details.cshtml.cs`,
   deletes them from the old page model, and lands the `/Assessment` 301.
   Nothing is duplicated and nothing is registered without a caller.
2. **Owned paths gain** `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`
   (handler surface) and `src/Pegasus.Web/Pages/Cases/Details.cshtml`
   (section include points), under the capacity-one shared lock.
3. **Sequencing.** ENG-034 runs serial in wave 3, after CASE-038 and
   [[CASE-039]] have merged and released the `Details.cshtml.cs` lease. The
   lane refreshes with `git merge --no-edit origin/dev` before implementing.
4. **CASE-038 is unchanged in substance**: its section shells stay
   heading-only and its PR carries no Assessment handler, so it merges with
   no unreachable code.

## Simplification pass (2026-09-04)

Run by gpt-5.6-sol (low) over `git diff origin/dev` (plus the new
`CaseEngineerSectionsWebTests.cs`) after implementation, then re-verified by
Claude (build + Core + Architecture + the changed integration/browser
classes + the scoped Test UI capture/verify/catalogue, all rerun green after
applying the findings below).

| # | Lens | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | Reuse | Damage, Settlement and Report partials each repeated the same assessment-field lookup and absent-value label. | **Fixed.** Added one `DetailsModel.AssessmentValue` presentation helper, reused `CaseWorkspace.AbsentValue`, removed the duplicate `EngineerSections.NotRecorded` label. |
| 2 | Simplification | No dead code, redundant branch, or removable over-complication found in the changed route/page-model/partial/test shapes. | Not applicable. |
| 3 | Efficiency | `ReadEditorPost` re-materialized six posted line-field collections to arrays on every loop iteration. | **Fixed.** Materialized each collection once before the loop and reused the arrays. |
| 4 | Altitude | Diff stays within Web composition/presentation, integration tests and generated Test UI snapshots; nothing moved into Core, persistence, CSS/JS or another ticket's owned files. | Not applicable. |
| 5 | Behavioural bugs | None identified during this quality-only pass. | Not applicable. |

## Deviations recorded during implementation (2026-09-04)

1. **Test UI generated index (`docs/design/test-ui/index.html`).** Files.md
   scopes new Case-record snapshot *states* to UIIMP-014, but does not
   anticipate that reclassifying the retired Assessment entry to `redirect`
   and deleting its snapshot leaves a broken local link in the checked-in,
   mechanically-generated `index.html` — `Test-UiCatalogue.ps1` fails without
   fixing it. Precedent: PLAT-029 committed the equivalent 1-line `index.html`
   update in the same commit as its own catalogue/route changes. Applied the
   minimal mechanical fix (moved the one `/Cases/{id:guid}/Assessment` row
   from the visual list to the non-visual table, matching the already-owned
   `catalogue.json` entry) rather than leaving the catalogue gate red or
   absorbing UIIMP-014's actual scope (new snapshot states).
2. **Section fragment URL.** The controller's briefing named
   `/Cases/{id}/Section?section=<key>` as "the CASE-038 contract as corrected
   2026-09-04" for lazy fragments; the binding jump/redirect target
   throughout the plan, tests and this implementation is
   `/Cases/{id}?section=<key>` (confirmed against the merged CASE-038 host
   actually on `origin/dev`). The 301 and all Case-page redirects use
   `/Cases/{id}?section=estimate` as the plan specifies; no separate
   `/Section` fragment endpoint exists to call.
3. **CASE-039 not yet merged at implementation time.** Per this
   repository's parallel-build policy, "serialized" plan text sets merge
   order, not build order: implementation proceeded now, in this ticket's own
   worktree, against `origin/dev` as of `90a759184` (CASE-038 merged, CASE-039
   still in progress). The `Details.cshtml.cs`/`Details.cshtml` capacity-one
   lease and merge ordering behind CASE-039 remain the reviewing controller's
   to sequence at merge time.
4. Pre-existing host naming (`instructionConfirmedByStaff` /
   `imagesConfirmedByStaff` parameter names on the completeness call) was left
   unchanged: no staff-review control or wording is rendered anywhere in
   ENG-034's sections (proved by the new test), and renaming those pre-existing
   parameters is CASE-038/PLAT-070 scope, not this ticket's.
5. `Details.cshtml` needed no edit: CASE-038 had already landed all four
   section include points.
