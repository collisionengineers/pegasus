---
name: kanmer-plan
description: Plan a Kanmer ticket — turn its research and files documents into a concrete plan and an executable checklist. Use when the user says "plan", "design the approach for" or "break down" a ticket, or when a researched ticket needs its plan before implementation. DO NOT USE FOR the research itself (kanmer-research — do that first if research or files is missing) or for implementing the plan (kanmer-execute).
---

# Planning a Kanmer ticket

A plan is only as good as what it is built from. The plan is written FROM the
research and files documents — never before them, never instead of them.

**`get_doc_gates <id>` is the authority on what this ticket needs**, and it is
the only one — a profile's requirements are resolved per board and change
without this file changing. Some profiles ask for no plan at all. Ask rather
than assuming, and do not reason from `board.yml`: requirements are injected at
resolve time, so its `profiles:` block is not the effective set.

## Workflow

1. **Check the inputs.** `get_item` for the ticket, then `get_ticket_doc` for
   `research` and `files`. If either is missing or visibly stale, do the
   `kanmer-research` job first — you should not plan around the gap, whether or
   not this ticket's profile happens to gate on them.
2. **The ticket is in Preparing.** Research and planning share that stage in the
   six-stage board, so there is no move between them — the move you are working
   towards is Preparing → Implementing.
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
7. **Put the open questions to the user, then revise the plan around the
   answers.** This is the moment for it: research surfaced them, the plan is
   what would otherwise silently assume one. Ask them together, each with a
   recommendation, and record the answer in `open-questions` — a question
   answered in chat and not written down is a question nobody can find later.
   Take trivial defaults rather than asking; say in the document that you took
   them.

When the documents exist and the user has approved, `get_doc_gates <id>` shows
the Preparing → Implementing boundary passable and the ticket is ready for
`kanmer-execute`. If it still reports `questions-resolved` unmet, step 7 is not
finished: `open-questions` has unticked `- [ ]` lines. Answer them and tick, or
move them under `## Parked (explicitly deferred)` with a reason for deferring.

A move may cross **one** gated boundary at a time, so do not try to jump a
planned ticket further than Implementing — the move is refused, and the refusal
names the next stage.

---

**Hand off to `kanmer-execute`**, which takes the ticket into a worktree and
works the checklist you just wrote. If planning turned up a question only the
user can answer, hand off to *them* first — an unanswered question is the one
thing that should stop this ticket reaching Implementing.
