# Open questions — DELIV-003

- [x] DELIV-003 begins after DELIV-002's policy PR is merged into `dev` with
  CI green, rather than after Kanmer Done. This avoids the proof-cycle: the
  first DELIV-003 promotion supplies merged-`main` evidence for both tickets.
- [x] DELIV-002 owns the explicit, single-use allowance to merge
  `origin/main` into this ticket's `origin/dev`-based branch and PR it to
  `dev`. If the merged policy lacks that allowance, stop and correct
  DELIV-002; do not use an alternate shared-ref operation.

## Parked (explicitly deferred)

- The exact `origin/main` and `origin/dev` SHAs, and the corresponding
  `MERGE AUTH GRANTED`, are determined only after the convergence PR lands.
  They are execution-time approval data, not a planning assumption.
