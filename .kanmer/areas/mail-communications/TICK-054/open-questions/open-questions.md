# Open questions — MAIL-13

- [x] **What Outlook mutation scope is required?** — Pegasus supports read/unread, approved-category add/remove, flag/unflag, ordinary confirmed folder movement through MAIL-07, recoverable deletion to Deleted Items, and restoration. Compose/reply/forward/send remains MAIL-12.
- [x] **Does MAIL-13 supersede the binding no-permanent-deletion rule?** — No. Operator decision 2026-08-20: recoverable deletion is acceptable. Pegasus exposes no permanent-delete action; the protected operator truth, FRD-04, ADR-0004, and design prohibition remain unchanged.
- [x] **Which Outlook categories are approved and who owns that list?** — The exact list is TBD and must be configurable in Pegasus email administration through [[MAIL-004]]. MAIL-13 accepts only that configured catalogue, never arbitrary strings. MAIL-004 research will prove or reject proposed email search/linking callers; unsupported speculative use cases produce no dormant code.

## Settled action boundary

- Read/unread, one configured approved-category add/remove, and flag/unflag apply to one opened exact message.
- Deletion to Deleted Items and restoration are exact recoverable moves reusing MAIL-07; restore targets the server-recorded prior approved folder.
- MAIL-07 keeps ordinary designated-folder moves; MAIL-12 keeps compose/reply/forward/send.
- Retained Pegasus evidence/history is never deleted by an Outlook action.
- No Pegasus role receives permanent deletion.

## Parked (explicitly deferred)

- [x] **What live Outlook/Graph verification is required?** — After separately approved permission activation, use an exact operator-approved disposable message to exercise read/unread, one configured category add/remove, flag/unflag, designated-folder movement, deletion to Deleted Items, and restoration. Record immutable identity and before/after state; abort on mismatch. Permanent deletion is excluded.
- [x] **Are Graph permissions and a live journey authorized by this decision?** — No. Local implementation uses fake Graph HTTP and LocalDB. Production activation first needs exact approval for the Entra application `Mail.ReadWrite`/admin-consent change, Exchange Application RBAC scope, and a negative outside-scope test. Each live write still needs exact-target approval immediately before execution.
