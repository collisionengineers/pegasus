# Open questions — MAIL-11

No unresolved operator question. Follow FRD-08: individual-message results, explicit mailbox/folder scope, accessible pagination, visible match location, unsupported attachment disclosure and no backlog reconstruction.

## Parked (explicitly deferred)

- [x] **What live Outlook/Graph/cloud verification is required?** — Resolved by the operator on 2026-08-19: after deployment, exercise the full authenticated, read-only production journey against the currently linked mailbox—browse and paginate, apply mailbox/folder scope, search retained body and supported attachment filename/content, inspect visible match locations and scoped threads, and search accepted Deleted Items where the already-approved Graph scope permits it. Do not broaden Graph permissions, reconstruct historical mail, or mutate messages/cloud state.

# Research refresh — 2026-08-20

No new unresolved operator question was found after checking FRD-08, both epic contexts, the 2026-08-19 live-verification decision, and current source at origin/main/origin/dev.

The implementation plan must choose the smallest single persisted representation for searchable attachment text and unsupported status using the existing intake-reader output. That is a technical design choice under the repository's one-owner rule, not missing product intent.

## Parked (explicitly deferred)

- [x] **What live Outlook/Graph/cloud verification is required?** — Existing 2026-08-19 answer remains binding: the post-deploy journey is read-only, limited to the currently linked mailbox and already-approved Graph scope, with no broader permission, historical reconstruction or mutation.
