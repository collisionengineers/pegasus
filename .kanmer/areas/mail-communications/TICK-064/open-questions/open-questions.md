# Open questions — MAIL-23

- [x] **Are operational classification and Outlook folder destination separate?** — Yes. Every recognised message retains its detailed category/subtype; folder destination is a separately mapped fact.
- [x] **Which Outlook folder types form the canonical starting set?** — Instructions, Audits, Diminution, New clients, Case queries, Enquiries, Billing, Pre-instructions, No action, Images, Cancellations, Case updates and Other.
- [x] **How is the mapping documented?** — FRD-08 owns one exhaustive in-repo table from detailed classification criteria/evidence to operational destination and administrator-approved folder type/identity. Infrastructure resolves stable approved folder identities; clients never supply arbitrary destinations.

## Parked (explicitly deferred)

- [ ] Real Outlook/Graph/cloud activation and live verification requires exact-target approval.
