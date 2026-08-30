## Implementation — 2026-08-25

Merged `origin/main` into the `origin/dev`-based task branch without force or conflict. Net diff is exactly `.codex/config.toml` and `.mcp.json`: `C:\Users\Alex` becomes `C:\Users\PC`; command, `--root`, and `--repo-root` structure is unchanged. All referenced paths exist; JSON parses; live Kanmer `get_status` reports the same packaged executable, board worktree and repository root; `git diff --check` passes. PR #540 targets `dev`. No product/build/schema/infrastructure behavior changed.
