# Plan body template

Use for `create_item` bodies with `type: "plan"`. A plan coordinates several
tickets toward one outcome; it is the map, tickets are the moves.

---

## Goal

The outcome in one paragraph, with the definition of done.

## Context

Why now; constraints; relevant research: [[RES-00X]].

## Tickets

| Ticket | Covers | Depends on |
|---|---|---|
| [[TICK-00A]] | … | — |
| [[TICK-00B]] | … | [[TICK-00A]] |

(Create the tickets with `create_item`, then fill this table with their real ids
and link each ticket back to this plan with `link_items`.)

## Risks / open questions

- Things that could invalidate the plan, and who decides.

## Status log

- YYYY-MM-DD: created; scope agreed with user.
