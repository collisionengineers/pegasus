# Plan — CASE-007

Branch task/case-007-case-page from origin/dev (717eaf9f). Steps, each naming reuse:

1. **Section economy** (reuse: the new design rule + existing `hasLease`/edit-context booleans in `_CaseWorkflow`): each of Lifecycle actions, Immutable report approval, Report-Sent evidence, Case tasks, Vehicle evidence, Case custody, chase-draft panels renders only when it has content or an action available in the current mode; edit-only panels render only under an active lease. The Case detail panel stays (it carries values).
2. **EVA card** (reuse: ENG-003's `<details>` readiness-chip pattern + `_StatusChip`): one compact card — state line (Can generate / N items outstanding disclosure with operator-worded items), Generate form only when available; every dev-speak bullet rewritten in business words or dropped where it duplicates the readiness list.
3. **Edit toggle** (reuse: existing acquire/release lease handlers + reason-dialog pattern): the action bar gets one toggle ("Edit" / "Editing — finish"); toggle-off posts release; with unsaved changes, `site.js` dirty tracking opens a dialog (Save changes → submits the open section form; Discard → release; Keep editing → close). The Edit-mode panel disappears.
4. **Copy**: `EfCaseAcceptanceStore` writes "Details are incomplete"; `OperatorLabels.ChaseReason` maps the stored legacy "Accepted intake is incomplete" for display (no data migration); inspection-mode raw enum → `OperatorLabels.InspectionMode`; remaining narration lines removed per the T0 rules.
5. **CSS**: `.detail-list` rows contain their icons (the escaping glyphs on the Dates panel).
6. Tests: update assertions on removed panels/copy; keep INTK-013 badge parity untouched; focused suites: CaseDetailsWebTests, CaseWorkflowPersistenceTests (web slices), browser assessment/case suites; Release build 0/0.

Deviation: subagents barred — self-review in scratch.
