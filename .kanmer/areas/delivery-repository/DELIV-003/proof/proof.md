# Proof — DELIV-003 (verified on merged `main`, 2026-08-18)

- Convergence PR #399 (`a592beae`, merge of `origin/main` `2b0df78c` into the task branch, no tree change) merged to `dev` 2026-08-18T09:25:51Z; from then `origin/main` was an ancestor of `origin/dev` (`git merge-base --is-ancestor origin/main origin/dev` → true at preflight).
- First exact-SHA promotion executed as part of release 9 ([[DELIV-008]]): recorded `origin/dev` SHA `f1e116c6eb939f901f32e5f89d58d1d8a4701851` (PR #400 checks 10/10 SUCCESS), operator `MERGE AUTH GRANTED`, atomic lease-checked push → `2b0df78c..f1e116c6 main`; readback both remote heads == `f1e116c6…`.
- Revised main-push guard on run 32133221206: `Main history guard passed: 9 new first-parent commit(s); main head is contained in the release branch.`; whole run success.
- Documentation refresh determination: no source change in the convergence itself; the release-9 docs refresh landed via PR #404.

Checklist items 5–7 of this ticket are thereby satisfied. PR #399 merged 2026-08-18.
