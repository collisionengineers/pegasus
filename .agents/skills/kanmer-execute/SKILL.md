---
name: kanmer-execute
description: Implement a Kanmer ticket — take it in its own git worktree and branch, work through its checklist with live progress notes, write proof.md, and open the PR. Use when the user says "work on", "implement", "take" or "build" a ticket, or when a planned ticket is ready for implementation. DO NOT USE FOR planning (kanmer-plan — required first), reviewing the result (kanmer-review), or post-merge cleanup (kanmer-closeout).
---

# Executing a Kanmer ticket

Execution happens in isolation: every ticket gets its own branch and its own
git worktree, so the main checkout stays clean and parallel work never
collides. `take_ticket` records both on the ticket — the human's board shows
the ⛏ badge with the branch, which is how they know the work is live.

## Preconditions

`get_ticket_doc` for `plan` and `checklist`. If either is missing, the
ticket isn't ready — do the `kanmer-plan` skill's job first. If the ticket
is already taken (`get_item` shows `taken`), coordinate with whoever holds
it rather than passing `force`.

## Start: worktree, branch, take

Conventions: branch `<ticket-id-lowercase>-<short-slug>` (e.g.
`api-003-retry-upload`), worktree `.worktrees/<ticket-id-lowercase>` at the
repo root. The ticket id in both makes board ↔ git greppable in either
direction.

```sh
git fetch origin
git worktree add .worktrees/<id> -b <id>-<slug> origin/main
cd .worktrees/<id>
```

(If the default branch isn't `main`, `git symbolic-ref --short
refs/remotes/origin/HEAD` names it.) Make sure `.worktrees/` is listed in
`.gitignore` — add the line if it isn't, once, committed.

Then take the ticket with **exactly what you just created**, not intentions:

```
take_ticket id: <ID>, branch: "<id>-<slug>", worktree: ".worktrees/<id>"
```

`take_ticket` moves the ticket to the working stage (default
`implementing`).

## Work the checklist

- Tick boxes in checklist.md with `set_ticket_doc` as you complete them —
  the GUI renders them as live checkboxes, so this is your progress bar.
- Add discoveries, gotchas and course-corrections as progress notes with
  `set_ticket_doc(doc: "checklist", append: true)` — never resend a whole
  document just to add a line.
- Commit as you go on the ticket's branch; small commits with real messages.
- If the plan turns out wrong, stop and fix plan.md (and the checklist)
  before coding around it — the documents are the shared truth, not your
  memory.

## Finish: report, PR, review stage

1. **Write `post-implementation-report.md`** from
   `assets/post-implementation-report-template.md`: every file change + its
   rationale, how the work meets each linked governing doc (`refs`), risks and
   follow-ups, and what `kanmer-verify` should run on merged `main`. This is the
   reviewers' brief and the board's **Implementing → Review** gate — call
   `get_doc_gates` to confirm. (`proof.md` comes later, from `kanmer-verify`, on
   merged main — not here.)
2. **Record traceability:** `update_item <id> commits: [...] prs: [...]`, and
   `link_doc` any governing doc, so the ticket ties back to the code.
3. **Push and open the PR** from `assets/pr-template.md` (the PR *description*,
   distinct from `kanmer-review`'s 4 review docs) — What/Why from the ticket
   body, the ticked checklist as the change list, and `Kanmer: <ID>` in the
   footer so the PR maps back to the ticket:

   ```sh
   git push -u origin <id>-<slug>
   gh pr create --title "<ticket title> (<ID>)" --body-file <assembled>
   ```

4. **Move the ticket to the review stage** (`move_item`, resolve the id against
   `list_board`) — the post-implementation-report gate must pass first. Record
   the PR URL with `append_scratch <id> execute "opened PR <url>"`. The ticket
   stays taken until the PR is merged, verified (`kanmer-verify`), and closed
   out (`kanmer-closeout`).

## Pausing instead of finishing

If you stop before the PR merges and someone else may pick the ticket up:
first append the resume point to the progress notes ("paused; resume in
`.worktrees/api-003` on `api-003-retry-upload`") — release erases the
`branch`/`worktree` fields — then `take_ticket action: "release"`. Keep the
worktree and branch; they are the resume point. A ticket left taken looks
in-progress to everyone.
