---
id: MAIL-029
type: ticket
title: >-
  Inbox attachments table is missing the Search content, Custody and Preview
  columns (§1.3)
status: backlog
area: mail-communications
order: 650
assignee: ''
profile: fix
labels:
  - ui
  - mail
  - wave-4
groups:
  - EPIC-011
links:
  - MAIL-025
  - MAIL-026
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-05-documents-extraction-and-custody.md
archived: false
created: '2026-08-29T14:01:00.717Z'
updated: '2026-09-03T15:15:28.214Z'
---

## What

`context.md` §1.3 specifies the message page's attachments table as:

> Attachments table (File, Type, Size, Search content, Custody, Preview).

On merged `dev` at `b92cb9a7`, `src/Pegasus.Web/Pages/Mail/Message.cshtml` renders
only **File, Type, Size**. The `Search content`, `Custody` and `Preview` columns are
absent, and no `preview` control appears anywhere on the page.

## Why

Custody is a first-class concept in this product — `frd-05` governs document custody
and the Case Files view renders a custody chip for exactly this reason. An attachment
whose custody state is invisible on the message page, while the same evidence shows
its custody on the Case, is an inconsistency an operator will trip over. `Search
content` and `Preview` are likewise drawn in the approved contract.

Found during the [[MAIL-025]] strict rule-14 audit, 2026-08-29. Recorded as a gap
with no owner rather than folded into MAIL-025, whose scope was the list and message
page port.

## Approach

- Reuse the existing custody chip convention rather than inventing a second one —
  `Pages/Cases/Shared/_CaseDocuments.cshtml` already renders custody for documents;
  name what you reuse.
- `Search content` reports whether the attachment's text was extracted and is
  searchable. Find the existing projection for that state before adding a query.
- `Preview` must resolve to a real handler or not be drawn at all. Check whether
  `Pages/Shared/_EvidenceViewer.cshtml` is the existing viewer to reuse.
- No explanatory copy: column headers and values only.

## Verification

- [ ] The attachments table renders File, Type, Size, Search content, Custody and
      Preview.
- [ ] Custody uses the existing chip convention, not a second implementation.
- [ ] Preview opens a real viewer, or the column is absent — no inert control.
