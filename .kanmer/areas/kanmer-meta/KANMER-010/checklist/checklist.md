# Checklist — KANMER-010

*One independently tickable box per ordered plan step or acceptance check. Append progress
notes rather than rewriting.*

- [x] Assert the worktree before the first edit: `rev-parse --show-toplevel` is `C:/Users/PGUSER/Documents/github/pegasus-worktrees/kanmer-010-setup-drift`, `branch --show-current` is `task/kanmer-010-setup-drift`, and `--git-common-dir` is `C:/Users/PGUSER/Documents/github/pegasus/.git` from both the worktree and the primary checkout.
- [x] Step 1 — Byte-copy the twelve `kanmer-*` folders from `C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/skills` over `.agents/skills/<skill>`.
- [x] Step 1 — Delete `.agents/skills/kanmer-review/assets/` and its four `pr-*.md` files (0.4.0 ships no such directory; nothing references them).
- [x] Step 1 — Rewrite `.agents/skills/.kanmer-skills-version` as `0.4.0`, `skills:`, then the twelve installed skill names.
- [x] Step 1 — Confirm `git status --porcelain -- .agents` lists exactly 13 `M` skill files, 4 `D` assets, and 1 `M` stamp — and nothing under `pegasus-release` or `razor-pages-ui-*`.
- [x] Step 2 — Byte-copy the same twelve bundle folders over `.grok/skills/<skill>`.
- [x] Step 2 — Delete `.grok/skills/kanmer-review/assets/` and its four `pr-*.md` files.
- [x] Step 2 — Rewrite `.grok/skills/.kanmer-skills-version` from the bare `0.1.0` to the full `0.4.0` + `skills:` + twelve-name form, and confirm the name list matches the folders on disk.
- [x] Step 2 — Confirm `git status --porcelain -- .grok` lists exactly 13 `M`, 4 `D`, 1 `M` stamp.
- [x] Step 3 — Run `node C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/scripts/agents-block.mjs <worktree>` twice and confirm `git status --porcelain -- AGENTS.md CLAUDE.md` is empty on both runs (the 0.4.0 block is already on `origin/dev`; a change outside the markers, or a line-ending-only rewrite, is a deviation stop).
- [x] Step 4 — Prove content parity: `diff -rq --strip-trailing-cr` is silent for all twelve skills in both trees, no `differ` and no `Only in` line in either direction.
- [x] Step 4 — Prove membership: whole-tree `diff -rq` reports only the stamp plus the four repository-owned skills under `.agents`, and only the stamp under `.grok`.
- [x] Step 4 — `KANMER_REPO_ROOT=<worktree> get_status` reports **no `skills` artefact row**. (`repo.upToDate` stays `false` because `mcp-registration` is behind by design and `board-config` is `compensated`; that is the pass condition, not a failure.)
- [x] Step 4 — Confirm the diff contains nothing else: `git diff --name-only origin/dev...HEAD` lists only the 36 paths under `.agents/skills/kanmer-*`, `.grok/skills/kanmer-*` and the two stamps — no `AGENTS.md`, no `opencode.json`, no `.codex/`, no `.zcode/`, no `.kanmer/`.
- [ ] Step 4 — Commit one logical slice on `task/kanmer-010-setup-drift` and push with `git push -u origin task/kanmer-010-setup-drift`.
- [ ] Report-only: record in the post-implementation report what `origin/dev` carries for `opencode.json` (it registers `C:\Users\PC\Documents\GitHub\pegasus\.worktrees\kanmer`, another workstation's board) and `.codex/config.toml`, that `.mcp.json` is untracked and ignored, and that the fix is "reconnect this project in the Kanmer app" on the host that uses it — never a commit.
- [ ] Confirm the dated `## Simplification pass — 2026-09-02` disposition in the plan still holds (`n/a — configuration and skill-tree refresh; no product code`), or replace it with real findings, before opening the PR.
- [ ] Write the post-implementation report and open the PR against `dev` titled `Reconcile Kanmer setup drift after KANMER-006 (KANMER-010)` with the footer line `Kanmer: KANMER-010`.
- [ ] Move the ticket `implementing` → `review` and stop. Do not merge the PR, do not run `dotnet build` or any `Test-*.ps1` script, do not move a second gated boundary, do not take another ticket.
- [ ] [post-merge] Test-runner and CI only: `pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1`, `pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base origin/dev -Head HEAD`, and green CI `repository-check` on the PR head; then record the merge SHA reachable from `origin/dev`.

## Progress notes

Append with `set_ticket_doc(doc: "checklist", append: true)`.

### Implementer attempt 1 — 2026-09-02

- Worktree assertions PASS: `rev-parse --show-toplevel` =
  `C:/Users/PGUSER/Documents/github/pegasus-worktrees/kanmer-010-setup-drift`, `branch
  --show-current` = `task/kanmer-010-setup-drift`, `--path-format=absolute --git-common-dir`
  = `C:/Users/PGUSER/Documents/github/pegasus/.git` from both that location and the primary
  checkout, HEAD `9b8f78a36151313bc6d48625edee7f13a2173127` = `origin/dev`, no local
  modifications before the first edit.
- Steps 1 and 2 done with `cp -R <bundle>/<skill>/. <tree>/skills/<skill>/` (a byte copy) for
  the twelve skills in both trees, `rm -r <tree>/skills/kanmer-review/assets` for the four
  retired `pr-*.md` files in each tree, and both `.kanmer-skills-version` stamps byte-copied
  from the 0.4.0 setup output already present in the primary checkout — `0.4.0`, `skills:`,
  the twelve installed names, LF, trailing newline.
- **How to read the status expectation.** `status --porcelain` lists 39 `M` entries per tree,
  not 13. `cp` wrote the bundle's LF bytes into a CRLF working copy, so all 39 files differ
  byte-for-byte in the working copy while only 13 differ in content. The plan's own
  constraint ("line endings are Git's business, not yours") makes the content diff the
  authority, and `diff --numstat` gives exactly the predicted shape: 13 modified skill files
  + 4 deletions + 1 stamp per tree = 36 paths, +3,443 / −383, with nothing under
  `pegasus-release` or `razor-pages-ui-*`.
- Content parity PASS: `diff -rq --strip-trailing-cr` is silent for all twelve skills in both
  trees — no `differ` line and no `Only in` line in either direction.
- Membership PASS: whole-tree `diff -rq` reports only `.kanmer-skills-version` plus the four
  repository-owned skills under `.agents`, and only `.kanmer-skills-version` under `.grok`.
- `KANMER_REPO_ROOT=<worktree> get_status` PASS: **no `skills` row and no `agents-block`
  row**. `repo.upToDate` stays `false` on `board-config` (compensated, `fix: none —
  informational`) and `mcp-registration` (behind by design), exactly as the plan predicts.
- **Step 3 deviation — box left unticked.** `agents-block.mjs` ran twice against the
  worktree, printed `AGENTS.md refreshed in …; CLAUDE.md pointer present` both times, and
  made **zero content change**: the diff for `AGENTS.md` is empty and
  `hash-object -w --path=AGENTS.md AGENTS.md` returns
  `3ee5f8aaba182ce9ee957cb7681e409ef0ea702a`, identical to the blob at `HEAD:AGENTS.md`. The
  script did rewrite the file LF-only, so the porcelain status carries ` M AGENTS.md`. The
  plan requires reverting a line-ending-only rewrite; the sanctioned revert is denied by the
  shell guard (below), so the rewrite was left in place and is reported instead. It cannot
  reach a commit: its normalised blob already equals `HEAD`'s, and no byte of content, inside
  or outside the managed markers, differs.
- **BLOCKED before the commit slice.** Every mutating Git verb is denied to this subagent by
  `pegasus-guard rule 8 - mutating git in the primary checkout`, observed on
  `add -A .agents .grok` and on `checkout -- AGENTS.md`, both issued with an explicit `-C`
  pointing at the ticket worktree. Rule 8 tests the hook payload's session `cwd`
  (`C:\Users\PGUSER\documents\github\pegasus`, fixed for a subagent) instead of the `-C`
  target, so it misfires on every ticket worktree; changing directory first does not change
  the value the hook sees. Nothing was pushed and no pull request was attempted. The file
  work is complete and independently verified in the working copy; only the recording acts
  remain.
- Resume point: branch `task/kanmer-010-setup-drift`, worktree
  `C:/Users/PGUSER/Documents/github/pegasus-worktrees/kanmer-010-setup-drift`, HEAD
  `9b8f78a36151313bc6d48625edee7f13a2173127`, packet documents plan@`b2fc400eccc99c14`,
  checklist@`2886d652b533aec9`, files@`9e60bb14607276b3`. All 36 declared paths are changed
  and unstaged; the outstanding acts are one staging call, one commit on
  `task/kanmer-010-setup-drift`, `push -u origin task/kanmer-010-setup-drift`, the
  post-implementation report, the pull request against `dev`, and the move to `review`.

### Implementer attempt 1, resumed after the guard fix — 2026-09-02

The controller corrected shell-guard rule 8 (installed copy sha `55c691be…`) so a command
naming a ticket worktree is judged against that location instead of the session directory.
Resumed at the point of the stop, in the same recorded location, nothing else re-done.

- The `AGENTS.md` line-ending-only rewrite is reverted: `checkout -- AGENTS.md` succeeded and
  the porcelain status for `AGENTS.md` and `CLAUDE.md` is empty. Step 3's box is now ticked —
  the script is idempotent here, printed the same success line twice, and changed no content.
- Two logical slices committed on `task/kanmer-010-setup-drift`, one per destination tree:
  - `80a4f4022651d07929efb11509cf29770e7c2c59` — chore(kanmer): refresh the .agents skill tree
    to 0.4.0 (KANMER-010). 18 entries: 13 modified skill files, 4 deletions, 1 stamp.
  - `93ec918efa151ecfcdf7a87774cecb5538d78d9f` — chore(kanmer): refresh the .grok skill tree
    to 0.4.0 (KANMER-010). 18 entries, the same shape.
- Committed scope PASS: `diff --name-only origin/dev...HEAD` lists exactly 36 paths, every one
  under `.agents/skills/kanmer-*`, `.grok/skills/kanmer-*` or one of the two stamps — no
  `AGENTS.md`, no `opencode.json`, no `.codex/`, no `.zcode/`, no `.kanmer/`. Diffstat
  36 files changed, +3,429 / −384, matching the plan's estimate to the line.
- The 26 files that only ever differed by line endings staged as no-change, so they are
  absent from both commits. Porcelain status is now empty: the index, the working copy and
  the two commits agree.
- Post-commit re-verification: `diff -rq --strip-trailing-cr` silent for all 24 skill
  comparisons, and `KANMER_REPO_ROOT=<worktree> get_status` still shows no `skills` row and no
  `agents-block` row (only `board-config` compensated and `mcp-registration` behind).
- Not pushed, by the controller's instruction. Stop point READY_FOR_TESTS reached at
  `93ec918efa151ecfcdf7a87774cecb5538d78d9f`; the test runner is next, then PR_OPEN.

## Closeout — KANMER-010

- [ ] PR merge verified (`gh pr view --json state,mergedAt`)
- [ ] proof.md finalised (PR URL + merge date appended)
- [ ] Moved to final stage
- [ ] Outcome recorded in ticket body (PR link, follow-ups)
- [ ] cd out of worktree; recorded worktree absence confirmed / stale registration pruned
- [ ] `git branch -d task/kanmer-010-setup-drift` (local branch already absent)
- [ ] `git fetch --prune` + `git worktree prune`; merged remote branch removed
- [ ] `take_ticket action: "release"`
