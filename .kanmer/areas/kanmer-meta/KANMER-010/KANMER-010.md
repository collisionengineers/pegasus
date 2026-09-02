---
id: KANMER-010
type: ticket
title: Reconcile Kanmer setup drift after KANMER-006
status: preparing
area: kanmer-meta
assignee: ''
profile: chore
stageEntered:
  preparing: '2026-09-02T00:55:56.588Z'
labels:
  - kanmer
  - setup
  - phase-0
links:
  - KANMER-006
  - KANMER-009
deployment: n/a
archived: false
created: '2026-09-01T14:40:45.085Z'
updated: '2026-09-02T01:10:25.856Z'
---

## What

Run the current Kanmer setup reconciliation (`kanmer-setup`, Kanmer 0.4.0) for the drift reported after KANMER-006, and deliver the refreshed repository artefacts to `dev`.

## Why

`get_status` on 2026-09-01 (repo root at `fb3f07ac`, server 0.3.12 `639df4cf`) reported four `behind` artefacts: the managed `AGENTS.md` block; `.agents/skills` (15 differ, 10 missing); `.grok/skills` (15 differ); the `mcp-registration` (`opencode.json` pointed at another workstation's board). On 2026-09-02 the Kanmer plugin became 0.4.0 (server sha `efe89029`; the local plugin copy carries a display patch, sha `e15615a1`), so the bundled skills changed again: against 0.4.0, `.agents/skills` and `.grok/skills` on `origin/dev` are stamped 0.3.3 and 0.1.0 and differ in 44 files against the bundle (re-measure in the worktree before quoting figures), and the managed block on `origin/dev` lacks the 0.4.0 board-branch, resumed-worktree and MCP-convention paragraphs.

The 0.4.0 `kanmer-setup` was run on 2026-09-02 in the primary checkout on the operator's command, so the target content is known and `get_status.repo.upToDate` is already `true` there; those working-tree changes are uncommitted on `main`. This ticket lands the same refresh on `dev` through a reviewed PR (precedent PR #638, KANMER-006, which truthfully closed the earlier slice and is not reopened). The direct dev commit `9b8f78a3` ("carry the board branch by env, not by cwd") is the baseline this ticket reconciles from.

## Approach

- Worktree `../pegasus-worktrees/kanmer-010-setup-drift` on `task/kanmer-010-setup-drift` from `origin/dev` (`9b8f78a3`); PR to `dev`.
- Managed block: `node C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/scripts/agents-block.mjs <worktree>` (idempotent; rewrites only between the markers; keeps the CLAUDE.md pointer). Every repository-owned line outside the markers stays byte-identical, including the Repository task workflow section that overrides the block's worktree text.
- Skill trees: replace each Kanmer-owned folder in `.agents/skills` and `.grok/skills` with the bundled 0.4.0 folder (`C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/skills/<skill>`, the twelve `kanmer-*` skills present at each destination), remove the retired paths `kanmer-import` and `kanmer-research/assets/impact-template.md` if present, drop stale non-bundled leftovers inside Kanmer-owned folders (`.agents/skills/kanmer-review/assets/pr-*.md`, which nothing references), and rewrite each `.kanmer-skills-version` stamp as `0.4.0` followed by `skills:` and the installed skill names. Repository-owned skills (`pegasus-release`, `razor-pages-ui-*`) are untouched.
- MCP registrations: `opencode.json` and `.codex/config.toml` are user-owned, machine-specific files that the Kanmer GUI Connect rewrites (it did so on this workstation on 2026-09-02). Do not commit a workstation path; record in the post-implementation report what `origin/dev` carries and that the fix is "reconnect this project in the Kanmer app" on the host that uses it. `.mcp.json` is untracked and ignored.
- No product code changes; never edit the board branch or `.worktrees/kanmer`.

## Verification

- [ ] `diff -rq <worktree>/.agents/skills C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/skills` and the same for `.grok/skills` report only the stamp file and repository-owned skills.
- [ ] `node .../scripts/agents-block.mjs <worktree>` a second time changes nothing; `git diff` shows the block, the skill trees and the stamps only.
- [ ] `KANMER_ROOT=C:/Users/PGUSER/Documents/github/pegasus/.worktrees/kanmer KANMER_REPO_ROOT=<worktree> bash tools/kanmer-call.sh get_status` (both env values are read by the server) reports `repo.upToDate: true` with no `behind` entry, or only the `mcp-registration` entry the operator defers; if the server cannot resolve the worktree that way, the `diff -rq` check plus the second idempotent `agents-block.mjs` run is the evidence.
- [ ] The refreshed files are LF in the bundle and the repository renormalizes to CRLF: commit what `git add` produces; never hand-convert line endings.
- [ ] `scripts/Test-DocumentationLinks.ps1` and `scripts/Test-MarkdownPlacement.ps1` pass in the runner; CI `repository-check` green.
- [ ] Merge SHA recorded and reachable from `origin/dev`.

## Outcome
