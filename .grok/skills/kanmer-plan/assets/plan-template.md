# Plan — <ticket id>: <title>

*The plan. Not the checklist — reasoning establishes bounded work; the checklist distils it into independently observable actions.*

## Objective
One bounded outcome.
## Starting state
Verified current behaviour, source paths, components, and constraints.
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
