# CASE-049 research

## Question

Why does ready Case handoff still require two actions and native estimating still require EVA?

## Findings

- At integrated dev4d7ad4a0d2593300fd02527838aa2f1cf6555860, AssignCaseEngineer validates Review, eligible Engineer and sign-off defaults, but EfCaseWorkflowStore.AssignEngineerAsync sets only assignment fields. StartCaseWork performs the separate transition. The only assignment form is inside _EvaHandoff; _CaseWorkflow offers a second Start report preparation dialog.
- AssessmentAccessState carries latest Review/export versions. Both EfAssessmentAccessSource and EfAssessmentWorkspaceSource query workflow history and EVA proxies solely for this obsolete prerequisite. CanOpenReports already omits it, duplicating the same lifecycle state set. Details mutation guards and estimate imports still use CanOpen and are blocked without EVA.
- Current FRD-11/D30 makes Engineer sections viewable, writable With Engineer and read-only when Complete/terminal; its report generation explicitly does not require EVA. FRD-01/12 still describe EVA as the implicit review act. Current operator instruction explicitly replaces that with native Engineer handoff; update only those affected behavioral statements. Historical protected operator notes remain historical, with the current directive controlling this ticket.
- MutateAsync already owns serializable state, lease, expected version, operation identity/replay, before/after history and lease settlement. Reuse it; no schema/store/worker needed. Report Sent evidence checks state_ReportPreparation or case_reopened_ReportPreparation history, so handoff must record the existing state transition event rather than leave an assignment-only history record.
- Existing tests cover assignment eligibility, persisted completeness, stale version/lease, workflow/reports and real Case browser handlers. Extend these; no test framework. Optional EVA exports/proxies remain evidence for their own workflow, not native prerequisites.
- Historical CASE-040/ENG-034/CASE-047 claims belong to merged predecessor work and are preserved. No fresh live implementation lane owns this residual change. ENG-041 currently edits Details/test fixture; wait for its merge before source edits. PLAT-072 owns schema/check-box removal, not this handoff change.

## Implications

One guarded assignment/transition is the human handoff. Keep actual headless transition callers where they represent a valid handoff to the already assigned Engineer; remove the redundant mandatory UI step, not an unrelated valid command. Remove the obsolete export tuple and queries; one Core state/read-only policy serves native sections/reports. No manual reviewed checkbox, no auto estimate/report save and no change to external EVA delivery assertions.

## Open questions

None: current user statement establishes native handoff and optional EVA. Focused UI uses the existing dialog/action style and existing eligible Engineer data.
