# Open questions — TICK-047 / MAIL-05

No unresolved product question remains. MAIL-05 is a read-only exact-message recommendation derived from the current classification and the MAIL-23 mailbox-approved binding. It accepts no destination and performs no move.

## Parked (explicitly deferred)

- [x] **Which designated Outlook destinations apply?** — Resolved by FRD-08 and [[TICK-064]]. MAIL-23 owns the canonical classification-to-logical-folder/no-recommendation policy and mailbox-approved bindings for Instructions, Audits, Diminution, New clients, Case queries, Enquiries, Billing, Pre-instructions, No action, Images, Cancellations, Case updates and Other. Unidentified has no automatic recommendation; Triage remains separate and does not itself infer a folder.
- [x] **When may MAIL-05 be planned and implemented?** — [[TICK-064]] must land first and now structurally blocks this ticket. Refresh MAIL-05 against the actual merged policy and binding symbols; do not guess them in advance.
- [x] **What live Outlook/Graph/cloud verification is required?** — Resolved by the operator on 2026-08-19. After deployment, perform an authenticated, read-only production mailbox-viewer check for the currently linked mailbox. Confirm that a real classified retained message displays the current policy-designated, administrator-configured exact folder recommendation and provenance, or an honest unavailable state. Do not confirm or move the message, create/rename folders, alter mailbox configuration, broaden Graph scope, or mutate cloud state.
