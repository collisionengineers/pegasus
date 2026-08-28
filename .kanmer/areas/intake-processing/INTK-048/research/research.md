# Research — INTK-048: manual Case links stay Unidentified

## Question

Why did U38 and U39 remain open after staff added their retained material to
QDOS26030, and what is the smallest change that restores the existing
supersession contract?

## Findings

- Live SQL readback showed active `IntakeManualAssociations` from both origin
  receipts to QDOS26030, written at 10:16:44Z and 10:16:45Z. Matching
  `intake_case_linked` workflow events exist on the Case.
- Both U38 and U39 remained `Open` after repeated successful executions of
  `StagedArtifactReconciliationFunction`. The timer and worker are healthy.
- `UploadCaseDecision` reuses `ILinkIntake`; the durable association and
  workflow event prove the link transaction succeeded. The queue state, not
  the link, is the failed outcome.
- `IntakeReceipt.CurrentCaseId` and `CurrentCaseReference` already define the
  effective association, preferring the active manual association over the
  original accepted association.
- `ReconcileUnidentifiedDestinations.ResolveForReceiptAsync` returns before
  looking for a destination when `ProcessIntake.IsUnidentifiedEligible` is
  true, then accepts a Case only when `Decision == CaseCreated`. Staff linking
  changes the effective association but deliberately does not rewrite the
  immutable original processing decision.
- Existing tests cover automatic Case creation, Image Intake promotion, Triage
  promotion, and genuinely unidentified receipts. They do not cover an
  eligible receipt with an active manual Case association.
- FRD-02 already requires automatic resolution when an origin receipt reaches a
  formal Case and requires genuine unidentified work to remain open.
- One SQL deadlock occurred in `UnifiedWorkFunction` near the original intake
  processing time. It is not on the manual-link or reconciliation path and does
  not explain the durable links plus persistently open U-items.

## Implications

The existing reconciliation owner should treat `CurrentCaseId` as a real
destination before using the original decision to decide that material is still
unidentified. The web action, persistence transaction, worker schedule, schema,
and FRD need no change. Existing effective-association helpers avoid a second
copy of manual-versus-accepted precedence.

## Open questions

None. The governing behavior and the effective association owner already exist.
