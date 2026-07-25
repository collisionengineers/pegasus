---
name: draft-implementation-plan
description: Write a single evidence-backed implementation-plan draft for a persisted repository planning task. Use only when explicitly routed after baseline, research, and option artifacts exist and before independent plan review.
---

# Draft Implementation Plan

Act as the sole synthesis author. Read the persisted brief, baseline, research, options, decisions, and live authorities. Write `draft/implementation-plan.md` with outcome-oriented slices: policy owner, real caller, data and adapter boundary, failure behavior, observability, tests, documentation, rollout/recovery, replacement paths, and deferred-capability impact.

For every required command, include the exact reprobed CLI/API/MCP command, expected effect, authority, and evidence. State a parallel-worker/worktree decision and a safe partition if applicable. Preserve uncertainty and assumptions in `open-questions.md` and `awareness.md`; do not silently decide them.

Do not independently review or accept the draft, generate a pack, implement, or call the Codex harness `update_plan` tool. That tool is reserved for the later implementer, not plan writing.
