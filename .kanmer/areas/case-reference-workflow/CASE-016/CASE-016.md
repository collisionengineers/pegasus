---
id: CASE-016
type: ticket
title: Take the word Immutable out of every operator-facing page
status: review
area: case-reference-workflow
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-22T00:48:49.299Z'
  implementing: '2026-08-22T00:48:52.059Z'
  review: '2026-08-22T00:51:10.133Z'
labels:
  - qdos26009
  - design
  - ui
links: []
refs:
  - docs/design/README.md
deployment: not-deployed
archived: false
created: '2026-08-21T23:30:27.991Z'
updated: '2026-08-22T00:51:10.133Z'
---

## Why — operator direction (2026-08-22)

> "Remove the word 'Immutable' from the history tab in cases, as well as any other appearance of the word in user facing pages (if any). This is dev speak leaking into frontend."

## Evidence — eight occurrences in operator-facing markup

| File | Text |
| --- | --- |
| `Cases/Shared/_CaseHistory.cshtml:7` | **Immutable case history** (the History tab heading) |
| `Cases/Shared/_CaseSummary.cshtml:227` | **Immutable item** |
| `Cases/Shared/_CaseWorkflow.cshtml:282` | **Immutable report approval** |
| `Cases/Shared/_CaseWorkflow.cshtml:295` | **Immutable report identity** |
| `Cases/Shared/_CaseWorkflow.cshtml:298` | **Approve immutable report** |
| `Administration/Index.cshtml:47` | "Immutable identities, replaced only through…" |
| `Administration/Principals/Index.cshtml:34` | "A principal identity is immutable once created…" |
| `Administration/Principals/Replace.cshtml:95` | "…is normalized to uppercase and is immutable." |

The last three are also explanatory copy, which `docs/design/README.md` bans independently of the word.

## Scope

Remove the word from operator-facing text. Where it was doing work, the replacement is a plain label — not a synonym and not a new sentence, since the approved necessary-copy list is closed. The word stays valid as an internal code identifier; the ban is on what an operator reads.

The History-tab rename and operator notes are tracked separately — this ticket is the vocabulary sweep only.

## How to verify

A scan of `src/Pegasus.Web/Pages/**/*.cshtml` for the word returns only Razor comments and code identifiers.
