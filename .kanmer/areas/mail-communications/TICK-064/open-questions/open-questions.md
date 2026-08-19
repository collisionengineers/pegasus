# Open questions — MAIL-23

- [x] **Are operational classification and Outlook folder destination separate?** — Yes. Every recognised message retains its detailed category/subtype; folder destination is a separately mapped fact.
- [x] **Which Outlook folder types form the canonical starting set?** — Instructions, Audits, Diminution, New clients, Case queries, Enquiries, Billing, Pre-instructions, No action, Images, Cancellations, Case updates and Other.
- [x] **How is the mapping documented?** — FRD-08 owns one exhaustive in-repo table from detailed classification criteria/evidence to operational destination and administrator-approved folder type/identity. Infrastructure resolves stable approved folder identities; clients never supply arbitrary destinations.

## Parked (explicitly deferred)

- [x] **What live Outlook/Graph/cloud verification is required?** — Resolved by the operator on 2026-08-19: after deployment, perform an authenticated, read-only production check for the currently linked mailbox that resolves and displays every configured canonical folder identity. For each of the 13 folder types, record the stable business key/label and resolved administrator-approved identity, or report it honestly as unconfigured/unavailable. Do not create, rename, move, or remap Outlook folders and do not invent fallback identities.
