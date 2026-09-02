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

1. Orient with `get_status`; inspect the project identity (`project.project_id`
   when `identity` is `logical`, else the legacy `project.fingerprint`) and the
   server's `compat.expectedProject` / `compat.expectedRevision` capabilities.
2. Make `get_execution_packet <id>` the **first ticket-specific data call**.
   For a known frozen batch, include the same nonempty `controller_run` from
   the controller's durable run record. A refusal is a normal result, not an
   invitation to reconstruct the packet or mint a new run identity.
3. If `ready: false`, return its exact `code`, `reason`, and `missing` values
   in the external hand-off and stop without mutating the ticket, except for the deliberately resumable occupancy case:
   when the refusal names an existing branch and worktree, retry this one call
   with those exact values in `resume`. If that retry refuses, stop. Do not
   call `get_item`, take the ticket, run Git, or write a document after any
   other refusal.
4. Retain the ready packet. If it reports `ticket.taken`, it is a resumed
   ticket: validate and reuse that exact recorded worktree and branch; do not
   create another worktree or call `take_ticket` to take it again — renew the
   lease instead (`take_ticket action: "renew"` with the packet's
   `claim.leaseId` / `claim.leaseRevision`). Otherwise acquire the packet's
   workspace: an untaken frozen batch member reuses `ticket.workspace`, while
   an isolated ticket creates and validates a fresh worktree before take. Send `expected_project` only when the
   preceding status call advertised `compat.expectedProject: "optional"`.
5. Work only the packet's files and checklist, and record progress with
   version-aware MCP writes. Renew the claim before any long command.
6. Write the post-implementation report, record traceability (commits and the
   PR in `prs[]`), push the branch, and open a PR whose body contains
   `Kanmer: <ID>` — or, on the re-entry lane after a needs-changes return,
   push to the branch behind the **existing** PR and never open a second one.
7. Re-read `get_doc_gates`, then move only `implementing` → `review` when its
   requirements pass. Stop for an independent reviewer.

The ticket stays taken through review, verify, and closeout. This skill never
merges its own PR and never starts another ticket.

## Packet first and refusal

`get_status` is orientation, not ticket data. After it, call:

```
get_execution_packet id: <ID>
```

For a ticket in a declared batch, the call is instead:

```
get_execution_packet id: <ID>, controller_run: "<durable-controller-run>"
```

Retain that nonempty `controller_run` in the controller's durable run record
across reconnects and restarts. Batch authority is the exact pair of the actual
MCP request actor observed by the server and that durable run id; an
`assignee`, `controller`, copied owner label, exact resume path, or a newly
minted per-call id is not authority. Omission or mismatch is a normal
`GATE_BLOCKED` refusal and is not a reason to reconstruct the packet.

The packet is read-only and does not take, move, write, dispatch, or create a
worktree. It is ordered to refuse unsafe execution: a non-ticket/legacy item,
spike, unmet `leave-preparing` gate, unresolved questions, an incomplete or
unsafe taken location, or an occupied ticket. On any `{ready: false,
code: "GATE_BLOCKED", ...}` response, return the
exact refusal in the external hand-off and stop before every ticket, Git, or
document action. The sole retry is an occupancy refusal that includes its
recorded branch and worktree: submit those two literal values as
`resume: { branch, worktree }` to the same call. That is a deliberate resume
confirmation, not a force flag; a changed or incomplete value is refused.
Do not turn `missing` into a guessed plan, run `kanmer-plan` inside execute,
or retry by passing `force`; hand every other refusal back to the named
preparation phase or operator.

A ready packet contains the full ticket body, ordered group contexts, resolved
gates, and the versioned `plan`, `checklist`, and `files` index documents. It
also lists every extra Markdown path, an ATX `stopCondition`, a command hint,
and non-blocking warnings about other tickets' stale locations. Treat those
versions as optimistic concurrency tokens: read every listed path and pass its
version to a replacement. Then discover and read every human-supplied file in
the ticket's `reference/` directory before editing — including non-Markdown
inputs deliberately omitted from `extraDocs`. Do not silently overwrite a
human edit; re-read the packet and re-plan if a version conflict occurs. A
warning never authorizes a repair outside this ticket; retain it for the
external hand-off.

`ticket.taken` selects the execution lane. A missing value means a fresh
lease, not necessarily a new Git worktree: when `claim.batch` is present,
`ticket.workspace` carries that manifest's immutable branch and portable
worktree and the member must take those exact values without creating another
worktree. In isolated mode, create the ticket worktree and then take it. A present value means
resume the exact already-recorded branch and worktree. It is not permission to
create another worktree, retake the ticket, clear its ownership, or replace its
uncommitted work.

### One bounded step at a time

A controller driving a constrained worker adds `step` to the same call —
`get_execution_packet id: <ID>, step: <n>` for a 1-based ordered step, or
`step: "next"` for the first step the checklist has not ticked. The response
gains a `step` block: a versioned packet naming the only files that step may
touch and any descriptive symbols, the files it must not, its exact tests, commands, expected
output, done condition, deviation stop, and a stop condition that ends the work
after that one step. Issuance requires at least one mapped unchecked checklist
marker for the selected ordered step; a plan-only or unrelated checklist
refuses normally while the whole-ticket setup packet remains available. Only
an exact level-three `### Step N — <title>` heading is a structured boundary:
declared numbers start at 1 and remain contiguous, while nested or explanatory
headings never become steps. Named checklist authority exists only when the
checkbox label begins with `Step N`; an explanatory prose mention of `step N`
never maps that checkbox to a step. Both
routes use one lexical, de-duplicated group census: counted ticket documents
plus unique group ids are capped at 256 before any group or context read, and a
missing or conflicting resolved identity refuses. Core binds the requested
ticket record, completes a canonical metadata census and preflights per-file
and aggregate byte bounds before opening ticket-document, group-record or
context content. It reads those bytes through identity-bound capped handles;
replacement, growth, symlink, special-file or hard-link evidence refuses.
Physical confinement is anchored at the configured project root: a junction at
that root is allowed, but any symlink or junction below it, including `.kanmer`
and ticket, document or group directories, refuses.
Scratch and reference documents stay revision-exempt but still consume the
inventory and aggregate bounds.
Execute exactly that step, then stop and report so the
controller can reconcile the actual changes before another packet is issued.
The controller, not the worker, retains the exact full `step-packet/2` object
inside the live dispatch/reconciliation chain before dispatch. `packetId` is
tamper-evident identity, not authentication: reconcile only the controller's
retained object, never a worker-returned or reconstructed packet. Do not
copy the full packet into an automation ledger. If a crash or reconnect loses
that retained object, record packet-loss as `INCONCLUSIVE`, issue no successor,
and do not rebuild authority from live state.

At the stop, call `reconcile_ticket` with that exact retained `step_packet`.
Kanmer derives the actual HEAD, index, worktree and pre-dirty deltas itself;
caller-supplied changed-path summaries are not proof. Missing, unreadable,
unstable, escaped, unconfined or unprovable linked, or hard-linked workspace
evidence is `INCONCLUSIVE`; a
forbidden or undeclared path is FAIL. The path matcher is iterative and
explicitly bounded; its shared budget is charged before raw path parsing and
before every literal or wildcard comparison. Exhaustion is `INCONCLUSIVE`,
never authorization or an undeclared-path claim. Dirty regular-file bytes are
read once through one capped handle whose pre-open, handle-before/after and
post-path device, inode, type, mode, link-count and size facts must agree; the
handle closes on every result. The only permitted ticket-document change is the
selected checklist marker from unchecked to checked: every other raw line body,
CRLF/CR/LF terminator, final-newline state, ticket authority field and counted
document remains bound. Exact checklist bytes retain a leading UTF-8 BOM.
Compilation and strict verification derive every marker state from those bytes,
require a completed prefix and unfinished selected step, and refuse any checked
successor marker.
Free-form symbol names are not mechanically provable from Git paths. Any
actual change while `allowedSymbols` is non-empty adds
`STEP_SYMBOL_SCOPE_INCONCLUSIVE`; forbidden or undeclared path FAIL takes
precedence. No-change invents no symbol finding, and empty symbols preserve
file-scoped PASS.
This Git evidence covers tracked, staged, unstaged and untracked paths plus both
rename endpoints. Changed-path evidence also includes one bounded complete union
of every path touched by every intervening commit, including paths later
reverted; a non-ancestor baseline or exhausted history is `INCONCLUSIVE`.
That history census validates both old and new modes from every intervening
tree edge; any intervening `120000` symbolic-link or `160000` Git-link mode
refuses even if a later commit restores a regular endpoint.
A packet workspace HEAD is a full 40- or 64-character Git
object ID. Every sample also hashes one bounded NUL
`git ls-files -v -s -z` index census, binding flag, mode, object id, stage and
path: assume-unchanged or skip-worktree entries refuse; nonzero stages and
gitlinks refuse without index mutation; census drift between samples is
`INCONCLUSIVE`. On filesystems that expose the owner-executable bit, every clean
tracked regular path must agree with its indexed `100644`/`100755` executable
class; disagreement refuses. A tracked mode-`120000` path is retained only when its checkout
representation and capped target bytes are identity-bound and its physical
target is an indexed tracked regular file inside the worktree; external,
chained-external, dangling, unreadable, unstable or over-budget links refuse.
Tracked-link target bytes retain a leading UTF-8 BOM.
Ignored or untracked link targets refuse.
Execution authority requires exactly one selected ticket endpoint across v2
areas and legacy v1 storage; duplicates refuse before either record is opened.
Ignored paths and `.git` / common-directory metadata are outside it; a
constrained worker must never mutate them. Any need or attempt is a deviation
stop and the controller records `INCONCLUSIVE` — an absent path is not proof
that an ignored or Git-metadata write was safe.
Only PASS may authorize the next step, by sending the complete exact prior
packet as `prior_step_packet`; a short packet id, reconstruction, numeric skip
or worker summary cannot advance. Write `post-implementation-report` only
after the final step has reconciled PASS.
A plan that cannot be compiled into a bounded step is a normal
`ready:false, code:"GATE_BLOCKED"` refusal carrying a `validation` report; hand
it back to `kanmer-plan` rather than guessing the missing fields. Without
`step` the packet is unchanged and its `validation` report is advisory only.

## Project capability and worktree

`ticket.taken` selects the execution lane: a missing value means fresh work
authority, either on the projected frozen-batch workspace or on a new isolated
workspace; a present value means resume the exact already-recorded branch and worktree.

Before the first mutating call, retain the project identity from
`get_status`: `project.project_id` when `project.identity` is `logical`,
otherwise the legacy `project.fingerprint`. If and only if the response
advertises `compat.expectedProject: "optional"`, pass that value as the
top-level `expected_project` on writes. Older servers do not accept the field,
so omit it when the capability is absent. It is never nested in ticket fields
or packet documents.

When the server also advertises `compat.expectedRevision: "optional"`, treat
the packet's `ticket.revision` (and each later `get_item.revision`) as the
ticket-wide token: pass it as `expected_revision` on document and ticket
writes so a plan, proof or review record rewritten by another writer since
your read is refused as `REVISION_CONFLICT` instead of overwritten. Re-read
and re-apply on a conflict; never retry blind.

### Resumed packet

When `packet.ticket.taken` is present, use its `branch` and `worktree` values
literally. They must be the same pair supplied to `resume` when the prior
occupancy refusal needed that retry. From the repository root, validate the
existing worktree without modifying it:

```sh
git -C <recorded-worktree> rev-parse --show-toplevel
git -C <recorded-worktree> rev-parse --git-common-dir
git -C <source-repository-root> rev-parse --git-common-dir
git -C <recorded-worktree> branch --show-current
```

The first command must resolve to the recorded worktree root; the two
common-directory values must name the same source repository; and the final
command must exactly equal the recorded branch. Before editing, call
`list_items` and compare the candidate against every other active ticket's
recorded worktree. It must not be `.worktrees/kanmer`, the board root, the
shared source checkout, or another ticket's worktree. A missing path, detached
or different branch, duplicate location, or unexpected repository is a stop:
return the observed condition in the external hand-off without a ticket write.
The server issues resumed packets only while the ticket remains in
`implementing`; a ticket in Review or Verifying stays taken for traceability,
not for further implementation. Do not repair it by
`git worktree add`, checkout, reset, or `take_ticket`. Existing uncommitted
changes belong to the resumed ticket and are not a reason to clean or recreate
it.

Once the location is validated, renew the lease so the controller can see the
ticket is alive again, naming the lease the packet reported:

```
take_ticket id: <ID>, action: "renew", lease_id: <claim.leaseId>, lease_revision: <claim.leaseRevision>
```

For a manifest-backed batch, add
`controller_run: "<durable-controller-run>"` from the same durable run record.
A modern batch renewal always requires both current `lease_id` and
`lease_revision` plus that exact run id; it never enters the no-token owner
compatibility lane. Missing either CAS token or the run id is a stop.

Every successful renew returns the next `lease_revision`; keep it for the next
heartbeat. Renew refuses `LEASE_EXPIRED` when the lease is no longer current
(it was reclaimed by another controller) and `CLAIM_NOT_OWNED` when a legacy
claim belongs to another controller; both are a stop, not a reason to `force`
or `transfer` from inside execute — a transfer of an expired lease is the
controller's or operator's act and is recorded before this skill is dispatched.
Renew at least every `claim.heartbeatMinutes` (default 5).
Renew again before every long command (a full test rail, a build, a dispatch)
so the lease does not expire under a healthy worker; the packet's
`claim.expiresAt` shows the window. For a command that outlives the window, renew with
`phase: "running-command", extend_minutes: <n>` (bounded by
`claim.commandMaxMinutes`) and renew back with `phase: "implementing"` when it
ends.

### Re-entry after a needs-changes return

A resumed packet whose `claim.reviewRound` is at least 1 is a **remediation
round**: `kanmer-review` wrote a `needs-changes` attestation for this ticket's
PR and moved it `review` → `implementing` with a reason. The branch, worktree,
PR and claim are all the ones already recorded. On this lane:

- Read `get_ticket_doc(doc: "scratch/review")` and work only its `open`
  blocker/major findings plus whatever the plan still requires; do not widen
  the scope because you are back in the file. Read the `## Transitions` note
  in `scratch/execution.md` for the recorded reason.
- Commit on the recorded branch and push it (`git push origin <branch>`); the
  existing PR updates from that push. Never run `gh pr create` on this lane
  and never open a second PR for the same ticket — the store binds the
  attestation to the PR already in `prs[]`, and a new PR breaks that binding.
  Never rewrite history the reviewer already attested unless the controller
  says so; a new head is expected, a force-push that hides the old one is not.
- Replace the post-implementation report as a whole file, adding a
  `## Remediation round <review_round>` section that lists each finding id,
  what changed for it, and the commit. The reviewer's delta review is scoped
  to exactly that list plus the changed lines.
- Finish as for any run: `get_doc_gates`, then move one gated boundary,
  `implementing` → `review`, and record the new head SHA in execute scratch.

The remediation budget is the ticket's, not the worker's: one batch plus one
delta review by default. If the findings cannot be closed in this batch, say
so in the report and stop; do not iterate privately with the reviewer.

### Fresh packet

Only when `packet.ticket.taken` is absent, create the worktree from the
repository root after the packet is ready. **Branch from the packet's
`delivery.baseBranch`, not from `main`** — the project declares which branch its
work integrates into, and assuming `main` is how a `dev`-integrating project
silently builds on the wrong base:

An untaken frozen batch member is the exception to worktree creation. Its
packet has `claim.batch`, and `ticket.workspace`, `claim.workspace`,
`claim.batch.branch` / `claim.batch.workspace`, and any compiled
`step.workspace` all name the already-created shared location. Validate that
location and call `take_ticket` with those exact branch/worktree values plus
the same `controller_run`; keep `ticket.taken: null` as proof that this member
still needs its own lease. Never create `.worktrees/<member-id>` for it.

For an isolated ticket, create the worktree:

```sh
git fetch origin
git worktree add .worktrees/<id-lowercase> -b <id>-<slug> origin/<delivery.baseBranch>
```

`packet.delivery` also names `baseSha` — the exact commit the base branch stood
at when the packet was built — plus `prTarget` and `verificationTarget`. Record
`baseSha` in the post-implementation report when `baseShaState` is `resolved`;
when it is `unavailable` Git could not answer, so say so rather than inventing
one. For an ordinary ticket all three branches are the integration branch; for a
ticket whose delivery record already names the release branch they are that
release branch, and `delivery.backportRequired` names the integration branch the
hotfix still owes a backport to.

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
for an unrecorded ticket and never `force` a taken ticket. A resumed ticket is
already taken, so it deliberately skips this fresh-ticket creation and take
sequence.

### Batch lane (deliberate, FRD-030)

Isolated mode above is the default. Only when the operator or controller has
explicitly named two or more small related tickets as one batch, the first
member's take declares and freezes the membership in one call —
`take_ticket id: <first>, branch: "<batch>-<slug>", worktree: ".worktrees/<batch>", batch: "<batch>", batch_members: [<every member id>], controller_run: "<durable-controller-run>"`
— and every later member is taken on that exact recorded worktree and branch
with `batch: "<batch>"` and the same `controller_run` (any other workspace is
`BATCH_WORKSPACE_MISMATCH`; adding a ticket later is `BATCH_FROZEN`). The
packet's `claim.batch` lists the complete frozen roster, immutable branch and
workspace, and which members are still non-terminal. Before a later member is
taken, the same branch and portable worktree are also projected through
`ticket.workspace` and a compiled `step.workspace` so packet-first execution
can acquire the existing shared location without guessing it.

Before the first packet or take, the controller reads one nonempty
`controller_run` from its durable run record and keeps it unchanged across
reconnects, restarts, workers, and every member operation. Pending, active and
releasing manifests persist both that run id and the actual MCP request actor.
Declaration, pending recovery, every later member take, batch renew, and every
batch execution packet exact-match that actor/run pair. Another actor, run id,
member list, declaring ticket, branch, or worktree is refused; after activation
nobody may add a member, and per-member transfer or a caller-supplied
`assignee`/`controller` label is not a recovery path.

The manifest's worktree identity is canonical and repository-relative, with
the branch recorded separately. After a checkout is copied or relocated, pass
the equivalent worktree under the new repository root; a persisted absolute
host path is never batch authority.

Every modern batch heartbeat calls `take_ticket action: "renew"` with the
current `lease_id`, current `lease_revision`, and that same `controller_run`.
The isolated/legacy no-token compatibility lane never applies to a
manifest-backed batch. Terminal release is different: after every immutable
roster member is terminal, a fresh `kanmer-closeout` actor may release the
batch without matching the implementation actor or `controller_run`.

A batch ships one PR whose body contains one standalone `Kanmer: <ID>` footer
for every member in the complete frozen roster, with no omission or extra
ticket. Each member still writes its own post-implementation report, exact-head
review attestation, review mapping, and proof. Never `release` a member while
`claim.batch.pending` names a non-terminal sibling — the shared workspace is
still theirs.

The first batch member alone is the sole PR creator. After pushing the shared
manifest branch, query open PRs in the resolved source repository for that
branch. For the designated first member, zero matching open PRs means create
the one shared PR with the exact complete frozen footer roster; exactly one
match means validate and reuse it after an interrupted create or lost response;
more than one match is ambiguous and must stop. A later member pushes the same
manifest branch and never calls `gh pr create`: zero matches is missing and
must stop, exactly one is validated and reused, and more than one must stop.

Validation is fail-closed. The live PR must belong to the resolved source
repository for both base and head, be open, have base `delivery.prTarget`, have
head branch `claim.batch.branch`, expose a current pushed head SHA matching the
shared branch, and contain the exact complete frozen footer roster with no
omission or extra. A repository, state, base, head, SHA, or roster mismatch
stops in Implementing. Record the validated shared PR in the current member's
own `prs[]` and record the head in its execute report/scratch before moving it
to Review; every later member repeats that validation and records the same PR,
but never creates a second one.

## Work only the packet

- Work only the packet's `files` scope — and, when a `step` block is present,
  only that step's `allowedFiles`; treat any `allowedSymbols` as a narrower
  descriptive boundary that reconciliation will fail closed on. Do not absorb another
  ticket, repair unrelated failures, or redesign the workflow.
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
2. Record the reachable implementation commit(s) with `update_item`. Immediately
   after step 3 creates, resolves or validates the PR, record its number or URL
   in this ticket's `prs[]` (every batch member records the same shared PR in
   its own array). The `prs` entry is what lets a later `needs-changes`
   attestation return this ticket to Implementing on the same PR; a ticket
   without it cannot take the sanctioned return.
   Link governing docs only when the packet authorizes the link; do not invent
   refs. Keep all writes project-bound when the capability was advertised.
3. Push the ticket branch and open the PR with the ticket title and
   `Kanmer: <ID>` footer — on a fresh lane only; a re-entry lane pushes to the
   branch of the PR already recorded and skips `gh pr create`:

   ```sh
   git push -u origin <id>-<slug>
   gh pr create --base <delivery.prTarget> --title "<ticket title> (<ID>)" --body-file <assembled-body>
   ```

   `--base` is not optional: without it `gh` falls back to the repository's
   default branch, which is the integration branch only by coincidence. The
   merge gate reports `WRONG_TARGET` when a pull request misses the configured
   target. The batch lane above replaces this generic creation command: only
   its first member may create the shared PR, while every later member must
   resolve, validate, reuse and record that one PR.

4. Read `get_doc_gates <id>` immediately before `move_item`. Move one gated
   boundary only, from `implementing` to `review`, and record the PR URL in
   an ordinary execute scratch note. If the gate or move refuses, preserve the
   exact error and remain in the current stage.

The hand-off is the open PR plus the ticket in Review. The author does not
write the review attestation, review the PR, merge it, move it to Verifying, or
clean up the implementation worktree, or start another ticket.

## Pausing

If work must pause before review, leave the ticket taken and append the exact
resume point — branch, worktree, packet version, and last command/result — to
execute scratch. A worker that backgrounds a long command is not notified
while it is stopped: read the command's own log file before ending the turn,
or run it in the foreground with a long timeout; never end a turn "waiting
for a notification". A later worker uses the occupied-ticket `resume` confirmation
and reuses that same recorded location. Do not release a paused ticket that
retains a worktree or branch: release clears the metadata that makes a resume
safe. Release is closeout cleanup only after the recorded location is no longer
an execution target. A refusal, missing dependency, or user-only decision is a
stop, not a reason to guess.

---

**Hand off to `kanmer-review`** once the PR is open and the ticket is in Review.
The author does not merge: review owns the independent attestation and merge
point, never starts another ticket, and this skill's last act is the Review
hand-off.
