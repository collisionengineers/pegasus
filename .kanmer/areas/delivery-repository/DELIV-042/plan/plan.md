# Plan — DELIV-042 board setup for EPIC-012

Board-only chore; no repository change.

## Steps

1. `create_group` EPIC-012 with the goal body; `set_group_doc context.md` from the implementation plan Appendix B plus the operator's 2026-09-02 answers (D29–D43). Reuses: kanmer-tickets group conventions, `assets/group-context.md` headings.
2. `create_items` for the fifteen new tickets (Appendix A payloads; `docs_todo` on the twelve that wait for DELIV-041; `refs` on the three chores). Reuses: `assets/ticket-template.md` body shape.
3. `update_item` with `expected_updated` on CASE-029, ENG-029, ENG-031, DOCS-017, CASE-009 (re-scope, group membership); `append_scratch` on INTK-032, DELIV-030, UIIMP-010, TICK-083 (notes only, no scope change).
4. `link_items rel:"blocks"` for the dependency table; `relates` for TICK-083, AUTO-015, INTK-032.
5. Write `automation/runs/20260902T203000Z-claude-fable.md` and `automation/current.md` (kanmer-auto run-state template); read both back.

## Acceptance

- `get_group EPIC-012` lists 21 members; `get_links DELIV-041` shows 13 blocks edges; `get_links UIIMP-014` shows 10 blocked-by edges.
- Every new ticket is in Backlog with either `refs` or `docs_todo`.

## Stop condition

Stop after the run record is read back; DELIV-041 is the next lane.

## Simplification pass

n/a — board-only.
