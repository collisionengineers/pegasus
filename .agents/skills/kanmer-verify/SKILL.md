---
name: kanmer-verify
description: Verify a merged ticket on main and write proof.md — the Verifying→Done gate. Use after a PR merges and the ticket is in Verifying to run the real checks (build/tests/manual), capture evidence, then move to Done. DO NOT USE for pre-merge review (kanmer-review) or git cleanup (kanmer-closeout).
---

# Kanmer verify

The **Verifying** stage validates the *shipped* behaviour on merged `main`, not
the feature branch. `proof.md` is the hard gate into Done — symmetric with the
other phase skills.

## When
A ticket reaches Verifying after its PR has been reviewed and **merged** (the
merge point owned by kanmer-review). You validate what actually landed.

## Workflow
1. `get_item <id>` and `get_doc_gates <id>` — confirm the ticket is in Verifying
   and see the remaining gate (`proof.md` before Done).
2. Check out merged `main` (not the feature branch) and pull. **Do this in the
   main checkout or the ticket's own worktree — never in `.worktrees/kanmer`.**
   In a repo set up through the GUI the board lives there on the board branch
   and MCP is rooted in it; checking main out over it takes the board offline
   mid-verification. This is the one step in the roster that switches branches,
   so it is the one that has to say which checkout it means.
3. Run the real evidence: the build, the test suites named in `plan.md` /
   `checklist.md`, and any manual/GUI checks. Record the exact commands and
   their output.
4. `set_ticket_doc <id> proof "<evidence>"` — commands run, results (e.g.
   "vitest 104/104 green"), and screenshots for UI work (markdown embedding
   images under the ticket folder's `assets/`). This is verification of the
   merged result, not a promise.
5. `append_scratch <id> verify "<notes>"` for anything provisional or in-progress.
6. `move_item <id> done` — the proof gate now passes. If it fails, the evidence
   is missing or the shipped result regressed: fix (new ticket) or re-open.

Hand off to **kanmer-closeout** for git cleanup and recording
`commits` / `prs` / `deployment` on the ticket.
