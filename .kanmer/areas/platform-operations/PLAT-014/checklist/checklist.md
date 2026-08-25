# Checklist — PLAT-014

- [x] Update only the Windows branch of `Get-PegasusDatabaseState` to recognize the inner, requested-instance LocalDB missing line (allowing trailing whitespace) while preserving all existing state, `Unknown`, Linux/Docker, and caller behavior, and without treating the wrapping `information failed` line as `Missing`.
- [x] Add `scripts/Test-PegasusPlatform.ps1` assertions for the exact two-line zero-exit missing fixture, wrong-instance missing, wrapper-only failure, unrelated output, Running, Stopped, contradictory state-plus-missing, and non-zero missing outcomes without mutating LocalDB.
- [x] Add an always-run `windows-latest` CI job that explicitly invokes `./scripts/Test-PegasusPlatform.ps1`, without adding conditional change-classification plumbing.
- [ ] Run the focused PowerShell test plus the runbook's canonical locked restore, Release build, and non-corpus test commands; record exact results.
- [x] Run the required reuse, simplification, efficiency, and altitude pass over the branch diff and append dated findings/dispositions to plan.md.
- [x] From a clean committed checkout, record the pre-existing LocalDB inventory and complete Offline Doctor → Initialize → Start → Status → Smoke → exact-run Reset. Reset any leftover Failed run from a prior Start attempt through the supported action, not by hand.
- [x] Confirm the exact run directory and `PegasusDevelopment_<run-id>` instance are absent after Reset and every pre-existing LocalDB instance remains present.
- [x] Write the post-implementation report with test/build/lifecycle evidence and exact run identity, keep progress current, and open the PR to `dev`.
- [ ] After independent review and merge, produce merged-source proof and hand [[PLAT-005]] back to its supported visual-capture lifecycle.

## Progress notes

(append with `set_ticket_doc(doc: "checklist", append: true)`)

- 2026-08-20: focused parser and CI-change-classification tests passed; locked restore and Release build passed. The canonical non-corpus command exceeded both a 10-minute and a 30-minute command window without a terminal test result, so it remains an unverified timeout rather than a pass.
- 2026-08-20: from committed `6cb9c59a`, Offline Doctor/Initialize completed; owned run `67a53c21ebc54bcc8c3cc98d6dab7c19` reached healthy Status and passed Smoke. Exact-run Reset removed its directory and `PegasusDevelopment_67a53c21ebc54bcc8c3cc98d6dab7c19`; pre-existing `MSSQLLocalDB` remained.

- 2026-08-20: committed `6cb9c59a`, pushed `task/plat-014-localdb-detection`, and opened PR #471 to `dev`: https://github.com/collisionengineers/pegasus/pull/471.

- 2026-08-20: independent review filed [[PR-023]] after the first Windows CI job printed success but returned exit 1. Commit `4c7b459f` resets only the test's success-path process exit state; direct and GitHub-style local invocations pass. PR #471 re-ran and remains awaiting green CI/re-review.

<!-- kanmer-groom:release-take:PLAT-014:2026-08-25 -->
### Board-hygiene claim release — 2026-08-25

Audit record written before releasing this completed ticket's stale take. Previous assignee: `codex-mcp-client`; branch: `task/plat-014-localdb-detection`; worktree: `../pegasus-worktrees/plat-014`; taken at: `2026-08-20T10:41:05.974Z`. The branch and worktree coordinates are preserved here; this groom does not delete either.
