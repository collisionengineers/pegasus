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
updated: '2026-08-23T15:19:38.272Z'
---

## What the operator asked for

> *"Document custody box in evidence is unneeded detail. We only need to see the
> files that are considered case evidence. If they show here, they should be on
> box."*

## What it does today

`Pages/Cases/Shared/_CaseDocuments.cshtml` renders a table with a row per
**occurrence × version**, and columns for **Role / source**, **Revision state**,
**File**, **Custody**, **EVA eligibility** and **Action** — plus an "Open Box
case folder" link, an export-selection checkbox column, per-row removal and
third-party-confirmation forms, and an upload form.

That is a custody ledger. The operator wants the evidence: which files this case
holds. The second sentence is the design rule — *if it is listed here, it is in
Box* — so custody state does not need a column of its own to be true.

## Shape of the change

Keep on the Evidence tab: the file, what it is, and the ability to open it
(which [[DOCS-011]] turns into a preview).

Move or drop: revision state, per-row custody status, EVA-eligibility prose,
and the historical-revision rows. A case document has exactly one current
revision that matters to an operator reading the case.

## Three things to be careful of, none of them cosmetic

1. **The controls in that table are the only place they exist.** Removing an
   occurrence, confirming third-party vehicle evidence and selecting versions
   for a selective export all live in those columns. Whatever survives must keep
   a home for them, or they leave the product silently.
2. **[[CASE-022]] is in the same partial.** The upload-request section sits
   inside this file; the operator separately reports being unable to find it.
   Deleting around it without rehoming it makes that worse.
3. **Historical revisions are audit surface.** Dropping them from this view is
   right; dropping the ability to reach them at all is a different decision and
   is not what was asked for.

## Design authority

`docs/design/README.md` — page economy and no explanatory copy. The
"EVA eligibility" column is explanatory copy in a table cell ("Eligible unless
staff confirms third-party vehicle evidence"), which is precisely what that rule
excludes.
