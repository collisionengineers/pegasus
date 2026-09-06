# B08 estimate controls — implementation record

Integrated into `task/pegasus-v1-casework` as `125f5638d` (squash of helper branch `b-work/b08e`: bb494901e, 6dfe552fe, ba613f982, 766b7814f; base `4a6820611`, landed over G19 `4e15248d6`). Opus implementer under claude-fable-b orchestration, 2026-09-06. Closes the deviation recorded in `plan/b04-phase2a-report` (VAT status derived from the percentage on save).

## Delivered

- Core (`Estimates.cs`, `EstimateImport.cs`): `EstimateDetails.VatPolicy` stands on `RepairerVatStatus.Unknown` when no policy is recorded (charges nothing, blocks Use as Current); imports record no policy because no parser reads a VAT position.
- Editor (`_CaseEstimate.cshtml`, `Details.cshtml.cs`): `estimateVatStatus` select; four category checkboxes (`VatCategoryField(category)` names, hidden false companions per the page convention); `estimateDiscountParts|Materials|Specialist|Overall` percentages converted to fractions (`EstimateDiscount.PercentValue` is the one conversion); override recorded when the posted set differs from `DefaultFor(status)` or the status is Unknown; a non-enum status falls back to Unknown before Core (`Enum.IsDefined`, closing a 500 path); Use estimate gated with its `data-condition` while `BlocksAcceptance`. Read-only header shows status, charged categories and discounts as values. Labels in `CaseWorkspaceLabels`. Rate card: no B-owned query over `LabourRateCards` exists, so the one hourly-rate input stays and `Rate` remains null (picker deferred).
- Tests: `EstimateTests`, `AssessmentReportProjectionTests`, `AssessmentReportRenderingTests`, `AssessmentPersistenceIntegrationTests` state the policy each fixture means; new proofs for the unrecorded case (Core + persistence), and web proofs in `AssessmentEstimateImportWebTests` for the post/render round trip, the Unknown-with-categories override that unblocks, and the gated control with its condition.

## Verification (Windows, PowerShell 7, Release)

| Where | Check | Result |
| --- | --- | --- |
| squashed tree 125f5638d | build; full Core; Architecture; estimate/workspace/report persistence + amendment | 0/0; 1477/1477; 100/100; 98/98 |
| combined tree (A 9028aa12b + B) | `AssessmentEstimateImportWebTests\|CaseEngineerSectionsWebTests\|CaseDetailsWebTests.Estimate*\|AssessmentReportDraftWebTests\|AutomationAssessmentIngressTests\|NoWorkspaceGate\|ElevenOrdered` | 44 / 45 |

The one combined failure is A-owned `AutomationAssessmentIngressTests.EstimateSaveRequiresTheHeldEstimateJobAndLandsAsAnUnconfirmedAiDraft` (line 878 asserts VAT 81.08 under the old derivation); an AI draft with no recorded policy is Unknown → VAT 0. Reported to A with the two options (assert the honest figures, or add an explicit VAT status input to the MCP save).

## Simplification pass (2026-09-06)

Run by the helper with a code-simplifier reviewer over the slice diff: 4 findings applied (one percent conversion, dead blank guard removed, category list as `IReadOnlyList`, undefined status fallback), 2 not applied with reasons (page-model static field-name helper kept — established convention; literal discount field names kept — convention for header fields), 5 considered-and-rejected recorded in code comments (explicit category list over `Enum.GetValues` to keep order and exclude None/All — a fifth Core category must be added there).

## Deviations

- Rate-card picker deferred (no B-owned query yet); `EstimateRateSnapshot` persists when a caller supplies one.
- Test UI snapshots unchanged: no snapshot captures the estimate editor form.
