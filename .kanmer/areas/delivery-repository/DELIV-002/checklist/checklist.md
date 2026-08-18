# Checklist — DELIV-002

Derived from `plan.md`; each box is independently checkable.

- [x] Replace the merge-commit release guidance in `docs/engineering.md` with
  the canonical non-force exact-SHA procedure, its exclusions, and the
  one-time branch-local DELIV-003 convergence allowance.
- [x] Align `AGENTS.md`'s authorization and allowed-operation rules with the
  canonical procedure: permit that one convergence PR, forbid a direct
  `dev` update, and keep the exception single-use.
- [x] Change `scripts/Test-MainBranchHistory.ps1` from a two-parent predicate
  to append-only and release-branch ancestry validation.
- [x] Fetch `origin/dev`, pass it to the revised guard, and preserve the
  existing CI path-classification wiring in `.github/workflows/ci.yml`.
- [x] Update `MainBranchHistoryGuardTests.cs` for fast-forward acceptance,
  later-`dev` ancestry, direct-main rejection, synthetic-merge rejection, and
  the existing invalid-history cases.
- [x] Run the four-lens simplification pass over the task diff and record dated
  dispositions in `plan.md`; open the reviewed single PR to `dev`.
- [x] Run `dotnet restore`, the Release build, focused architecture tests, and
  documentation-link validation; record their results in the
  post-implementation report.

## Progress notes

- 2026-08-18: [[DELIV-003]] begins after this ticket's PR is merged into
  `dev` with CI green, not after this ticket reaches Done. Its first
  promotion supplies the merged-`main` proof for both tickets.
- 2026-08-18: Implemented the delivery-policy, guard, workflow, and fixture
  changes in `task/deliv-002-fast-forward-main-release`. Local validation
  passed: restore, Release build, 96 architecture tests, documentation links,
  CI-change classification, and Markdown-placement checks.
- 2026-08-18: Opened PR #396 to `dev`; independent Kanmer review is next.
