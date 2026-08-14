# Proof — SIMPLI-001 (ai-centre extracted + removed from Pegasus)

## Extraction (the ticket's core)
- `git archive HEAD:workspaces/ai-centre` (tracked tree only — excludes `corpus/`,
  caches, build output, credentials, nested git) → **266 files** at
  **`C:/Users/PC/Documents/GitHub/ai-centre`**, initialised as a standalone git
  repo (`main`, commit `a02a1de`). Top level: `README.md docs ml-ops services skills`.
- Sanity: no nested `.git`, no `*.env`/secret files, no corpus leaked.
- Full commit history is additionally retained in Pegasus git objects (the tree
  existed on `dev` through fb55c164), so nothing is lost.

## Removal from Pegasus (PR #375 → dev `7daeef53`)
- `workspaces/ai-centre/` (272 files, −22,892 lines) removed; `origin/dev:workspaces/ai-centre/README.md` no longer exists.
- Root references cleaned: `AGENTS.md` protected-skills rail + workspace-invariant clause; `docs/current-architecture.md` Workspaces entry; `docs/runbook.md` (pgvector note + build/test commands); `workspaces/README.md` register row; `.github/workflows/workspaces.yml` two ai-centre validation steps.
- **No ADR filed; ADR-0009 untouched** (remains valid history for the workspaces that stay). The ai-centre workspace's own internal ADRs were deleted with the tree.
- `pwsh ./scripts/Test-DocumentationLinks.ps1` → green (99 files). No `ai-centre`/`collision-brain` reference remains outside ADR-0009 history and git history.
- Rebased on post-#374 `dev`; merge conflicts (3 modify/delete on ai-centre files #374 had link-retargeted) resolved by deletion.

## Not in scope here
- Publishing the standalone repo to a remote (external write; needs an approved org/name/target). The local standalone repo is ready to push when that target is provided.
