# Open questions — TICK-011

Created retrospectively on 2026-08-19 by [[DELIV-012]]. The ticket reached
`done` with no `open-questions` document at all, so the questions-resolved gate
passed vacuously — it counts unticked lines in a file that did not exist. This
document gives the one genuinely open item a tracked home; it does not reopen
the ticket, whose delivery evidence stands.

## Open

- [ ] **INT-17 has no production caller execution.** Automatic VRM reading is
  shipped and present in the deployed release-10 tree (verified: 20 ImageIntake
  paths in `origin/main`, delivery commits `ef3eb4c7` and `ba65c1ed`), and its
  focused Core suite passes on merged `main` (78/78). What has never been
  evidenced is the capability *running in production against real intake* — the
  2026-08-19 production JPEG that prompted [[INTK-006]] is the live counter-example:
  both recognition layers ran and the suggestion fell below threshold, leaving the
  material with no Image Intake, association or Image-initiated Case.
  Owner: [[INTK-006]] (grouped recognition, detector/recognizer diagnostics) and
  [[INTK-008]] (Image-initiated Case lifecycle). This item closes when one of
  those demonstrates a real production recognition outcome, not when INT-17's
  code ships again.

## Parked (explicitly deferred)

- The two unreachable SHAs formerly cited in `proof` (`ae6f0c2d`, `f7d99b18`).
  Withdrawn rather than re-derived: the reachable delivery commits are recorded
  in `proof` and the pre-rebase objects have no refs, so nothing further can be
  proved from them.
