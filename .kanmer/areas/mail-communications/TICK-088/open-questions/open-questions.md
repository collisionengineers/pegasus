# Open questions — MAIL-12

- [x] **Is MAIL-12 activated despite its Later / 0.5.0 allocation?** — Yes. Activate it for full implementation in EPIC-006.
- [x] **What level of implementation is required?** — Full authenticated compose, reply, forward and send functionality through approved mailboxes, including recipient/CC/BCC, subject/body, attachments, thread/reply semantics, signatures configured by the approved mailbox, drafts, explicit send confirmation, idempotent submission, visible failure/retry, permanent attribution and Sent evidence reconciliation. UI and Automation callers must reuse one Core implementation.

## Parked (explicitly deferred)

- [ ] Enabling or verifying a real Outlook/Graph mailbox remains an external write and requires explicit approval for exact targets and operations.
