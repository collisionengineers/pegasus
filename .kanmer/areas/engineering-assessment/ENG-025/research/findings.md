# Research — ENG-025 (verified premises vs assumptions)

Every premise below was checked read-only on `origin/dev` @ 4d696225
(worktree `../pegasus-worktrees/eng-025-assessment-shell`) unless marked
ASSUMED.

## Verified

1. **ENG-026 (#595) is NOT on `origin/dev`.** PR state OPEN (gh pr view
   595). `git merge-base --is-ancestor c97889f1 origin/dev` fails; branch
   `origin/task/eng-026-estimates` holds it. My base already equals
   origin/dev HEAD, so "merge origin/dev" is a no-op and brings no
   `IListCaseEstimates`. Nothing estimate-editor-shaped can compile
   against it on this branch; the wave-2 ticket body itself scopes the
   Estimates pane to "the current single estimate/import +
   accept-specification handlers" (multi-estimate editor is wave 4,
   ENG-028). No `Migrations/*` conflict arises (nothing to merge).
2. **AUTO-011 AI job ledger IS on dev** (`Core/AiWork/AiJobs.cs`,
   `AiJobOperations.cs`, DI in Infrastructure line 330):
   `ICreateAiJob` + `CreateAiJobCommand(AiJobKind.Estimate, SubjectId,
   SubjectReference, Instruction, TargetPercentOfEngineerValue, Actor,
   OperationKey)`; `AiJobPolicy.ValidateNew` refuses without a confirmed
   Engineer's Value ("An estimate job needs a confirmed Engineer's Value
   on the case.") and bounds the target 1..100;
   `IsEligibleEstimateCaseState` = ReportPreparation|PostReport.
   `ICreateAiJob` is composed unconditionally (Infrastructure DI), unlike
   the old `ISendCaseToAi` (Web `Features:SendToAi` gate).
3. **Access policy today** (`AssessmentWorkspace.cs:33-42`):
   `CanOpen = State is Review or ReportPreparation && export >= review`.
   Users of the policy: `EfAssessmentWorkspaceSource` (returns null when
   !CanOpen), `EfAssessmentAccessSource`, `Details.cshtml.cs`
   `CanOpenAssessment` (Case workspace "Open Assessment" control),
   `GenerateCaseAssessmentReportDraft` (NotFound when !CanOpen), page
   `CanAccessAsync`. All flow through `AssessmentAccessState.CanOpen`, so
   one Core change moves every gate. FRD-11 "Report-draft entry point"
   section (dev) states D11 verbatim: Report preparation or later +
   current-cycle export; never Not ready/Review/Held; editable in
   ReportPreparation/PostReport; read-only in PostReportComplete;
   unavailable in other terminal outcomes.
4. **Shell exists** (PLAT-029 + CASE-012 #599, verified in tree):
   `page-header/eyebrow/page-actions`, `record-ribbon`/`ribbon-item` (5 on
   Case), `presence-strip`, `record-bar`/`record-bar-end`/`gated`
   data-condition pattern, `dialog-backdrop[data-dialog]` + `data-dialog-open`
   /`data-dialog-close` + `dialog[data-focus-trap]` (site.js), `_EvidenceViewer`
   + `[data-evidence-set]`/`[data-evidence-item]`, `_StatusChip`,
   `assessment-v3`/`estimate-*` CSS block (site.css 676-745),
   site.js modules already present: estimate tablist roving tabindex,
   `input[type=range][data-range-output][data-range-base][data-range-amount-output]`,
   `[data-rail-toggle]` collapse, dropzone, image rotate.
5. **Evidence data**: instruction docs = `CaseFiles.Live(details.Documents)`
   filtered `SemanticRole.Instruction` (route `/Cases/Documents/Download`
   inline, DOCS-011); images = `ICaseEvidenceImageQueries.ListForCaseAsync`
   (custody-confirmed Image-role docs; `IsCaseDocument` picks the route),
   exactly what Details "Instruction photographs" renders.
6. **Estimate import seam** (ENG-002, wired on dev): `IEstimateDocumentParser`
   (AudatexPdf only), handler does parse → retain (`IAddCaseDocument`) →
   `StartDraftAsync` under the operator's edit lease; accept handler records
   the typed basis. A manual "New estimate" is impossible today:
   `RepairSpecificationPolicy.ValidateSource` demands artifact ref, source
   version and SHA-256 for every non-legacy route, so no handler exists.
7. **Report draft seam** (DELIV-012): `GenerateCaseAssessmentReportDraft`
   read-only, returns PDF; readiness via `AssessmentReportProjection.Prepare`.
8. **site.js has no iframe-src-on-dialog-open pattern** (needed for an
   in-page PDF preview dialog); `[data-preview-template]` clones server
   templates for correspondence rows only.

## Assumptions

- ASSUMED: the orchestrator's "New estimate / estimate tabs / editor via
  ENG-026" clause is conditional on ENG-026 being on the branch base ("when
  available"); with it absent, the binding rules (context.md: never render an
  inert control; ticket body: multi-estimate editor is wave 4) reduce the
  pane to the current single-estimate surfaces. Recorded in plan step 0.
- ASSUMED: with the old readiness panel not drawn by contract §1.9, the
  report-draft controls state their not-ready condition on the control
  (design README "Absent versus disabled"), and the readiness itemisation
  remains a Core concern surfaced only where drawn later.
