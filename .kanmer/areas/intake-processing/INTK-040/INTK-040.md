---
id: INTK-040
type: ticket
title: Route unidentified mailbox image attachments through Image Intake
status: implementing
area: intake-processing
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-08-25T14:43:12.393Z'
taken_at: '2026-08-25T14:45:04.050Z'
branch: task/intk-040-mailbox-image-intake
worktree: ../pegasus-worktrees/intk-040-mailbox-image-intake
labels:
  - operator-reported
  - production-defect
  - image-intake
  - mailbox
  - unidentified
links:
  - INTK-039
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-05-documents-extraction-and-custody.md
  - docs/frd/frd-12-operator-experience.md
  - docs/adr/0029-image-initiated-case-projection.md
archived: false
created: '2026-08-25T14:43:04.295Z'
updated: '2026-08-25T14:45:04.050Z'
---

## What

For future otherwise-Unidentified mailbox emails that carry direct vehicle-image attachments, route those attachments as one grouped Image Intake submission through the same lifecycle used by manual uploads, replacing the parent email-level Unidentified outcome.

## Why

Production reference U35 completed as `NoUsableIdentification` even though its retained evidence included three direct JPEG vehicle photographs with a readable registration. The mailbox receipt was not image-only because the retained EML and inline images were evaluated together, so Image Intake never scanned the photographs. This extends the grouped lifecycle completed by [[INTK-039]].

## Approach

- Select only direct image attachments from an otherwise-Unidentified, non-instruction mailbox receipt.
- Submit them as one source-preserving grouped Image Intake submission linked to the parent receipt.
- Let the existing grouped VRM, association, Image-initiated Case, custody and Unidentified outcomes remain the sole business implementation.
- Leave U35 itself unchanged; this applies only to newly processed mail.

## Verification

- [ ] A U35-shaped email with three direct JPEGs follows grouped Image Intake and produces the existing matched, Image-initiated Case, no-readable, or conflicting-registration group outcome.
- [ ] EML source bytes and inline images are excluded from image custody.
- [ ] Instruction-bearing mail, mail already routed to Case/Triage, and mail without direct image attachments retain existing behavior.
- [ ] Retry is idempotent and terminal child-submission failure remains visible as a technical-failure Unidentified item.

## Outcome
