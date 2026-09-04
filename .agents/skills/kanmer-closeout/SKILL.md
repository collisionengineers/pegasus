---
name: kanmer-closeout
description: Close out a verified Done ticket or an explicitly retired post-merge verification failure — finalize proof and traceability, clean its Git workspace, then release. For a batch, census the immutable roster and remove shared Git while its manifest remains linked before releasing every member. Use after kanmer-verify's success or terminal-retirement handoff. DO NOT USE FOR deciding whether the work is good, verifying it, or inventing a failure disposition.
---

# Closing out a Kanmer ticket

Closeout is what stops the two slow leaks: tickets that sit taken forever,
and worktrees/branches that outlive their PR. It runs in a fixed order —
**Kanmer record-keeping first, Git cleanup second, release last**. An isolated
ticket follows that order directly. A batch first completes every member's
record and warning-free all-terminal census, captures its immutable roster and
shared Git identity, and cleans that one worktree and branch while the manifest
remains authoritative. Only then does it release members idempotently. Proof
evidence stays available through the terminal check, and any interruption in
member release remains discoverable from the retained manifest.

Start by appending `assets/closeout-checklist.md` to the ticket's
checklist.md (`set_ticket_doc append: true`) so the human watches cleanup
progress live.

## 0. Gate: is the PR actually merged?

On any resumed or suspicious Review/Verifying ticket, call
`reconcile_ticket id: <ID>` as a dry run first and, only when it returns a
recommendation, apply that recommendation with `apply_reconciliation id: <ID>,
expected_revision: <the recommendation's revision>` before re-reading anything
by hand; the inspector never mutates, and
its typed evidence is the cheapest account of why the ticket is not already
where it belongs.

```sh
gh pr view <branch> --json state,mergedAt,url
```

Proceed only on `state: "MERGED"`. `OPEN` → not a closeout; stop and say so.
`CLOSED` without merge → the abandoned path below.

## 1. Kanmer half

The ticket is in exactly one accepted terminal shape:

- **verified success** — status Done, not archived, final proof result `PASS`,
  or `WAIVED_BY_OPERATOR` with the operator identity and reason in the proof
  body (the human disposition `kanmer-verify` describes; a waiver without
  those is not final); or
- **retired non-success** — status Verifying, archived, final proof result is
  not `PASS`, and the Outcome names the operator's irrecoverable/superseded
  reason plus a successor ticket or explicit no-successor disposition.

Reject every other shape. Closeout never decides that a failure is terminal,
archives it, moves it to Done, or changes its proof result. Those decisions and
evidence belong to `kanmer-verify` and the operator.

1. **Confirm `proof.md` is final** (`get_ticket_doc doc: "proof"`); append the PR
   URL and merge date if verify didn't.
2. **Record traceability** (`update_item` with `expected_updated`): `commits`
   (the merged SHAs), `prs` (the merged PR ref), and — if the board tracks
   deployment — set `deployment` (`n/a` for non-deployable work, `not-deployed`,
   or the environment it shipped to). CI auto-detection is out of scope; set it
   from what actually happened.
3. **Record the Outcome** in the ticket body's Outcome section: follow-up ticket
   ids and anything that shipped differently than planned. For retired
   non-success, preserve the operator, reason, proof result and successor or
   explicit no-successor disposition verbatim.

## 2. Git half

**The board's own worktree is not yours to remove.** In a repo set up through
the GUI the board lives in `.worktrees/kanmer` on the board branch, and MCP is
rooted there — every command in this section takes the **ticket's** worktree and
branch, the ones `take_ticket` recorded on the ticket. Read them off `get_item`
rather than globbing `.worktrees/*`; removing `.worktrees/kanmer` destroys the
checkout the board is being served from.

```sh
# never remove the directory you're standing in — return to the main checkout
cd "$(git worktree list --porcelain | head -1 | cut -c 10-)"

git worktree remove .worktrees/<id>
git branch -d <id>-<slug>       # -D only per the table below
git fetch --prune origin
git worktree prune
```

If the host repo doesn't auto-delete merged branches:
`git push origin --delete <id>-<slug>`.

**Batch members share one worktree and branch (FRD-030).** Before any batch
cleanup action, first call `list_items include_archived: true`. Use the
summaries' `batch.id` to discover every ticket with the exact same batch id;
never infer membership from a matching branch, worktree, or only the active
board. While an authoritative manifest is `active` or `releasing`,
`list_items include_archived: true` is the sole complete roster census: it
keeps projecting the manifest onto every member until manifest unlink — even
after a partial release has cleared all ticket-local batch fields.
`search_items` projects batch metadata only onto matching non-archived results
and is never a complete roster census. Read `batch.state`, the complete `batch.members`,
`batch.workspace`, and `batch.branch` from that projection. This is the fresh
closeout discovery path after any interruption; do not depend on remembering a
member id's old claim fields. Surface listing warnings and refuse a `pending`,
inconsistent, missing, or conflicting projection instead of guessing the
roster. Capture the immutable roster and shared Git path, finish the Kanmer
half for every member, and require every immutable-roster member to be terminal
— Done, or archived under the accepted retired non-success shape — before
cleanup starts.

After that all-terminal check, keep the manifest linked and do not release any
member yet. Using the captured `batch.workspace` and `batch.branch`, remove the
one shared worktree and delete the shared branch with the Git steps above while
the manifest remains the authoritative recovery record. If recovering an
already-`releasing` manifest, perform or confirm this cleanup before issuing
another member release. If any shared Git cleanup step fails, stop, retain the
manifest and member evidence, and do not call `take_ticket action: "release"`.
An unreadable, incomplete, or changed roster is likewise a stop rather than
permission to clean Git.

Only after that shared Git cleanup succeeds, a fresh closeout agent may call
`take_ticket action: "release"` for every roster member. Terminal batch release
is deliberately not actor-bound: it does not require the original MCP actor or
`controller_run`, because implementation ownership is over and closeout may be
recovering another process's interruption. Release is idempotent across the
manifest's `releasing` phase: after an interruption, repeat the remaining
releases until the final release unlinks the manifest. Git cleanup has already
succeeded, so that final unlink cannot make a live shared worktree or branch
undiscoverable. A release before the all-terminal check refuses `BATCH_ACTIVE`.

## 3. Release, last

`take_ticket action: "release"` — issued only once nothing is actually in
flight. Success stays Done; retired non-success stays archived in Verifying.
In both cases Git shows nothing left and the live board has no stale taken work.
For a batch, the warning-free census, final record checks, and shared Git
cleanup all happen while the manifest remains linked. Only afterward release
every member idempotently; an interrupted `active` or `releasing` pass remains
discoverable until the last release performs final unlink.

## Edge cases

| Case | Do this |
|---|---|
| **PR still `OPEN`** | Not a closeout. Stop, tell the user, touch nothing — the ticket stays taken in review. |
| **PR `CLOSED` without merge** | Never move to the final stage. Ask the user: rework (leave everything, move the ticket back a stage) or abandon (append why to checklist.md, then the abandoned-cleanup below). |
| **Abandoning: worktree dirty / branch unmerged** | Show the user `git -C .worktrees/<id> status --porcelain` and the unmerged commits before any `--force` / `-D`. Only after they confirm it's disposable: `git worktree remove --force`, `git branch -D`, release, then archive or re-stage the ticket per the user. |
| **`git worktree remove` refuses (dirty)** on a merged PR | That's the safety working. Commit-and-push or stash anything that matters to the branch first; `--force` only for confirmed-disposable output (build artifacts) after showing the user what's there. |
| **`git branch -d` refuses ("not fully merged")** | Expected after a squash- or rebase-merge — the PR's commits aren't ancestors of main. Because step 0 verified `MERGED`, `git branch -D` is safe **in that case only**. If merge state couldn't be verified (no `gh`, no network), don't `-D`; leave the branch and flag it. |
| **You're standing inside the worktree** | You can't delete the directory you're in (Windows holds the cwd handle). The `cd` to the main checkout is step one of the git half precisely for this. |
| **Pausing, not closing** (work will resume) | This is not closeout. Leave the ticket taken, retain its recorded branch and worktree, and use `kanmer-execute`'s pausing section to append the resume point. Do **not** release it: release clears the metadata the resume lane requires. |
| **Worktree recorded on the ticket but gone on disk** | `git worktree list`: registered but missing → `git worktree prune`; directory lingering but unregistered → plain `rm -rf` of the leftover dir. Either way continue normally — a missing worktree is just less to clean. |
| **`.worktrees/kanmer` shows up in `git worktree list`** | Not a leftover — that is the **board's** worktree, on the board branch, and MCP is rooted in it. Never `remove`, `prune` away, `rm -rf`, or `push --delete` it, and never `-D` the board branch. It is the one entry in that directory this skill does not own. |
| **Several tickets share one branch** | For a manifest-backed batch, follow the batch census and order above: complete every record, clean the shared Git path while the manifest is linked, then release the roster. For a legacy non-batch shared branch, do the Kanmer half per ticket and the Git half only when the **last** closes — `list_items` and check no other ticket's `taken.branch` matches first. |

---

**No successor — this is the end of the pipeline.** The ticket is either Done
with PASS (or an operator waiver) or archived in Verifying with an explicit
non-success disposition;
the worktree and branch are gone, and the ticket is released. If closeout produced
follow-up work, that is a **new ticket** via `kanmer-tickets`, starting again at
Backlog; it is not a continuation of this one. Control returns to whoever was
driving — often `kanmer-auto`, which counts this ticket as finished and moves to
the next in its roster.
