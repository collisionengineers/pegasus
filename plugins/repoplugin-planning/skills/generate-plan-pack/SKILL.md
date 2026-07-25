---
name: generate-plan-pack
description: Generate the final implementation-ready plan pack for a reviewed repository planning task. Use only as plan-repository-change's automatic final transition after open questions, conflicts, uncertainty, and decisions are incorporated and independently reviewed.
---

# Generate Plan Pack

Generate automatically only after the review records no blocking findings or open questions. Refuse to create or overwrite a pack when evidence is stale, user answers have not been reviewed, or the accepted scope changed. A later requirement becomes `changes/RC-NNN.md` and reopens only affected artifacts.

Create final Markdown under `.repoplugin/tasks/<task-id>/planning/plan-pack/`:

- `implementation-plan.md` — ordered changes, caller, policy owner, data/adapters, failure behavior, observability, tests, docs, rollout/recovery, and deferred impact;
- `acceptance-checklist.md` — independently checkable completion criteria;
- `evidence-and-commands.md` — exact researched commands, effects, authority, and validation;
- `parallel-and-worktree-decision.md` — justification, partitioning, or a reason not to parallelize;
- `deferred-capability-impact.md` and `handoff.md`.

The implementation plan must explicitly require the implementer to call the Codex harness `update_plan` tool before any edit or delegation and maintain it through execution. It must state that `update_plan` is a harness tool, and that plan Markdown/checklists are not a substitute. Do not tell planning agents to use it just for creating this pack.

Make that harness call the first item in `acceptance-checklist.md`. The plan must also tell the implementer to attach to this same task and maintain `implementation/notes.md`, `implementation/decisions.md`, `implementation/deviations.md`, and `implementation/awareness.md` through `$repoplugin-task-contracts:persist-repository-task-artifact`; these durable records do not replace the harness plan.
