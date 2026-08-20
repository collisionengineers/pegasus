# Proof — INTK-007

## Merge

PR #424, merge commit `ed3be51c95bc2a055606e5210131d37de9de2dd1` on `dev`/`main`
— this PR's merge **is** the release-12 head itself (the last PR integrated
before promotion).

## Deployment

Shipped in **release 12** (`ed3be51c95bc2a055606e5210131d37de9de2dd1`,
deployed 2026-08-19 ~22:40–22:52Z). See [[DELIV-012]] proof for the
release-12 deployment readbacks, including migration
`20260819115323_UnidentifiedWork` applied within the 8-migration batch.

## Production evidence

Per [[DELIV-012]] proof's original signed-in production verification and the
release-13 operator review: `U1`–`U6` are live, real, allocated references
(U1–U5 from the release-12 migration backfill against real retained
receipts; U6 recovered by [[INTK-011]]'s reconciliation in release 13).
Vocabulary replacement is substantively complete: the "Needs sorting"
stale-term inventory in this ticket's own post-implementation report found
176 hits pre-fix, migrated 12 files including the `AGENTS.md`/`CLAUDE.md`
product-invariant wording, and every operator-facing surface renders
"Unidentified" — the persisted `IntakeDecision.NeedsSorting` internal
enum/DB code is deliberately retained as compatibility, not stale
operator-facing text.

## Honest qualification

- The ticket's own PIR names two unfinished follow-ups: (1) FRD-03/FRD-08's
  Triage-without-VRM wording still says "Needs sorting" against
  `operator-notes.md`'s now-updated "Unidentified (formerly Needs sorting)"
  — a named, undone cross-document reconciliation; (2) full-suite
  IntegrationTests execution did not emit a final summary in that session
  (a lingering test host), stated as "verify on merged main" rather than
  claimed complete.
- The **surface this ticket shipped — a top-level "Unidentified" nav entry
  and its own Index/Details pages — was restructured by [[INTK-009]] in
  release 13**: the nav entry was removed and Unidentified became a Queues
  tab with image/e-mail filters, and the Details page copy was rebuilt
  (operator feedback: "a ton of slop", raw GUIDs, "intake"/"custody"
  wording visible). INTK-009's fixes targeted exactly the presentation
  layer this ticket shipped; the underlying Core contract, U-reference
  allocation, and history this ticket built are unchanged and still the
  live mechanism behind U1–U6.
