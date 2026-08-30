---
name: kanmer-auto
description: Autonomously clear one explicit Kanmer group (epic or horizon), preserving a durable, resumable run record on that group while driving eligible tickets through their profile pipelines up to a requested point. Use when the user says "clear HZN-003", "work through 0.3.3", or "finish this epic". DO NOT USE FOR a single ticket or an ungrouped area — use the phase skills directly, or create/select a group first.
---

# Clearing a group autonomously

`kanmer-auto` is orchestration, not a new workflow. Each ticket still follows
the phase skills' procedures exactly as written; this skill selects the roster,
orders safe work, reconciles every result, and controls how many lanes run at
once. The controller never turns a worker message into board evidence.

## Orientation and durable-state resume

This skill runs one explicit existing group per invocation. Area-only and
ad-hoc selections have no durable batch owner: stop before mutation and ask the
operator to name or create an epic/horizon. Do not add MCP tools, ticket fields,
entities, leases, hidden local state, or automatic merging.

At startup, before any ticket write or dispatch, read `get_status`, `get_group`,
the group's `context.md`, and `automation/current.md`. A current record with
status `running`, `paused`, or `blocked` is resumed and reconciled; it is not
silently replaced. Validate its schema, group, project fingerprint, controller
ownership, and referenced history path. A different controller owning a
`running` record is a stop predicate.

Durable state belongs in the group's documents, never in a ticket:

- Current-run pointer: `automation/current.md`
- Immutable run history: `automation/runs/<run-id>.md`

For a new run, create a path-safe unique UTC id (adding a numeric suffix on
collision), fill `assets/run-state-template.md`, and keep its required
frontmatter (`kind`, `schema`, `run_id`, `group`, `project_fingerprint`,
`controller`, `status`, `created_at`, `updated_at`, `lane_limit`,
`stop_reason`) and headings (Selection contract, Run invariants, Ticket ledger,
Event log, Resume instruction). Run status is exactly `running`, `paused`,
`blocked`, `completed`, or `aborted`; ticket dispositions are exactly `queued`,
`active`, `waiting`, `blocked`, `finished`, or `skipped`.

Write and read it back: the complete history record before writing
`automation/current.md`; write and read back the pointer before dispatch. Never
overwrite an old history record. Update and read back both documents around
every assignment, worker result, reconciliation, wait, pause, block,
completion, or abort. Store operational state only: roster, target, lane
partition, skip reasons, worker outcomes, and concise operator answers. Never
store secrets, full prompts, or large command output.

## 1. Roster and gates-first readiness

1. Call `get_status`, then `list_items group: "<explicit group>"`; use the
   group's order and show the resolved roster, target point, and exclusions to
   the operator before starting. `list_items`, not `get_group`, supplies the
   taken, blocked, and profile fields needed for selection.
2. Read the group's shared context. Drop archived or blocked tickets and
   tickets taken by another actor; coordinate rather than using `force`.
3. Parse the requested target: “up to review” stops each ticket after its PR is
   open and its ticket is in Review; the default is closeout, subject to the
   human merge boundary. Resolve stage names with `list_board`.
4. For every retained ticket, call `get_doc_gates` and use its current stage,
   reachable stages, and first unmet next-boundary requirement as the routing
   table. Do not restate profile-to-document mappings in this skill.
5. Advance one gated boundary per move. Set `docs_todo` only when a governing
   document genuinely needs to be written; do not create optional documents to
   normalize the roster. A ticket with no currently required preparation phase
   routes to its next applicable action.
6. A user-only question at any phase parks that ticket as `waiting`, quotes the
   question and recommendation in the event log, and pauses that lane. Never
   guess an operator answer.

## 2. Lane assignment

Compare every retained ticket's `files` document. Disjoint file sets may use
different lanes; overlapping files share one serial lane; a `blocks` edge orders
the blocker before its dependent regardless of file disjointness. Cap parallel
work at approximately three lanes.

Before assigning a ticket, re-read its item, links/dependencies, taken state,
required document versions, activity, and `get_doc_gates`. Record the lane as
`active` with worker, branch/worktree when known, attempt, timestamp, action,
and stop condition; append a `lane-assigned` event; write/read back the full
run record; only then dispatch.

Each lane uses its own `.worktrees/<id>` worktree and branch. No lane may touch
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
successor.

On every result or timeout, the controller:

1. stops conflicting dispatch while the result is uncertain;
2. re-reads the live item, links/dependencies, documents and versions, activity,
   Git/PR state where applicable, and `get_doc_gates`;
3. compares actual mutations, stage, gate, checklist, branch/worktree, commit,
   PR and error evidence with the approved scope;
4. records the worker result, reconciliation, discrepancy, and one next action
   in the ledger/event log; and
5. writes and reads back the run record before selecting another action.

After anything merges to `main`, lanes still in flight rebase before opening a
PR (`git fetch origin && git rebase origin/main`). A failed ticket does not
silently disappear: record the exact failure, release it only under the phase
skill's rules, return it to the appropriate stage, and classify it in the run.

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
8. a ticket occupied by another actor;
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
build, or verification commands. Never use force takeover as fallback. On
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
