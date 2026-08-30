---
name: kanmer-plan
description: Plan a Kanmer ticket — turn the inputs its live gates require into a concrete plan and executable checklist. Use when the user says "plan", "design the approach for" or "break down" a ticket. DO NOT USE FOR research itself (kanmer-research handles a required or materially missing input) or for implementing the plan (kanmer-execute).
---

# Planning a Kanmer ticket

A plan is only as good as the evidence it actually needs. It is written from
the inputs the live gate report requires, plus existing relevant evidence; it
does not create research or files merely for ceremony.

**`get_doc_gates <id>` is the authority on what this ticket needs**, and it is
the only one — a profile's requirements are resolved per board and change
without this file changing. Some profiles ask for no plan at all. Ask rather
than assuming, and do not reason from `board.yml`: requirements are injected at
resolve time, so its `profiles:` block is not the effective set.

## Workflow

1. **Route from live gates, then inspect inputs.** Call `get_item`, `get_links`,
   and group context where applicable, then `get_doc_gates <id>`. Inspect the
   requirements on the next relevant boundary before deciding which documents
   to read. Fetch required documents and any existing evidence relevant to the
   ticket. Create or refresh `research`/`files` only when that live boundary
   requires them, or when you name a concrete **material hole**: unresolved
   evidence or decision, or uncertainty about the exact affected files or
   contract that would otherwise make the ordered plan speculative. Generic
   usefulness or a desire for completeness is not a material hole. When no
   such hole exists, proceed directly to the required planning deliverable.
   When the project declares sources, resolve them with `get_sources` for the
   ticket's area/labels and cite applicable source ids/URLs in the plan or
   research inputs. A declared MCP/plugin is only a preference for an already
   connected/installed source; consult it only when resolution says
   `available`, and record `unknown`/`unavailable` entries as skipped. A
   declared llms.txt is bounded documentation, not authority. Do not add
   installation, authentication, auto-trust, or an unbounded crawl to a plan.
2. **Select optional work-type overlays.** After the ticket evidence is clear,
   manually copy zero or more matching prompt sets into the brief:
   `assets/brief-fix.md`, `assets/brief-ui-ux.md`, `assets/brief-docs.md`,
   `assets/brief-cloud-infra.md`, and `assets/brief-data-migration.md`. They
   supplement the shared plan and checklist; choose none when they add no
   value, and combine them when work crosses domains. They are templates, never
   an automatic classifier, ticket field, profile mapping, or gate.
3. **The ticket is in Preparing.** Research and planning share that stage in the
   six-stage board, so there is no move between them — the move you are working
   towards is Preparing → Implementing.
4. **Write `plan.md`** from `assets/plan-template.md`, the bounded execution
   brief: Objective, Starting state, Required changes, Expected files, Do not
   modify, Constraints, Ordered steps, Acceptance checks, Commands, Failure and
   deviation rules, and its exact `## Stop condition`. It **must** carry a **Governing docs**
   section — how the plan meets each linked PRD/FRD/ADR (`refs`), or, *only with
   explicit user authorization*, how it modifies one, or why a new ADR is being
   written. Design decisions become **ADRs** via `kanmer-docs`, linked into
   `refs`. (Gates check a doc exists; this content rule is enforced here and
   checked by `kanmer-review`.)
5. **Resolve planner decisions before dispatch.** In Required changes, words
   such as `investigate`, `decide`, `choose`, or `determine` are an advisory
   warning that planning remains: resolve it or use a spike. This is not a hard
   gate. For user-visible, contested, or grouped work, derive a compact approval
   paragraph from `assets/approval-contract.md`; the 300–600-word asset is guidance,
   not a required document type.
6. **Distill `checklist.md`** from `assets/checklist-template.md`: one `- [ ]`
   box per plan step, ending with the verification the post-implementation
   report will summarise. Each box must be independently checkable — "wire the
   retry call", not "do the backend".
   `[pre-review]` and `[post-merge]` labels are advisory human/skill text; gates
   ignore them, so `get_doc_gates` remains authoritative.
7. **Sanity-check scope.** If the plan grew beyond one unit of work, split it:
   file the extra tickets (`kanmer-tickets`), link with `rel: "blocks"` where
   order matters, and shrink this plan back to its ticket.
8. **For user-visible or contested work, ask for approval before implementation
   starts.** Show a short paragraph — intended outcome, in/out of scope, key
   decision or risk, and the exact approval boundary — rather than pasting the
   whole plan. This is human-facing guidance, not an invented core gate.
9. **Put the open questions to the user, then revise the plan around the
   answers.** This is the moment for it: research surfaced them, the plan is
   what would otherwise silently assume one. Ask them together, each with a
   recommendation, and record the answer in `open-questions` — a question
   answered in chat and not written down is a question nobody can find later.
   Take trivial defaults rather than asking; say in the document that you took
   them.

When the documents required by the live report exist, any needed approval has
arrived, and no question remains, `get_doc_gates <id>` shows the Preparing →
Implementing boundary passable and the ticket is ready for `kanmer-execute`.
If it still reports `questions-resolved` unmet, step 9 is not finished:
`open-questions` has unticked `- [ ]` lines. Answer them and tick, or move them
under `## Parked (explicitly deferred)` with a reason for deferring.

A move may cross **one** gated boundary at a time, so do not try to jump a
planned ticket further than Implementing — the move is refused, and the refusal
names the next stage.

---

**Hand off to `kanmer-execute`** when the request/context needs no further human
approval and questions are resolved; it takes the ticket into a worktree and
works the checklist. If planning turned up a question only the user can answer,
or needs approval, hand off to *them* first — do not make implementation guess.
