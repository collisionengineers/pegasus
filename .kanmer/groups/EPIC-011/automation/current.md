---
kind: auto-current
schema: 1
run_id: 20260901T215000Z-claude-controller
run_path: automation/runs/20260901T215000Z-claude-controller.md
group: EPIC-011
project_fingerprint: kanmer-proj-v1:65ac6d3b3a807ee23c64e34dae763abaa4e3978566f2ec3ba2acec76734884a0
controller: claude-code/fable-5.1@PGUSER
status: running
updated_at: 2026-09-01T22:05:00Z
---

# Current auto run — EPIC-011

## Resume instruction

Open `automation/runs/20260901T215000Z-claude-controller.md`, then re-read the group
context, the live roster, and `get_doc_gates` for every ticket before dispatching. This
pointer is written only after its complete run record has been written and read back.
The Claude Code runbook is `pegasus-work-pack/orchestration/claude/orchestration-plan.md`
(restart protocol in §6); the guard hooks must be active (canary: a Bash `git stash
list` is denied) before any worker is dispatched.
