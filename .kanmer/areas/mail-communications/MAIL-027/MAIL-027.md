---
id: MAIL-027
type: ticket
title: >-
  Outbound mail via the approved mailbox, flag, delete and EVA-sent report
  detection
status: backlog
area: mail-communications
assignee: ''
profile: feature
labels:
  - backend
  - wave-3
  - mail
  - requires-live-approval
groups:
  - EPIC-011
  - EPIC-006
links:
  - MAIL-024
blocks:
  - MAIL-025
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
archived: false
created: '2026-08-28T08:35:47.116Z'
updated: '2026-08-29T13:10:30.310Z'
---

## What

Wave 3 of [[EPIC-011]]; implements ADR-0036 (`docs/adr/0036-outbound-mail-via-approved-mailbox.md`, merged to dev by [[MAIL-024]]) / FRD-08 § Outbound correspondence and § EVA-sent report detection. `Core/Mail/OutboundMail.cs`: `IComposeOutboundMail`/`ISendOutboundMail` (staff `PerformCasework`, approved mailbox identity, Reply/Forward/Compose shapes, Case link), Graph `Mail.Send` adapter composed only by explicit configuration with the unavailable implementation by default (local alpha and every test profile never mutate a mailbox); the Sent item is retained and auto-linked by the existing Sent-evidence poll. `IFlagRetainedMail` (retained-message fact). Delete = folder mover to Deleted Items with reason (extend `IRetainedMailFolderMover`). `DetectEvaSentReports` worker step: report mail matching a Case reference with a PDF report attachment → attach as report document, link Sent evidence, enter post-report work (never closure; ambiguous matches surface for staff). Production activation is a separately approved live write.

## Owns

`src/Pegasus.Core/Mail/**` (new), `Core/Intake/RetainedMail.cs` (flag), `Core/Intake/RetainedMailFolderMove.cs`, `src/Pegasus.Infrastructure/Email/Graph*Send*.cs`, Worker registration, migration (flag columns), Core/Infrastructure tests.

## Verification

- [ ] No test profile sends or mutates a mailbox; composed-or-absent proven.
