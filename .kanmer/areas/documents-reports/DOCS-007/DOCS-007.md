---
id: DOCS-007
type: ticket
title: Register instruction attachments and photographs as case documents in Box
status: implementing
area: documents-reports
assignee: ''
profile: fix
stageEntered:
  implementing: '2026-08-21T21:37:46.695Z'
labels:
  - regression
  - qdos26008
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-21T18:17:19.280Z'
updated: '2026-08-21T21:37:46.695Z'
---

## Why

Two operator observations, one cause: the instructions and original report are absent from the case's evidence, and images appear to be stored Pegasus-side and *then* sent to Box.

**Root cause.** `EfQueuedCustodyProcessor.RetainInstructionAttachmentsAsync` does upload every attachment and selected photograph to Box under `Evidence/Original instruction` at ordinals 002+. But `CompleteCaseCustodyAsync` records only the **source** version against the case — no `CaseDocument` / `DocumentVersion` / `DocumentOccurrence` rows are written for them. The files are in Box; the records are not. So Pegasus cannot show them, and the Evidence gallery instead serves the Azure blob copies held by the intake artifact store.

## Decision taken

Operator chose: **Box is the record, drop the local copy.** Register through the existing `IAddCaseDocument` route (`EfDocumentCustodyStore`), which in Production already writes to `BoxDocumentContentStore`. Serve the Evidence tab from those documents via `IDownloadCaseDocument` so display and custody share one route. Intake asset blobs then age out on retention.

Additive and ordered: register documents first, then switch the read. Never delete-then-rebuild — existing cases must keep rendering throughout. A document that failed to register keeps its blob.

This also converges one of the three competing definitions of "the case's images" ([[SIMPLI-016]]).

## How to verify

The case Evidence tab lists instruction, report and genuine photographs, served from Box.
