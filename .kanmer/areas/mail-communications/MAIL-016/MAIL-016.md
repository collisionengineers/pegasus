---
id: MAIL-016
type: ticket
title: Correct the stale mailbox assertion left by MAIL-013
status: done
area: mail-communications
order: 2410
assignee: claude-fable-5
profile: fix
stageEntered:
  implementing: '2026-08-27T08:25:49.385Z'
  review: '2026-08-27T08:26:29.648Z'
  verifying: '2026-08-27T08:53:43.481Z'
  done: '2026-08-27T09:23:34.729Z'
labels:
  - mailbox
  - tests
  - ci
links:
  - MAIL-013
  - UIIMP-004
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - 78c734cc
prs:
  - '567'
deployment: n/a
archived: false
created: '2026-08-27T08:16:52.507Z'
updated: '2026-09-01T14:44:33.988Z'
---

## Problem

[[MAIL-013]] changed `MailWorkspaceWebTests.FirstMailboxFilter` to the stable
mailbox GUID (`TestMailboxId.From("instructions")`), but
`ExactMessageCanBeSearchedLinkedUnlinkedAndLinkedToAReplacement` still asserts
the literal `mailbox=instructions`. PR #563 merged with `sql-integration (1)`
red on exactly that assertion; #562 and #566 inherit the failure. The CI
diagnostic URL shows the page echoing the requested GUID correctly — the
product is right, the assertion is stale.

## Required outcome

Assert `mailbox={FirstMailboxFilter}`; no product change. `dev` green again.
The nine `fix(mail)` workaround commits on [[UIIMP-004]] are reverted there.

## Outcome
