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
   current ticket timestamp, `review_round`, `remediation_budget`, PR diff,
   current head SHA, checks, reviews, comments, and unresolved threads. Read
   the pushed board tip (`get_status.boardSync`) for `board_sha`.
3. Settle the expected reviewers: every identity in `expected_reviewers` has
   posted on the exact head, or is recorded as timeout-absent. Do not write a
   final attestation before that.
4. Compare the implementation and report to the plan and governing docs. Check
   scope, production callers, tests, runtime artefacts, and every packet
   acceptance claim. Round 0 is the consolidated review of the whole PR; a
   round after a needs-changes return is a delta review (scope below).
5. Replace `scratch/review.md` as one version-aware file with the exact
   attestation schema, mapping every review thread on the head to an `F-###`
   finding. Never use `append_scratch` for this record.
6. On `needs-changes` with in-scope blocker/major findings, return the same
   ticket to Implementing: `move_item <id> implementing reason: "..."`. The
   branch, worktree, PR and claim stay as they are. Stop for `kanmer-execute`.
7. On `pass`, re-gather the PR immediately before a merge decision. Only an
   independent pass with all required checks green and no open blocker may
   merge.
8. With explicit user or standing delegation, merge the PR, re-read gates, and
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

## Expected reviewers and the settle rule

`expected_reviewers` is the set of **independent reviewer identities named for
this ticket** — the subagent reviewer(s) the controller dispatched or the
operator assigned, written as stable identities (the same strings they use as
`reviewer`). It is not the list of GitHub accounts or bots that happen to
comment. Codex, GitHub code-review bots and similar automated commenters are
**never** expected reviewers and never a gate: they are not part of this
workflow, and their absence blocks nothing. When such a thread does exist on
the reviewed head it is ordinary evidence and is dispositioned like any other
thread.

The attestation is authoritative only once the set has **settled** on the
exact `head_sha`:

- every expected reviewer has posted its findings (a review, a thread, or its
  own attestation) on that head; or
- a reviewer that has not posted by the agreed deadline is listed in the body
  as `timeout-absent: <identity> — <deadline>`; the controller or operator, not
  the reviewer, decides that deadline.

Until then, do not write a `pass` or `needs-changes` attestation for that
head; record the wait in ordinary review scratch. If any expected reviewer, or
any thread at all, posts on the **same head** after the attestation was
written, the attestation is no longer authoritative: re-gather and replace it
with a fresh whole-file record — replaced, never appended. A thread that was
gathered before it existed is exactly the failure this rule prevents.

`threads_snapshot` is the list of every review thread on the head at gather
time, one entry per thread:

```yaml
threads_snapshot:
  - source: github            # github | attestation | manual
    id: "PRRT_kwDOA…"         # the external thread/review id, verbatim
    author: "<who posted>"
    resolved: false
    finding: F-003            # the F-id this thread maps to; never blank
```

Every external thread id maps to an `F-###` finding id — one thread may map
to an existing finding, several threads may share one, but a thread without a
finding is an undispositioned thread and the attestation is invalid. Finding
ids are Kanmer ids; a raw GitHub id is never a finding id.

## Consolidated review, remediation batch, delta review

A ticket gets **one consolidated review, one in-scope remediation batch, and
one delta review**. `review_round` on the ticket counts the returns already
made; `remediation_budget` (default 1) is how many are allowed before an
operator must intervene.

- `review_round` = 0: the consolidated review covers the whole PR — diff,
  packet, plan, governing docs, checks and threads. Report every finding you
  will ever raise on this PR here; a finding first raised in a later round
  needs a written reason (changed lines, new evidence, a reviewer that settled
  late).
- `review_round` ≥ 1: the delta review is limited to the original findings,
  the lines changed since the previously attested `head_sha`, their direct
  callers and contracts, and the relevant tests. It does not re-open an
  unrestricted audit of unchanged code. Carry forward the previous findings
  with their new dispositions (`fixed`, or still `open` with why).

Only these block a merge: an open blocker or major finding, a failed or
missing required check, a stale review (head, plan, ticket timestamp or
threads moved), unmet acceptance checks, or an unresolved security,
data-loss or destructive risk. Dispositioned minor and note findings are
residual risk, recorded in the body, and do not block; an `open` minor or note
is not dispositioned and cannot pass.

Normalize external labels by their actual consequence: map P1 to blocker or major.
Map P2 to minor unless live evidence shows that it invalidates approved
acceptance, causes security or data loss, breaks a supported production path,
or prevents required verification. An external label never overrides the live
evidence.

### Root-cause classification

Two or more findings arising from one underlying mechanism are **one root-cause
class**. Record the class once and choose exactly one remedy for it: replace the
implementation approach, revise the plan, narrow the approved contract with a
stated threat model, or defer the whole class to one follow-up ticket. Never one
patch, and never one ticket, per example. Repeated grammar variants against a
hand-written parser, repeated path-normalization aliases, and repeated missing
registrations from one duplicated composition rule are each a single class,
however many examples the diff exposes.

A GitHub thread that GitHub marks **outdated** — a thread on a line the fix has
since changed — is dispositioned `obsolete-after-change` with a reason naming
the superseding commit, `superseded by <full-sha>`. It is never a current open
finding. The thread and its history are preserved, and a reviewer that reasserts
the same defect against the current head raises it as a new finding with current
evidence.

**What consumes no remediation budget:** re-auditing an unchanged head, a
restated finding, an outdated thread, an automated bot thread (see "Expected
reviewers and the settle rule" above rather than restating it), a disposition
edit, PR metadata that changes no code, and a new minor or note finding. That is
the deliberate property of `backwardMoveEffects` in `store.ts`: `review_round`
advances only when a `move_item` actually returns the ticket to Implementing.
Three audits of one head and one finding are one observed condition, not three
remediation failures.

### Batch PRs

A frozen batch receives one fresh independent review of the shared PR at its
exact full head SHA, not one repeated review per ticket. Before recording a
pass, call `list_items include_archived: true` and read the authoritative
manifest projection from the summaries: `batch.state` must be `active`,
`batch.members` is the complete frozen roster, and `batch.workspace` plus
`batch.branch` identify the one shared Git path. Do not derive membership from
only currently taken tickets or matching owner labels. A pending,
inconsistent, or missing projection is a stop. Confirm every projected member
is in Review, names that exact PR in `prs[]`, and is bound to the same head.
Also validate the live PR itself: its base and head repositories are the
resolved source repository, its base is the configured `delivery.prTarget`,
its head branch is `batch.branch`, its full head SHA is the pushed shared head,
and its standalone `Kanmer: <ID>` footers equal `batch.members` exactly.
Treat a dependency whose blocker is another member of that exact manifest
roster as internal ordering already covered by the shared PR. An external or
dangling blocker remains a stop, and the ordinary single-ticket rule is
unchanged.

Then write a separate, member-owned whole-file `scratch/review.md` attestation
for every member in the complete frozen roster. Every record must truthfully
say `independent: true` and `verdict: pass`, use the exact shared `pr` and full
`head_sha`, and carry that member's own `plan_hash`, `ticket_updated`, thread
mapping, findings, and dispositions. One shared attestation, a leader-only
record, or a record copied with another member's versions is not batch proof.
The protected merge gate accepts the batch only when the complete roster has
these valid exact-PR, exact-head attestations.

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
board_sha: "<full SHA of the pushed board branch tip you reviewed against>"
expected_reviewers: []
threads_snapshot: []
findings: []
```

`verdict` is exactly `pass` or `needs-changes`. `board_sha` is the full commit
id of the board branch tip (`get_status.boardSync.localSha`, with `ahead` at 0)
after the board has been pushed; the CI gate checks it is on the remote board
and reports `SYNC_REQUIRED` when the attestation names a board the remote has
never seen — push the board first, then write it. The gate reads the **remote**
board tip and does not re-run when the board is pushed, so confirm the push
before you treat any gate result as current — on a server that does not report
`boardSync`, compare the board worktree's board-branch tip with its remote
counterpart using absolute paths. A gate that passes while recording that no
review attestation exists is that same stale board, not a verdict you may rely
on. `expected_reviewers` is the
settled set from the section above and `threads_snapshot` the thread list
mapped to findings; this skill always writes both (an empty list is a truthful
value when the controller named no reviewer or the head has no threads), and
a present but malformed value invalidates the record. `independent` is true
only for an actual independent reviewer. Every finding has a stable id, severity
(`blocker | major | minor | note`), non-empty summary, and a disposition:
`open | fixed | rejected-with-reason | accepted-risk | deferred-to-ticket |
obsolete-after-change`.
`rejected-with-reason`, `accepted-risk` and `obsolete-after-change` require a
reason, and for `obsolete-after-change` that reason names the superseding
commit (`superseded by <full-sha>`); a
`deferred-to-ticket` finding requires the linked ticket id. The Markdown body
must explain changes, acceptance checks, findings, dispositions, and residual
risk, while frontmatter remains the machine-facing authority.

Every comment gets a disposition. Fix it in the PR, reject it with a reason,
accept the named risk, or file and link a blocking ticket. Do not silently
drop a review thread. Branch protection that sets
`required_conversation_resolution` holds the PR at a blocked merge state until
every thread is resolved, however green the checks and whatever the approval
count, so **dispositioning a finding and resolving its thread are one
obligation** — yours, not the author's or the controller's. Post the disposition
publicly on the PR first, so the record survives outside the board, and resolve
the thread only after that. A review that disposes every finding and resolves no
thread leaves a PR that cannot merge. If changes are needed, write `needs-changes` and take
the sanctioned return below; do not merge. A stale head, stale plan version,
changed ticket timestamp, new thread on the same head, unresolved
blocker/major finding, or changed required checks requires a fresh gather and
replacement attestation. Review does not silently implement a new scope: send
an authorized change back through execute on the current PR with its plan
alignment intact.

## The sanctioned needs-changes return

A `needs-changes` attestation bound to the current head is the only agent-side
authority to move a ticket from Review back to Implementing. The store enforces
it: `move_item` refuses `REVIEW_RETURN_NEEDS_ATTESTATION` unless
`scratch/review.md` is a valid `needs-changes` attestation whose `pr` matches
an entry in the ticket's `prs[]` (or the reason begins `operator:`). So, in
this order:

1. Confirm the PR is recorded on the ticket (`get_item` → `prs`); if the
   author omitted it, add it with `update_item prs` — it is the same PR, not a
   new fact.
2. Write the `needs-changes` attestation for the exact head with every
   in-scope blocker/major finding `open`.
3. Move one gated boundary backwards, from `review` to `implementing`, with a
   reason that quotes the blocking finding ids:

   ```
   move_item id: <ID>, status: "implementing", reason: "needs-changes on <head_sha>: F-001, F-004"
   ```

   The reviewer may make this call, or the controller that dispatched it; the
   author never does. The move is audited under `## Transitions` in
   `scratch/execution.md` and increments `review_round`. The ticket keeps its
   branch, worktree, PR and claim: remediation happens on the **same PR**
   through `kanmer-execute`'s re-entry lane, never on a second PR or a new
   ticket.
4. If the move refuses `REMEDIATION_BUDGET_EXHAUSTED`, the budget is spent
   (`review_round` already equals `remediation_budget`). Do not retry, widen
   the attestation, or ask the author to open a new PR. Report the exact
   refusal; only an operator may re-open the loop with a reason beginning
   `operator:`, which also raises the budget.

Findings that are genuinely out of this ticket's packet scope are not a reason
to return it: disposition them `deferred-to-ticket` with a linked ticket and
judge the PR on its own scope.

## Decide and merge

`pass` requires all of the following:

- the reviewer is independent and the attestation says so truthfully;
- the expected reviewers have settled on this exact head and every thread on
  it is in `threads_snapshot` with a finding;
- the diff matches the bounded packet and post-implementation report;
- governing-doc obligations and the plan's acceptance checks are met;
- all required checks exist and are green; and
- every review finding/thread has a terminal disposition; no finding of any
  severity remains `open`.

Re-run the PR view, diff/head, check, and thread gather immediately before the
merge command. If anything moved, replace the attestation with the new head
and plan/ticket values.

Immediately before `gh pr merge`, re-check that the board branch is pushed —
the gate reads the remote board tip and does not re-run on a board push, so a
gate result from before the push is evidence about a board the remote never
saw:

```sh
git -C <absolute-path-to-board-worktree> rev-parse <board-branch>
git -C <absolute-repository-root> rev-parse origin/<board-branch>
```

The two must be equal, and `<board-branch>` is read from
`get_status.boardWorktree.expectedBranch` and never hardcoded — this mirrors
`kanmer-auto`'s "Push the board before trusting a gate" and fails for the same
reason a hardcoded `main` does. Thread resolution is enforced by GitHub branch
protection (`required_conversation_resolution`) and is **load-bearing**: a PR
whose findings are all dispositioned but whose threads are left unresolved sits
at a blocked merge state however green its checks, and `enforce_admins` leaves
no bypass to merge it anyway.

With authorization, merge through GitHub only after
that final pass:

```sh
gh pr merge <pr> --squash --delete-branch=false
```

If the merge fails or GitHub reports a non-merged state, preserve the exact
failure, leave the ticket in Review, and do not move it. After a confirmed
ordinary merge, call `get_doc_gates` and move exactly one gated boundary, one
stage, `review` → `verifying`.

After a confirmed shared batch merge, re-read `list_items include_archived:
true` and require the active manifest projection to retain the exact roster,
PR branch and workspace just reviewed. Process `batch.members` in immutable
manifest order. Re-read each member immediately before acting. If the member
is in Review, call its `get_doc_gates` and move exactly Review → Verifying with
its current `expected_updated`. If it is already Verifying, that is the
idempotent no-op for an interrupted prior scan. Any other stage, a changed
manifest, or a refused move is a stop; retain the exact member resume point and
do not skip ahead. Re-running the same ordered scan safely skips members it
already advanced. Finally re-read the complete roster and hand off to
verification only when every member is Verifying. Review must never write
proof; the merged SHA and per-member proof belong to `kanmer-verify`.

For both ordinary and batch merges, the merged SHA belongs to
`kanmer-verify`; do not write proof here.

Review feedback that needs implementation goes back through the sanctioned
return above on the same ticket and PR; it does not become a separate PR
Review ticket. Do not change an unticked open question while applying review
fixes without the owner's decision. The whole-file `scratch/review.md`
attestation is the current review record; deleted legacy `pr-*` assets are not
part of this workflow.

---

**Hand off to `kanmer-execute`** after a `needs-changes` return (the ticket is
in Implementing on its existing PR), or **hand off to `kanmer-verify`** only
after an independent pass, authorized merge, confirmed merged PR, and the
single Review → Verifying move. Verify owns exact-SHA evidence and the Done
decision; this skill never self-reviews or silently merges an author's own
work.
