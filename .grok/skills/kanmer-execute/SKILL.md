---
name: kanmer-execute
description: Implement one Kanmer ticket from its read-only execution packet — take its recorded worktree and branch, work only the bounded checklist, write the post-implementation report, and open the PR. Use when the user says "work on", "implement", "take" or "build" a ticket, or when a planned ticket is ready for implementation. DO NOT USE FOR planning (kanmer-plan — required first), reviewing the result (kanmer-review), or post-merge cleanup (kanmer-closeout).
---

# Executing a Kanmer ticket

Execution is a bounded hand-off. The ticket's `get_execution_packet` response,
not an agent's memory or a separately reconstructed folder, is the input to
this skill. It supplies the project identity, ticket and group context,
profile-resolved gates, versioned plan/checklist/files documents, extra paths,
commands, and one explicit stop condition. Keep those values for the whole
run and stop when the packet says to stop.

## Workflow

1. Orient with `get_status`; inspect the project fingerprint and the server's
   `compat.expectedProject` capability.
2. Make `get_execution_packet <id>` the **first ticket-specific data call**.
   A refusal is a normal result, not an invitation to reconstruct the packet.
3. If `ready: false`, quote its exact `code`, `reason`, and `missing` values
   in scratch and stop. Do not call `get_item`, take the ticket, run Git, or
   write a document after that refusal.
4. Retain the ready packet and then create and validate the exact worktree and
   branch it requires. Send `expected_project` only when the preceding status
   call advertised `compat.expectedProject: "optional"`.
5. Take the ticket with the exact branch and worktree, work only the packet's
   files and checklist, and record progress with version-aware MCP writes.
6. Write the post-implementation report, record traceability, push the branch,
   and open a PR whose body contains `Kanmer: <ID>`.
7. Re-read `get_doc_gates`, then move only `implementing` → `review` when its
   requirements pass. Stop for an independent reviewer.

The ticket stays taken through review, verify, and closeout. This skill never
merges its own PR and never starts another ticket.

## Packet first and refusal

`get_status` is orientation, not ticket data. After it, call:

```
get_execution_packet id: <ID>
```

The packet is read-only and does not take, move, write, dispatch, or create a
worktree. It is ordered to refuse unsafe execution: a non-ticket/legacy item,
spike, unmet `leave-preparing` gate, unresolved questions, or an occupied
ticket. On any `{ready: false, code: "GATE_BLOCKED", ...}` response, quote the
exact refusal in the hand-off and stop before every other ticket, Git, or
document action. Do not turn `missing` into a guessed plan, run `kanmer-plan`
inside execute, or retry by passing `force`; hand the ticket back to the named
preparation phase or operator.

A ready packet contains the full ticket body, ordered group contexts, resolved
gates, and the versioned `plan`, `checklist`, and `files` index documents. It
also lists every extra Markdown path, an ATX `stopCondition`, and a command
hint. Treat those versions as optimistic concurrency tokens: read every listed
path and pass its version to a replacement. Do not silently overwrite a human
edit; re-read the packet and re-plan if a version conflict occurs.

## Project capability and worktree

Before the first mutating call, retain `project.fingerprint` from
`get_status`. If and only if the response advertises
`compat.expectedProject: "optional"`, pass that value as the top-level
`expected_project` on writes. Older servers do not accept the field, so omit it
when the capability is absent. It is never nested in ticket fields or packet
documents.

Create the worktree from the repository root, after the packet is ready:

```sh
git fetch origin
git worktree add .worktrees/<id-lowercase> -b <id>-<slug> origin/main
```

Validate that the target is exactly `.worktrees/<id-lowercase>`, is not the
board worktree `.worktrees/kanmer`, and is not another ticket's worktree. Do
not create, switch, push, or remove the board branch/worktree. Confirm that
`.worktrees/` is ignored by the repository before creating it; if that setup
condition is absent, report the deviation rather than hiding it in an
unrelated change. Take only after the path and branch exist, with exactly what
was created:

```
take_ticket id: <ID>, branch: "<id>-<slug>", worktree: ".worktrees/<id-lowercase>"
```

The ticket comes before the branch in the board record; never invent a branch
for an unrecorded ticket and never `force` a taken ticket.

## Work only the packet

- Work only the packet's `files` scope. Do not absorb another ticket, repair
  unrelated failures, or redesign the workflow.
- Tick checklist boxes with `set_ticket_doc` using the version returned by the
  packet/read. Use `append_scratch` for running notes only; preserve failed
  attempts and exact exits.
- If the plan, files map, or stop condition is contradicted, pause, record the
  deviation, and re-read or revise the governing packet documents before
  coding around them. A useful discovery is not authorization for a new file.
- Run the packet's named commands in the recorded worktree. A command that
  cannot run is `INCONCLUSIVE`, not a fabricated pass. Keep the first failure
  when a later retry succeeds.
- Preserve the applicable production-caller, runtime-artifact,
  schema/grant, and test-proof rules from the packet's templates and governing
  docs; a registered-but-unreachable or test-only implementation is not done.
- Stop at the packet's stop condition, even when a follow-up looks convenient.

## Finish: report, PR, Review

1. Write `post-implementation-report.md` as a whole document. List every file
   changed and why, map the result to the plan's governing docs, name risks and
   follow-ups, and tell `kanmer-verify` which checks belong on the merged
   result. `proof.md` is not an execution document and is written only after a
   review merge.
2. Record the reachable implementation commit(s) and PR with `update_item`.
   Link governing docs only when the packet authorizes the link; do not invent
   refs. Keep all writes project-bound when the capability was advertised.
3. Push the ticket branch and open the PR with the ticket title and
   `Kanmer: <ID>` footer:

   ```sh
   git push -u origin <id>-<slug>
   gh pr create --title "<ticket title> (<ID>)" --body-file <assembled-body>
   ```

4. Read `get_doc_gates <id>` immediately before `move_item`. Move one gated
   boundary only, from `implementing` to `review`, and record the PR URL in
   an ordinary execute scratch note. If the gate or move refuses, preserve the
   exact error and remain in the current stage.

The hand-off is the open PR plus the ticket in Review. The author does not
write the review attestation, review the PR, merge it, move it to Verifying, or
clean up the implementation worktree, or start another ticket.

## Pausing

If work must pause before review, append the exact resume point — branch,
worktree, packet version, and last command/result — to execute scratch. Release
only when another worker may safely resume; release clears the branch and
worktree fields, so retain the physical worktree and branch as the named resume
point. A refusal, missing dependency, or user-only decision is a stop, not a
reason to guess.

---

**Hand off to `kanmer-review`** once the PR is open and the ticket is in Review.
The author does not merge: review owns the independent attestation and merge
point, never starts another ticket, and this skill's last act is the Review
hand-off.
