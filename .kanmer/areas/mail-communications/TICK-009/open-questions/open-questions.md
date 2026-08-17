# Open questions — TICK-009

*The open questions. Not scratch — these **block** the ticket at three real gates; scratch is the notepad and is never gated.*

Unresolved questions that shape or block this work. Resolve or escalate — never guess silently. Questions only the user can answer go to them now, not at planning time.

**The format is load-bearing, not decoration.** `questions-resolved` counts unticked `- [ ]` lines *above the Parked heading* and blocks `leave-preparing`, `enter-review` and `enter-done` while any remain. So: **one question per checkbox**, and record the answer on the same bullet when you tick it. A question buried in a paragraph is invisible to the gate and to the next reader.

- [x] **Deploy or live-verify classification in this ticket?** — Remaining MAIL-21 evidence states include deployment and live verification, which need exact-target approval. Taken default: no. This ticket produces local volume-cohort evidence only.
- [x] **Invent labelled ground truth from this machine's flat `corpus/*.eml` filenames?** — Corpus is immutable and unlabelled here. Taken default: no. Volume counts only; labelled accuracy stays skippable when those folders are absent.
- [x] **Add new QDOS predicates or a multi-rule winner?** — Precedence is an open decision; extra families are deferred by `boundaries.md`. Taken default: no. Keep explicit ambiguity.

## Parked (explicitly deferred)

The escape hatch, and the reason parking is honest rather than a way to tick a box you did not answer: everything below this heading is **not counted**. Say why it is safe to defer and what would reopen it. Rename this heading and the gate stops seeing it — the exact string is asserted by a test.

- [ ] Operator acceptance of classification thresholds — reopen when a labelled cohort + untouched holdout exists on an approved machine and the operator reviews it (parallel to INT-21, not this slice).
- [ ] Staff confirmation UI, correction history, folder move, queue mapping — owned by MAIL-04/05/02/23 and UI-10/14 at `Next / 0.3.0`.
