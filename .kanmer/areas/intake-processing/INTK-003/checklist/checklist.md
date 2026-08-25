# Checklist

- [x] Confirm INTK-041 is merged and INTK-040 overlap is clear; create/take a fresh INTK-003 worktree from `origin/dev`.
- [x] Extend the existing intake recovery contract with the one-minute stale-dispatch cutoff.
- [x] Implement bounded, oldest-first, race-safe recovery of unleased stale `dispatched` rows to `pending`.
- [x] Update existing interface fakes/decorators without adding a parallel recovery route.
- [x] Add stale/fresh, race, bounded fairness, redispatch, and process-once tests.
- [x] Run focused tests and required Release verification.
- [x] Run and record the four simplification lenses.
- [x] Write the report, commit `a2f46891`, push, open PR #551 to `dev`, and move to Review.
