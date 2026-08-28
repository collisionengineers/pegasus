---
name: kanmer-closeout
description: Close out a verified Kanmer ticket — confirm proof.md is final, record its commits/PRs and deployment status, then remove the worktree, delete the branch, and release, so nothing stale accumulates. Runs after kanmer-verify has validated the merged result and moved the ticket to Done. Use when the user says "close out <ID>", "wrap up this ticket", or "clean up the worktree/branch". DO NOT USE FOR deciding whether the work is good (kanmer-review), verifying on merged main (kanmer-verify), or tickets whose PR hasn't merged yet.
---

# Closing out a Kanmer ticket

Closeout is what stops the two slow leaks: tickets that sit taken forever,
and worktrees/branches that outlive their PR. It runs in a fixed order —
**kanmer record-keeping first, git cleanup second, release dead last** —
because proof evidence may still need the worktree alive, a git snag must
not strand the board, and a lingering ⛏ badge on a done ticket is a visible
prompt to finish cleanup, unlike a silently orphaned directory.

Start by appending `assets/closeout-checklist.md` to the ticket's
checklist.md (`set_ticket_doc append: true`) so the human watches cleanup
progress live.

## 0. Gate: is the PR actually merged?

```sh
gh pr view <branch> --json state,mergedAt,url
```

Proceed only on `state: "MERGED"`. `OPEN` → not a closeout; stop and say so.
`CLOSED` without merge → the abandoned path below.

## 1. Kanmer half

The ticket is already at **Done** — `kanmer-verify` validated the merged result
and wrote `proof.md` on merged main. Closeout is record-keeping, not a stage move.

1. **Confirm `proof.md` is final** (`get_ticket_doc doc: "proof"`); append the PR
   URL and merge date if verify didn't.
2. **Record traceability** (`update_item` with `expected_updated`): `commits`
   (the merged SHAs), `prs` (the merged PR ref), and — if the board tracks
   deployment — set `deployment` (`n/a` for non-deployable work, `not-deployed`,
   or the environment it shipped to). CI auto-detection is out of scope; set it
   from what actually happened.
3. **Record the Outcome** in the ticket body's Outcome section: follow-up ticket
   ids and anything that shipped differently than planned.

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

## 3. Release, last

`take_ticket action: "release"` — issued only once nothing is actually in
flight. Done: board shows the ticket finished, git shows nothing left.

## Edge cases

| Case | Do this |
|---|---|
| **PR still `OPEN`** | Not a closeout. Stop, tell the user, touch nothing — the ticket stays taken in review. |
| **PR `CLOSED` without merge** | Never move to the final stage. Ask the user: rework (leave everything, move the ticket back a stage) or abandon (append why to checklist.md, then the abandoned-cleanup below). |
| **Abandoning: worktree dirty / branch unmerged** | Show the user `git -C .worktrees/<id> status --porcelain` and the unmerged commits before any `--force` / `-D`. Only after they confirm it's disposable: `git worktree remove --force`, `git branch -D`, release, then archive or re-stage the ticket per the user. |
| **`git worktree remove` refuses (dirty)** on a merged PR | That's the safety working. Commit-and-push or stash anything that matters to the branch first; `--force` only for confirmed-disposable output (build artifacts) after showing the user what's there. |
| **`git branch -d` refuses ("not fully merged")** | Expected after a squash- or rebase-merge — the PR's commits aren't ancestors of main. Because step 0 verified `MERGED`, `git branch -D` is safe **in that case only**. If merge state couldn't be verified (no `gh`, no network), don't `-D`; leave the branch and flag it. |
| **You're standing inside the worktree** | You can't delete the directory you're in (Windows holds the cwd handle). The `cd` to the main checkout is step one of the git half precisely for this. |
| **Pausing, not closing** (work will resume) | Append the resume point (branch + worktree path) to checklist progress notes, release the ticket, and **keep** the worktree and branch — see `kanmer-execute`'s pausing section. |
| **Worktree recorded on the ticket but gone on disk** | `git worktree list`: registered but missing → `git worktree prune`; directory lingering but unregistered → plain `rm -rf` of the leftover dir. Either way continue normally — a missing worktree is just less to clean. |
| **`.worktrees/kanmer` shows up in `git worktree list`** | Not a leftover — that is the **board's** worktree, on the board branch, and MCP is rooted in it. Never `remove`, `prune` away, `rm -rf`, or `push --delete` it, and never `-D` the board branch. It is the one entry in that directory this skill does not own. |
| **Several tickets share one branch** | Do the kanmer half per ticket as each finishes; do the git half only when the **last** of them closes — `list_items` and check no other ticket's `taken.branch` matches first. |

---

**No successor — this is the end of the pipeline.** The ticket is Done, the
worktree and branch are gone, and the ticket is released. If closeout produced
follow-up work, that is a **new ticket** via `kanmer-tickets`, starting again at
Backlog; it is not a continuation of this one. Control returns to whoever was
driving — often `kanmer-auto`, which counts this ticket as finished and moves to
the next in its roster.
