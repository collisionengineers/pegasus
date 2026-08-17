# Open questions — TICK-010

*The open questions. Not scratch — these **block** the ticket at three real gates; scratch is the notepad and is never gated.*

Unresolved questions that shape or block this work. Resolve or escalate — never guess silently. Questions only the user can answer go to them now, not at planning time.

**The format is load-bearing, not decoration.** `questions-resolved` counts unticked `- [ ]` lines *above the Parked heading* and blocks `leave-preparing`, `enter-review` and `enter-done` while any remain. So: **one question per checkbox**, and record the answer on the same bullet when you tick it. A question buried in a paragraph is invisible to the gate and to the next reader.

- [x] **Build the staff confirmation UI in this ticket?** — Capability title says "user-confirmed", which is the operator-confirmed taxonomy (operator-notes + FRD-08), not per-message Inbox confirmation. Taken default: no. UI-10 / 0.3.0 owns that surface. This ticket proves Other and Sent persist/reload.

## Parked (explicitly deferred)

The escape hatch, and the reason parking is honest rather than a way to tick a box you did not answer: everything below this heading is **not counted**. Say why it is safe to defer and what would reopen it. Rename this heading and the gate stops seeing it — the exact string is asserted by a test.

- [ ] Automated rules for remaining families and reply-to-Sent — `boundaries.md` defers automated application beyond the delivered QDOS-route classification; reopen with accepted predicates (open-decisions mailbox section).
- [ ] Authorised correction/reversal append-only history — MAIL-04 (`Next / 0.3.0`).
- [ ] Category to operational queue or Outlook folder — MAIL-02 / MAIL-23 / MAIL-05.
