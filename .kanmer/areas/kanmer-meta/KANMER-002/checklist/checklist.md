# Checklist — KANMER-002

- [x] Preserve renderer integration plan decisions and remaining work in [[SIMPLI-015]].
- [x] Create the active root plan and update repository process/navigation away from `docs/temp-plans/`.
- [x] Move the design authority and entire design tree into `docs/design/`.
- [x] Retarget every live design consumer, build input, link and comment.
- [x] Retire all covered temporary plans and the temporary-plan validator carve-out.
- [x] Verify and remove only the byte-identical unreferenced reference duplicate.
- [x] Recheck and clean only exact obsolete ignored artifact subtrees.
- [x] Run stale-path, documentation, design-system and renderer verification.
- [x] Write the post-implementation report with exact changed files and evidence.

## Progress notes

- Local ignored artifact audit on 2026-08-17 found the previously identified obsolete planning/audit candidates already absent. The remaining `artifacts/tools` is the sole dotnet-ef installation (not a duplicate), so it and all active artifact roots were preserved; no local artifact deletion was justified.
- Documentation link validation passed for 214 tracked Markdown files.
- The moved design-system package built successfully.
- CollisionRenderer.Core built with 0 warnings/errors and its focused suite passed 173/173.

- Review remediation: [[PR-001]] identified the package-relative `guidelinesGlob` error. Corrected it to `../README.md`; explicit resolution proves the canonical file exists, and focused checks pass.

## Closeout — KANMER-002 (2026-08-18)

- [x] PR #379 MERGED 2026-08-17T05:21:47Z
- [x] proof.md written on merged `main`; moved to Done; Outcome recorded
- [x] Worktree `../pegasus-worktrees/kanmer-002-repo-plan-doc-cleanup` already absent (pruned); stray local branch `KANMER-002-repo-doc-cleanup` and remote `task/kanmer-002-repo-plan-doc-cleanup` deleted
- [x] Released
