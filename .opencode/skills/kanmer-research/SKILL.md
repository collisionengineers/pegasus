---
name: kanmer-research
description: Research a Kanmer ticket before it gets planned — dig into the codebase, record what you learned in the ticket's research/ folder, and survey what the change touches in files/. Use when the user says "research", "investigate", "dig into" or "scope out" a ticket, when a ticket is missing its research or files document, and always before writing any plan. DO NOT USE FOR writing the plan itself (kanmer-plan) or implementing the change (kanmer-execute).
---

# Researching a Kanmer ticket

Research happens in **Preparing**: you change no code and need no branch or
worktree. Its output is the documents in the ticket's folder, written with
`set_ticket_doc` — they are what the plan is later built FROM, so their job is
to make planning boring.

**`get_doc_gates <id>` is the authority on what this ticket needs**, and the
only one. Profiles mean the answer differs per ticket, and it is resolved from
the board at call time — not from `board.yml`, whose `profiles:` block is not
the effective set. Ask it rather than assuming a pipeline.

## Workflow

1. **Read the ticket.** `get_item` for the body (the What/Why is your research
   question) and `get_links` for related tickets — prior research on a linked
   ticket often answers half the question. If the ticket is in a group, read the
   group's `context.md` (`get_group_doc`): the constraint binding the batch is
   written once, there, and applies to this ticket too. Check `get_ticket_doc`
   for existing research; extend it (`append: true`) rather than overwrite.
   If the project declares sources, call `get_sources` with the ticket's area
   and labels, passing MCP/plugin host observations only when they are known.
   Use only declarations resolved as `available` research inputs; record their
   ids/URLs and fetch failures, explicitly record `unknown`/`unavailable`
   declarations as skipped, and never treat a declaration as authority or an
   installation request. Use `fetch_source` only for an available, declared
   llms.txt source.
2. **Leave Backlog.** For most profiles this needs a governing doc: link the
   FRD/PRD this ticket implements (`link_doc <id> docs/functional/frd/<slug>.md`),
   or — if it is genuinely still to be written — set `docs_todo` and hand off to
   `kanmer-docs`. `get_doc_gates` says whether your ticket's profile asks for it
   at all. Then `move_item <id> preparing` — one stage, because a move crosses
   at most one gated boundary and a jump is refused even when every document
   already exists.
3. **Investigate.** Read the code, run read-only commands, check docs and
   history. Chase the question the ticket actually asks. Keep provisional
   working notes with `append_scratch <id> research "<note>"` — scratch is the
   notepad, never a pipeline document.
4. **Write `research`** from `assets/research-template.md`: the question,
   findings each with their source, and what they imply for this ticket.
5. **Write `files`** from `assets/files-template.md`: the files/modules the
   change touches and the risk in each, ripple effects (callers, tests, docs,
   build artifacts), and what is deliberately out of scope. Add a second table
   of **context files** — what an implementer must read to avoid a trap, and
   what each one tells them.
6. **Write `open-questions`** for anything unresolved — the questions the plan
   must not silently assume. Ones only the user can answer go to the user
   **now**, not at planning time. New work that does not belong here becomes its
   own ticket (`kanmer-tickets`), linked with `[[ID]]` or `rel: "blocks"` if it
   must land first.

Documents are folders, not single files: `research/` may hold several markdown
files and any of them satisfies the requirement. Deep research that runs long
belongs in several named files under `research/` rather than one sprawling one.
Files under `reference/`, `scratch/` and `assets/` never satisfy a gate.

Re-run `get_doc_gates <id>` when you think you are finished; it is the authority
on whether the ticket is ready for `kanmer-plan`, including on the questions —
`questions-resolved` is unmet while `open-questions` holds an unticked `- [ ]`
above `## Parked (explicitly deferred)`. Write each question as its own `- [ ]`
so it can be answered and ticked one at a time.

---

**Hand off to `kanmer-plan`**, which writes the plan and checklist from what you
just wrote — the ticket stays in Preparing across both, because research and
planning share that stage. A `spike` may be finished here instead: ask
`get_doc_gates`, which knows what this ticket's profile actually owes.
