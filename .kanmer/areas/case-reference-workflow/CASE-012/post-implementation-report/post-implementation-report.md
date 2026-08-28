# Post-implementation reports — CASE-012

Three runs took this ticket. Read them as follows, and do not mistake one for
another:

| Record | Branch | Status |
| --- | --- | --- |
| `report.md` in this folder | `task/case-012-case-workspace` (PR #599) | **merged to dev** — the Case workspace frame and Overview that are in the product |
| §1 below | `task/case-012-eva-send-salvage` | **current** — round 3, the rest of lane E1 |
| §2 below | `task/case-012-case-workspace-parallel` | **superseded** — never merged, must not be merged |

`report.md` is the shipped record. This file's §2 is not: it describes a
parallel run that lost. Neither file is deleted, because §2 is still the only
description of two partials CASE-027 may want as prior art.

## 1. Round 3 — the rest of lane E1 (`task/case-012-eva-send-salvage`)

### Why there was a round 3

The ticket sat at `verifying`, but `EPIC-011/waves.md` gives lane E1 four more
files than PR #599 touched. `Create.*`, `Eva/Send.*`, `Workflow.*` and
`Closure.*` were all still at base == dev. Create and Eva/Send were drawn in
the pre-EPIC-011 vocabulary, and Create's outer wrapper used `page-heading`,
which wave 1 defines nowhere in `site.css`, so the page rendered unstyled.
Done means wired, so the ticket went back to `implementing`.

### What shipped

| File | Change |
| --- | --- |
| `docs/design/test-ui/catalogue.json` | both `case-details` branch texts rewritten; they still described the pre-redesign page and would have misreported the workspace to UIIMP-005's gate |
| `Pages/Cases/Create.cshtml` | ported to the shipped design system; six read rows and their provenance glyphs reduced to one `Row` local function; explanatory copy removed |
| `Pages/Cases/Eva/Send.cshtml` | ported to the design system; outcome shown through `_StatusChip`, times through `OperatorLabels.OfficeTime` |
| `Pages/Cases/Eva/Send.cshtml.cs` | class summary corrected — it claimed the bar opens this page, which stopped being true at PR #599 |
| `tests/…/CaseDetailsWebTests.cs` | the three salvaged pins, retargeted; fixture store gains `AvailableReportSentEvidence` |

`Workflow.cshtml` and `Closure.cshtml` needed no port: both are two-line
`@page`/`@model` files with no markup, already classified `redirect` in the
catalogue, and their handlers are the live POST targets of the lifecycle
dialogs PR #599 shipped. They are not subsumed and must not be deleted. That
finding is reported rather than acted on; the deletion question, if any, is
UIIMP-009's.

### Reuse

`_ErrorSummary` (previously an orphan partial, now with its first caller),
`_InstructionDraftFields`, `_StatusChip`, `OperatorLabels.OfficeTime`, and the
design-system classes `page-header`, `panel`/`panel-head`/`panel-body`,
`definition-list`/`definition`, `field`, `notice`, `cluster`, `stack`,
`provenance`, and `grid grid-2` with `label.choice` — the last two being the
idiom the merged `_CaseWorkflow.cshtml` already uses in this lane. No new CSS,
no new script, no new package, no new abstraction.

### Salvage ruling

The full ruling on `task/case-012-case-workspace-parallel` is in the ticket
scratch. In short: superseded for the Case workspace, must not be merged (9
predicted conflicts, and merging it would revert MAIL-025, CASE-025, ENG-026
and PLAT-023). Only `Eva/Send.*`, `Create.cshtml` and the catalogue text were
salvaged. Its Engineer-assignment form on the Send page, its widened Send
state gate, its `?tab=` aliases, and its `_CaseVehicle`/`_CaseFiles` partials
(lane E2) were each dropped for a stated reason.

### Verification

`dotnet build ./Pegasus.slnx --configuration Release` — succeeded, 0 warnings,
0 errors. `dotnet test … --filter "FullyQualifiedName~CaseDetailsWebTests"` —
42 passed, 0 failed; the three new pins alone are 15 of those. `dotnet test …
--filter "FullyQualifiedName~CaseCreateWebTests"` — 17 passed, 0 failed. The
Browser journey that exercises the Send page was not run: the orchestrator
owns that gate.

### Simplification pass — 2026-08-28

Applied: the six near-identical read rows and the separate `Prov` local
function in `Create.cshtml` became one `Row` function; the page's ad-hoc
validation summary became the shared `_ErrorSummary` partial; the Send page's
own copy of the office time format became `OperatorLabels.OfficeTime` and its
bare outcome text became `_StatusChip`.

Considered and rejected: dropping `data-word` from the provenance glyph, which
renders no tooltip on the new `.provenance` class — kept because the merged
`Mail/Message.cshtml` writes it and the existing convention wins; the split is
reported instead. Also rejected: folding `Eva/Send`'s `CanSubmitToApi` into
`DetailsModel.CanSubmitToEva` — both ask the same Core policy
(`EvaSubmissionPolicy.AllowsManualSubmission`) for their own render, which is
composition, not a second rule.

### Out-of-scope findings

Eight, listed in the ticket scratch under "Reported out of scope". The two
that most affect other lanes: `Eva/Send.cshtml` has no catalogue entry at all
(a `Test-UiCatalogue.ps1` failure that needs a captured prototype), and with
script off nothing on the workspace links to the EVA handoff — the reason this
round kept and ported the Send page rather than reducing it to a handler.

## 2. SUPERSEDED — parallel run (`task/case-012-case-workspace-parallel`)

**This section records a run that was never merged and must not be merged. It
is kept only because it is the sole description of the `_CaseVehicle` and
`_CaseFiles` partials CASE-027 may read as prior art. Nothing below describes
what is in the product; `report.md` does.**

### Stop condition: two implementations of one ticket

This run took CASE-012 at 15:14 (board showed it untaken, `preparing`) and
implemented it on the worktree `../pegasus-worktrees/case-012-case-workspace`
from `origin/dev` 5ca2572c. At push time the remote
`task/case-012-case-workspace` already carried five commits from a different
run (2204117a…b7c4d8d2, 15:22–16:27) with PR #599 open. Nothing was
force-pushed, merged over, or duplicated as a PR. This run's commits are
parked, untouched, at `origin/task/case-012-case-workspace-parallel`
(head 866fe459, `origin/dev` d6b00b2b merged) for the orchestrator to keep,
cherry-pick from, or delete. The local worktree still points at that head.

Commits: 54da5583 frame/Overview/sections/EVA handoff · ac5bb48f tests ·
f70b9fb8 catalogue text · 5d89f0c7 merge origin/dev · 866fe459
simplification. `dotnet build ./Pegasus.slnx --configuration Release` green
after the merge; `Test-UiCatalogue.ps1` reports only the pre-existing
`Administration/Principals/EvaSubmission.cshtml` gap (PLAT-029 noted it).
Tests not run by the implementer (orchestrator wave loop).

### What this run shipped (differences from PR #599 worth comparing)

- `Details.cshtml(.cs)`: `?section=` with `?tab=` aliases; `page-header`
  (Back to Cases + `_FreshnessBanner` with `section` refresh field);
  `record-ribbon`; `presence-strip[data-edit-authority]` with
  `EditModeDisplay.HeldBy`; `record-bar` mapped to existing handlers only
  (ClaimLease, ReleaseLease + `_EditFinishConfirm`, RenewLease +
  `_EditHeartbeat`, Workflow/Hold|ReleaseHold via `_ReasonDialog`,
  Custody/CreateRequestUploadLink as a direct post, Eva/Send link labelled
  "Send to EVA" in Review or "Download EVA package" when With Engineer or
  Complete and `AssessmentAccessState.LatestExportVersion` is set,
  Tasks/LinkReportEvidence "Report sent" dialog rendered only With Engineer
  with non-empty `AvailableReportSentEvidence` (D10; radios name mailbox ·
  Sent time), Closure/Reopen "Return to Engineer" (Complete) and "Reopen
  Case" dialog with Destination select (closed, not Created in error),
  Open Assessment on `CanOpen` only, Close Case dialog with outcome select
  (Post-report complete only from PostReport). Sticky `edit-bar` (lease
  text, Discard → finish form, Save `[data-edit-save]` → `#case-edit-form`).
  `ViewData["WorkspaceRecord"]` set. `case-workspace` = `_CaseWorkspaceNav`
  | `case-main` | `case-context` (State, Version, Due, Engineer, Edit
  authority; Next action card from the first blocker).
- `DetailsModel`: `Sections` (one list for nav + next-action), `Section`,
  `AssessmentAccess`/`CanOpenAssessment`/`IsExported`, `EngineerName` via
  `IStaffAccountQueries.GetAsync`, `IsEditing`, `DueDate`, `LoadedAtUtc`,
  `Blockers` (`CaseBlocker` rows from completeness values when NotReady and
  the policy is unmet, custody `CanRetry`, scheduled due work).
- `_CaseSummary` (Overview): `workflow-stepper` (four D3 stages;
  `workflow-exception` chip for Held/closed), Outstanding requirements
  (`blocker-list` + Confirm completeness form while editing NotReady), edit
  form (six fields + Reason; remaining editable values hidden at current
  values because a null clears the confirmed value), Case overview panel
  (Work facts incl. "Report approved" by name and "Report sent" mailbox ·
  time; Parties; accident card). Only populated facts render.
- New `_CaseVehicle` (facts + lookup/suggestion forms; the disabled
  "Look up vehicle"/"Check vehicle history" buttons are gone), new
  `_CaseFiles` (custody panel + `_CaseDocuments` + galleries), `_CaseHistory`
  (+ chase history, chase draft, "Record chase" form). `_CaseWorkflow`
  deleted: task create/assign/complete/cancel, report approval (typed
  identity + SHA-256), return-to-review, Engineer finding, linked
  replacement, archive and unlink leave the UI; every handler stays.
- `Eva/Send`: EVA handoff — allows Review, ReportPreparation, PostReport,
  PostReportComplete; API only in Review; Engineer select (enabled accounts
  with the Engineer role via `IStaffAccountQueries.ListAsync`, because
  `IListStaffAccounts` requires `ManageStaffAccounts`) posting
  Workflow/AssignEngineer while the lease is held in ReportPreparation;
  "Download export" kept for the journey pin. `Create.cshtml` on the
  vocabulary (hint copy dropped; "Nothing in this file said where the
  vehicle is" kept for its test pin).
- Tests: `RecordBar` → `class="record-bar"`; D11 theory asserts absence of
  Open Assessment when access is refused; new `SendToEvaRendersOnlyInReview`,
  `ReportSentRendersOnlyWithDetectedEvidenceWhileWithEngineer` (mailbox
  visible, handles/hashes not), `SectionQueryAndTabAliasesSelectTheSameSection`;
  fixture store gains `State` and `AvailableReportSentEvidence`; custody
  and chase pins retargeted to `?section=files` / `?section=notes`;
  approval test posts its own `approvalId` and asserts no `artifactSha256`
  input; `OperatorJourneyTests` retargeted ("Edit Case", files section).

### What CASE-027 / CASE-029 / CASE-030 / ENG-025 must know

- Valuations is accepted as `?section=valuations` but has no nav item until
  it has content (group rule: no inert control). CASE-027 adds the nav row
  to `DetailsModel.Sections`.
- `.workflow-stepper` is a 5-column grid in site.css for four D3 stages —
  wave 5 (site.css owner) should make it `repeat(4, …)`.
- `AssessmentAccessPolicy.CanOpen` still opens Review + exported; D11 says
  never Review. The view follows `CanOpen`; ENG-025 owns the policy.
- "Unsaved" chip omitted (no dirtiness script; a static chip would lie).
  The `[data-edit-toggle-off]` confirm covers Discard as well as Finish.
- Create upload link posts with no dialog (the handler has no fields);
  CASE-029 adds Recipient/Reason. `_ReasonDialog` cannot carry a select or
  radios; CASE-030's dialogs need a body slot or their own markup (as here).
- Report approval has no UI now; if a route other than the report page
  needs it, that is a new control, not this one restored.
- `EditModeDisplay.HeldBy` names no time, so "until T." is not rendered.
