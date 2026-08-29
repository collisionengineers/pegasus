# Proof — KANMER-006: Reconcile the current Kanmer setup drift

## What was verified, and where

Verified on merged `dev` at `b92cb9a7` (`b92cb9a7b8bf7727b452aa397d9df04084da1270`),
in the primary checkout `C:/Users/PC/Documents/GitHub/pegasus`, on 2026-08-29.
The ticket shipped through PR
[#582](https://github.com/collisionengineers/pegasus/pull/582)
("KANMER-006: reconcile the current Kanmer setup drift", `task/kanmer-006-setup-drift`
→ `dev`, merged 2026-08-28T08:19:09Z) as merge commit `742c8f1e`, carrying the two
recorded commits `cc8863d3` (AGENTS.md managed block) and `0248da08` (`.grok/skills`
refresh). All three are ancestors of `b92cb9a7`:

```
git merge-base --is-ancestor cc8863d3 b92cb9a7  -> ancestor
git merge-base --is-ancestor 0248da08 b92cb9a7  -> ancestor
git merge-base --is-ancestor 742c8f1e b92cb9a7  -> ancestor
```

Nothing has touched those paths since:

```
git log --oneline 0248da08..b92cb9a7 -- .grok/skills AGENTS.md
  -> (no output)
```

**The headline result is mixed.** Two artefacts were genuinely reconciled and are
proven clean against the running engine. Neither of the ticket's own two
Verification items passes: the `.claude/skills` drift named in the ticket's *Why*
is still live, and TICK-222 was never reassigned. See **Outstanding**.

## Evidence

### The AGENTS.md managed block now byte-matches Kanmer 0.3.3

Tier: **deployed / exercised** — the running MCP server reads this file and no
longer reports it.

The consumer is the engine itself. `kanmer-mcp.cjs` (v0.3.3, sha256
`03196057…`, `…/Kanmer/resources/plugins/kanmer/mcp/kanmer-mcp.cjs`) defines the
markers it hashes:

```
var BLOCK_START = "<!-- kanmer:instructions:start — managed by kanmer-setup; edits inside will be overwritten -->";
var BLOCK_END   = "<!-- kanmer:instructions:end -->";
```

Extracting the body between those markers from `AGENTS.md` and from the bundled
`skills/kanmer-setup/SKILL.md`, normalising CRLF, and hashing both:

```
AGENTS.md sha256(block body) = 7b6a306b1d97d7e6171d16b77fa79c37165e87bc344d1f17ecb6667531db7f8e  (2446 bytes)
bundled   sha256(block body) = 7b6a306b1d97d7e6171d16b77fa79c37165e87bc344d1f17ecb6667531db7f8e  (2446 bytes)
MATCH: True
```

Correspondingly, live `get_status` on 2026-08-29 lists **no** `agents-block`
entry in `repo.stale` — it lists only `skills`, `skills-stamp` and
`board-config`. `AGENTS.md:1` and `AGENTS.md:22` hold the two markers.
`CLAUDE.md` is a symlink to `AGENTS.md` (`120000` mode in the index), so the
Claude-side instructions moved with it; `cmp CLAUDE.md AGENTS.md` reports
identical bytes.

### No repo-authored text was lost when the block was rewritten

Tier: **build/test** (diff analysis, not a runtime check).

The risk in `cc8863d3` was silently dropping repository conventions that had been
written *inside* the managed markers. Diffing removed against added lines:

```
removed lines: 10  added lines: 14
lines removed and NOT re-added elsewhere:
  - Work each fresh ticket on its own branch and worktree: … A resumed execution packet …
```

That single line is accounted for: it was split into the bundled bullet
("Work each ticket on its own branch and worktree: worktree `.worktrees/<id>`…")
and the "A resumed execution packet is available only in `implementing`…"
paragraph, which now sits verbatim under the new `## Kanmer conventions for this
repository` heading below the end marker. The board-branch convention, the local
MCP convention and the whole "Agent conduct" section relocated unchanged. No
repository rule was dropped.

### `.grok/skills` is content-complete against the 0.3.3 bundle

Tier: **deployed / exercised** — the engine hashes this tree and no longer
reports it.

`kanmer-mcp.cjs` checks three destinations:

```
var SKILL_DESTINATIONS = [ ".claude/skills", ".agents/skills", ".grok/skills" ];
var SKILLS_STAMP_FILE = ".kanmer-skills-version";
```

Comparing the bundled tree against the tracked one, file by file, stripping the
CRLF the Windows checkout applies:

```
bundled files: 33, differ after CR-strip: 0
bundled files missing from .grok/skills: (none)
git ls-files .grok/skills | wc -l  -> 44
```

The 11 extra tracked files are repository assets the 0.3.3 bundle does not ship
and the check does not flag —`.kanmer-skills-version`,
`kanmer-auto/assets/{current-run,run-state}-template.md`,
`kanmer-docs/assets/agents-template.md`,
`kanmer-plan/assets/{approval-contract,brief-cloud-infra,brief-data-migration,brief-docs,brief-fix,brief-ui-ux}.md`,
`kanmer-tickets/assets/group-context.md`. Scratch records that these were left in
place deliberately. Live `get_status` names only `.claude/skills` in its `skills`
entry, confirming `.grok/skills` passes the engine's own content hash.

### `.claude/` is genuinely outside PR reach — the plan's premise checks out

Tier: **build/test** (read-only repository check).

```
grep -n "claude" .gitignore   -> 25:/.claude/
git ls-files .claude | wc -l  -> 0
```

So the plan's step 3 (`.claude/skills` cannot be reconciled through a PR) is a
verified fact, not an excuse. It does not, however, make the drift reconciled.

### Solution build and test

Tier: **build/test**, cited not re-run, per the canonical gate evidence for
merged `dev` at `b92cb9a7`: `dotnet restore --locked-mode` exit 0; `dotnet build
--configuration Release` "Build succeeded. 0 Warning(s), 0 Error(s)"; `dotnet
test … --filter 'Category!=Corpus&Category!=Browser'` — ArchitectureTests 100
passed, Core.Tests 1133 passed, IntegrationTests 1022 passed / 2 pre-existing
skips, 0 failed. This ticket changed only `AGENTS.md` and `.grok/skills`, so it
carries no test of its own; the suite proves it broke nothing.

## The ticket's own verification items

| Item | Status | Evidence |
| --- | --- | --- |
| `get_status.repo.upToDate` is true, or every remaining entry is explicitly informational/compensated | **Not met** | Live `get_status` 2026-08-29: `upToDate: false`; `skills` = `behind` (".claude/skills: 1 file(s) differ … affected skills: kanmer-setup"), `skills-stamp` = `unstamped` (".claude/skills has no .kanmer-skills-version"). Only the third entry, `board-config`, is `compensated`. `behind` and `unstamped` are not informational — the tool defines `behind` as "act on it". |
| TICK-222 is in `delivery-repository`, has `docs_todo: false`, and remains Done with its release evidence unchanged | **Not met (one clause of three)** | `.kanmer/areas/_none/TICK-222/TICK-222.md` still reads `area: ''`; the folder is still under `areas/_none/`. `docs_todo` is absent (false) and the record is unchanged — `status: done`, commits `5e8ceff0`, `c56f00f8`, `7e9465b0…`, PR `540` — but that state predates this ticket: TICK-222's `updated` is `2026-08-26T14:34:46.354Z`, before KANMER-006 was taken at `2026-08-28T08:11:49.416Z`. No `update_item` from this ticket ever landed on it. |

## Outstanding

**1. The drift named in the ticket's own *Why* is still live.** The ticket was
written about `.claude/skills/kanmer-setup` differing from packaged 0.3.3 and
`.claude/skills` carrying no stamp. What shipped reconciled two *different*
artefacts — the AGENTS.md managed block and `.grok/skills` — both of which were
also in the reported set but neither of which is what the *Why* paragraph named.
The `.claude/skills/kanmer-setup/SKILL.md` still differs from the bundle by 299
diff lines after CR-normalisation, and `.claude/skills` still has no
`.kanmer-skills-version`. This is honestly recorded in the ticket's scratch, but
recording is not reconciling. It needs an operator action outside any PR:
reconnect the project in the Kanmer app (which writes the stamp), or copy
`…/Kanmer/resources/plugins/kanmer/skills/kanmer-setup/SKILL.md` over
`.claude/skills/kanmer-setup/SKILL.md`. **No ticket currently owns this** — it
was neither closed nor deferred to a successor.

**2. TICK-222's area assignment was never made.** Scratch records that
`update_item TICK-222 area: delivery-repository` still failed with Windows
`EPERM` on renaming `.kanmer/areas/_none/TICK-222`, and that no manual move was
attempted — correct conduct, but the item is undone. It requires no code and no
PR; it can be retried at any time the lock is gone. Not retried here: this proof
is read-only on the board apart from writing this document.

**3. Observation, not a defect.** `.grok/skills/.kanmer-skills-version` records
`0.1.0` while the tree's content is now 0.3.3. The engine's check is content-hash,
not version string, so it does not flag this and `upToDate` is unaffected — but
the stamp no longer describes what it stamps.

**4. Deferred by scratch, unticketed.** The Pegasus half of `AGENTS.md` duplicates
the "Operator-facing explanation is a defect" bullet verbatim under Simplicity
rails. Correctly ruled out of scope, but no follow-up ticket was raised.

## Scope of this proof

Written against merged `dev` at `b92cb9a7` per decision D15. `main` has not been
promoted; the exact-SHA `dev` → `main` promotion happens at wave 5.

**This ticket is held in Verifying, not moved to Done.** Both of its Verification
items fail against live evidence, and one of them (TICK-222's area) is still
actionable with no code change. What did ship is real and proven clean; the
ticket's stated objective is not yet met.
