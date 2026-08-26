---
kind: auto-run
schema: 1
run_id: <UTC timestamp and controller slug>
group: <EPIC-000 or HZN-000>
project_fingerprint: <get_status project identity>
controller: <controller slug>
status: running
created_at: <ISO-8601 UTC timestamp>
updated_at: <ISO-8601 UTC timestamp>
lane_limit: 3
stop_reason:
---

# Auto run — <run_id>

## Selection contract

- Group: `<group>`
- Target point: `<stage or closeout>`
- Included tickets: `<ordered IDs>`
- Lane partition: `<lane → ordered IDs>`
- Skipped tickets and reasons: `<IDs and reasons>`
- Project fingerprint: `<project_fingerprint>`

## Run invariants

- The controller is `<controller>` and the maximum concurrent lanes are `<lane_limit>`.
- This run uses only the existing Kanmer tools and phase skills.
- The controller never auto-merges a pull request.

## Ticket ledger

| Order | Ticket | Observed stage | Gates / next action | Disposition | Worker | Branch / worktree | Attempt | Last action | Last result | PR | Updated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |

## Event log

- `<timestamp>` — run created; roster and live gates recorded.

## Resume instruction

Re-read this record, the group context, current live ticket state, and each ticket's live gates before dispatching any new action. Reconcile the ledger; do not repeat a completed action solely because this run was interrupted.
