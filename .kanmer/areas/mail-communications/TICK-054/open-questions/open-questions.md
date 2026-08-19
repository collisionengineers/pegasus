# Open questions — MAIL-13

- [x] **What Outlook mutation scope is required?** — Pegasus is a full email-management system. Incorporate the message-management functionality represented by the email workspace: read/unread, approved Outlook categories, flags, folder movement, deletion to Deleted Items, restoration, and permanent deletion where Outlook permits it. Every mutation remains authorised, exact-message-scoped, version-checked, attributable, idempotent, visibly recoverable where Outlook supports recovery, and explicitly confirmed for destructive actions. Compose/reply/forward/send remains owned by MAIL-12.

## Parked (explicitly deferred)

- [x] **Is full live Outlook/Graph verification required?** — Yes. Resolved by the operator on 2026-08-19: production acceptance must exercise read/unread, approved category, flag/unflag, designated folder movement, delete to Deleted Items, restoration, and explicitly confirmed permanent deletion where Outlook supports it. Use only an exact operator-approved disposable test message. Record immutable identity and initial state, obtain exact mailbox/message/folder/operation approval immediately before writes, verify restoration before the irreversible step, and obtain fresh explicit confirmation immediately before permanent deletion. Abort on identity/version mismatch; never use operational correspondence.
