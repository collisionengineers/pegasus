# Research — CASE-012 Case workspace frame, Overview and action bar

Wave 2 lane E1 of [[EPIC-011]]. Contract: group context §1.8 and
`docs/design/README.md` §Case workspace, plus decisions D3, D10, D11 and the
inherited [[PLAT-015]] scope (named staff, no raw ids/hashes, no inactive
controls, no `_CaseWorkflow` narration).

## Diff estimate

About 1,300 lines touched: `Details.cshtml` rewritten (~330), `Details.cshtml.cs`
(+120), `_CaseSummary.cshtml` rewritten (~260), `_CaseWorkflow.cshtml` deleted
(-380), new `_CaseWorkspaceNav.cshtml` (~30), new `_CaseVehicle.cshtml` (~90,
moved content), new `_CaseFiles.cshtml` (~110, moved content), `_CaseHistory`
(+60 moved chase content), `Eva/Send.cshtml(.cs)` (~150), `Create.cshtml`
(~80 class swaps), five test files (~120).

## Read-only checks performed (verified)

- `origin/dev` = 5ca2572c (PLAT-029 shell merged; CASE-024 heartbeat merged).
- `site.css` (dev) declares `.record`, `.record-bar`, `.record-bar-end`,
  `.record-ribbon`/`.ribbon-item`/`.ribbon-label`/`.ribbon-value`,
  `.presence-strip`/`.presence-dot`, `.edit-bar`, `.case-workspace`,
  `.case-section-nav`, `.case-main`, `.case-context`, `.workflow-stepper`
  (5-column grid), `.workflow-step(-icon)`, `.is-complete`/`.is-current`,
  `.workflow-exception`, `.blocker-list`/`.blocker`/`.blocker-actions`,
  `.case-overview-panel`/`.case-overview-grid`/`.overview-facts`,
  `.accident-card(-title|-meta)`, `.definition-list`/`.definition`,
  `.dialog-*`, `.notice--*`, `.status--*`. The prototype's `WORKFLOW_STAGES`
  has four entries (Not ready, Review, With Engineer, Complete) while the
  stepper grid is `repeat(5, …)` — a site.css mismatch owned by wave 5.
- `site.js` (dev) hooks: `[data-edit-toggle-off]` (finish-edit confirm),
  `[data-edit-renew]` (hidden when the heartbeat runs), `[data-edit-heartbeat]`,
  `[data-edit-save]` (Ctrl S), `[data-refresh-form]` (F5), `[data-dialog]`
  + `[data-dialog-open]`/`[data-dialog-close]`, `main[data-workspace-record]`
  from `ViewData["WorkspaceRecord"] = (Href, Label)`.
- `_ReasonDialog` takes `DialogId`, `DialogTitle`, `DialogActionUrl`,
  `DialogHiddenFields`, optional `DialogConsequence`; the field is named
  `Reason` (binder matches `reason`).
- `_PageHeader` renders a single primary action only; Back + Refresh needs
  the `page-header`/`page-title`/`page-actions` markup written directly.
- `_FreshnessBanner` model is `DateTimeOffset?`, `RefreshFields` for the GET.
- `AssessmentAccessPolicy.CanOpen` = Review or ReportPreparation with an
  export at or after the latest Review version; `AssessmentAccessState`
  carries `LatestExportVersion` (the "exported" fact for Download EVA
  package). Review+exported still opens — D11 says never Review; the view
  renders on `CanOpen` as briefed and the gap is reported.
- Core `ApprovedMailboxReportSentEvidence` carries `MailboxIdentity`,
  `SentAtUtc`, `DiscoveredAtUtc`, `LinkedAtUtc` — enough for the D10
  statement without ids or hashes.
- `IListStaffAccounts` requires `ManageStaffAccounts` (Administrator);
  Engineers/Users would be refused. `IStaffAccountQueries.ListAsync/GetAsync`
  (the port `ActorDisplayNames` uses) is the fit for named Engineer
  selection and display.
- `EfCaseDataStore.SetConfirmed`: a null field on Save removes the confirmed
  value, so the six-field edit form must carry the other editable fields as
  hidden inputs with their current values.
- `AssignEngineerAsync` (infra) requires `ReportPreparation`; the export
  writes the First-sent-to-Engineer proxy but does not transition state;
  `Workflow/StartWork` is the Review → ReportPreparation transition and has
  no control in §1.8.
- `RequireClosureIsAllowed`: `PostReportComplete` only from `PostReport`.
  `ValidateReopen`: readiness only for the Review destination;
  ReportPreparation needs an assigned Engineer; PostReport needs Sent
  evidence. `ValidateClose` refuses CreatedInError/SourceEmailUnlinked.
- `Eva/Send` redirects unless Review; `OperatorJourneyTests` pins the link
  text "Send to EVA" and the button "Download export".
- Test pins on the current page: `class="record__bar"` (RecordBar helper),
  `data-edit-authority`, "Case locked - X is editing", "Editing cannot be
  taken over", "Edit case"/"Finish editing"/"Recover editing", "Hold case",
  "Release hold", "Transition to report preparation", "Record manual
  chase" (+ `attemptedAtUtc` input), "Approve report" (+ `approvalId`
  input), "Copy this secret now", "Open assessment", "Send to EVA",
  `?tab=evidence`, "Retry custody" with a labelled Reason on `/Cases/{id}`.
- `catalogue.json` lists `Cases/Details` (default/unavailable/conflict) and
  `Cases/Create` (default); no `Eva/Send` entry.

## Assumed (not checked)

- The fixture case in `RecordingCaseDetailsStore` is `NotReady` with no
  export; browser-seeded cases reach Review with completeness all true.
