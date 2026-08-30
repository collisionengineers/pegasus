---
name: kanmer-review
description: Independently review a Kanmer ticket's PR against its packet, plan, governing docs, current head SHA, checks, and review threads; write the versioned whole-file review attestation, disposition every finding, then merge only with authorization and move one stage to Verifying. Use when a ticket sits in Review or a PR needs review. DO NOT USE for verifying the merged result (kanmer-verify), post-merge cleanup (kanmer-closeout), or implementing changes (kanmer-execute). An author must not self-review or merge.
---

# Reviewing a Kanmer ticket

Review is an independent evidence decision, not an author's self-description.
The reviewer binds the decision to the current PR head, the plan document
version, the ticket revision, required checks, and every review thread. The
review record is a machine-facing `scratch/review.md` attestation; it is not a
running note. GitHub owns merge physics, while Kanmer owns the evidence trail
and the one-stage board transition.

## Workflow

1. Confirm the live ticket is in Review, identify the PR, and verify that you
   are not its author. A self-review may inspect and report but must set
   `independent: false`, must not claim a pass, and must not merge.
   Independence is a **distinct agent-role** boundary, not a distinct GitHub
   credential: a separately assigned reviewer may use the same repository
   account as the author. GitHub still decides whether that account can approve
   or merge under its own permissions, review, conversation, and merge-policy
   rules.
2. Gather the ticket, every packet document, governing refs, group context,
   current ticket timestamp, PR diff, current head SHA, checks, reviews,
   comments, and unresolved threads.
3. Compare the implementation and report to the plan and governing docs. Check
   scope, production callers, tests, runtime artefacts, and every packet
   acceptance claim.
4. Replace `scratch/review.md` as one version-aware file with the exact
   attestation schema. Never use `append_scratch` for this record.
5. Re-gather the PR immediately before a merge decision. Only an independent
   pass with all required checks green and no open blocker may merge.
6. With explicit user or standing delegation, merge the PR, re-read gates, and
   move only `review` → `verifying`. Stop for `kanmer-verify`.

## Gather the immutable review inputs

Use MCP first for the board and documents. Read the packet paths, plan and
post-implementation report, `refs`, and the first feature-group `context.md`
when present. Read `open-questions` before applying any fix. Record the
`get_ticket_doc(doc: "plan").version` as `plan_hash` and the ticket's current
`updated` timestamp as `ticket_updated`; these values bind the attestation.

Use GitHub's current PR identity, not a branch name that can move:

```sh
gh pr view <pr> --json url,state,title,headRefOid,reviewDecision,statusCheckRollup,reviews,comments
gh pr diff <pr>
```

`headRefOid` is the full SHA reviewed. Capture the full diff, review comments,
requested changes, and unresolved threads; use the GitHub GraphQL review-thread
surface when the JSON view does not expose whether a thread is resolved. A
required check that is red, pending, or absent is not a pass. A check that is
not required may still be recorded as evidence, but cannot be presented as a
required green gate.

Do not inspect a PR by checking a branch out over `.worktrees/kanmer`. That is
the board worktree and remains on its board branch. If the ticket's
implementation worktree is useful for context, keep it read-only and do not
switch it to `main`.

## The whole-file review attestation

Read `get_ticket_doc(id: <ID>, doc: "scratch/review")` first. Replace it with
`set_ticket_doc`, passing the returned `version` as `expected_version`; do not
append to it and do not overwrite a concurrent review. The frontmatter is
exactly:

```yaml
kind: review-attestation
pr: "123"
head_sha: "<full reviewed PR head SHA>"
verdict: pass
reviewer: "<stable reviewer identity>"
independent: true
plan_hash: "<get_ticket_doc(doc: \"plan\").version>"
ticket_updated: "<ticket updated timestamp read for review>"
findings: []
```

`verdict` is exactly `pass` or `needs-changes`. `independent` is true only for
an actual independent reviewer. Every finding has a stable id, severity
(`blocker | major | minor | note`), non-empty summary, and a disposition:
`open | fixed | rejected-with-reason | accepted-risk | deferred-to-ticket`.
`rejected-with-reason` and `accepted-risk` require a reason; a
`deferred-to-ticket` finding requires the linked ticket id. The Markdown body
must explain changes, acceptance checks, findings, dispositions, and residual
risk, while frontmatter remains the machine-facing authority.

Every comment gets a disposition. Fix it in the PR, reject it with a reason,
accept the named risk, or file and link a blocking ticket. Do not silently
drop a review thread. If changes are needed, write `needs-changes`, leave the
ticket in Review, and do not merge. A stale head, stale plan version, changed
ticket timestamp, unresolved blocker/major finding, or changed required checks
requires a fresh gather and replacement attestation. Review does not silently
implement a new scope: send an authorized change back through execute/current
PR with its plan alignment intact.

## Decide and merge

`pass` requires all of the following:

- the reviewer is independent and the attestation says so truthfully;
- the diff matches the bounded packet and post-implementation report;
- governing-doc obligations and the plan's acceptance checks are met;
- all required checks exist and are green; and
- every review finding/thread has a disposition with no open blocker or major.

Re-run the PR view, diff/head, check, and thread gather immediately before the
merge command. If anything moved, replace the attestation with the new head
and plan/ticket values. With authorization, merge through GitHub only after
that final pass:

```sh
gh pr merge <pr> --squash --delete-branch=false
```

If the merge fails or GitHub reports a non-merged state, preserve the exact
failure, leave the ticket in Review, and do not move it. After a confirmed
merge, call `get_doc_gates` and move exactly one gated boundary, one stage,
`review` → `verifying`.
The merged SHA belongs to `kanmer-verify`; do not write proof here.

Review feedback that needs implementation becomes a linked PR Review ticket;
the original remains blocked until it is dispositioned and re-reviewed. Do not
change an unticked open question while applying review fixes without the
owner's decision. The whole-file `scratch/review.md` attestation is the current
review record; deleted legacy `pr-*` assets are not part of this workflow.

---

**Hand off to `kanmer-verify`** only after an independent pass, authorized
merge, confirmed merged PR, and the single Review → Verifying move. Verify
owns exact-SHA evidence and the Done decision; this skill never self-reviews
or silently merges an author's own work.
