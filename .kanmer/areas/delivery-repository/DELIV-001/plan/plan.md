# Plan — DELIV-001

Docs-only; ~2 files (+~120 / −~10): `AGENTS.md` (managed Kanmer block untouched) and `docs/engineering.md`.

## Steps

1. `AGENTS.md`: insert **Simplicity rails** (proposal A + A′ — eight one-line rules incl. "simplify without over-correcting" and "the pass is quality, not correctness") after the "Prove the actual caller" bullet in *Planning process*; amend *Repository task workflow* step 3 (plans state reuse; research separates verified from assumed), step 4 (run the simplification pass over the branch's own diff before the PR; record it in the ticket plan), step 5 (reviewer checks the pass ran and dispositions are honest).
2. `docs/engineering.md`: new `## Simplicity` section (proposal E + E′): the four lenses table, dispositions, skip rules, balance, efficiency smells, scope and timing, fault-handling shape, test support, plan sizing. Cross-link from `## Engineering invariants` intro line if the register there lists sections.
3. Verify: `scripts/Test-DocumentationLinks.ps1`; markdown placement (no new .md); AGENTS.md managed block byte-identical (kanmer-setup owns it).
4. PR to `dev` (docs-only review: diff + description for missing/unauthorised scope); merge; verify; proof; closeout.

The proposal text is on `scratch-proposal` (main + addendum with `[skill]`/`[agent]` provenance).
