# Checklist — MAIL-10

- [x] Original MAIL-10 implementation and PR-048..050 correction handoff.
- [x] Read PR-051/052 and inspect protected authority/release conventions.
- [ ] Bind prepared authority to exact message, receipt and Link/Unlink intent.
- [ ] Reject/compensate cross-message and both cross-action uses.
- [ ] Preserve exact same-message/same-action replay.
- [ ] Retain recovery authority when release is transiently unconfirmed.
- [ ] Add exact authenticated transfer and fail-once release retry tests.
- [ ] Run simplification and proportional Release verification.
- [ ] Reconcile blocker/TICK docs, PIR, traceability and commit.
- [ ] Push replacement head and leave TICK-052, PR-051 and PR-052 in Review.
