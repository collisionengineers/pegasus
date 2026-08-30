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

---

# HELD — re-verified 2026-08-29, closeout board walk

## Verdict: **this ticket does NOT reach Done.** It stays in Verifying.

Re-verified against **merged `dev` at
`450b9234a6f5626f21adea3c4da244550a3bdace`** (2026-08-29 18:03:20 +0100).
`b92cb9a7`, the SHA the body above was written at, is an ancestor of it.

This remains **dev-merged evidence, pending the single wave-5 `dev` → `main`
promotion**. (For this ticket the code half is `AGENTS.md` and `.grok/skills`,
which ship in no artifact; the unmet half is live board/tooling state, which no
promotion changes.)

The body above already concluded "held in Verifying, not moved to Done". Both
of its Verification items were re-checked from live state rather than taken on
trust, and **both still fail**.

## Item 1 — `get_status.repo.upToDate` — re-checked live, still FAILS

A fresh `get_status` call at the start of this closeout returned:

```
repo.upToDate: false
repo.stale:
  - artefact: skills          state: behind
    detail: ".claude/skills: 1 file(s) differ from the bundled skills and
             0 are missing — affected skills: kanmer-setup."
    fix:    "run kanmer-setup (it reconciles; FRD-013), or reconnect this
             project in the Kanmer app"
  - artefact: skills-stamp    state: unstamped
    detail: ".claude/skills has no .kanmer-skills-version, so nothing records
             which Kanmer wrote it or which skills it owns there."
  - artefact: board-config    state: compensated
    detail: "board.yml's profiles omit questions-resolved; core injects it at
             read time … the gate is in force"
    fix:    "none — informational"
```

The ticket's acceptance is: *"`get_status.repo.upToDate` is true, **or** every
remaining entry is explicitly informational/compensated."* Neither branch
holds. `upToDate` is `false`, and only **one** of three entries
(`board-config`) is `compensated`. The tool itself defines `behind` as "act on
it", and `unstamped` as "no evidence either way" — neither is informational.

This is the drift the ticket's own *Why* paragraph was written about. What
shipped reconciled two **different** artefacts (the `AGENTS.md` managed block
and `.grok/skills`), both real and both proven clean in the body above, but
neither is the one the ticket named.

## Item 2 — TICK-222's area — re-checked live, still FAILS

A fresh `get_item TICK-222` returned:

```
id: TICK-222   status: done   area: ""          <- still unassigned
updated: 2026-08-26T14:34:46.354Z               <- predates KANMER-006's take
```

The acceptance is: *"TICK-222 is in `delivery-repository`, has
`docs_todo: false`, and remains Done with its release evidence unchanged."*
Two clauses of three hold — it is Done, its commits (`5e8ceff0`, `c56f00f8`,
`7e9465b0…`) and PR 540 are unchanged, and `docs_todo` is absent. The area
clause does not: `area` is still the empty string, so the item is still filed
under `areas/_none/`, and its `updated` timestamp is still earlier than
KANMER-006's `taken_at` of `2026-08-28T08:11:49.416Z` — no `update_item` from
this ticket ever landed on it.

**This item was not retried during this walk.** The closeout brief is explicit
that the board is reconciled through the MCP tools and that no worktree,
branch or repository state is to be changed; retrying the area assignment is a
board mutation on a *different* ticket that this pass was not asked to make,
and the Windows `EPERM` folder-rename lock that blocked it three times may
still be present. It remains actionable at any time, needs no code and no PR.

## Why this is a hold and not a pass

Rule 20 binds: *"Verify with exit codes … INCONCLUSIVE is not PASS, and a later
pass does not erase a failure. Done requires PASS."* Both acceptance items
return a definite **FAIL** against live state, not an inconclusive. There is no
reading on which this ticket is finished.

Rule 14 is not the barrier here — this is a `chore`-profile board/tooling
ticket that ships no runtime capability, so D20's strict caller rule has
nothing to bite on. The barrier is simply that the ticket's own stated
objective is not met.

## What has to happen for this to reach Done

Two actions, neither requiring code, a PR, or a merge:

1. **Reconcile `.claude/skills`.** `.claude/` is git-ignored
   (`.gitignore:25 /.claude/`, `git ls-files .claude` → 0 files), so this can
   never arrive through a PR. It needs either the `kanmer-setup` skill run
   against live status, or the project reconnected in the Kanmer app — which
   also writes the missing `.kanmer-skills-version` stamp. **No ticket other
   than KANMER-006 owns this.**
2. **Assign TICK-222 to `delivery-repository`** with `update_item` (never a
   manual folder move), once the Windows process lock is clear.

Then re-run `get_status` and confirm `repo.upToDate` is `true` or every
remaining entry reads `compensated`.

## Two observations carried forward, unchanged

- `.grok/skills/.kanmer-skills-version` records `0.1.0` while its content is
  0.3.3. The engine's check is a content hash, so this is not flagged and does
  not affect `upToDate` — but the stamp no longer describes what it stamps.
- `AGENTS.md` duplicates the "Operator-facing explanation is a defect" bullet
  verbatim under Simplicity rails. Confirmed still present at `450b9234`.
  Correctly out of this ticket's scope; still unticketed.

## What this evidence does NOT prove

- **It does not prove the two shipped artefacts regressed.** They did not: the
  `AGENTS.md` managed block still byte-matches the 0.3.3 bundle (live
  `get_status` reports no `agents-block` entry), and `.grok/skills` still
  passes the engine's content hash (live `get_status` names only
  `.claude/skills`).
- **It does not prove `.claude/skills` is broken in use** — only that it
  differs from the bundle and carries no ownership stamp.
- **No board mutation was made to TICK-222** during this walk.

---

# Re-checked 2026-08-30, after the Kanmer GUI was closed

## Item 2 — TICK-222's area — now **PASSES**

The blocker was environmental, not a decision. Three attempts on 2026-08-28 and
two more on 2026-08-30 failed with Windows `EPERM` renaming
`.kanmer/areas/_none/TICK-222`, because the Kanmer GUI (four processes) held
directory watch handles on every existing ticket folder. Proved it was the lock
and not a permission fault: a **freshly created** directory under `areas/`
renamed fine in the same moment that both ticket folders refused.

With the GUI closed, `update_item` succeeded immediately and **no folder was
moved by hand**, as this ticket's own body instructs:

```
TICK-222 -> areas/delivery-repository/TICK-222   (TICK-222.md, plan, proof, scratch all carried)
  status: done      docs_todo: absent (false)
  commits: 5e8ceff0, c56f00f8, 7e9465b0…   prs: 540   deployment: n/a   <- release evidence unchanged
```

`TICK-223` was moved to `ui-improvement` in the same pass, and **no ticket on the
board now lacks an area** (direct count: 0). `areas/_none/` is empty.

## Item 1 — `get_status.repo.upToDate` — still **FAILS**, and is worse than recorded

Closing the GUI did not fix this. It made it **visible**: the stale list went
from **three entries to six**.

| Artefact | 2026-08-29 and earlier today (GUI running) | Now (GUI closed) |
| --- | --- | --- |
| `agents-block` | *not reported* | **behind** |
| `.claude/skills` | behind | behind |
| `skills-stamp` | unstamped | unstamped |
| `.agents/skills` | *not reported* | **behind — 15 differ, 10 missing** |
| `.grok/skills` | *not reported* | **behind — 15 differ** |
| `board-config` | compensated | compensated |

**This matters beyond this ticket.** Every previous evaluation of this
acceptance line — including the `# HELD` section above, written 2026-08-29 —
was made against an under-reporting checker. The conclusion (`FAILS`) was right,
but the scope was understated by half.

**Inference, flagged as such:** the most likely cause is that the checker could
not enumerate those directories while the GUI held handles on them and skipped
them silently rather than reporting them as unknown. I did not instrument the
checker to prove that, so it stays an inference.

**It is not a regression from today's work.** `git status` over `AGENTS.md`,
`.claude/skills`, `.agents/skills` and `.grok/skills` is clean — no tracked file
was modified in this session, and this board-groom pass touched none of them.

### A concrete cause for `agents-block`, worth having before the fix runs

`AGENTS.md`'s managed block is **lines 1–22**. The block Kanmer 0.3.3 ships (the
same one rendered into `CLAUDE.md`) is several times that length — it carries the
stages, the gate rules, the doc-folder guidance and the skill order. So
`AGENTS.md` is not subtly out of sync; it carries a **materially older and
shorter** version of the instructions.

Line 1 also begins with a **UTF-8 BOM** before the start marker, which is worth
checking against the reconciler's expectations before assuming a plain rewrite
will settle it.

Note the interaction with [[UIIMP-005]]: `04e580c5` edited `AGENTS.md` to record
the Test UI regenerate/verify convention. If that content sits **inside** the
managed block, `kanmer-setup` will overwrite it — the marker says so in terms.
Check that before reconciling, or the fix silently deletes a convention this
repo depends on.

`.grok/skills` was last written by **this ticket's own commit `0248da08`**
("refresh .grok/skills from the bundled 0.3.3 skills") and is reported behind
again, which is the strongest signal that the earlier reconciliation was measured
against the same incomplete picture.

## Verdict: **still HELD.** It stays in Verifying.

One of two acceptance items now passes. The other fails wider than recorded.

## What was deliberately NOT done

`kanmer-setup` was **not run**. It is this ticket's remediation, it rewrites
`AGENTS.md` and two tracked skill trees, and under the repository workflow that
belongs on this ticket's own branch with a reviewed PR — not inside a
board-grooming pass. Running it here would also have risked the UIIMP-005
interaction above, unexamined.

### Correction to the UIIMP-005 caution above — checked, and it is safe

The section above warned that `kanmer-setup` might overwrite UIIMP-005's Test UI
convention if that content sits inside the managed block. **It does not.**

```
managed block:            lines 1–22
Test UI convention:       lines 109–112  ("After changing a routed Razor page…")
04e580c5 added 7 lines, all outside the block
```

So reconciling `AGENTS.md` will not delete the regenerate/verify convention. The
caution was worth raising and is now closed; do not carry it forward as a
blocker on the fix.

The other two points stand unchanged: the block is materially shorter than the
one Kanmer 0.3.3 ships, and line 1 carries a UTF-8 BOM ahead of the start marker.

---

# PASS — 2026-08-30, on merged `dev`

PR #638 squash-merged to `dev` at `a426ba57`; primary checkout updated to it;
worktree and branch removed.

## Both acceptance items now met

**Item 1 — `get_status.repo.upToDate`.** Live call after the merge:

```
repo.upToDate: true
repo.stale:
  - skills-stamp   unstamped     (GUI action: "reconnect in the Kanmer app")
  - board-config   compensated   ("none — informational")
```

Six entries down to two, and the acceptance reads "`upToDate` is true, **or**
every remaining entry is explicitly informational/compensated". `upToDate` is
now literally true, so the first branch holds outright.

**Item 2 — TICK-222.** In `delivery-repository`, `docs_todo` absent, still Done,
commits `5e8ceff0`/`c56f00f8`/`7e9465b0` and PR 540 unchanged. Moved by tool, no
folder touched by hand.

## What is NOT proved

- `skills-stamp` is still unstamped. It is not fixable from here — writing
  `.kanmer-skills-version` is a GUI action. It is disclosed, not resolved.
- `.claude/skills` is gitignored, so its reconciliation is local to this
  workstation and rides in no artifact.
- Nothing here is deployed; this ticket ships in no runtime artifact
  (`deployment: n/a`).
