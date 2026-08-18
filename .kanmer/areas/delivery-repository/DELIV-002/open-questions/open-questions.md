# Open questions — DELIV-002

- [x] GitHub-side branch protection and rulesets are intentionally out of scope on subscription grounds. The revised main-history guard is detective after a push; it cannot prevent an otherwise valid direct fast-forward.
- [x] The authorized release procedure is a manual, non-force promotion by the person holding `MERGE AUTH GRANTED`: fetch both refs; prove `origin/main` is an ancestor of `origin/dev`; record the reviewed `origin/dev` SHA; push that SHA to `refs/heads/main`; fetch again and require both remote heads to equal the recorded SHA. Any failed check stops the release.

## Parked (explicitly deferred)

- None.
