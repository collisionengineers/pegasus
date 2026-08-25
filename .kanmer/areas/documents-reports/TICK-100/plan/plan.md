# Plan — TICK-100: close the unsupported RPT-05 boundary

## Approach

Close this ticket at the same decision/closed-boundary tier as [[TICK-099]]. RPT-05 is allocated to Later / 1.1.0 but is not active: no approved Collision Engineers addendum artifact or confirmed caller exists, and the imported generic `addendum-report` preset is not product authority. [[DOCS-004]] retains the future activation work. This ticket records that unavailable, fail-closed state and introduces no dormant implementation.

## Governing docs

- `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` already owns immutable report identity, reasoned successor versions, retained earlier artifacts, and no silent overwrite. It does not authorize addendum wording or workflow.
- `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md` requires any future caller to reuse the Core-owned contract and Infrastructure adapter; it forbids a second renderer boundary.
- `docs/capabilities.md` remains allocation only. No governing document changes in this decision closeout.

## Steps

1. Record RPT-05 as unsupported, unavailable, and fail closed until both a representative approved addendum artifact and a named real workflow/caller exist.
2. Cite [[SIMPLI-014]] as proof that the integrated application activates assessment and fee-note only; addendum rendering is absent.
3. Leave all future contract, wording, predecessor/delta, authorization, recovery, parity, and caller work with [[DOCS-004]].
4. Remove obsolete implementation blocker edges to this completed decision while retaining ordinary relationships to the prerequisite and implementation records.
5. Complete a zero-repository-diff report, retrospective review, and proof that make no rendering or deployment claim.

## Verification

- Confirm the merged SIMPLI-014 proof states addendum and every legacy template remain unavailable.
- Confirm current `origin/dev` contains no live `addendum-report` selector in application source or tests and no `workspaces/report-renderer` tree.
- Confirm DOCS-004 remains Backlog with both activation conditions explicit.
- Confirm this ticket has no repository commit, PR, worktree, deployment, or cloud action.

## Risks

The main risk is that Done could be read as delivered. Every closing record therefore says Done means the deferral boundary is decided; RPT-05 itself remains unavailable.
