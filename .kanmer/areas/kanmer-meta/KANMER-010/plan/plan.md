# Plan — KANMER-010: Reconcile Kanmer setup drift after KANMER-006

**Diff estimate (plan sizing): 36 files changed, ~+3,429 / −384 lines — 26 modified skill
files (+3,414 / −308), 2 stamp files (+15 / −2), 8 deleted stale asset files (−74), and
`AGENTS.md` inspected with 0 lines changed.** Every one of those lines is vendored text
copied byte-for-byte from the Kanmer 0.4.0 plugin bundle; nothing is authored here. The
line count is large, the decision surface is nil, and no product code, test, script or
packaged artifact is in scope. One unit of work, four steps.

## Objective

Make the Kanmer-owned skill trees in `.agents/skills` and `.grok/skills` on `dev`
byte-identical to the Kanmer 0.4.0 plugin bundle, stamp both trees `0.4.0`, and confirm the
`AGENTS.md` managed block is already current — so `get_status` for this repository reports
no `skills` artefact behind.

## Starting state

Evidence: `files`@`9e60bb14607276b3`. No `research` document exists and the live gate report
does not require one; the surface was surveyed directly against Git and the bundle, and the
Kanmer server's own comparison is cited as authority rather than reconstructed.

Verified 2026-09-02 in the worktree
`C:/Users/PGUSER/Documents/github/pegasus-worktrees/kanmer-010-setup-drift`, branch
`task/kanmer-010-setup-drift`, HEAD `9b8f78a36151313bc6d48625edee7f13a2173127` =
`origin/dev`, Git common dir `C:/Users/PGUSER/Documents/github/pegasus/.git`:

- `KANMER_REPO_ROOT=<worktree> get_status` (server 0.4.0, sha `e15615a1`) reports
  `repo.upToDate: false` with four rows: `skills` behind for `.agents/skills`
  (*13 file(s) differ … and 0 are missing*), `skills` behind for `.grok/skills`
  (*13 file(s) differ … and 0 are missing*), `board-config` **compensated**
  (informational, no fix exists), and `mcp-registration` behind
  (`opencode.json` registers another workstation's board).
- **Correction to the dispatch premise.** There is **no** `agents-block` row, and the
  managed block on `origin/dev` is already the 0.4.0 block: extracting the
  `kanmer:instructions:start … end` span from `origin/dev:AGENTS.md` and from the
  post-`kanmer-setup` reference file in the primary checkout gives 81 identical lines, and
  the two whole files share one `md5sum` (`de212acf7d677db0624e17731df82f0e`). The
  board-branch (`KANMER_BOARD_BRANCH`), resumed-execution-packet and MCP-convention
  paragraphs are all present on `dev` already. `AGENTS.md` therefore needs no edit, and the
  0.4.0 block arrived with commit `9b8f78a3`/PR #638 rather than being left behind by them.
- **Correction to the dispatch premise.** The stale `pr-*.md` leftovers exist in **both**
  trees, not only `.agents`: `.agents/skills/kanmer-review/assets/` and
  `.grok/skills/kanmer-review/assets/` each hold the same four files (37 lines each tree).
  0.4.0's `kanmer-review` ships no `assets/` directory, and nothing in the repository or
  the bundle references those files.
- The retired paths `kanmer-import` and `kanmer-research/assets/impact-template.md` are
  **already absent** from both trees on `origin/dev` (`git ls-tree -r origin/dev` finds
  neither). Nothing to remove.
- Of the 39 bundled files per tree, only 13 differ in content. The remaining 26 report as
  differing to a naive `diff` purely because `core.autocrlf` is `true` here and the
  skill Markdown is not pinned in `.gitattributes`: the working tree is CRLF, the stored
  blob and the bundle are LF. That is the whole gap between "44 files differ" and the
  server's "13 differ".
- Both stamps are wrong. `.agents/skills/.kanmer-skills-version` reads `0.3.3` over the
  correct `skills:` + twelve-name body. `.grok/skills/.kanmer-skills-version` is a bare
  `0.1.0` with no `skills:` line at all.
- A third destination, `.zcode/skills`, holds only the repository-owned `pegasus-release`
  and a bare `0.1.0` stamp with zero `kanmer-*` folders installed. The server reports
  nothing for it and the reference 0.4.0 setup run left it untouched. Out of scope.

## Governing docs

The ticket carries no `refs`, and `get_doc_gates` returns an empty `refs`/`references` set,
so there is no linked PRD, FRD or ADR to meet, modify or supersede.

- **Meets** `AGENTS.md` → *Repository task workflow*: one `task/<slug>` branch, one
  worktree, one PR to `dev`; step 3's requirement that the plan states per step what
  existing code it reuses (each step below names the bundle path or script it reuses);
  step 4's dated `Simplification pass` disposition, recorded below.
- **Meets** `AGENTS.md` rule 24 (*a PR that changes commands or conventions updates
  `AGENTS.md` in the same PR*) **vacuously and deliberately**: this PR changes no command
  and no convention, so `AGENTS.md` must come out byte-identical. The only `AGENTS.md` edit
  this ticket could ever authorize is the managed block written by `agents-block.mjs`, and
  that block is already current.
- **Modifies** nothing. No governing document is amended, and no authorization for one was
  sought or granted.
- **New ADR:** none, and none is owed. There is no design decision here — the target
  content is fixed by an external vendor bundle, and choosing it is not an architectural
  choice the repository makes. (`get_status`'s fix hint cites *FRD-013*; that is Kanmer's
  own functional document, not a document under this repository's `docs/`.)

## Required changes

1. Each of the twelve `kanmer-*` folders under `.agents/skills` and under `.grok/skills`
   becomes byte-identical to the same-named folder in
   `C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/skills`, ignoring only the
   CRLF/LF translation Git performs on commit.
2. `.agents/skills/kanmer-review/assets/` and `.grok/skills/kanmer-review/assets/` are
   removed, with their four files each, because 0.4.0 ships no such directory.
3. `.agents/skills/.kanmer-skills-version` and `.grok/skills/.kanmer-skills-version` each
   read exactly: `0.4.0`, then `skills:`, then the twelve installed skill names one per
   line, in the order `kanmer-auto`, `kanmer-closeout`, `kanmer-docs`, `kanmer-execute`,
   `kanmer-groom`, `kanmer-plan`, `kanmer-report`, `kanmer-research`, `kanmer-review`,
   `kanmer-setup`, `kanmer-tickets`, `kanmer-verify`.
4. `AGENTS.md` is confirmed current by running the sanctioned writer and observing that Git
   reports no change. No hand edit, inside the markers or outside them.
5. The machine-specific MCP registrations are **reported, not committed**.

No behaviour, contract, schema, route or dependency changes. No file is added.

## Expected files

| Action | Repo-root-relative path | Responsibility |
|---|---|---|
| Modify/Delete | `.agents/skills/kanmer-*/**` | The twelve Kanmer-owned skill folders. Vendored content copied from the 0.4.0 bundle; not authored, not edited by hand. Includes deleting `kanmer-review/assets/pr-*.md`. |
| Modify | `.agents/skills/.kanmer-skills-version` | Generated stamp: version line, `skills:`, installed names. |
| Modify/Delete | `.grok/skills/kanmer-*/**` | Same twelve folders, same bundle source, same deletions. |
| Modify | `.grok/skills/.kanmer-skills-version` | Same stamp, rewritten from the wrong bare-version format. |
| Inspect | `AGENTS.md` | Expected to come out unchanged. In scope only so the idempotent `agents-block.mjs` run is authorized and any surprise rewrite is visible and reviewable rather than out of bounds. |

## Do not modify

- `opencode.json` — tracked, user-owned, machine-specific. `origin/dev` points at another
  workstation's board. Report only; never commit a local path here.
- `.codex/**` — `.codex/config.toml` is the same class of file. Report only.
- `.mcp.json` — untracked and ignored.
- `.agents/skills/pegasus-release/**`, `.agents/skills/razor-pages-ui-design/**`,
  `.agents/skills/razor-pages-ui-implementation/**`,
  `.agents/skills/razor-pages-ui-review/**` — repository-owned skills. Not in the bundle,
  not compared by the server.
- `.zcode/**` — third destination with no Kanmer skills installed. Installing them would be
  new work, not reconciliation.
- `.kanmer/**` — the board. Never edited by a ticket. Never touch
  `C:/Users/PGUSER/Documents/github/pegasus/.worktrees/kanmer` or the branch
  `kanmer-board`.
- `.github/**`, `scripts/**`, `docs/**`, `src/**`, `tests/**`, `infra/**`, `.gitattributes`,
  `Pegasus.slnx` — no product, pipeline or documentation change is implied.

## Constraints

- **Path and process.** Work only in
  `C:/Users/PGUSER/Documents/github/pegasus-worktrees/kanmer-010-setup-drift` on
  `task/kanmer-010-setup-drift`. Assert before the first edit: `rev-parse --show-toplevel`
  equals that path; `rev-parse --path-format=absolute --git-common-dir` equals
  `C:/Users/PGUSER/Documents/github/pegasus/.git` from both the worktree and the primary
  checkout; `branch --show-current` equals the recorded branch. Refresh only with
  `git merge --no-edit origin/dev`. Never push anything but
  `git push -u origin task/kanmer-010-setup-drift`.
- **Byte copy, never a text round-trip.** Copy bundle files with `Copy-Item` or `cp` —
  a byte copy. Never `Get-Content | Set-Content` or `Out-File`: those re-encode, and on
  PowerShell 5 `Out-File` writes UTF-16, which would corrupt every file.
- **Line endings are Git's business, not yours.** `core.autocrlf` is `true` and skill
  Markdown is unpinned in `.gitattributes`. LF files landing in a CRLF working tree is
  expected and normalizes on commit. Do **not** add `.gitattributes` entries, do not
  convert line endings by hand, and do not treat a CRLF/LF-only `diff` as drift — verify
  content with `diff --strip-trailing-cr` or against the `origin/dev` blob.
- **Add no new Markdown under `.agents/`.** `scripts/Test-MarkdownPlacement.ps1` allows new
  `.md` under `.grok` but not under `.agents`. The 0.4.0 bundle introduces no file the
  repository lacks, so this constraint is satisfied by copying only — but adding anything
  would fail the gate.
- **`AGENTS.md` only via `agents-block.mjs`.** Never hand-edit inside the markers; never
  touch a byte outside them. The *Repository task workflow* section, which overrides the
  block's worktree text, must survive verbatim.
- **Git Bash path mangling.** Any Bash command containing a `<rev>:<path>` argument (for
  example `git show origin/dev:AGENTS.md`) needs `MSYS_NO_PATHCONV=1`, or MSYS rewrites it
  to `origin\dev;AGENTS.md` and the command fails with "ambiguous argument". Use PowerShell
  or set that variable.
- **No build, no tests.** A change confined to `.agents`, `.grok` and `AGENTS.md` compiles
  nothing. `dotnet build ./Pegasus.slnx` is **not** required and must not be reported as
  evidence for this ticket; `dotnet test`, snapshot, catalogue, browser, Playwright and
  `Invoke-LocalDevelopment` scripts are the test runner's, not the implementer's.
- **Secrets and data.** Never print or commit a token, connection string or environment
  value while reading the MCP registrations; `corpus/` stays read-only.

## Ordered steps

### Step 1 — Refresh the `.agents/skills` Kanmer folders from the 0.4.0 bundle

- Preconditions: worktree assertions above pass; working tree clean at
  `9b8f78a36151313bc6d48625edee7f13a2173127`.
- Reuses: the Kanmer 0.4.0 plugin bundle
  `C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/skills/<skill>` as the sole
  source of content — no file is authored, and no earlier repository copy is edited.
- Files: `.agents/skills/kanmer-*/**`, `.agents/skills/.kanmer-skills-version`
- Change: for each of `kanmer-auto`, `kanmer-closeout`, `kanmer-docs`, `kanmer-execute`,
  `kanmer-groom`, `kanmer-plan`, `kanmer-report`, `kanmer-research`, `kanmer-review`,
  `kanmer-setup`, `kanmer-tickets`, `kanmer-verify`, byte-copy the bundle folder over
  `.agents/skills/<skill>`; then delete `.agents/skills/kanmer-review/assets/` and its four
  `pr-*.md` files, which 0.4.0 does not ship; then write
  `.agents/skills/.kanmer-skills-version` as `0.4.0`, `skills:`, and the twelve names in the
  order listed above.
- Preserved behaviour: `.agents/skills/pegasus-release`, `razor-pages-ui-design`,
  `razor-pages-ui-implementation` and `razor-pages-ui-review` are untouched — verify with
  `git status` that no path under them appears.
- Forbidden: adding a file the bundle does not contain; editing bundle content while
  copying; touching `.gitattributes`; converting line endings by hand.
- Negative cases: a `git status` entry outside `.agents/skills/kanmer-*` or the stamp is a
  failure. A whole-file diff on all 39 files instead of 13 means the copy re-encoded the
  files — discard and redo with a byte copy.
- Tests: none; this is vendored content with no caller. Parity is proved mechanically in
  Step 4.
- Commands: `Copy-Item -Recurse -Force` per folder (or `cp -R`); `Remove-Item -Recurse`
  for `kanmer-review/assets`; then
  `git -C <worktree> status --porcelain -- .agents`.
- Expected output: exactly 13 `M` entries under `.agents/skills/kanmer-*`, 4 `D` entries
  under `.agents/skills/kanmer-review/assets/`, and 1 `M` for the stamp. 18 entries, nothing
  else.
- Done when: that status output matches, and `git diff --stat -- .agents` shows about
  +1,707 / −154 across the 13 modified files plus the stamp and the deletions.
- Deviation stop: any status entry outside the declared paths; any bundle file missing from
  the plugin cache; a `diff` that still reports content differences after the copy.

### Step 2 — Refresh the `.grok/skills` Kanmer folders from the same bundle

- Preconditions: Step 1 done.
- Reuses: the same twelve bundle folders — the identical source, so the two trees converge
  on identical content rather than being maintained separately.
- Files: `.grok/skills/kanmer-*/**`, `.grok/skills/.kanmer-skills-version`
- Change: the same byte copy for the same twelve folders; delete
  `.grok/skills/kanmer-review/assets/` and its four `pr-*.md` files; rewrite
  `.grok/skills/.kanmer-skills-version` from the bare `0.1.0` to the full fourteen-line
  form (`0.4.0`, `skills:`, twelve names) so both trees carry the same stamp format the GUI
  writes.
- Preserved behaviour: `.grok/skills` contains no repository-owned skill, so the tree ends
  up holding exactly the twelve Kanmer folders plus the stamp.
- Forbidden: leaving the stamp in the old bare-version format; adding a `skills:` list that
  does not match the folders actually present.
- Negative cases: a stamp whose name list disagrees with the folders on disk is a failure.
- Tests: none, same reason.
- Commands: as Step 1, against `.grok`; then
  `git -C <worktree> status --porcelain -- .grok`.
- Expected output: 13 `M` under `.grok/skills/kanmer-*`, 4 `D` under
  `.grok/skills/kanmer-review/assets/`, 1 `M` for the stamp.
- Done when: that matches, and the two trees are identical to each other apart from
  nothing — `diff -rq <worktree>/.agents/skills/kanmer-plan <worktree>/.grok/skills/kanmer-plan`
  is silent for every skill.
- Deviation stop: as Step 1; also stop if `.grok` turns out to contain a repository-owned
  skill this plan did not anticipate.

### Step 3 — Reconcile the `AGENTS.md` managed block with the sanctioned writer

- Preconditions: Steps 1–2 done.
- Reuses: `C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/scripts/agents-block.mjs`
  — the one sanctioned writer of the block, shared with the GUI's Connect flow. No prose
  copy of the block is transcribed by hand.
- Files: `AGENTS.md`
- Change: none expected. Run
  `node C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/scripts/agents-block.mjs C:/Users/PGUSER/Documents/github/pegasus-worktrees/kanmer-010-setup-drift`
  and confirm Git reports `AGENTS.md` unchanged. The block on `origin/dev` is already the
  0.4.0 block (verified: identical 81-line span, identical whole-file `md5sum`), and
  `get_status` reports no `agents-block` row.
- Preserved behaviour: every byte outside the markers, including the *Repository task
  workflow* section and the 24 agent-conduct rules; the `CLAUDE.md` pointer.
- Forbidden: hand-editing `AGENTS.md`; committing a line-ending-only rewrite of it; adding
  or removing a marker.
- Negative cases: if the script throws on malformed markers, stop — that is a human's
  problem, not something to guess at. If `git status` shows `AGENTS.md` modified, inspect
  `git diff -- AGENTS.md`: a **content** change inside the markers is a genuine 0.4.0
  block update and may be committed as the one authorized `AGENTS.md` edit; a change
  **outside** the markers, or a pure line-ending rewrite with no content change, must be
  reverted and reported.
- Tests: run the script a second time and confirm it is a no-op — that is the idempotence
  check the ticket asks for.
- Commands:
  `node <plugin>/scripts/agents-block.mjs <worktree>` (twice), then
  `git -C <worktree> status --porcelain -- AGENTS.md CLAUDE.md`.
- Expected output: the script prints `AGENTS.md refreshed in …; CLAUDE.md pointer present`
  on both runs, and the `git status` output is **empty**.
- Done when: `git status` shows no `AGENTS.md` or `CLAUDE.md` entry.
- Deviation stop: any `AGENTS.md` change outside the markers; a thrown marker error; a
  `CLAUDE.md` change.

### Step 4 — Prove parity, commit, report the registrations, open the PR

- Preconditions: Steps 1–3 done; the working tree carries exactly the 36 declared entries.
- Reuses: the Kanmer server's own file-by-file comparison (`get_status`) as the acceptance
  oracle rather than a bespoke check, and the repository's existing `repository-check`
  workflow as the merge gate.
- Files: `.agents/skills/kanmer-*/**`, `.agents/skills/.kanmer-skills-version`,
  `.grok/skills/kanmer-*/**`, `.grok/skills/.kanmer-skills-version`, `AGENTS.md`
- Change: no further edits. Prove parity, then commit in one logical slice and open the PR.
- Preserved behaviour: nothing outside the declared paths enters the commit — check with
  `git diff --cached --name-only` before committing.
- Forbidden: staging `opencode.json`, `.codex/config.toml`, `.mcp.json`, `.zcode/**` or
  anything under `.kanmer/`; running `dotnet build`, `dotnet test` or any `Test-*.ps1`
  script; merging the PR; moving the ticket past `review`.
- Negative cases: `git status --porcelain` must list nothing beyond the 36 declared
  entries. If `opencode.json` or `.codex/config.toml` appears modified because the GUI
  rewrote it on this workstation, leave it unstaged and say so in the report.
- Tests: none owned by this step. `scripts/Test-DocumentationLinks.ps1` and
  `scripts/Test-MarkdownPlacement.ps1` belong to the test-runner role and to CI
  `repository-check`; both are rails this change cannot break (`.agents` and `.grok` are
  excluded from the link scan, and no `.md` is added or renamed).
- Commands: the parity and status commands under **Commands** below, then
  `git -C <worktree> add`/`commit`, `git push -u origin task/kanmer-010-setup-drift`,
  `gh pr create --base dev`.
- Expected output: `get_status` shows **no** `skills` row; `git log` shows one commit; the
  PR is open against `dev`.
- Done when: the PR exists with the exact title and footer in the stop condition, the
  post-implementation report records what `origin/dev` carries for `opencode.json` and
  `.codex/config.toml` and that the fix is "reconnect this project in the Kanmer app" on the
  host that uses it, and the ticket has moved `implementing` → `review`.
- Deviation stop: a `skills` row still behind after the copy; an unexpected path in the
  commit; a CI failure; any temptation to fix the MCP registration in Git.

## Acceptance checks

- **No production caller, registration, route or composition entry exists or is implied.**
  Skill files and the `AGENTS.md` block are read by agents and by the Kanmer server's
  comparison; nothing in `Pegasus.slnx` references them, and this ticket deliberately
  creates no such reference (`AGENTS.md`: *a workspace, skill, prompt, or model never
  becomes an application policy owner*).
- **No runtime dependency and no packaged artifact is affected**, so there is nothing to
  prove ships.
- **No schema change**, so no migration, grants census, runtime-role permission or rollback
  handling is owed.
- **Parity is the claim, and it is proved mechanically, not asserted.** For each of the
  twelve skills and each of the two trees, `diff -rq --strip-trailing-cr <tree>/<skill>
  <bundle>/<skill>` is silent — no `differ` line, no `Only in` line in either direction.
- **The server agrees.** `KANMER_REPO_ROOT=<worktree> get_status` reports **no `skills`
  artefact row**. `repo.upToDate` will still be `false`: `mcp-registration` stays behind by
  design and `board-config` stays `compensated` with `fix: none — informational`. Treat the
  absence of a `skills` row as the pass condition; chasing `upToDate: true` would mean
  committing another workstation's path.
- **Both stamps read `0.4.0`** followed by `skills:` and the twelve installed names, and the
  name list matches the folders on disk.
- **`AGENTS.md` and `CLAUDE.md` are unchanged**, and `agents-block.mjs` is a no-op on a
  second run.
- **The diff contains nothing else.** `git diff --name-only origin/dev...HEAD` lists only
  paths matching `.agents/skills/kanmer-*`, `.agents/skills/.kanmer-skills-version`,
  `.grok/skills/kanmer-*` or `.grok/skills/.kanmer-skills-version` — 36 paths, no
  `AGENTS.md`, no `opencode.json`, no `.codex/`, no `.zcode/`.
- **CI `repository-check` is green** on the PR head. That is the merge gate.

## Commands

Implementer, cwd `C:/Users/PGUSER/Documents/github/pegasus-worktrees/kanmer-010-setup-drift`:

```
git -C <worktree> rev-parse --show-toplevel
git -C <worktree> branch --show-current
git -C <worktree> rev-parse --path-format=absolute --git-common-dir
node C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/scripts/agents-block.mjs C:/Users/PGUSER/Documents/github/pegasus-worktrees/kanmer-010-setup-drift
git -C <worktree> status --porcelain
git -C <worktree> diff --stat
git -C <worktree> diff --name-only origin/dev...HEAD
```

Parity, per tree (`.agents` and `.grok`) and per skill — the `--strip-trailing-cr` form is
the meaningful one on this checkout:

```
diff -rq --strip-trailing-cr <worktree>/<tree>/skills/<skill> C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/skills/<skill>
```

Whole-tree `diff -rq <worktree>/.agents/skills <bundle>/skills` is also worth running once:
it must report only the stamp file and the four repository-owned skills under `.agents`
(`pegasus-release`, `razor-pages-ui-design`, `razor-pages-ui-implementation`,
`razor-pages-ui-review`), and only the stamp file under `.grok`. Expect a `differ` line for
every file from the CRLF/LF translation; that form checks *membership*, and the
`--strip-trailing-cr` form checks *content*.

Board state, from `C:/Users/PGUSER/Documents/github/pegasus-work-pack/orchestration/claude`:

```
KANMER_REPO_ROOT=C:/Users/PGUSER/Documents/github/pegasus-worktrees/kanmer-010-setup-drift bash tools/kanmer-call.sh get_status '{}'
```

Prefix any Bash command carrying a `<rev>:<path>` argument with `MSYS_NO_PATHCONV=1`.

Test-runner role and CI `repository-check` (**the implementer does not run these**), cwd the
worktree:

```
pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1
pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base origin/dev -Head HEAD
pwsh -NoProfile -File ./scripts/Test-TestMarkdownPlacement.ps1
```

`Test-MarkdownPlacement.ps1` takes mandatory `-Base` and `-Head`; invoking it without them
fails on the parameter, not on the change.

**`dotnet build ./Pegasus.slnx` is not needed and is not part of this ticket's evidence.**
The change touches only `.agents/**`, `.grok/**` and (as an inspection) `AGENTS.md`; none of
those is compiled, referenced by `Pegasus.slnx`, or packaged. `dotnet test`, snapshot,
catalogue, browser, Playwright and `Invoke-LocalDevelopment` are likewise out of scope for
every role on this ticket. Tests: controller wave loop.

## Failure and deviation rules

- Stop and report, do not improvise, on: a failing acceptance check; a `skills` row still
  behind after the copy; a bundle folder or file missing from the plugin cache; an
  `AGENTS.md` change outside the markers or a thrown marker error; any `git status` entry
  outside the 36 declared paths; a CI `repository-check` failure.
- A pressure to widen scope is a stop, not a judgement call. Specifically: do **not**
  install Kanmer skills into `.zcode/skills`, do **not** "fix" `opencode.json` or
  `.codex/config.toml` in Git, do **not** add `.gitattributes` entries for skill Markdown,
  and do **not** edit the retired-path or stamp conventions.
- The MCP registration is reported, never committed. Record in the post-implementation
  report what `origin/dev` carries and that the fix is to reconnect the project in the
  Kanmer app on the host that uses it.
- A deviation is written down before it is acted on, and it is never a silent redesign. If a
  decision the plan does not settle becomes necessary, choose the option most consistent
  with `AGENTS.md` and the bundle, record it in `open-questions` at once, and stop rather
  than stacking a second decision on the first.
- Never merge the PR, never move more than one gated boundary, never start or take another
  ticket.

## Simplification pass — 2026-09-02

**Disposition: n/a — configuration and skill-tree refresh; no product code.** Confirmed by
the implementer against the actual diff (36 paths, +3,429 / −384) before opening the pull
request, with the four lenses applied one at a time:

- **Reuse.** Every changed byte of the twelve skill folders comes from
  `C:/Users/PGUSER/.claude/plugins/cache/kanmer/kanmer/0.4.0/skills` by byte copy, and both
  `.kanmer-skills-version` stamps were copied from the output the sanctioned 0.4.0 setup run
  had already written in the primary checkout rather than retyped from the plan's name list —
  so the generator, not this ticket, remains the author of the stamp format. The `AGENTS.md`
  block was left to `agents-block.mjs`, its only sanctioned writer. Nothing was reimplemented.
- **Simplification.** There is no authored logic to simplify. Any edit to the copied text
  would destroy the byte-parity this ticket exists to establish, so the correct disposition is
  to change nothing.
- **Efficiency.** Not applicable: no code path, query, allocation or loop changes. The 26
  files that differed only by line endings were deliberately left to normalise on staging
  instead of being rewritten, which keeps them out of both commits and off the reviewer's
  plate.
- **Altitude.** Both destinations are refreshed from one source in the same change, so
  `.agents/skills` and `.grok/skills` converge instead of drifting apart as separately
  maintained copies. Scope was held at reconciliation: `.zcode/skills` (no Kanmer skills
  installed), the repository-owned `pegasus-release` and `razor-pages-ui-*` skills, and the
  machine-specific MCP registrations were all left alone, the last of these reported rather
  than committed.

Recorded to satisfy `AGENTS.md` *Repository task workflow* step 4, which allows the `n/a`
disposition for a docs-only task.

## Stop condition

Stop at **PR_OPEN**. The final boundary is: the commit is pushed to
`task/kanmer-010-setup-drift`, a pull request is open against `dev` titled
`Reconcile Kanmer setup drift after KANMER-006 (KANMER-010)` with the footer line
`Kanmer: KANMER-010`, the post-implementation report is written (including the report-only
note about `opencode.json` and `.codex/config.toml`), and the ticket has moved
`implementing` → `review`.

Do not merge the PR. Do not move the ticket past `review`. Do not start, take or plan
another ticket. Review and merge belong to an agent that did not implement this work.
