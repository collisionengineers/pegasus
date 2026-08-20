---
id: MAIL-002
type: ticket
title: >-
  Mailbox administration hides mailbox identifiers and adds addresses by email
  alone
status: verifying
area: mail-communications
assignee: claude-code
profile: fix
stageEntered:
  preparing: '2026-08-20T03:49:15.419Z'
  review: '2026-08-20T04:28:26.194Z'
  verifying: '2026-08-20T06:12:58.799Z'
taken_at: '2026-08-20T04:20:47.934Z'
branch: task/mail-002-admin-no-identifiers
worktree: ../pegasus-worktrees/mail-002
labels:
  - administration
  - ui
  - operator-reported
links:
  - PLAT-009
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
  - docs/frd/frd-12-operator-experience.md
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/451'
archived: false
created: '2026-08-20T03:16:37.641Z'
updated: '2026-08-20T06:12:58.799Z'
---

## What

Operator, 2026-08-20, verbatim: *"Administration for emails still has dev facing fields such as mailbox ID - not appropriate for a user facing application. Mailbox IDs need to be handled by us, in the backend. Never shown to users, even those that are set as Administrators. There must be a simple user facing option to add an email address. The page also contains narration/bad UX copy."*

So `/Administration/Mailboxes`:
- never displays or asks for mailbox/folder identifiers — the backend resolves them (e.g. via Graph lookup from the address);
- offers a simple add-an-email-address flow (address + route scope, nothing else);
- loses the remaining narration copy.

## Why

[[PLAT-009]] fixed the layout; the identifier fields and copy survived. Internal identifiers operator-facing violate the design authority (docs/design/README.md:168).

## Verification

- [ ] No mailbox/folder ID appears anywhere on the page (view or form), for any role.
- [ ] Adding an address by email alone produces a working approved mailbox (identity resolved backend-side); failures are stated honestly.
- [ ] Copy passes the narration and banned-terms rules; browser + accessibility suites green.
