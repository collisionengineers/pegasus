---
name: kanmer-research
description: Research a Kanmer ticket before it gets planned — dig into the codebase, record what you learned in the ticket's research.md, and survey what the change touches in impact.md. Use when the user says "research", "investigate", "dig into" or "scope out" a ticket, when a ticket is missing its research or impact document, and always before writing any plan.md. DO NOT USE FOR writing the plan itself (kanmer-plan) or implementing the change (kanmer-execute).
---

# Researching a Kanmer ticket

Research is the read-only **Researching** stage: you change no code and need no
branch or worktree. Its output is the docs in the ticket's folder, written with
`set_ticket_doc` — they are what plan.md is later built FROM, so their job is to
make planning boring. `get_doc_gates <id>` shows what each transition needs.

## Steps

1. **Read the ticket.** `get_item` for the body (the What/Why is your research
   question) and `get_links` for related tickets — prior research on a linked
   ticket often answers half the question. Check `get_ticket_doc` for an existing
   research.md; extend it (`append: true`) rather than overwrite it.
2. **Leave Backlog into Researching.** The **leave-Backlog gate** requires a
   governing doc: link the FRD/PRD this ticket implements (`link_doc <id>
   docs/frd/<slug>.md`), or — if it's genuinely still to be written — set
   `docs_todo` and hand off to `kanmer-docs`. Then `move_item <id> researching`.
3. **Investigate.** Read the code, run read-only commands, check docs and
   history. Chase the question the ticket actually asks. Keep provisional
   working notes with `append_scratch <id> research "<note>"` — scratch is the
   notepad, never a pipeline doc.
4. **Write `research.md`** from `assets/research-template.md`: the question,
   findings each with their source, and what they imply for this ticket.
5. **Write `impact.md`** from `assets/impact-template.md`: the files/modules the
   change touches and the risk in each, ripple effects (callers, tests, docs,
   build artifacts), and what's deliberately out of scope.
6. **Write `open-questions.md`** from `assets/open-questions-template.md` for
   anything unresolved — the questions plan.md must not silently assume. Ones
   only the user can answer go to the user **now**, not at planning time. New
   work that doesn't belong here becomes its own ticket (`kanmer-tickets`),
   linked with `[[ID]]` or `rel: "blocks"` if it must land first.

`research.md` + `impact.md` are the **Researching → Planning** gate. When both
exist and the open questions are answered or explicitly parked, the ticket is
ready for `kanmer-plan`.
