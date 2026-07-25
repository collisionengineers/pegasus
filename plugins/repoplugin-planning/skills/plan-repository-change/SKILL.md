---
name: plan-repository-change
description: Coordinate a material repository change from user interview through evidence, independent review, open-question resolution, and a final implementation-ready plan pack. Use for cross-cutting features, architecture, workflow, integration, documentation, or other changes whose scope or decisions need to be established before implementation.
---

# Plan Repository Change

This entry point works in either Codex Default or Plan mode; do not require the user to switch modes.

Create or resolve one shared task with `$repoplugin-task-contracts:resolve-repository-task`; use its task ID throughout. Write planning artifacts beneath `.repoplugin/tasks/<task-id>/planning/` through `$repoplugin-task-contracts:persist-repository-task-artifact`. Do not reconstruct a task from chat or silently create a second one.

## Establish the baseline

Interview the user one material question at a time when the answer could change scope, architecture, UX, safety, or acceptance. Record:

- `brief.md` — stated outcome, constraints, authority, and proof;
- `baseline.md` — known knowns, known unknowns, unknown knowns, and unknown unknowns;
- `blind-spots.md` — discovered risks, precedents, and knowledge gaps;
- `awareness.md` — assumptions, issues, limitations, and facts needing user attention.

Read the repository authorities and current owners before making claims. A contradiction, missing product decision, or irreversible choice belongs in `open-questions.md`; only the user resolves it.

## Research, options, draft, and review

Route bounded evidence work through `$repoplugin-planning:research-repository-change`. Use parallel explorers or researchers only for independent read-only questions, each with a distinct artifact target. Route the blind-spot pass and comparison through `$repoplugin-planning:explore-solution-options`, then persist `research/`, `brainstorm/options.md`, and `decisions.md` before one author writes `draft/implementation-plan.md` through `$repoplugin-planning:draft-implementation-plan`.

Use a fresh reviewer through `$repoplugin-planning:review-implementation-plan`. Persist `review/round-NNN.md` and canonical `open-questions.md`. Ask only material open questions, record initial interview answers in the question and decision records, incorporate them, and re-review affected work. Never silently patch established requirements or an accepted plan: a later user change starts `changes/RC-NNN.md`, identifies affected artifacts, and reopens only that scope.

## Generate and hand off

Run `$repoplugin-planning:generate-plan-pack` automatically only when every question, conflict, uncertainty, and open decision is incorporated and independently reviewed. The pack contains `implementation-plan.md`, `acceptance-checklist.md`, `evidence-and-commands.md`, `parallel-and-worktree-decision.md`, `deferred-capability-impact.md`, and `handoff.md`.

Every generated implementation plan must require its implementer to call the Codex harness `update_plan` tool before editing or delegating, and to keep it updated through execution. Say explicitly that `update_plan` is a Codex harness tool; Markdown checklists do not replace it. Planning agents do not call `update_plan` merely to write a plan.

The plan's acceptance checklist starts with that harness call and instructs the implementer to attach to the same task and maintain the implementation notes, decisions, deviations, and awareness Markdown required by `$repoplugin-implementation:implement-plan-pack`.

State exact CLI, API, or MCP commands, expected effect, authority, and research source when a plan requires them. Include the real caller, policy owner, tests, documentation, subagent partitioning, and whether parallel workers or worktrees are justified.
