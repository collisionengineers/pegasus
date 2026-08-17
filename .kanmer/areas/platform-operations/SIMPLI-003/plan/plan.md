# Plan — SIMPLI-003: define the alpha journey and freeze non-cutover scope

Docs-only; ~3 files (`docs/prd/pegasus-product.md`, `docs/open-decisions.md`, `docs/capabilities.md`), roughly +70 / −40. No new Markdown file; no code.

## Approach

Route the decision to its owner and make the classification explicit. The journey was decided on 2026-08-02 and sits in `docs/open-decisions.md`, which by AGENTS.md routing holds unresolved questions; product scope belongs to the PRD. So: the PRD gains a "The alpha journey" section (journey sentence, ordered critical path, acceptance boundary, and the two classes of `Now` work — **journey** and **non-blocking for cutover**), open-decisions keeps only its open activation items with a back-link, and the register's activation column states the class on every `Now` row that lacks it. Horizon changes are the operator's (parked questions) and are not made here. Reuses: the decided text verbatim; the register's existing "non-blocking" wording; the SIMPLI-007 note that the acceptance roster is register-derived.

Governing docs: PRD `docs/prd/pegasus-product.md` (edited — it is the owner); `docs/capabilities.md` (schedule/registry — activation-column wording only, no horizon or target change); `docs/open-decisions.md`.

## Steps

1. **PRD** — add `## The alpha journey` after the scope paragraph: (a) the 2026-08-02 journey sentence; (b) the ordered critical path with capability IDs; (c) the acceptance boundary (alpha ends at the EVA handoff; reports/RPT are `Later`); (d) "Non-blocking for cutover" — the set from `open-decisions.md:34` plus the evaluator cluster and AI-09, each with its ID; (e) "Accepted before alpha, outside the ordered path" — TRI-01–09, EXT-14, INT-13, INT-27, flagged for the operator (parked question).
2. **open-decisions.md** — replace `:13-37` with a two-line back-link to the PRD section; keep `:59-118` (open activation details) intact.
3. **capabilities.md** — for each `Now` row in the non-blocking set whose activation cell does not already say so, append "Non-blocking for the `0.1.0-alpha.1` cutover ([PRD](prd/pegasus-product.md#the-alpha-journey))." No timing/target column changes.
4. **Verify** — `scripts/Test-DocumentationLinks.ps1`; markdown placement (no new .md); the register still has 131 `0.1.0-alpha.1` rows (roster unchanged — assert with the count); anchors resolve.
5. **PR to `dev`** (docs-only review) → merge → verify → proof → closeout. Report the two parked operator decisions in the PR description and the ticket outcome.

## Verification (ticket acceptance — "the alpha journey and paused scope are documented and unambiguous")

- The PRD states the journey and the critical path in one place, cited from the register and the board (HZN-003).
- Every `Now / 0.1.0-alpha.1` row is either on the critical path or carries "non-blocking for cutover" (or is in the flagged twelve awaiting the operator).
- open-decisions no longer claims ownership of the journey.

## Risks / stop rules

- Do not change any row's timing or target release (product authority; roster coupling).
- Do not restate operator-notes; cite them.
