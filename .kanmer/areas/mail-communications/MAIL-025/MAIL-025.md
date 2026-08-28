---
id: MAIL-025
type: ticket
title: Port the Inbox list and message pages to the Integrated Operations Workspace
status: verifying
area: mail-communications
assignee: zcode
profile: feature
stageEntered:
  preparing: '2026-08-28T11:25:43.590Z'
  review: '2026-08-28T14:25:03.997Z'
  verifying: '2026-08-28T18:39:58.208Z'
taken_at: '2026-08-28T13:40:46.480Z'
branch: task/mail-025-inbox-port
worktree: ../pegasus-worktrees/mail-025-inbox-port
labels:
  - ui
  - wave-2
  - inbox
groups:
  - EPIC-011
  - EPIC-006
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
archived: false
created: '2026-08-28T08:35:23.835Z'
updated: '2026-08-28T18:39:58.208Z'
---

## What

Wave 2 lane B of [[EPIC-011]]. Port `Pages/Mail/Index.cshtml` (filter bar, three panes: scope list with icon wells and counts, messages with sort toggle and bounded pagination, server-rendered preview for `?selected=`) and `Pages/Mail/Message.cshtml` (record head/bar, tabs Message / Attachments / Thread / Case, decision card, corrections timeline, attachments table, case association dialogs) to `context.md` §1.3. Scope counts need per-scope count queries on `IRetainedMailQueries` (add them here). Reply / Forward / Compose / Flag / Delete are NOT rendered in this ticket (wave 4, after the outbound-mail backend).

## Owns

`src/Pegasus.Web/Pages/Mail/**`, `src/Pegasus.Web/Presentation/MailBodyPresentation.cs`, `MailClassificationSelection.cs`, `src/Pegasus.Core/Intake/RetainedMail.cs` (count/sort additions only), `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs`, `Browser/MailWorkspaceBrowserTests.cs`.

## Blocked by

[[PLAT-029]].

## Verification

- [ ] Existing handlers (classification correction, folder move, association) keep antiforgery, version and reason behaviour.
- [ ] No clipped text/overflow at 1580/1100/760.
