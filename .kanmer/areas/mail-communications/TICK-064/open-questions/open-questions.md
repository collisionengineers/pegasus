# Open questions — TICK-064 / MAIL-23

- [x] **Are operational classification and Outlook folder destination separate?** — Yes. MAIL-02's operational destination remains a pure Core projection of the detailed classification; MAIL-23 supplies the separate logical folder outcome and mailbox-approved exact identity binding.
- [x] **Which Outlook folder types form the canonical starting set?** — Instructions, Audits, Diminution, New clients, Case queries, Enquiries, Billing, Pre-instructions, No action, Images, Cancellations, Case updates and Other.
- [x] **How is the mapping documented?** — FRD-08 owns the exhaustive classification/destination/folder catalogue. Code implements one typed Core projection from the current classification; Infrastructure resolves administrator-approved exact identities and does not copy the catalogue.
- [x] **What does MAIL-23 own versus MAIL-05 and MAIL-07?** — MAIL-23 owns the logical-folder policy and approved mailbox binding. MAIL-05 owns a recommendation for one retained message. MAIL-07 owns separate staff confirmation and the exact-message move. None accepts an arbitrary client-selected destination.
- [x] **What is the fail-closed vocabulary after INTK-007?** — Unidentified. It is distinct from Triage, Blocked intake, incomplete Audit evidence and Image Intake; it has no automatic folder recommendation.
- [x] **Must MAIL-23 persist a recommendation on each retained message?** — No. The recommendation is re-derived from the current classification and mailbox binding, so correction cannot leave stale duplicate state.

## Parked (explicitly deferred)

- [x] **What live Outlook/Graph/cloud verification is required?** — After deployment, perform an authenticated read-only check for the currently linked mailbox. For each of the 13 logical folder types, record the stable type key/label and resolved administrator-approved exact identity, or report it honestly as unconfigured/unavailable. Do not create, rename, move or remap folders, broaden Graph scope, or invent fallback identities.
