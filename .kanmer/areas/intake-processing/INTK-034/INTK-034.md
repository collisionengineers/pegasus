---
id: INTK-034
type: ticket
title: Retain a Triage request's images as Triage evidence
status: implementing
area: intake-processing
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-24T09:18:52.454Z'
taken_at: '2026-08-24T09:18:57.201Z'
branch: task/intk-034-triage-images
worktree: ../pegasus-worktrees/intk-034-triage-images
labels:
  - triage
  - deferred-from-INTK-033
links: []
refs:
  - docs/frd/frd-03-triage.md
  - docs/frd/frd-05-documents-extraction-and-custody.md
archived: false
created: '2026-08-24T08:33:08.101Z'
updated: '2026-08-24T09:18:57.202Z'
---

## Why

Both QDOS Triage templates attach the client's vehicle damage photographs, and
assessing those photographs is the entire point of a Triage: *"Please see the
attached images to determine if the vehicle is repairable or a total loss."*

[[INTK-033]] deliberately scoped this out and the independent review agreed the
scoping was defensible — nothing is lost today, because the attachments are
retained as receipt assets and the Triage detail page links straight to them
(`Pages/Triage/Details.cshtml:56` → `/Intake/Details/{Origin.ReceiptId}`).

What is absent is a Triage evidence surface of its own: the engineer reaches the
images through the originating e-mail rather than through the Triage they are
assessing.

## Not yet decided

Whether this is wanted at all is an operator question. Retaining the images a
second time under the Triage would duplicate custody; surfacing the receipt's
existing assets on the Triage page would not. Ask before building.

## Verify

A Triage opened from an intake message shows its vehicle photographs without
the engineer navigating to the e-mail.
