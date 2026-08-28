---
id: MAIL-024
type: ticket
title: >-
  FRD-08 and ADR-0036: outbound mail from an approved mailbox and EVA-sent
  report detection
status: verifying
area: mail-communications
assignee: claude-code
profile: chore
stageEntered:
  preparing: '2026-08-28T08:08:21.145Z'
  review: '2026-08-28T08:15:21.870Z'
  verifying: '2026-08-28T08:21:31.930Z'
taken_at: '2026-08-28T08:13:35.265Z'
branch: task/mail-024-outbound-mail-docs
worktree: ../pegasus-worktrees/mail-024-outbound-mail-docs
labels:
  - docs
  - mail
  - adr
groups:
  - EPIC-011
links: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - 47d9144af70005f97efb8f1540b400dee3905646
  - b65081db010c27df8dab93fe78f9f43765bfdaaf
prs:
  - '584'
archived: false
created: '2026-08-28T08:05:30.146Z'
updated: '2026-08-28T08:21:31.930Z'
---

## What

- New `docs/adr/0036-outbound-mail-via-approved-mailbox.md` (one decision): staff-initiated Reply/Forward/Compose send through Microsoft Graph `Mail.Send` as the approved mailbox identity; the resulting Sent item is the evidence FRD-08 already defines and is auto-linked to the Case; Flag and Delete (to Deleted Items, with reason) are Outlook mutations; production activation is an explicit configuration switch approved separately; local alpha never mutates a mailbox (D4).
- FRD-08: outbound correspondence rules (who may send, from which mailbox, what is retained), and EVA-sent report detection (D10): a report mail matching a Case reference with a PDF attachment is detected in the approved mailbox, the PDF attached to the Case, Sent evidence linked, and the Case enters post-report work; closure remains a separate reasoned step.
- `docs/boundaries.md`: the automated-correspondence row is rewritten to state what is now in scope (staff send) and what remains excluded (autonomous send).

## Owns

`docs/frd/frd-08-email-mailbox-and-background-processing.md`, `docs/adr/0036-outbound-mail-via-approved-mailbox.md`, the boundaries row (coordinate: the FRD-12/capabilities ticket owns the rest of `docs/boundaries.md` — touch only the correspondence row).

## Verification

- [x] ADR frontmatter valid; one decision.
- [x] `scripts/Test-DocumentationLinks.ps1` passes.
