# Research — DOCS-020: consistent report inputs

## Question
Can the current report generation and delivery paths retain or send stale
report inputs despite their Case and generation guards?

## Findings
- Baseline origin/dev 2e50fde474ce35eb32eff8677eb2327cb6aad272. PR675 full review
  findings 3/4 reproduced by source inspection. No test/build was run.
- EfAssessmentReportProjectionSource reads workspace before workflow and then
  documents, profile, image-preparation and valuation queries. FreezeAsync in
  EfCaseReportGenerationStore reads these outside its transaction and never
  compares input CaseVersion with the guarded workflow. Move the first workflow
  read before workspace and compare its Header.Version; freeze must compare
  the captured version again. Existing CaseEditAuthority owns version refusal.
- Signatory profile changes have no workflow version. Re-resolve the selected
  profile with EfStaffAccountQueries and CaseSignOffEngineerResolver inside the
  short freeze transaction and reject a changed tuple. The same-context stale
  helper already exists; admin eligibility/name/qualification/signature/default
  mutations must stale affected current generations without rewriting snapshots.
- SourceDocumentsChanged and SignatoryChanged have no callers. Real document
  source mutations occur in EfDocumentCustodyStore (including its shared
  PrepareAddAsync for market research), EfQueuedCustodyProcessor and
  EfCaseArtifactCustody immediate/recovery confirmations. INTK061 owns only the
  unrelated request handover in EfDocumentRequestStore, not these files.
- EfCaseArtifactCustody marks every artifact Generated, including public uploads
  and Glass input. Only GeneratedCaseArtifacts.OperationKey identifies report
  outputs; use this existing identity to exclude report output from report
  source evidence and self-invalidation, never the broad Source enum.
- FRD11 explicitly preserves valid generations on notes/recipient edits.
  Therefore do not require blanket generation.CaseVersion equality with today's
  workflow in preparation. Atomic relevant-input invalidation feeds the existing
  Core delivery state guard. The send-time captured preparation version and
  addressing checks remain unchanged.
- The freeze and read-only preview use UTC calendar dates; report timestamp UI
  explicitly renders UTC. LondonCalendar owns the requested civil date/time.
- Foundation grants only SELECT,INSERT for Web report generation/artifact rows,
  despite existing confirm/stale UPDATE callers. Worker has neither report
  generation SELECT/UPDATE nor artifact SELECT needed by source invalidation.
  Include a narrowly scoped permission migration, bootstrap expected census,
  and actual runtime-role caller test, not a broad grant or duplicate fake list.
- Existing report persistence harness, document custody durability tests,
  account administration persistence tests, and runtime-role harness supply
  focused tests; no new framework is needed. Declared research sources: none.

## Implications
One root-cause class: report inputs are not consistently guarded or invalidated.
Reuse Core report/source/version/eligibility policies and existing transactions;
never hold Chromium, document-byte reads, Box or Graph inside report freeze locks.
Root explicitly agreed notes/recipient exception and exact generated identity.
No schema column, runtime unit or package is required.

## Open questions
None. Current user/root authorizes this bounded correction and grants.
