# Checklist — DELIV-008

- [x] Promotion preflight (ancestry, SHA == PR #400 head, CI 10/10) and MERGE AUTH recorded.
- [x] Atomic exact-SHA push; both heads == `f1e116c6`; main-push run green incl. guard.
- [x] Artifacts built and validated (Local/Artifact/PreUpload/PreMigration/PreProvision); manifest SHA recorded.
- [x] Image pushed to ACR; digest verified.
- [x] Migrations applied; database bootstrap verified; history head read back.
- [x] `azd provision` (after correcting the six secret-URI inputs); web revision `--f1e116c6eb93` healthy; MCP gate on.
- [x] Worker deployed (`config-zip`); smoke passed; nine functions enabled; KV references resolved; inbox poll advancing.
- [x] AUTO-001 live evidence captured; kill switch exercised and restored.
- [x] Docs refresh PR #404 reviewed and merged to `dev`.

## Closeout — DELIV-008

- [x] PR merge verified (#400 MERGED by the push; #404 MERGED `de94c1d0`)
- [x] proof.md finalised
- [x] Moved to final stage
- [x] Outcome recorded in ticket body
- [x] Worktree removed (`../pegasus-worktrees/deliv-008-release-9`); note: its ignored `artifacts/releases/0.1.0-alpha.1/` went with it — manifest fields, hashes and log lines are preserved in scratch/proof/operations; image is immutable in ACR by digest.
- [x] `git branch -d task/deliv-008-release-9`; remote deleted
- [x] `git fetch --prune` + `git worktree prune`
- [x] `take_ticket action: "release"`
