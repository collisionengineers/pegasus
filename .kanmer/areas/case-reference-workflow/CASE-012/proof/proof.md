# Proof — CASE-012: Redesign the Case page workspace

## What was verified, and where

Verified on merged `dev` at `b92cb9a7` (`Merge pull request #612 …`), the
head of the wave-A gate run. CASE-012 shipped in two merges, both reachable
from that SHA: PR #599 → merge `4d696225` (2026-08-28, the workspace frame,
Overview, section placeholders and lifecycle dialogs) and PR #615 → merge
`210727dd` (2026-08-29, the rest of lane E1 — `Eva/Send`, `Create`, the
`Workflow`/`Closure` disposition and the catalogue text). Every commit named
in the ticket's records was checked with `git merge-base --is-ancestor` and
is reachable from `b92cb9a7`: `4d696225`, `210727dd`, `12d462d1` (#599 head),
`2dcf69a4` (#615 head), `2204117a`, `9b102a3d`, `0f316f76`, `1c15dfa5`,
`0b4e14bc`. Both PRs merged green: `gh pr view 599/615 --json
statusCheckRollup` reports `SUCCESS` for `unit`, `sql-integration (1..3)`,
`browser` and `sql-integration-coverage` on each (#615's `browser` check
completed `2026-08-29T09:01:50Z`, run `33243866629`). Build and test tiers
below cite the orchestrator's canonical gate evidence for `b92cb9a7`; no
suite was re-run here.

## Evidence

### The build and the test suite are green on merged `dev`

Tier: build/test.

From the canonical gate evidence for `b92cb9a7` (not re-run here):

```
dotnet build ./Pegasus.slnx --configuration Release --no-restore
  -> Build succeeded. 0 Warning(s), 0 Error(s).
dotnet test … --filter 'Category!=Corpus&Category!=Browser'
  -> Pegasus.IntegrationTests  Failed: 0, Passed: 1022, Skipped: 2
  -> Pegasus.Core.Tests        Failed: 0, Passed: 1133, Skipped: 0
  -> Pegasus.ArchitectureTests Failed: 0, Passed:  100, Skipped: 0
```

Every test named below lives in `Pegasus.IntegrationTests` and is inside that
1022, except the Browser-category journey, which is covered by the `browser`
CI check on each merged PR.

### The Case workspace frame renders — header, ribbon, presence, record bar, edit bar

Tier: build/test.

`src/Pegasus.Web/Pages/Cases/Details.cshtml:57-77` is the `page-header`
(eyebrow `Case workspace · @registration`, h1 reference, Back to Cases,
section-carrying Refresh); `:100-121` the five-cell `.record-ribbon`
(Case/PO, Registration, Claimant, Principal, State chip); `:126-142` the
`.presence-strip`; `:148` the `.record-bar`; `:281-297` the sticky
`.edit-bar`. Each class is defined in the shipped stylesheet, not the wave-5
legacy block: `src/Pegasus.Web/wwwroot/css/site.css:383` `.record-ribbon`,
`:341` `.presence-strip`, `:380` `.record-bar`, `:390` `.edit-bar`, `:398`
`.case-workspace`, `:399` `.case-section-nav`, `:405-406` `.case-context` /
`.case-main`.

`tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs:505`
(`WrongHolderProjectionClearsProtectedLeaseAuthorityAndFallsBackToRecovery`)
and `:1072` (`EditModeCopyAvoidsBannedOperatorVocabularyInEveryState`) render
the page over the real pipeline and assert on that markup.

### The six workspace sections render and exactly one is current

Tier: build/test.

`src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml:10-18` is the
one list of sections (Case Overview, Vehicle, Valuations, Inspection address,
Case Files, Notes); `Details.cshtml.cs:68-75` maps `?section=` onto them with
`overview` as the fallback; `Details.cshtml:299-427` renders the selected
body, `_CaseWorkflow` + `_CaseSummary` for Overview.

`CaseDetailsWebTests.cs:162`
(`SectionQuerySelectsOneSectionAndUnknownValuesFallBackToOverview`) is a
nine-row theory that GETs the real route and asserts `CurrentSectionLabel`
for each section, plus `?section=evidence` and `?tab=case-files` falling back
to Case Overview.

### The lifecycle dialogs render and post to existing Core handlers

Tier: build/test, with the handler registrations named.

Dialogs in `Details.cshtml`: Place on Hold / Release Hold `:517-546`
(`/Cases/Workflow` handlers `Hold`/`ReleaseHold`), Close Case `:554-574`
(`/Cases/Closure?handler=Close`) with the wrong-principal linked-replacement
form `:576-599` (`/Cases/Workflow?handler=CreateLinkedReplacement`), Reopen
Case `:606-651` (`/Cases/Closure?handler=Reopen`), Return to Engineer
`:655-676` (`Reopen` with destination `ReportPreparation`). Every target is
an existing handler: `Pages/Cases/Workflow.cshtml.cs:26 OnPostHoldAsync`,
`:42 OnPostReleaseHoldAsync`, `:180 OnPostCreateLinkedReplacementAsync`;
`Pages/Cases/Closure.cshtml.cs:52 OnPostCloseAsync`, `:69 OnPostReopenAsync`.

`CaseDetailsWebTests.cs:431`
(`LifecyclePostsBindHoldReleaseAndReportPreparationToAuthenticatedLease`)
asserts the rendered `data-dialog="case-hold-dialog"` and `name="reason"`,
then posts `/Cases/{id}/Workflow?handler=Hold|ReleaseHold|StartWork` and
checks each reached Core carrying the authenticated actor's subject id, the
case version and the lease.

### The EVA handoff renders, gated to Review, and posts the real routes

Tier: build/test.

`Details.cshtml:44` `var canSendToEva = isReview;`; `:249-254` the bar
control; `:680-724` the dialog — the named Engineer `<select>` posting
`/Cases/Workflow?handler=AssignEngineer`, `Export ZIP` posting
`/Cases/Documents/Export?handler=Bundle`, and `Send via API` posting
`/Cases/Eva/Send?handler=Submit` only when `Model.CanSubmitToEva`
(`Details.cshtml.cs:486`, `EvaSubmissionPolicy.AllowsManualSubmission`).

`CaseDetailsWebTests.cs:63` (`SendToEvaRendersOnlyInReview`) pins all three
in one theory: the bar text, `data-dialog="eva-handoff-dialog"` and the
`/Documents/Export` route are present for `Review` and absent for `NotReady`
and `ReportPreparation`.

### D10 — "report sent" confirms detected evidence and never asserts a send

Tier: build/test.

`Details.cshtml:46-53` gates the control on edit authority + Report
preparation + `AvailableReportSentEvidence.Count > 0`; `:730-760` renders one
confirmation form per detected item, showing only mailbox and office time and
posting `/Cases/Tasks?handler=LinkReportEvidence`.

`CaseDetailsWebTests.cs:101`
(`ReportSentRendersOnlyWithDetectedEvidenceWhileWithEngineer`) seeds evidence
carrying folder, item, internet-message, conversation, reply-chain and
occurrence handles plus two 64-character hashes, then asserts the mailbox is
in the visible text and that none of the handles or hashes are.

### `Cases/Eva/Send` renders

Tier: build/test (Browser CI), plus the shipped source.

`src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml:1` routes
`/Cases/{caseId:guid}/Eva/Send`; `Send.cshtml.cs:54 OnGetAsync` backs it;
`:8-19` is the design-system `page-header`, `:21-88` the panel, with the
recorded outcome through `Shared/_StatusChip` and both times through
`OperatorLabels.OfficeTime`. `:101-105 OutcomeTone` gives `Rejected` and the
unreachable-transport fallback `red` and keeps `Partial` `amber`.

Render evidence:
`tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs:139` navigates
a real browser to `/Cases/{id}/Eva/Send` and exports by keyboard from the
rendered page (`:140-161`). The class carries `[Trait("Category","Browser")]`
(`:24`), so it is outside the orchestrator's filtered run; it is inside PR
#615's `browser` CI check, which completed `SUCCESS`.

### `Cases/Create` renders

Tier: build/test, with production callers named.

`src/Pegasus.Web/Pages/Cases/Create.cshtml:41-52` is the `page-header`;
`:22-36` the `Row` local function emitting `definition` rows with a
`class="prov"` provenance glyph — the class site.css actually styles a
tooltip for (`site.css:1857`, `:1875 .prov::after`); `:75` uses the shared
`Shared/_ErrorSummary` partial. No `page-heading` remains anywhere in tracked
source (`git grep page-heading -- src` matches only a stale
`bin/Release/**/publish` artefact), which is the class the page previously
used and which `site.css` defines nowhere.

`tests/Pegasus.IntegrationTests/CaseCreateWebTests.cs:554-558`
(`OpenCreateScreenAsync`) GETs `/Cases/Create?receiptId=…` and asserts
`HttpStatusCode.OK`; it is the entry point for the file's other tests.

Production callers of the route: `Pages/Index.cshtml:24`,
`Pages/Intake/Details.cshtml:544`, `Pages/Shared/_ShellDialogs.cshtml:64`,
`Presentation/UploadOutcome.cs:322`, `wwwroot/js/site.js:1364`.

### `Workflow.cshtml` and `Closure.cshtml` — the disposition

Tier: registration (they are POST targets), plus file evidence.

Both are two-line `@page`/`@model` files with no markup
(`Pages/Cases/Workflow.cshtml`, `Pages/Cases/Closure.cshtml`), and neither
code-behind has an `OnGet`: `Workflow.cshtml.cs` has only `OnPostHoldAsync`,
`OnPostReleaseHoldAsync`, `OnPostReturnToReviewAsync`,
`OnPostAssignEngineerAsync`, `OnPostStartWorkAsync`,
`OnPostRecordEngineerFindingAsync`, `OnPostCreateLinkedReplacementAsync`;
`Closure.cshtml.cs` only `OnPostRecordReportApprovalAsync`,
`OnPostCloseAsync`, `OnPostReopenAsync`, `OnPostArchiveAsync`. A GET
therefore renders nothing — it does not redirect.

CASE-012 shipped no port for them (correct: there is no markup to port) and
instead corrected the false claim about them. On `dev`,
`docs/design/test-ui/catalogue.json` now classifies both `protocol` with the
reason "POST-only workflow/closure actions; GET has no handler and renders no
content." They are live: the workspace dialogs above post to their handlers.

The identical false `redirect` reason survives on `/Cases/{id}/Custody`,
`/Cases/{id}/Tasks` and `/Cases/{id}/Vehicle`, which have the same
no-`OnGet` shape. Those files are CASE-027's (lane E2); reported, not fixed —
recorded under Outstanding.

### The export marker is a Core constant with a real caller

Tier: registration + build/test.

`src/Pegasus.Core/Eva/EvaBundleSchema.cs:108`
`public const string BundleExportedHistoryEventKind = "eva_bundle_exported";`
Writer: `src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs:157`.
Reader: `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs:452-456`, which sets
`HasExportedBundle` and so decides the "Send to EVA" / "Download EVA package"
label at `Details.cshtml:252`. One list, one owner, two named callers.

### No raw identifiers, transport handles, hashes or dead controls on the Case surface

Tier: build/test.

The Engineer selector posts an id but shows the account name
(`Details.cshtml:674-679`, options from `Details.cshtml.cs:470`
`staffAccountQueries.ListAsync`); the context column shows
`Model.EngineerDisplayName` (`:437`), resolved at `Details.cshtml.cs:458-461`.
`git grep disabled` across `Details.cshtml`, `Cases/Shared/**`,
`Create.cshtml` and `Eva/Send.cshtml` finds exactly one gated control —
`Open Assessment` (`Details.cshtml:269-273`), which is a Core state gate
(`AssessmentAccessPolicy.CanOpen`,
`src/Pegasus.Core/Assessment/AssessmentWorkspace.cs:45-53`, now D11-correct:
Report preparation or later plus a current-cycle export), not an uncomposed
capability.

Pins: `CaseDetailsWebTests.cs:26`
(`AssessmentControlReflectsTheSharedAccessDecision`), `:187`
(`CaseHistoryShowsResolvedActorNamesAndNeverARawSubjectId`), `:963`
(`ANonHolderSeesTheEditingStaffAccountByNameAndNeverItsIdentifier`), `:1036`
(`AnAutomationHolderIsNamedAsAiAndNeverAsAMemberOfStaff`), `:874`
(`RefusedRetentionKeepsEditorialValuesAndNeverIdentifiersOrRoutingFields`).

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| The approved redesign meets the resulting acceptance criteria and does not regress Case workflows | Proven for no-regression; partly unproven for "approved redesign" | Suite green on `b92cb9a7` (1022 IntegrationTests, 1133 Core, 100 Architecture) plus both PRs' `browser` checks `SUCCESS`. The §1.8 contract is met except the "Download EVA package (With Engineer or Complete)" clause, deliberately not shipped and awaiting an operator ruling (below) |
| Case and Assessment surfaces expose no raw identifiers, transport handles, hashes, dead controls or explanatory narration (inherited [[PLAT-015]]) | Proven for the Case surface; **unproven for Assessment** | Case: the five pins above. Assessment: CASE-012's diff touches no Assessment file — that surface was delivered by [[ENG-025]], so this proof does not carry it |
| Named staff selectors replace Engineer/assignee GUID inputs (inherited) | Proven for the Case workspace | `Details.cshtml:674-679`; `Details.cshtml.cs:458-461`. A raw GUID `engineerId` input survives at `Pages/Search/Index.cshtml:77` — a different page, not in this ticket's files |
| Report-Sent evidence shown as mailbox + time, transport handles internal (inherited) | Proven | `Details.cshtml:747-751`; `CaseDetailsWebTests.cs:101` asserts the six handles and two 64-char hashes are absent from visible text |
| Typed-SHA report-approval input removed (inherited) | Proven the input is gone; the handler is now caller-less | No `artifactSha256` input in any view; `git grep RecordReportApproval -- src/Pegasus.Web` matches only `Closure.cshtml.cs:23`. See Outstanding |
| `_CaseWorkflow` lifecycle/version narration and the Assessment "Most of the report is written for you" card removed (inherited) | Proven | `AssessmentCopyWebTests.cs:39` asserts the card's copy is absent |
| Plan acceptance: every action control maps to an existing named handler | Proven | Every form in `Details.cshtml` names an existing `OnPost*` — enumerated above |
| Plan acceptance: no new CSS, script, package or abstraction | Proven | PR diffs touch no `site.css`, no `site.js`, no project file |
| Plan acceptance: no clipped text or overflow at 1580 / 1100 / 760 | **Unproven** | No layout walk was run here. [[UIIMP-010]] owns it |

## Outstanding

Nothing found contradicts what the ticket claims to have shipped. These are
gaps the implementer reported before merge, each confirmed still open on
`b92cb9a7` and each owned elsewhere.

- **Script-off route to the EVA handoff.** The bar control is
  `data-dialog-open="eva-handoff-dialog"` (`Details.cshtml:250`), inert
  without `site.js`; `git grep "Cases/Eva/Send" -- src` finds one match, the
  POST form at `Details.cshtml:705`. No GET link exists from the workspace,
  which is why the Send page was kept and ported and why
  `OperatorJourneyTests.cs:139` navigates by URL. Owned by **[[TICK-223]]**.
- **Layout integrity at 1580 / 1100 / 760.** Not walked. Owned by
  **UIIMP-010** (`tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs`
  exists on `dev`).
- **The Test-UI snapshot corpus is stale for both redesigned pages.**
  `docs/design/test-ui/pages/case-details--default.html` and
  `case-create--default.html` were last regenerated in `35292cff`
  (2026-08-26) and contain none of `record-ribbon`, `case-section-nav`,
  `case-workspace`, `page-header` or `record-bar`; the Create snapshot still
  contains `page-heading`. CASE-012 correctly rewrote the catalogue *branch
  text*, so catalogue and corpus now disagree. Not blocking — no CI job runs
  `scripts/Test-UiCatalogue.ps1` on `dev` yet. Owned by **UIIMP-005**, which
  the epic's merge-ordering constraint puts last among the UI lanes,
  regenerating against the final markup.
- **`Cases/Eva/Send.cshtml` has no catalogue entry at all** (`grep -c
  "Eva/Send" docs/design/test-ui/catalogue.json` → 0), so
  `Test-UiCatalogue.ps1` will report an unclassified routed Razor source. A
  `visual` entry needs a captured snapshot. Owned by **UIIMP-005**.
- **§1.8's "Download EVA package (With Engineer or Complete, exported)" is
  not shipped.** `Details.cshtml:44` gates the control on Review alone and
  only switches its label once exported, so With Engineer and Complete have
  no route to the package. The plan cites FRD-07 (`CaseNotInReviewException`)
  and the prototype's final render as the reason; `SendToEvaRendersOnlyInReview`
  now pins Review-only as the shipped behaviour. **Needs an operator ruling
  between the §1.8 contract and FRD-07** — recorded, not resolved here.
- **`ViewData["WorkspaceRecord"]` has no writer.** `_Layout.cshtml:43` reads
  it to build the §1.1 workspace tab strip; `git grep WorkspaceRecord -- src`
  returns that one read. The strip never carries a case tab. Owned by
  **PLAT-029 / UIIMP-009**.
- **`Closure.OnPostRecordReportApprovalAsync` now has no production caller.**
  Removing the typed-SHA form (correct, and inherited scope) left the handler
  reachable only from `tests/…/CaseReportApprovalWebTests.cs:58,68`. The plan
  disposed this as "handler contract pinned for its future caller". By rule
  14 that is test-only code; it is pre-existing rather than new, so it is
  recorded here rather than treated as a CASE-012 defect. **No owner ticket
  yet.**
- **`docs/current-architecture.md:525` is stale.** It still says the Send to
  EVA control "opens `/Cases/{caseId}/Eva/Send`" (it opens a dialog since
  #599) and that "Assessment is available only in Review or Report
  preparation after an export", which contradicts D11 and the shipped
  `AssessmentAccessPolicy`. Owned by **DELIV-030** (wave-5 current-state
  docs).
- **The false `redirect` catalogue reason survives on `/Cases/{id}/Custody`,
  `/Cases/{id}/Tasks` and `/Cases/{id}/Vehicle`** — same no-`OnGet` shape
  CASE-012 corrected for `Workflow`/`Closure`. Owned by **CASE-027** (lane
  E2) or UIIMP-005.
- **`Mail/Message.cshtml` writes `class="provenance"` with `data-word`**,
  which renders no tooltip (`site.css:365` defines `.provenance` with no
  `::after`). Same defect CASE-012 fixed in `Create.cshtml`. Owned by
  **MAIL-025 / PLAT-029**.
- **No deployed evidence exists for any claim here.** Nothing in this proof
  shows the redesigned workspace running in a Pegasus deployment. Every claim
  above is tier 1 (registration) or tier 2 (build/test); none is tier 3.
- **The board record carries no `commits`.** `CASE-012` has `prs` #599 and
  #615 but an empty commits field, and `docs_todo` is still `true`. Recorded,
  not changed by this proof.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not
been promoted; the exact-SHA `dev` → `main` promotion happens at wave 5 and
needs explicit `MERGE AUTH GRANTED`.

## 2026-08-29 — Reversed out of Done under the strict rule 14 (D20/D21)

The operator settled rule 14 in favour of the strict reading after this proof was
written, and separately ruled that a disabled control or a closed feature gate is
never a delivered capability (D21). An independent GPT-5.6 audit, adjudicated
against this ticket's own What/Owns/Verification scope, found the following named
capabilities are not delivered on merged `dev` at `b92cb9a7`:

| Capability | Why it does not qualify | Wired by |
| --- | --- | --- |
| Named staff selector for the case-task assignee — inherited bullet 1 verbatim: "Replace task assignee and Engineer GUID inputs or displays with named staff selectors and business-readable names, reusing the existing staff-account query and display-name convention." [[PLAT-015]] routes it here by name (`PLAT-015.md:34`, `:49`) | This ticket delivered the Engineer half and **deleted** the task half. At `2204117a^`, `_CaseWorkflow.cshtml:264,268,272,283` carried the `Assignee ID` inputs on AssignTask/CreateTask, the `assignee @(task.AssigneeId…)` GUID render, and the CompleteTask/CancelTask forms. On `b92cb9a7` none of it exists: `grep -rn "CreateTask\|AssignTask\|CompleteTask\|CancelTask" src` returns only the four handler declarations at `src/Pegasus.Web/Pages/Cases/Tasks.cshtml.cs:61,89,117,143` — no `.cshtml`, no `site.js`, and no `Mcp/*.cs` injects `ICreateCaseTask`/`IAssignCaseTask`/`ICompleteCaseTask`/`ICancelCaseTask` (only `Tasks.cshtml.cs:18-21`), so the open `Features:AutomationMcp` gate rescues nothing. Four ports registered at `DependencyInjection.cs:349-352` now have no reachable consumer — D21's last row. The remove-list in this ticket is a closed enumeration ("inactive vehicle/history/query, Audatex/Glass's, estimate-tab, and assessment-form controls") and case tasks are not in it, so deletion was not an authorised disposition; "replace" was the instruction. | [[CASE-027]] (backlog) owns `src/Pegasus.Web/Pages/Cases/Tasks.*`; [[CASE-029]] (backlog) also lists `Tasks.*`. **Warning: neither ticket's What currently names task create/assign/complete/cancel**, so one of them needs a scope amendment or a new ticket — no board record today promises this caller. |
| Report-approval recording — inherited bullet 2 told this ticket to *replace* "typed SHA inputs" with "the mailbox address, relevant times, and a verified evidence statement" | The evidence-statement half shipped and is wired (`Details.cshtml:739` → `Tasks.cshtml.cs:201`). But the typed-SHA report-approval form was deleted with no operator route left: `RecordReportApproval` matches only `src/Pegasus.Web/Pages/Cases/Closure.cshtml.cs:23`, POSTed solely by `tests/Pegasus.IntegrationTests/CaseReportApprovalWebTests.cs:58,68`, while the Overview panel still renders "Report approved" from a record nothing can now create. `proof/proof.md:286-287` dismisses this as "pre-existing rather than new" — the precise loophole D20 closes, and it was this ticket that removed the caller from a file it owns. | [[CASE-030]] (backlog) owns `src/Pegasus.Web/Pages/Cases/Closure.*`, but its What names only the Report-sent dialog, Return to Engineer and Close Case. **No existing ticket names `RecordReportApproval`**; [[ENG-025]] did not wire it. |

Nothing in the proof above is withdrawn — it remains accurate at the tier it claims.
What changed is the bar, not the evidence. This ticket's own report concedes the
first item at `report.md:112` ("Task CRUD lost its only UI … no follow-up ticket
unless the operator asks"), and its Verification claim "does not regress Case
workflows" cannot stand against four orphaned ports.

The redesign itself is otherwise well wired — the frame, six-section nav, lease
cluster, hold/close/reopen/return dialogs, upload link, engineer assignment, ZIP
export, report-sent confirm, notes and chase all trace to live handlers. The
reversal is narrow: it turns on one half of one inherited bullet, and on a port the
ticket orphaned.

### Findings that were NOT counted against this ticket

This ticket's own text is thin — What is one sentence, Verification is one sentence,
and the "Inherited scope from [[PLAT-015]]" block is its Owns section (two "replace"
bullets and two "remove" bullets). Everything below sits outside those bullets and
belongs to `waves.md` lanes E1/E2/wave 4.

- Manual EVA API submission behind the closed per-Principal toggle
  (`docs/operations.md:358-361`, "no Principal has either EVA toggle on") —
  pre-existing EXT-04 capability, [[TICK-077]] (verifying); the handler
  `Eva/Send.cshtml.cs` `OnPostSubmitAsync` arrived in `09beefef`, before this
  ticket, which only redrew a conditional control. Opening it is an operator
  activation, not a code ticket.
- "Download EVA package (With Engineer or Complete, exported)" — §1.8 clause only,
  absent from this ticket's own text; contradicts FRD-07's Review-only export
  (`CaseNotInReviewException`). No owner ticket; the proof correctly escalates it
  for an operator ruling.
- Unsaved-state chip on the edit bar — §1.8 only; needs a dirty-state producer in
  `site.js`, owned by [[PLAT-029]] / [[UIIMP-009]].
- Manual vehicle Refresh DVLA / Refresh DVSA-MOT, suggestion accept/correct, Vehicle
  History — [[CASE-027]] and [[CASE-029]]. This ticket's inherited bullet 3 ordered
  removal of the inactive vehicle/history/query controls, so their absence is
  compliance, not failure.
- Valuations Add/Edit — [[CASE-029]], backed by [[ENG-027]].
- Inspection-address Edit/Cancel/Save — [[CASE-027]].
- Case correspondence Compose/Reply/Forward — [[CASE-027]] draws the rows and defers
  the buttons to wave 4; [[MAIL-026]] supplies them, [[MAIL-027]] the backend.
- `IRecordEngineerFinding` orphaned (`DependencyInjection.cs:369`; only consumer
  `Workflow.cshtml.cs:156`, POSTed only by tests) — not named anywhere in this
  ticket's What/Owns/Verification. A genuine epic-level orphan with no owner;
  belongs on [[ENG-025]]'s account or a new ticket, not on this one.
- Permanently inert Glass's / Audatex buttons at
  `Pages/Cases/Assessment/Index.cshtml:211,214` — [[ENG-025]], whose What names them
  as D7 seams. D22 ratifies the rendering while D21 denies they are delivered; this
  ticket's diff touches no Assessment file, so its inherited "Case and Assessment
  surfaces" verification can neither be discharged nor failed here. Now owned by
  [[TICK-085]] (Glass's) and [[ENG-030]] (Audatex).
- Raw `engineerId` GUID input surviving at `Pages/Search/Index.cshtml:77` —
  [[CASE-026]] owns `Pages/Search/**` (`waves.md` lane D).
