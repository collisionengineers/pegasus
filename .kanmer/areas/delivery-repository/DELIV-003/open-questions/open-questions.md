# Open questions — DELIV-003

- [ ] Does “after DELIV-002 completed” mean **after DELIV-002's PR is merged
  into `dev` and its CI is green** (recommended), rather than after Kanmer
  Done? Waiting for Done deadlocks: DELIV-002 needs this first promotion to
  reach merged `main` and obtain its required proof.

## Parked (explicitly deferred)

- The exact `origin/main` and `origin/dev` SHAs, and the corresponding
  `MERGE AUTH GRANTED`, are determined only after the convergence PR lands.
  They are execution-time approval data, not a planning assumption.
