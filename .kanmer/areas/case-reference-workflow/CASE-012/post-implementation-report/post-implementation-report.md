# Post-implementation report — CASE-012 (parallel run, branch `task/case-012-case-workspace-parallel`)

## Stop condition: two implementations of one ticket

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

## What this run shipped (differences from PR #599 worth comparing)

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

## What CASE-027 / CASE-029 / CASE-030 / ENG-025 must know

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
