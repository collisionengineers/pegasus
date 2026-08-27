# Independent review — PR #567 at 78c734cc — 2026-08-27

Reviewer: fresh general-purpose agent (not the implementer).

- Root cause confirmed: MAIL-013 (`c74c3257`) rewrote every request in
  `MailWorkspaceWebTests` to `FirstMailboxFilter` (the GUID pinned in
  `TestMailboxId.cs:12-15`) and left the literal at line 178 untouched.
  `Message.cshtml.cs:42-43,172` binds and echoes `mailbox` unchanged in every
  `asp-route-mailbox` — the page could never render `mailbox=instructions`.
  No product bug.
- Diff: one line; assertion strength preserved (now specific to the seeded
  mailbox, matching sibling assertions at 554/560/1030).
- `git grep mailbox=instructions` on origin/dev: only line 178. Nothing missed.

Verdict: **APPROVE**, merge conditional on `sql-integration (1)` green.
