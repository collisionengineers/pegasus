# Open questions — MAIL-13

- [x] **What Outlook mutation scope is required?** — Pegasus is a full email-management system. Incorporate the message-management functionality represented by the email workspace: read/unread, approved Outlook categories, flags, folder movement, deletion to Deleted Items, restoration, and permanent deletion where Outlook permits it. Every mutation remains authorised, exact-message-scoped, version-checked, attributable, idempotent, visibly recoverable where Outlook supports recovery, and explicitly confirmed for destructive actions. Compose/reply/forward/send remains owned by MAIL-12.

## Parked (explicitly deferred)

- [x] **Is full live Outlook/Graph verification required?** — Yes. Resolved by the operator on 2026-08-19: production acceptance must exercise read/unread, approved category, flag/unflag, designated folder movement, delete to Deleted Items, restoration, and explicitly confirmed permanent deletion where Outlook supports it. Use only an exact operator-approved disposable test message. Record immutable identity and initial state, obtain exact mailbox/message/folder/operation approval immediately before writes, verify restoration before the irreversible step, and obtain fresh explicit confirmation immediately before permanent deletion. Abort on identity/version mismatch; never use operational correspondence.

# Research refresh — 2026-08-20

- [ ] **Does the 2026-08-19 MAIL-13 decision supersede the binding “no permanent deletion” rule, and if so which staff role may perform it?** — Protected `docs/operator-notes.md` says Administrators have “No permanent deletion”; FRD-04 prohibits it for every staff role; accepted ADR-0004 says the domain permits it through no surface; and design repeats the prohibition. The ticket decision requests permanent deletion where Outlook supports it. Do not implement or amend the protected rule by inference. The operator must explicitly resolve the conflict and name the permitted actor boundary; then reconcile the governing documents before code.
- [ ] **Which Outlook category names are approved, who owns that list, and may Pegasus assign a category absent from the mailbox master list?** — No canonical category set/configuration exists on `origin/dev`, and a generic mailbox-rule editor is prohibited before policy acceptance. The implementation may not accept arbitrary strings or invent values. Once answered, category add/remove must preserve unrelated existing categories.

## Settled action boundary

- Read/unread, one approved category add/remove, and flag/unflag apply to one opened exact message.
- Deletion to Deleted Items and restoration are exact moves reusing MAIL-07; restore targets the server-recorded prior approved folder.
- MAIL-07 keeps ordinary designated folder moves; MAIL-12 keeps compose/reply/forward/send.
- Retained Pegasus evidence/history is never deleted by an Outlook action.

## Parked (explicitly deferred)

- [x] **Are Graph permissions and a live journey authorized by this ticket decision?** — No. Local implementation uses fake Graph HTTP and LocalDB. Production activation first needs exact approval for the Entra application `Mail.ReadWrite`/admin-consent change, Exchange Application RBAC scope, and a negative outside-scope test. Each live write then needs exact operator approval for the disposable mailbox/message/folder/category/actions immediately before execution. Permission is not operation authority.
- [x] **If permanent deletion is later authorized, is its earlier journey approval enough?** — No. Verify the restored exact immutable message and current Deleted Items state, then obtain fresh explicit confirmation immediately before `permanentDelete`. Abort on stale version, identity/folder mismatch, or uncertain target. An unknown response is recorded and never blindly retried. Graph places the item in Purges; do not claim guaranteed physical erasure where tenant hold/retention applies.
