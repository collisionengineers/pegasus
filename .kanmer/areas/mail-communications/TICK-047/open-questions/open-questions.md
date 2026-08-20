# Open questions — TICK-047 / MAIL-05

No unresolved product or technical choice remains. MAIL-23 is merged to `origin/dev`; MAIL-05 uses its exact `MailLogicalFolderPolicy`, `IApprovedMailboxStore`, `ApprovedMailbox.MailboxIdentity`, and typed folder bindings.

## Parked (explicitly deferred)

- [x] **Which folder vocabulary and mapping apply?** — The sole owners are `MailLogicalFolders.All` and `MailLogicalFolderPolicy.Map`; no list or mapping is copied.
- [x] **How is the exact mailbox selected?** — Ordinal match of retained `Summary.MailboxId` to the current approved row's `MailboxIdentity`, not its aggregate `Id` or address.
- [x] **What happens when a recommendation cannot be proven?** — Render an accessible unavailable state with the Core-derived reason. Never infer a fallback; `NoAction` is still a valid configured recommendation.
- [x] **What remains outside this ticket?** — MAIL-06/07 confirmation/move, persistence, transactions, operation keys, Graph calls, retry, MCP expansion, deployment, and all mailbox/cloud writes.
- [x] **What later live evidence is permitted?** — After deployment, only the already-approved authenticated read-only viewer check; no confirmation, folder/configuration change, Graph-scope change, or mailbox mutation.
