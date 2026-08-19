# Files — INTK-006

## Change surfaces

| Path/module | Why it is touched | Risk |
|---|---|---|
| `src/Pegasus.Web/Pages/UploadStatus.cshtml.cs` | Map the existing processed receipt decision/case state into honest operator wording and next action. | Creating a second decision taxonomy instead of using Core/Presentation labels. |
| `src/Pegasus.Web/Pages/UploadStatus.cshtml` | Show `Needs sorting` and an appropriate receipt/queue action instead of generic `Complete`. | Disclosing internal vocabulary or implying case creation should have occurred. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeWorkStore.cs` / queued-status query implementation | Only if the current `QueuedIntakeStatus` projection lacks the canonical receipt decision required by the UI. | Widening a port for one page when an existing query/helper already exposes it. Search first. |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | Possible minimal status projection change if the decision is not already carried. | Optional-field/wrapper smell; preserve one Core owner. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Reuse or add one presentation mapping for terminal intake outcomes if no existing label fits. | Duplicate label table. |
| Focused Upload/queued-status integration and browser tests | Reproduce a completed JPEG `NeedsSorting` receipt with no case and assert visible outcome/action. | A test that only asserts “Complete” and misses operator comprehension. |

## Ripple effects

- INTK-005 may change the same Upload/status surfaces; sequence or coordinate the tickets to avoid overlapping implementation.
- PLAT-006/DELIV-011 changes Upload and status presentation but not business outcome projection; implementation must start from its merged result.
- Do not change `ProcessIntake`, image recognition thresholds, principal identification, or Case allocation: production evidence shows those gates behaved correctly.
- Web telemetry remains absent by documented architecture; adding it is broader OPS-07 work, not necessary to correct this UI defect.
- The recurring Sent-evidence polling exception is unrelated and must not be “fixed” under this ticket.

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/frd/frd-02-intake-and-source-identity.md` | Receipt/decision semantics and fail-closed intake. |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Image/VRM suggestions do not establish Case allocation. |
| `docs/operator-notes.md` | Upload success is explicitly not case creation. Protected business truth. |
| `src/Pegasus.Web/Pages/Upload.cshtml.cs` | POST ends at durable staging and redirects to status. |
| `src/Pegasus.Web/Pages/UploadStatus.cshtml(.cs)` | The generic terminal message that hides `NeedsSorting`. |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | Canonical terminal decision owner. |
| `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` | Post-persistence image recognition/registration, distinct from Case allocation. |
| `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs` | Direct-image `NeedsSorting` contract. |
| TICK-011 documents | INT-17 scope, caller, production qualification, and non-Case boundary. |
| `docs/operations.md` | Release-10 production topology and known Web/Worker telemetry boundary. |

## Deliberately out of scope

- Creating a case from an unowned/unidentified image.
- Changing recognition engine/model/threshold or claiming production INT-17 recognition from this evidence.
- Adding Web OpenTelemetry, repairing Sent-evidence polling, deploying, or changing cloud state.

## Corrected primary change surface — 2026-08-19

The exact trace promotes these from context to primary implementation surfaces:

| Path/module | Required investigation/change |
|---|---|
| `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` | Replace the below-bar/no-readable third path with the operator-required Image-Only case fallback while preserving unique-match association. |
| `src/Pegasus.Core/ImageIntake/ImageIntakeCasePairing.cs` | Confirm overlap/unique-match rules are shared rather than reimplemented. |
| Existing Core case-creation port/use case | Reuse the sole Case allocation owner for Image-Only creation; do not create a parallel case writer. |
| `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` | Determine whether an Image Intake record must precede both association and Image-Only case creation, and preserve immutable receipt/reference identity. |
| `tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs` plus persistence/web tests | Add low-confidence, no-readable, ambiguous, unique-match, and fallback-case regression coverage. |

FRD-06 and the appropriate Case/Reference governing document need behaviour reconciliation before planning because current threshold-gated semantics do not state the exhaustive two-outcome rule.
