---
id: DOCS-006
type: ticket
title: Retain extracted instruction images as case evidence and Box files
status: done
area: documents-reports
order: 980
assignee: claude-code
profile: feature
stageEntered:
  implementing: '2026-08-21T11:45:42.776Z'
  review: '2026-08-21T12:12:54.749Z'
  verifying: '2026-08-21T13:22:57.885Z'
  done: '2026-08-22T06:04:30.343Z'
labels:
  - custody
  - evidence
  - images
links: []
refs:
  - docs/frd/frd-05-documents-extraction-and-custody.md
deployment: production
archived: false
created: '2026-08-21T11:43:29.114Z'
updated: '2026-08-26T14:34:44.254Z'
---

## What

QDOS26007's instruction arrived with `1_Images-V1.pdf` — 17 vehicle photos
embedded in a PDF. The intake reader already extracts them
(`IntakeAssets` Kind=`embedded_image`, with page numbers), but nothing
promotes them: custody retains only Kind=`attachment`
(`EfQueuedCustodyProcessor.RetainInstructionAttachmentsAsync`), and the case
Evidence tab counts only `Documents + ImageIntakes` — the case shows
Evidence 0 and Box holds only the .eml and the PDFs (operator report
2026-08-21, issue 6: "Need to extract these out and attach as evidence +
store in box").

## Approach

- **Custody:** after the attachments pass, retain the receipt's
  `embedded_image` assets as individual files beside the source in
  `Evidence/Original instruction`, ordinals continuing the existing scheme,
  op key `{OperationKey}:embedded:{assetId:N}`, deduped by `ContentHash` and
  filtered to plausible photos (the letterhead logos repeat identically at
  234 B–28 KB; the damage photos run 60–320 KB — threshold measured from the
  corpus and recorded in the plan). Local custody parity.
- **Evidence tab:** the case's linked receipts' image assets (embedded and
  direct image attachments) render as a thumbnail gallery on
  `Cases/Details` (reuse the existing receipt-image endpoint and the
  CASE-006 gallery pattern); `EvidenceCount` includes them.
- End-to-end proof through `CustodyOutboxIntegrationTests` with a real
  images-PDF email from the local corpus (skip-if-absent).

## Verification

- [ ] A case created from an images-PDF instruction shows the photos on its
      Evidence tab and the same files beside the source in Box/local custody.
- [ ] Letterhead logos and inline signature images are not promoted.
- [ ] Replay verifies rather than re-uploads (idempotent op keys).
- [ ] Custody durability + case web suites green; Release build 0/0.
