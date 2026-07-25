---
name: debug-repository-failure
description: Reproduce and diagnose a repository failure using observed facts, explicit hypotheses, and a durable root-cause handoff. Use for failing tests, runtime errors, broken UI paths, integration failures, regressions, or unexplained behavior before an implementation route changes code.
---

# Debug Repository Failure

Resolve one shared task with `$repoplugin-task-contracts:resolve-repository-task`; a standalone failure may create its own task. Persist ordinary Markdown through `$repoplugin-task-contracts:persist-repository-task-artifact` under `.repoplugin/tasks/<task-id>/debugging/`. Attach to an existing task only with an explicit task ID or handoff. Read [diagnostic-records.md](references/diagnostic-records.md) before recording evidence.

## Reproduce before explaining

Create `failure-brief.md` with the reported symptom, expected behavior, impact, environment, reported caller, and exact reproduction attempt. Start with the smallest safe reproduction. Preserve logs, commands, inputs, timestamps, and observed output needed for another agent to repeat it. If it does not reproduce, record that fact and the differences rather than inventing a cause.

Do not edit the suspected implementation while diagnosing. A request to fix follows the implementation route after diagnosis; an urgent safe mitigation still needs explicit authority and a recorded reason.

## Separate evidence from explanation

Write `facts.md` for direct observations and `hypotheses.md` for candidate causes. Number each hypothesis; state supporting evidence, evidence against, the next cheapest discriminating check, and status. Change status only after running the stated check. Treat a stack trace, text match, or registration as evidence—not proof that the real caller or policy owner is responsible.

Trace the actual entry point through the owner, boundaries, configuration, persistence, and tests. Use focused read-only inspection and tests first. Separate a repository consistency failure from a product-behavior failure. Preserve local corpus boundaries: never upload, publish, commit, rename, or modify `corpus/`; use safe local summaries only.

For UI failures, use the in-app Browser when visual or route inspection is relevant. Use Chrome only when a necessary existing signed-in state cannot be reproduced otherwise. Record the browser limitation rather than treating a screenshot as a complete diagnosis.

## Conclude or return remediation

When evidence supports one cause, write `root-cause-report.md`: symptom, reproduction, facts, rejected hypotheses, root cause, scope, affected caller/policy owner, and a proposed remedy with tests and risks. If evidence remains ambiguous, write `diagnostic-handoff.md` with the unresolved hypotheses and the next discriminating action; do not label a guess as root cause.

For an approved remedy, write `remediation-NNN.md` and a small implementation handoff using `$repoplugin-task-contracts:persist-repository-task-artifact`. The implementation owner must decide and make the code change; after a fix, route back to `$repoplugin-validation:test-and-validate-repository-change` for independent revalidation. Validate the task folder with `$repoplugin-task-contracts:validate-repository-task` before reporting a diagnostic package ready.
