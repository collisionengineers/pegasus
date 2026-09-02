---
kind: auto-current
schema: 1
run_id: 20260901T215000Z-claude-controller
run_path: automation/runs/20260901T215000Z-claude-controller.md
group: EPIC-011
project_fingerprint: kanmer-proj-v1:37ebffe6d69ce76e2373ed932409501bc9980c1171d272d7908873f6ada150ec
project_id: b40b93fc-17b8-46f6-b7e1-db4d8977dea6
controller: claude-code/fable-5.1@PGUSER
status: running
updated_at: 2026-09-02T01:05:00Z
---

# Current auto run — EPIC-011

## Resume instruction

Open `automation/runs/20260901T215000Z-claude-controller.md`, then re-read the group
context, the live roster, and `get_doc_gates` for every ticket before dispatching. This
pointer is written only after its complete run record has been written and read back.
The Claude Code runbook is `pegasus-work-pack/orchestration/claude/orchestration-plan.md`
(restart protocol in §6); the guard hooks must be active (canary: a Bash `git stash
list` is denied) before any worker is dispatched. Resumed 2026-09-02 in session a179cc54 (guard active). When the
Kanmer MCP connection is unavailable, use `pegasus-work-pack/orchestration/claude/tools/kanmer-call.sh`
for every board read and write; the run record names the in-flight lanes.
