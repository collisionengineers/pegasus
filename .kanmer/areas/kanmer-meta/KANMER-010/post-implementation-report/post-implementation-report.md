# Post-implementation report — KANMER-010

*The report. Not the proof — this is the author's **claim**, written before merge; proof is
**evidence**, gathered after.*

## Summary

The Kanmer-owned skill trees in `.agents/skills` and `.grok/skills` are now byte-identical to
the Kanmer 0.4.0 plugin bundle, and both `.kanmer-skills-version` stamps read `0.4.0` followed
by `skills:` and the twelve installed skill names. Twelve `kanmer-*` folders per tree were
byte-copied from `C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/skills`; the four
retired `kanmer-review/assets/pr-*.md` files, which 0.4.0 no longer ships, were removed from
each tree. `AGENTS.md` was inspected and deliberately left unchanged. The outcome the ticket
exists for is observable from the Kanmer server itself: `KANMER_REPO_ROOT=<worktree>
get_status` now reports **no `skills` artefact row and no `agents-block` row**, where before it
reported two `skills` rows behind (*13 file(s) differ … and 0 are missing* for each tree).

Delivered as two commits on `task/kanmer-010-setup-drift` over base
`9b8f78a36151313bc6d48625edee7f13a2173127` (= `origin/dev`):
`80a4f4022651d07929efb11509cf29770e7c2c59` (`.agents`) and
`93ec918efa151ecfcdf7a87774cecb5538d78d9f` (`.grok`), 18 entries each — 13 modified skill
files, 4 deletions, 1 stamp. The whole change is 36 paths, 36 files changed, +3,429 / −384,
which is the plan's diff estimate to the line. Every changed byte is vendored text from the
bundle or generated stamp content; nothing was authored here.

## Changes

| File | Change | Why |
|---|---|---|
| `.agents/skills/kanmer-*/…` — 13 files | modified | Byte copies of the 0.4.0 bundle: `kanmer-auto/SKILL.md`, `kanmer-auto/assets/current-run-template.md`, `kanmer-auto/assets/run-state-template.md`, `kanmer-closeout/SKILL.md`, `kanmer-execute/SKILL.md`, `kanmer-groom/SKILL.md`, `kanmer-plan/SKILL.md`, `kanmer-plan/assets/plan-template.md`, `kanmer-report/SKILL.md`, `kanmer-review/SKILL.md`, `kanmer-tickets/SKILL.md`, `kanmer-tickets/references/tool-reference.md`, `kanmer-verify/SKILL.md`. |
| `.agents/skills/kanmer-review/assets/pr-changes-summary.md`, `pr-comment-disposition.md`, `pr-comments.md`, `pr-review.md` | removed | 0.4.0's `kanmer-review` ships no `assets/` directory, and a repository-wide search finds no SKILL.md, script or workflow that references these four files. |
| `.agents/skills/.kanmer-skills-version` | modified | Stamp said `0.3.3`; now `0.4.0` over the same `skills:` + twelve-name body. |
| `.grok/skills/kanmer-*/…` — the same 13 modified, the same 4 removed | modified / removed | The same twelve bundle folders, so both destinations carry identical content instead of drifting apart. |
| `.grok/skills/.kanmer-skills-version` | modified | Was a bare `0.1.0` with no `skills:` line at all — the wrong format as well as the wrong version. Now the full fourteen-line form. |
| `AGENTS.md` | **not changed** | In scope only so the idempotent `agents-block.mjs` run was authorized. See the finding below; the file is in neither commit. |

Both stamps were byte-copied from the output the sanctioned 0.4.0 setup run had already written
in the primary checkout, so the generator — not this ticket — remains the author of the stamp
format.

### Finding: the `AGENTS.md` managed block was already at 0.4.0 on `dev`

The dispatch premise that `AGENTS.md` still needed the 0.4.0 block is false, and the planner's
correction is confirmed by execution. `agents-block.mjs` ran twice against the worktree,
printed `AGENTS.md refreshed in …; CLAUDE.md pointer present` both times, and changed **no
content**: the diff for `AGENTS.md` is empty, and `hash-object -w --path=AGENTS.md AGENTS.md`
returns `3ee5f8aaba182ce9ee957cb7681e409ef0ea702a`, identical to the blob at `HEAD:AGENTS.md`.
The script writes UTF-8 with LF, so on this CRLF checkout it did rewrite the file's line
endings; that rewrite carried zero content change and was reverted, so `AGENTS.md` and
`CLAUDE.md` are unmodified and absent from both commits. The 0.4.0 block reached `dev` with
commit `9b8f78a3` (PR #638), and `get_status` accordingly reports no `agents-block` row. This
also keeps `AGENTS.md` rule 24 satisfied vacuously: this pull request changes no command and no
convention, so `AGENTS.md` must come out byte-identical, and it does.

### Report-only: the machine-specific MCP registrations are deliberately not committed

- `opencode.json` is tracked but user-owned and machine-specific. On `origin/dev` it registers
  Kanmer against `C:\Users\PC\Documents\GitHub\pegasus\.worktrees\kanmer` and
  `C:\Users\PC\Documents\GitHub\pegasus` — another workstation's board and checkout. That is
  the `mcp-registration` row `get_status` still reports as behind, and it is expected to stay:
  committing this workstation's path would simply break the other host.
- `.codex/config.toml` is the same class of file but is already host-agnostic — its Kanmer
  entry invokes `(Join-Path $env:LOCALAPPDATA 'Kanmer\bin\kanmer-mcp.cmd')` and pins only
  `KANMER_BOARD_BRANCH = "kanmer-board"` — so it needs no change at all.
- `.mcp.json` is untracked and ignored (`.gitignore:81`). There is nothing to commit.
- None of the three is modified in the ticket worktree, and none appears in either commit. The
  fix for a machine-specific registration is **to reconnect this project in the Kanmer app on
  the host that uses it** — never a commit. No token, connection string or environment value
  was printed while reading them.

## Governing docs

The ticket carries no `refs`, and `get_doc_gates` returns an empty reference set, so there is no
linked PRD, FRD or ADR to meet, modify or supersede. Nothing was linked, and no reference was
invented.

- **Meets** `AGENTS.md` → *Repository task workflow*: one `task/<slug>` branch, one worktree,
  one pull request into `dev`; step 3's per-step reuse statement (each step named the bundle
  path or script it reused); step 4's dated `Simplification pass` disposition, recorded in the
  plan under `## Simplification pass — 2026-09-02` as `n/a — configuration and skill-tree
  refresh; no product code`, with the four lenses applied and their findings written out.
- **Meets** `AGENTS.md` rule 24 vacuously and deliberately, as described in the finding above.
- **Modifies** nothing. No governing document is amended and no authorization for one was
  sought or granted. No new ADR is owed: the target content is fixed by an external vendor
  bundle, and adopting it is not an architectural choice this repository makes.
- **No production caller, registration, route, schema or packaged artifact** is created or
  implied. Skill files are read by agents and by the Kanmer server's file-by-file comparison;
  nothing in `Pegasus.slnx` references them, and this ticket deliberately creates no such
  reference.

## Risks / follow-ups

- `repo.upToDate` stays `false` after this merge, by design: `mcp-registration` is behind for
  the reason above and `board-config` is `compensated` with `fix: none — informational`. The
  success signal is the **absence of a `skills` row**, not `upToDate: true`. Chasing
  `upToDate: true` would mean committing another workstation's path.
- `.zcode/skills` holds only the repository-owned `pegasus-release` plus a bare `0.1.0` stamp
  and has **zero** `kanmer-*` folders installed, so the server reports nothing for it and the
  0.4.0 setup run left it untouched. Installing skills there would be new work, not
  reconciliation, and is deliberately out of scope. No follow-up ticket was created; raise one
  only if `.zcode` is meant to be a Kanmer destination.
- `core.autocrlf` is `true` here and skill Markdown is not pinned in `.gitattributes`, so the
  bundle's LF files land in a CRLF working copy. 26 of the 39 bundled files per tree therefore
  looked modified in the working copy while their content already matched; staging normalised
  them and they are in neither commit. No line ending was hand-converted and `.gitattributes`
  was deliberately not touched — a reviewer should expect exactly 13 content-modified skill
  files per tree, not 39.
- The refreshed `kanmer-plan/SKILL.md` and `kanmer-tickets/references/tool-reference.md` are the
  copies future agents will read once this merges; until then the plugin bundle is the
  authority. Nothing imports them, so there is no build ordering to respect.
- Vendored-content risk: this trades local edits for parity. If anyone had hand-edited a Kanmer
  skill file in-repo, that edit is now gone. Nothing in the diff suggests one existed — the 13
  differences are exactly the 0.3.3-to-0.4.0 upstream delta.

## Verification hand-off

The merge gate is CI `repository-check` on the pull request head; its `documentation` job runs
the repository's three documentation scripts. No job builds or tests anything for a change
confined to `.agents` and `.grok`.

On the merged result, `kanmer-verify` should run, from a checkout of `dev` at the merge commit:

1. `scripts/Test-DocumentationLinks.ps1` — expect PASS. Both skill trees are excluded from the
   link scan, so this is a rail the change cannot break, not a check of the change.
2. `scripts/Test-MarkdownPlacement.ps1` with `-Base origin/dev -Head HEAD` — expect PASS. Both
   parameters are mandatory; omitting them fails on the parameter, not on the change. The
   change adds and renames no `.md`, which matters because `.agents` is not on the
   allowed-new-Markdown list.
3. For each of the twelve skills and each of `.agents/skills` and `.grok/skills`:
   `diff -rq --strip-trailing-cr <tree>/<skill> C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/skills/<skill>`
   — expect silence, with no `differ` and no `Only in` line in either direction. The
   `--strip-trailing-cr` form is the meaningful one on a CRLF checkout; the plain form checks
   membership and will report a `differ` line per file from line endings alone.
4. `KANMER_REPO_ROOT=<merged checkout> get_status` — expect **no `skills` row and no
   `agents-block` row**; `board-config` compensated and `mcp-registration` behind are expected
   to remain, and `repo.upToDate: false` is the pass condition, not a failure.
5. Both `.kanmer-skills-version` files read `0.4.0`, then `skills:`, then the twelve installed
   names, and that name list matches the folders on disk.
6. `AGENTS.md` and `CLAUDE.md` are byte-identical to their pre-merge state, and
   `node <plugin>/0.4.0/scripts/agents-block.mjs <checkout>` is a no-op on content.

No screenshots are owed: there is no UI in this change. Builds, unit and integration tests,
snapshots, the UI catalogue, browser and Playwright rails and the local host script are all out
of scope for every role on this ticket — nothing in the diff is compiled, referenced by
`Pegasus.slnx` or packaged.

## Test evidence at the READY_FOR_TESTS stop

Run by the controller's test runner against head `93ec918e`, recorded under
`runs/20260901T215000Z-claude-controller/KANMER-010/tests/`: documentation links PASS; Markdown
placement INCONCLUSIVE on the first attempt (the script requires `-Base`/`-Head`) then PASS
with `-Base origin/dev -Head HEAD`; `agents-block.mjs` idempotence PASS; skills-match-bundle
PASS; Kanmer status against the worktree PASS. The implementer ran no test rail (role rule).
