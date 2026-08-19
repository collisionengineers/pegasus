# Open questions — MAIL-13

- [x] **What Outlook mutation scope is required?** — Pegasus is a full email-management system. Incorporate the message-management functionality represented by the email workspace: read/unread, approved Outlook categories, flags, folder movement, deletion to Deleted Items, restoration, and permanent deletion where Outlook permits it. Every mutation remains authorised, exact-message-scoped, version-checked, attributable, idempotent, visibly recoverable where Outlook supports recovery, and explicitly confirmed for destructive actions. Compose/reply/forward/send remains owned by MAIL-12.

## Parked (explicitly deferred)

- [ ] Real Outlook/Graph/cloud activation and live verification — requires explicit approval for exact targets and operations.
