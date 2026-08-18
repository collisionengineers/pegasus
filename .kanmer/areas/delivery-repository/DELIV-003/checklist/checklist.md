# Checklist — DELIV-003

Derived from `plan.md`; each box is independently checkable.

- [ ] Verify DELIV-002 is merged and green on `origin/dev`, and that its
  merged policy contains the required exact-SHA procedure and single-use
  convergence allowance.
- [ ] Create the dedicated task worktree from that `origin/dev`, record the
  starting remote SHAs, and merge `origin/main` into the task branch without
  rewriting history or directly updating `dev`.
- [ ] Re-read current-state documentation and either update observed facts in
  the task branch or record the justified no-change determination.
- [ ] Run applicable checks and the four-lens simplification pass; commit,
  push, and open the reviewed convergence PR to `dev`.
- [ ] After the PR merges green, fetch remote refs, prove `main ≤ dev`, record
  the exact `dev` SHA, and obtain explicit `MERGE AUTH GRANTED` for those
  current refs before any `main` update.
- [ ] Promote only the recorded SHA without force, fetch both refs, require
  equality, and confirm the revised main-push CI run passes.
- [ ] Write merged-`main` proof for DELIV-003 and DELIV-002 with the release
  evidence and documentation-refresh determination.

## Progress notes

- 2026-08-18: Planned while DELIV-002 PR #396 awaits independent review. The
  ticket remains in Preparing and must not be taken until that policy reaches
  `origin/dev` with green CI.
