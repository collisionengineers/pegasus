# Open questions — MAIL-11

No unresolved operator question. Follow FRD-08: individual-message results, explicit mailbox/folder scope, accessible pagination, visible match location, unsupported attachment disclosure and no backlog reconstruction.

## Parked (explicitly deferred)

- [x] **What live Outlook/Graph/cloud verification is required?** — Resolved by the operator on 2026-08-19: after deployment, exercise the full authenticated, read-only production journey against the currently linked mailbox—browse and paginate, apply mailbox/folder scope, search retained body and supported attachment filename/content, inspect visible match locations and scoped threads, and search accepted Deleted Items where the already-approved Graph scope permits it. Do not broaden Graph permissions, reconstruct historical mail, or mutate messages/cloud state.
