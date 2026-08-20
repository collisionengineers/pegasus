## Independent review — PR #465 (orchestrator, 2026-08-20)

Verdict: **pass**, merge on green CI.

- The decision step is a staff decision through existing Core machinery, exactly as the fail-closed rules demand: `UploadCaseDecision` orchestrates search (`ISearchCases`) and attach via `IGetCase` → `IAcquireCaseEditLease` → `ILinkIntake` (which already owns the ImageIntake merge transition), with deterministic replay keys — no second association pipeline, no silent automation beyond INT-28's bar.
- Report-not-reoffer preserved (INTK-010's constraint); a staff link is never worded as automation's (Core-derived `AssociationWasStaffDecision`), and the one-sentence owner (`OperatorLabels.AssociatedWithCase`) resolves the five-site duplication its own lens found.
- The app's first combobox is properly accessible (script-added ARIA, keyboard complete, abortable debounced fetch) and the axe scan caught + fixed a real contrast defect in passing.
- The flaky-risk test (image-group merge by typed reference) was stabilised the honest way — running the Worker's reconciliation sweep as production does, 5/5 consecutive — not by widening tolerances.
- Simplification pass ran with four lenses and honest dispositions; named follow-ups (Intake/Details unification, store-level image-only test, ordinal-zero lookup convergence note) are quality debts, recorded, not hidden.
- FRD-02/FRD-12 updated. 6/6 + 13/13 + browser/a11y suites green.
