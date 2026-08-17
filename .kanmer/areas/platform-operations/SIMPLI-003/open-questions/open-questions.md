# Open questions — SIMPLI-003

- [x] **Where does the journey live?** Decided (planner, 2026-08-17, from AGENTS.md routing): the PRD `docs/prd/pegasus-product.md` gains a "The alpha journey" section carrying the 2026-08-02 decision verbatim (journey sentence, ordered critical path, acceptance boundary, non-blocking set); `docs/open-decisions.md` keeps only its genuinely open activation items and back-links to the PRD.
- [x] **What does "paused" map to?** Decided (planner): the register's existing vocabulary — a `Now / 0.1.0-alpha.1` row is either **journey** (on the critical path; a cutover gate) or **non-blocking for cutover** (may land in `0.1.0-alpha.1`, does not gate it), stated in the activation column. No new state; no horizon change in this ticket.

## Parked (explicitly deferred)

- [ ] **Should the non-blocking rows also move to `Next`?** — *Parked: product authority (`docs/capabilities.md:317`); not needed to define the journey; note the runbook coupling: re-targeting shrinks the `OfflineCandidate` roster automatically.* Rows: MCP-01–04, MCP-06, INT-17, INT-31, AI-09, EVAL-01–05, OPS-22, MAIL-20, MAIL-14, MAIL-16, EXT-01/02 live adapters (replay accepted), OPS-09, DATA-01. Operator to decide; if yes, a one-line register edit per row plus a `docs/boundaries.md` "deferred until after alpha" note.
- [ ] **Are TRI-01–09, EXT-14, INT-13, INT-27 journey or not?** — *Parked: product authority; the register says "required and accepted before 0.1.0-alpha.1", the decided path omits them.* The PRD section will list them under "accepted before alpha, outside the ordered critical path" so the contradiction is visible rather than silently resolved either way. Operator to confirm.
