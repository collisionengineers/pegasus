# Case record — Place on Hold and Release Hold — how it works

Read from the live source on 13 September 2026.

- Place on Hold: one dialog with a Reason field. Posts `Hold` on `Pages/Cases/Workflow.cshtml.cs`; Core `IPutCaseOnHold` with `PutCaseOnHoldRequest` (Case id, expected version, actor, operation key, reason, edit-lease token). No date is recorded.
- Release Hold: one dialog with a Reason field. Posts `ReleaseHold`; Core `IReleaseCaseHold`.
- Effect on the chase schedule: holding pauses the missing-material chase (held-at and the remaining interval are kept); releasing resumes it. Closing, archiving or replacing the Case stops it (`CaseChaseState.Stop`).
- Where a held Case shows: the Held metric and Cases › Held tab, and the Work Centre as a Held decision row, whose chip today borrows the chase date.

Governing documentation: [FRD-01](../../../../../../../docs/frd/frd-01-case-identity-and-lifecycle.md) ("the single named action to place a pre-report Case in Held pending staff decision"), [FRD-12 § Work Centre](../../../../../../../docs/frd/frd-12-operator-experience.md#work-centre).
