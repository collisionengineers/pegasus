# B04 review fixes — implementation record

Integrated into `task/pegasus-v1-casework` as `4a6820611` (squash of helper branch `b-work/b04r`: 6677cc0ab, f69679513, bd615ea6c, b29fc09cd, 0ee8b7d30; base `2a0660870`). Opus implementer under claude-fable-b orchestration, 2026-09-06. Fixes Stream A's two findings on B04 phase 2a (PR 672 comment 5561957465).

## Delivered

1. **Separate paint labour rate removed.** `EstimateDetails.PaintLabourRate` and its validation (Estimates.cs); the import header (EstimateImport.cs); `EfRepairSpecificationStore` writes the Foundation column null and no longer reads it (schema untouched); the editor field `estimatePaintLabourRate` and read-only display (`_CaseEstimate.cshtml`); `EstimateEditorPost.PaintLabourRate`, `Money(form["estimatePaintLabourRate"])` and every constructor argument (Details.cshtml.cs); parsers never populated it. Ten B-owned test files updated for constructor arity; no assertion weakened. The only edit outside B-owned files: one positional `null,` deleted from Stream A's `AssessmentMcpTools.cs` estimate save so the branch compiles — isolated in helper commit f69679513 and disclosed to A on PR 672 with the offer to re-apply as an A-authored patch. Now-unread C labels `EngineerLabels.PaintLabourRate`/`.PaintLabourRatePerHour` reported, not edited.
2. **Amendment attribution.** `TimeProvider` injected into `DetailsModel`; `OnPostSaveEstimateAsync` takes the server time once and, per line matched by `lineId`, `EstimateLineAmendment.Stamp(saved, loaded, actor, now)` sets `AmendedBy`/`AmendedAtUtc` when any of the eight editable values differ, keeps the prior attribution when unchanged, leaves new rows untouched. Operation compared via `EstimateOperations.FromLineType` on both sides so the lossy type round trip is not an amendment.
3. **Regressions.** `tests/Pegasus.IntegrationTests/EstimateLineAmendmentTests.cs` (12 cases, no host; `InternalsVisibleTo` already covers Pegasus.IntegrationTests and Pegasus.ArchitectureTests). `AssessmentEstimateImportWebTests.SavingTheEditorStampsTheChangedLineAndKeepsTheUntouchedOnes`: real editor GET, real form POST changing one line, reload through the page's read path; asserts the changed line's actor and fixed clock, the untouched line's original stamp, origin/materials/row identity survival and discounts/VAT/rate round trip. The recording store's `SaveEstimateRequest` handler now applies an existing-estimate save and maps every recorded fact.

## Verification (Windows, PowerShell 7, Release)

| Where | Check | Result |
| --- | --- | --- |
| helper | solution build | 0 / 0 |
| helper | Core `~Estimate\|~RepairSpecification\|~AssessmentPolicy\|~AssessmentReportProjection` | 153 passed |
| helper | integration `~AssessmentPersistenceIntegrationTests\|~EstimateParser\|~Audatex\|~EstimateLineAmendmentTests` | 77 passed |
| helper | Architecture; `~CaseWorkspacePersistenceTests` | 100; 10 |
| squashed tree 4a6820611 | build; full Core; Architecture; estimate/workspace/report persistence + amendment | 0/0; 1477/1477; 100/100; 98/98 |
| combined tree (A 9028aa12b + B) | `AssessmentEstimateImportWebTests\|CaseEngineerSectionsWebTests\|CaseDetailsWebTests.Estimate*\|AssessmentReportDraftWebTests\|AutomationAssessmentIngressTests\|EstimateLineAmendmentTests` | 51 / 51 |

## Deviations

- The one-token edit in A-owned `AssessmentMcpTools.cs` (see above).
- Test UI snapshots unchanged: no snapshot captures the estimate editor form.
- The VAT-status derivation on save and the Estimate section's VAT/discount controls remain the B08 follow-up recorded in `plan/b04-phase2a-report`.

## Simplification pass (2026-09-06)

The helper kept the comparison in one internal helper and one stamping site; nothing further applied or deferred.
