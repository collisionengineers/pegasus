---
name: kanmer-auto
description: Run a durable goal controller over a frozen roster — one ticket, one explicit Kanmer group, one area, an explicit ticket list, or the prepared board — keeping a resumable run record on the host group while driving each roster ticket through its profile pipeline with independent review and exact-merge verification. Use for /goal, or when the user says "clear HZN-003", "work through 0.3.3", or "finish this epic". DO NOT USE FOR one phase of one ticket — use the phase skills directly.
---

# Driving a goal run autonomously

`kanmer-auto` is orchestration, not a new workflow. Each ticket still follows
the phase skills' procedures exactly as written; this skill freezes the roster,
orders safe work, reconciles every result against live state, and controls how
many lanes run at once. The controller never turns a worker message into board
evidence, and never becomes the reviewer or the verifier of work it dispatched.

## Orientation, scope and durable-state resume

A `/goal` run drives one **frozen roster** through the phase skills. It accepts
five scopes: one ticket, one explicit existing group, one area, an explicit
ticket list, or the prepared board. A group scope is the ordinary case and needs
nothing further.

Every other scope has no durable batch owner of its own, so it names a **run
host group** whose `automation/` folder owns the record: stop before mutation
and ask the operator to name or create an epic/horizon to host the run. The host
group's membership is **not** the roster. The roster is the ordered list frozen
in the run record's `## Selection contract`, resolved once at run creation from
the scope and never re-resolved. A ticket created after that freeze never joins
a running roster, whatever its group, area or `blocks` edge, and neither does a
quick capture (an item whose profile is `capture`, FRD-032): a capture enters
goal selection only after an explicit promotion, and only in a later run. Do not
add MCP tools, ticket fields, entities, leases, hidden local state, or automatic
merging.

Several controllers may run against one board at once when their scopes and
their workspaces are disjoint. A controller owns the roster it froze and the
workspaces it leased — never the project, and never a ticket merely because the
ticket exists on the board.

At startup, before any ticket write or dispatch, read `get_status`, `get_group`,
the group's `context.md`, and `automation/current.md`. A current record with
status `running`, `paused`, or `blocked` is resumed and reconciled; it is not
silently replaced. Validate its schema, group, project fingerprint, controller
ownership, and referenced history path. A different controller owning a
`running` record is a stop predicate.

The current run-record schema is **`schema: 3`**. Schema 2 introduced `scope`,
`scope_selector`, `authority` and `delivery_target`; schema 3 is the first
schema that carries `transient_retry_limit` and the ledger's durable
`Transient` count. Only a schema-3 active record may be resumed under this
contract.

An active `schema: 1` or `schema: 2` record is not resumed or normalized into
schema 3, and is **never rewritten in place** with a new schema number or
schema-3 fields. Before superseding one, reconcile every legacy lane and worker
from the complete ledger against current board, claim, workspace, Git, GitHub,
CI and recorded worker-result evidence. Every legacy worker must be proven
inactive; a returned terminal result is evidence, while silence or an unknown
dispatch state is not.

If any legacy worker is still active or its state is uncertain, preserve the
old ledger and `automation/current.md` pointer byte-for-byte, create no
successor, and stop with the exact worker, lane and evidence plus one operator
handoff. Never close the old run merely to make a successor possible. Only a
fully quiescent legacy run may be superseded.

After proving quiescence, derive one deterministic successor `run_id` from the
legacy identity. Before preparing its intent, resolve every successor value
that the legacy schema did not record and make the source of each value
auditable. Copy fields the legacy record does contain and record `legacy-field`
as their source. Schema 1 was group-only, so derive only `scope: group` and
`scope_selector: <legacy group>` from that published schema and record
`schema-1-group-contract` as their source. For authority and delivery absent
from schema 1, and the retry limit and each per-ticket `Transient` count absent
from schema 1/2, use an exact value already supplied by the operator or obtain
one bounded operator decision before mutation. Resolve delivery against the
live project policy and require the operator-authorised target; a project
fingerprint mismatch is still a stop. Reconstruct each transient count from
retained attempts when possible. When it is not provable, the only fail-closed
normalization is the chosen retry limit (budget exhausted), with the operator
decision and evidence gap recorded; never silently initialize an unknown count
to zero.

Before closing anything, append a `successor-prepared` event under the legacy
schema and read the ledger back. That durable intent names the successor id,
project fingerprint, scope and selector, authority, delivery target, lane and
retry limits, and the exact ordered roster with every current run disposition.
It also carries a `field_resolution` entry for every successor field that was
absent from the legacy record, naming the resolved value, source, evidence or
operator decision, and reason. Missing or conflicting field-resolution
evidence makes the intent malformed and stops the handoff. By default the
successor preserves that exact legacy roster and those dispositions. A fresh
selection is permitted only when explicit operator authority for fresh
selection is recorded in the prepared intent; a normal resume never silently
absorbs later tickets.

After the intent is durable, close the legacy run under its own schema with a
terminal status that schema already allows, normally `aborted`, and record the
exact reason plus prepared successor id in its existing `stop_reason` and event
log. Create the distinct schema-3 successor at the exact prepared id if it is
absent, or validate an already-present successor against the complete intent;
write and read back its complete history record, then update
`automation/current.md` last.

Startup rolls this transition forward idempotently whenever the pointer names
an active or terminal legacy record with a `successor-prepared` event. For an
active record, re-prove quiescence before closing it; for a terminal record,
create the exact successor if absent or validate it if present. If a handoff
has begun but the intent is absent or malformed, its id conflicts, or the
present successor differs from it, stop without changing the pointer and never
choose an alternate id. A schema-1 record still lacks all four schema-2 fields,
while a schema-2 record lacks the schema-3 retry budget and count; neither
absence may be silently defaulted or accepted without the durable
`field_resolution` above. An unknown or absent `schema` is a stop because it
supplies no safe contract under which to resume or terminally close the record.

### Preflight before the first mutation

Before the roster is frozen, and before any ticket write or dispatch:

- **Identity.** `get_status.project.fingerprint` is the run's identity. For a
  **resumed** run it must equal the existing record's `project_fingerprint`,
  and a mismatch is a stop, not a value to overwrite. For a **new** run there is
  no record yet — the record is created after the roster is frozen — so the
  fingerprint read here is the value written into it at creation, and there is
  nothing to compare it against. Send the fingerprint as `expected_project` on
  writes when `compat.expectedProject` is advertised as optional.
- **Repo staleness.** Report every `get_status.repo.stale` entry. A `behind`
  artefact is an operator action through `kanmer-setup`; it is never a repair
  this controller performs in the middle of a run.
- **Delivery target.** Read the project's delivery policy once and record the
  resolved **PR target** and **verification target** in the run record. A
  controller never hardcodes `main`: the branch a ticket's PR aims at, and the
  branch whose merged SHA is verified, come from that policy and from each
  ticket's own execution packet. A project that declares no policy resolves to
  main-only, which is a resolved answer rather than an absent one — and is
  exactly why a hardcoded `main` looks correct on such a board and is still
  wrong on the next one.
- **Board worktree.** `get_status.boardWorktree` must be healthy and on its
  board branch. `.worktrees/kanmer` is that board worktree: never a lane, a
  rebase target, a cleanup target, or a working directory.
- **Capability.** Whatever the run intends to dispatch must exist now —
  subagent dispatch, the GitHub CLI, the packet tools. An unavailable required
  capability is a stop before dispatch, not a failed lane after it.

Durable state belongs in the group's documents, never in a ticket:

- Current-run pointer: `automation/current.md`
- Immutable run history: `automation/runs/<run-id>.md`

For a new run, create a path-safe unique UTC id (adding a numeric suffix on
collision), fill `assets/run-state-template.md`, and keep its required
frontmatter (`kind`, `schema`, `run_id`, `group`, `scope`, `scope_selector`,
`authority`, `delivery_target`, `project_fingerprint`, `controller`, `status`,
`created_at`, `updated_at`, `lane_limit`, `transient_retry_limit`,
`stop_reason`) and headings (Selection contract, Run invariants, Ticket ledger,
Event log, Resume instruction). Refuse creation when any required field is
absent or malformed. Run status is exactly `running`, `paused`, `blocked`,
`completed`, or `aborted`; ticket dispositions are exactly `queued`, `active`,
`waiting`, `blocked`, `target-reached`, `finished`, or `skipped`.

Write and read it back: the complete history record before writing
`automation/current.md`; write and read back the pointer before dispatch. Never
overwrite an old history record. Update and read back both documents around
every assignment, worker result, reconciliation, wait, pause, block,
completion, or abort. Store operational state only: roster, target, lane
partition, skip reasons, worker outcomes, and concise operator answers. Never
store secrets, full prompts, or large command output.

## 1. Roster and gates-first readiness

1. Call `get_status`, then **resolve the roster from the run's declared
   scope**. Each of the five scopes has its own resolution step, and every one
   of them resolves through `list_items` or `get_item` — never `get_group`,
   whose derived members carry only id/title/stage and not the taken, blocked
   and profile fields selection needs:
   - **ticket scope** — `get_item "<TICKET-ID>"` for the one named ticket. The
     roster is that single id. The run still names a host group, and that
     group's membership is never added to it.
   - **group scope** — `list_items group: "<explicit group>"`, in the group's
     order.
   - **area scope** — `list_items area: "<area id>"`, in board order resolved
     with `list_board`, not in id order.
   - **list scope** — `get_item` for each id the operator named, in the order
     they named them, which is the roster order. An unknown or archived id is a
     stop before the freeze, never a silently dropped member.
   - **board scope** — `list_items` with no scope filter, in board order: the
     prepared board is every non-archived ticket that step 2's exclusions do
     not drop.

   Then show the resolved roster, its scope and selector, the target point and
   the exclusions to the operator before starting. Whichever step produced it,
   the result is one ordered list frozen into `## Selection contract` at that
   moment and never re-resolved, and steps 2–6 below apply to it identically:
   the gates-first readiness rules do not vary by scope, and a ticket that
   appears in a later `list_items` answer never joins a roster that is already
   frozen.
   Parse the requested target **before resolving dependency feasibility**:
   “up to review” stops each ticket after its PR is open and its ticket is in
   Review; the default is closeout, subject to the human merge boundary. Resolve
   stage names with `list_board`, and record both the requested target and the
   board's final stage in the Selection contract. Dependency selection below
   uses one explicit predicate: the target **reaches the board's final stage**
   when it is `closeout` or resolves to that final stage itself; every other
   target is earlier. Do not compare the literal word `closeout` with a stage id
   or assume that every target closes a live `blocks` edge.

2. Read the run host group's shared context. **Before resolving any dependency
   edge, apply every ordinary exclusion.** Drop archived tickets, and drop
   **quick captures** — a summary with `capture: true` (profile
   `capture`) is a recorded observation, not selected work, and promoting one is
   an operator decision this skill never makes. Report them in the exclusions
   rather than silently omitting them; the server refuses to move, take or
   packet one (`CAPTURE_NOT_PROMOTED`), so a capture that reaches selection is a
   bug in the roster, not a ticket to unblock.

   Claim classification is part of those ordinary exclusions and therefore
   happens before outside-blocker closure or cycle detection. A ticket taken by
   another actor is handled by its claim state, never by `force`:
   - a **live** foreign claim (`claim_expires_at` in the future, or a
      pause/resume note in its scratch) belongs to that actor — exclude it and
      coordinate;
   - an **expired** foreign claim (`get_execution_packet` refuses with the
      claim expired, or `claim_expires_at` has passed with no live run record)
      is inspected and recorded as assignment-eligible without mutation. Keep
      its branch, worktree and dirty-state evidence, but do not append scratch,
      transfer or otherwise write during selection; it must first survive every
      feasibility rule below. Never pass `force`, and never `release` a claim
      that still has a worktree.

   After all ordinary exclusions and expired-claim classification, but before
   outside-roster closure or any dependency pruning, determine exact target
   satisfaction for every surviving candidate from its current item, gates and
   every live provider fact that target requires. For **up to review**, require
   the ticket to be in Review and fetch the ticket's linked current PR: it must
   be open against the recorded delivery target, and its current head SHA must
   be known. Record the PR number, target branch, exact head and observation
   time with the `target-reached` disposition. Stored `prs` metadata, the item
   and gates alone never prove that target; unavailable or contradictory
   provider evidence leaves the member nonterminal and `waiting`, not
   target-reached. An archived or unpromoted quick capture never receives `target-reached`;
   mandatory exclusions removed it first. A member already at the requested
   target remains in the frozen roster with a terminal `target-reached` run
   disposition; remove it only from the set that still needs advancement,
   never exclude or dependency-block it. Target satisfaction does not erase
   outgoing blocker evidence: that member remains a live blocker for
   unsatisfied members until its actual board state clears the edge. A
   target-reached member whose expired claim was classified is never
   transferred.

   **A `blocked` flag is a fact about the board, not about this run.**
   `list_items` reports `blocked: true` whenever *any* live ticket anywhere
   declares a `blocks` edge to it, so the flag on its own never says whether the
   blocker is one this run is about to freeze in. Dropping on the flag alone
   would contradict section 2, which orders a blocker before its dependent — an
   ordering that can never happen for a dependent already dropped. Resolve it
   instead: read the blocked ticket's `blockedBy` with `get_links`, then judge
   each blocker's liveness from that blocker's own item — its `archived` flag
   and its stage against the board's last stage — because `blockedBy` is derived
   from the `blocks` edges alone and is not filtered by liveness.

   Resolve outside-roster exclusions to a fixed point **after** all ordinary
   exclusions and target classification. Apply that fixed point only to
   nonterminal members in the set that still needs advancement. A terminal
   `target-reached` member is never an exclusion candidate or the dependent
   receiving a dependency disposition, although its outgoing live edges remain
   blocker evidence for unsatisfied members. For each member still needing
   advancement, read every live blocker. If any blocker is outside the current
   candidate set, exclude the dependent, name the blocking ids and where they
   sit, then repeat: a blocker excluded on one pass is an outside-roster blocker
   for its dependents on the next. Stop only when one complete pass removes
   nothing. With `A -> B -> A` and A live-foreign-claimed, exclude A for its
   claim before graph construction, then exclude B with A named during the
   fixed point; record no cycle for that excluded pair.

   Only after ordinary exclusions and that external-blocker fixed point,
   **before retaining any dependent under the in-roster rule below**, build the
   directed graph from the remaining live in-roster edges, but admit an edge
   only when its dependent is a nonterminal member in the needs-advancement
   set. Filter by the dependent, not the blocker: a terminal
   `target-reached` member may remain a blocker source, but no incoming
   dependency edge is admitted for it. Detect every cyclic strongly connected
   component in that filtered graph, including a
   one-ticket self-loop. For each component, record an exact ordered witness
   path (`A -> B -> A`) and
   its complete member set in the Selection contract, ticket ledger, event log
   and report.

   The component's **cycle-affected set** is every cycle member (necessarily
   nonterminal and needing advancement) plus every transitive nonterminal
   dependent reachable from it along those same filtered
   blocker-to-dependent edges. Give every affected ticket a terminal
   run-ledger disposition of `blocked`, name the originating cycle path and
   members in its reason, and dispatch none of them. With `A -> B -> A`,
   `B -> C`, and `C -> E`, all of A, B, C and E are terminally blocked; C and E
   never wait behind a blocker that cannot finish. If A is already terminal
   `target-reached` in the apparent `A -> B -> A`, omit `B -> A` because A is
   not an eligible dependent: A stays `target-reached` as a blocker source,
   and only B proceeds to the later feasibility rule. Record every component
   separately, including multiple components and self-loops. A cycle is an
   explicit blocking disposition, never a queue that waits for one member to
   precede another. Only safe acyclic internal edges reach the
   target-feasibility rule.

   **Only a target that reaches the board's final stage clears a live blocker
   edge.** When the requested target does not reach that final stage, terminally
   block each dependent on a remaining acyclic live edge and every transitive downstream
   dependent in the run ledger; keep all of them in the frozen roster, name the
   blocker, requested target and final stage in the reason, and dispatch none
   of the affected dependents. The blocker and every unrelated safe lane still
   proceed to the requested target. For up-to-review `A -> B`, A reaches Review
   while B and B's downstream dependents are terminally blocked, because A is
   still a live blocker there. For closeout `A -> B`, retain and serially order
   both because closeout reaches the final stage and can clear A's edge; an
   explicit Done target has the same result. An already-Done A creates no live
   edge and therefore does not affect B.

   After that target-feasibility closure, apply the retention rule:

   - **Every live blocker is inside the roster being frozen and the requested
     target reaches the board's final stage** — keep the dependent. It is queued
     work, not an exclusion: section 2 puts it in one serial lane behind its
     blockers, so the run orders its own dependency chain instead of silently
     shedding it.
   - **Any live blocker is outside the roster being frozen** — exclude the
     dependent during the fixed point above, and say so in the exclusions,
     naming the blocking ids and where they sit. Nothing in this run will clear
     them.

   Keep the run `running` while any unaffected safe lane can proceed; neither
   cycle members nor target-affected dependents cancel or pause an independent
   lane. Only after every safe lane has a terminal disposition and no lane is
   active or waiting, set the run to `blocked` with a `stop_reason` naming every
   cyclic component, target-blocking edge and affected member. For `A -> B -> A`
   plus independent D, D reaches its target before the run becomes blocked. A
   run with any cycle-affected or target-affected ticket is never reported
   `completed`.

   Freeze a dependency-safety snapshot with the roster: exact live blocker
   edges, blocker liveness, target bindings, claim classification, and the
   relevant run dispositions. Before every assignment and after every worker
   result or timeout, compare live state with that snapshot.

   Target binding has one revalidation procedure and it runs before dependency
   feasibility. When a snapshot comparison observes any changed target fact or
   outgoing blocker liveness for a `target-reached` member, first revalidate
   that terminal blocker source even though it is outside the
   needs-advancement set. Immediately before any terminal run-status transition
   or final report, run the same procedure for every `target-reached` member.
   Re-gather the current item, gates and target-specific live provider facts
   and compare them with the recorded target binding. No dependent that relies
   on that source is assigned until this pass has a durable result.

   The revalidation outcomes are disjoint:

   - **Valid.** Refresh the exact binding and observation time, then continue
     dependency feasibility.
   - **Affirmatively stale or contradictory.** Any available required fact
     that disproves the binding makes this outcome authoritative even when
     some other provider is unavailable. Preserve the old binding and every
     current fact, then replace `target-reached` with a terminal `blocked`
     disposition whose reason starts `target evidence stale:`. This is a
     terminal-to-terminal correction: never reopen or dispatch the member, and
     propagate its terminal non-success before dependency feasibility. Never
     report the run `completed` or the member at target from stale evidence.
   - **Unavailable or unknown.** Only the absence of a required live fact, with
     no available fact disproving the binding, earns this outcome. Preserve
     `target-reached` and its last valid binding; record the unavailable fact,
     provider, observation time and exact resume action in the run, keep every
     dependent relying on it `waiting`, and dispatch none of those dependents.
     Unrelated safe lanes continue. When none remains ready, set the run
     `paused` with a stop reason starting `target evidence unavailable:`.
     Resume only after provider capability changes or an explicit resume, then
     run this same revalidation again. Unavailability never consumes the
     verification retry budget, becomes terminal `blocked`, or permits
     `completed`.

   Only after every implicated terminal source is valid or affirmatively
   corrected does a changed snapshot re-run outside-roster closure,
   cyclic-component and target-feasibility rules for nonterminal frozen members
   that still need advancement. A change never changes membership. Map a
   post-freeze exclusion to a terminal `blocked` disposition instead of dropping
   the member. Persist and read back the replacement snapshot and every target
   revalidation result or resulting disposition before any next dispatch.

   If a frozen in-roster blocker reaches a terminal non-success disposition
   while its edge is still live, it cannot clear that edge. Give every
   transitive unsatisfied dependent a terminal `blocked` disposition naming the
   blocker and failure; unrelated safe lanes continue. A removed edge may make
   a still-nonterminal queued member eligible, but no graph change reopens a
   terminal run disposition.
3. For every retained ticket, call `get_doc_gates` and use its current stage,
   reachable stages, and first unmet next-boundary requirement as the routing
   table. Do not restate profile-to-document mappings in this skill.
4. Advance one gated boundary per move. Set `docs_todo` only when a governing
   document genuinely needs to be written; do not create optional documents to
   normalize the roster. A ticket with no currently required preparation phase
   routes to its next applicable action.
5. A user-only question at any phase parks that ticket as `waiting`, quotes the
   question and recommendation in the event log, and pauses that lane. Never
   guess an operator answer.

## 2. Lane assignment

Compare every retained ticket's `files` document. Disjoint file sets may use
different lanes; overlapping files share one serial lane; a `blocks` edge orders
the blocker before its dependent regardless of file disjointness. Cap parallel
work at approximately three lanes.

Files are not the only overlap. Two tickets also share one serial lane when they
touch the same **contract or API surface** — the same exported type, tool name
or schema — the same **migration** sequence, the same **lockfile** or dependency
manifest, or the same **heavyweight shared resource**: a release channel, a
device, a fixed port, or the single hosted CI rail. Resource overlap is real
even when the diffs are disjoint, and it is not free to ignore: two heavy
verification rails running at once is a documented cause of host timing
failures, so hold the second rail rather than reading its flake as a regression.

Before assigning a ticket, re-read its item, links/dependencies, taken state,
required document versions, activity, and `get_doc_gates`, then perform the
dependency-snapshot comparison above. For an expired foreign claim, transfer
only now, immediately before the member's first assignment and only after it
survived feasibility: re-read the claim and collect the branch, worktree and
dirty-work evidence into the run record, then call `take_ticket action: "transfer"` directly.
Do not append ticket scratch before transfer. The transfer
path re-collects recovery evidence and rechecks lease liveness under the write
lock; only a successful transfer records its preserved-work summary in ticket
scratch. Never transfer a terminal, excluded, target-reached or otherwise no-longer-advancing member.
A `CLAIM_LIVE` refusal means it was renewed and the
ticket remains byte-for-byte unchanged; retain the frozen member with a
terminal `blocked` live-claim disposition and dispatch nothing for it. Record
the lane as `active` with worker, branch/worktree when known, attempt,
timestamp, action, and stop condition; append a `lane-assigned` event;
write/read back the full run record; only then dispatch.

Each lane uses its own `.worktrees/<id>` worktree and branch. The one
exception is a deliberate batch lane (FRD-030): two or more small related
tickets the run record names as one batch share one worktree, branch and PR,
declared and frozen by the first member's `take_ticket` with `batch` and
`batch_members` plus `controller_run: "<run_id>"`; the lane is not cleared
until every member is terminal, and no other ticket may join it or take its
workspace. Use the automation ledger's immutable schema-3 `run_id` as the
batch `controller_run`; never a worker id, session id, reconnect id, or
per-call id. Pass it unchanged on the first batch declaration, an exact
pending-declaration recovery, every packet-first `get_execution_packet`, every
later member `take_ticket`, and every `take_ticket action: "renew"` together
with the current `lease_id` and `lease_revision` CAS pair. Retain it across
reconnects, restarts and worker handoffs. No lane may touch
`.worktrees/kanmer`, which is the board worktree on the board branch and is
never a lane, rebase target, or cleanup target. A ticket runs through the
existing phase skills only: `kanmer-research` → `kanmer-plan` →
`kanmer-execute` → independent `kanmer-review` → `kanmer-verify` →
`kanmer-closeout`, only as far as the requested target permits.

## 3. Controller action loop and result reconciliation

The controller chooses one safe next action per ready lane. The worker receives
the execution packet/approved plan, exact role and allowed scope, and its
mandatory Stop condition. The worker returns at that Stop condition or a
mandatory stop predicate; it never chooses another ticket or dispatches a
successor. Workers renew their own lease (`take_ticket action: "renew"` with
the packet's `claim.leaseId` / `claim.leaseRevision`) on resume, at least every
`claim.heartbeatMinutes`, and before long commands (`phase: "running-command"`
with a bounded `extend_minutes`); a `LEASE_EXPIRED` refusal means the lease was
reclaimed and the worker stops; a subagent worker that backgrounds a command
reads that command's log itself before returning — it is not notified while
stopped, and a worker that ends its turn "waiting for a notification" is a
failed worker, reconciled from live state like any other.

For a constrained step, the trusted controller retains the exact full
`step-packet/2` object inside the live dispatch/reconciliation chain before
dispatch and later supplies that same object to `reconcile_ticket
step_packet:`. Its `packetId` is tamper-evident identity, not authentication;
a worker-returned or reconstructed packet is never authority. The run
ledger records the packet id and compact outcome, not the full packet or
prompt. If a crash or reconnect loses the controller-retained object, record
packet-loss as `INCONCLUSIVE`, dispatch no successor, and never rebuild it from
worker text or current board/Git state. A successor step is issued only after
the exact retained predecessor reconciles PASS and is supplied whole as
`prior_step_packet`. Initial issuance likewise requires at least one mapped
unchecked checklist marker for the selected ordered step; a plan-only or
unrelated checklist cannot become worker authority. Only an exact level-three
`### Step N — <title>` heading is a structured boundary: declared numbers
start at 1 and remain contiguous, while nested or explanatory headings never
become steps. Named checklist authority exists only when the checkbox label
begins with `Step N`; an explanatory prose mention of `step N` never maps that
checkbox to a step. Exact checklist bytes
retain a leading UTF-8 BOM. Compilation and strict verification derive every
marker state from those bytes, require a completed prefix and unfinished
selected step, and refuse any checked successor marker. Whole-ticket and
constrained issuance share one lexical, de-duplicated group census: counted
ticket documents plus unique group ids are capped at 256 before any group or
context read, and missing or conflicting resolved identity refuses. Core binds
the requested ticket record, completes the canonical metadata census and
preflights per-file and aggregate byte bounds before opening ticket-document,
group-record or context content. Replacement, growth, symlink, special-file or
hard-link evidence refuses through identity-bound capped handles; scratch and
reference stay revision-exempt but consume inventory and aggregate bounds.
Physical confinement is anchored at the configured project root: a junction at
that root is allowed, but any symlink or junction below it, including `.kanmer`
and ticket, document or group directories, refuses.

On every result or timeout, the controller:

1. stops conflicting dispatch while the result is uncertain;
2. re-reads the live item, links/dependencies, documents and versions, activity,
   Git/PR state where applicable, and `get_doc_gates`;
3. compares the live dependency state with the frozen snapshot and applies the
   post-result revalidation and downstream-failure propagation above;
4. calls packet-aware `reconcile_ticket` when constrained, then compares its
   bounded actual HEAD/index/worktree deltas, stage, gate, exact checklist,
   counted ticket documents, branch/worktree, commit, PR and error evidence
   with the approved scope; missing or unreadable evidence is `INCONCLUSIVE`,
   while undeclared or forbidden changes are FAIL. The shared iterative
   path-match budget is charged before raw path parsing and before every literal
   or wildcard comparison; exhaustion is `INCONCLUSIVE`. Dirty regular-file
   bytes are read once through one capped handle whose pre-open,
   handle-before/after and post-path device, inode, type, mode, link-count and
   size facts must agree, and the handle closes on every result. Checklist
   reconciliation preserves every raw CRLF/CR/LF terminator and final-newline
   state outside the selected marker;
   any actual change with non-empty free-form `allowedSymbols` adds
   `STEP_SYMBOL_SCOPE_INCONCLUSIVE`; forbidden or undeclared path FAIL takes
   precedence, no-change invents no symbol finding, and empty symbols preserve
   file-scoped PASS;
5. records the worker result, reconciliation, discrepancy, and one next action
   in the ledger/event log; and
6. writes and reads back the run record before selecting another action.

The constrained Git census covers tracked, staged, unstaged and untracked
paths plus both rename endpoints. Changed-path evidence also includes one bounded
complete union of every path touched by every intervening commit, including
paths later reverted; a non-ancestor baseline or exhausted history is
`INCONCLUSIVE`. That history census validates both old and new modes from every
intervening tree edge; any intervening `120000` symbolic-link or `160000`
Git-link mode refuses even if a later commit restores a regular endpoint. A packet workspace HEAD is a full 40- or
64-character Git object ID. Every sample also hashes one bounded NUL
`git ls-files -v -s -z` index census, binding flag, mode, object id, stage and
path: assume-unchanged or skip-worktree entries refuse; nonzero stages and
gitlinks refuse without index mutation; census drift is `INCONCLUSIVE`. On
filesystems that expose the owner-executable bit, every clean tracked regular
path must agree with its indexed `100644`/`100755` executable class;
disagreement refuses. A tracked
mode-`120000` path is retained only when its checkout representation and capped
target bytes are identity-bound and its physical target is an indexed tracked
regular file inside the worktree; external, chained-external, dangling,
unreadable, unstable or over-budget links refuse.
Tracked-link target bytes retain a leading UTF-8 BOM.
Ignored or untracked link targets refuse.
Execution authority requires exactly one selected ticket endpoint across v2
areas and legacy v1 storage; duplicates refuse before either record is opened.
Ignored paths and `.git` / common-directory metadata are outside it and
constrained workers must never mutate them. Any need or attempt is a deviation stop
recorded as `INCONCLUSIVE`; absence from the census never authorizes such a write.

After anything merges to the run's recorded `delivery_target`, lanes still in
flight rebase onto that same target before opening a PR, with absolute paths and
never a literal branch name (`git -C <absolute-lane-worktree> fetch origin`,
then `git -C <absolute-lane-worktree> rebase origin/<delivery_target>`). The
integration branch is policy resolved in the preflight, not a constant: a
controller that types `main` here has hardcoded the one branch the preflight
exists to stop it hardcoding. A failed ticket does not
silently disappear: record the exact failure, release it only under the phase
skill's rules, return it to the appropriate stage, and classify it in the run.

Two results are routed rather than stopped on:

- A **`needs-changes` review** on a lane's PR. The reviewer (or this
  controller) moves the ticket `review` → `implementing` with a reason, as
  `kanmer-review`'s sanctioned return describes; the next action for that lane
  is `kanmer-execute` on the **same** branch, worktree and PR (its re-entry
  lane), followed by the reviewer's delta review. Read `review_round` and
  `remediation_budget` from the item before dispatching: a
  `REMEDIATION_BUDGET_EXHAUSTED` refusal forbids another remediation round. If
  the delta review identifies the implementation approach itself as the root
  cause, route the ticket through the one controlled replan below; otherwise
  quote the refusal and stop that lane for the operator.
- A **non-PASS verification** with a `failure_class`. Route by
  `kanmer-verify`'s table — `transient` reruns in Verifying, `inconclusive`
  waits with the missing check named, `implementation` returns the ticket to
  Implementing, `plan` returns it to Preparing — each by one `move_item` with
  a reason quoting the proof. A proof without a class is `inconclusive` until
  the verifier classifies it.

### The transient retry budget

`transient` is the only routing outcome that returns a lane to the stage it came
from, so it is the only one that can loop. `kanmer-verify` decides *whether* a
red run earns that class; only a number decides *how often*. The run record
carries **`transient_retry_limit`**, defaulting to **2** re-runs per ticket per
run, and the ledger's `Transient` column counts what each ticket has spent. No
tool enforces this — it is the controller's own budget, which is exactly why it
is written into the record and counted there rather than remembered.

Both permitted fresh-verifier authorization paths in section 9 spend this one
budget: the evidence-bootstrap path that can establish the classification
evidence, and the classified-transient path after an authoritative
`failure_class: transient`. Every dispatch admitted by either path is one
**logical verifier attempt**. The bootstrap path may admit at most one
evidence-establishing attempt per ticket per run; the classified-transient path
may admit another fresh independent attempt whenever durable budget remains.
Immediately before its first dispatch, reserve that attempt by incrementing the
ticket's durable `Transient` count, writing the run record and reading it back.
A launch proven to have failed before mutation may use section 9's one logged
transport retry against the same reservation: do not increment it again,
decrement it or reset it. Unknown launch status dispatches nothing. The default
of 2 deliberately leaves room for one evidence-bootstrap and one
classified-transient attempt. Raising the limit adds classified-transient-path
capacity; it never adds a third authorization path; classification never resets the count.

On the attempt that would exceed the limit the lane does **not** re-run. Set
that ticket `blocked`, and quote this refusal verbatim in the ledger, the event
log and the report:

```
transient budget exhausted: <ticket> spent <n>/<transient_retry_limit> re-runs at <SHA>; last failing check <check>. Not retried again without an operator decision.
```

Retain every attempt, red and green, in the proof, and continue other safe
lanes. Raising the limit is an operator action recorded in the run record, never
a controller one, and it is predicate 15's budget boundary rather than a new
predicate. A ticket that keeps producing the same red check after its budget is
gone is telling you about the rail, not about the change, and re-asking is not
an answer.

### Push the board before trusting a gate

The merge gate reads the **remote** board tip, and it does not re-run when the
board is pushed. A gate result is therefore evidence only about a board the
remote has already seen. Before treating one as current, confirm the board
branch is pushed, from a normal checkout and with absolute paths:

```sh
git -C <absolute-path-to-board-worktree> rev-parse <board-branch>
git -C <absolute-repository-root> rev-parse origin/<board-branch>
```

`<board-branch>` is the **configured** board branch, never a hardcoded
`kanmer-board`: read it from `get_status.boardWorktree.expectedBranch`, which is
the repository variable `KANMER_BOARD_BRANCH` the hosted gate itself uses and
which falls back to `kanmer-board` only when unset. This is the same defect
class as a hardcoded `main`, and it is wrong for the same reason — the default
being right on this board is not the branch being fixed.

The two must be equal. On a server that reports it, `get_status.boardSync` with
`ahead` at 0 and a matching local SHA is the same fact; older servers do not
expose it, so the git comparison is the portable form and the one to state in a
hand-off. A gate that passes while recording that no review attestation exists,
or that fails with `SYNC_REQUIRED`, is a stale-board artefact and not a verdict
about the work: get the board synced — the operator pushes it unless they have
explicitly granted this run that authority — then re-run the failed job at the
same SHA and read the result again. Never manufacture the missing evidence, and
never commit or push the board branch outside an explicit grant.

### Read the evidence, not its summary

- Read a proof or a review attestation **in full**, never frontmatter-only. The
  frontmatter carries the only machine-readable verdict, and prose appended
  later can contradict it; a frontmatter-only read is a recorded cause of a
  wrong disposition.
- A `threads_snapshot` is a YAML **array**, one entry per thread. A mapping is
  an invalid attestation even where the gate downgrades it to a warning.
- Every controller git command uses an **absolute path**. A shell whose working
  directory had drifted into the board worktree once ran a merge there; git
  refused it, but the guard is the absolute path, not the luck.
- Concurrent verifiers get distinct log paths. Two verifiers sharing one log
  file destroy each other's evidence.
- Never run a secrets-manager listing command to inventory names, and never
  rely on a post-hoc text filter to redact output that has already been
  produced.
- A reviewer finding dispositioned minor, note or accepted-risk does not become
  a new ticket: filing one un-accepts the risk that was just accepted. A new
  ticket needs a blocker or major finding, or one that blocks a named governing
  acceptance criterion. Everything else stays recorded as residual risk on the
  ticket that owns it, and a roster that grows faster than it clears is the
  failure this prevents. The one exception is `kanmer-review`'s
  **`deferred-to-ticket`** disposition, which is invalid without a linked
  ticket: a finding the reviewer genuinely defers as out of scope takes that
  disposition *and* its ticket whatever its severity, because the alternative is
  a finding with no legal disposition at all. Deferring is the deliberate act; a
  minor left as accepted residual risk is not deferred and gets no ticket.

### Bounded churn and the escalation boundary

A ticket gets one consolidated review, one in-scope remediation batch, and one
delta review. `kanmer-review` owns those rules and this skill does not restate
them; what the controller owns is the route out when they are spent.

- The delta review still blocks, and the blocking finding identifies the
  **implementation approach itself** as the root cause — the implementation
  follows the plan, but the planned mechanism is wrong. The controller may take
  **one automatic replan** for that ticket even when the remediation budget is
  exhausted, because this changes the approved approach rather than buying
  another remediation round. Require that exact root-cause classification from
  the independent delta review and confirm the ledger has no prior replan for
  the ticket. Then make one `move_item` to `preparing` with a reason quoting
  the finding ids, one fresh planning subagent, one plan revision, then
  re-execute on the same ticket. Record it once in the ledger's replan column.
  It does not raise `remediation_budget`, and it neither resets nor increments
  `review_round`, so no number of replans can buy a fresh remediation round.
- `move_item` refuses `REMEDIATION_BUDGET_EXHAUSTED`. The budget is genuinely
  spent, so the controller never retries Review → Implementing or raises the
  budget. The one approach-level replan above is the sole controlled route to
  Preparing and creates no new remediation allowance. Without its exact
  independent classification, or after that replan is spent, the lane goes
  `blocked` with the refusal quoted verbatim; only an operator may re-open the
  remediation loop with a reason beginning `operator:`.
- The replan is bounded too. A ticket that still fails materially after its one
  replan stops as an explicit `blocked` outcome carrying the exact evidence. It
  is not retried again, and the same work does not reappear as a fresh ticket.

### Active Review and Verifying invariants

A selected ticket in **Review** must have an open PR, a current head SHA, an
active or immediately queued reviewer, and an attestation state. The one
exemption is the supported **up to review** target point, whose stop condition
is precisely a ticket parked in Review with its PR open: at that target such a
ticket is **at target**, not unexplained, and requires no queued reviewer. Every
other target still requires one. A selected
ticket in **Verifying** must have a confirmed merged PR, an exact merge SHA, an
active or immediately queued verification attempt, and a known proof state.

Anything else is an unexplained state, and it is reconciled before the run
reports anything. The controller's first act is the reconciliation pair, not a
manual audit: on any resumed or suspicious Review/Verifying ticket, call
`reconcile_ticket id: <ID>` as a dry run first and, only when it returns a
recommendation, apply that recommendation with `apply_reconciliation id: <ID>,
expected_revision: <the recommendation's revision>` before re-reading anything
by hand. A merged PR still sitting in
Review is then moved on through its own gates, and a PASS proof still sitting in
Verifying is moved and closed out.
For a merged batch PR, re-read the active manifest and resume
`kanmer-review`'s immutable-roster handoff instead of reconciling only the
current member: Review advances exactly to Verifying, already-Verifying is the
idempotent no-op, and any other member state stops. Dispatch no batch verifier
until the complete roster re-read is Verifying.
Verifying is not a holding column. The run is never reported as `completed`
while a selected ticket sits in an unexplained Review or Verifying state, and a
standup summary of the rest of the roster does not substitute for that.

## 4. Mandatory stop predicates

These predicates stop or pause dispatch; none may be reported as successful
completion merely because a partial roster has a standup summary:

1. wrong project fingerprint or required capability;
2. unhealthy or unknown board worktree for the required action;
3. durable run-state write/readback failure;
4. an unresolved non-parked question;
5. a missing required governing or pipeline document;
6. a materially stale approved plan or document version;
7. a live dependency;
8. a ticket occupied by another actor's live claim (an expired claim is
   transferred, not stopped on);
9. a branch/worktree mismatch or unsafe path;
10. worker-reported plan deviation, ambiguity, destructive risk, security or
    secret risk;
11. a required command or environment unavailable;
12. a failed test or verification;
13. unknown PR, check, or merge state;
14. the plan's explicit `## Stop condition`;
15. an operator target, time, budget, or cancellation boundary;
16. no safe ready work; or
17. true run completion.

The only successful terminal stop is an exhausted roster at the declared
target. The only operator-wait stop is a genuine operator-only question. A
partial-roster report presented as success is a defect; safety predicates use
`paused`, `blocked`, or `aborted` with their exact reason instead.

A failed verification first stops the lane as retryable; auto never infers that
it is terminal. If the operator explicitly declares the result irrecoverable or
superseded and supplies the reason plus a successor ticket or an explicit
no-successor disposition, resume through `kanmer-verify`'s terminal-retirement
path and then `kanmer-closeout`. The archived Verifying ticket is reported as
skipped/retired non-success, never cleared or Done. Without that disposition,
the lane remains stopped with the exact failed check and resume action.

## 5. Persisted stop/hand-off format

Before an intentional safe stop, set the accurate run status and `stop_reason`.
Record the exact predicate text/id, affected tickets, observed stage/gates and
document versions, worker/attempt, commands and evidence, remaining roster,
and one deterministic resume read/action. Append the stop event. Write/read
back the complete history record first, then write/read back the pointer. If
state persistence fails, report that failure and never claim a durable
hand-off. A user-only question is quoted rather than collapsed into “blocked”.

## 6. Serial fallback — `lane_limit: 1`

If parallel worker dispatch is unavailable before a worker starts, persist
`lane_limit: 1` and a `parallel-unavailable` event/reason, then use the same
ordered roster, gates-first readiness, controller loop, durable state cadence,
per-ticket take/worktree/packet rules, and stop predicates. Subagents matter
because fresh worker contexts keep a long controller run small enough to finish;
without them the run may span invocations, and the persisted state makes that
span resumable rather than a new or duplicated run.

Serial mode permits only one active or uncertain ticket. It does not pre-take
future tickets, broaden scope, combine tickets, or skip reconciliation. Finish
and persist the current action before assigning the next ticket. If the
controller safely adopts a designated preparation or execution role, it says
so explicitly and obeys that phase skill, then returns to controller mode at
the role's Stop condition.

## 7. Role-independence boundaries

Serial execution is not permission for one context to impersonate every role.
Implementation never self-reviews; independent review and post-merge
verification remain separate actor/context requirements. If the required
reviewer or verifier is unavailable, stop at that boundary with the exact
handoff, and do not waive merge-SHA proof, required checks, or live evidence.
The controller never runs `gh pr merge` and never treats a passing PR as
merged.

Independence is a distinct **run identity**, not a distinct account: the
implementation run's identity must differ from the reviewer's, and the verifier
is a third. Record all three in the ledger, so an attestation that says it is
independent can be checked against the run rather than believed.

The controller **coordinates** the merge; it does not perform it. It dispatches
the independent reviewer that holds the merge point, and reconciles the merge
from GitHub afterwards. Handing a blocked merge back to the reviewer that
withheld it is the correct move, not an escalation — that reviewer is the actor
whose condition has to be satisfied.

Branch protection that sets `required_conversation_resolution` holds a PR at a
blocked merge state until every review thread is resolved, however green its
checks and whatever its approval count. Dispositioning a finding in the
attestation and resolving its thread on the PR are **one obligation**, owed by
the reviewer that dispositioned it, and discharged in that order so the record
survives outside the board. A reviewer that does the first without the second
leaves a PR that cannot merge; reconcile that as an unmet review obligation, not
as a merge failure, and never as a reason for the controller to merge instead.

## 8. Completion definition

Worker completion means return at its assigned Stop condition, not ticket
completion. A ticket reaches its lane target only with live Kanmer stage,
documents, gates and Git/PR/proof evidence. The run is `completed` only when
every selected non-skipped ticket reaches the declared target and no lane is
active or waiting. Waiting, blocked, skipped, and failed reasons remain visible;
worker final text, checklist prose, or a partial summary cannot complete a
ticket or run.

## 9. Failure and retry rules

Distinguish a launch that definitely failed before mutation from an unknown
worker status. For a clearly transient pre-mutation transport failure, record
it, re-read taken/activity/Git/PR state, and allow at most one logged launch
retry. If status is unknown, mark the lane waiting/blocked and dispatch nothing
conflicting. Never automatically retry failed implementation, migration, test,
or build commands. A failed verification command is never rerun directly by the
controller or by the same verifier. There are exactly two authorization paths
that may admit logical verification attempts to fresh independent verifiers.
Every admitted attempt requires room below `transient_retry_limit` and one
durable `Transient` reservation before its first dispatch:

1. **Evidence bootstrap.** The authoritative prior proof records both
   `result: FAIL` or `result: INCONCLUSIVE` and
   `failure_class: inconclusive`, and explicitly requests a re-run of the same
   failing job at the same SHA. A `FAIL` proof also retains the non-zero failing
   attempt. Before dispatch, confirm that the failing path is untouched by the
   diff and record a concrete environmental mechanism hypothesis. A fresh
   independent verifier performs the re-run. This path may admit at most one
   evidence-establishing logical attempt per ticket per run. It gathers evidence
   for `kanmer-verify`; it never lets the controller self-classify the failure
   as transient.
2. **Classified transient.** An authoritative exact-SHA proof already records
   `failure_class: transient`; a fresh independent verifier may perform another
   bounded re-run whenever the durable budget still has room. Raising the limit
   adds capacity only to this path and never creates a third authorization path.

Reserve the count once per logical attempt immediately before its first
dispatch. When that launch is confirmed to have failed before mutation, the
single logged transport retry permitted above reuses the same reservation and
does not increment, decrement or reset it. Unknown launch status dispatches no
replacement. Any proof lacking the allowed result, the exact class, the
explicit evidence-bootstrap request or, for `FAIL`, the retained non-zero
attempt never enters the bootstrap route. Implementation or plan failures never
enter either route, and no launch, test, build or migration retry borrows this
verification budget. Never use force takeover as fallback: a dead worker's
expired claim is transferred as in section 1, and a live one is waited on. On
resume, reconcile the unknown attempt from live state before any new action.

## 10. Report

At the requested target, report every roster ticket exactly once in one of four
lists: **cleared** (closed out), **at target** (parked at the requested point),
**parked** (operator-only question, quoted with ticket id and recommendation),
or **skipped** (blocked, taken, or failed with the exact reason). A partial
roster is not a successful report. Finish the run record as `completed`,
`paused`, `blocked`, or `aborted` only when the corresponding predicate is
true, preserving the final ledger and report in immutable history.

## 11. Phase boundaries

Preparation uses each ticket's resolved gates, not a universal document list.
Execution uses its packet, ticket worktree, checklist and no-merge boundary.
Review uses current PR-head evidence and independence. Verification begins only
after a confirmed merge and exact merge SHA. Done requires live proof and
questions gates. Every stage move uses Kanmer; auto never edits board files or
bypasses MCP.

---

**No successor — this skill is the hand-off.** It drives the phase skills
in order for each roster ticket, stopping each at the requested target or an
operator-only question. When the roster is exhausted, control returns to the
operator with the four-list report above. The controller never merges its own
PR and never starts another ticket from a worker context.
