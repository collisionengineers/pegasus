\# Full check: report and fee note generation route



\## Context



The operator asked for a full check of the assessment-report and fee-note generation process — the whole route and its associated files. Deliverable chosen: \*\*a findings report only, no code changes\*\* (operator answer). Two behaviour decisions were also taken and will be recorded as the recommended fixes: (1) a separate fee-note request should \*\*attach to the Case's current generation\*\* rather than supersede it; (2) workspace Save should stale the generation \*\*only when a printed fact actually changed\*\*.



Route checked (dev @ `9c4c09eb7`, clean for these files):

`\_CaseReport.cshtml` + `Details.cshtml.cs` handlers → `Core/Reports/CaseReportGeneration.cs` (`GenerateCaseReport`) → `EfCaseReportGenerationStore.FreezeAsync` (readiness, projection, snapshot hash, artifact row) → `EfCaseReportContentSource.ComposeAsync` → `QuestPdfAssessmentReportRenderer` + `AssessmentReportLayout` → `EfCaseArtifactCustody.RetainAsync` → `ConfirmArtifactAsync` → `CaseReportDeliveryPreparation` / `EfCaseReportDeliveryPreparationStore` → staff send. Spec owners: FRD-11 §§ Initial renderer activation, Report generation entry point; ADR-0050; `reference/rendererref1/DESIGN\_SPEC.md`. Tests: `tests/Pegasus.Core.Tests/Reports/\*` (4 files, 101 facts/theories) and `tests/Pegasus.IntegrationTests/Reports/\*` (6 files).



\## Output



One file: `artifacts/audits/2026-09-16/report-and-fee-note-route-audit.md` (artifacts/ is git-ignored, same location and Finding/Evidence/Proposed-change format as `artifacts/audits/2026-09-11/\*`). No source, docs, git or cloud writes. No build/test run is needed (prose-only deliverable; every finding below was verified by reading the cited lines).



\## Findings to record (ranked; all citations verified against dev)



\### A — Defects / spec contradictions



\- \*\*A1 Fee note on a later day (or after any Case version bump) supersedes the report.\*\* `ReportDate` and `CaseVersion` stay inside the hashed material (`EfCaseReportGenerationStore.cs:872-878` blanks only key/actor/time); a non-matching hash creates a new generation and `SupersedeCurrentAsync` sets the old one `Stale` (`:884-896`). Same for report-with-fee-note followed by separate fee note (`IncludeFeeNote` forced false for `Kind=FeeNote`, `:138-139`). Result: report is unsendable (`CaseReportDeliveryPolicy.RequireDeliverable`), new generation holds only the fee note, and the UI shows a stale notice with no reason. Contradicts `ICaseReportGenerationStore.FreezeAsync` contract "requesting the second kind of an existing snapshot reuses that generation" (`CaseReportGeneration.cs:343-346`). \*\*Proposed:\*\* for `Kind=FeeNote`, when a current non-stale generation exists at the same Case version, add the artifact to it and render from its frozen snapshot (same report date); hide "Generate fee note" when the current generation's report already `IncludeFeeNote`.

\- \*\*A2 Every workspace Save stales the generation, unconditionally.\*\* `EfCaseWorkspaceStore.cs:259-268` calls `MarkStaleAsync(…, "case\_workspace\_saved")` with no change check (no no-op short-circuit exists). FRD-11 L117-119: "relevant accepted fact changes mark a generation stale; notes and recipient edits do not". \*\*Proposed:\*\* gate on the already-computed before/after fields, data, `estimateChanged`, `imagesPrepared`.

\- \*\*A3 Two generate forms share one operation key.\*\* `\_CaseReport.cshtml:64,100` both post `Model.GenerateReportOperationKey`; replay lookup ignores kind (`EfCaseReportGenerationStore.cs:75-89`). Back-button "Generate fee note" replays the report artifact and the page says `FeeNoteGenerated` (`Details.cshtml.cs:1707-1709`). \*\*Proposed:\*\* separate `GenerateFeeNoteOperationKey`; replay should also assert the kind.

\- \*\*A4 Printed labour/paint hours do not reconcile to priced labour.\*\* `ReportRepairCosts.For` sums `WorkUnits`/`PaintWorkUnits` over all lines (`AssessmentReportRendering.cs:241-245`) while `EstimateTotals.Compute` excludes specialist hours and stray paint hours (`Estimates.cs:313-337`). `Validate` checks money only. \*\*Proposed:\*\* expose panel/paint hours from `EstimateTotals` and print those.

\- \*\*A5 Fee-note fallback wording is invented.\*\* `AssessmentReportLayout.cs:363-365` prints "Independent automotive engineering assessment" when no description lines are recorded. FRD-11 L39-45 forbids substituted/placeholder wording; readiness does not require description lines. \*\*Proposed:\*\* make description lines a readiness requirement (or accept operator-approved default copy explicitly).

\- \*\*A6 Combined document's fee-note pages lose the VAT number in the footer.\*\* `Footer` keys on artifact kind, not page (`AssessmentReportLayout.cs:116-120`); DESIGN\_SPEC L19 requires it on the fee-note page header and footer. Header is right (`:322`).

\- \*\*A7 Mileage is frozen with the ambient culture.\*\* `AssessmentReportProjection.cs:259-261` `$"{value:N0} {mileageUnit}"`; every other formatter names a culture. Snapshot text/hash depends on host culture (Linux container ⇒ likely no thousands separator). \*\*Proposed:\*\* `en-GB`.

\- \*\*A8 A `Failed` custody upload is never retried.\*\* `RetainCaseAsync` returns the stored status for an existing Pending/Failed version (`EfCaseArtifactCustody.cs:260-266`); the retry path only consults custody for Pending/Unknown with ids (`CaseReportGeneration.cs:700-711`); the worker reconciler confirms only `DocumentVersion`, never `GeneratedCaseArtifacts` (`:759-777` + `SourceDocumentChangedAsync` returns false for generated artifacts). A Failed artifact stays Failed until the hash changes (next London day). \*\*Proposed:\*\* allow re-upload when the stored version is Failed with a matching hash, or let the reconciler confirm the artifact row.

\- \*\*A9 Non-S total loss fails at render with "Retry the operation".\*\* `AssessmentReportSnapshot.Validate` (`:439-443`) fails closed for categories A/B/N/N/A (correct per DESIGN\_SPEC placeholders) but `CaseReportReadiness.Evaluate` does not surface it, so the operator sees the generic catch message (`Details.cshtml.cs:1675-1685`). \*\*Proposed:\*\* add a readiness reason.



\### B — Route/UX hazards



\- \*\*B1 Success paths drop the operator out of edit mode.\*\* `ClearLeaseState()` after generate/prepare (`Details.cshtml.cs:1706,1794`) forgets the browser token while the server lease persists → `CanRecoverLease` on the next render (`CaseMutationPageModel.cs:167-168`); workspace Save keeps editing (`:1505`). Generating report → fee note → prepare requires re-entering edit mode each time.

\- \*\*B2 No re-prepare once a preparation exists.\*\* `canPrepareDelivery` requires `preparation is null` (`\_CaseReport.cshtml:44-48`); send refuses on any Case version change since preparation (`CaseReportDeliveryPreparation.cs:294-298`). A non-staling version bump (case-data edit, automation write) wedges delivery until a material change forces regeneration.

\- \*\*B3 Automation (MCP) writes bypass the stale wiring.\*\* `ISaveAssessment`/`ISaveCase` are used only by `AssessmentMcpTools.cs:174-176`; `EfCaseAssessmentStore.SaveAsync` and `EfCaseDataStore.SaveAsync` bump `workflow.Version` (`:168`, `:140/250`) and never call `MarkStaleAsync`; the projection reads unconfirmed field values (`AssessmentReportProjection.cs:146-150`). Mostly mitigated by unconfirmed-field readiness items, but it feeds B2.

\- \*\*B4 Preview and artifact download 500 instead of refusing.\*\* `PreviewReportDraftAsync` has no try/catch (`Details.cshtml.cs:1578-1606`) unlike the draft download (`:1536-1543`); `OnGetGeneratedArtifactAsync` lets `InvalidOperationException` from `OpenAsync` (`EfCaseReportGenerationStore.cs:654-661`) surface. `DateTimeOffset.UtcNow` at `:1602` bypasses `TimeProvider`.

\- \*\*B5 Freeze/prepare/send races surface as 500.\*\* Catch lists (`:1675-1679, 1784-1786, 1831-1833`) omit `DbUpdateException`/`SqlException` (filtered unique index `(CaseId, SnapshotHash)`, serializable conflicts).

\- \*\*B6 Stale-reason vocabulary drift.\*\* `CaseReportStaleReasons` (`CaseReportGeneration.cs:432-441`) is used only for `SourceDocumentsChanged`/`SignatoryChanged`; stores pass ad-hoc strings (`"case\_workspace\_saved"`, `"asset\_preparation\_changed"`, `"estimate\_accepted"`, `"valuation\_applied"`, `"vehicle\_lookup\_filled"`…). Also `SupersedeCurrentAsync` rewrites state to `Stale` while the interface doc says superseded generations are never rewritten (`:366-367`).

\- \*\*B7 Fee VAT rate lives in two places.\*\* `0.20m` in `AssessmentReportRendering.cs:506` and the literal "VAT @ 20%" in `AssessmentReportLayout.cs:419`; the estimate block derives its label from the estimate (`:920`). Rounding (AwayFromZero, 2 dp) matches DESIGN\_SPEC L31.

\- \*\*B8 Bank/VAT/fee-terms constants are template text not frozen facts\*\* (`AssessmentReportRendering.cs:11-19`): editing them without bumping `TemplateVersion` changes a retried Pending artifact's bytes; bumping it makes every unconfirmed snapshot unrenderable (`:469-472`). The 89-day term wording could not be verified against `reference/rendererref1` (PDF text not extractable); introduced in `cdb50cd2b` "rendererref1 parity".

\- \*\*B9 Dead/duplicate Core surface.\*\* `ICaseReportGenerationQueries`/`CaseReportGeneration` record (registered, no caller in Web/Worker), `CaseReportDeliveryPolicy.Address` unused, `SameRecipients` unused, duplicate `IsComplete` (`CaseReportGeneration.cs:616` vs `AssessmentReportRendering.cs:294`), stale doc comment about deferred image curation (`AssessmentReportProjection.cs:17-25`).



\### C — What is sound (record briefly)



Freeze/render/confirm three-phase shape with Serializable short transactions; source-census recheck and signatory recheck inside the freeze; hash re-verification of images, signature and PDF; report outputs excluded from their own sources (`EfAssessmentReportProjectionSource.cs:212-213`); stale wiring present in workspace, asset-preparation, estimate, valuation, vehicle-lookup, custody and staff-account stores; one-owner readiness (`CaseReportReadiness.Evaluate`) used by the page; UK-culture money/date formatting; embedded fonts, no environment fonts; delivery pins artifacts by hash and refuses on drift; presentation events deduped per London day.



\### D — Test gaps (from the coverage map)



Untested: fee VAT rounding beyond one case; cross-day/second-kind supersession (A1); estimate-change and image-preparation staling; two-artifact (report + fee note) delivery persistence/send; real `EfCaseArtifactCustody` with a `case-report:` identity and `FindByOperationKeyAsync` recovery; Web `POST GenerateReport` outcomes NotReady/Failed/NotFound; real rendering of total-loss/cash-in-lieu/contract-repair layouts (only repairable renders for real); `AssessmentReportRendererTests` lacks the `SqlServer` trait yet builds a LocalDB provider; `CaseReportApprovalWebTests.cs` tests downloads, not approval; FRD-11 R6 (Audit `a.`/`ap.` references), R21 (fee-note preview), R28 (correction never alters an issued fee note) have no implementation or test.



\## Verification



\- Prose-only deliverable under git-ignored `artifacts/`: no dotnet restore/build/test, no placement gate (not under docs/).

\- Before writing, re-open each cited line once more from the report's citations list to confirm line numbers on dev HEAD.

\- Report ends with the two operator decisions and the recommended fix order (A1, A2, A3, A8, A4, A5, A9, A6, A7, then B-items) so it can seed a ticket plan.



