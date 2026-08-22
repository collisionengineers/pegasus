---
id: DOCS-009
type: ticket
title: 'Record intake photographs as images, not instruction documents'
status: implementing
area: documents-reports
assignee: claude-code
profile: fix
stageEntered:
  preparing: '2026-08-22T19:43:45.801Z'
taken_at: '2026-08-22T19:49:13.261Z'
branch: task/qdos26011-regressions
worktree: ../pegasus-worktrees/qdos26011-regressions
labels:
  - qdos26011
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-22T19:42:25.308Z'
updated: '2026-08-22T19:49:13.261Z'
---

## The defect

`EfQueuedCustodyProcessor.cs:306` gives **every** accepted intake attachment `DocumentSemanticRole.Instruction`, whatever its media type. Only photographs found embedded inside a PDF get `DocumentSemanticRole.Image` (`:341`).

## Verified impact (prod, read-only, 2026-08-22)

QDOS26011's ten case documents:

| Role | Files |
| --- | --- |
| `Instruction` | eight JPEG photographs **and** the instruction PDF |
| `OriginalSource` | the `.eml` |

Every genuine photograph is filed as an instruction document. Two consequences, both live:

1. The Evidence tab's EVA-eligibility column reads "Not an image" against all eight photographs, because it tests `SemanticRole == Image`.
2. `EvaHandoffStore.cs:85` selects bundle images with the same test, so an EVA bundle for this case would contain **zero** images — which is the greater part of why nothing can be exported ([[CASE-019]]).

## Scope

- An accepted intake attachment whose media type is an image is retained as `DocumentSemanticRole.Image`; everything else keeps `Instruction`.
- Reuse the existing media-type test rather than adding a second one — `InstructionEvidenceImages.IsImage` already owns that question.
- Existing cases carry the wrong role in `DocumentOccurrences.SemanticRole`; correct them so QDOS26009–26011 behave like cases accepted after the fix.

## How to verify

QDOS26011's eight photographs read as images on the Evidence tab and are selected into its EVA bundle; the instruction PDF and the `.eml` keep their present roles.
