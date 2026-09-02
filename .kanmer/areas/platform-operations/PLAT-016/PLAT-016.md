---
id: PLAT-016
type: ticket
title: >-
  Ban explanatory copy and page bloat in the design authority and repository
  rails
status: done
area: platform-operations
order: 120
assignee: claude-code
profile: chore
stageEntered:
  review: '2026-08-20T14:16:45.504Z'
  verifying: '2026-08-20T14:16:51.517Z'
  done: '2026-08-20T20:51:00.663Z'
labels:
  - design
  - governance
  - operator-reported
links: []
deployment: n/a
archived: false
created: '2026-08-20T14:06:46.148Z'
updated: '2026-09-01T14:44:31.760Z'
---

## Why

Operator, 2026-08-20, on the assessment page's copy: *"more fucking explanations. Fix this and add strict AGENTS.md rules, as well as updating anything related to design specifically banning this."* Also: *"the page is extremely long, contravening our design"*, *"STOP EXPLAINING PAGES"*, *"Fields have 'required' under their box but there is surely better ways to denote this visually"*, and *"Prefer filter dropdown boxes, not clickable tabs"* with newest-first sorting.

## What

docs/design/README.md gains binding rules: no explanatory copy (labels only, no field hints, no how-it-works sentences, no worked-example prose; required state shown visually; at most one consequence sentence on a destructive action); pages render only populated, relevant sections (empty-state and edit-only panels absent in read-only view; page length is a defect); filters are dropdowns, not pill tabs; tables default newest-first with column-header sorting. AGENTS.md (+ CLAUDE.md copy) gains the matching simplicity-rails bullet.

## How to verify

The new rules read as enforceable tests; Test-DocumentationLinks passes; later UI tickets in this round cite them.

## Outcome
