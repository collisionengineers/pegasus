---
id: INTK-030
type: ticket
title: Keep email signatures and logos out of case evidence images
status: review
area: intake-processing
assignee: ''
profile: fix
stageEntered:
  implementing: '2026-08-21T21:37:43.995Z'
  review: '2026-08-21T22:06:49.361Z'
labels:
  - regression
  - qdos26008
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-21T18:17:19.111Z'
updated: '2026-08-21T22:06:49.361Z'
---

## Why

QDOS26008's evidence images begin with two email signature graphics / logos. These are not evidence.

**Root cause.** `InstructionEvidenceImages.Select` admits **every** attached image unconditionally — only *embedded* images face the 40 KB photograph floor. Outlook signature graphics that arrive as attachments, or that lose their `cid` when a message is forwarded, pass straight through. They also sort first, which is why they were the first two.

## Fix direction

In order: exclude anything the reader classified inline; exclude attachments carrying a `cid` reference from the body; then apply a photograph test to attachments as well as embedded images. Prefer pixel dimensions and aspect ratio over bytes alone — a logo can be a large PNG and a phone photo a small JPEG — using the width and bounding box the PDF path already captures.

The threshold must be **corpus-measured** the way the existing 40 KB floor was, and the measurement recorded here. Do not invent a number.

## How to verify

Tests must include a real signature block and a real damage photograph from the corpus. The QDOS26008 gallery must lose the two logos and keep every genuine photograph.
