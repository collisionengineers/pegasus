# Checklist — DELIV-009

- [x] Preflight, MERGE AUTH, atomic push, readback, main-push run green (after flake re-runs).
- [x] Artifacts built/validated; image pushed; digest verified; provision preview reviewed; `azd provision`; web readback.
- [x] Worker `config-zip`; smoke passed; poll state advancing.
- [x] Live connector-flow evidence captured (see AUTO-002 proof).
- [x] Docs PR #407 reviewed and merged.
- [x] Artifacts copied to `artifacts/releases/release-10-d8de29cb/` before worktree removal; azd env synced back.

## Closeout — DELIV-009 (2026-08-18)

- [x] PR #406 MERGED (by the push); PR #407 MERGED (`f79c24d9`)
- [x] proof.md finalised; Done; Outcome recorded; deployment = production
- [x] Worktree `../pegasus-worktrees/deliv-009-release-10` removed; local + remote branch deleted; prune
- [x] Released
