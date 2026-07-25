---
name: implement-plan-pack
description: Execute an explicitly validated Repoplugin repository plan pack. Use when the user asks to implement an approved plan, apply a remediation handoff, or take an authorized delivery step through local verification and optional Git/PR preparation.
---

# Implement plan pack

Implement one validated plan, not a new planning process. Use `$repoplugin-task-contracts:resolve-repository-task` to resolve an explicit task ID and validated-plan handoff before writing under the task area. If either is absent, refuse and route to `$repoplugin-planning:plan-repository-change`.

## Start and coordinate

1. Read repository and nearest applicable agent guidance, then the validated plan and its task state. Preserve unrelated dirty work.
2. Before **any** edit, branch or worktree action, configuration mutation, or implementation-worker delegation, call the actual Codex harness `update_plan` tool. This is a harness tool, not a Markdown file: use exactly one in-progress item and keep the plan current as work completes.
3. Identify the real caller, policy owner, evidence path, affected persistence/configuration, failure behavior, tests, documentation, and relevant deferrals before implementation. Search for current owners and callers before adding an alternative.
4. Delegate only independent, bounded, non-overlapping work. Retain one implementation owner. Use a worktree only when its isolation benefit is justified and the user has explicitly authorized creating it.

## Work in the shared task area

Maintain concise Markdown under `<task>/implementation/`:

- `notes.md` — progress and caller evidence;
- `decisions.md` — material choices and rationale;
- `deviations.md` — plan deviations, cause, impact, and disposition;
- `awareness.md` — assumptions, risks, blockers, and user-visible implications.

Use `$repoplugin-task-contracts:persist-repository-task-artifact` to create or update these artifacts. Do not invent journals, a second task state machine, or a duplicate task folder.

## Implement and prove

1. Execute plan steps in dependency order. Keep boundaries thin and extend the existing policy owner.
2. Prove behavior through the real caller, then run the plan's focused checks and applicable repository checks. Record commands, outcomes, omissions, and limitations in `notes.md`.
3. If a requirement changes, pause all affected work, record it in `awareness.md`, and return it to the planning requirement-change/open-question workflow. Continue only work demonstrated independent of the change.
4. Accept remediation Markdown from review, validation, or debugging. Confirm its task and plan identity, route a bounded fix to the original owner where possible, record the result, and request re-review. Do not silently reinterpret scope.

## Git, PR, and external boundaries

Use the exact templates in [Git and GitHub handoff](references/git-and-github-handoff.md) only after re-probing state. Stage literal approved paths; never use `git add -A`. Commits, push, PR creation, external comments/replies, and worktree removal each require separate user authorization. When authorized, use narrow factual commits and attach local validation evidence.

## Completion

Return the task ID, plan handoff, callers proved, changed paths, checks run, remediation status, unrun checks, and authorization-gated actions. Mark the task complete through `$repoplugin-task-contracts:validate-repository-task` only after the validated scope and required reviews are complete.
