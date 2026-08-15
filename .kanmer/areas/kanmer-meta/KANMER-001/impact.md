# Impact — KANMER-001: Retarget Kanmer tickets citing the retired NOW.md / requirements.md

## What the change touches

**Board files only.** Every edit lands in `.kanmer/areas/**` through the kanmer MCP tools (ticket bodies via `update_item`, TICK-012/TICK-017 pipeline docs via `set_ticket_doc`), committed on the `kanmer-board` branch by the board tooling — no `src/`, `tests/`, `infra/`, `scripts/`, or `docs/` change, no task-branch PR content of its own. The task branch carries only this ticket's own pipeline docs if anything.

Concrete surface (from research.md):

- **44 category-(b) body edits** — retarget `requirements.md#anchor` links to the mapped FRD file + heading (all 16 anchors have verified live homes; zero unmapped) and rename the 19 generic "Requirements" link texts to name the owning FRD.
- **5 category-(a) unique-content body edits** (TICK-001, 118, 120, 199, 201) — replace the NOW.md source line with the Kanmer board / `docs/operations.md` / `docs/open-decisions.md` reference; preserve their substantive content untouched.
- **95 archival candidates** — `archived: true` plus a short migration note each; underlying queue-line ideas preserved (open-decisions.md line or board note) where not already covered.
- **2 pipeline-doc edits** (TICK-012 research/plan/proof; TICK-017 research) — retarget or annotate historical quotes.
- **Scope-decision dependent:** 7 done tickets and 16 already-archived tickets also carry dead links (see Decisions below).

## Blast radius and risks

- **The board is the human's live shared workspace.** ~150 edits and ~95 archivals change what Alex sees immediately, and archival drops visible todo count from 194 to roughly 99. This is exactly the propose-then-apply situation: **the archival batch and the mass edit need operator sign-off before applying** — the plan.md must present the batches for approval, not silently reshuffle.
- **The ticket's own hold clause.** Body says operator directed *hold* until "a Kanmer update" lands. The board is now format 2 with the doc-gate pipeline, which appears to be that update — but lifting the hold is the operator's call, not an inference. Blocked-on-confirmation before execution.
- **Concurrent work.** TICK-015/016/033 (in-progress) and TICK-012 (review) may be actively worked; body edits there risk clobbering concurrent changes. Mitigate with `expected_updated` on every `update_item` and re-read on conflict; coordinate rather than force.
- **Under-matching risk.** A pass that pattern-matches only the 105-ticket boilerplate misses the 8 unique-prose NOW.md citations; the retarget tooling must work from the explicit ID inventory in research.md, not a regex alone.
- **Over-archiving risk.** TICK-001, 118, 120, 199, 201 cite NOW.md but carry real content — explicitly excluded from archival. TICK-217/218/219 are intentionally-superseded archived stubs with migration notes — leave untouched unless the operator wants their links fixed too.
- **Idempotence/auditability.** Each edit should be one `update_item` with a one-line migration note so `get_activity` gives a reviewable trail; a partial run must be resumable from the inventory list without double-editing.

## Dependencies and sequencing

- **SIMPLI-004 / SIMPLI-006 (done)** are the upstream retirement this ticket reacts to; their proof docs are the mapping source of truth (51 slugs preserved, 0 unmapped) — already independently re-verified in research.md.
- **[[KANMER-002]]** (same branch/worktree) touches repo docs, not board bodies; the structural survey confirmed no board ticket cites `design/`, `docs/design.md`, `reference/`, or `artifacts/` paths, so the two tickets do not collide. If KANMER-002 later moves `docs/design.md` → `docs/design/README.md`, that is a *separate* future retarget class — out of scope here.
- **SIMPLI-005 (board triage, done)** is precedent for board-wide batch edits.

## Decisions needed before plan.md is final

1. **Lift the hold?** The Kanmer v2 update the operator was waiting for appears to be in place.
2. **Archived tickets (16) in scope?** They are inert; recommendation: leave bodies as-is (history), since archived views are explicitly historical.
3. **Done tickets (7) in scope?** Links are dead but the tickets are closed; recommendation: retarget them (cheap, keeps the rendered board link-clean) — but it is edit-of-record on completed work, so operator preference rules.
4. **Disposal shape for the 95:** plain archive with migration note, vs. archive + one consolidated "queue remainder" note in open-decisions.md. Recommendation: the latter only for clusters whose idea has no surviving capability/ticket coverage.

## Acceptance shape

- Zero `NOW.md` / `requirements.md` citations remain in active (non-archived) ticket bodies; every retargeted link resolves to a live FRD/operations/open-decisions heading.
- The 95 approved archival candidates are `archived: true`, each with a migration note; the 5 protected unique-content tickets are retargeted, not archived.
- TICK-012 / TICK-017 pipeline docs updated or historically annotated.
- An activity trail exists for every edit; the operator approved the batches before they were applied.

---

## Decisions recorded (operator, 2026-08-14)

1. **Hold lifted** — the mass retarget may proceed.
2. **Done tickets (7): in scope** — retarget their canonical-owner links.
3. **Archived tickets (16): in scope** — retarget them too (including the TICK-217/218/219 migrated-validation stubs' links).
4. **Disposal shape for the 95 (updated constraint):** the renderer cluster **TICK-203–TICK-216 must not be blindly archived** — the operator confirmed the renderer and document extractor are being *integrated into the repo* (see [[SIMPLI-015]]), so that cluster's queue-line content is consolidated/retargeted into [[SIMPLI-015]] rather than dropped. Disposal shape for the remaining ~83 boilerplate candidates is still per the original plan (archive with migration note; consolidated open-decisions note only where no surviving coverage exists).

**Anomaly resolved:** TICK-017's "blocked" badge came from a pre-existing `TICK-012 → blocks → TICK-017` dependency edge (created 2026-08-12, before this task); since TICK-017 is done, the edge was stale. Removed 2026-08-14; a plain relates-link between the two was retained for history.
