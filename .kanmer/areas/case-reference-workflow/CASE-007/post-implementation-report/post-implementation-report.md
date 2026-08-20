# Post-implementation report — CASE-007

Branch task/case-007-case-page (0ffeab79). Delivered every limb:

- **Section economy**: read-only renders only populated sections — Lifecycle actions (edit-only), Immutable report approval (recorded or offerable), Report-Sent evidence (rows or unlink), Case tasks (rows or edit), Vehicle evidence (evidence or edit action), Case custody (rows), EVA (revisions or edit) — empty-state panels gone per the new design rule. A fresh Not-ready case's page now ends shortly after Chase history.
- **EVA card**: compact; outstanding items behind the assessment page's disclosure-chip pattern; Core reasons rewritten in operator words ("EVA hand-off is not switched on." / "Available while the case is in Review." / "At least one stored vehicle image is required." / "Completeness has not been confirmed."); generate button plainly "Generate EVA handoff"; revision line keeps "integrity verified", drops the version integer.
- **Edit toggle**: action bar Edit case / Finish editing / Renew editing / Recover editing on the existing lease handlers; held-by-another shows a visible compact note (name, availability, cannot-take-over — no GUIDs); the Edit-mode panel is gone. Finishing with unsaved changes opens the Save / Discard / Keep-editing dialog (`site.js` dirty tracking on lease-carrying forms).
- **Copy**: chase reason writes "Details are incomplete" and legacy stored rows display-map ("intake" is off the page); inspection mode renders words via `OperatorLabels.InspectionMode`; page explainers removed; datarow CSS flexes so provenance icons stay inside panels.

Tests: CaseDetailsWebTests + CaseEditModeWebTests 23/23 (holder/recovery/vocabulary tests retargeted to the bar and note — same guarantees, new surface); OperatorJourneyTests 4/4 (flow reworked for the toggle + disclosure; revision-count asserts replaced by an occurrence-stable check); Core 842/842; Triage/Custodial/Chase/RailCounts 11/11; Release build 0/0.

Deviation: subagents barred — self-reviewed.

## Verification hand-off
Post-deploy: QDOS case pages end after Chase history read-only, no "intake"/dev-speak, toggle + dialog work live.
