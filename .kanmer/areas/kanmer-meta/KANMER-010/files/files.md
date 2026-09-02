# Files — KANMER-010

*The files document. Not the research — this is the **surface area** of the change, not the findings behind it.*

Surveyed 2026-09-02 against `origin/dev` `9b8f78a36151313bc6d48625edee7f13a2173127` in the
worktree `C:/Users/PGUSER/Documents/github/pegasus-worktrees/kanmer-010-setup-drift`, and
against the Kanmer 0.4.0 plugin bundle
`C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0` (server sha `e15615a1`).

Authority for the drift: `KANMER_REPO_ROOT=<worktree> get_status`, read 2026-09-02, which
reports exactly two `skills` rows behind — `.agents/skills: 13 file(s) differ … and 0 are
missing` and `.grok/skills: 13 file(s) differ … and 0 are missing` — plus one
`mcp-registration` row behind and one `board-config` row `compensated`. It reports **no**
`agents-block` row: the managed `AGENTS.md` block on `origin/dev` is already the 0.4.0
block.

## Where the change lands

| Path | Why |
|---|---|
| `.agents/skills/kanmer-*/**` | 13 files carry 0.3.3 content and must become byte-identical to the bundle: `kanmer-auto/SKILL.md`, `kanmer-auto/assets/current-run-template.md`, `kanmer-auto/assets/run-state-template.md`, `kanmer-closeout/SKILL.md`, `kanmer-execute/SKILL.md`, `kanmer-groom/SKILL.md`, `kanmer-plan/SKILL.md`, `kanmer-plan/assets/plan-template.md`, `kanmer-report/SKILL.md`, `kanmer-review/SKILL.md`, `kanmer-tickets/SKILL.md`, `kanmer-tickets/references/tool-reference.md`, `kanmer-verify/SKILL.md`. The other 26 bundled files already match content-for-content (their working-tree CRLF is `core.autocrlf`, not drift). |
| `.agents/skills/kanmer-review/assets/pr-changes-summary.md`, `pr-comment-disposition.md`, `pr-comments.md`, `pr-review.md` | Delete. 0.4.0's `kanmer-review` ships no `assets/` directory at all (`diff -rq` reports `Only in .agents/skills/kanmer-review: assets`), and a repository-wide grep finds no SKILL.md, script or workflow that references them. 37 lines total. |
| `.agents/skills/.kanmer-skills-version` | Stamp says `0.3.3`; must say `0.4.0` with the same `skills:` + twelve-name body it already has. |
| `.grok/skills/kanmer-*/**` | The same 13 files, with the same target content — the two trees are identical on `origin/dev` apart from their stamps. |
| `.grok/skills/kanmer-review/assets/pr-*.md` | Delete, same four files, same reason. |
| `.grok/skills/.kanmer-skills-version` | Stamp is a bare `0.1.0` with **no** `skills:` line and no names — the wrong format as well as the wrong version. Must become the fourteen-line form the GUI writes (`0.4.0`, `skills:`, the twelve installed names). |
| `AGENTS.md` | **Inspect, expect no change.** The managed block between `<!-- kanmer:instructions:start -->` and `end` on `origin/dev` is already byte-identical to the 0.4.0 block (81 lines, verified by extracting the span from `origin/dev:AGENTS.md` and from the post-`kanmer-setup` reference file in the primary checkout — `md5sum` identical for the whole file). The path stays in scope only so the idempotent `agents-block.mjs` run is authorized and any surprise rewrite is a declared, reviewable edit rather than an out-of-scope one. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/scripts/agents-block.mjs` | The only sanctioned writer of the `AGENTS.md` block. Usage `node <script> <repo-root>`. It replaces **only** the span between the markers, throws on malformed markers, adds the `CLAUDE.md` pointer only when missing, and writes the file only when the text would change. It writes UTF-8 with LF, so on this Windows checkout it will rewrite the whole file's line endings even when the block content is identical — `git` normalizes that back to the stored blob, so `git status` must still show `AGENTS.md` clean. |
| `C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/skills/` | The source of truth for the twelve `kanmer-*` folders (39 files). The bundle adds no file that the repository lacks — `diff -rq` produces no `Only in <bundle>` line for any skill — so this change adds no new Markdown anywhere. |
| `.gitattributes` | Pins `eol=lf` for specific reference, design-sync and design-system paths only. Skill Markdown is **not** pinned, and `core.autocrlf` is `true` in this checkout, so working-tree CRLF against bundle LF is expected and is not content drift. Compare with `diff --strip-trailing-cr`, or compare against the `origin/dev` blob. |
| `scripts/Test-MarkdownPlacement.ps1` | `Test-AllowedMarkdownPath` allows new `.md` under `docs/(prd|frd|adr|design)`, `workspaces/document-extraction`, `.design-sync`, `.grok`, `.stitch`, `design/planning-and-old-designs` — **`.agents` is not on that list**. Only `A`/`C`/`R` changes are checked, and this change adds and renames nothing, so it passes; but any new `.md` under `.agents/` would fail the gate. |
| `scripts/Test-DocumentationLinks.ps1` | Excludes `^(node_modules\|corpus\|artifacts\|\.git\|\.claude\|\.agents\|\.codex\|\.grok\|\.kanmer)/`, so neither skill tree is link-scanned. It is a rail this change cannot break, not a check of the change. |
| `.github/workflows/ci.yml` | The merge gate is the workflow named `repository-check`. Its `documentation` job runs `Test-TestMarkdownPlacement.ps1`, `Test-DocumentationLinks.ps1` and `Test-UiCatalogue.ps1`. No job builds or tests anything for a change confined to `.agents`, `.grok` and `AGENTS.md`. |
| `AGENTS.md` "Repository task workflow" (step 4) and rule 24 | Step 4 requires the dated `Simplification pass` disposition in this plan and allows `n/a` for a docs-only task. Rule 24 limits `AGENTS.md` edits to a PR that changes commands or conventions — this PR changes neither, which is the second reason `AGENTS.md` must come out unchanged. |

## Ripple effects

- No product code, test, script, workflow, migration or packaged artifact is affected. No
  caller, route or registration exists for a skill file: the trees are read by agents and
  by the Kanmer server's file-by-file comparison, never by `Pegasus.slnx`.
- The refreshed `.agents/skills/kanmer-plan/SKILL.md` and `kanmer-tickets/references/tool-reference.md`
  are the copies future agents read once KANMER-010 merges; until then the plugin bundle is
  the authority. Nothing in the repository imports them, so there is no build ordering.
- `get_status` will still report `repo.upToDate: false` after this change, because
  `mcp-registration` stays behind and `board-config` stays `compensated`. The observable
  success signal is the **absence of any `skills` row**, not `upToDate: true`.

## Out of scope

| Path | Why it is deliberately untouched |
|---|---|
| `opencode.json` | Tracked but user-owned and machine-specific. `origin/dev` registers Kanmer against `C:\Users\PC\Documents\GitHub\pegasus\.worktrees\kanmer` — another workstation. The Kanmer GUI's Connect rewrites it locally; committing this workstation's path would break the other host. Report only. |
| `.codex/config.toml` | Same class of file, same reason. Report only. |
| `.mcp.json` | Untracked and ignored. Nothing to commit. |
| `.agents/skills/pegasus-release/**`, `.agents/skills/razor-pages-ui-design/**`, `.agents/skills/razor-pages-ui-implementation/**`, `.agents/skills/razor-pages-ui-review/**` | Repository-owned skills that live beside the Kanmer-owned folders. The bundle does not contain them and the server does not compare them. |
| `.zcode/skills/**` | A third destination exists — `.zcode/skills` holds only `pegasus-release` plus a bare `0.1.0` stamp, with **zero** `kanmer-*` folders installed. Because the server compares only bundled folders that already exist at a destination, it reports nothing for `.zcode`, and the 0.4.0 `kanmer-setup` run in the primary checkout left it untouched (`git status -- .zcode` is clean there). Installing skills there or bumping its stamp would be new work, not reconciliation. |
| `.kanmer/**`, `.worktrees/kanmer`, branch `kanmer-board` | The board. Never edited by a ticket. |
| `board.yml` profiles (`board-config`, state `compensated`) | `get_status` marks it informational: core injects `questions-resolved` at read time, so the gate is in force and the file merely no longer lists every effective requirement. No fix exists to apply. |
| `docs/**`, `src/**`, `tests/**`, `scripts/**`, `infra/**`, `.github/**` | No product, documentation or pipeline change is implied by a skill-tree refresh. |
