# Plan — KANMER-006 reconcile the current Kanmer setup drift

## Diff estimate

- `AGENTS.md`: managed block body replaced with the literal body shipped in
  Kanmer 0.3.3 `kanmer-setup/SKILL.md` (~12 lines changed); the board-branch
  paragraphs and the "Agent conduct" section that currently sit *inside* the
  markers move verbatim to just below the end marker so nothing is lost.
- `.grok/skills/**`: 15 tracked files overwritten with the bundled 0.3.3
  copies (the `get_status` "differ" list). Extra tracked assets not shipped by
  0.3.3 stay; they are not flagged.
- No code, no board.yml, no `.worktrees/` change. Roughly 40 files, mostly
  wholesale template refreshes.

## Steps

1. Reconcile `AGENTS.md`: `get_status` compares the between-marker body by
   SHA-256 against the bundled skill's block, so the body must match exactly.
   Rewrite the body from the bundle; relocate the repo-authored text that had
   been placed inside the markers to directly after `<!-- kanmer:instructions:end -->`.
   The `scripts/agents-block.mjs` script is not in the plugin install, so this
   is the hand-edit path the skill allows.
2. Reconcile `.grok/skills`: copy every file under the bundled
   `plugins/kanmer/skills` tree over the tracked copy (idempotent copy).
3. `.claude/skills` is gitignored (`/.claude/` in `.gitignore`) and only
   exists in the main checkout, so it cannot be reconciled through a PR; the
   fix is a local copy of the bundled `kanmer-setup/SKILL.md` plus the stamp,
   done by the operator (or "reconnect" in the Kanmer app). Record in scratch.
4. Board hygiene: `update_item TICK-222 area: delivery-repository`; note the
   `C:/Users/Alex/…` worktree paths on CASE-024 and MAIL-017 in scratch.
5. Commit in slices (AGENTS.md; .grok/skills), push, PR to `dev`.

## Reuse

Bundled skills tree and SKILL.md block body are the single source; no new
files, no scripts added.

## Simplification pass

n/a — docs-only.

---

## Authorised scope extension — 2026-08-30

The ticket's Approach says "reconcile **only** the reported behind/unstamped
artefacts". This pass exceeds that by one line, with operator authority recorded
here so the change is not an agent's unilateral widening.

### Why the stated scope could not be met as written

Refreshing `.grok/skills` from the bundled Kanmer skills reintroduces a link to
`docs/manual/greenfield.md` — a file in **Kanmer's own product repo**, not this
one — and `scripts/Test-DocumentationLinks.ps1` fails on it:

```
BROKEN .grok/skills/kanmer-setup/SKILL.md: ../../../../docs/manual/greenfield.md
1 broken relative Markdown link(s).      exit 1
```

That link had been removed once before, by `81fd677f` "fix(docs): remove broken
Kanmer greenfield link".

`Test-DocumentationLinks.ps1:14` excludes `.claude`, `.agents`, `.codex` and
`.kanmer` — every other vendored agent-skill tree — but **not `.grok`**. So
`.grok/skills` cannot be byte-identical to the bundled skills *and* pass CI.

**This makes acceptance item 1 unsatisfiable by construction.** Repeating the
`81fd677f` hand-deletion permanently diverges the tree from what Kanmer ships, so
`get_status` reports it behind forever and `repo.upToDate` can never be true —
with the same manual deletion owed after every future Kanmer update.

### The decision

Put to the operator on 2026-08-30 with three options: add `.grok` to the
exclusion list; strip the four lines again following precedent; or strip now and
file the conflict separately.

**Operator chose: add `.grok` to the exclusion list.** One character of a regex
in `scripts/Test-DocumentationLinks.ps1`.

### Why the gate is not weakened

`.grok/skills` is vendored third-party content this repo does not author, whose
links point into their own repository — the same property that already justifies
excluding `.claude`, `.agents` and `.codex`. Its absence from that list was an
oversight, not a decision.

**Proven, not asserted** (rule 21 — a gate that gates nothing is a defect):

```
./scripts/Test-DocumentationLinks.ps1                    -> exit 0, 87 files
plant a broken link in docs/index.md, re-run             -> exit 1, names it
restore docs/index.md, re-run                            -> exit 0
```

### Consequence for this ticket's acceptance

With `.grok` excluded, both trees stay byte-identical to bundled, so acceptance
item 1 becomes reachable once the primary checkout carries the merged change.
Without it, item 1 was impossible.
