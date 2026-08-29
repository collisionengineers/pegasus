# EPIC-011 closeout decisions — 2026-08-29

Binding for every member ticket, alongside `context.md`, `waves.md` and the three
earlier decision documents. Recorded by the orchestration session at the start of
the closeout run, after the full branch/PR/board examination.

## D23 — Audatex ships as a drawn seam, and that closes ENG-030

Operator decision: *"Audatex cannot be implemented yet. Just put the button in.
That counts as done."*

`ENG-030` is titled "Audatex direct estimating-service link, **or the operator
decision to drop it**". The operator has taken a third option: park it as a drawn
seam and record that as the outcome. The ticket closes on the recorded decision
plus the rendered control.

### This does not weaken D21 — read both carefully

The distinction is what the ticket's own deliverable *is*:

- **ENG-030 names no runtime capability to wire.** Its deliverable is the
  decision and the seam. There is no Audatex capability being claimed, so there
  is nothing for rule 14 to find unwired.
- **D21 still binds every other ticket.** No ticket may claim a capability
  delivered when that capability sits behind a disabled control or a closed gate.
  The table in `decisions-2026-08-29-done-rule.md` is unchanged.

A later agent must not read D23 as "disabled controls are deliverable now". They
are not. D22's formulation still governs: **draw it; never claim it.** D23 only
settles that a ticket whose entire scope is *decide and draw* is finished when it
has decided and drawn.

### The shape it takes

Already correct on merged `dev` at `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml:214`:

```html
<span class="gated" data-condition="...">
    <button type="button" class="btn" disabled aria-disabled="true">Audatex</button>
</span>
```

`ENG-030` must additionally record the exclusion in `docs/boundaries.md` so the
seam is a *named, ticketed* integration point and not an unexplained inert
control. Note `PLAT-061`: always set `data-condition`, or `.gated::after` paints
an empty pill.

## D24 — Three adjacent items join the closeout

Not EPIC-011 members, but in the closeout's path:

| Item | State | What it needs |
| --- | --- | --- |
| `TICK-077` (EXT-04) | `verifying`; code already shipped in release 36 | Proof against merged `dev`, walk to Done, remove the 195-file-stale branch and worktree. **No code.** |
| PR #601 (`INTK-048`) | MERGEABLE, CLEAN | Merge. It touches Unidentified receipts, adjacent to `INTK-046` (Done) and `INTK-047` (backlog) — leaving it open risks a conflict with the INTK-047 lane. |
| PR #600 (`DOCS-015`) | MERGEABLE, CLEAN, docs-only | Merge. No build impact. |

## D25 — One release, at the end

`main` is promoted once, after wave 5, as **release 37**, carrying the whole
epic. Production currently serves release 36 (2026-08-28); `dev` is 231 commits
and **9 migrations** ahead, head → `20260828185508_ProviderDeclaredInstruction`.

This confirms `waves.md` and D15 rather than changing them. Proof documents are
still written against merged `dev` and must name the SHA they were taken at.

## D26 — Every activation batches into the deploy

**No lane touches a feature flag or performs a live activation.** Lanes build,
test and prove their work locally and stop at the activation boundary.

`Features:ProviderApi` (TICK-058), the MAIL-028 retained-mail folder mover, and
the AUTO-012/AUTO-013 residual paths are opened together in the release, under a
single approval conversation, with their activation evidence recorded there.

This follows the standing repository rule that a closed gate is a disabled flag
and not a partially shipped feature: nothing behind a closed gate is claimed as
delivered until the gate is open in the deployed estate and `docs/operations.md`
records it.

## Merge-loop rule adopted from the DELIV-035 break

`DELIV-035` recorded a `dev` build break caused by orchestration, not by either
lane: `INTK-001` (#620) narrowed `QueuedIntakeStatus` while `TICK-058` (#594)
added a test constructing it with the removed member. Both were green on their
own CI. They were merged minutes apart in one batch, so neither CI run ever saw
the other's change, and git had nothing to report because the files do not
overlap.

**From here: when two PRs in a batch touch related Core types, the second gets a
`dev` merge and a fresh CI pass before it goes in — or they go in one at a
time.** A mergeability check cannot catch a semantic conflict between disjoint
files; only re-running the build against the merged result can.

## Three CI flakes, and what is true about each

| Flake | Rate | Ticket | Nature |
| --- | --- | --- | --- |
| SQL connect timeout | intermittent | `DELIV-031` | test harness; fix merged (`ConnectTimeout` 15→60) |
| Credential tamper no-op | **~1 in 16 (6.25 %)** | `DELIV-034` | test-only defect |
| Regex match timeout | unknown | `DELIV-036` | **production defect**, not test-only |

The credential rate is 6.25 %, not the 1-in-64 assumed when `DELIV-034` was
filed: the secret's final base64url character encodes only 4 bits, so it is drawn
from a 16-symbol alphabet that includes `'A'`
(`src/Pegasus.Core/Cases/PrincipalCredentials.cs:293`).

`DELIV-036` carries its own root-cause analysis. Two things it settles, so no
lane re-derives them: it is **not** catastrophic backtracking, and the 100 ms
budget is **not** the defect.

## Salvage, do not rebuild

Two branches are stale against `dev` but carry unique, wanted commits. Preserve
work that is not yours:

- `task/eng-028-estimate-editor` @ `6b4d11db` — the **ENG-028 multi-estimate
  editor**, ~1,676 lines across `Pages/Cases/Assessment/Index.cshtml(.cs)`,
  `OperatorLabels.cs` and `AssessmentEstimateImportWebTests.cs`. The ENG-028 lane
  starts from this commit; it does not start from scratch.
- `task/case-012-case-workspace-parallel` @ `866fe459` — a CASE-012 "one section
  list, one Due rule, one editing flag" refactor. Confirmed **not** on `dev`
  (`_CaseWorkspaceNav.cshtml` still holds the inline list). It is exactly the
  repository's own "one list per concept" rule.

## Model allocation for the closeout

Four reasoners. Codex gets real implementation lanes, including hard ones — not a
token share of mechanical tasks.

| Reasoner | Gets |
| --- | --- |
| Opus subagents | Cross-cutting Core work, migrations, high-blast-radius removals, final proofs |
| Sonnet subagents | Board walks, doc tickets, small single-file fixes |
| `gpt-5.6-sol` (high) | Implementation lanes |
| `gpt-5.6-terra` (xhigh) | Implementation lanes |
| `gpt-5.6-luna` (xhigh) | Implementation and adversarial verification |

**Cross-model verification is mandatory: no lane is verified by the model family
that built it.** Claude-built lanes are refuted by a Codex model; Codex-built
lanes by Opus. Single-model verification shares blind spots — in this programme
Codex refuted all three Claude-remediated lanes in one round, and 9 of 10 reports
were refuted in another.

Codex cannot reach the Kanmer MCP server from its sandbox. The pattern is a thin
Claude wrapper: Codex does the engineering and git; the Claude side does the
board work and **independently re-runs** the build and tests rather than trusting
the reported numbers.
