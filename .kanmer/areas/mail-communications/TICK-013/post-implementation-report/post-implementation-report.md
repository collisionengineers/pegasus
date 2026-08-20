# Post-implementation report — MAIL-14 (backfill, VERIFY2, 2026-08-20)

No code was written under this ticket: MAIL-14 was already implemented, tested, and shipped (release ≤13). This ticket's work was read-only verification of the committed capability text against the shipped code and live production state.

- Detection contract matches the FRD clause-by-clause: exact reply-chain identity match, retained immutable item/conversation identities, approval-scoped polling, ambiguous/absent → unconfirmed. Evidence in `research.md`.
- Caller is real and running: `SentEvidencePollFunction` enabled in production; `ApprovedSentPollStates.LastCompletedAtUtc = 2026-08-20 05:39:15Z` with no failure code.
- Nothing in the shipped code deviated from the capability text; no fixes were required under this ticket (the one adjacent defect — throwing on an unapproved mailbox — is owned and fixed by MAIL-003, merged to dev 2026-08-20, not yet released).

Deviations from plan: none (plan was verification-only and was executed as written).
