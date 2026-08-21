---
id: PLAT-032
type: ticket
title: Simplification and duplicate-route sweep across the codebase
status: backlog
area: platform-operations
assignee: ''
profile: chore
labels:
  - simplification
links:
  - DOCS-007
  - INTK-030
  - PR-039
  - PR-043
  - PR-044
docs_todo: true
deployment: n/a
archived: false
created: '2026-08-21T18:17:19.735Z'
updated: '2026-08-21T21:46:37.579Z'
---

## Why

Operator direction: check for excessive bloat, duplicate callers and functions, and make document parsing, retrievals and similar all use the same routes wherever possible.

Operator also decided this ships **separately, after** the QDOS26008 regression fixes land, so a broad refactor cannot destabilise or delay them.

## Starting roster — already evidenced

1. **Two content-store routes for the same evidence** — the intake artifact blob store versus the Box document store. Largely closed by [[DOCS-007]]; verify nothing re-introduces a second path.
2. **Three definitions of "the case's images"** — `InstructionEvidenceImages`, `ICaseEvidenceImageQueries`, and the EVA store's own `DocumentOccurrence` query. Converged by [[DOCS-007]]; confirm and delete whichever becomes dead.
3. **`RetainAccepted*` overload pairs** duplicated across `BoxCaseCustody` and `LocalCaseCustody` — four near-identical wrappers differing only by lease guard.
4. **Inline-image classification written twice** in the MIME reader (`MimeKitPdfPigOpenXmlIntakeSourceReader.cs:862` and `.DocMsg.cs:234`) — precisely the kind of drift that produced [[INTK-030]].

## Added by the Release 17 review of the `codex-mcp-client` tickets (2026-08-21)

5. **`RetainedMailFolderMoveResult` carries four fields nothing reads.**
   `ExpectedClassificationVersion`, `ExpectedRecommendationPolicyKey`,
   `ExpectedRecommendationPolicyVersion` and `ExpectedMailboxVersion` are written at
   `EfRetainedMailFolderMoveStore.cs:303-306` from the persisted operation row and read
   by no caller: the only consumer, `Message.cshtml.cs:541`, reads `Outcome`. They echo
   the request's own expectations back out of a public Core contract. `IsReplay`,
   `OperationKey` and `FailureReason` on the same record need the same check. Shipped by
   [[PR-039]] / [[PR-043]] / [[PR-044]] (tick-049); behaviour is correct, the surface is
   larger than its callers.

6. **`Mail/Message.cshtml.cs` is 1,025 lines** — 49% larger than the next-biggest page
   model (`Cases/Create.cshtml.cs`, 689) — carrying link, unlink, folder-move,
   classification-correction and case-search handlers with their lease preparation. Not a
   defect and not a blocker; a split candidate if it grows again.

## Cleared by that review — checked, not defects

- PR #477's headline **+8,996 lines is 7,116 lines of generated EF `Designer.cs`**. Real
  new code across all four codex branches is ≈2,500 lines, which is proportionate.
- The `Succeeded` / `Failed` / **`Uncertain`** move taxonomy is required by the runbook's
  recovery rules, not gold-plating.
- The mailbox and folder `<nav>` elements on `Mail/Index.cshtml` are navigation with
  `aria-label`s, not pill rows standing in for a filter dropdown — the view filter already
  uses a labelled `select`.

## Already fixed rather than deferred

Two duplications found during Release 17 were fixed in place, because the work had to
touch every copy anyway and leaving one stale would have been a live defect:

- the **terminal-state vocabulary**, written three times (`CaseLifecycleRules.IsTerminal`,
  `EvaHandoffStore`, `EfVehicleWorkflowStore`) — [[INTK-029]];
- **stopping a case's chase schedule**, written four times across the workflow, case-data,
  replacement and intake-mutation stores — now `CaseChaseState.Stop`.

## Constraint

The `code-simplifier` subagent is unavailable under the standing no-subagents constraint, so the pass is done directly. Per repo convention the findings are recorded in this ticket's plan/research before the PR.
