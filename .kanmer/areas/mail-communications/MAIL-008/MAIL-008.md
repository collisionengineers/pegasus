---
id: MAIL-008
type: ticket
title: Map mail classification and folder-move reason to operator labels
status: done
area: mail-communications
order: 1440
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-21T10:42:16.179Z'
  review: '2026-08-21T11:42:35.774Z'
  verifying: '2026-08-21T12:15:01.989Z'
  done: '2026-08-21T15:06:47.517Z'
labels:
  - ui
  - web
  - defect
  - design-approved
links: []
blocks:
  - MAIL-006
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
deployment: production
archived: false
created: '2026-08-21T09:37:20.423Z'
updated: '2026-09-01T14:44:33.019Z'
---

## What

Two machine-worded values reach the operator on `/Inbox/{id}` and must pass
through `Pegasus.Web.Presentation.OperatorLabels` instead:

1. **The classification.** `MessageModel.DecisionLabel` renders
   `MailCategory.Name`, which for received families is the kebab-case family
   name from `MailClassificationContracts.CategoryName` —
   `new-instruction-received`, `in-progress-cases`, `pre-instruction-emails`,
   `non-client-related`, `post-report-emails`, `internal-cc`, `billing` — with
   the subtype appended after a slash. An operator currently reads
   `new-instruction-received/inspection`.
2. **The folder-move reason.** `RetainedMailSuggestedMove.Reason` is policy
   text. It is currently rendered as a paragraph, and under the approved design
   it becomes the reason recorded against a move confirmation, so it is read
   even more prominently.

## Why

`docs/design/README.md:171` — "Raw `ToString()` of enums, snake_case event
codes, GUIDs, hashes, storage paths, version integers and byte counts never
reach markup." A kebab-case registry key is the same defect. This is live on
`dev` today, not a regression this work introduces.

The approved design at
`docs/design/references/mockups/inbox-message-page/` puts both values in the
Decision card and the move confirmation, where they are the primary thing read.
`Filed.dc.html` and `Moving.dc.html` show **proposals** — `New instruction ·
Inspection` and `Classified as a new inspection instruction` — which are not
settled terms and need the operator's wording before they ship.

Blocks [[MAIL-006]] only in the sense that the redesign should not ship the
slug in a more prominent position; either land this first, or land both
together.

## Approach

- One label map in `OperatorLabels`, beside the existing
  `MailOperationalDestinationLabel`, covering every `ReceivedMailFamily` and
  `SentMailFamily` plus the subtype, and preserving the settled casing of
  `Audit`, `Triage`, `Unidentified`, `Blocked`, `Not ready`, `Review`, `Held`.
- The reasoned `Other` category carries an operator-supplied `OtherName` —
  render that as given; it is already their words.
- **The wording is the operator's, not ours.** Bring the full list of families
  and subtypes for sign-off before implementing; do not invent labels and do
  not ship the proposals from the mockups unreviewed.
- Do not touch `MailClassificationContracts.CategoryName` — it is the settled
  registry key used for parsing and persistence, and `ParseReceivedFamily`
  round-trips against it.

## Verification

- [ ] No kebab-case family name renders on any Mail page; grep the response
      HTML in `MailWorkspaceWebTests` for `-received`, `-cases`, `-emails`.
- [ ] Every enum member has a label — a test over `Enum.GetValues` with no
      fallback to `Humanise`.
- [ ] `ParseReceivedFamily` / `ParseSentFamily` still round-trip.
- [ ] The rendered value matches the wording the operator signed off.
