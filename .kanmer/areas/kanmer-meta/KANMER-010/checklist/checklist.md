# Checklist — KANMER-010

*One independently tickable box per ordered plan step or acceptance check. Append progress
notes rather than rewriting.*

- [ ] Assert the worktree before the first edit: `rev-parse --show-toplevel` is `C:/Users/PGUSER/Documents/github/pegasus-worktrees/kanmer-010-setup-drift`, `branch --show-current` is `task/kanmer-010-setup-drift`, and `--git-common-dir` is `C:/Users/PGUSER/Documents/github/pegasus/.git` from both the worktree and the primary checkout.
- [ ] Step 1 — Byte-copy the twelve `kanmer-*` folders from `C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/skills` over `.agents/skills/<skill>`.
- [ ] Step 1 — Delete `.agents/skills/kanmer-review/assets/` and its four `pr-*.md` files (0.4.0 ships no such directory; nothing references them).
- [ ] Step 1 — Rewrite `.agents/skills/.kanmer-skills-version` as `0.4.0`, `skills:`, then the twelve installed skill names.
- [ ] Step 1 — Confirm `git status --porcelain -- .agents` lists exactly 13 `M` skill files, 4 `D` assets, and 1 `M` stamp — and nothing under `pegasus-release` or `razor-pages-ui-*`.
- [ ] Step 2 — Byte-copy the same twelve bundle folders over `.grok/skills/<skill>`.
- [ ] Step 2 — Delete `.grok/skills/kanmer-review/assets/` and its four `pr-*.md` files.
- [ ] Step 2 — Rewrite `.grok/skills/.kanmer-skills-version` from the bare `0.1.0` to the full `0.4.0` + `skills:` + twelve-name form, and confirm the name list matches the folders on disk.
- [ ] Step 2 — Confirm `git status --porcelain -- .grok` lists exactly 13 `M`, 4 `D`, 1 `M` stamp.
- [ ] Step 3 — Run `node C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/scripts/agents-block.mjs <worktree>` twice and confirm `git status --porcelain -- AGENTS.md CLAUDE.md` is empty on both runs (the 0.4.0 block is already on `origin/dev`; a change outside the markers, or a line-ending-only rewrite, is a deviation stop).
- [ ] Step 4 — Prove content parity: `diff -rq --strip-trailing-cr` is silent for all twelve skills in both trees, no `differ` and no `Only in` line in either direction.
- [ ] Step 4 — Prove membership: whole-tree `diff -rq` reports only the stamp plus the four repository-owned skills under `.agents`, and only the stamp under `.grok`.
- [ ] Step 4 — `KANMER_REPO_ROOT=<worktree> get_status` reports **no `skills` artefact row**. (`repo.upToDate` stays `false` because `mcp-registration` is behind by design and `board-config` is `compensated`; that is the pass condition, not a failure.)
- [ ] Step 4 — Confirm the diff contains nothing else: `git diff --name-only origin/dev...HEAD` lists only the 36 paths under `.agents/skills/kanmer-*`, `.grok/skills/kanmer-*` and the two stamps — no `AGENTS.md`, no `opencode.json`, no `.codex/`, no `.zcode/`, no `.kanmer/`.
- [ ] Step 4 — Commit one logical slice on `task/kanmer-010-setup-drift` and push with `git push -u origin task/kanmer-010-setup-drift`.
- [ ] Report-only: record in the post-implementation report what `origin/dev` carries for `opencode.json` (it registers `C:\Users\PC\Documents\GitHub\pegasus\.worktrees\kanmer`, another workstation's board) and `.codex/config.toml`, that `.mcp.json` is untracked and ignored, and that the fix is "reconnect this project in the Kanmer app" on the host that uses it — never a commit.
- [ ] Confirm the dated `## Simplification pass — 2026-09-02` disposition in the plan still holds (`n/a — configuration and skill-tree refresh; no product code`), or replace it with real findings, before opening the PR.
- [ ] Write the post-implementation report and open the PR against `dev` titled `Reconcile Kanmer setup drift after KANMER-006 (KANMER-010)` with the footer line `Kanmer: KANMER-010`.
- [ ] Move the ticket `implementing` → `review` and stop. Do not merge the PR, do not run `dotnet build` or any `Test-*.ps1` script, do not move a second gated boundary, do not take another ticket.
- [ ] [post-merge] Test-runner and CI only: `pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1`, `pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base origin/dev -Head HEAD`, and green CI `repository-check` on the PR head; then record the merge SHA reachable from `origin/dev`.

## Progress notes

Append with `set_ticket_doc(doc: "checklist", append: true)`.
