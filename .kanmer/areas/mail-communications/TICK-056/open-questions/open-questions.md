# Open questions — UI-10

No unresolved operator question. The detailed user-visible behaviour is already settled in FRD-08 and docs/design/README.md; action controls appear only as their owning MAIL tickets become available.

## Parked (explicitly deferred)

- [x] **What live Outlook/Graph/cloud verification is required?** — Resolved by the operator on 2026-08-19: after deployment, run the full authenticated production browser journey through the email workspace—default list, mailbox/folder/queue/search filters, pagination, freshness/refresh, accessible preview, exact-message detail, attachments/thread, classification and operational destination, folder recommendation, suggestions, navigation context, and available action controls. Read-only behavior may be exercised directly. A mutation control may be executed only when its owning MAIL ticket separately authorizes the exact target and operation; UI-10 grants no additional write authority.
