---
name: kanmer-plan
description: Plan a Kanmer ticket — turn its research.md and impact.md into a concrete plan.md and an executable checklist.md. Use when the user says "plan", "design the approach for" or "break down" a ticket, or when a researched ticket needs its plan before implementation. DO NOT USE FOR the research itself (kanmer-research — do that first if research.md or impact.md is missing) or for implementing the plan (kanmer-execute).
---

# Planning a Kanmer ticket

A plan is only as good as what it's built from. plan.md is written FROM
research.md and impact.md — never before them, never instead of them.

## Steps

1. **Check the inputs.** `get_item` for the ticket, then `get_ticket_doc` for
   `research` and `impact`. If either is missing or visibly stale, do the
   `kanmer-research` job first — the **Researching → Planning** gate won't let
   the ticket into Planning without them, and you shouldn't plan around the gap.
2. **The ticket is in the Planning stage** (it left Researching once research +
   impact existed). Resolve stage ids against `list_board`.
3. **Write `plan.md`** from `assets/plan-template.md`: the chosen approach and
   why it beat the alternatives, concrete ordered steps, how proof will be
   produced, and risks with mitigations. It **must** carry a **Governing docs**
   section — how the plan meets each linked PRD/FRD/ADR (`refs`), or, *only with
   explicit user authorization*, how it modifies one, or why a new ADR is being
   written. Design decisions become **ADRs** via `kanmer-docs`, linked into
   `refs`. (Gates check a doc exists; this content rule is enforced here and
   checked by `kanmer-review`.)
4. **Distill `checklist.md`** from `assets/checklist-template.md`: one `- [ ]`
   box per plan step, ending with the verification the post-implementation
   report will summarise. Each box must be independently checkable — "wire the
   retry call", not "do the backend".
5. **Sanity-check scope.** If the plan grew beyond one unit of work, split it:
   file the extra tickets (`kanmer-tickets`), link with `rel: "blocks"` where
   order matters, and shrink this plan back to its ticket.
6. **If the plan changes anything user-visible or contested, show it to the user
   before implementation starts** — a paragraph summary, not the whole document.

`plan.md` + `checklist.md` are the **Planning → Implementing** gate. When they
exist and the user has approved, `get_doc_gates <id>` will show the move is
clear, and the ticket is ready for `kanmer-execute`.
