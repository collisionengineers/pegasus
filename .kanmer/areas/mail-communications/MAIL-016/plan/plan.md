# Plan — MAIL-016

## Premise (verified)

- CI evidence: PR #563, #566 and #562 all fail `sql-integration (1)` on
  `ExactMessageCanBeSearchedLinkedUnlinkedAndLinkedToAReplacement` with
  `Not found: "mailbox=instructions"`. The diagnostic assertion added on #562
  printed the actual candidate URL:
  `?mailbox=49f47eb9-c5b0-464f-b8f0-8c90ba061728&pageNumber=2&…` — which is
  exactly `TestMailboxId.From("instructions")`, the value the test itself sent.
- `git show c74c3257` (MAIL-013) changed every request in this file from
  `mailbox={FirstMailboxId}` to `mailbox={FirstMailboxFilter}` (the GUID) but
  left line 178 asserting the old literal.

## Step

1. Change line 178 to `Assert.Contains($"mailbox={FirstMailboxFilter}", …)`.
   Reuses the existing `FirstMailboxFilter` helper; nothing else changes.

## Acceptance

- Focused local run of `MailWorkspaceWebTests`: pass.
- Fresh GitHub run: all three SQL shards green.

## Simplification pass — 2026-08-27

n/a — one assertion, no product code.
