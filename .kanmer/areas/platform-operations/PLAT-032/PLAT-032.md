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
links: []
docs_todo: true
deployment: n/a
archived: false
created: '2026-08-21T18:17:19.735Z'
updated: '2026-08-21T18:17:19.735Z'
---

## Why

Operator direction: check for excessive bloat, duplicate callers and functions, and make document parsing, retrievals and similar all use the same routes wherever possible.

Operator also decided this ships **separately, after** the QDOS26008 regression fixes land, so a broad refactor cannot destabilise or delay them.

## Starting roster — already evidenced

1. **Two content-store routes for the same evidence** — the intake artifact blob store versus the Box document store. Largely closed by [[DOCS-007]]; verify nothing re-introduces a second path.
2. **Three definitions of "the case's images"** — `InstructionEvidenceImages`, `ICaseEvidenceImageQueries`, and the EVA store's own `DocumentOccurrence` query. Converged by [[DOCS-007]]; confirm and delete whichever becomes dead.
3. **`RetainAccepted*` overload pairs** duplicated across `BoxCaseCustody` and `LocalCaseCustody` — four near-identical wrappers differing only by lease guard.
4. **Inline-image classification written twice** in the MIME reader (`MimeKitPdfPigOpenXmlIntakeSourceReader.cs:862` and `.DocMsg.cs:234`) — precisely the kind of drift that produced [[INTK-030]].

The sweep itself is a full pass, not just these four.

## Constraint

The `code-simplifier` subagent is unavailable under the standing no-subagents constraint, so the pass is done directly. Per repo convention the findings are recorded in this ticket's plan/research before the PR.
