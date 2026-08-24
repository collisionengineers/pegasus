---
id: DOCS-014
type: ticket
title: 'Record a preview as a view, not as a completed download'
status: backlog
area: documents-reports
assignee: ''
profile: fix
labels:
  - review-finding
  - custody
  - audit
links: []
docs_todo: true
archived: false
created: '2026-08-24T14:26:21.572Z'
updated: '2026-08-24T14:26:21.572Z'
---

## Why

`Download.cshtml.cs` calls `IDownloadCaseDocument.ExecuteAsync` with a fresh
`web-download:` operation key **before** it looks at `inline`. The production
`EfDocumentCustodyStore` writes an append-only `document_downloaded` action
record on that path, so the business event is committed whether the operator
asked to save the file or merely looked at it.

[[DOCS-011]] added an in-place preview with paging over the same route. Flicking
through twenty photographs therefore writes twenty completed-download records.

## Why it is not a merge blocker

The error is conservative: it over-records rather than under-records, so no real
download is ever hidden. Custody history stays complete; it gains entries that
overstate what happened.

It is still wrong for this product. Case history is evidence here, and the
distinction between "a member of staff viewed this" and "a member of staff took
a copy of this" is one an operator may later need to rely on.

## What to do

Carry the intent into the Core read so the store can record the right event.
`inline` already exists at the page boundary; the command does not carry it.
Decide whether a view is a distinct recorded event or simply not recorded, and
note that a media type the viewer refuses to inline still falls back to a real
download and must keep recording as one.

Raised by automated review on PR #535 alongside two findings that were fixed
there (the production-blocked PDF iframe and the visible hidden image).

## Verify

Page through a case's images, then check the Notes/history surface: previews do
not appear as downloads, and pressing the download control still records one.
