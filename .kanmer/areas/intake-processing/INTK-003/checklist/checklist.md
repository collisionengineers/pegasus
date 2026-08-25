# Checklist

- [ ] Confirm INTK-041 is merged and INTK-040 overlap is clear; create/take a fresh INTK-003 worktree from `origin/dev`.
- [ ] Extend the existing intake recovery contract with the one-minute stale-dispatch cutoff.
- [ ] Implement bounded, oldest-first, race-safe recovery of unleased stale `dispatched` rows to `pending`.
- [ ] Update existing interface fakes/decorators without adding a parallel recovery route.
- [ ] Add stale/fresh, race, bounded fairness, redispatch, and process-once tests.
- [ ] Run focused tests and required Release verification.
- [ ] Run and record the four simplification lenses.
- [ ] Write the report, commit, push, open the PR to `dev`, and move to Review.
