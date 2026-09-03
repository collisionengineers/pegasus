# Plan — <ticket id>: <title>

*The plan. Not the checklist — reasoning establishes bounded work; the checklist distils it into independently observable actions.*

## Objective
One bounded outcome.
## Starting state
Verified current behaviour, source paths, components, and constraints.
Pin the evidence versions this plan was written against on one line, so a later worker can tell whether the plan went stale — for example: Evidence: `research/research.md`@`3f2b1c…`, `files/files.md`@`9c01ab…`. `get_ticket_doc` returns each version.
## Governing docs
For each linked PRD/FRD/ADR: **Meets** the requirement, **Modifies** only with explicit authorization, or records a **New ADR**. Review checks this against the diff.
## Required changes
Exact behaviour and contract changes.

> **Advisory:** `investigate`, `decide`, `choose`, and `determine` usually mean planner work remains. Resolve it before dispatch or use a spike. This is not a gate or regex score.

## Expected files
| Action | Repo-root-relative path | Responsibility |
|---|---|---|
| Modify/Add/Inspect | `path/to/file` | Include generated-artifact status. |
## Do not modify
Protected surfaces and forbidden scope.
## Constraints
Only applicable compatibility, dependency, path, security/data, performance, and architectural constraints.
## Ordered steps
1. Input, target path/symbol, expected result, and ordering dependency.

For work a constrained worker will execute one step at a time, write each step as its own `### Step N — <title>` sub-section here instead of a bare list item. That is the form `get_execution_packet id: <ID>, step: <n>` compiles into a bounded packet; a bare list item stays readable but cannot be compiled. Labelled bullets, one per line:

### Step N — <title>
- Preconditions: what must already be true.
- Files: only the paths this step may touch — each must also appear in Expected files, and none may appear in Do not modify.
- Symbols: the exact functions, types, or exports involved.
- Change: the exact change.
- Preserved behaviour: what must still hold afterwards.
- Forbidden: behaviour this step must not introduce.
- Negative cases: what must fail, and how.
- Tests: the test files that prove it.
- Commands: the exact commands to run.
- Expected output: what a passing run looks like.
- Done when: the observable done condition.
- Deviation stop: what makes the worker stop and report instead of improvising.
## Acceptance checks
- When applicable, name the production caller, registration, route, or composition entry.
- When applicable, prove runtime dependencies ship in the packaged/deployed artifact.
- When applicable, schema changes include migration, grants/bootstrap census, runtime-role permission, and rollback/data-loss handling in the same diff.
- Tests prove the claim without weakened assertions; retain exact commands and exit evidence.
## Commands
Focused checks, full repository rail, and post-merge/environment checks with cwd/environment.
## Failure and deviation rules
Stop and report failing checks, unknown APIs/files, scope expansion, dependency additions, governing conflicts, or unsafe commands; deviations are not silent redesigns.
## Stop condition
State the final boundary. Do not merge or start another ticket unless the approved skill phase explicitly owns it.
