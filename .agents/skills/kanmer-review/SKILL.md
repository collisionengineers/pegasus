---
name: kanmer-review
description: Review a Kanmer ticket's PR — write the 4-doc review set, check the post-implementation report and the plan's Governing-docs section against the diff, turn feedback into blocking tickets, then merge and move the ticket to Verifying. Use when the user says "review" a ticket or PR, when a ticket sits in the review stage, or when PR review comments arrive and need tracking. DO NOT USE FOR verifying the merged result (kanmer-verify), post-merge cleanup (kanmer-closeout), or implementing changes (kanmer-execute).
---

# Reviewing a Kanmer ticket

Review is where the claim ("done — see the post-implementation report") meets the
evidence. The reviewer checks that meeting actually happens, then owns the **merge
point**: a passing review merges the PR and moves the ticket to **Verifying**,
where `kanmer-verify` validates the shipped result on merged main. Review is the
sole owner of PR-feedback → tickets (`kanmer-import` delegates PR comments here).

## Gather

`get_item` for the ticket, `get_ticket_doc` for `plan` and
`post-implementation-report`, its `refs` (the governing docs the plan must meet),
and the PR itself (`gh pr view <branch> --json url,state,title`, then
`gh pr diff <branch>`; the PR URL is in the ticket's scratch/notes and the id is
in the PR body's `Kanmer:` footer).

## Write the 4-doc review set

Into the ticket folder with `set_ticket_doc` (per-area doc set on a PR-review
board; see `get_doc_gates`):

1. **`pr-changes-summary`** — what the diff actually changes, file by file, in
   the reviewer's own words (not the author's report).
2. **`pr-comments`** — every point raised, each tagged blocking / non-blocking.
3. **`pr-comment-disposition`** — for each comment: fixed-in-PR, filed-as-ticket,
   or won't-do-because. Nothing said in review silently evaporates.
4. **`pr-review`** — the verdict and what was checked.

## Check

1. **Report against diff.** Does `post-implementation-report.md` list every file
   change with an honest rationale, and match what the diff does?
2. **Governing docs.** Does the plan's **Governing docs** section hold against the
   change — each linked PRD/FRD/ADR met, any modification actually authorized,
   any new ADR actually written?
3. **The code** — correctness, tests, and whether impact.md's ripple effects
   (callers, docs, build artifacts) were followed up. Unplanned extras belong in
   their own tickets, not smuggled in.

## Outcomes

- **Passes** — record it in `pr-review`, then (with the user's go-ahead, or their
  standing delegation) **merge the PR** (`gh pr merge`), and `move_item <id>
  verifying`. Hand off to `kanmer-verify` to validate on merged main and write
  `proof.md`.
- **Needs changes** — file each substantive point as a ticket in the **PR Review**
  area (`kanmer-tickets`; `PR-` prefix), linked with `rel: "blocks"` so the
  original visibly can't close. Trivial nits go straight into the PR as review
  comments. The blocking tickets are worked by `kanmer-execute`; re-review when
  they land.

## Incoming PR feedback

A human's review comments on a PR you're tracking follow the same rule: each
substantive comment becomes a PR Review ticket blocking the original, captured in
`pr-comments` + `pr-comment-disposition`.
