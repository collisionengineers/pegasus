# Open questions — UI-14

No separate operator question beyond MAIL-02's exhaustive mapping. Keep Needs sorting and Triage distinct even if navigation presents them beside the three named queues.

## Parked (explicitly deferred)

- [x] **What live Outlook/Graph/cloud verification is required?** — Resolved by the operator on 2026-08-19: after deployment, run an authenticated, read-only production check of the detailed classification views and distinct Receiving work, Queries, Needs sorting, and Triage queues against real retained mail. Verify counts, filtering, paging, preserved scope, and exact classifications where examples exist. Record an empty queue honestly when production has no current example; do not fabricate data, broaden Graph scope, or mutate mail/cloud state.
