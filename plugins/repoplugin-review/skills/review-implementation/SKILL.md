---
name: review-implementation
description: Independently assess a repository implementation against its validated Repoplugin plan, task artifacts, callers, tests, documentation, and scope. Use after implementation or remediation when a read-only review and a concise remediation handoff are needed.
---

# Review implementation

Review independently and read-only by default. Resolve the explicit shared task and validated plan with `$repoplugin-task-contracts:resolve-repository-task`; do not infer a task from the latest folder. Do not edit implementation, state, or product documentation as part of review.

## Review workflow

1. Read repository guidance, validated plan, plan review, task implementation notes, decisions, deviations, awareness, and prior remediation.
2. Compare the plan to the real diff and changed files. Confirm each requirement, exclusion, caller, policy owner, persisted/configuration change, failure path, observability claim, test, documentation change, and deferred-capability impact.
3. Trace the actual entry point rather than treating registrations or uncalled code as completion. Run or inspect relevant local checks as evidence permits. State whether evidence is runtime, static, unavailable, or unrun.
4. Check scope creep, unrelated dirty-work impact, assumptions, deviations, contradictions, unfinished user questions, and regressions.
5. Write a concise Markdown remediation handoff in `<task>/review/` through `$repoplugin-task-contracts:persist-repository-task-artifact`. Include task ID, plan handoff, severity, exact evidence, affected paths/owner, required outcome, and re-check. Mark `no findings` explicitly when appropriate.
6. Return the handoff to the implementation owner. The reviewer does not silently fix findings; implementation routes bounded fixes and requests a new review.

## Review result

Separate blockers, required remediation, advisory observations, evidence limits, and user decisions. A plan match alone is not proof of execution; a passing static check alone is not proof of a real caller.
