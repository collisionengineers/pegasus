# Open questions — MAIL-01

No unresolved operator question. Use mailbox + RFC Internet-Message-ID as the durable duplicate boundary when present, retain Graph/provider IDs separately, and fail closed when required identity is absent or contradictory.

## Parked (explicitly deferred)

- [x] **Is Outlook/Graph activation still outstanding?** — No. Resolved with the operator on 2026-08-19: the production Graph mailbox route is already live. Release 1 live-verified Inbox/Sent processing; release 10 re-verified the enabled Worker and a successful Inbox poll; a 2026-08-19 read-only Azure check found both `InboxPollFunction` and `SentEvidencePollFunction` enabled. This closes the generic activation question. Any future live write or post-MAIL-01 verification that changes or rebinds exact mailbox/cloud state still requires fresh approval for its exact targets and operations.
