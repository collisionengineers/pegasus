# Checklist — INTK-048

- [x] Recognize effective current Case associations in the reconciliation owner.
- [x] Add focused Core regression coverage.
- [x] Add real-persistence manual-link reconciliation coverage.
- [x] Run focused and canonical verification commands with exit codes.
- [x] Complete and record the behavior-preserving simplification pass.
- [x] Write the post-implementation report, commit, push, and open the PR.

## Progress notes

- 2026-08-28: locked restore passed; final Release build passed with 0 warnings
  and 0 errors; focused Core reconciliation tests passed 9/9; SQL-backed
  `UnidentifiedReconciliationTests` passed 3/3.
- 2026-08-28: the first canonical non-Corpus run passed Core 1,096/1,096 and
  Architecture 100/100; Integration passed 1,102/1,103. The sole failure was
  `DueChaserSweepPersistenceTests.ExpiredRequestLinkIsNotAttachedToGeneratedDraft`
  timing out during SQL post-login, the existing [[DELIV-031]] runner flake.
  The failed test then passed 1/1 in isolation.
- 2026-08-28: the exact final-source canonical rerun passed Core 1,096/1,096,
  Architecture 100/100, and Integration 1,103/1,103 (exit 0). The later PASS
  is recorded alongside, and does not erase, the earlier failure.
- 2026-08-28: committed `14e0ad6f`, pushed the ticket branch, and opened
  PR #601 against `dev`.
