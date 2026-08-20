# Open questions — TICK-057

No unresolved question remains for planning. UI-14 uses the canonical MAIL-22 taxonomy and MAIL-02 operational-destination policy, renders **Unidentified** for the former broad Needs sorting abstention, and preserves **Triage** as a separate workflow and queue.

## Parked (explicitly deferred)

- [x] **What live Outlook/Graph/cloud verification is required?** — Resolved by the operator on 2026-08-19. After deployment, perform an authenticated, read-only production check of the detailed classification views and the distinct Receiving work, Queries, reasoned Other, Unidentified, and Triage filters against current retained mail. Record mailbox/folder/filter scope, counts, pagination and exact classifications where examples exist. An empty view is honest evidence; do not fabricate mail, broaden Graph scope, or mutate mailbox/cloud state.
- [x] **Does UI-14 wait for MAIL-23?** — Resolved by the accepted programme order. MAIL-23 lands first because it overlaps the retained-mail store and Index model, then UI-14 refreshes from merged `origin/dev`. MAIL-23’s Outlook-folder binding is not a behavioural prerequisite for application queue filtering.
