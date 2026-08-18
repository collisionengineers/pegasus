# Proof — DELIV-002 (verified on merged `main`, 2026-08-18)

Policy exercised for real by the first exact-SHA fast-forward release (release 9, [[DELIV-008]]):

- Preflight: `git fetch origin`; `git merge-base --is-ancestor origin/main origin/dev` → true (main `2b0df78c`, dev `f1e116c6`); recorded SHA `f1e116c6eb939f901f32e5f89d58d1d8a4701851` == PR #400 head with all 10 `repository-check` checks SUCCESS.
- `MERGE AUTH GRANTED` given by the operator for the preflight SHA (2026-08-18).
- `git push --atomic --force-with-lease=refs/heads/dev:f1e116c6… origin f1e116c6…:refs/heads/main f1e116c6…:refs/heads/dev` → `2b0df78c..f1e116c6 main`; readback `origin/main == origin/dev == f1e116c6…`.
- Main-push run 32133221206 (`repository-check`, all 10 jobs success). Guard step "Require main history to be contained in dev": `Main history guard passed: 9 new first-parent commit(s); main head is contained in the release branch.` — the revised `scripts/Test-MainBranchHistory.ps1` accepted the fast-forward.
- Local on `f1e116c6`: `dotnet test tests/Pegasus.ArchitectureTests … --filter FullyQualifiedName~MainBranchHistoryGuardTests` → 8/8 passed; full architecture suite 96/96; `Test-DocumentationLinks.ps1` → 222 files resolve.
- PR #400 (dev→main) auto-closed as MERGED at commit `f1e116c6` — no PR merge was used for the promotion.

PR #396 merged 2026-08-18T09:21:50Z (`dcbdb129`).
