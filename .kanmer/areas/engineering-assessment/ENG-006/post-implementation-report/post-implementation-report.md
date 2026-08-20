# Post-implementation report — ENG-006

Branch task/eng-assessment-page (eb746784). Delivered every limb:

- **Copy strip**: 39 hint sentences removed across the assessment sections; 24 "Required." hints became `label.req` markers (`::after " *"`) with `aria-required` on the control; the three explainer status-cards, both explaining captions, the Working column, the remove-and-refit note and the section ledes are gone.
- **Estimate containment**: the repair-lines entry grid dropped "Where this came from" and "Why this line" (unbound provenance micro-columns) and its `min-width` fell 78rem → 56rem; wide tables stay in their scrolling `.table-wrap`.
- **Damage diagram**: nine clickable region buttons in one POST form — a click saves `assessment.impact_location` via `OnPostSaveDamageAsync` → lease acquisition → `ISaveAssessment` (the existing seam the MCP route uses); the saved region renders `aria-pressed="true"` highlighted; the Impact location dropdown preselects the same saved value; the report draft already requires/prints `ImpactLocation`, so saving is the export.
- **Method preselect**: radios check from `CaseDetails.Data.Inspection.Mode.Current` (QDOS default Image Based Assessment recorded server-side).

Tests: new `AssessmentDamageAndCopyWebTests` 8/8 (6 no-copy sections, damage save round-trip with recorded `SaveAssessmentRequest` + highlight re-render, method preselect); AssessmentVehiclePrefill/EstimateImport/SendToAi 12/12; `AssessmentPolicyTests` 20/20; Release build 0/0.

Deviation: subagents barred — self-reviewed.

## Verification hand-off
Post-deploy: assessment sections carry no hint sentences; clicking a damage region persists and highlights; IBA case preselects the method radio.
