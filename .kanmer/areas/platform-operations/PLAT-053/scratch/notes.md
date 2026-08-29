## 2026-08-29 — round-2 remediation of the adversarial verification

Commit `99483f55` pushed to `task/plat-053-external-work-vocabulary`; PR #613
body rewritten (via `gh api -X PATCH` — `gh pr edit` fails on this machine
with `authentication token is missing required scopes [read:project]`).

All seven findings disposed in the `plan` doc under "Review findings —
dispositions (round 2)": five fixed, two deferred to created tickets
([[PLAT-056]] ten remaining files, [[PLAT-057]] EfEvaSubmissionWorkStore
coverage). Nothing rejected, nothing risk-accepted, nothing silenced.

The verifier was right on every finding I could check. Its two honesty
callouts were fixed at source rather than reworded: the "mechanical
substitution" claim is now true of all three files because the restructure
was withdrawn, and the `EvaSubmissionPersistenceTests` citation is struck
from `plan`, `files` and `post-implementation-report`.

Real numbers this round: build exit 0 / 0 warnings / 0 errors; focused
tests 138 passed, 0 failed, 1 skipped across 14 classes (101/0/1 + 37/0/0),
both runs exit 0. Widened from the 3 classes cited in round 1 to every
non-Browser class touching `ExternalWorkItems` or `IEvaSubmissionQueries`.

Ticket left in `review` and `proof` left unwritten, per lane instructions.
