---
kind: auto-run
schema: 3
run_id: <UTC timestamp and controller slug>
group: <EPIC-000 or HZN-000 — the run host group whose automation/ owns this record>
scope: group
scope_selector: <the frozen selection: ticket id, group id, area id, explicit id list, or board>
authority: <what the operator granted this run, and what it must still ask for>
delivery_target: <resolved PR target branch / verification target branch>
project_fingerprint: <get_status project identity>
controller: <controller slug>
status: running
created_at: <ISO-8601 UTC timestamp>
updated_at: <ISO-8601 UTC timestamp>
lane_limit: 3
transient_retry_limit: 2
stop_reason:
---

# Auto run — <run_id>

## Selection contract

- Group: `<group>` (run host group — its membership is not the roster)
- Scope: `<scope>` / selector `<scope_selector>`
- Target point: `<stage or closeout>`
- Included tickets: `<ordered IDs>` — **frozen at `<ISO-8601 UTC timestamp>`**; nothing joins later
- Lane partition: `<lane → ordered IDs>`
- Skipped tickets and reasons: `<IDs and reasons>`
- Project fingerprint: `<project_fingerprint>`

## Run invariants

- The controller is `<controller>` and the maximum concurrent lanes are `<lane_limit>`.
- This run uses only the existing Kanmer tools and phase skills.
- The controller never auto-merges a pull request; it dispatches the independent reviewer that holds the merge point.
- The roster is frozen. A ticket created after the freeze, and any quick capture, is out of this run.
- The PR target and the verification target come from the recorded `delivery_target`, never from a hardcoded branch name.
- `transient` re-runs are bounded by `transient_retry_limit` per ticket; a lane that would exceed it blocks with the exact refusal instead of re-running.

## Ticket ledger

Disposition is exactly one of `queued`, `active`, `waiting`, `blocked`,
`target-reached`, `finished`, or `skipped`; `target-reached` is terminal and
records the exact target evidence that established it.

| Order | Ticket | Observed stage | Gates / next action | Disposition | Worker | Branch / worktree | Attempt | Transient | Replan | Last action | Last result | PR | Updated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

## Event log

- `<timestamp>` — run created; roster and live gates recorded.

## Resume instruction

Re-read this record, the group context, current live ticket state, and each ticket's live gates before dispatching any new action. Reconcile the ledger; do not repeat a completed action solely because this run was interrupted. Re-resolve nothing about the roster: it was frozen in the Selection contract above. Confirm the board branch is pushed before treating any gate result as current.
