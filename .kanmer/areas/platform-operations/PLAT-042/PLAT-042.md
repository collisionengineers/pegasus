---
id: PLAT-042
type: ticket
title: 'Bind the additive-migration rule to cutover, not to today'
status: review
area: platform-operations
assignee: claude-code
profile: chore
stageEntered:
  implementing: '2026-08-24T10:36:02.168Z'
  review: '2026-08-24T10:36:44.353Z'
taken_at: '2026-08-24T10:34:42.606Z'
branch: task/plat-042-additive-rule-at-cutover
worktree: ../pegasus-worktrees/plat-042
labels:
  - governance
  - runbook
  - pre-cutover
  - operator-reported
links: []
blocks:
  - ENG-014
refs:
  - docs/runbook.md
archived: false
created: '2026-08-24T10:34:11.430Z'
updated: '2026-08-24T10:36:44.353Z'
---

## What

`docs/runbook.md:1140-1144` requires every migration to be additive so a
rolled-back application still runs. State that this binds **from cutover**, and
that before cutover a non-additive migration is allowed with its consequence
recorded.

## Why

Operator direction (2026-08-24): *"this is NOT RELEASED YET. this runbook line is
planning around legacy/fallbacks assuming its a live app - this isnt good. its
going to cause bloat."*

Checked before acting — the same three checks that settled the EVA manifest in
[[DOCS-013]]:

| Check | Result |
| --- | --- |
| `grep -rn -iE 'roll.forward\|additive migration\|rollback' reference/` | **zero hits** — no operator source |
| Present in the root commit? | **No** — this one git *can* date |
| When did it enter? | `25e170ff`, 2026-08-20, *"Record release 14 and the previous-artifact rollback procedure"* |

So it is four days old, written while documenting a rollback procedure, and
generalised from that into a standing constraint on every future migration.

**And the premise is not yet true.** `docs/open-decisions.md:22-33` puts the full
QDOS cutover at **step 7 of 8** on the ordered critical path. We are around step
5 — *"EVA bundle from a real case"*. No live QDOS instruction is worked in
Pegasus today. The production database has been wiped twice, and
`EvaHandoffRevisions` is empty (`docs/operations.md:410-411`).

Rolling back the application is therefore not a data-preserving recovery route,
because there is no business data to preserve. The rule buys nothing right now
and charges for it: honouring it means expand/contract, which doubles the
migration count — one release to make a column nullable, another to drop it —
against a database that still gets wiped.

Current text:

> **Database: schema is roll-forward only.** Releases keep migrations additive
> so the previous application runs against the newer schema; a migration that
> cannot honour that must ship an accepted recovery strategy instead.

## Approach

- Amend `docs/runbook.md:1140-1144` so the additive requirement **starts at
  cutover**. Before cutover: a non-additive migration is allowed, and the
  recovery route is rebuild-from-empty, not rollback. Record the consequence in
  operations at release time rather than engineering around it.
- Keep the rule intact for after cutover — it is correct *then*, and this ticket
  is about when it starts binding, not about deleting it.
- Add the switch-over to the cutover checklist in `open-decisions.md` step 7, so
  the constraint turns on with the thing it protects rather than being
  remembered.

**Immediate consumer:** [[ENG-014]] drops three columns from
`EvaHandoffRevisions` and currently reads as violating this rule. It should merge
after, or alongside, this amendment.

## Worth a reviewer's eye

This is the third inherited constraint this week that turned out to protect a
maturity the product has not reached — after the EVA manifest and the
`provenance.json` sidecar. Worth asking, while in the runbook, whether other
pre-cutover rules are paying for the same insurance. Do not sweep speculatively;
name any candidates for their own ticket.

Docs-only: the simplification pass records "n/a — docs-only".

## Verification

- [ ] The runbook states plainly when the additive rule starts binding
- [ ] The pre-cutover recovery route (rebuild, not rollback) is stated
- [ ] `open-decisions.md` step 7 carries the switch-over
- [ ] [[ENG-014]]'s migration no longer reads as a rule violation
