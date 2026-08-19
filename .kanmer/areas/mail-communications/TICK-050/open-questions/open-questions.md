# Open questions — MAIL-08

- [x] **Are suggestions advisory only?** — Yes. They are advisory, but may include a **Move** button. The button invokes the separately owned MAIL-07 confirmation workflow; it does not bypass eligibility, confirmation, destination validation, version checks, history, or failure handling.

## Parked (explicitly deferred)

- [x] **What live Outlook/Graph/cloud verification is required?** — Resolved by the operator on 2026-08-19: after deployment, perform an authenticated, read-only production mailbox-viewer check showing the suggested next actions for a real retained message. Do not invoke Move or any other action, and do not change Outlook, Graph, mailbox configuration, or cloud state.
