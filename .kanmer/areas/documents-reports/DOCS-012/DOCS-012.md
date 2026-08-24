---
id: DOCS-012
type: ticket
title: 'Show case evidence on the Evidence tab, not the document custody ledger'
status: backlog
area: documents-reports
assignee: ''
profile: fix
labels:
  - found-during-qa
  - ui
  - design
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-23T15:19:38.272Z'
updated: '2026-08-24T08:59:48.073Z'
---

## What the operator saw

> *"**Issue 2** — Document custody box in evidence is unneeded detail. We only
> need to see the files that are considered case evidence. If they show here,
> they should be on box."*

The direction is clear. What is not settled is **what happens to the six things
that live nowhere else**, and one binding design row that currently says the
opposite.

## The ledger is the only home for six controls

`_CaseDocuments.cshtml` has exactly one caller (`Cases/Details.cshtml:174`) and
**no test asserts any of its strings** — so removing it breaks nothing
mechanically. But grep says it is the sole surface in the whole Web project for:

| Capability | Only location |
| --- | --- |
| Per-version Box custody state | `:58` — the only `OperatorLabels.CustodyState` caller in any `.cshtml` |
| Case Box folder link and folder state | `:14-23` — the only `CustodyFolderState` caller |
| Confirm third-party vehicle evidence | `:88-89` + form `:116-118` |
| Remove an occurrence | `:76-77` + form `:111-113` |
| **Selective** export by version | `:53`, `:101` — the whole-case export at `Details.cshtml:122-129` is a different, lease-free GET |
| Manual `Retain document` upload | `:127-132` |

Historical revisions become unreachable entirely: `Download` **requires** a
`versionId` and nothing else in the UI enumerates them.

## One thing in it is plainly wrong and can go regardless

The `EVA eligibility` cell renders *"Eligible unless staff confirms third-party
vehicle evidence"* — a how-it-works sentence in a table cell, which
`docs/design/README.md` bans outright.

## The blocking conflict

`docs/design/README.md` carries a binding row for this exact panel:

> *Evidence/document panel | Original/source/version/logical removal/closed lock;
> Box/external state; issued report versions; exact Outlook evidence…*

This ticket as written deletes `version`, `logical removal` and `Box/external
state` from that panel. The operator's word outranks the design authority — but
the authority has to be **amended in the same task**, and amending a binding
design row is a product decision, not an implementation detail.

## Questions only the operator can answer

1. **Does "unneeded detail" include the Box folder link and per-file custody
   state?** *"If they show here, they should be on box"* reads as *presume
   custody rather than display it*. But `:58` is the only place a **failed or
   pending** Box write is visible — presume it and a file that never reached Box
   still shows as evidence.
   **Recommendation:** keep the Box folder link; drop the per-row custody column;
   surface a non-confirmed custody state as an exception on the row — a state,
   not a sentence.

2. **Are historical revisions to remain reachable at all?**
   **Recommendation:** show the current revision, with prior revisions behind the
   `<details>` disclosure the product already uses elsewhere.

3. **Where do the six controls go?** Remove occurrence, confirm third-party
   evidence, selective export, manual upload, plus the two above.
   **Recommendation:** per-row actions in edit mode, selective-export submit
   retained, manual upload beside them. Placement needs sign-off; wording does
   not — they are existing labels.

## Sequencing

Whatever is decided, DOCS-012 lands **before** [[DOCS-011]] — it decides which
surface survives, and DOCS-011's preview trigger lives on a row inside it. Both
tickets and [[CASE-022]] edit the same file; they cannot run in parallel.
