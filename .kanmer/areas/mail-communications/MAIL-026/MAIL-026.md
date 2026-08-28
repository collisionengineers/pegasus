---
id: MAIL-026
type: ticket
title: >-
  Mail composer (Reply/Forward/Compose), Flag, Delete and Case correspondence
  actions
status: backlog
area: mail-communications
assignee: ''
profile: feature
labels:
  - ui
  - wave-4
  - mail
groups:
  - EPIC-011
  - EPIC-006
links: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-28T08:35:24.176Z'
updated: '2026-08-28T08:35:24.176Z'
---

## What

Wave 4 of [[EPIC-011]]. Record-bar actions on `Pages/Mail/Message` (Reply dark, Forward, Compose, Flag, Delete with reason) and the Case Files correspondence buttons, opening the composer dialog (To, Subject, Message, Case, From read-only) → Send through the outbound-mail use case; Query-response AI job drafts prefill the composer. Controls render only when the outbound capability is composed.

## Owns

`src/Pegasus.Web/Pages/Mail/Message.*`, `Cases/Shared/_CaseDocuments.cshtml` (correspondence section), tests.

## Blocked by

The Inbox port ticket, the outbound-mail ticket, the Case views port ticket.
