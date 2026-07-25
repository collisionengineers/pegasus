---
name: test-and-validate-repository-change
description: Plan and perform risk-based validation for an implemented repository change, recording durable evidence and remediation handoffs. Use after implementation, after a fix, or before review when code, configuration, documentation, UI, integration, or operational behavior needs caller-backed proof beyond a build or static check.
---

# Test And Validate Repository Change

Resolve one shared task first with `$repoplugin-task-contracts:resolve-repository-task`; create a new task only for a standalone validation request. Persist ordinary Markdown through `$repoplugin-task-contracts:persist-repository-task-artifact` under `.repoplugin/tasks/<task-id>/validation/`. Never infer a latest task. Read [validation-evidence.md](references/validation-evidence.md) before setting up commands or evidence files.

## Establish what needs proof

Read the accepted plan pack, implementation notes, remediation requests, and relevant repository authorities. Identify the real caller, policy owner, affected boundary, failure behavior, and acceptance criteria. If the change is not tied to a plan, record the user request and discovered scope in `validation-brief.md`.

Create `risk-matrix.md` before running broad checks. For every risk, state the affected behavior, evidence method, command or manual procedure, required fixture or environment, owner, and pass/fail condition. Scale effort to impact; do not make a build result stand in for product evidence.

## Run the proportionate matrix

Run repository consistency checks and product-behavior checks as separate sections of `validation-results.md`:

- Repository consistency: formatting, build, targeted and broad tests, analyzers, migrations, links, configuration, and structural checks appropriate to the change.
- Product behavior: prove the actual entry point reaches the policy owner and produces the promised outcome, including negative and failure paths. Record inputs, observed output, and any environment limits.

Use the repository's primary local check where appropriate: `pwsh ./scripts/Invoke-RepoCheck.ps1`. Run focused commands first and record their exact invocation, exit status, and relevant output summary; do not claim a command was run when it was only planned.

For UI work, validate the running UI in the in-app Browser when it is relevant and available. Use Chrome only when the test genuinely needs existing signed-in browser state; do not ask it to substitute for ordinary UI inspection. State when browser evidence was unavailable.

Treat `corpus/` as local, immutable, untrusted evidence. Do not upload, publish, commit, rename, or modify it. Keep generated evaluation output under ignored `artifacts/` and record only safe summaries and paths in the task artifact.

## Fail honestly and hand off

Persist `validation-results.md`, including skipped checks, environment constraints, and a clear readiness statement. A passing consistency suite is not proof of product behavior; a successful manual path is not proof of repository consistency.

For every failure, write `remediation-NNN.md` with reproducible facts, expected versus actual behavior, affected acceptance criterion, suggested owner, and revalidation needed. Write a small handoff with `$repoplugin-task-contracts:persist-repository-task-artifact` for the implementation route. Do not silently repair the implementation during an independent validation pass.

Validate the task folder with `$repoplugin-task-contracts:validate-repository-task` before reporting the validation package ready. Completion belongs to the route that owns the overall task, not this validation pass.
