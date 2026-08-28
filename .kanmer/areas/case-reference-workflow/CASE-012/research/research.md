# CASE-012 research — Case workspace frame + Overview (EPIC-011 wave 2, lane E1)

Branch `task/case-012-case-workspace` from `origin/dev` @ 108f3c41.
Binding contracts read: EPIC-011 `context.md` §1.8 + D1–D14, `waves.md`,
`docs/design/README.md` §Case workspace (line 923), FRD-01 §lifecycle actions,
FRD-07 §EVA handoff, FRD-12 §Case workspace, prototype final render layer
(`caseActionBar`, `caseEditBar`, `sectionOverviewV2`, `workflowStepper`,
`blockerMarkup`, `caseContext`, `caseSections` final splice at prototype
line 1926).

## Premises verified by read-only check

1. Design system ready on dev: `site.css` carries `.record-*`, `.presence-strip`,
   `.edit-bar`, `.case-workspace`, `.case-section-nav`, `.case-context`,
   `.workflow-stepper`, `.blocker-*`, `.case-overview-*`, `.overview-facts`,
   `.accident-card`, `.context-card`, `.decision-row`; `site.js` carries the
   div-backdrop dialog system (`[data-dialog]`, `[data-dialog-open]`,
   `[data-dialog-initial-focus]`), edit-finish confirm, edit heartbeat,
   `data-edit-save` (Ctrl+S), `data-refresh-form` (F5).
2. Dialog openers must be `type="button"` (the div-backdrop binding does not
   `preventDefault`, so anchors would navigate as well as open) — convention
   confirmed in `Mail/Message.cshtml`. Consequence: with script off a dialog
   control is inert; the no-script route for the EVA export is the unchanged
   `Eva/Send` page (reached by URL, nothing on the case links to it —
   reported to review).
3. Edit-lease machinery (CASE-024 heartbeat, KANMER-005 exclusivity) is merged:
   `RestoreLeaseState`, `ClaimLeaseAsync`, `ReleaseLeaseAsync`,
   `HeartbeatLeaseAsync`, `_EditHeartbeat`, `_EditFinishConfirm`,
   `EditModeDisplay`. Reused as-is; no lease logic reimplemented.
4. EVA seams exist: `ISubmitCaseToEva` (Eva/Send `Submit` handler),
   `IExportCaseBundle` (Documents/Export `Bundle` handler, POST-only),
   `IAssignCaseEngineer` (Workflow `AssignEngineer`). Core enforces
   Review-only export (`CaseNotInReviewException`, EvaHandoffStore line 71) —
   so the EVA control is offered in Review only; the prototype's final render
   agrees (`caseActionBar`: EVA button only when `state==='Review'`; label
   "Download EVA package" once exported, else "Send to EVA").
   §1.8's parenthetical "(With Engineer or Complete, exported)" cannot be
   implemented honestly against that Core gate — deviation recorded in plan.
5. Exported marker: history event `eva_bundle_exported` (EvaHandoffStore
   line 157, private const) — promoted to a public Core const and reused.
6. Engineer display/selection: `IStaffAccountQueries` (DI-registered,
   documented host port) + `ActorDisplayNames` convention. Admin-gated
   `IListStaffAccounts` is NOT usable for the staff-facing selector; the raw
   query port is the sanctioned read path (same one ActorDisplayNames uses).
7. Save semantics: `EfCaseDataStore.ApplyEditableData` overwrites every
   editable field from the post — a 6-field-only form would clear the other
   12 values. The drawn 6 fields render visible; the other 12 post hidden
   with their current confirmed values.
8. `AssignEngineerAsync` sets the engineer without a state change; the
   Review → With Engineer move is `ITransitionCase` (`StartWork`), whose only
   surface is this page — kept as an edit-mode action (undrawn, required).
9. Assessment access: `IGetAssessmentAccess` is the one owner the page calls.
   Correction (review round 1): today's `AssessmentAccessPolicy` still
   permits Review once `LatestExportVersion >= LatestReviewVersion`; the D11
   "With Engineer onwards, never Review" policy is ENG-025's named scope, and
   this page simply renders the access decision rather than restating either
   rule. Original premise overstated D11 as already server-enforced.
10. `AddNote` needs no lease; hold/release/close/reopen/save/assign need the
    lease token (FRD-01: every mutation carries lease + version).
11. Wrong-principal closure is atomic: `CaseLifecycle.ValidateClose` refuses
    a bare `CreatedInError` outcome (CaseLifecycle.cs:482-486 — "requires the
    atomic corrected-principal replacement action"), so
    `Workflow/CreateLinkedReplacement` is the only route and its dev markup
    surface was the old `_CaseWorkflow` form. Verified when restoring the
    dialog in review round 1.
12. Existing lane tests live in
    `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` (+ its partials),
    `CaseClosureWebTests.cs`, `CaseWorkflowWebTests.cs`,
    `CaseReportApprovalWebTests.cs`, `CaseEditModeWebTests.cs`, and the
    browser journeys in `Browser/OperatorJourneyTests.cs` — the web tests
    pin the current markup and were retargeted; the closure/workflow/edit-
    mode partials post to handlers directly and needed no changes.

## Assumed (not verifiable without a browser; build-only lane)

- The wave-1 CSS/JS render as authored for the classes above (proof is the
  orchestrator's browser walk, not this ticket).
- `section` query param routing is acceptable over path segments (design
  README routes table names `?section=` — verified; listed here only to note
  no client route generation depends on path segments).
