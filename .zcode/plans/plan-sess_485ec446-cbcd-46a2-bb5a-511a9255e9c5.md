## Fix: root the zcode Kanmer MCP at the board worktree

Historical session evidence. This plan is retired from active execution; do
not recreate its worktrees, rerun its merges or treat it as release authority.
The owning v1 stream plans and current Kanmer tickets govern current work.
Configuration evidence does not establish a fresh process restart.

**Problem.** `.zcode/config.json` launches the Kanmer MCP with `cwd` set to the
source checkout (`C:\Users\Alex\Documents\GitHub\pegasus`). The launcher sets
`KANMER_PROVIDER_CWD=!CD!` from that cwd, so ADR-0012 discovery binds to the
source checkout, where `.kanmer/` holds no real board — the server therefore
serves a *synthesized default board* (one PR Review area, zero tickets). The
real board (11 areas, board branch `kanmer-board`) lives in
`.worktrees/kanmer/.kanmer/`, exactly as AGENTS.md describes.

**Change (one file).** Edit `.zcode/config.json`, kanmer server entry:

- `cwd`: `C:\Users\Alex\Documents\GitHub\pegasus`
  → `C:\Users\Alex\Documents\GitHub\pegasus\.worktrees\kanmer`

No other fields change — the `KANMER_BOARD_BRANCH=kanmer-board` env is already
correct, and the launcher's `!CD!` will then resolve to the worktree.

**Verify.** After the edit, the MCP must be restarted to pick up the new cwd
(reconnect in ZCode or restart the session). On the fresh server, `get_status`
should show:

- `rootSource` rooted at `...\pegasus\.worktrees\kanmer`
- `boardSource`: `file` (not `default`)
- `boardWorktree.onBoardBranch`: true, non-zero `ticketCount`

I will re-run `get_status` + `list_items` after restart and report. If the
restart requires a session reload from the user, I'll hand back with exact
verification steps.

Note: this fixes only the zcode registration. The other stale artefacts flagged
earlier (`opencode.json` pointing at `C:\Users\PC\…`, `.codex/config.toml`,
skills/AGENTS block drift) remain — they belong to `kanmer-setup`, which you've
chosen not to run now.